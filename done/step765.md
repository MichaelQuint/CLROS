# Step 765 - Diagnostic stub invocation formatter shared ASCII writer

## Summary

`DiagnosticStubInvocationReadinessFormatter` now writes diagnostic-stub
address and byte-count fields through `OS.Std.Text.AsciiTextBuilder` instead of
the formatter-local unsigned decimal writer. The formatter already used shared
decimal byte-count helpers; this step completes the write-side migration and
strengthens the host source guard.

## Why

Diagnostic-stub invocation markers are part of the early diagnostics evidence
chain. Their caller-owned marker formatting must stay on the shared no-runtime
ASCII helper surface so future panic/serial code does not reintroduce local
numeric formatting loops in diagnostic paths.

## Files changed

- `src/OS.Kernel/Diagnostics/DiagnosticStubInvocationReadinessFormatter.cs`
- `tests/OS.HostTests/DiagnosticStubInvocationReadinessFormatterTests.cs`
- `docs/diagnostics.md`
- `done/step765.md`
- `artifacts/logs/issue725-host-build-20260525.log`
- `artifacts/logs/issue725-hosttests-20260525.log`
- `artifacts/logs/issue725-git-diff-check-20260525.log`

## Tests run

- `dotnet build .\tests\OS.HostTests\OS.HostTests.csproj --nologo -p:UseAppHost=false -o .\artifacts\issue725-diagnostic-stub-formatter\bin\Debug\net10.0`
- `dotnet .\artifacts\issue725-diagnostic-stub-formatter\bin\Debug\net10.0\OS.HostTests.dll`
- `git diff --check`

## Evidence

- Build log: `artifacts/logs/issue725-host-build-20260525.log`
  - `Build succeeded.`
  - `0 Warning(s)`
  - `0 Error(s)`
- Host-test log: `artifacts/logs/issue725-hosttests-20260525.log`
  - `TEST_PASS diagnostics.stub_invocation formatter formats ready marker`
  - `TEST_PASS diagnostics.stub_invocation formatter formats not-ready marker`
  - `TEST_PASS diagnostics.stub_invocation formatter reports short destination`
  - `TEST_PASS diagnostics.stub_invocation formatter rejects invalid status`
  - `TEST_PASS diagnostics.stub_invocation formatter uses shared ascii text builder`
  - `TEST_PASS diagnostics.stub_invocation formatter default result is not success`
  - `TEST_PASS host.tests`
- Diff-check log: `artifacts/logs/issue725-git-diff-check-20260525.log`
  - `git diff --check` exited 0.
  - The log contains existing Git LF-to-CRLF working-copy warnings from the dirty
    tree and no whitespace-error failure.

## Failures

- None in the focused build, host-test run, or diff check.
- No QEMU smoke was run; this is host-tested marker formatting only.

## Deferred work

- This does not invoke generated stubs, bind unmanaged delegates, perform CPU
  port I/O, mutate page permissions, replace the managed `BootSequence` marker
  path, or prove QEMU serial output from an OS-owned COM1 backend.
- Continue migrating remaining diagnostic/runtime/app/network formatters that
  still carry local decimal writers.

## Evidence boundary

This step proves caller-owned marker formatting for already-produced diagnostic
stub invocation readiness results. It does not prove live descriptor-table
installation, live COM1 port I/O, generated-stub invocation, panic recovery,
QEMU behavior, hosted .NET, Kestrel, or cloud readiness.
