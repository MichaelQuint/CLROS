# Step 278 - PAL Immediate Wait Table

## What Changed

- Added `OSHostWaitTable` as a host-tested in-process PAL wait/event model.
- Added opaque generation-based wait handles with deterministic capacity
  exhaustion and stale-handle rejection.
- Added manual-reset and auto-reset event creation, set, reset, immediate
  `WaitOne`, wait-any `WaitMany`, cancellation/interruption, and close
  semantics.
- Kept wait behavior status-coded with no exceptions in kernel-facing paths and
  no managed references crossing the future ABI boundary.
- Aligned the wait table's immediate timeout kind with the shared
  `OSHostTimeout`/`OSHostSocketConstants.TimeoutKindImmediate` contract.
- Updated PAL, Kestrel, and completion-gate documentation.

## Why It Changed

ASP.NET Minimal API/Kestrel and the future hosted .NET 10 runtime need event
and wait semantics for cancellation, socket readiness, request timeouts,
ThreadPool work, and synchronization. This step defines the smallest immediate
host contract before exported `OSHost_*` wait symbols, scheduler blocking,
timer completion integration, or hosted runtime integration exists.

## Files Changed

- `src/OS.Kernel/Pal/OSHostWaitTable.cs`
- `tests/OS.HostTests/PalWaitTableTests.cs`
- `docs/pal.md`
- `docs/kestrel.md`
- `docs/completion-gates.md`
- `done/step278.md`

## Tests Run

```powershell
dotnet build .\tests\OS.HostTests\OS.HostTests.csproj --nologo -m:1
dotnet build .\OS.slnx --nologo -m:1
dotnet run --project .\tests\OS.HostTests\OS.HostTests.csproj --no-build
```

## Logs And Evidence

`dotnet build .\tests\OS.HostTests\OS.HostTests.csproj --nologo -m:1`
passed:

```text
Build succeeded.
0 Warning(s)
0 Error(s)
```

`dotnet build .\OS.slnx --nologo -m:1` passed:

```text
Build succeeded.
0 Warning(s)
0 Error(s)
```

The full host-test run exited `0` and emitted:

```text
TEST_PASS pal.wait manual reset event stays signaled
TEST_PASS pal.wait auto reset consumes signal
TEST_PASS pal.wait many reports first signaled
TEST_PASS pal.wait reset and close reject invalid
TEST_PASS pal.wait invalid options are rejected
TEST_PASS host.tests
```

## Failures

The first integrated host-test run saw one unrelated
`qemu dry-run emits default workload profile` failure while the dirty tree had
recent profile/tooling edits. The wait-table markers had already passed. A full
solution rebuild reset the built tool output, and the rerun of the complete
host-test harness exited `0`.

## Evidence Boundary

This is host-only PAL contract evidence. It does not export `OSHost_WaitOne`,
`OSHost_WaitMany`, `OSHost_EventCreate`, `OSHost_EventSet`,
`OSHost_EventReset`, or `OSHost_EventClose`. It does not block, park threads,
wake scheduler threads, consume timer completions, implement `Monitor`,
ThreadPool, `Task`, hosted .NET 10, ASP.NET Minimal API, or Kestrel behavior in
OS.

## Deferred Work

- Add exported fail-fast or `OSHOST_STATUS_NOT_SUPPORTED` wait/event symbols
  when a hosted runtime adapter needs linkable ABI entry points.
- Integrate wait handles with timer completions after the timer and scheduler
  implementation exists.
- Add real blocking waits, scheduler wakeups, and hosted runtime synchronization
  semantics in later thread/sync issues.
