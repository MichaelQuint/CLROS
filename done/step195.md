# Step 195 - Virtio Device Descriptor Catalog

## Summary

Implemented issue #214 by adding a host-testable virtio device descriptor catalog. The catalog composes successful virtio PCI binding-plan metadata with caller-provided device-configuration bytes and caller-provided feature/queue metadata to produce typed block or network descriptor records.

## Why

The existing virtio pipeline could identify and validate modern virtio PCI block/network candidates, parse device-specific configuration bytes, and plan initialization profiles independently. Driver attachment needs one deterministic metadata layer that joins those pieces before live MMIO, status writes, feature negotiation, queue setup, or device I/O are introduced.

## Files Changed

- `src/OS.Kernel/Drivers/VirtioDeviceDescriptorCatalog.cs`
- `tests/OS.HostTests/VirtioDeviceDescriptorCatalogTests.cs`
- `docs/drivers.md`
- `done/step195.md`

## Tests Run

- `dotnet build .\OS.slnx --nologo -m:1`
- `dotnet run --project .\tests\OS.HostTests\OS.HostTests.csproj --no-build`
- `.\scripts\check-naming.ps1`
- `git diff --check`

## Evidence

Observed host-test markers:

- `TEST_PASS drivers.virtio.descriptor.catalog creates block descriptor`
- `TEST_PASS drivers.virtio.descriptor.catalog creates network descriptor`
- `TEST_PASS drivers.virtio.descriptor.catalog reports missing device config`
- `TEST_PASS drivers.virtio.descriptor.catalog reports unsupported kind`
- `TEST_PASS drivers.virtio.descriptor.catalog reports config parse failure`
- `TEST_PASS drivers.virtio.descriptor.catalog reports initialization failure`
- `TEST_PASS drivers.virtio.descriptor.catalog reports destination exhaustion`

## Failures

None in this slice. `git diff --check` reported CRLF normalization warnings only, matching the existing repository behavior.

## Deferred Work

- Live ECAM reads.
- BAR sizing and MMIO mapping.
- Virtio status-register writes.
- Feature-register reads and live `FEATURES_OK` observation.
- Queue setup and DMA ownership.
- Virtio-blk sector reads.
- Virtio-net packet I/O.
- Hosted .NET 10, Minimal API, and Kestrel evidence.
