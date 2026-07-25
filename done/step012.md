# Step 012 - Read-Only Block Device Contract

## What Changed

- Added a minimal read-only block-device contract under `src/OS.Kernel/Storage`.
- Added `BlockDeviceInfo` for block size, block count, and total capacity metadata.
- Added `BlockDeviceStatus`, `BlockDeviceInfoResult`, and `BlockReadResult` for status-coded storage errors.
- Added `IReadOnlyBlockDevice` with `ReadBlocks(ulong firstBlock, uint blockCount, Span<byte> destination)`.
- Added `BlockDeviceRead.Validate` for shared range, geometry, destination, and overflow checks.
- Added host-only in-memory block-device tests.
- Updated `docs/cstorage.md`.

## Why It Changed

Virtio-blk, partition parsing, filesystem reading, manifest loading, and app packaging need a small first-party block contract before any driver or filesystem code starts using ad hoc byte reads.

## Files Changed

- `src/OS.Kernel/Storage/BlockDeviceInfo.cs`
- `src/OS.Kernel/Storage/BlockDeviceInfoResult.cs`
- `src/OS.Kernel/Storage/BlockDeviceRead.cs`
- `src/OS.Kernel/Storage/BlockDeviceStatus.cs`
- `src/OS.Kernel/Storage/BlockReadResult.cs`
- `src/OS.Kernel/Storage/IReadOnlyBlockDevice.cs`
- `tests/OS.HostTests/StorageBlockDeviceTests.cs`
- `docs/cstorage.md`

## Tests Run

```powershell
dotnet build .\OS.slnx --nologo -m:1
dotnet run --project .\tests\OS.HostTests\OS.HostTests.csproj --no-build
.\scripts\check-naming.ps1
git diff --check
```

## Logs and Evidence

- Managed build passed with 0 warnings and 0 errors.
- Host tests emitted `TEST_PASS host.tests`.
- New storage markers passed:
  - `TEST_PASS storage block device reads exact byte range`
  - `TEST_PASS storage block device reads final block boundary`
  - `TEST_PASS storage block device rejects out-of-range reads`
  - `TEST_PASS storage block device rejects destination too small`
  - `TEST_PASS storage block device rejects invalid block sizes`
  - `TEST_PASS storage block device detects oversized read overflow`
  - `TEST_PASS storage block device accepts zero-block read`
- Naming/source guard emitted `TEST_PASS naming.guard` and `TEST_PASS source.firewall`.
- `git diff --check` exited 0. It printed line-ending normalization warnings only.

## Failures

No failures remain for this slice.

## Deferred Work

- Virtio-blk PCI discovery and queue setup.
- GPT/MBR parsing.
- FAT/package filesystem reads.
- Manifest and packaged app loading.
- Writes, flush, discard/TRIM.

## Evidence Boundary

This step proves the read-only block-device contract and host in-memory behavior. It does not prove virtio-blk, real disk I/O, partition parsing, filesystems, manifest loading, app loading, hosted .NET 10, ASP.NET Minimal API, or Kestrel.
