# Step 526 - ENA Admin Command Default Result Guard

## Summary

Added explicit initialization metadata to ENA admin command planning,
submission, and completion-validation result structs so default/zero values do
not report success.

## Why

`EnaAdminCommandPlanStatus.Success` is the zero enum value. Without an explicit
initialized bit, default result structs could look successful before command
planning, descriptor writing, or completion validation actually ran.

## Files changed

- `src/OS.Kernel/Drivers/EnaAdminCommandPlan.cs`
- `tests/OS.HostTests/EnaAdminCommandPlanTests.cs`
- `docs/drivers.md`
- `docs/completion-gates.md`
- `done/step526.md`

## Tests run

- `dotnet build .\tests\OS.HostTests\OS.HostTests.csproj --nologo`
- `dotnet run --project .\tests\OS.HostTests\OS.HostTests.csproj --no-build | Select-String -Pattern 'drivers\.ena\.admin\.command rejects default results|TEST_PASS host\.tests|TEST_FAIL'`
- `dotnet build .\OS.slnx --nologo -m:1`
- `.\scripts\check-naming.ps1`
- `git diff --check`

## Evidence

```text
dotnet build .\tests\OS.HostTests\OS.HostTests.csproj --nologo
Build succeeded.
0 Warning(s)
0 Error(s)

TEST_START drivers.ena.admin.command rejects default results
TEST_PASS drivers.ena.admin.command rejects default results
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

- This is host-tested ENA admin command metadata only. It does not execute live
  ENA admin commands, map BARs, allocate DMA memory, ring doorbells, poll
  completions, configure interrupts, move packets, host .NET, prove Kestrel, or
  prove cloud/production readiness.

