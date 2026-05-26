# Step 603 - PAL Mock Thread Table

## Summary

Added a host-tested `OSHostThreadTable` mock for future PAL thread lifecycle
status translation. The table validates thread-create options, assigns opaque
thread handles and deterministic thread ids, records exit codes, supports
immediate join-after-exit, exposes a stable current-thread id/yield status, and
rejects default result values as success evidence.

## Why

The PAL/Kestrel ledger had POD thread creation structs and wait/event mocks, but
no thread lifecycle model. This gives future hosted runtime adapter work a
small, status-coded thread contract without claiming scheduler, stack, context
switch, or hosted .NET behavior.

## Files changed

- `src/OS.Kernel/Pal/OSHostThreadTable.cs`
  - Added mock thread lifecycle state, result structs, entry snapshots, create,
    mark-exited, join, current-id, and yield behavior.
- `tests/OS.HostTests/PalThreadTableTests.cs`
  - Added host tests with stable `pal.thread table` markers.
- `docs/pal.md`
  - Documented the mock thread table and evidence boundary.
- `work/PAL/pal-design.md`
  - Updated PAL design notes for current host-only thread evidence.
- `docs/completion-gates.md`
  - Recorded host-only thread table evidence without changing gate status.

## Tests run

```powershell
dotnet build .\tests\OS.HostTests\OS.HostTests.csproj --nologo
.\tests\OS.HostTests\bin\Debug\net10.0\OS.HostTests.exe | Select-String -Pattern 'TEST_FAIL|Unhandled|Exception|pal\.thread table|host\.tests'
```

## Evidence

- Host-test build completed successfully with 0 warnings and 0 errors after
  stale host-test output locks were gone.
- Focused marker scan emitted:
  - `TEST_PASS pal.thread table creates joinable thread`
  - `TEST_PASS pal.thread table rejects invalid create options`
  - `TEST_PASS pal.thread table reports exhaustion and reuse`
  - `TEST_PASS pal.thread table current id and yield are stable`
  - `TEST_PASS pal.thread table default results are not success`

## Failures

- A concurrent full host harness run exited with code 1 after the new
  `pal.thread table` markers. The filtered output did not include a failure
  marker or exception text, so the broader failure is not attributed to this
  slice.
- Earlier rebuild attempts were affected by stale `OS.HostTests`/MSBuild
  processes holding output DLLs. After those stale locks were gone, the
  host-test build passed.

## Deferred work

- No unmanaged `OSHost_Thread*` exports exist.
- The mock does not allocate stacks, invoke entrypoints, context switch, block
  joins, wake scheduler threads, provide TLS/thread-static bases, implement
  `Thread`, `ThreadPool`, `Task`, or support Kestrel.
