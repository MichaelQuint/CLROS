# Step 763 - Argument Vector Formatter Shared ASCII Helpers

## Summary

`NativeAppArgumentVectorDiagnosticFormatter` now uses the shared
`std/no-runtime/shared` ASCII decimal and text-builder helpers for
byte-counting and caller-owned marker writes. The host test suite has a source
guard to prevent the app-model formatter from growing local decimal loops again.

## Why

The native-tier app model is accumulating diagnostic markers that must be safe
for no-runtime and future kernel use. Routing argument-vector marker formatting
through `OS.Std.Text.AsciiDecimalFormatter` and `OS.Std.Text.AsciiTextBuilder`
keeps numeric formatting centralized before these markers become part of
firmware-independent boot/app launch evidence.

## Files changed

- `src/OS.Kernel/Manifest/NativeAppArgumentVectorDiagnosticFormatter.cs`
- `tests/OS.HostTests/NativeAppArgumentVectorPlanTests.cs`
- `docs/app-model.md`
- `docs/nativeaot-nostdlib-limits.md`
- `done/step763.md`
- `artifacts/logs/issue722-host-build-20260525.log`
- `artifacts/logs/issue722-hosttests-20260525.log`
- `artifacts/logs/issue722-git-diff-check-postdone-20260525.log`

## Tests run

- `dotnet build .\tests\OS.HostTests\OS.HostTests.csproj --nologo -p:UseAppHost=false -o .\artifacts\issue722-args-vector-formatter\bin\Debug\net10.0`
- `dotnet .\artifacts\issue722-args-vector-formatter\bin\Debug\net10.0\OS.HostTests.dll`
- `git diff --check`

## Evidence

- Build log: `artifacts/logs/issue722-host-build-20260525.log`
  - `Build succeeded.`
  - `0 Warning(s)`
  - `0 Error(s)`
- Host-test log: `artifacts/logs/issue722-hosttests-20260525.log`
  - `TEST_PASS appmodel args vector formatter formats success marker`
  - `TEST_PASS appmodel args vector formatter reports short destination`
  - `TEST_PASS appmodel args vector formatter rejects default result`
  - `TEST_PASS appmodel args vector formatter uses shared ascii text builder`
  - `TEST_PASS host.tests`
- Diff-check log: `artifacts/logs/issue722-git-diff-check-postdone-20260525.log`
  - `git diff --check` exited 0.
  - The log contains existing Git LF-to-CRLF working-copy warnings from the dirty
    tree and no whitespace-error failure.

## Failures

- None in the focused build, host-test run, or diff check.

## Deferred work

- This does not materialize process arguments, load or execute a native app,
  wire the readiness marker into the default boot path, start hosted .NET, or
  start Kestrel.
- Remaining app-model/network diagnostic formatters still need separate slices
  before the local formatting backlog is gone.

## Evidence boundary

This step proves caller-owned byte-buffer formatting for already-produced
argument-vector planner results. It is host-tested formatter evidence only; it
does not prove live manifest storage reads, process argument delivery, native app
execution, QEMU boot behavior, hosted .NET, Kestrel, or cloud readiness.
