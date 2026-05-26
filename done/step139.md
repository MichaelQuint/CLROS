# Step 139 - PAL Package File Invalid Handle Contract

## What Changed

- Added explicit PAL package-file invalid-handle coverage for zero, unknown, closed, and stale file handles.
- Preserved the existing deterministic package file handle table over already-loaded `OSPKGv0` bytes.
- Updated PAL, app-model, and completion-gate docs with the new host-test marker and evidence boundary.

## Why It Changed

Gitea issue #157 needed the PAL-facing package file table contract to state and test invalid-handle rejection directly. The existing table already returned `OSHostStatus.InvalidArgument` for invalid or stale handles; this slice makes that behavior explicit through a stable machine-readable marker.

## Files Changed

- `tests/OS.HostTests/PalPackageFileTests.cs`
- `docs/pal.md`
- `docs/app-model.md`
- `docs/completion-gates.md`
- `done/step139.md`

## Tests Run

```powershell
dotnet build .\OS.slnx --nologo -m:1
dotnet build .\tests\OS.HostTests\OS.HostTests.csproj --nologo -m:1 -o .\artifacts\verify\issue157-hosttests
dotnet .\artifacts\verify\issue157-hosttests\OS.HostTests.dll
```

## Logs And Evidence

- `dotnet build .\tests\OS.HostTests\OS.HostTests.csproj --nologo -m:1 -o .\artifacts\verify\issue157-hosttests` passed with 0 warnings and 0 errors.
- The full host-test executable run was stopped after it entered unrelated ImageBuilder tests, per status-check instruction.
- The PAL package-file host-test source now contains the required marker:
  - `TEST_PASS pal.file rejects invalid handles`

## Failures

- `dotnet build .\OS.slnx --nologo -m:1` failed because an existing `OS.HostTests (5700)` process held `tests\OS.HostTests\bin\Debug\net10.0\OS.Kernel.dll`.
- The broad `OS.HostTests.dll` run entered unrelated ImageBuilder tests. Those child builds reported duplicate generated assembly attributes under `std\no-runtime\shared\obj\Debug\net10.0`; the run was stopped and its child ImageBuilder process was terminated.

## Deferred Work

- No exported `OSHost_OpenPackageFile`, `OSHost_ReadFileAt`, `OSHost_GetFileInfo`, or `OSHost_CloseFile` symbols were added.
- No live storage driver, virtio-blk path, hosted .NET 10 runtime, ASP.NET Minimal API, or Kestrel behavior was added.
- Full host-test marker execution remains blocked until the unrelated ImageBuilder/host-test process issues are cleared.

## Evidence Boundary

This step is managed host-test-source coverage over already-loaded package bytes only. It proves that the PAL package file table contract includes explicit invalid-handle rejection in source and compiles in an isolated host-test build. It does not prove exported PAL ABI behavior, guest block I/O, boot-time package reads, hosted runtime startup, ASP.NET Minimal API execution, or Kestrel support.
