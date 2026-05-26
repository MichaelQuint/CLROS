# Step 185 - Virtio Block Device Configuration Parser

## What Changed

- Added `VirtioBlockDeviceConfig` as the narrow virtio-blk device-configuration parser for issue #204.
- Parsed the little-endian 64-bit capacity field at offset 0 from caller-provided config bytes only.
- Converted the raw virtio capacity, in 512-byte sectors, through `BlockDeviceInfo.Create`.
- Preserved raw capacity, sector size, nested `BlockDeviceStatus`, and parser status metadata.
- Added host tests for successful capacity parsing, block-info creation, short config rejection, zero-capacity rejection, and capacity-overflow rejection.
- Updated `docs/cstorage.md` with the current parser-only storage status.

## Why

The virtio-blk read path now has request planning, split-ring publication, completion parsing, and lifecycle coordination. It still needed a small, testable bridge from the device-configuration capacity field to the generic block-device geometry contract before a future live PCI/MMIO driver can expose a read-only block device.

## Files Changed

- `src/OS.Kernel/Storage/VirtioBlockDeviceConfig.cs`
- `tests/OS.HostTests/StorageVirtioBlockDeviceConfigTests.cs`
- `docs/cstorage.md`
- `done/step185.md`

## Evidence

- `dotnet build .\OS.slnx --nologo -m:1` passed with 0 warnings and 0 errors.
- `dotnet run --project .\tests\OS.HostTests\OS.HostTests.csproj --no-build` emitted `TEST_PASS host.tests`.
- `scripts/check-naming.ps1` emitted `TEST_PASS naming.guard` and `TEST_PASS source.firewall`.
- `git diff --check` exited 0 with CRLF normalization warnings only.

Required issue #204 markers:

- `TEST_PASS storage.virtioblk.config parses capacity`
- `TEST_PASS storage.virtioblk.config creates block info`
- `TEST_PASS storage.virtioblk.config rejects short config`
- `TEST_PASS storage.virtioblk.config rejects zero capacity`
- `TEST_PASS storage.virtioblk.config reports capacity overflow`

## Failures

- No failures in this slice.

## Deferred Work

- No MMIO config reads are performed.
- No PCI BAR mapping, feature negotiation, queue setup, DMA ownership, notification, interrupts, sector reads, filesystem reads, hosted .NET 10, Minimal API, or Kestrel behavior is proven by this step.

## Evidence Boundary

This is host-testable parser evidence over caller-owned config bytes only. It proves conversion from a virtio-blk capacity field to `BlockDeviceInfo`; it does not prove live virtio-blk hardware access or QEMU disk-read behavior.
