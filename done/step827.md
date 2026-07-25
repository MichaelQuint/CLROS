# Step 827 - M00-003 CI and Gate Test Locators

## Summary

Migrated CI-workflow and completion-gate tests to the shared CLROS root locator.

## Why

Independent legacy root searches could disagree with the canonical repository
identity or silently skip solution inspection.

## Files changed

- `tests/OS.HostTests/CiWorkflowTests.cs`
- `tests/OS.HostTests/CompletionGateTests.cs`
- `docs/completion-gates.md`

## Tests run

```powershell
dotnet run --project .\tests\OS.HostTests\OS.HostTests.csproj --no-build
```

## Evidence

The CI and completion-gate test groups execute from the shared root and are
included in the complete passing host baseline in Step 845.

## Failures

Legacy `OS.slnx` lookup is preserved only as a negative regression.

## Deferred work

Live gate promotion remains tied to subsystem evidence.

## Evidence boundary

This proves repository location and documentation consistency, not a later
completion gate.
