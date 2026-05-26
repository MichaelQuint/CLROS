# Step 508 - Virtio PCI derived BAR-size binding catalog default guard

## Summary

Implemented Gitea issue #493 by making
`VirtioPciBindingDerivedBarSizeCatalogResult` reject default/zero values as
successful fake-derived virtio PCI binding catalog evidence.

## Why

`VirtioPciBindingDerivedBarSizeCatalogStatus.Success` is zero. The result
struct previously reported success for default values even though no candidate
scan, configuration-source lookup, fake-derived BAR sizing, or catalog entry
publication had been composed. Catalog evidence must only be successful after
explicit builder creation.

## Files changed

- `src/OS.Kernel/Drivers/VirtioPciBindingDerivedBarSizeCatalog.cs`
- `tests/OS.HostTests/VirtioPciBindingDerivedBarSizeCatalogTests.cs`
- `docs/drivers.md`
- `docs/completion-gates.md`
- `done/step508.md`

## Tests run

- `dotnet build .\tests\OS.HostTests\OS.HostTests.csproj --nologo`
- `dotnet run --project .\tests\OS.HostTests\OS.HostTests.csproj --no-build`
  filtered for `drivers.virtio.pci.binding.derive.catalog default result is not success`
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

TEST_START drivers.virtio.pci.binding.derive.catalog default result is not success
TEST_PASS drivers.virtio.pci.binding.derive.catalog default result is not success
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

- This is host-tested fake-derived virtio PCI binding catalog metadata only. It
  does not enumerate live PCI devices, execute live config-space writes or
  reads, restore BARs on hardware, validate live device behavior, map ECAM or
  BARs, perform MMIO or DMA, bind live drivers, perform packet or block I/O,
  prove cloud-provider hardware support, support ASP.NET Minimal API/Kestrel,
  prove cloud readiness, or prove production readiness.
