# Step 736 - PAL Export Catalog Shared Decimal Formatter

## Summary

Migrated the PAL export-catalog validation diagnostic formatter to the shared no-runtime ASCII decimal formatter. `OSHostExportCatalogDiagnosticFormatter` now uses `OS.Std.Text.AsciiDecimalFormatter` for integer byte counts and output while preserving the existing `PAL_EXPORT_CATALOG_VALIDATION` marker text.

## Why

The PAL export catalog had local integer-to-ASCII counting and writing loops. Shared string, numeric, and BCL-like helpers must live in `std/no-runtime/shared` before kernel and PAL usage. This slice removes the PAL-local decimal loops and adds a source-shape test so future formatter work keeps using the shared no-runtime surface.

## Files changed

- `src/OS.Kernel/Pal/OSHostExportCatalog.cs`
- `tests/OS.HostTests/PalExportCatalogTests.cs`
- `docs/pal.md`
- `docs/nativeaot-nostdlib-limits.md`
- `done/step736.md`

## Tests run

- `dotnet build .\tests\OS.HostTests\OS.HostTests.csproj --nologo`
- `dotnet run --project .\tests\OS.HostTests\OS.HostTests.csproj --no-build`
- `git diff --check`

## Evidence

- `artifacts\logs\issue702-host-build-20260525.log`: host test project build succeeded with 0 warnings and 0 errors.
- `artifacts\logs\issue702-hosttests-20260525.log`: host tests passed with:
  - `TEST_PASS pal.export catalog formatter uses shared decimal formatter`
  - `TEST_PASS pal.export catalog formats validation marker`
  - `TEST_PASS pal.export catalog formatter reports short destination`
  - `TEST_PASS pal.export catalog formatter default result is not success`
  - `TEST_PASS host.tests`
- `artifacts\logs\issue702-git-diff-check-postdone-20260525.log`: `git diff --check` exited 0; output contains only existing LF-to-CRLF working-copy warnings.

## Failures

No test failure is known for this slice.

## Deferred work

- Continue migrating any remaining PAL/kernel diagnostic formatters to shared no-runtime helpers before adding new local utility code.
- Keep unmanaged `OSHost_*` export implementation, hosted .NET, Kestrel, and live PAL calls in later milestone issues.

## Evidence boundary

This is host-tested formatter cleanup only. It does not export unmanaged `OSHost_*` symbols, call a hosted runtime, implement live PAL calls, run .NET 10 applications, prove Kestrel, boot QEMU, or prove cloud readiness.
