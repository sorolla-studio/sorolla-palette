#!/usr/bin/env bash
#
# build-docs.sh - regenerate Documentation~/api-reference.md from XML doc
# comments in Runtime/*.cs via DocFX + DocFxMarkdownGen.
#
# Requirements:
#   - dotnet SDK (8.0+ runtime accessible via DOTNET_ROLL_FORWARD)
#   - docfx global tool: dotnet tool install -g docfx
#   - DocFxMarkdownGen (dfmg) global tool: dotnet tool install -g DocFxMarkdownGen
#   - UNITY_PATH env var, e.g. /Applications/Unity/Hub/Editor/6000.4.0f1
#
# Usage:
#   UNITY_PATH=/Applications/Unity/Hub/Editor/6000.4.0f1 bash Tools~/build-docs.sh
#
set -euo pipefail

: "${UNITY_PATH:?UNITY_PATH must be set, e.g. /Applications/Unity/Hub/Editor/6000.4.0f1}"

# Resolve package root (parent of Tools~/)
PKG_ROOT="$(cd "$(dirname "$0")/.." && pwd)"
DOCFX_DIR="$PKG_ROOT/Documentation~/docfx"
OUTPUT="$PKG_ROOT/Documentation~/api-reference.md"

# Ensure dotnet tools are on PATH
export PATH="$PATH:$HOME/.dotnet/tools"

# DocFxMarkdownGen targets .NET 8; allow running on newer runtimes
export DOTNET_ROLL_FORWARD=LatestMajor

command -v docfx >/dev/null 2>&1 || { echo "error: docfx not installed. Run: dotnet tool install -g docfx"; exit 1; }
command -v dfmg  >/dev/null 2>&1 || { echo "error: dfmg not installed. Run: dotnet tool install -g DocFxMarkdownGen"; exit 1; }

cd "$DOCFX_DIR"

# Clean previous outputs so stale files never leak into the concatenated result
rm -rf api md bin obj

echo "→ Building shadow csproj against Unity DLLs..."
dotnet build sdk.csproj -c Release --nologo -v minimal

echo "→ Extracting DocFX metadata..."
docfx metadata docfx.json --logLevel warning

echo "→ Converting YAML → markdown..."
dfmg >/dev/null

echo "→ Injecting <example> blocks (dfmg drops them)..."
python3 "$PKG_ROOT/Tools~/inject-examples.py" "$DOCFX_DIR/api" "$DOCFX_DIR/md/Sorolla.Palette"

# Concat per-type markdown into a single api-reference.md.
# Order: main facade class first, then config, then enums.
TYPES_DIR="$DOCFX_DIR/md/Sorolla.Palette"
if [ ! -d "$TYPES_DIR" ]; then
  echo "error: expected generated markdown at $TYPES_DIR — dfmg output structure changed?"
  exit 1
fi

ORDER=(
  "Palette.md"
  "SorollaConfig.md"
  "PlatformAdUnitId.md"
  "ProgressionStatus.md"
  "ResourceFlowType.md"
)

# Verify every expected file exists
for f in "${ORDER[@]}"; do
  if [ ! -f "$TYPES_DIR/$f" ]; then
    echo "error: missing generated file $TYPES_DIR/$f"
    exit 1
  fi
done

# Build the concatenated file with a DO NOT EDIT banner.
# Each section is stripped of its YAML frontmatter (between leading --- markers)
# and separated by a horizontal rule.
TMP="$(mktemp)"
{
  printf '<!-- DO NOT EDIT. This file is generated from XML doc comments in Runtime/*.cs.\n'
  printf '     To regenerate: UNITY_PATH=/path/to/Unity/Editor bash Tools~/build-docs.sh\n'
  printf '     Source of truth: `///` XML comments on public members in Runtime/Palette.cs,\n'
  printf '     Runtime/SorollaConfig.cs, etc. CI enforces staleness via .github/workflows/docs-check.yml. -->\n\n'
  printf '# Sorolla SDK API Reference\n\n'
  printf 'Complete reference for the public `Sorolla.Palette` namespace. For task-oriented\n'
  printf 'guides and examples, see `Documentation~/guides/` and `Documentation~/quick-start.md`.\n\n'
  printf -- '---\n\n'

  for f in "${ORDER[@]}"; do
    # 1. Strip YAML frontmatter (between leading --- markers).
    # 2. Demote all headings by one level so the top "# Sorolla SDK API Reference" stays sole H1.
    # 3. Rewrite dfmg's broken per-file cross-type links to simple inline-code type names.
    #    Pattern: [Sorolla.Palette.Foo](../Sorolla.Palette/Foo) -> `Foo`
    awk '
      BEGIN { in_fm=0 }
      NR==1 && /^---$/ { in_fm=1; next }
      in_fm && /^---$/ { in_fm=0; next }
      in_fm { next }
      /^#{1,5} / { sub(/^#/, "##"); print; next }
      { print }
    ' "$TYPES_DIR/$f" | sed -E \
        -e 's#\[[^]]*\.([A-Za-z0-9_]+(\([^)]*\))?)\]\(\.\./Sorolla\.Palette/[^)]*\)#`\1`#g' \
        -e 's#\[([A-Za-z0-9_]+)\]\(\.\./Sorolla\.Palette/[^)]*\)#`\1`#g' \
        -e 's#`Sorolla\.Palette\.(Palette\.)?([A-Za-z0-9_]+(\([^`]*\))?)`#`\2`#g'
    printf '\n---\n\n'
  done
} > "$TMP"

mv "$TMP" "$OUTPUT"

# Clean up intermediate artifacts (they're gitignored but still disk noise)
rm -rf "$DOCFX_DIR/api" "$DOCFX_DIR/md" "$DOCFX_DIR/bin" "$DOCFX_DIR/obj"

LINES=$(wc -l < "$OUTPUT" | tr -d ' ')
echo "✓ Regenerated Documentation~/api-reference.md ($LINES lines)"
