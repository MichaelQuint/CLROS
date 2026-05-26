# Step 805 - Native App Argument Handoff BootSequence Readiness

## Summary

Added a host-tested `BootSequence` marker boundary for native app argument
handoff readiness. `BootSequence.RunNativeAppArgumentHandoffReadiness` now
emits `APP_ARGS_HANDOFF_READY` for initialized successful handoff plans and
status-coded `APP_ARGS_HANDOFF_NOT_READY` markers for failed or default plans.

## Why

The app model already had an invocation-plus-argument-vector handoff planner,
but boot evidence could not expose that handoff as an ordered readiness marker.
This step gives future app-launch wiring a stable serial/event marker without
changing the default boot order or claiming app execution.

## Files changed

- `src/OS.Kernel/Boot/BootSequence.cs`
- `tests/OS.HostTests/NativeAppArgumentHandoffPlanTests.cs`
- `docs/app-model.md`
- `docs/boot.md`
- `docs/completion-gates.md`
- `done/step805.md`

## Tests run

- `dotnet build .\tests\OS.HostTests\OS.HostTests.csproj --nologo`
  - Result: pass
- `dotnet run --project .\tests\OS.HostTests\OS.HostTests.csproj --no-build`
  - Result: pass
- Scoped `git diff --check` for this slice
  - Result: pass
- Scoped trailing-whitespace scan for this slice
  - Result: pass

## Evidence

- `artifacts/logs/issue763-host-build.log`
  - `Build succeeded.`
  - `0 Warning(s)`
  - `0 Error(s)`
- `artifacts/logs/issue763-host-tests.log`
  - `TEST_PASS boot.sequence app args handoff ready`
  - `TEST_PASS boot.sequence app args handoff rejects failed result`
  - `TEST_PASS boot.sequence app args handoff rejects default result`
  - `TEST_PASS boot.sequence app args handoff preserves default markers`
  - `TEST_PASS host.tests`
- `artifacts/logs/issue763-diff-check.log`
  - `git-diff-check-exit=0`
- `artifacts/logs/issue763-trailing-whitespace.log`
  - `TEST_PASS issue763.trailing_whitespace`

## Failures

An initial build failed because the new test-local `ISerialSink` helper needed
the diagnostics namespace import. The import was added and the subsequent
build and full host-test run passed.

## Deferred work

This is BootSequence marker/readiness evidence only. It does not wire the
marker into the default boot path, perform live app invocation, define a final
native app ABI, materialize `argc`/`argv`, allocate or switch stacks, start
hosted .NET, start ASP.NET Minimal API, start Kestrel, prove cloud readiness,
or prove production readiness.

