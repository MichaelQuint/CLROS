# Step 499 - PCI ECAM address default guard

## Summary

Implemented Gitea issue #484 by making
`PciEcamConfigurationAddressResult` reject default/zero values as successful
PCI ECAM address-planning evidence.

## Why

`PciEcamConfigurationSpaceStatus.Success` is zero. The address result
previously reported success for default values even though no segment, BDF
tuple, register offset, ECAM offset, or physical address had been initialized.
PCI ECAM address metadata must only be successful after explicit validation and
calculation.

## Files changed

- `src/OS.Kernel/Drivers/PciEcamConfigurationSpace.cs`
- `tests/OS.HostTests/PciEcamConfigurationSpaceTests.cs`
- `docs/drivers.md`
- `docs/completion-gates.md`
- `done/step499.md`

## Tests run

- `dotnet build .\tests\OS.HostTests\OS.HostTests.csproj --nologo`
- `dotnet run --project .\tests\OS.HostTests\OS.HostTests.csproj --no-build`
  filtered for `drivers.pci.ecam default result is not success` and
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

TEST_START drivers.pci.ecam default result is not success
TEST_PASS drivers.pci.ecam default result is not success
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

- This is host-tested PCI ECAM address metadata only. It does not map ECAM,
  perform MMIO reads or writes, enumerate PCI, size or map BARs, bind drivers,
  prove cloud-provider hardware support, support ASP.NET Minimal API/Kestrel,
  prove cloud readiness, or prove production readiness.
