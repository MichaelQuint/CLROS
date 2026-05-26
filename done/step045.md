# Step 045 - GPT EFI System Partition Selection

## What Changed

- Added a status-coded GPT ESP selector at `GptParser.SelectPrimaryEfiSystemPartition`.
- Added `GptPartitionSelectionResult` to return the selected GPT header, partition entry, entry index, and start/end LBA metadata.
- Added `GptStatus.EfiSystemPartitionNotFound` for a primary GPT with no ESP entry.
- Reused the existing primary GPT header validation and `OS.Std.Binary.EndianPrimitives`-based entry parsing.
- Added host tests for ESP first, ESP non-first, missing ESP, empty-entry skipping, partition-entry short read, and out-of-range ESP entries.
- Updated `docs/cstorage.md` to state this is host-tested parser evidence only, not virtio-blk or guest manifest loading.

## Why It Changed

Issue #63 needs the storage parser layer to identify the EFI System Partition before future guest-side FAT32 manifest and package reads. The selector keeps the current storage boundary read-only, primary-GPT-only, and status-coded.

## Files Changed

- `src/OS.Kernel/Storage/GptParser.cs`
- `src/OS.Kernel/Storage/GptStatus.cs`
- `src/OS.Kernel/Storage/GptPartitionSelectionResult.cs`
- `tests/OS.HostTests/StorageGptParserTests.cs`
- `docs/cstorage.md`
- `done/step045.md`

## Tests Run

```powershell
dotnet build .\OS.slnx --nologo -m:1
dotnet run --project .\tests\OS.HostTests\OS.HostTests.csproj --no-build
```

## Evidence

- Solution build passed with 0 warnings and 0 errors.
- Host test marker: `TEST_PASS storage gpt selects efi system partition`
- Host test marker: `TEST_PASS storage gpt selects non-first efi system partition`
- Host test marker: `TEST_PASS storage gpt reports missing efi system partition`
- Host test marker: `TEST_PASS storage gpt skips empty entries while selecting esp`
- Host test marker: `TEST_PASS storage gpt reports esp entry array short read`
- Host test marker: `TEST_PASS storage gpt rejects esp entry range out of bounds`
- Full host test marker: `TEST_PASS host.tests`
- `git diff --check` exited 0 and reported only existing LF-to-CRLF warnings.

## Failures

None.

## Deferred Work

- GPT header CRC32 and partition-array CRC32 validation.
- Backup GPT fallback.
- General GPT partition enumeration beyond ESP selection.
- FAT32 long-file-name lookup for `\OS\MANIFEST.JSON`.
- FAT chain walking and file-content reads.
- Virtio-blk integration.

## Evidence Boundary

This step proves host-tested primary GPT ESP selection over an `IReadOnlyBlockDevice`. It does not prove virtio-blk, guest-side FAT32 file reads, manifest loading, package loading, backup GPT fallback, or storage behavior in QEMU.
