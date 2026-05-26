# Step 503 - PCI BAR sizing probe default guard

## Summary

Implemented Gitea issue #488 by making `PciBarSizingProbePlanResult` reject
default/zero values as successful BAR sizing probe command-plan evidence.

## Why

`PciBarSizingProbePlanStatus.Success` is zero. The result struct previously
reported success for default values even though no decoded BAR descriptors had
been consumed and no caller-owned command plan had been written. Probe-plan
evidence must only be successful after explicit planner creation.

## Files changed

- `src/OS.Kernel/Drivers/PciBarSizingProbePlan.cs`
- `tests/OS.HostTests/PciBarSizingProbePlanTests.cs`
- `docs/drivers.md`
- `docs/completion-gates.md`
- `done/step503.md`

## Tests run

- `dotnet build .\tests\OS.HostTests\OS.HostTests.csproj --nologo`
- `dotnet run --project .\tests\OS.HostTests\OS.HostTests.csproj --no-build`
  filtered for `drivers.pci.bar.sizeprobe default result is not success` and
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

TEST_START drivers.pci.bar.sizeprobe default result is not success
TEST_PASS drivers.pci.bar.sizeprobe default result is not success
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

- This is host-tested caller-owned PCI BAR sizing probe command metadata only.
  It does not execute live config-space writes or reads, restore BARs on
  hardware, validate live device behavior, map ECAM or BARs, perform MMIO or
  DMA, bind drivers, prove cloud-provider hardware support, support ASP.NET
  Minimal API/Kestrel, prove cloud readiness, or prove production readiness.
