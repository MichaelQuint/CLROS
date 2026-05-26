# Step 654 - PAL wait/event early-mode validation

## Summary

Added a host-tested `OSHostWaitCapabilityResult` and wait-table capability
query for the current early single-thread mock wait/event mode. Tightened
wait-table timeout validation so malformed `OSHostTimeout` shapes return
`OSHostStatus.InvalidArgument`, while valid non-immediate waits continue to
return `OSHostStatus.NotSupported` until blocking waits exist.

## Why

The PAL wait/event model already covered manual-reset and auto-reset immediate
wait behavior, but it did not expose table-level capability metadata and treated
some malformed timeout structs the same as unsupported blocking waits. The new
contract keeps the ABI status-code boundary explicit and prevents default or
zero capability values from being mistaken for hosted-runtime readiness.

## Files changed

- `src/OS.Kernel/Pal/OSHostWaitTable.cs`
- `tests/OS.HostTests/PalWaitTableTests.cs`
- `docs/pal.md`
- `work/PAL/pal-design.md`
- `done/step654.md`

## Tests run

```powershell
dotnet build .\tests\OS.HostTests\OS.HostTests.csproj --nologo -m:1 /p:OSWorkloadProfile=SingleMinimalApi /p:UseSharedCompilation=false -nr:false
dotnet run --project .\tests\OS.HostTests\OS.HostTests.csproj --no-build
git diff --check -- src/OS.Kernel/Pal/OSHostWaitTable.cs tests/OS.HostTests/PalWaitTableTests.cs docs/pal.md work/PAL/pal-design.md done/step654.md
```

## Evidence

- Focused host-test build passed with 0 warnings and 0 errors.
- Full host-test run emitted the new wait-table markers before later unrelated
  QEMU-runner failures:
  - `TEST_PASS pal.wait reports early mode capabilities`
  - `TEST_PASS pal.wait rejects invalid timeout and unsupported waits`
  - `TEST_PASS pal.wait invalid options are rejected`
  - `TEST_PASS pal.wait default results are not success`
- `git diff --check` for the touched files passed.

## Failures

- `dotnet run --project .\tests\OS.HostTests\OS.HostTests.csproj --no-build`
  exited nonzero after the PAL wait markers passed because unrelated QEMU runner
  script tests failed:
  - `TEST_FAIL qemu smoke script dry-run propagates workload profile`
  - `TEST_FAIL qemu dry-run emits native app marker requirements TimeoutException: run-qemu-smoke.ps1 did not exit within 120 seconds.`
  - Final harness line: `TEST_FAIL qemu.runner.tests failures=2`

## Deferred work

- Blocking wait, wait-all, scheduler park/wake, hardware timer integration,
  `Monitor`, ThreadPool, hosted .NET, and Kestrel synchronization behavior
  remain future work.

## Evidence boundary

This is host-tested in-process PAL wait/event contract evidence only. It does
not export `OSHost_*` wait symbols, block or wake scheduler threads, integrate
hardware timers, run a hosted runtime, or prove Kestrel readiness.

