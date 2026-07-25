# Step 505 - PCI BAR sizing probe execution default guard

## Summary

Implemented Gitea issue #490 by making
`PciBarSizingProbeExecutionResult` and `PciConfigDwordReadResult` reject
default/zero values as successful fake PCI config-access evidence.

## Why

`PciBarSizingProbeExecutionStatus.Success` and
`PciConfigDwordAccessStatus.Success` are zero. The result structs previously
reported success for default values even though no injected config accessor had
run, no command had executed, and no caller-owned BAR sizing readback had been
captured. Fake-execution evidence must only be successful after explicit
executor or accessor result creation.

## Files changed

- `src/OS.Kernel/Drivers/PciBarSizingProbeExecutionPlan.cs`
- `tests/OS.HostTests/PciBarSizingProbeExecutionPlanTests.cs`
- `docs/drivers.md`
- `docs/completion-gates.md`
- `done/step505.md`

## Tests run

- `dotnet build .\tests\OS.HostTests\OS.HostTests.csproj --nologo`
- `dotnet run --project .\tests\OS.HostTests\OS.HostTests.csproj --no-build`
  filtered for `drivers.pci.bar.sizeprobe.fakeexec default result is not success`
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

TEST_START drivers.pci.bar.sizeprobe.fakeexec default result is not success
TEST_PASS drivers.pci.bar.sizeprobe.fakeexec default result is not success
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

- This is host-tested fake PCI config-accessor evidence only. It does not
  execute live config-space writes or reads, restore BARs on hardware, validate
  live device behavior, map ECAM or BARs, perform MMIO or DMA, bind drivers,
  prove cloud-provider hardware support, support ASP.NET Minimal API/Kestrel,
  prove cloud readiness, or prove production readiness.
