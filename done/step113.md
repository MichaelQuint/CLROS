# Step 113 - Read-Only Package Filesystem API

## What Changed

- Added `OS.Storage.OsPackageFileSystem` as a tiny read-only wrapper over the OS package parser.
- Added status-coded open/read results for package-relative files.
- Added full-file and bounded range reads into caller-owned destination spans.
- Added host tests for file open, missing-file rejection, invalid path rejection, destination-too-small, and invalid range failures.

## Why It Changed

Issue #131 gives future app-launch code a small package-file access surface without reaching into parser internals or adding a general filesystem. This is the next host-testable step after package parsing and package loading.

## Files Changed

- `src/OS.Kernel/Storage/OsPackageFileSystem.cs`
- `tests/OS.HostTests/StoragePackageFileSystemTests.cs`
- `docs/cstorage.md`
- `docs/app-model.md`
- `docs/completion-gates.md`

## Tests Run

```powershell
dotnet build .\OS.slnx --nologo -m:1
dotnet run --project .\tests\OS.HostTests\OS.HostTests.csproj --no-build
```

## Logs And Evidence

- Build passed with 0 warnings and 0 errors.
- Full host tests were logged to `artifacts/logs/host-tests-current.log`.
- Full host tests emitted:
  - `TEST_PASS storage.package opens file`
  - `TEST_PASS storage.package rejects missing file`
  - `TEST_PASS storage.package enforces bounds`
  - `TEST_PASS host.tests`

## Failures

- None in the parent-owned verification run.

## Deferred Work

- No package directory enumeration.
- No package compression, signatures, mmap, or executable payload loading.
- No boot-time package file reads from virtio-blk.
- No app ABI, hosted .NET 10, ASP.NET Minimal API, or Kestrel.

## Evidence Boundary

This step is host parser/API evidence only. It does not prove virtio-blk, live guest disk access, app execution, hosted .NET 10, Minimal API, or Kestrel.
