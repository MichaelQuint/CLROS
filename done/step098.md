# Step 098 - Partition-Relative Block Device View

## What Changed

- Added `PartitionBlockDevice` as a read-only `IReadOnlyBlockDevice` wrapper over a bounded parent-device region.
- Added status-coded creation validation for null parent, invalid parent geometry, zero-length partitions, out-of-range starts, and partitions crossing the parent end.
- Added host tests for relative reads and boundary rejection.

## Why It Changed

Issue #116 provides the adapter needed to pass GPT ESP partitions to FAT32 as relative volumes without teaching FAT32 about whole-disk offsets.

## Files Changed

- `src/OS.Kernel/Storage/PartitionBlockDevice.cs`
- `tests/OS.HostTests/StoragePartitionBlockDeviceTests.cs`
- `docs/cstorage.md`
- `docs/completion-gates.md`

## Tests Run

```powershell
dotnet build .\OS.slnx --nologo -m:1
dotnet run --project .\tests\OS.HostTests\OS.HostTests.csproj --no-build
```

## Logs And Evidence

- Host tests emitted:
  - `TEST_PASS storage partition device reads relative blocks`
  - `TEST_PASS storage partition device rejects out of range partition`
  - `TEST_PASS storage partition device rejects read past partition end`

## Failures

- No build or host-test failures after integration.

## Deferred Work

- No real virtio-blk parent device.
- No partition cache.
- No write support.
- No boot-time disk integration.

## Evidence Boundary

This step proves host-side partition offset and bounds behavior only. It is not real disk or filesystem evidence.
