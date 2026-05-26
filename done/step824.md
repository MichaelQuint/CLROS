# Step 824 - Diagnostic Stub Callable Binding Formatter

## Summary

Added a caller-owned diagnostic formatter for
`DiagnosticStubCallableBindingPlan` so callable-stub binding readiness can be
reported as stable ASCII marker bytes before any live unmanaged binding or COM1
port-I/O exists.

## Why

Gitea #782 needed the existing callable-binding preflight to have the same
machine-readable marker coverage as adjacent diagnostics readiness contracts.
The formatter keeps the boundary value-only and status-coded: it formats
success and failure metadata, reports destination capacity, rejects default or
invalid status metadata, and uses shared ASCII text helpers instead of
diagnostics-local append logic.

## Files changed

- `src/OS.Kernel/Diagnostics/DiagnosticStubCallableBindingPlanDiagnosticFormatter.cs`
- `tests/OS.HostTests/DiagnosticStubCallableBindingPlanDiagnosticFormatterTests.cs`
- `docs/diagnostics.md`
- `done/step824.md`

## Tests run

- `dotnet build .\tests\OS.HostTests\OS.HostTests.csproj --nologo`
- `dotnet .\tests\OS.HostTests\bin\Debug\net10.0\OS.HostTests.dll`
- Scoped issue #782 host-test builds/runs in temporary logs.

## Evidence

- `artifacts/logs/issue777-repro-host-tests.log` contains
  `TEST_PASS host.tests` and all required #782 markers:
  - `TEST_PASS diagnostics.stub_callable formatter formats success marker`
  - `TEST_PASS diagnostics.stub_callable formatter formats failure marker`
  - `TEST_PASS diagnostics.stub_callable formatter reports short destination`
  - `TEST_PASS diagnostics.stub_callable formatter rejects default result`
  - `TEST_PASS diagnostics.stub_callable formatter rejects invalid status`
  - `TEST_PASS diagnostics.stub_callable formatter uses shared ascii text builder`
  - `TEST_PASS diagnostics.stub_callable formatter result contains no managed references`
- `artifacts/logs/issue782-hosttests-build.log`,
  `artifacts/logs/issue782-hosttests-build-isolated.log`, and
  `artifacts/logs/issue782-kernel-build.log` contain build evidence from the
  worker's scoped runs.
- `artifacts/logs/issue782-hosttests-run-temp.log` and
  `artifacts/logs/issue782-hosttests-run-temp-git.log` contain the worker's
  scoped host-test execution output.

## Failures

- None in the final full host executable run.

## Deferred work

- This is host-tested marker-format evidence only. It does not bind delegates,
  execute generated stubs, perform CPU port I/O, write serial output, mutate
  page permissions, install descriptor tables, or prove QEMU panic behavior.

