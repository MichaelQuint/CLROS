# Step 799 - FAT32 final-block copy BufferOperations guard

## Summary

Issue #757 migrates the FAT32 parser's final partial-block file-read copy from
direct span `CopyTo` onto the shared no-runtime
`OS.Std.Memory.BufferOperations.MemoryCopy` helper.

## Why

FAT32 file reads are kernel-facing storage parser contracts. Moving the final
partial-block copy onto the shared buffer operation path reduces parser-local
BCL-like copy behavior while preserving existing FAT chain traversal,
destination validation, and `Fat32Status` behavior.

## Files changed

- `src/OS.Kernel/Storage/Fat32Parser.cs`
- `tests/OS.HostTests/StorageFat32ParserTests.cs`
- `docs/cstorage.md`
- `done/step799.md`

## Tests run

- `dotnet build .\tests\OS.HostTests\OS.HostTests.csproj --nologo`
  - Result: pass
  - Log: `artifacts/logs/issue757-host-build.log`
- `dotnet .\tests\OS.HostTests\bin\Debug\net10.0\OS.HostTests.dll`
  - Result: pass
  - Log: `artifacts/logs/issue757-host-tests.log`
- `git diff --check -- src/OS.Kernel/Storage/Fat32Parser.cs tests/OS.HostTests/StorageFat32ParserTests.cs docs/cstorage.md done/step799.md`
  - Result: pass
  - Log: `artifacts/logs/issue757-diff-check.log`
  - Notes: log contains existing Git line-ending normalization warnings only.
- Scoped trailing whitespace scan over the files above
  - Result: pass
  - Log: `artifacts/logs/issue757-trailing-whitespace.log`

## Evidence

- `TEST_PASS done.step.allocate`
- `TEST_PASS storage fat32 final block copy uses shared buffer operations`
- `TEST_PASS storage fat32 reads root short-name file content`
- `TEST_PASS storage fat32 reads OS/MANIFEST.JSON content`
- `TEST_PASS storage fat32 reads chained file content`
- `TEST_PASS host.tests`
- `Fat32Parser` now imports `OS.Std.Memory` and copies the validated final
  partial block into the caller-owned destination through
  `BufferOperations.MemoryCopy`.
- Unexpected shared-copy failure after the existing destination/read validation
  maps to `Fat32Status.FileDestinationTooSmall`.

## Failures

None.

## Deferred work

This is host-tested FAT32 parser copy evidence only. It does not prove live
virtio-blk, post-ExitBootServices disk reads, boot-time manifest/package
loading, native app launch, hosted .NET, ASP.NET Minimal API, or Kestrel
behavior.

