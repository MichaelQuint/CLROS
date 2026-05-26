# Step 680 - NativeAOT Class Constructor State Table

## Summary

Added a host-tested, allocation-free NativeAOT class-constructor state table
over caller-owned storage.

## Why

Future class-constructor runner work needs deterministic state tracking before
the OS can safely allow lazy static reference fields or cctor-dependent BCL
paths. This slice records the state transitions and failure/reentry boundaries
without executing constructor code or changing live runtime helper behavior.

## Files changed

- `src/OS.Kernel/Runtime/NativeAotClassConstructorStateTable.cs`
  - Added numeric `NativeAotClassConstructorKey` values over module/type ids.
  - Added value-only `NativeAotClassConstructorStateEntry` storage records.
  - Added states `Uninitialized`, `Initializing`, `Initialized`, and `Failed`.
  - Added status-coded register, begin, initialized, failed, and readback APIs
    over caller-owned spans.
  - Detects invalid keys, missing keys, duplicate keys, capacity exhaustion,
    reentry cycles, failed cctor reuse, already-initialized states,
    not-initializing transitions, missing failure codes, invalid states, and
    default-result success bugs.
- `tests/OS.HostTests/NativeAotClassConstructorStateTableTests.cs`
  - Added focused host markers for success transitions, reentry, failure
    preservation, duplicate/missing/capacity rejection, default-result
    rejection, and no-managed-reference layouts.
- `docs/nativeaot-nostdlib-limits.md`
  - Documented the state-table behavior, test markers, and boundary that this
    is not a full cctor runner.
- `docs/completion-gates.md`
  - Added Gate 4 evidence text while keeping the gate failed.
- `done/step680.md`
  - Recorded this milestone.

## Tests run

- `dotnet build .\tests\OS.HostTests\OS.HostTests.csproj --nologo`
- `dotnet run --project .\tests\OS.HostTests\OS.HostTests.csproj --no-build
  | Select-String -Pattern "runtime\.cctor state|host\.tests"`
- `dotnet run --project .\tests\OS.HostTests\OS.HostTests.csproj --no-build
  2>&1 | Select-String -Pattern
  "TEST_FAIL|Unhandled|Exception|runtime\.cctor state|host\.tests|qemu dry-run emits native app marker requirements"`
  (diagnostic rerun for the nonzero host-test boundary)
- `git diff --check -- .\src\OS.Kernel\Runtime .\tests\OS.HostTests
  .\docs\nativeaot-nostdlib-limits.md .\docs\completion-gates.md .\done`

## Evidence

- Focused host-test build passed with 0 warnings and 0 errors after clearing a
  stale `OS.HostTests.exe` lock.
- The host-test run emitted:
  - `TEST_PASS runtime.cctor state begins and completes`
  - `TEST_PASS runtime.cctor state detects reentry cycle`
  - `TEST_PASS runtime.cctor state preserves failure`
  - `TEST_PASS runtime.cctor state rejects duplicate keys`
  - `TEST_PASS runtime.cctor state default result is not success`
  - `TEST_PASS runtime.cctor state layouts contain no managed references`

## Failures

- The first build attempt failed with MSB3027/MSB3021 because a stale
  `OS.HostTests.exe` process held the test executable. After stopping that
  lock-holder, the same build command passed.
- The exact filtered host-test command did not reach `TEST_PASS host.tests`
  because the wider host-test suite exits nonzero outside this issue's scope.
  A stderr-inclusive diagnostic rerun emitted all new cctor markers, then later
  emitted `TEST_FAIL storage.virtioblk package file staging formatter formats
  success marker`, `TEST_FAIL storage.virtioblk package file staging formatter
  reports short destination`, and `Unhandled exception.
  System.InvalidOperationException: Virtio block package read preflight host
  tests failed: 2`. No cctor `TEST_FAIL` marker was observed. The
  storage/virtio-blk formatter failure is outside this issue's assigned runtime
  cctor state-table scope and was not modified.

## Deferred work

- Connect the state table to a real NativeAOT class-constructor runner.
- Discover and register cctor metadata from runtime/type-manager data.
- Execute constructor bodies, module initializers, lazy static reference
  initialization, and failure diagnostics only after exception/unwind and
  runtime-helper prerequisites are implemented.

## Evidence boundary

This is host-tested value-state transition evidence only. It does not execute
constructors, delegates, reflection, dynamic invocation, module initialization,
exception handling, metadata discovery, lazy static reference initialization,
QEMU runtime-helper probes, hosted .NET 10, ASP.NET Minimal API, Kestrel, or
cloud readiness.
