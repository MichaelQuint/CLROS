# Step 282 - Native App Lifecycle Readiness Boundary

## What Changed

- Added `NativeAppLifecyclePlanner` as a host-tested readiness planner between
  app invocation metadata and future launcher/supervisor execution.
- Added `NativeAppLogSinkReadiness` to derive bounded app-log readiness from
  `OSHostAppLogTable` capacity and record-size limits.
- Added `NativeAppLifecyclePlanResult` and status values for invocation-plan
  failure, invalid app identity, and app-log unavailability.
- Added `BootSequence.RunAppLifecycleReadiness` with stable
  `APP_LIFECYCLE_READY` and
  `APP_LIFECYCLE_NOT_READY status=<status> invocation=<status> log=<status>`
  markers.
- Added host tests for lifecycle planning and boot marker aggregation.
- Updated boot, boot-order, app-model, and completion-gate docs.

## Why It Changed

The app launch chain needed one more explicit boundary after invocation planning
and app-log readiness, but before any supervisor, app ABI, stack switching, or
app execution exists. This makes future launcher work consume a small
status-coded readiness object instead of inferring lifecycle readiness from
several unrelated contracts.

## Files Changed

- `src/OS.Kernel/Runtime/NativeAppLifecyclePlan.cs`
- `src/OS.Kernel/Boot/BootSequence.cs`
- `tests/OS.HostTests/NativeAppLifecyclePlanTests.cs`
- `tests/OS.HostTests/BootSequenceAppLifecycleReadinessTests.cs`
- `docs/app-model.md`
- `docs/boot.md`
- `docs/boot-order.md`
- `docs/completion-gates.md`
- `done/step282.md`

## Tests Run

```powershell
dotnet build .\OS.slnx --nologo -m:1
dotnet .\tests\OS.HostTests\bin\Debug\net10.0\OS.HostTests.dll
```

## Logs And Evidence

The solution build passed:

```text
Build succeeded.
0 Warning(s)
0 Error(s)
```

The focused host-test run emitted:

```text
TEST_PASS boot.sequence app lifecycle ready
TEST_PASS boot.sequence app lifecycle rejects dependency failure
TEST_PASS boot.sequence app lifecycle preserves default markers
TEST_PASS appmodel.lifecycle accepts invocation and log sink
TEST_PASS appmodel.lifecycle rejects invocation failure
TEST_PASS appmodel.lifecycle rejects missing log sink
TEST_PASS appmodel.lifecycle preserves app identity
TEST_PASS host.tests
```

## Failures

No failures were observed for this slice.

## Evidence Boundary

This is host-only readiness evidence. It does not prove live package loading,
app supervisor state, stdout/stderr routing, restart policy, exit-code capture,
native app execution, hosted .NET 10, ASP.NET Minimal API, or Kestrel behavior.

## Deferred Work

- Wire lifecycle readiness into the future launcher after live package loading
  and app ABI work exist.
- Connect app-log and app-exit markers to real guest app execution.
- Teach QEMU smoke to require lifecycle markers only after the live boot path
  can emit them from guest execution context.
