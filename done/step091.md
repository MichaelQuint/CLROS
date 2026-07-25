# Step 091 - FAT32 First-Level Directory Lookup

## What Changed

- Added FAT32 first-level directory metadata lookup for paths such as `OS/MANIFEST.JSON`.
- Added status handling for missing directories, non-directory parents, unsupported nested paths, invalid directory clusters, and child-directory read failures.
- Added host tests for `OS` directory metadata and `OS/MANIFEST.JSON` child metadata.

## Why It Changed

Issue #109 moves storage from root-only lookup toward the generated ESP layout used by the image builder, where OS package files live under `\OS`.

## Files Changed

- `src/OS.Kernel/Storage/Fat32Parser.cs`
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
  - `TEST_PASS storage fat32 finds OS directory metadata`
  - `TEST_PASS storage fat32 finds OS/MANIFEST.JSON metadata`
  - `TEST_PASS storage fat32 rejects unsupported nested path`

## Failures

- No build or host-test failures after integration.

## Deferred Work

- No file-content read for `OS/MANIFEST.JSON` yet.
- No FAT chain walking.
- No recursive path traversal.
- No virtio-blk or real disk evidence.

## Evidence Boundary

This step proves host-side FAT32 first-level metadata lookup only. It is not manifest loading or booted storage evidence.
