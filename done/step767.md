# Step 767 - Cctor work-item formatter shared ASCII helpers

## Summary
Migrated `NativeAotClassConstructorWorkItemPlanDiagnosticFormatter` to the
shared `OS.Std.Text.AsciiDecimalFormatter` and
`OS.Std.Text.AsciiTextBuilder` helpers. The
`NATIVEAOT_CCTOR_WORK_ITEMS status=` marker shape remains stable while removing
formatter-local int/uint decimal byte-count helpers, append helpers, and direct
digit writes.

## Why
The cctor work-item formatter is Phase D runtime diagnostic evidence for
already-produced class-constructor work-item plans. It should use the shared
no-runtime ASCII helper surface so integer formatting behavior stays
centralized and source-guarded before future cctor runner diagnostics build on
the same pattern.

## Files changed
- `src/OS.Kernel/Runtime/NativeAotClassConstructorWorkItemPlanDiagnosticFormatter.cs`
- `tests/OS.HostTests/NativeAotClassConstructorWorkItemPlanTests.cs`
- `docs/nativeaot-nostdlib-limits.md`
- `done/step767.md`

## Tests run
- `dotnet build .\tests\OS.HostTests\OS.HostTests.csproj --nologo -p:UseAppHost=false -o .\artifacts\issue724-cctor-work-item-formatter\bin\Debug\net10.0`
- `dotnet .\artifacts\issue724-cctor-work-item-formatter\bin\Debug\net10.0\OS.HostTests.dll`
- `git diff --check`

## Evidence
- Build log: `artifacts/logs/issue724-host-build-20260525.log`
  - `Build succeeded.`
  - `0 Warning(s)`
  - `0 Error(s)`
- Host test log: `artifacts/logs/issue724-hosttests-20260525.log`
  - `TEST_PASS runtime.cctor work item plans uninitialized entries`
  - `TEST_PASS runtime.cctor work item skips initialized entries`
  - `TEST_PASS runtime.cctor work item rejects missing target token`
  - `TEST_PASS runtime.cctor work item rejects missing target metadata`
  - `TEST_PASS runtime.cctor work item reports destination capacity`
  - `TEST_PASS runtime.cctor work item rejects failed preflight`
  - `TEST_PASS runtime.cctor work item rejects pending preflight`
  - `TEST_PASS runtime.cctor work item rejects default preflight`
  - `TEST_PASS runtime.cctor work item formatter formats success marker`
  - `TEST_PASS runtime.cctor work item formatter formats failure marker`
  - `TEST_PASS runtime.cctor work item formatter reports short destination`
  - `TEST_PASS runtime.cctor work item formatter rejects default result`
  - `TEST_PASS runtime.cctor work item formatter uses shared ascii text builder`
  - `TEST_PASS runtime.cctor work item layouts contain no managed references`
  - `TEST_PASS host.tests`
- Diff-check log: `artifacts/logs/issue724-git-diff-check-20260525.log`
  - `git diff --check` exited 0.
  - The log contains existing Git LF-to-CRLF working-copy warnings from the
    dirty tree and no whitespace-error failure.

## Failures
- None in the focused build or host-test run.
- `git diff --check` produced existing LF-to-CRLF working-copy warnings from
  unrelated dirty files in the shared worktree, but Git returned exit code 0 and
  no whitespace-error diagnostics were present.

## Deferred work
- Wire cctor work-item planning into a future live runtime cctor runner only
  after metadata discovery, constructor invocation, delegate dispatch, lazy
  static reference handling, exception flow, and module initialization have
  separate evidence.
- Continue migrating remaining runtime diagnostic formatters that still carry
  local integer formatting loops.

## Evidence boundary
This step proves caller-owned byte-buffer formatting for already-produced
NativeAOT cctor work-item plan results and source-guards use of the shared
ASCII text builder and decimal formatter. It does not discover metadata, invoke
constructors, call delegates, run module initializers, handle cctor exceptions,
support lazy static reference fields, emit QEMU cctor markers, complete
NativeAOT runtime helper support, start hosted .NET, start Kestrel, or provide
cloud-provider evidence.

