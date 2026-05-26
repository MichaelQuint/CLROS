# Step 594 - OS.Std NativeInt pointer-sized arithmetic

## Summary

Added a small `OS.Std.NativeInt` helper slice for status-coded `nuint`
arithmetic and alignment planning.

## Why

Low-level boot, runtime, MMIO, and page-planning code repeatedly needs checked
pointer-sized addition, subtraction, and power-of-two alignment behavior. Keeping
that behavior in `std/no-runtime/shared` avoids ad hoc arithmetic helpers in
kernel layers and keeps overflow failures explicit.

## Files changed

- `std/no-runtime/shared/NativeInt.cs`
- `tests/OS.HostTests/StdNativeIntTests.cs`
- `docs/nativeaot-nostdlib-limits.md`
- `docs/completion-gates.md`
- `done/step594.md`

## Tests run

- `dotnet build .\tests\OS.HostTests\OS.HostTests.csproj --nologo`
- `dotnet run --project .\tests\OS.HostTests\OS.HostTests.csproj --no-build | Select-String -Pattern 'std\.nativeint|TEST_FAIL|TEST_PASS host\.tests'`

## Evidence

- Host-test project build passed with 0 warnings and 0 errors.
- Focused host-test output included:
  - `TEST_PASS std.nativeint checked arithmetic`
  - `TEST_PASS std.nativeint alignment helpers`
  - `TEST_PASS std.nativeint default result is not success`
  - `TEST_PASS host.tests`

## Failures

- No test failures in the focused commands above.

## Deferred work

- This does not implement `System.IntPtr`/`System.UIntPtr` replacement types,
  signed pointer arithmetic, unsafe memory access, page-table mutation,
  NativeAOT allocation helpers, GC integration, or hosted .NET runtime support.

