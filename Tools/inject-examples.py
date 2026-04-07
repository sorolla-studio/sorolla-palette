#!/usr/bin/env python3
"""
inject-examples.py - inject <example> blocks from DocFX YAML metadata into
the markdown produced by DocFxMarkdownGen.

DocFxMarkdownGen ignores the `example:` field in DocFX YAML, dropping any
<example> XML doc comment content from the rendered output. This script
walks the YAML metadata, extracts non-empty example fields, and writes them
into the corresponding markdown files as fenced code blocks placed
immediately after each member's Declaration code block.

Usage:
    python3 Tools/inject-examples.py <yaml-dir> <markdown-dir>

Example:
    python3 Tools/inject-examples.py Documentation~/docfx/api Documentation~/docfx/md/Sorolla.Palette
"""

import re
import sys
from pathlib import Path


def parse_docfx_yaml(yaml_path):
    """
    Minimal line-based parser for DocFX metadata YAML. Returns a list of
    (member_name, [example_strings]) tuples for entries that have non-empty
    examples. Avoids needing PyYAML as a dependency.
    """
    members = []
    current_name = None
    in_example = False
    examples = []
    current_example_lines = []

    def flush_member():
        if current_name and examples:
            members.append((current_name, list(examples)))

    with open(yaml_path, "r", encoding="utf-8") as f:
        for raw in f:
            line = raw.rstrip("\n")

            # Top-level item boundary: "- uid: ..."
            if line.startswith("- uid:"):
                if in_example and current_example_lines:
                    examples.append("\n".join(current_example_lines).rstrip())
                    current_example_lines = []
                flush_member()
                current_name = None
                in_example = False
                examples = []
                continue

            # name: <something>  -- the dfmg-rendered short name we'll match on
            if current_name is None and line.startswith("  name: "):
                current_name = line[len("  name: "):].strip()
                continue

            # example: []  -> empty list, skip
            if line == "  example: []":
                in_example = False
                continue

            # example:   -> start of a non-empty list
            if line == "  example:":
                in_example = True
                current_example_lines = []
                continue

            if in_example:
                # End of example list = next top-level field at 2-space indent
                # that isn't a list item or continuation.
                if (line.startswith("  ") and not line.startswith("   ")
                        and not line.startswith("  - ")):
                    if current_example_lines:
                        examples.append("\n".join(current_example_lines).rstrip())
                        current_example_lines = []
                    in_example = False
                    continue

                # New list item: "  - foo" or "  - >-"
                if line.startswith("  - "):
                    if current_example_lines:
                        examples.append("\n".join(current_example_lines).rstrip())
                        current_example_lines = []
                    rest = line[len("  - "):]
                    if rest == ">-":
                        # Folded scalar, body follows on next lines
                        continue
                    current_example_lines.append(rest)
                    continue

                # Continuation line of a folded scalar (4-space indent)
                if line.startswith("    "):
                    current_example_lines.append(line[4:])
                    continue

                # Blank line inside a folded scalar
                if line.strip() == "":
                    current_example_lines.append("")
                    continue

        # EOF
        if in_example and current_example_lines:
            examples.append("\n".join(current_example_lines).rstrip())
        flush_member()

    return members


_PRE_CODE_RE = re.compile(
    r"<pre><code[^>]*>(.*?)</code></pre>", re.DOTALL
)


def _clean_example(raw):
    """Strip DocFX's <pre><code class="lang-csharp"> wrapper if present,
    decode common HTML entities, and collapse the spurious blank lines DocFX
    inserts when folding multi-line code into a YAML scalar."""
    m = _PRE_CODE_RE.search(raw)
    body = m.group(1) if m else raw
    body = (
        body
        .replace("&lt;", "<")
        .replace("&gt;", ">")
        .replace("&amp;", "&")
        .replace("&quot;", '"')
    )
    # DocFX's folded YAML scalar inserts blank lines around lines that start
    # with `{` etc. Collapse all blank lines inside the snippet — short
    # examples don't need internal whitespace.
    body = re.sub(r"\n\s*\n", "\n", body)
    return body.strip("\n").rstrip()


def _normalize_heading(name):
    """Normalize a member name for fuzzy matching against markdown headings.
    Strips whitespace, decodes HTML entities, removes spaces inside generic
    type arguments so 'Dictionary<string, object>' matches both encoded and
    spaced variants."""
    return (
        name
        .replace("&lt;", "<")
        .replace("&gt;", ">")
        .replace(" ", "")
    )


def inject_examples_into_markdown(md_path, members_with_examples):
    """
    For each (name, examples) entry, find the matching `#### name` heading
    in the markdown file and insert an "###### Example" block + fenced csharp
    code immediately after the next `csharp title="Declaration"` code block
    (or after the parameters table if one follows the declaration).
    """
    if not members_with_examples:
        return False

    text = md_path.read_text(encoding="utf-8")
    lines = text.split("\n")

    by_name = {
        _normalize_heading(name): [_clean_example(e) for e in examples]
        for name, examples in members_with_examples
    }
    out = []
    i = 0
    injected = 0

    while i < len(lines):
        line = lines[i]
        out.append(line)

        # Match a member heading in dfmg output: "### Foo(args)" or "### Foo".
        # build-docs.sh later demotes these to #### during concatenation.
        m = re.match(r"^### (.+)$", line)
        normalized = _normalize_heading(m.group(1)) if m else None
        if m and normalized in by_name:
            examples = by_name[normalized]

            # Walk forward to find end of declaration code block, then
            # inject the example block right after it (and before the
            # Parameters table, if any). Putting examples *before* the
            # parameter list reads better for short snippets.
            j = i + 1
            in_code = False

            while j < len(lines):
                cur = lines[j]
                out.append(cur)
                if cur.startswith("```csharp"):
                    in_code = True
                elif cur.startswith("```") and in_code:
                    in_code = False
                    out.append("")
                    out.append("###### Example")
                    for ex in examples:
                        out.append("")
                        out.append("```csharp")
                        out.append(ex)
                        out.append("```")
                    injected += 1
                    i = j + 1
                    break
                j += 1
            else:
                i = j
            continue

        i += 1

    if injected:
        md_path.write_text("\n".join(out), encoding="utf-8")
    return injected


def main():
    if len(sys.argv) != 3:
        print(f"usage: {sys.argv[0]} <yaml-dir> <markdown-dir>", file=sys.stderr)
        sys.exit(2)

    yaml_dir = Path(sys.argv[1])
    md_dir = Path(sys.argv[2])

    if not yaml_dir.is_dir():
        print(f"error: yaml dir not found: {yaml_dir}", file=sys.stderr)
        sys.exit(1)
    if not md_dir.is_dir():
        print(f"error: markdown dir not found: {md_dir}", file=sys.stderr)
        sys.exit(1)

    total_injected = 0
    for yml in sorted(yaml_dir.glob("Sorolla.Palette.*.yml")):
        if yml.name == "toc.yml":
            continue
        members = parse_docfx_yaml(yml)
        if not members:
            continue
        # The yaml file is named e.g. Sorolla.Palette.Palette.yml; the matching
        # markdown file is md/Sorolla.Palette/Palette.md
        type_name = yml.stem.split(".")[-1]
        md_path = md_dir / f"{type_name}.md"
        if not md_path.exists():
            continue
        injected = inject_examples_into_markdown(md_path, members)
        if injected:
            print(f"  injected {injected} example(s) into {md_path.name}")
            total_injected += injected

    print(f"✓ Total examples injected: {total_injected}")


if __name__ == "__main__":
    main()
