# Step 748 - Char Span MemoryExtensions Slice

## Summary

Added a host-tested char-span slice to `OS.Std.MemoryExtensions`.

## Why

Phase C needs BCL-shaped shared helpers to exist in `std/no-runtime/shared`
before kernel, app, manifest, and protocol code grow local utility loops. The
existing helper was byte-oriented; this step adds the narrow char-oriented
surface needed for portable text code while keeping the implementation
allocation-free and documented as a partial `OS.Std` subset.

## Files changed

- `std/no-runtime/shared/MemoryExtensions.cs`
  - Added `ReadOnlySpan<char>` overloads for `IndexOf(char)`,
    `LastIndexOf(char)`, `SequenceEqual(ReadOnlySpan<char>)`,
    `StartsWith(ReadOnlySpan<char>)`, `EndsWith(ReadOnlySpan<char>)`,
    `CopyTo(Span<char>)`, and `TryCopyTo(Span<char>)`.
  - Preserved BCL-shaped empty prefix/suffix behavior, `-1` search misses,
    overlap-safe copy behavior, throwing `CopyTo` for too-small destinations,
    and no-write `TryCopyTo` failure.
- `tests/OS.HostTests/StdMemoryExtensionsTests.cs`
  - Added focused char search, sequence/prefix/suffix, `CopyTo`, and
    `TryCopyTo` markers.
- `docs/nativeaot-nostdlib-limits.md`
  - Documented the supported char-span subset, tests, and namespace boundary.
- `done/step748.md`
  - Recorded this implementation slice and evidence.

## Tests run

- `dotnet build .\tests\OS.HostTests\OS.HostTests.csproj --nologo -p:UseAppHost=false -o .\artifacts\issue3-std-char-memoryextensions\bin\Debug\net10.0`
- `dotnet .\artifacts\issue3-std-char-memoryextensions\bin\Debug\net10.0\OS.HostTests.dll`
- `git diff --check`

## Evidence

- `artifacts/logs/issue3-std-char-memoryextensions-host-build-final-20260525.log`
  - Host-test project build succeeded with 0 warnings and 0 errors.
- `artifacts/logs/issue3-std-char-memoryextensions-hosttests-final-20260525.log`
  - Full shaped host harness ended with `TEST_PASS host.tests`.
  - Focused char markers passed:
    - `TEST_PASS std.memoryextensions char search`
    - `TEST_PASS std.memoryextensions char sequence prefix suffix`
    - `TEST_PASS std.memoryextensions char copyto`
    - `TEST_PASS std.memoryextensions char trycopyto`

## Failures

- `artifacts/logs/issue3-std-char-memoryextensions-hosttests-20260525.log`
  captured an initial failed host-test run because the new test expected the
  last `p` in `alpha beta alpha` at index 12 instead of the correct index 13.
  The assertion was corrected and the full shaped host harness was rerun
  successfully.

## Deferred work

- Add broader char helpers only when a caller needs them, with tests and docs.
- Keep this type under `OS.Std` until the supported surface is broad enough and
  compatible enough to justify any canonical `System.MemoryExtensions`
  exposure.

## Evidence boundary

Host-tested `std/no-runtime/shared` utility evidence only. This does not
complete the BCL, NativeAOT runtime helpers, exception handling, hosted .NET 10,
Kestrel, QEMU boot behavior, cloud readiness, or production readiness.

