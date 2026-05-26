# Step 537 - ENA Admin Queue Default Plan Budget Guard

## Summary
- Updated `EnaAdminQueueLayoutPlan.FitsBudget` so default/zero plans no longer
  satisfy the budget-fit predicate.
- Added host marker coverage for the default plan predicate.
- Updated the driver and completion-gate docs.

## Why
The ENA admin queue layout plan result already rejects default result-success
values, but the plan convenience predicate could still return `true` for a
default plan because `0 <= 0`. A layout with zero required bytes is not valid
admin queue evidence, so the predicate now requires nonzero planned bytes before
checking the budget.

## Files changed
- `src/OS.Kernel/Drivers/EnaAdminQueueLayoutPlan.cs`
- `tests/OS.HostTests/EnaAdminQueueLayoutPlanTests.cs`
- `docs/drivers.md`
- `docs/completion-gates.md`
- `done/step537.md`

## Tests run
- `dotnet build .\tests\OS.HostTests\OS.HostTests.csproj --nologo`
- `dotnet run --project .\tests\OS.HostTests\OS.HostTests.csproj --no-build`
  filtered for `drivers.ena.admin.queue default plan does not fit budget`,
  `TEST_PASS host.tests`, and failures.
- `dotnet build .\OS.slnx --nologo -m:1`
- `.\scripts\check-naming.ps1`
- `git diff --check`

## Evidence
```text
dotnet build .\tests\OS.HostTests\OS.HostTests.csproj --nologo

Build succeeded.
    0 Warning(s)
    0 Error(s)
```

```text
TEST_START drivers.ena.admin.queue default plan does not fit budget
TEST_PASS drivers.ena.admin.queue default plan does not fit budget
TEST_PASS host.tests
```

```text
dotnet build .\OS.slnx --nologo -m:1

Build succeeded.
    0 Warning(s)
    0 Error(s)
```

```text
TEST_START naming.guard
TEST_PASS naming.guard
TEST_START source.firewall
TEST_PASS source.firewall
```

```text
git diff --check
ExitCode: 0; existing LF-to-CRLF working-copy warnings only.
```

## Failures
- None for this slice.

## Deferred work
- This is host-tested ENA admin queue layout metadata only.
- No live ENA PCI discovery, BAR mapping, DMA allocation/mapping, admin queue
  programming, doorbell writes, packet I/O, cloud readiness, or production
  readiness is claimed.

