# Step 837 - M00-013 Reference Provenance and Permission

## Summary

Recorded actionable-reference upstream, revision, license, permission, and
reuse boundaries.

## Why

Direct source reuse needs durable provenance without committing a private chat
or broadening permission to third-party material.

## Files changed

- `docs/license-audit.md`
- `docs/reference-map.md`
- `docs/fork-diff.md`
- `docs/issue-labels.md`

## Tests run

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File .\scripts\check-naming.ps1
```

## Evidence

The sanitized attestation records Daniil Vaino's SharpOS owner grant, the
attachment byte count and SHA-256, current public CC0 corroboration, intended
CLROS scope, and exclusions. The private transcript is not committed.

## Failures

The supplied archives lack `.git` metadata and top-level license files, so
local-snapshot revision identity cannot be fabricated.

## Deferred work

Vendor files, GPL ManagedDoom, WAD/media, and the absent CoreCLR fork require
their own provenance and terms.

## Evidence boundary

This establishes authorization for SharpOS-authored source adaptation; it does
not establish ownership of third-party dependencies or CLROS correctness.
