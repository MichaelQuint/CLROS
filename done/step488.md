# Step 488 - Fault probe stub default guard

## Summary

Implemented Gitea issue #473 by making `FaultProbeStubPlanResult` reject
default/zero values and incomplete byte-count metadata as successful destructive
fault-probe byte-plan evidence.

## Why

`FaultProbeStubPlanStatus.Success` is zero. The fault-probe stub result
previously reported success for `default(FaultProbeStubPlanResult)` even though
no probe kind, byte count, or required-byte metadata had been initialized.
Destructive probe bytes are disabled by default and must remain explicitly
planned before later controlled-fault readiness can trust them.

## Files changed

- `src/OS.Kernel/Diagnostics/FaultProbeStubPlan.cs`
- `tests/OS.HostTests/FaultProbeStubPlanTests.cs`
- `docs/diagnostics.md`
- `docs/completion-gates.md`
- `done/step488.md`

## Tests run

- `dotnet build .\tests\OS.HostTests\OS.HostTests.csproj --nologo`
- `dotnet run --project .\tests\OS.HostTests\OS.HostTests.csproj --no-build`
  filtered for `fault probe stub plan default result is not success` and
  `TEST_PASS host.tests`
- `dotnet build .\OS.slnx --nologo -m:1`
- `.\scripts\check-naming.ps1`
- `git diff --check`

## Evidence

```text
dotnet build .\tests\OS.HostTests\OS.HostTests.csproj --nologo
Build succeeded.
0 Warning(s)
0 Error(s)

TEST_START fault probe stub plan default result is not success
TEST_PASS fault probe stub plan default result is not success
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

- This is host-tested byte-plan evidence only. It does not execute `ud2`, write
  through address zero, enable destructive probes by default, install handlers,
  prove QEMU fault behavior, support ASP.NET Minimal API/Kestrel, prove cloud
  readiness, or prove production readiness.
