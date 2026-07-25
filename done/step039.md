# Step 039 - FAT32 BPB And Root Directory Parser

## What Changed

- Added a status-coded FAT32 BPB parser over `IReadOnlyBlockDevice`.
- Added FAT32 BPB, directory entry, directory-read, file-lookup, and status value types.
- Added first-root-cluster directory enumeration into a caller-provided span.
- Added short 8.3 root file lookup returning directory-entry metadata.
- Added host tests for valid BPB parsing, corrupt BPB cases, root entry enumeration, short-name lookup, unsupported long-name paths, and truncated directory entries.
- Updated storage docs with the FAT32 parser boundary and unsupported behavior.

## Why It Changed

The storage path needs guest-side filesystem parser groundwork before manifest and app package reads can be implemented. This slice keeps FAT32 behavior host-testable over `IReadOnlyBlockDevice` and avoids depending on virtio-blk or image-builder internals.

## Files Changed

- `src/OS.Kernel/Storage/Fat32Status.cs`
- `src/OS.Kernel/Storage/Fat32Bpb.cs`
- `src/OS.Kernel/Storage/Fat32BpbParseResult.cs`
- `src/OS.Kernel/Storage/Fat32DirectoryEntry.cs`
- `src/OS.Kernel/Storage/Fat32DirectoryReadResult.cs`
- `src/OS.Kernel/Storage/Fat32FileLookupResult.cs`
- `src/OS.Kernel/Storage/Fat32Parser.cs`
- `tests/OS.HostTests/StorageFat32ParserTests.cs`
- `docs/cstorage.md`
- `done/step039.md`

## Tests Run

```powershell
dotnet build .\OS.slnx --nologo -m:1
dotnet run --project .\tests\OS.HostTests\OS.HostTests.csproj --no-build
.\scripts\check-naming.ps1
git diff --check
```

## Evidence

- Host test marker: `TEST_PASS storage fat32 parses valid BPB`
- Host test marker: `TEST_PASS storage fat32 rejects bad boot signature`
- Host test marker: `TEST_PASS storage fat32 rejects unsupported sector size`
- Host test marker: `TEST_PASS storage fat32 rejects invalid sectors per cluster`
- Host test marker: `TEST_PASS storage fat32 rejects invalid FAT count`
- Host test marker: `TEST_PASS storage fat32 enumerates root directory entries`
- Host test marker: `TEST_PASS storage fat32 reports missing root short-name file`
- Host test marker: `TEST_PASS storage fat32 finds root short-name file metadata`
- Host test marker: `TEST_PASS storage fat32 rejects unsupported long-name path`
- Host test marker: `TEST_PASS storage fat32 reports truncated directory entry`
- Full host test marker: `TEST_PASS host.tests`

## Failures

None for this parser slice after main-thread verification.

## Deferred Work

- VFAT long-file-name parsing for `MANIFEST.JSON` and longer package paths.
- FAT chain walking.
- File-content reads.
- Subdirectory traversal.
- GPT ESP selection and integration.
- Virtio-blk integration.
- Manifest and app package loading.

## Evidence Boundary

This step proves host-tested FAT32 BPB parsing, first-root-cluster enumeration, and short 8.3 root metadata lookup over a mocked block device. It does not prove virtio-blk, real disk I/O, long-file-name support, file-content reads, manifest loading, app loading, hosted .NET 10, ASP.NET Minimal API, or Kestrel support.
