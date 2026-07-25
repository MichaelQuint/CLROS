# Step 769 - Cctor runner preflight formatter shared ASCII helpers

## Summary
Migrated `NativeAotClassConstructorRunnerPreflightDiagnosticFormatter` to the
shared `OS.Std.Text.AsciiDecimalFormatter` digit-count helpers and
`OS.Std.Text.AsciiTextBuilder` append helpers. The
`NATIVEAOT_CCTOR_RUNNER_PREFLIGHT status=` diagnostic marker shape, required
byte calculation, short-destination behavior, and default-result rejection
remain unchanged.

## Why
The cctor runner preflight formatter is Phase D runtime diagnostic evidence for
already-produced class-constructor preflight metadata. It should use the shared
no-runtime ASCII helper surface instead of carrying formatter-local decimal
counting, digit-writing, or byte-copy loops.

## Files changed
- `src/OS.Kernel/Runtime/NativeAotClassConstructorRunnerPreflight.cs`
- `tests/OS.HostTests/NativeAotClassConstructorStateTableTests.cs`
- `docs/nativeaot-nostdlib-limits.md`
- `done/step769.md`

## Tests run
- `dotnet build .\tests\OS.HostTests\OS.HostTests.csproj --nologo -p:UseAppHost=false -o .\artifacts\issue728-cctor-runner-preflight-formatter\bin\Debug\net10.0 *>&1 | Tee-Object -FilePath .\artifacts\logs\issue728-host-build-20260525.log`
- `dotnet .\artifacts\issue728-cctor-runner-preflight-formatter\bin\Debug\net10.0\OS.HostTests.dll *>&1 | Tee-Object -FilePath .\artifacts\logs\issue728-hosttests-20260525.log`
- `git diff --check *>&1 | Tee-Object -FilePath .\artifacts\logs\issue728-git-diff-check-20260525.log`
- Clean integrated rerun:
  `dotnet .\artifacts\issue730-cpu-fault-panic-formatter\bin\Debug\net10.0\OS.HostTests.dll *>&1 | Tee-Object -FilePath .\artifacts\logs\issue730-hosttests-20260525.log`
- Clean integrated source check:
  `git diff --check *>&1 | Tee-Object -FilePath .\artifacts\logs\issue730-git-diff-check-20260525.log`

## Evidence
- Build log: `artifacts/logs/issue728-host-build-20260525.log`
  - `Build succeeded.`
  - `0 Warning(s)`
  - `0 Error(s)`
- Host test log: `artifacts/logs/issue728-hosttests-20260525.log`
  - `TEST_PASS runtime.cctor runner preflight ready`
  - `TEST_PASS runtime.cctor runner preflight rejects default table`
  - `TEST_PASS runtime.cctor runner preflight rejects failed state`
  - `TEST_PASS runtime.cctor runner preflight rejects pending state`
  - `TEST_PASS runtime.cctor runner preflight rejects missing metadata`
  - `TEST_PASS runtime.cctor runner preflight formatter reports short destination`
  - `TEST_PASS runtime.cctor runner preflight formatter uses shared ascii text builder`
  - `TEST_PASS boot.sequence cctor runner preflight ready`
  - `TEST_PASS boot.sequence cctor runner preflight rejects failed result`
  - `TEST_PASS boot.sequence cctor runner preflight rejects default result`
  - `TEST_PASS boot.sequence cctor runner preflight preserves default markers`
  - `TEST_PASS host.tests`
- Diff-check log: `artifacts/logs/issue728-git-diff-check-20260525.log`
  - The required command produced existing Git LF-to-CRLF working-copy warnings
    from the dirty shared tree.
  - No `git diff --check` whitespace-error diagnostics were present.
- Clean integrated host-test rerun:
  `artifacts/logs/issue730-hosttests-20260525.log`
  - `TEST_PASS runtime.cctor runner preflight formatter uses shared ascii text builder`
  - `TEST_PASS host.tests`
- Clean integrated source check:
  `artifacts/logs/issue730-git-diff-check-20260525.log`
  exited 0 with only existing LF-to-CRLF working-copy warnings.

## Failures
- None in the focused build or full host-test run.
- The required `git diff --check` PowerShell pipeline returned a nonzero
  process result after Git wrote existing LF-to-CRLF warnings to stderr in the
  dirty shared worktree. No whitespace-error diagnostics were present in the
  saved log. The clean integrated source check exited 0 with the same class of
  warnings and no issue-specific whitespace error.

## Deferred work
- Wire cctor preflight diagnostics into a future live runtime cctor runner only
  after metadata discovery, constructor invocation, delegate dispatch, lazy
  static reference handling, exception flow, and module initialization have
  separate evidence.

## Evidence boundary
This step proves caller-owned byte-buffer formatting for already-produced
NativeAOT cctor runner preflight results and source-guards use of the shared
ASCII text builder and decimal formatter. It does not discover metadata, invoke
constructors, call delegates, run module initializers, handle cctor exceptions,
support lazy static reference fields, emit QEMU cctor markers, complete
NativeAOT runtime helper support, start hosted .NET, start Kestrel, or provide
cloud-provider evidence.

