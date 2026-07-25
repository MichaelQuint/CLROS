# Step 107 - GPT ESP Package Loader

## What Changed

- Added a storage loader that composes primary GPT ESP discovery, partition-relative reads, FAT32 `OS/APP.PKG` lookup, and OS package metadata parsing.
- Added status-coded package load results that preserve the failing layer: GPT, partition view, FAT32 read, or package parse.
- Added host tests for successful package readback, missing package failure, and malformed package failure.

## Why It Changed

Issue #125 moves storage from separate parser pieces toward the future boot/app-package boundary. The OS still does not have virtio-blk, but the parser stack can now prove that a package placed in an ESP image can be found and validated through the same block-device abstraction a real driver will implement.

## Files Changed

- `src/OS.Kernel/Storage/OsPackageLoader.cs`
- `tests/OS.HostTests/OsPackageLoaderTests.cs`
- `docs/cstorage.md`
- `docs/completion-gates.md`

## Tests Run

```powershell
dotnet build .\OS.slnx --nologo -m:1
dotnet run --project .\tests\OS.HostTests\OS.HostTests.csproj --no-build > .\artifacts\logs\host-tests-current.log
```

## Logs And Evidence

- Build passed with 0 warnings and 0 errors.
- Host tests emitted:
  - `TEST_PASS storage.package loader reads package from gpt esp`
  - `TEST_PASS storage.package loader rejects missing package`
  - `TEST_PASS storage.package loader rejects malformed package`
  - `TEST_PASS host.tests`

## Failures

- No failures in the final coordinated verification pass.

## Deferred Work

- No virtio-blk-backed package read.
- No boot-time package load.
- No package signatures, compression, alternate package paths, payload loading, or app execution.

## Evidence Boundary

This step proves a host in-memory block-device path only. It is not real disk, post-ExitBootServices storage, native-tier app launch, hosted .NET 10, Minimal API, or Kestrel evidence.
