# Step 490 - COM1 backend plan default guard

## Summary

Implemented Gitea issue #475 by making `Com1PortIoBackendPlanResult` reject
default/zero values and incomplete success metadata as successful COM1
port-I/O backend prerequisite evidence.

## Why

`Com1PortIoBackendPlanStatus.Success` is zero. The backend-plan result
previously reported success for `default(Com1PortIoBackendPlanResult)` even
though no serial configuration validation, port operation/stub plan validation,
or executable-stub memory capacity metadata had been initialized. Future COM1
backend wiring must only trust explicitly constructed plan results.

## Files changed

- `src/OS.Kernel/Diagnostics/Com1PortIoBackendPlan.cs`
- `tests/OS.HostTests/Com1PortIoBackendPlanTests.cs`
- `docs/diagnostics.md`
- `docs/completion-gates.md`
- `done/step490.md`

## Tests run

- `dotnet build .\tests\OS.HostTests\OS.HostTests.csproj --nologo`
- `dotnet run --project .\tests\OS.HostTests\OS.HostTests.csproj --no-build`
  filtered for `com1 port io backend plan default result is not success`
  and `TEST_PASS host.tests`
- `dotnet build .\OS.slnx --nologo -m:1`
- `.\scripts\check-naming.ps1`
- `git diff --check`

## Evidence

```text
dotnet build .\tests\OS.HostTests\OS.HostTests.csproj --nologo
Build succeeded.
0 Warning(s)
0 Error(s)

TEST_START com1 port io backend plan default result is not success
TEST_PASS com1 port io backend plan default result is not success
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
exit 0; existing LF-to-CRLF working-copy warnings only
```

## Failures

- None.

## Deferred work

- This is host-tested metadata evidence only. It does not allocate or execute
  generated stubs, perform CPU port I/O, install a live owned COM1 backend,
  prove post-`ExitBootServices` serial output, support ASP.NET Minimal
  API/Kestrel, prove cloud readiness, or prove production readiness.
