# Step 834 - M00-010 Reference Workspace Rules

## Summary

Restored `references/AGENTS.override.md` with read-mostly, provenance, and build
isolation rules.

## Why

Reference snapshots are usable engineering inputs but must not contaminate the
first-party build or silently become CLROS proof.

## Files changed

- `references/AGENTS.override.md`
- `docs/reference-map.md`

## Tests run

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File .\scripts\check-naming.ps1
```

## Evidence

The override permits study and authorized adaptation while requiring
file-level provenance and excluding reference projects from first-party builds.

## Failures

The previous override was missing.

## Deferred work

Each future adaptation must record its exact source boundary.

## Evidence boundary

This proves reference-handling policy, not correctness of adapted code.
