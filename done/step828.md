# Step 828 - M00-004 FetchApp and HTTP Test Locators

## Summary

Migrated FetchApp and HTTP host-test root discovery to the shared CLROS locator.

## Why

Networking contract tests must not depend on the removed `OS.slnx` name.

## Files changed

- `tests/OS.HostTests/FetchAppHttpCompletionPlanTests.cs`
- `tests/OS.HostTests/FetchAppHttpTcpExchangePlanTests.cs`
- `tests/OS.HostTests/HttpResponseParserTests.cs`
- `docs/networking.md`

## Tests run

```powershell
dotnet run --project .\tests\OS.HostTests\OS.HostTests.csproj --no-build
```

## Evidence

All affected host-test groups run in the integrated baseline recorded in Step
845.

## Failures

Only the stale repository sentinel was changed; networking behavior was not
promoted.

## Deferred work

Live packet I/O remains a later milestone.

## Evidence boundary

This proves host-test execution, not live networking or FetchApp execution.
