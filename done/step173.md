# Step 173 - PCI ECAM Segment Scan Coordinator

## What Changed

- Added `PciEcamSegmentScanner` to bridge parsed MCFG `PciEcamSegment` records to the existing injected-reader `PciBusScanner`.
- Added `PciEcamScannedDevice` output records that preserve segment index, segment group, ECAM base physical address, and segment bus range for each discovered PCI function.
- Added host tests for single-segment scans, multi-segment scans, segment metadata preservation, destination exhaustion, nested scanner failure propagation, and invalid segment-range rejection.
- Updated the PCI section in `docs/drivers.md`.

## Why

Gitea issue #192 needed a coordination layer between ACPI MCFG parsing and PCI bus scanning without jumping to live ECAM MMIO. This keeps the path status-coded, caller-buffered, and testable while preserving enough segment metadata for later virtio discovery and BAR validation.

## Files Changed

- `src/OS.Kernel/Drivers/PciEcamSegmentScanner.cs`
- `tests/OS.HostTests/PciEcamSegmentScannerTests.cs`
- `docs/drivers.md`
- `done/step173.md`

## Evidence

- `dotnet build .\OS.slnx --nologo -m:1` passed.
- `dotnet run --project .\tests\OS.HostTests\OS.HostTests.csproj --no-build` emitted `TEST_PASS host.tests`.
- Observed markers:
  - `TEST_PASS drivers.pci.ecam scans one segment`
  - `TEST_PASS drivers.pci.ecam scans multiple segments`
  - `TEST_PASS drivers.pci.ecam preserves segment metadata`
  - `TEST_PASS drivers.pci.ecam reports destination exhaustion`
  - `TEST_PASS drivers.pci.ecam reports nested scanner failure`
  - `TEST_PASS drivers.pci.ecam rejects invalid segment range`

## Failures

- None in the completed build and host-test run.

## Deferred Work

- Implement a safe ECAM-backed first-64-byte configuration-space reader.
- Feed real ECAM scanner snapshots into virtio PCI capability parsing and device discovery.
- Add live QEMU PCI enumeration evidence after physical-memory mapping and MMIO read rules exist.

## Evidence Boundary

This is host-tested scan-coordination evidence only. It does not dereference ECAM base addresses, map physical memory, perform MMIO reads, walk capability lists, map BARs, configure interrupts, bind drivers, or prove QEMU hardware enumeration.
