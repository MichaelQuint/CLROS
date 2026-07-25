# Step 124 - Package File Enumeration Contracts

## What Changed

- Added deterministic zero-based package file-table enumeration to `OsPackageParser`.
- Added bounded package path copy by file index with status-coded out-of-range and destination-too-small failures.
- Added `OsPackageFileSystem.EnumerateFile` as the kernel-facing wrapper for caller-owned path buffers.
- Added `OSHostPackageFileTable.Enumerate` as the host-tested PAL mock path for future package enumeration behavior.
- Updated storage, PAL, app-model, and Kestrel docs to record the host-test evidence boundary.

## Why It Changed

Issue #142 needed package readers and future hosted-runtime PAL adapters to discover packaged files without allocation, exceptions, directory traversal, or host-owned strings. This slice keeps enumeration deterministic and bounded over already-loaded package bytes only.

## Files Changed

- `src/OS.Kernel/Storage/OsPackageParser.cs`
- `src/OS.Kernel/Storage/OsPackageFileSystem.cs`
- `src/OS.Kernel/Pal/OSHostPackageFileTable.cs`
- `tests/OS.HostTests/StoragePackageParserTests.cs`
- `tests/OS.HostTests/StoragePackageFileSystemTests.cs`
- `tests/OS.HostTests/PalPackageFileTests.cs`
- `docs/cstorage.md`
- `docs/pal.md`
- `docs/app-model.md`
- `docs/kestrel.md`
- `done/step124.md`

## Tests Run

```powershell
dotnet build .\OS.slnx --nologo -m:1
dotnet run --project .\tests\OS.HostTests\OS.HostTests.csproj --no-build
.\scripts\check-naming.ps1
.\scripts\run-qemu-smoke.ps1 -DryRun
git diff --check
```

## Logs And Evidence

- Solution build passed with 0 warnings and 0 errors.
- Parent-owned full host tests emitted:
  - `TEST_PASS storage.package enumerates file table`
  - `TEST_PASS storage.package enumeration exposes file paths`
  - `TEST_PASS storage.package enumeration rejects out of range index`
  - `TEST_PASS storage.package filesystem enumerates bounded paths`
  - `TEST_PASS pal.file enumerates package files`
  - `TEST_PASS pal.file enumeration reports buffer too small`
  - `TEST_PASS host.tests`
- Naming/source guard emitted `TEST_PASS naming.guard` and `TEST_PASS source.firewall`.
- QEMU dry-run emitted `TEST_PASS qemu.runner.dryrun`.
- `git diff --check` exited successfully. It still printed existing line-ending normalization warnings.

## Failures

- None in the parent-owned verification run.

## Deferred Work

- No exported `OSHost_EnumeratePackage` symbol was added.
- No guest block I/O, virtio-blk, package directory enumeration, manifest config/environment reads, hosted .NET runtime, ASP.NET Minimal API launch, or Kestrel behavior was added.

## Evidence Boundary

This step proves host-tested enumeration over already-loaded package bytes only. It does not prove live boot package reads, guest storage, app loading, hosted .NET 10, ASP.NET Minimal API, or Kestrel.
