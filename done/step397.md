# Step 397 - FAT32 Directory Chain Lookup

## Summary
Extended FAT32 root and first-level directory metadata lookup to walk FAT directory-cluster chains instead of stopping at the first directory cluster.

## Why
Generated and future cloud images can place enough entries in `\` or `\OS` that metadata lands after the first directory cluster. File content chains already worked; directory metadata needed the same deterministic chain handling before boot-time package reads can rely on FAT32 layout.

## Files changed
- `src/OS.Kernel/Storage/Fat32Status.cs`
- `src/OS.Kernel/Storage/Fat32Parser.cs`
- `tests/OS.HostTests/StorageFat32ParserTests.cs`
- `docs/cstorage.md`
- `docs/completion-gates.md`
- `done/step397.md`

## Tests run
- `dotnet build .\tests\OS.HostTests\OS.HostTests.csproj --nologo`
- `dotnet run --project .\tests\OS.HostTests\OS.HostTests.csproj --no-build`
- `dotnet build .\OS.slnx --nologo -m:1`
- `git diff --check`

## Evidence
- Focused host-test build completed with 0 warnings and 0 errors.
- Host-test marker scan emitted:
  - `TEST_PASS storage fat32 reads chained root directory entries`
  - `TEST_PASS storage fat32 finds chained root directory file metadata`
  - `TEST_PASS storage fat32 reads chained first-level directory entries`
  - `TEST_PASS storage fat32 rejects directory chain loop`
  - `TEST_PASS storage fat32 reports short chained directory read`
  - `TEST_PASS host.tests`
- Full solution build completed with 0 warnings and 0 errors.
- `git diff --check` exited 0 with only LF-to-CRLF working-copy normalization warnings.

## Failures
- None.

## Deferred work
- Keep nested directory recursion unsupported until a separate issue defines the path depth and allocation boundary.
- Wire storage reads through live virtio-blk after real device enumeration, queue ownership, notification, completion, and boot-time storage integration exist.

## Evidence boundary
- This is host-tested FAT32 parser behavior over an injected `IReadOnlyBlockDevice`. It does not prove live virtio-blk reads, boot-time manifest/package loading, native app execution, hosted .NET 10, ASP.NET Minimal API/Kestrel, cloud readiness, or production readiness.

