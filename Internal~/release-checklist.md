# Release Checklist

Internal runbook for cutting a new `com.sorolla.sdk` release. Captures the flow inferred from real release history (`chore(release): 3.14.x`) + the stall-free URL policy (no `#vX.Y.Z` pin in docs).

---

## Pre-release

Fix/feature commits land first, separate from the release commit. A single `fix(...)`, `feat(...)`, or `refactor(...)` commit per logical change — never bundled with the version bump.

- Conventional-commit subject: `fix(area): what`, `feat(area): what`, `refactor(area)!: what` (bang for breaking).
- Stage explicit filenames (`.cs` + `.cs.meta` pairs). Never `git add -A` / `git add .`.
- Push fix commits as you go; the release commit can batch several fixes.

---

## Release commit

One commit, one version bump, two files touched.

| File | Change |
|------|--------|
| `package.json` | `"version": "X.Y.Z"` |
| `CHANGELOG.md` | New `## [X.Y.Z] - YYYY-MM-DD` section at top |

Install URLs in `README.md` and `Documentation~/quick-start.md` are **unpinned by policy** (bare `...sorolla-palette.git`). Do not re-pin per release — a pinned URL drifts and ships stale onboarding.

Locally, before pushing, run:

```bash
./Tools~/build-docs.sh
```

GH Actions regenerates `Documentation~/api-reference.md` on push, but running locally catches docfx errors before they're a red CI badge on the release tag. Do not commit the regenerated file — CI owns it.

Commit:

```bash
git add package.json CHANGELOG.md
git commit -m "chore(release): X.Y.Z"
```

---

## Tag + push + GitHub release

```bash
git tag -a vX.Y.Z -m "vX.Y.Z - Short title"
git push origin master --tags
gh release create vX.Y.Z --title "vX.Y.Z - Short title" --notes-file <(awk '/^## \[X.Y.Z\]/,/^## \[/' CHANGELOG.md | head -n -1)
```

Short title convention (from history): the one-line theme of the fix. Examples:
- `v3.14.3 - Interstitial onFailed callback, stop mis-reporting failure as success`
- `v3.13.0 - Unity IAP v5 migration + revenue-integrity fixes`
- `v3.12.1 - expose MAX mediation + creative debuggers`

`--notes-file` trick: pipe the matching CHANGELOG section into `gh release create` so the release body mirrors the changelog without manual copy-paste.

---

## Verify

- `gh release list --limit 3` — new tag appears at top, marked Latest.
- GitHub Actions run is green (docfx + any other CI).
- Optional: fresh Unity project, `Add package from git URL` with the bare URL, confirm it resolves to the new version (`package.json` in `Library/PackageCache/` shows `X.Y.Z`).

---

## Never

- Re-pin the install URLs per release (policy: bare URL, zero maintenance).
- Amend a published release commit or tag.
- Force-push `master` or a tag.
- Bundle the version bump with fix commits (keep `chore(release): X.Y.Z` isolated).
- Bump version without adding a CHANGELOG entry dated today.
