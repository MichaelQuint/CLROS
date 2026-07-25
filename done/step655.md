# Step 655 - std/no-runtime ArrayHelpers sort contracts

## Summary

Added no-runtime-compatible `ArrayHelpers` sort helpers for unmanaged spans and
range-limited sorting, with explicit POD result metadata for status-coded
callers.

## Why

CloudReady kernel, driver, storage, networking, and app-model code need shared
array utilities in `std/no-runtime/shared` before kernel layers grow local
sorting helpers. The range-limited API gives host-tested behavior for invalid
ranges without relying on exceptions in runtime-sensitive paths.

## Files changed

- `std/no-runtime/shared/ArrayHelpers.cs`
  - Added `ArraySortStatus`.
  - Added unmanaged `ArraySortResult` metadata.
  - Added `Sort<T>(Span<T>)` for unmanaged comparable values.
  - Added `TrySort<T>(Span<T>, int start, int length)` with insertion-sort
    behavior and invalid-range status reporting.
- `tests/OS.HostTests/StdArrayHelpersTests.cs`
  - Added machine-readable tests for empty, single-item, already-sorted,
    reversed, duplicate, ranged, invalid-range, and default-result cases.
- `docs/nativeaot-nostdlib-limits.md`
  - Documented the supported `ArrayHelpers` sort surface and limitations.

## Tests run

- `dotnet build .\tests\OS.HostTests\OS.HostTests.csproj --nologo -m:1 /p:OSWorkloadProfile=SingleMinimalApi /p:UseSharedCompilation=false -nr:false`
- `git diff --check -- .\std\no-runtime\shared\ArrayHelpers.cs .\tests\OS.HostTests\StdArrayHelpersTests.cs .\docs\nativeaot-nostdlib-limits.md .\done\step655.md`
- `dotnet run --project .\tests\OS.HostTests\OS.HostTests.csproj --no-build`

## Evidence

- Host test project build completed with 0 warnings and 0 errors.
- Focused whitespace check completed without diff-check errors.
- Full host test log:
  `artifacts/logs/host-tests-arrayhelpers-sort-final.log`
- New marker coverage added:
  - `TEST_START std.arrayhelpers sort unmanaged spans`
  - `TEST_PASS std.arrayhelpers sort unmanaged spans`
  - `TEST_START std.arrayhelpers trysort range and status`
  - `TEST_PASS std.arrayhelpers trysort range and status`
  - `TEST_START std.arrayhelpers trysort default result is not success`
  - `TEST_PASS std.arrayhelpers trysort default result is not success`
- Full suite marker evidence:
  - `TEST_PASS host.tests`

## Failures

- No failures in the focused build, focused whitespace check, or full host test
  run.

## Deferred work

- Add broader BCL-compatible `Array.Sort` API surfaces only after the supported
  no-runtime behavior and allocation/exception boundaries are documented.
- Keep sort algorithms simple until callers prove larger data sets require a
  different no-allocation implementation.

