# Step 277 - PAL Monotonic Timer Table

## What Changed

- Added `OSHostTimerTable` as a host-tested in-process PAL timer model.
- Added opaque generation-based timer handles with deterministic capacity exhaustion and stale-handle rejection.
- Added deterministic monotonic snapshots with stable tick frequency and backwards-time rejection.
- Added one-shot timer arm/fire, cancel-before-fire, periodic catch-up, close, and invalid option handling.
- Added `OSHostTimeoutEvaluationResult` and `OSHostTimeoutState` for status-coded timeout/deadline comparison against the mock monotonic tick source.
- Added host tests for the timer table and updated PAL documentation.

## Why It Changed

ASP.NET Minimal API/Kestrel and the future hosted .NET 10 runtime need a PAL
time contract for request timeouts, keep-alives, cancellation, and scheduler
prerequisites. This step defines and tests the contract shape before any
exported `OSHost_*` timer symbols, guest scheduler, APIC/HPET/TSC timer, or
hosted runtime integration exists.

## Files Changed

- `src/OS.Kernel/Pal/OSHostTimerTable.cs`
- `tests/OS.HostTests/PalTimerTableTests.cs`
- `docs/pal.md`
- `done/step277.md`

## Tests Run

```powershell
dotnet build .\tests\OS.HostTests\OS.HostTests.csproj --nologo -m:1
dotnet run --project .\tests\OS.HostTests\OS.HostTests.csproj --no-build
dotnet build .\OS.slnx --nologo -m:1
.\scripts\check-naming.ps1
git diff --check
```

## Logs And Evidence

`dotnet build .\tests\OS.HostTests\OS.HostTests.csproj --nologo -m:1`
passed:

```text
Build succeeded.
0 Warning(s)
0 Error(s)
```

The full host-test run exited `0` and emitted:

```text
TEST_PASS pal.timer monotonic snapshot is stable
TEST_PASS pal.timer one shot arms and fires
TEST_PASS pal.timer cancel prevents fire
TEST_PASS pal.timer periodic advances deterministically
TEST_PASS pal.timer timeout deadline comparison is deterministic
TEST_PASS pal.timer invalid handles are rejected
TEST_PASS host.tests
```

`dotnet build .\OS.slnx --nologo -m:1` passed:

```text
Build succeeded.
0 Warning(s)
0 Error(s)
```

`.\scripts\check-naming.ps1` passed:

```text
TEST_PASS naming.guard
TEST_PASS source.firewall
```

`git diff --check` exited `0`. It printed only existing LF-to-CRLF normalization
warnings from the dirty worktree.

## Failures

An intermediate build during development saw missing `EvaluateRelativeTimeout`
and `EvaluateAbsoluteTimeout` methods while the timeout comparison helper was
only partially applied. The final implementation defines both helpers in
`OSHostTimerTable`, and the focused and full builds now pass.

## Evidence Boundary

This is host-only PAL contract evidence. It does not export
`OSHost_TimeGetMonotonicTicks`, `OSHost_TimeGetTickFrequency`,
`OSHost_TimerCreate`, `OSHost_TimerArm`, `OSHost_TimerCancel`,
`OSHost_TimerClose`, `OSHost_ThreadSleepUntil`, or wait functions. It does not
program APIC/HPET/TSC, deliver interrupts, wake scheduler threads, implement
`Task.Delay`, run hosted .NET 10, or prove Kestrel timeout behavior in OS.

## Deferred Work

- Split common timeout constants away from socket-specific helper names when the
  broader PAL wait domain is implemented.
- Add exported fail-fast or `OSHOST_STATUS_NOT_SUPPORTED` timer symbols when a
  hosted runtime adapter needs linkable ABI entry points.
- Connect timer events to wait handles, scheduler wakeups, and hardware timer
  interrupts in later platform/runtime issues.
