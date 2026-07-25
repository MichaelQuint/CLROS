# Step 755 - Phase F package staging formatter shared text builder

## Summary

- Mapped to Gitea issue #713.
- Updated the virtio-blk package-file staging preflight diagnostic formatter to use
  the shared no-runtime ASCII text builder for marker byte appends and UInt64
  decimal formatting.
- Added a host source guard that proves the formatter references
  `AsciiTextBuilder` and no longer carries private UInt64 digit-writing logic.
- Documented the new host marker in the storage evidence list.

## Why

Package staging diagnostics should share the same no-runtime text formatting
surface as the rest of the kernel-facing marker code instead of keeping local
byte-copy and decimal digit loops. This keeps the stable
`APP_PACKAGE_FILE_STAGING_PREFLIGHT` marker behavior while reducing duplicate
formatting logic in storage.

## Files changed

- `src/OS.Kernel/Storage/VirtioBlockPackageFileStagingPreflightDiagnosticFormatter.cs`
- `tests/OS.HostTests/StorageVirtioBlockPackageReadPreflightTests.cs`
- `docs/cstorage.md`
- `done/step755.md`

## Tests run

- `dotnet build .\tests\OS.HostTests\OS.HostTests.csproj --nologo`
- `dotnet run --project .\tests\OS.HostTests\OS.HostTests.csproj --no-build`
- `git diff --check`

## Evidence

- `artifacts/logs/issue-713-hosttests-build.log`: host test project build
  succeeded with 0 warnings and 0 errors.
- `artifacts/logs/issue-713-hosttests-run.log`: full host harness passed.
- `artifacts/logs/issue-713-git-diff-check.log`: `git diff --check` exited 0;
  output contains only existing LF-to-CRLF working-copy warnings.
- Observed markers:
  - `TEST_PASS storage.virtioblk package file staging formatter formats success marker`
  - `TEST_PASS storage.virtioblk package file staging formatter reports short destination`
  - `TEST_PASS storage.virtioblk package file staging formatter default result is not success`
  - `TEST_PASS storage.virtioblk package file staging formatter uses shared ascii text builder`
  - `TEST_PASS host.tests`

## Failures

- None.
- `git diff --check` emitted repository-wide LF-to-CRLF warnings for dirty
  files but returned success and reported no whitespace errors.

## Deferred work

- Evidence remains host-only storage/app-model composition. This step does not
  prove live virtio-blk MMIO/DMA, real disk reads, boot-time package loading,
  executable mapping, app execution, hosted .NET, Minimal API, or Kestrel.

