# Step 187 - Virtio PCI Binding Catalog

## Summary

Implemented issue #206 as a host-tested, metadata-only virtio PCI binding catalog. The catalog builds bounded output entries from already-selected `VirtioPciCandidate` records plus caller-provided PCI configuration snapshots and BAR-size sources keyed by `SourceIndex`.

## Why

The previous `VirtioPciBindingPlan` validated one candidate at a time. The next driver-binding slice needs a deterministic catalog step that can carry multiple validated block/network candidates forward without performing live ECAM reads, MMIO mapping, feature negotiation, queue setup, or driver attach.

## Files Changed

- `src/OS.Kernel/Drivers/VirtioPciBindingCatalog.cs`
- `tests/OS.HostTests/VirtioPciBindingCatalogTests.cs`
- `docs/drivers.md`
- `done/step187.md`

## Tests Run

- `dotnet build .\src\OS.Kernel\OS.Kernel.csproj --nologo -m:1`
- `dotnet build .\OS.slnx --nologo -m:1`
- `dotnet run --project .\tests\OS.HostTests\OS.HostTests.csproj --no-build`
- `.\scripts\check-naming.ps1`
- `git diff --check`

## Evidence

The full host-test run ended with `TEST_PASS host.tests`.

`.\scripts\check-naming.ps1` emitted `TEST_PASS naming.guard` and `TEST_PASS source.firewall`. `git diff --check` exited 0 with CRLF normalization warnings only.

Non-closing Gitea evidence comment: `http://192.168.2.42:3000/michael.quint/OS/issues/206#issuecomment-14831`

Issue-specific markers observed:

- `TEST_PASS drivers.virtio.pci.catalog binds multiple candidates`
- `TEST_PASS drivers.virtio.pci.catalog preserves source indexes`
- `TEST_PASS drivers.virtio.pci.catalog reports missing config source`
- `TEST_PASS drivers.virtio.pci.catalog reports missing bar-size source`
- `TEST_PASS drivers.virtio.pci.catalog reports binding failure`
- `TEST_PASS drivers.virtio.pci.catalog reports destination exhaustion`

## Failures

No failures were observed in this slice.

## Deferred Work

- Live ECAM-backed configuration reads.
- BAR sizing without caller-provided aperture sizes.
- MMIO mapping and page-permission policy for BARs.
- Virtio feature negotiation and queue setup.
- Driver attach for virtio-blk and virtio-net.
- QEMU hardware binding evidence.
