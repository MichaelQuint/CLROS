# Step 690 - NativeAOT Class Constructor Runner Preflight

## Summary

- Added `NativeAotClassConstructorRunnerPreflight` for the future class
  constructor runner. It validates caller-owned cctor state table metadata,
  type count, target metadata count, pending/reentrant states, failed states,
  and invalid states without executing constructors.
- Added caller-owned ASCII diagnostic formatting with the stable
  `NATIVEAOT_CCTOR_RUNNER_PREFLIGHT status=` prefix.
- Added focused host tests and updated the runtime helper ledger/docs.

## Why

The runtime needs a preflight contract before any class constructor runner can
be wired into NativeAOT helper behavior. This step records the metadata and
failure statuses needed by that future runner while keeping constructor
invocation, metadata discovery, module initialization, lazy static reference
field safety, and exception unwinding out of scope.

## Files changed

- `src/OS.Kernel/Runtime/NativeAotClassConstructorRunnerPreflight.cs`
- `src/OS.Kernel/Runtime/NativeAotHelperLedger.cs`
- `tests/OS.HostTests/NativeAotClassConstructorStateTableTests.cs`
- `tests/OS.HostTests/NativeAotHelperLedgerTests.cs`
- `docs/nativeaot-nostdlib-limits.md`
- `docs/completion-gates.md`
- `done/step690.md`

## Tests run

- `dotnet build .\tests\OS.HostTests\OS.HostTests.csproj --nologo`
- `dotnet run --project .\tests\OS.HostTests\OS.HostTests.csproj --no-build | Select-String -Pattern "cctor runner preflight|runtime.cctor state formatter|host.tests|TEST_FAIL"`
- `git diff --check -- src/OS.Kernel/Runtime tests/OS.HostTests/NativeAotClassConstructorStateTableTests.cs tests/OS.HostTests/NativeAotHelperLedgerTests.cs docs/nativeaot-nostdlib-limits.md docs/completion-gates.md done`

## Evidence

- The focused host-test build passed with 0 warnings and 0 errors after
  concurrent host-test processes released the output files.
- The focused no-build host-test marker filter exited 0, reached
  `TEST_PASS host.tests`, and emitted:
  - `TEST_PASS runtime.cctor state formatter formats success marker`
  - `TEST_PASS runtime.cctor state formatter reports short destination`
  - `TEST_PASS runtime.cctor state formatter rejects default result`
  - `TEST_PASS runtime.cctor runner preflight ready`
  - `TEST_PASS runtime.cctor runner preflight rejects default table`
  - `TEST_PASS runtime.cctor runner preflight rejects failed state`
  - `TEST_PASS runtime.cctor runner preflight rejects pending state`
  - `TEST_PASS runtime.cctor runner preflight rejects missing metadata`
  - `TEST_PASS runtime.cctor runner preflight formatter reports short destination`
- `git diff --check` exited 0 for the scoped runtime/test/doc/done paths.

## Failures

- Initial build attempts were blocked by concurrent `OS.HostTests` processes
  locking the normal host-test output directory. The build passed after those
  processes exited.

## Deferred work

- Implement real cctor metadata discovery and constructor invocation only in a
  later runtime-helper issue with matching tests and evidence.
