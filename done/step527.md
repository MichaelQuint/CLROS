# Step 527 - ENA Admin Queue Layout Default Result Guard

## Summary

Added explicit initialization metadata to the ENA admin queue layout result so a
default/zero result no longer reports success.

## Why

`EnaAdminQueueLayoutPlanStatus.Success` is the zero enum value. The queue layout
contract should only be successful after explicit layout planning validates the
queue depths, entry sizes, alignment, memory budget, and arithmetic bounds.

## Files changed

- `src/OS.Kernel/Drivers/EnaAdminQueueLayoutPlan.cs`
- `tests/OS.HostTests/EnaAdminQueueLayoutPlanTests.cs`
- `docs/drivers.md`
- `docs/completion-gates.md`
- `done/step527.md`

## Tests run

- `dotnet build .\tests\OS.HostTests\OS.HostTests.csproj --nologo`
- `dotnet run --project .\tests\OS.HostTests\OS.HostTests.csproj --no-build | Select-String -Pattern 'drivers\.ena\.admin\.queue rejects default result|TEST_PASS host\.tests|TEST_FAIL'`
- `dotnet build .\OS.slnx --nologo -m:1`
- `.\scripts\check-naming.ps1`
- `git diff --check`

## Evidence

```text
dotnet build .\tests\OS.HostTests\OS.HostTests.csproj --nologo
Build succeeded.
0 Warning(s)
0 Error(s)

TEST_START drivers.ena.admin.queue rejects default result
TEST_PASS drivers.ena.admin.queue rejects default result
TEST_PASS host.tests

dotnet build .\OS.slnx --nologo -m:1
Build succeeded.
0 Warning(s)
0 Error(s)

TEST_START naming.guard
TEST_PASS naming.guard
TEST_START source.firewall
TEST_PASS source.firewall

git diff --check
ExitCode: 0; existing LF-to-CRLF working-copy warnings only.
```

## Failures

- Claude Code delegation retry still reports `Not logged in - Please run /login`,
  so no Claude subagent wrote issue updates for this slice.

## Deferred work

- This is host-tested ENA admin queue layout metadata only. It does not allocate
  pages, map physical memory, program ENA BAR registers, ring doorbells, poll
  completions, configure AENQ, move packets, host .NET, prove Kestrel, or prove
  cloud/production readiness.

