# Step 801 - Native App Argument Handoff Plan

## Summary

Added a host-tested native app argument handoff metadata planner. The planner
composes an already-produced `NativeAppInvocationPlanResult` with an
already-produced `NativeAppArgumentVectorPlanResult` and records only primitive
metadata for future launcher work.

## Why

Issue #760 needs the app-model/runtime boundary to connect invocation metadata
with parsed manifest arguments before any final app ABI or execution path is
defined. This step keeps that handoff status-coded, default-result hardened, and
explicitly below app execution.

## Files changed

- `src/OS.Kernel/Runtime/NativeAppArgumentHandoffPlan.cs`
- `tests/OS.HostTests/NativeAppArgumentHandoffPlanTests.cs`
- `docs/app-model.md`
- `docs/completion-gates.md`
- `done/step801.md`

## Tests run

- `dotnet build .\tests\OS.HostTests\OS.HostTests.csproj --nologo`
  - Result: pass
- `dotnet .\tests\OS.HostTests\bin\Debug\net10.0\OS.HostTests.dll`
  - Result: failed after the new handoff tests passed
  - Failure: unrelated existing host-test failure
    `TEST_FAIL qemu smoke script dry-run propagates workload profile`, followed
    by `TEST_FAIL qemu.runner.tests failures=1`.
- `git diff --check -- src/OS.Kernel/Runtime/NativeAppArgumentHandoffPlan.cs tests/OS.HostTests/NativeAppArgumentHandoffPlanTests.cs docs/app-model.md docs/completion-gates.md done/step801.md`
  - Result: pass
  - Notes: Git emitted existing line-ending normalization warnings for the two
    docs.

## Evidence

- `TEST_START done.step.allocate`
- `NEXT_DONE_STEP done/step801.md`
- `TEST_PASS done.step.allocate`
- `TEST_PASS appmodel invocation args handoff composes vector`
- `TEST_PASS appmodel invocation args handoff rejects invocation failure`
- `TEST_PASS appmodel invocation args handoff rejects argument-vector failure`
- `TEST_PASS appmodel invocation args handoff rejects parsed-byte mismatch`
- `TEST_PASS appmodel invocation args handoff rejects default dependency results`
- `TEST_PASS appmodel invocation args handoff rejects impossible counts`
- `TEST_PASS appmodel invocation args handoff default result is not success`
- `TEST_PASS appmodel invocation args handoff layout contains no managed references`

## Failures

- The full host-test DLL currently fails outside this step's files:
  `TEST_FAIL qemu smoke script dry-run propagates workload profile` and
  `TEST_FAIL qemu.runner.tests failures=1`.

## Deferred work

- No final `argc`/`argv` ABI is defined.
- No stack allocation, stack switching, thread creation, hosted .NET startup,
  Kestrel startup, or app-code execution is implemented or claimed.
