# Step 644 - PAL thread early-mode capability and sleep status

## Summary

Extended the host-only `OSHostThreadTable` contract with explicit early-mode
capability metadata and an immediate-only sleep operation. Blocking sleep
requests now return status-coded `OSHostStatus.NotSupported` instead of being
treated as scheduler evidence.

## Why

Issue #4 needs a small PAL thread/sync slice that is disjoint from diagnostics,
std, networking, and tooling work. The previous thread table covered mock
create/join/current-id/yield behavior, but it did not expose whether the table
was hosted-runtime ready or how early single-thread sleep requests should fail.
This slice makes those boundaries explicit without adding exported PAL symbols
or pretending a scheduler exists.

## Files changed

- `src/OS.Kernel/Pal/OSHostThreadTable.cs`
  - Added thread capability flags and `OSHostThreadCapabilityResult`.
  - Added `OSHostThreadSleepResult` and `Sleep(OSHostTimeout)`.
  - Kept immediate sleep as a synchronous no-op and reported blocking sleep
    forms as `OSHostStatus.NotSupported`.
- `tests/OS.HostTests/PalThreadTableTests.cs`
  - Added markers for early-mode capability metadata, unsupported blocking
    sleep, invalid timeout/handle rejection, and default capability/sleep
    results not counting as success.
- `docs/pal.md`
  - Documented the thread table capability/sleep evidence and host-only
    boundary.
- `work/PAL/pal-design.md`
  - Updated the PAL design ledger and marker list for the thread table slice.
- `done/step644.md`
  - Records this evidence.

## Tests run

- `dotnet build .\tests\OS.HostTests\OS.HostTests.csproj --nologo -m:1 /p:OSWorkloadProfile=SingleMinimalApi /p:UseSharedCompilation=false -nr:false`
- `dotnet run --project .\tests\OS.HostTests\OS.HostTests.csproj --no-build`

## Evidence

- Build passed with 0 warnings and 0 errors.
- Full host-test run emitted:
  - `TEST_PASS pal.thread table creates joinable thread`
  - `TEST_PASS pal.thread table rejects invalid create options`
  - `TEST_PASS pal.thread table reports exhaustion and reuse`
  - `TEST_PASS pal.thread table reports early mode capabilities`
  - `TEST_PASS pal.thread table current id and yield are stable`
  - `TEST_PASS pal.thread table sleep reports unsupported blocking waits`
  - `TEST_PASS pal.thread table rejects invalid timeout and handles`
  - `TEST_PASS pal.thread table default results are not success`
  - `TEST_PASS host.tests`

## Failures

None in the focused build or full host-test run.

## Deferred work

This is host-tested PAL table evidence only. It does not export unmanaged
`OSHost_*` symbols, allocate stacks, invoke entry points, switch contexts,
block or wake scheduler threads, integrate timers with sleeps, implement
thread statics, run hosted .NET, or prove Kestrel/cloud readiness.
