# Step 739 - Phase C std byte-span Count and IndexOfAny

## Summary

- Added byte-only `OS.Std.MemoryExtensions.Count` and `IndexOfAny` helpers for
  `ReadOnlySpan<byte>`.
- Covered empty, missing, repeated, duplicate-needle, and earliest-index cases
  with focused host-test markers.
- Updated the no-stdlib limits doc with the precise MemoryExtensions status and
  test marker additions.

## Why

Phase C std work needs allocation-free byte-span query helpers that protocol and
parser code can share without pulling in LINQ, generic comparer paths,
exceptions, or hidden allocation behavior.

## Files changed

- `std/no-runtime/shared/MemoryExtensions.cs`
- `tests/OS.HostTests/StdMemoryExtensionsTests.cs`
- `docs/nativeaot-nostdlib-limits.md`
- `done/step739.md`

## Tests run

- `dotnet build .\tests\OS.HostTests\OS.HostTests.csproj --nologo`
- `dotnet run --project .\tests\OS.HostTests\OS.HostTests.csproj --no-build`
- `dotnet build .\OS.slnx --nologo -m:1`
- `git diff --check`

## Evidence

- Host test project build passed:
  `artifacts/logs/issue705-host-build-20260525-115932.log`
- Host tests passed:
  `artifacts/logs/issue705-hosttests-20260525-115945.log`
- Required markers:
  - `TEST_PASS std.memoryextensions count byte values`
  - `TEST_PASS std.memoryextensions indexofany byte values`
  - `TEST_PASS host.tests`
- Solution build passed:
  `artifacts/logs/issue705-solution-build-20260525-120328.log`
- Whitespace check passed with exit code 0:
  `artifacts/logs/issue705-git-diff-check-20260525-120342.log`

## Failures

- An initial host-test run failed because the new duplicate-needle
  `IndexOfAny` assertion expected index `1` while one of the needles matched
  byte `9` at index `0`. The test vector was corrected and the full host test
  run passed.
- The first solution build attempt hit transient output-file locks from the
  just-finished `OS.HostTests` process. The process had exited when checked,
  and a rerun passed.
- `git diff --check` emitted existing LF-to-CRLF working-copy warnings but no
  whitespace errors and returned exit code 0.

## Deferred work

- No networking, diagnostics, storage, or app-model files were changed.
- No runtime helper completion, QEMU boot proof, cloud readiness, or production
  readiness is claimed.
- Evidence is limited to host-tested `OS.Std` byte-span query helpers.
