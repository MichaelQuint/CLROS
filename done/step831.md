# Step 831 - M00-007 Build and Baseline Script Identity

## Summary

Updated build, baseline, naming, and CI scripts to require `CLROS.slnx`.

## Why

Every entry point must agree on the canonical repository sentinel.

## Files changed

- `scripts/check-naming.ps1`
- `scripts/run-baseline-evidence.ps1`
- `scripts/run-ci-smoke.ps1`
- `tests/OS.HostTests/CiSmokeScriptTests.cs`
- `docs/host-requirements.md`
- `docs/completion-gates.md`

## Tests run

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File .\scripts\check-naming.ps1
powershell -NoProfile -ExecutionPolicy Bypass -File .\scripts\run-ci-smoke.ps1 -DryRun
powershell -NoProfile -ExecutionPolicy Bypass -File .\scripts\run-baseline-evidence.ps1 -PlanOnly
```

## Evidence

All scripts name `CLROS.slnx`, fail if it is missing, and retain `OS.*` as the
technical assembly/namespace identity.

## Failures

The former naming script could skip the absent legacy solution; negative tests
now prevent that false green.

## Deferred work

Actual compatible-runner CI execution remains a separate evidence boundary.

## Evidence boundary

This proves script intent and local execution, not remote CI service execution.
