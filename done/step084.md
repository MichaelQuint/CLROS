# Step 084 - FAT32 Root File Read Helper

## What Changed

- Added `Fat32Parser.ReadRootFile` to read root-directory short-name or supported long-name files into caller-owned destination storage.
- Added `Fat32FileReadResult` and additional `Fat32Status` values for file-read failures.
- Added host tests for short-name content, `MANIFEST.JSON` long-name content, too-small destinations, and directory-as-file rejection.

## Why It Changed

Issue #102 needed the first guest-parser-shaped content read before manifest/package loading. The implementation keeps scope to mock block devices and root-directory files.

## Files Changed

- `src/OS.Kernel/Storage/Fat32Parser.cs`
- `src/OS.Kernel/Storage/Fat32FileReadResult.cs`
- `src/OS.Kernel/Storage/Fat32Status.cs`
- `tests/OS.HostTests/StorageFat32ParserTests.cs`
- `docs/cstorage.md`
- `docs/completion-gates.md`

## Tests Run

```powershell
dotnet build .\OS.slnx --nologo -m:1
dotnet run --project .\tests\OS.HostTests\OS.HostTests.csproj --no-build
```

## Logs And Evidence

- Host tests emitted:
  - `TEST_PASS storage fat32 reads root short-name file content`
  - `TEST_PASS storage fat32 reads MANIFEST.JSON root long-name file content`
  - `TEST_PASS storage fat32 rejects root file destination too small`
  - `TEST_PASS storage fat32 rejects directory read as file`

## Failures

- No build or host-test failures after integration.

## Deferred Work

- Root directory only.
- Contiguous-only file reads.
- No FAT chain walking.
- No `\OS\MANIFEST.JSON` subdirectory traversal.
- No virtio-blk or post-ExitBootServices storage evidence.

## Evidence Boundary

This step proves host/mock FAT32 file-content reads only. It is not manifest loading or real disk I/O.
