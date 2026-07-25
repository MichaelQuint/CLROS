# Step 601 - Std Integer Math Helper Slice

## Summary

Added a small host-tested `OS.Std.Math` helper slice for integer `Min`, `Max`,
`Clamp`, and `Abs` behavior in the shared no-runtime project.

## Why

The CloudReady std/runtime backlog requires BCL-like numeric helpers to land in
`std/no-runtime/shared` before kernel, app, or PAL code depends on them. This
slice closes one narrow documented gap without claiming a complete `System.Math`
replacement or NativeAOT runtime completion.

## Files changed

- `std/no-runtime/shared/Math.cs`
  - Adds partial `OS.Std.Math` helper methods for `int`, `uint`, `long`, and
    `ulong` where applicable.
  - Preserves BCL-shaped throwing behavior for invalid `Clamp` bounds and
    `Abs` overflow on signed minimum values.
- `tests/OS.HostTests/StdMathTests.cs`
  - Adds module-initializer host tests with stable `std.math` markers.
- `docs/nativeaot-nostdlib-limits.md`
  - Documents the implemented behavior, tests, known limitations, and remaining
    no-runtime math work.

## Tests run

- `dotnet build .\tests\OS.HostTests\OS.HostTests.csproj --nologo`
- `dotnet build .\tests\OS.HostTests\OS.HostTests.csproj --nologo -t:Rebuild /p:OSWorkloadProfile=SingleMinimalApi /p:UseSharedCompilation=false -nr:false`
- `dotnet .\tests\OS.HostTests\bin\Debug\net10.0\OS.HostTests.dll | Select-String -Pattern 'TEST_FAIL|Unhandled|Exception|std\.math|host\.tests'`
- `git diff --check -- std/no-runtime/shared/Math.cs tests/OS.HostTests/StdMathTests.cs docs/nativeaot-nostdlib-limits.md done/step601.md`

## Evidence

- Host-test build completed successfully with `0 Warning(s)` and `0 Error(s)`.
- Forced SingleMinimalApi rebuild completed successfully with `0 Warning(s)` and
  `0 Error(s)` after clearing stale generated-output locks.
- Focused host-test marker scan emitted:
  - `TEST_PASS std.math min max integer helpers`
  - `TEST_PASS std.math clamp integer bounds`
  - `TEST_PASS std.math abs overflow behavior`
  - `TEST_PASS host.tests`
- Scoped `git diff --check` passed; it only emitted the existing
  LF-to-CRLF warning for `docs/nativeaot-nostdlib-limits.md`.

## Failures

- A post-doc rebuild initially hit stale generated-output locks from prior
  `OS.HostTests.exe` processes and a reusable compiler server. The stale test
  processes were stopped after they remained alive past the failed harness run,
  `dotnet build-server shutdown` was run, and the final rebuild passed.
- `dotnet run --project .\tests\OS.HostTests\OS.HostTests.csproj --no-build`
  could not start `OS.HostTests.exe` after the forced rebuild because the apphost
  executable was absent; the final focused evidence used the built DLL directly.

## Deferred work

- This is not a complete `System.Math` or `System.MathF` compatibility surface.
- Floating-point, decimal, rounding, trigonometric, and remaining overload work
  remains deferred.
- Throwing paths are host-tested only; kernel paths without managed exception
  support should use status-coded alternatives or avoid those failure paths.
