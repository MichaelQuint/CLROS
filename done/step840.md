# Step 840 - M00-016 Stable SDK Selection

## Summary

Added `global.json` for stable SDK feature band `10.0.300`, `latestPatch`
roll-forward, and prerelease exclusion.

## Why

Repository builds need a reproducible .NET 10 SDK selection rule.

## Files changed

- `global.json`
- `docs/host-requirements.md`

## Tests run

```powershell
dotnet --version
dotnet build .\CLROS.slnx --nologo -m:1
```

## Evidence

The current host selects SDK `10.0.302` under the pinned stable feature band.

## Failures

The required SDK was initially unavailable on this host and was installed
before final verification.

## Deferred work

ILCompiler remains separately pinned to `10.0.8`.

## Evidence boundary

This proves local SDK selection and build compatibility.
