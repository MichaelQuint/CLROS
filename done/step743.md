# Step 743 - MemoryExtensions Count And IndexOfAny

## Summary

Added byte-oriented `OS.Std.MemoryExtensions.Count` and `IndexOfAny`
overloads for two and three byte values. The implementation scans the span from
the beginning and returns the earliest source index even when search needles are
duplicated.

## Why

Networking, manifest, and package parsers keep accumulating local byte-span
loops. This fills another small no-runtime shared helper gap before more
kernel/app code depends on duplicated inline search/count logic.

## Files changed

- `std/no-runtime/shared/MemoryExtensions.cs`
  - Added `Count(ReadOnlySpan<byte>, byte)`.
  - Added `IndexOfAny(ReadOnlySpan<byte>, byte, byte)`.
  - Added `IndexOfAny(ReadOnlySpan<byte>, byte, byte, byte)`.
- `tests/OS.HostTests/StdMemoryExtensionsTests.cs`
  - Added focused markers for byte counting and any-of search, including empty
    spans, duplicate needles, missing values, and earliest-index behavior.
- `docs/nativeaot-nostdlib-limits.md`
  - Documented the expanded partial `OS.Std.MemoryExtensions` helper surface and
    the focused host-test markers.
- `done/step743.md`
  - Recorded this implementation slice and evidence.

## Tests run

- `dotnet build .\tests\OS.HostTests\OS.HostTests.csproj --nologo`
- `dotnet run --project .\tests\OS.HostTests\OS.HostTests.csproj --no-build`
- `dotnet build .\OS.slnx --nologo -m:1`
- `git diff --check`

## Evidence

- `artifacts/logs/issue705-host-build-20260525-115932.log`
  - Host-test project build succeeded with 0 warnings and 0 errors.
- `artifacts/logs/issue705-hosttests-20260525-115945.log`
  - Full host harness ended with `TEST_PASS host.tests`.
  - New std markers passed:
    - `TEST_PASS std.memoryextensions count byte values`
    - `TEST_PASS std.memoryextensions indexofany byte values`
- `artifacts/logs/issue705-solution-build-20260525-120328.log`
  - `dotnet build .\OS.slnx --nologo -m:1` succeeded with 0 warnings and
    0 errors.
- `artifacts/logs/issue705-git-diff-check-20260525-120342.log`
  - `git diff --check` exited 0; output contains LF/CRLF warnings only.

## Failures

No final verification failures.

Earlier during parallel work, the three-value `IndexOfAny` duplicate-needle
case returned 0 instead of 1. The final implementation scans source bytes first
and now passes the duplicate-needle host test.

## Deferred work

- Keep adding `OS.Std.MemoryExtensions` helpers only when kernel/app consumers
  need them and tests can define the no-runtime contract.
- The helper remains under `OS.Std`; no canonical `System.MemoryExtensions`
  compatibility claim was added.

## Evidence boundary

Host-tested std/no-runtime helper evidence only. This does not change boot,
runtime helper, live storage, live networking, QEMU, hosted .NET, Kestrel, or
cloud readiness status.
