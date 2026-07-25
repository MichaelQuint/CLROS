# Step 682 - Native APP_EXIT Log Observer

## Summary

Added a host-tested native app exit log observer that scans bounded
`OSHostAppLogTable` records for machine-readable stdout `APP_EXIT` markers.
The observer uses `NativeAppExitStatusFormatter.TryParse` instead of duplicating
host runner parsing logic and returns value-type, status-coded metadata.

## Why

Future app supervisor work needs a kernel/runtime boundary that can consume
bounded app log records and find the first exit marker without exceptions,
hidden allocations, or host-tool parser dependencies. This slice moves one step
from marker formatting/parsing toward supervisor log ingestion while preserving
the no-live-app-execution boundary.

## Files changed

- `src/OS.Kernel/Runtime/NativeAppExitLogObserver.cs`
  - Added `NativeAppExitLogObserverStatus`.
  - Added `NativeAppExitLogObserverResult` with initialized-state, record index,
    stream id, flags, sequence number, byte counts, log/read status, parse
    status, parsed exit code/status, and launcher-failure metadata.
  - Added `NativeAppExitLogObserver.Observe`, which scans only
    machine-readable stdout records through `OSHostAppLogTable.TryGetEntry` and
    `TryCopyRecord`, uses caller-owned scratch storage, reports malformed
    markers, no marker, invalid table/record, and short scratch buffer statuses.
- `tests/OS.HostTests/NativeAppExitLogObserverTests.cs`
  - Added host coverage for success, ignored non-machine-readable records,
    malformed markers, no marker, short scratch buffers, invalid log tables, and
    default/zero result rejection.
- `docs/app-model.md`
  - Documented the observer policy, metadata preserved, status boundaries, and
    no-live-supervisor evidence boundary.
- `docs/completion-gates.md`
  - Added host-tested gate evidence and kept native app execution/capture gates
    failed.
- `done/step682.md`
  - Recorded this slice.

## Tests run

- `dotnet build .\tests\OS.HostTests\OS.HostTests.csproj --nologo`
- `dotnet run --project .\tests\OS.HostTests\OS.HostTests.csproj --no-build | Select-String -Pattern "appmodel\.exit observer|host\.tests"`

## Evidence

- Focused host-test build passed with 0 warnings and 0 errors.
- Filtered host-test run exited 0 and emitted:
  - `TEST_PASS appmodel.exit observer finds stdout marker`
  - `TEST_PASS appmodel.exit observer ignores non-machine-readable records`
  - `TEST_PASS appmodel.exit observer reports malformed marker`
  - `TEST_PASS appmodel.exit observer reports no marker`
  - `TEST_PASS appmodel.exit observer reports short record buffer`
  - `TEST_PASS appmodel.exit observer reports invalid log table`
  - `TEST_PASS appmodel.exit observer default result is not success`
  - `TEST_PASS host.tests`

## Failures

- None in the focused build or filtered host-test run.

## Deferred work

- Connect the observer to future supervisor-owned log ingestion after native app
  launch, stdout/stderr routing, and lifecycle state exist.
- Observe a real app exit in QEMU after executable mapping, app ABI entry, and
  app-log routing are implemented.
- Keep host QEMU runner `APP_EXIT_RESULT` summaries separate from kernel
  observer evidence until guest app execution is proven.

## Evidence boundary

This is host-tested app-log table scanning evidence only. It does not prove live
native app launch, live stdout/stderr routing, supervisor restart policy,
executable mapping, hosted .NET 10, ASP.NET Minimal API, Kestrel, cloud
readiness, or production readiness.
