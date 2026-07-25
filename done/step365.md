# Step 365 - PCI BAR sizing metadata

## Summary

Added a host-tested PCI BAR sizing planner that derives BAR aperture sizes from
decoded type-0 BAR descriptors plus caller-owned all-ones sizing readback
snapshots, then exposes the resulting size table for virtio aperture validation.

## Why

Virtio block and network binding already validate capability apertures against
BAR sizes, but those sizes were only supplied as arbitrary caller metadata in
tests. This slice adds the pure interpretation step for standard BAR sizing
readbacks without implementing the live PCI config-space write/read/restore
sequence.

## Files changed

- `src/OS.Kernel/Drivers/PciBarSizingPlan.cs`
- `tests/OS.HostTests/PciBarSizingPlanTests.cs`
- `docs/drivers.md`
- `docs/cstorage.md`
- `docs/virtio-net-mmio-boundary.md`
- `docs/completion-gates.md`
- `done/step365.md`

## Tests run

- `dotnet build .\tests\OS.HostTests\OS.HostTests.csproj --nologo`
- `dotnet run --project .\tests\OS.HostTests\OS.HostTests.csproj --no-build | Select-String -Pattern 'drivers\.pci\.bar\.size|TEST_FAIL|TEST_PASS host\.tests'`
- `dotnet build .\OS.slnx --nologo -m:1`
- `git diff --check`

## Evidence

Focused host-test marker output:

```text
TEST_PASS drivers.pci.bar.size decodes 32bit mmio aperture
TEST_PASS drivers.pci.bar.size decodes 64bit mmio aperture
TEST_PASS drivers.pci.bar.size records absent and consumed bars
TEST_PASS drivers.pci.bar.size rejects malformed readback
TEST_PASS drivers.pci.bar.size rejects base alignment mismatch
TEST_PASS drivers.pci.bar.size feeds virtio aperture validation
TEST_PASS drivers.pci.bar.size preserves no-live-config-write boundary
TEST_PASS host.tests
```

The focused host build and full solution build passed with 0 warnings and 0
errors. `git diff --check` reported no whitespace errors; it only emitted
LF-to-CRLF working-copy warnings on the Windows host.

## Failures

None in the focused build, marker scan, full solution build, or diff check.

## Deferred work

- Implement the live PCI config-space BAR sizing write/read/restore sequence
  that can safely produce the caller-owned sizing readback snapshots.
- Wire live BAR sizing, BAR restore, MMIO mapping/cacheability, and DMA
  ownership into future virtio-blk and virtio-net binding.
- Do not treat this as live PCI enumeration, BAR mapping, virtio-blk reads,
  virtio-net packet I/O, cloud readiness, hosted .NET 10 readiness, ASP.NET
  Minimal API readiness, or Kestrel evidence.

## Evidence boundary

This proves only caller-owned PCI BAR sizing-readback interpretation over
already decoded type-0 BAR descriptors. It does not perform live config-space
writes, live config-space reads, BAR restore, BAR mapping, MMIO, DMA allocation
or mapping, interrupts, packet I/O, block I/O, cloud provider readiness, hosted
.NET 10 readiness, ASP.NET Minimal API readiness, or Kestrel readiness.
