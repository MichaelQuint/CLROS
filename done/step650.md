# Step 650 - std/no-runtime ArrayHelpers TryBinarySearch Range Result

## Summary

Added a small status-coded `TryBinarySearch` slice to `OS.Std.ArrayHelpers`.
The helper searches a caller-specified range in a sorted unmanaged span and
returns explicit found, not-found, invalid-range, insertion-index, range, and
source-length metadata without allocating or throwing for range validation.

## Why

Kernel-facing no-runtime helpers need no-throw result contracts for invalid
inputs. The existing `BinarySearch` preserved BCL-shaped integer
insertion-point results, while this slice gives future kernel/runtime callers a
result struct that rejects invalid ranges and default results as success
evidence.

## Files changed

- `std/no-runtime/shared/ArrayHelpers.cs`
- `tests/OS.HostTests/StdArrayHelpersTests.cs`
- `docs/nativeaot-nostdlib-limits.md`
- `done/step650.md`

## Tests run

- `dotnet build .\std\no-runtime\shared\OS.Std.NoRuntime.Shared.csproj --nologo -m:1 /p:UseSharedCompilation=false -nr:false`
- `dotnet build .\tests\OS.HostTests\OS.HostTests.csproj --nologo -m:1 /p:OSWorkloadProfile=SingleMinimalApi /p:UseSharedCompilation=false -nr:false`
- `dotnet run --project .\tests\OS.HostTests\OS.HostTests.csproj --no-build`
- `git diff --check -- .\docs\nativeaot-nostdlib-limits.md .\std\no-runtime\shared\ArrayHelpers.cs .\tests\OS.HostTests\StdArrayHelpersTests.cs`
- `git diff --no-index --check -- NUL .\done\step650.md`
- `git diff --no-index --check -- NUL .\std\no-runtime\shared\ArrayHelpers.cs`
- `git diff --no-index --check -- NUL .\tests\OS.HostTests\StdArrayHelpersTests.cs`

## Evidence

- Shared std build passed with 0 warnings and 0 errors.
- Focused host-test build passed with 0 warnings and 0 errors.
- Full host-test run emitted:
  - `TEST_PASS std.arrayhelpers clear fill unmanaged spans`
  - `TEST_PASS std.arrayhelpers indexof unmanaged spans`
  - `TEST_PASS std.arrayhelpers binarysearch insertion points`
  - `TEST_PASS std.arrayhelpers trybinarysearch found range`
  - `TEST_PASS std.arrayhelpers trybinarysearch notfound range`
  - `TEST_PASS std.arrayhelpers trybinarysearch invalid range`
  - `TEST_PASS std.arrayhelpers trybinarysearch default result is not success`
  - `TEST_PASS host.tests`
- `git diff --check` passed for touched files. Checks for untracked owned files
  used `git diff --no-index --check`.

## Failures

- None after retry.
- Earlier attempts were blocked by concurrent agents holding normal output
  files, including `tools\OS.QemuRunner\bin\Debug\net10.0\OS.QemuRunner.exe`
  and `tests\OS.HostTests\bin\Debug\net10.0\OS.Kernel.dll`. Those locks cleared
  before the final canonical build and run.

## Deferred work

- Broader `System.Array` compatibility, comparer-based search, sorting,
  allocation, resize, managed-reference arrays, covariance, and
  multidimensional arrays remain unsupported.
