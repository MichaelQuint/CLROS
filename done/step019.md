# Step 019 - EFI NoStdLib Shim Boundary

## What Changed

- Added host tests that verify the EFI no-stdlib shim boundary remains explicit.
- Confirmed `src/OS/OS.csproj` is the only project that explicitly compiles `Runtime/MinimalRuntime.cs`.
- Confirmed `src/OS/OS.csproj` keeps `NoStdLib`, disables implicit framework references, disables default compile items, and avoids project references.
- Updated the no-stdlib limits document with an EFI-only shim boundary section and runtime-helper ledger.
- Changed the EFI shell type-cast helper placeholders to use a shared fail-fast halt path instead of returning successful cast results.
- Updated EFI build documentation to describe type-cast helpers as fail-fast placeholders.

## Why It Changed

Gitea issue #18 requires a clear boundary between host-testable `OS.Std.*` helpers and canonical no-stdlib `System.*` stubs. The EFI project needs a minimal shim to publish, but those stubs must not leak into normal managed projects or silently claim unsupported runtime behavior.

## Files Changed

- `src/OS/Runtime/MinimalRuntime.cs`
- `tests/OS.HostTests/NoStdLibShimBoundaryTests.cs`
- `docs/nativeaot-nostdlib-limits.md`
- `docs/efi-build.md`
- `done/step019.md`

## Tests Run

```powershell
dotnet build .\OS.slnx --nologo -m:1
dotnet run --project .\tests\OS.HostTests\OS.HostTests.csproj --no-build
.\scripts\build-efi-shell.ps1
git diff --check -- .\src\OS\Runtime\MinimalRuntime.cs .\tests\OS.HostTests\NoStdLibShimBoundaryTests.cs .\docs\nativeaot-nostdlib-limits.md .\docs\efi-build.md .\done\step019.md
```

## Logs And Evidence

- `dotnet build .\OS.slnx --nologo -m:1` passed with 0 warnings and 0 errors after clearing the stale test-process lock.
- Host tests emitted:
  - `TEST_PASS nostdlib shim boundary includes explicit EFI project`
  - `TEST_PASS nostdlib shim boundary is explicit in EFI project`
  - `TEST_PASS nostdlib shim boundary keeps System stubs out of host projects`
- Full host tests emitted `TEST_PASS host.tests`.
- EFI publish emitted `TEST_PASS efi.shell.publish` and staged `artifacts/efi/BOOTX64.EFI`.
- Scoped `git diff --check` exited 0 with only existing LF/CRLF normalization warnings.

## Failures

An initial full solution build was blocked by a stale `OS.HostTests.exe` process holding the test executable. The stale process was terminated before rerunning verification.

## Deferred Work

- Real allocation helpers.
- GC and write barriers.
- Correct type-cast/interface dispatch helpers.
- Class constructor runner.
- Thread-static support.
- Managed exception handling and unwind.
- TypeManager/RTR metadata scans.

## Evidence Boundary

This step proves the compile boundary and helper-status ledger for the EFI no-stdlib shim. It does not prove a complete BCL, managed heap, GC, exception handling, interface dispatch, class constructors, thread statics, hosted .NET 10, ASP.NET Minimal API, or Kestrel support.
