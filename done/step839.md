# Step 839 - M00-015 CI Smoke Workflow

## Summary

Restored `.gitea/workflows/ci-smoke.yml` to match tested local CI intent.

## Why

Documentation and host tests referenced a missing workflow.

## Files changed

- `.gitea/workflows/ci-smoke.yml`
- `tests/OS.HostTests/CiWorkflowTests.cs`
- `docs/completion-gates.md`

## Tests run

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File .\scripts\run-ci-smoke.ps1 -DryRun
dotnet run --project .\tests\OS.HostTests\OS.HostTests.csproj --no-build
```

## Evidence

Static workflow tests and the dry run verify build, host test, naming,
source-firewall, QEMU intent, diff checking, and artifact intent.

## Failures

The workflow file was absent.

## Deferred work

A compatible clean Windows runner must execute it before CI operation is
claimed.

## Evidence boundary

This proves workflow definition consistency, not a remote workflow run.
