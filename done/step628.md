# Step 628 - Native App Argument Vector Planning

## Summary

Added a host-tested native app argument-vector planner. The planner consumes a
successful `NativeAppLaunchPlanResult`, splits the source-backed `args=` bytes
into a bounded vector of source-backed argument spans, supports quoted spans for
arguments containing spaces, and rejects malformed quotes, too many arguments,
overlong arguments, failed launch plans, and default/zero results.

## Why

The native-tier app model already carries raw manifest argument bytes, but the
future launcher needs deterministic argument metadata before it can expose args
to a workload. This slice proves that argument discovery can happen without
copying strings, allocating app process state, or executing an app.

## Files changed

- `src/OS.Kernel/Manifest/NativeAppArgumentVectorPlan.cs`
- `tests/OS.HostTests/NativeAppArgumentVectorPlanTests.cs`
- `docs/app-model.md`
- `docs/manifest.md`
- `docs/completion-gates.md`
- `done/step628.md`

## Tests run

- `dotnet build .\tests\OS.HostTests\OS.HostTests.csproj --nologo -m:1 /p:OSWorkloadProfile=SingleMinimalApi /p:UseSharedCompilation=false -nr:false`
- `.\tests\OS.HostTests\bin\Debug\net10.0\OS.HostTests.exe` with output
  saved to `artifacts\logs\host-tests-step628.log`.

## Evidence

- Targeted host-test build completed with 0 warnings and 0 errors.
- Full host-test log emitted `TEST_PASS host.tests`.
- New markers in `artifacts\logs\host-tests-step628.log`:
  - `TEST_PASS appmodel args vector splits manifest args`
  - `TEST_PASS appmodel args vector preserves quoted spaces`
  - `TEST_PASS appmodel args vector accepts empty args`
  - `TEST_PASS appmodel args vector rejects too many args`
  - `TEST_PASS appmodel args vector rejects malformed quotes`
  - `TEST_PASS appmodel args vector rejects failed launch plan`
  - `TEST_PASS appmodel args vector default result is not success`

## Failures

- No build or host-test failures were observed for this slice.

## Deferred work

- Wire the argument vector into invocation and launch metadata only after the
  native app launcher has a concrete ABI surface for passing argc/argv-like
  data.
- Preserve the current no-execution boundary until storage, mapping,
  permissions, stack setup, and app entry invocation are implemented and tested.

## Evidence boundary

This is host-tested argument metadata planning only. It does not execute an app,
allocate an app stack, copy process strings, define the final app ABI, route
stdout/stderr, read live storage, start hosted .NET 10, or prove Kestrel/cloud
readiness.

