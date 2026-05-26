# Step 794 - Package filesystem shared buffer copies

## Summary

Migrated package filesystem read copies onto `OS.Std.Memory.BufferOperations.MemoryCopy`
and added a host source-guard marker for the package filesystem copy boundary.

## Why

The package filesystem is a kernel-facing parser wrapper that reads already-loaded
package bytes into caller-owned destinations. Using the shared no-runtime buffer
copy helper keeps this Phase F/G storage path aligned with the std memory
migration while preserving the existing status-coded validation contract.

## Files changed

- `src/OS.Kernel/Storage/OsPackageFileSystem.cs`
  - imports `OS.Std.Memory`;
  - uses `BufferOperations.MemoryCopy` for full-file reads and bounded range
    reads after existing size/range validation;
  - maps any defensive shared-copy failure after validation to the existing
    `DestinationTooSmall` status.
- `tests/OS.HostTests/StoragePackageFileSystemTests.cs`
  - adds `TEST_PASS storage.package filesystem copies use shared buffer operations`.
- `docs/cstorage.md`
  - records the narrow host-tested package filesystem shared-copy boundary.
- `done/step794.md`
  - records this milestone and evidence.

## Tests run

- `dotnet build .\tests\OS.HostTests\OS.HostTests.csproj --nologo`
- `dotnet run --project .\tests\OS.HostTests\OS.HostTests.csproj --no-build`
- Scoped `git diff --check` over the changed issue files.
- Scoped trailing-whitespace scan over the changed issue files.

## Evidence

- `artifacts/logs/issue752-build.log`
  - build succeeded with 0 warnings and 0 errors.
- `artifacts/logs/issue752-hosttests.log`
  - emitted `TEST_PASS storage.package filesystem copies use shared buffer operations`.
  - emitted `TEST_PASS host.tests`.
- Scoped whitespace checks passed.

## Failures

- None.

## Deferred work

- Live virtio-blk reads, boot-time package loading from hardware, exported PAL
  file calls, executable mapping, hosted .NET, ASP.NET Minimal API, and Kestrel
  remain deferred.

## Evidence boundary

This step proves only the host-tested in-memory package filesystem read-copy
contract over caller-owned package bytes and destinations. It is not live disk
I/O, post-`ExitBootServices` storage, app execution, hosted .NET, or Kestrel
evidence.
