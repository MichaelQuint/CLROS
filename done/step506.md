# Step 506 - PCI BAR sizing probe derived-size default guard

## Summary

Implemented Gitea issue #491 by making
`PciBarSizingProbeDerivedSizeResult` reject default/zero values as successful
fake-derived BAR sizing evidence.

## Why

`PciBarSizingProbeDerivedSizeStatus.Success` is zero. The result struct
previously reported success for default values even though no address plan, fake
execution, readback storage, or BAR sizing plan had been composed. Derived-size
evidence must only be successful after explicit planner creation.

## Files changed

- `src/OS.Kernel/Drivers/PciBarSizingProbeDerivedSizePlan.cs`
- `tests/OS.HostTests/PciBarSizingProbeDerivedSizePlanTests.cs`
- `docs/drivers.md`
- `docs/completion-gates.md`
- `done/step506.md`

## Tests run

- `dotnet build .\tests\OS.HostTests\OS.HostTests.csproj --nologo`
- `dotnet run --project .\tests\OS.HostTests\OS.HostTests.csproj --no-build`
  filtered for `drivers.pci.bar.sizeprobe.derive default result is not success`
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

TEST_START drivers.pci.bar.sizeprobe.derive default result is not success
TEST_PASS drivers.pci.bar.sizeprobe.derive default result is not success
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

- This is host-tested fake-derived BAR sizing evidence only. It does not
  execute live config-space writes or reads, restore BARs on hardware, validate
  live device behavior, map ECAM or BARs, perform MMIO or DMA, bind drivers,
  prove cloud-provider hardware support, support ASP.NET Minimal API/Kestrel,
  prove cloud readiness, or prove production readiness.
