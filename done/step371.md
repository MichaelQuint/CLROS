# Step 371 - Virtio PCI fake-derived BAR sizing binding catalog

## Summary

Added a host-tested bounded catalog composer that feeds multiple modern virtio
PCI candidates through the fake-derived BAR sizing binding path.

## Why

Step 370 proved one virtio PCI function can consume BAR sizes produced from
fake-captured BAR sizing readbacks. The next narrow slice was to preserve that
fake-executor provenance across multiple caller-provided candidates before a
future live PCI config accessor exists.

## Files changed

- `src/OS.Kernel/Drivers/VirtioPciBindingDerivedBarSizeCatalog.cs`
- `tests/OS.HostTests/VirtioPciBindingDerivedBarSizeCatalogTests.cs`
- `docs/drivers.md`
- `docs/cstorage.md`
- `docs/virtio-net-mmio-boundary.md`
- `docs/completion-gates.md`
- `done/step371.md`

## Tests run

- `dotnet build .\tests\OS.HostTests\OS.HostTests.csproj --nologo`
- `dotnet run --project .\tests\OS.HostTests\OS.HostTests.csproj --no-build | Select-String -Pattern 'drivers\.virtio\.pci\.binding\.derive\.catalog|TEST_PASS host\.tests|TEST_FAIL'`
- `dotnet build .\OS.slnx --nologo -m:1`
- `git diff --check`

## Evidence

Focused host-test marker output:

```text
TEST_PASS drivers.virtio.pci.binding.derive.catalog binds multiple candidates
TEST_PASS drivers.virtio.pci.binding.derive.catalog preserves source indexes
TEST_PASS drivers.virtio.pci.binding.derive.catalog reports missing config source
TEST_PASS drivers.virtio.pci.binding.derive.catalog propagates header parse failure
TEST_PASS drivers.virtio.pci.binding.derive.catalog propagates bar decode failure
TEST_PASS drivers.virtio.pci.binding.derive.catalog propagates derived size failure
TEST_PASS drivers.virtio.pci.binding.derive.catalog reports short size scratch
TEST_PASS drivers.virtio.pci.binding.derive.catalog propagates binding failure
TEST_PASS drivers.virtio.pci.binding.derive.catalog reports destination exhaustion
TEST_PASS drivers.virtio.pci.binding.derive.catalog preserves no-live-config-access boundary
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
- Treat live executor-produced BAR sizes as real virtio binding/catalog input
  only after that live accessor proves write/read/restore behavior on hardware.
- Continue from binding catalogs to driver attachment only after live BAR
  mapping, DMA ownership, queue setup, notification, and completion paths exist.
- Do not treat this as live PCI enumeration, BAR mapping, virtio-blk reads,
  virtio-net packet I/O, cloud readiness, hosted .NET 10 readiness, ASP.NET
  Minimal API readiness, or Kestrel evidence.

## Evidence boundary

This proves only host-tested virtio PCI binding catalog composition from
caller-provided candidates, PCI configuration bytes, an injected fake config
accessor, caller-owned scratch buffers, and BAR sizes produced by fake-captured
BAR sizing readbacks. The BAR sizes carry fake-executor provenance, not live
hardware provenance. It does not enumerate live PCI devices, execute live PCI
config-space reads or writes, restore BARs on hardware, validate that a device
accepted an all-ones sizing probe, map ECAM or BARs, perform volatile MMIO,
allocate or map DMA memory, bind drivers, perform packet or block I/O, or prove
QEMU/cloud, hosted .NET 10, ASP.NET Minimal API, or Kestrel readiness.
