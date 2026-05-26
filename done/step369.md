# Step 369 - PCI BAR sizing probe derived-size composition

## Summary

Added a host-tested composer that feeds fake-captured PCI BAR sizing probe
readbacks into `PciBarSizingPlanner` and exposes derived BAR aperture metadata
plus virtio aperture-validator input.

## Why

Step 368 proved fake config-accessor execution and caller-owned readback
capture for address-bound BAR sizing probe commands. The next narrow slice is
preserving that fake-execution provenance while deriving the BAR sizes consumed
by existing virtio PCI aperture validation.

## Files changed

- `src/OS.Kernel/Drivers/PciBarSizingProbeDerivedSizePlan.cs`
- `tests/OS.HostTests/PciBarSizingProbeDerivedSizePlanTests.cs`
- `docs/drivers.md`
- `docs/cstorage.md`
- `docs/virtio-net-mmio-boundary.md`
- `docs/completion-gates.md`
- `done/step369.md`

## Tests run

- `dotnet build .\tests\OS.HostTests\OS.HostTests.csproj --nologo`
- `dotnet run --project .\tests\OS.HostTests\OS.HostTests.csproj --no-build | Select-String -Pattern 'drivers\.pci\.bar\.sizeprobe\.derive|TEST_FAIL|TEST_PASS host\.tests'`
- `dotnet build .\OS.slnx --nologo -m:1`
- `git diff --check`

## Evidence

Focused host-test marker output:

```text
TEST_PASS drivers.pci.bar.sizeprobe.derive derives 32bit aperture from fake readback
TEST_PASS drivers.pci.bar.sizeprobe.derive derives 64bit aperture from fake readbacks
TEST_PASS drivers.pci.bar.sizeprobe.derive propagates execution failure
TEST_PASS drivers.pci.bar.sizeprobe.derive propagates sizing failure
TEST_PASS drivers.pci.bar.sizeprobe.derive feeds virtio aperture validation
TEST_PASS drivers.pci.bar.sizeprobe.derive preserves no-live-config-access boundary
TEST_PASS host.tests
```

The focused host build and full solution build passed with 0 warnings and 0
errors. `git diff --check` reported no whitespace errors; it only emitted
LF-to-CRLF working-copy warnings on the Windows host.

## Failures

None in the focused build, marker scan, full solution build, or diff check.

## Deferred work

- Implement a live PCI config-space accessor only after post-firmware
  physical-memory/MMIO ownership and BAR restore safety are represented.
- Use live executor-produced readbacks with `PciBarSizingPlanner` only after
  live config-space access, restore safety, ECAM mapping/cacheability, and DMA
  ownership prerequisites are tested.
- Do not treat this as live PCI enumeration, BAR mapping, virtio-blk reads,
  virtio-net packet I/O, cloud readiness, hosted .NET 10 readiness, ASP.NET
  Minimal API readiness, or Kestrel evidence.

## Evidence boundary

This proves only host-tested composition from fake-captured BAR sizing readbacks
into `PciBarSizingPlanner`-derived aperture metadata. It does not execute live
PCI config-space reads or writes, restore BARs on hardware, validate that a live
device accepted an all-ones probe, map ECAM or BARs, perform volatile MMIO,
allocate or map DMA memory, bind drivers, perform packet or block I/O, or prove
cloud provider readiness, hosted .NET 10 readiness, ASP.NET Minimal API
readiness, or Kestrel readiness.
