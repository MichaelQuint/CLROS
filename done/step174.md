# Step 174 - Virtio PCI BAR Aperture Validator

## What Changed

- Added `VirtioPciBarApertureValidator` to validate successful virtio PCI discovery metadata against decoded PCI BAR descriptors and caller-provided BAR sizes.
- Added host tests for valid required apertures, 64-bit memory BAR support, I/O BAR rejection, absent BAR rejection, consumed 64-bit upper-half rejection, BAR index and size-entry failures, out-of-size regions, overflowed regions, and failed discovery input.
- Updated the driver documentation to describe the metadata-only virtio PCI BAR aperture validation boundary.

## Why

Gitea issue #193 needs a small safety contract between virtio PCI capability discovery and any future MMIO access. The OS must prove that required common, notify, ISR, and device configuration capability regions point into present memory BAR apertures before a later driver maps or touches device registers.

## Files Changed

- `src/OS.Kernel/Drivers/VirtioPciBarApertureValidator.cs`
- `tests/OS.HostTests/VirtioPciBarApertureValidatorTests.cs`
- `docs/drivers.md`

## Evidence

- `dotnet build .\OS.slnx --nologo -m:1` passed with 0 warnings and 0 errors.
- `dotnet run --project .\tests\OS.HostTests\OS.HostTests.csproj --no-build` emitted `TEST_PASS host.tests`.
- `.\scripts\check-naming.ps1` emitted `TEST_PASS naming.guard` and `TEST_PASS source.firewall`.
- `git diff --check` exited 0 with CRLF normalization warnings only.

Observed issue markers:

- `TEST_PASS drivers.virtio.pci.bar validates required apertures`
- `TEST_PASS drivers.virtio.pci.bar accepts 64bit memory bar`
- `TEST_PASS drivers.virtio.pci.bar rejects io bar capability`
- `TEST_PASS drivers.virtio.pci.bar rejects absent capability bar`
- `TEST_PASS drivers.virtio.pci.bar rejects region beyond bar size`
- `TEST_PASS drivers.virtio.pci.bar rejects failed discovery input`

## Evidence Boundary

This is host-tested metadata validation only. It does not size BARs, write all-ones to PCI configuration space, map MMIO, allocate pages, negotiate virtio features, publish queues, notify devices, process interrupts, or perform storage/network I/O.

## Deferred Work

- Add a BAR sizing contract that can supply reliable aperture sizes without unsafe writes in early driver code.
- Feed real PCI enumeration and capability-parser output into this validator once ECAM reads and driver binding exist.
- Use validated apertures as a prerequisite for future virtio register mapping and feature negotiation.
