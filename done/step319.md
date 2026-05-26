# Step 319 - OSHost Package and Config Facades

## What Changed

- Added `OSHostPackageFileFacade`, a host-only `OSHost_*`-shaped adapter over `OSHostPackageFileTable`.
- Added `OSHostConfigEnvironmentFacade`, a host-only `OSHost_*`-shaped adapter over `OSHostConfigEnvironmentTable`.
- Added config enumeration to `OSHostConfigEnvironmentTable`, parallel to the existing environment enumeration path.
- Added package facade host tests for open/read/info/close, missing and unsafe paths, small read buffers, stale handles, handle exhaustion, package enumeration, malformed request shapes, and no-managed-reference result layouts.
- Added config facade host tests for config/environment lookup, config/environment enumeration, buffer sizing, missing keys, malformed request shapes, and no-managed-reference result layouts.
- Updated PAL, Kestrel, app-model, PAL design, and completion-gate docs to state this is host-only facade evidence, not exported PAL or Kestrel support.

## Why

Issue #319 needed package and config facades analogous to the existing memory, socket, timer, wait, and app-log facades. The tables already modeled package-file and config/environment behavior, but future hosted .NET 10/Kestrel work needs stable facade methods that validate POD-shaped request metadata before delegating to those tables.

This step keeps everything in-process and host-testable. It does not add unmanaged exports, CoreCLR integration, guest package reads, process environment propagation, or Kestrel execution.

## Files Changed

- `src/OS.Kernel/Pal/OSHostPackageFileFacade.cs`
- `src/OS.Kernel/Pal/OSHostConfigEnvironmentFacade.cs`
- `src/OS.Kernel/Pal/OSHostConfigEnvironmentTable.cs`
- `tests/OS.HostTests/PalPackageFileFacadeTests.cs`
- `tests/OS.HostTests/PalConfigEnvironmentFacadeTests.cs`
- `docs/pal.md`
- `docs/kestrel.md`
- `docs/app-model.md`
- `docs/completion-gates.md`
- `work/PAL/pal-design.md`
- `done/step319.md`

## Evidence

Commands run:

```powershell
dotnet build .\tests\OS.HostTests\OS.HostTests.csproj --nologo
dotnet run --project .\tests\OS.HostTests\OS.HostTests.csproj --no-build | Select-String -Pattern 'pal\.package facade|pal\.config facade|TEST_PASS host\.tests|TEST_FAIL'
dotnet build .\OS.slnx --nologo -m:1
git diff --check
```

Observed markers:

```text
TEST_PASS pal.config facade returns config and env values
TEST_PASS pal.config facade enumerates config and env
TEST_PASS pal.config facade reports buffer and missing keys
TEST_PASS pal.config facade rejects malformed request shapes
TEST_PASS pal.config facade result layouts contain no managed references
TEST_PASS pal.package facade opens reads info closes
TEST_PASS pal.package facade reports missing and unsafe paths
TEST_PASS pal.package facade reports small read buffers
TEST_PASS pal.package facade rejects stale handles
TEST_PASS pal.package facade reports handle exhaustion
TEST_PASS pal.package facade enumerates package files
TEST_PASS pal.package facade rejects malformed request shapes
TEST_PASS pal.package facade result layouts contain no managed references
TEST_PASS host.tests
```

`dotnet build .\OS.slnx --nologo -m:1` passed with 0 warnings and 0 errors.

`git diff --check` exited 0. Git reported LF-to-CRLF working-copy warnings for existing text files, but no whitespace errors.

## Failures

- The first build failed because the package facade test fixture passed `int` values to endian writer helpers that require unsigned widths. The fixture now casts package metadata fields explicitly to `uint` and `ushort`.

## Deferred Work

- No unmanaged `OSHost_OpenPackageFile`, `OSHost_ReadFileAt`, `OSHost_GetFileInfo`, `OSHost_CloseFile`, `OSHost_EnumeratePackage`, `OSHost_GetConfigValue`, `OSHost_EnumerateConfig`, `OSHost_GetEnvironmentValue`, or `OSHost_EnumerateEnvironment` symbols are exported.
- No guest package storage, virtio-blk, FAT/package boot read, hosted .NET 10 runtime, process environment propagation, ASP.NET configuration binding, or Kestrel behavior is implemented.
- No socket, TCP listener, HTTP request, QEMU HTTP probe, or cloud-provider proof is part of this step.

## Evidence Boundary

This step proves host-only facade status translation over already-loaded package/config bytes. It is a PAL prerequisite for future hosted .NET 10/Kestrel work, not Kestrel support and not guest runtime evidence.
