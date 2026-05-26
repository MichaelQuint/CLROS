# Step 372 - Virtio descriptor catalog from fake-derived PCI binding provenance

## Summary

Added a host-tested descriptor catalog path that consumes fake-derived virtio PCI
binding provenance while building block and network descriptor metadata.

## Why

Step 371 proved multiple virtio PCI candidates can be cataloged from
fake-derived BAR sizing metadata. The next narrow slice was to feed that
provenance into the descriptor layer that parses caller-provided device
configuration bytes and attaches initialization-profile metadata.

## Files changed

- `src/OS.Kernel/Drivers/VirtioDeviceDescriptorCatalog.cs`
- `tests/OS.HostTests/VirtioDeviceDescriptorCatalogTests.cs`
- `docs/drivers.md`
- `docs/cstorage.md`
- `docs/virtio-net-mmio-boundary.md`
- `docs/completion-gates.md`
- `done/step372.md`

## Tests run

- `dotnet build .\tests\OS.HostTests\OS.HostTests.csproj --nologo`
- `dotnet run --project .\tests\OS.HostTests\OS.HostTests.csproj --no-build | Select-String -Pattern 'drivers\.virtio\.descriptor\.catalog\.derive|TEST_PASS host\.tests|TEST_FAIL'`
- `dotnet build .\OS.slnx --nologo -m:1`
- `git diff --check`

## Evidence

Focused host-test marker output:

```text
TEST_PASS drivers.virtio.descriptor.catalog.derive creates block descriptor
TEST_PASS drivers.virtio.descriptor.catalog.derive creates network descriptor
TEST_PASS drivers.virtio.descriptor.catalog.derive reports derived binding failure
TEST_PASS drivers.virtio.descriptor.catalog.derive reports missing device config
TEST_PASS drivers.virtio.descriptor.catalog.derive reports config parse failure
TEST_PASS drivers.virtio.descriptor.catalog.derive reports initialization failure
TEST_PASS drivers.virtio.descriptor.catalog.derive reports destination exhaustion
TEST_PASS drivers.virtio.descriptor.catalog.derive preserves no-live-device boundary
TEST_PASS host.tests
```

The focused host build and full solution build passed with 0 warnings and 0
errors. `git diff --check` reported no whitespace errors; it only emitted
LF-to-CRLF working-copy warnings on the Windows host.

## Failures

None in the focused build, marker scan, full solution build, or diff check.

## Deferred work

- Feed live executor-produced BAR sizes into the descriptor catalog only after a
  live PCI config-space accessor proves safe write/read/restore behavior.
- Replace caller-provided device configuration bytes with live BAR-backed config
  reads only after BAR mapping, volatile access, and ordering rules exist.
- Continue from descriptor metadata to driver attachment only after live feature
  negotiation, queue setup, notification, DMA ownership, and completion paths
  exist.
- Do not treat this as live PCI enumeration, BAR mapping, virtio-blk reads,
  virtio-net packet I/O, cloud readiness, hosted .NET 10 readiness, ASP.NET
  Minimal API readiness, or Kestrel evidence.

## Evidence boundary

This proves only host-tested descriptor metadata composition from fake-derived
virtio PCI binding provenance, caller-provided block/network device
configuration bytes, feature bits, and queue counts. It preserves
`VirtioPciBindingDerivedBarSizePlanResult` failure provenance, but it does not
read live device configuration, write status registers, negotiate features with
hardware, configure queues, bind drivers, transfer packets, read sectors, or
prove QEMU/cloud, hosted .NET 10, ASP.NET Minimal API, or Kestrel readiness.
