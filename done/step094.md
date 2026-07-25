# Step 094 - FAT32 First-Level File Content Read

## What Changed

- Added FAT32 first-level file content reads for paths such as `OS/MANIFEST.JSON`.
- Reused the existing first-level directory metadata lookup and caller-owned destination buffers.
- Added host tests for successful content read, too-small destination, and missing parent directory behavior.

## Why It Changed

Issue #110 moves storage one step closer to reading the image-builder layout, where the manifest lives below the `\OS` directory instead of in the FAT32 root.

## Files Changed

- `src/OS.Kernel/Storage/Fat32Parser.cs`
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
  - `TEST_PASS storage fat32 reads OS/MANIFEST.JSON content`
  - `TEST_PASS storage fat32 rejects OS/MANIFEST.JSON destination too small`
  - `TEST_PASS storage fat32 rejects missing OS directory for content read`

## Failures

- No build or host-test failures after integration.

## Deferred Work

- No FAT chain walking.
- No manifest/package locator that combines GPT and FAT32.
- No virtio-blk or real disk evidence.
- No parsed manifest handoff to the app launcher.

## Evidence Boundary

This step proves host-side contiguous FAT32 first-level file content reads only. It is not manifest loading or booted storage evidence.
