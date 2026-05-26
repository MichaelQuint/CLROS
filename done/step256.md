# Step 256 - ENA PCI BAR And MSI-X Resource Snapshot Planner

## What Changed

- Added `EnaPciResourcePlan`, a host-testable ENA resource planner over
  caller-provided PCI discovery facts.
- Added `PciMsixCapabilityParser`, a caller-buffer parser for standard PCI
  MSI-X capability records.
- Added host tests for ENA MMIO BAR acceptance, MSI-X parsing, malformed BAR
  apertures, and malformed MSI-X regions.
- Updated ENA and AWS Nitro planning docs with the new metadata-only boundary.

## Why

ENA live driver bring-up needs a deterministic boundary between PCI discovery
and any later hardware ownership. This slice validates register BAR and MSI-X
metadata without enabling the device, mapping BARs, programming interrupts, or
allocating DMA memory.

## Files Changed

- `src/OS.Kernel/Drivers/EnaPciResourcePlan.cs`
- `tests/OS.HostTests/EnaPciResourcePlanTests.cs`
- `docs/ena-driver-plan.md`
- `docs/aws-nitro-device-plan.md`
- `done/step256.md`

## Tests Run

- `dotnet build .\OS.slnx --nologo -m:1`
  - Passed with 0 warnings and 0 errors.
- `dotnet run --project .\tests\OS.HostTests\OS.HostTests.csproj --no-build`
  - New ENA markers passed:
    - `TEST_PASS drivers.ena.pci.resources accepts mmio bar`
    - `TEST_PASS drivers.ena.pci.resources parses msix`
    - `TEST_PASS drivers.ena.pci.resources rejects malformed aperture`
    - `TEST_PASS drivers.ena.pci.resources rejects malformed msix`
  - A later integrated full-host run after the concurrent workload-profile
    tooling fix emitted `TEST_PASS host.tests`.

## Evidence Boundary

This is a parser/planner only. It performs no live PCI config reads or writes,
no BAR sizing writes, no BAR mapping, no PCI command programming, no MSI-X
enable, no interrupt-vector allocation, no DMA allocation, no ENA reset, no
admin queue execution, no TX/RX queue creation, and no packet I/O.

## Deferred Work

- Live PCI config-space reads and BAR sizing.
- Safe MMIO mapping for the ENA register BAR.
- PCI memory-space and bus-master command ownership.
- MSI-X table programming and interrupt routing.
- DMA allocation and queue publication.
- ENA admin queue execution and Ethernet RX/TX proof.
