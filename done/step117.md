# Step 117 - PAL Package File Handle Table

## What Changed

- Added `OSHostPackageFileTable`, a host-only mock PAL table over already-loaded OS package bytes.
- Added opaque package-file handles, file-info lookup, close handling, stale-handle rejection, bounded read-at behavior, unsafe-path rejection, and deterministic capacity exhaustion.
- Added `PalPackageFileTests` with the required issue markers.
- Updated PAL, storage, Kestrel, and completion-gate docs to keep the evidence boundary explicit.

## Why It Changed

Issue #138 needed the next file/config prerequisite contract for hosted .NET and Kestrel without exporting real `OSHost_*` ABI functions yet. The table models the read-only package file lifecycle that a hosted runtime adapter will eventually need while staying host-test-only.

## Files Changed

- `src/OS.Kernel/Pal/OSHostPackageFileTable.cs`
- `tests/OS.HostTests/PalPackageFileTests.cs`
- `docs/pal.md`
- `docs/kestrel.md`
- `docs/cstorage.md`
- `docs/completion-gates.md`

## Tests Run

```powershell
dotnet build .\OS.slnx --nologo -m:1
dotnet run --project .\tests\OS.HostTests\OS.HostTests.csproj --no-build
```

## Logs And Evidence

- Solution build passed with 0 warnings and 0 errors after concurrent ImageBuilder changes settled.
- Parent-owned full host tests emitted:
  - `TEST_PASS pal.file opens package file`
  - `TEST_PASS pal.file reads offset ranges`
  - `TEST_PASS pal.file rejects unsafe paths`
  - `TEST_PASS pal.file reports deterministic exhaustion`
  - `TEST_PASS host.tests`

## Failures

- None in the parent-owned verification run.

## Deferred Work

- No `OSHost_OpenPackageFile`, `OSHost_ReadFileAt`, `OSHost_GetFileInfo`, or `OSHost_CloseFile` symbols are exported.
- No guest package I/O, virtio-blk reads, config/environment PAL behavior, hosted .NET 10 runtime, ASP.NET Minimal API, or Kestrel behavior is implemented.

## Evidence Boundary

This step proves only a managed, in-process, host-testable mock package-file handle table over already-loaded package bytes. It does not prove exported PAL ABI behavior, guest storage, hosted runtime startup, app launch, Minimal API, or Kestrel.
