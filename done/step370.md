# Step 370 - Virtio PCI binding from fake-derived BAR sizes

## Summary

Added a host-tested virtio PCI binding composer that feeds fake-derived BAR
sizing metadata into the existing modern virtio PCI binding plan for network and
block devices.

## Why

Step 369 proved fake BAR sizing readbacks can be converted into derived BAR
aperture sizes. The next narrow slice was to prove those sizes can flow into
the virtio PCI binding plan while preserving both sizing provenance and binding
failure provenance.

## Files changed

- `src/OS.Kernel/Drivers/VirtioPciBindingDerivedBarSizePlan.cs`
- `tests/OS.HostTests/VirtioPciBindingDerivedBarSizePlanTests.cs`
- `docs/drivers.md`
- `docs/cstorage.md`
- `docs/virtio-net-mmio-boundary.md`
- `docs/completion-gates.md`
- `done/step370.md`

## Tests run

- `dotnet build .\tests\OS.HostTests\OS.HostTests.csproj --nologo`
- `dotnet run --project .\tests\OS.HostTests\OS.HostTests.csproj --no-build | Select-String -Pattern 'drivers\.virtio\.pci\.binding\.derive|TEST_PASS host\.tests|TEST_FAIL'`
- `dotnet build .\OS.slnx --nologo -m:1`
- `git diff --check`

## Evidence

Focused host-test marker output:

```text
TEST_PASS drivers.virtio.pci.binding.derive creates network binding from fake bar probe
TEST_PASS drivers.virtio.pci.binding.derive creates block binding from fake bar probe
TEST_PASS drivers.virtio.pci.binding.derive reports header parse failure
TEST_PASS drivers.virtio.pci.binding.derive reports bar decode failure
TEST_PASS drivers.virtio.pci.binding.derive propagates derived size failure
TEST_PASS drivers.virtio.pci.binding.derive reports short size destination
TEST_PASS drivers.virtio.pci.binding.derive propagates binding failure
TEST_PASS drivers.virtio.pci.binding.derive preserves no-live-config-access boundary
TEST_PASS host.tests
```

The focused host build and full solution build passed with 0 warnings and 0
errors. `git diff --check` reported no whitespace errors; it only emitted
LF-to-CRLF working-copy warnings on the Windows host.

## Failures

None in the focused build, marker scan, full solution build, or diff check.

## Deferred work

- Add a live PCI config-space accessor only after post-firmware MMIO ownership,
  ECAM mapping/cacheability, and BAR restore safety are tested.
- Treat live executor-produced BAR sizes as real virtio binding input only after
  that live accessor proves write/read/restore behavior on hardware.
- Continue from binding metadata to driver attachment only after live BAR
  mapping, DMA ownership, queue setup, notification, and completion paths exist.
- Do not treat this as live PCI enumeration, BAR mapping, virtio-blk reads,
  virtio-net packet I/O, cloud readiness, hosted .NET 10 readiness, ASP.NET
  Minimal API readiness, or Kestrel evidence.

## Evidence boundary

This proves only host-tested virtio PCI binding composition from caller-provided
PCI configuration bytes and BAR sizes produced by fake-captured BAR sizing
readbacks. The BAR sizes carry fake-executor provenance, not live hardware
provenance. It does not execute live PCI config-space reads or writes, restore
BARs on hardware, validate that a device accepted an all-ones sizing probe, map
ECAM or BARs, perform volatile MMIO, allocate or map DMA memory, bind drivers,
perform packet or block I/O, or prove QEMU/cloud, hosted .NET 10, ASP.NET
Minimal API, or Kestrel readiness.
