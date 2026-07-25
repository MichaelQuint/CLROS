# Step 502 - PCI BAR sizing default guard

## Summary

Implemented Gitea issue #487 by making PCI BAR sizing plan and size descriptor
success checks reject default/zero values unless they were initialized by the
sizing planner.

## Why

`PciBarSizingPlanStatus.Success` and `PciBarSizeDescriptorStatus.Success` are
zero. The result and descriptor structs previously reported success for default
values even though no decoded BAR set or caller-owned sizing readbacks had been
processed. BAR sizing evidence must only be successful after explicit planner
creation.

## Files changed

- `src/OS.Kernel/Drivers/PciBarSizingPlan.cs`
- `tests/OS.HostTests/PciBarSizingPlanTests.cs`
- `docs/drivers.md`
- `docs/completion-gates.md`
- `done/step502.md`

## Tests run

- `dotnet build .\tests\OS.HostTests\OS.HostTests.csproj --nologo`
- `dotnet run --project .\tests\OS.HostTests\OS.HostTests.csproj --no-build`
  filtered for `drivers.pci.bar.size default result is not success` and
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

TEST_START drivers.pci.bar.size default result is not success
TEST_PASS drivers.pci.bar.size default result is not success
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

- An initial focused build attempt failed because a stale concurrent no-build
  host test process locked `tests/OS.HostTests/bin/Debug/net10.0/OS.Kernel.dll`.
  The stale process was stopped and the focused build was rerun successfully.

## Deferred work

- This is host-tested caller-provided PCI BAR sizing metadata only. It does not
  access live ECAM, execute BAR sizing writes, restore BARs on hardware, map
  BARs, perform MMIO or DMA, bind drivers, prove cloud-provider hardware
  support, support ASP.NET Minimal API/Kestrel, prove cloud readiness, or prove
  production readiness.
