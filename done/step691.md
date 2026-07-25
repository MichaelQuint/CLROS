# Step 691 - App Exit Observer Boot Marker

## Summary

- Added a host-tested `BootSequence.RunNativeAppExitObserverReadiness`
  boundary that emits `APP_EXIT_OBSERVER_READY` for successful initialized
  `NativeAppExitLogObserverResult` values and
  `APP_EXIT_OBSERVER_NOT_READY` for default/uninitialized or failed observer
  metadata.
- Added boot-sequence host tests for ready, observer-failure, default-result,
  and default boot-marker preservation paths.
- Updated boot, app-model, and completion-gate docs with the host-tested
  evidence boundary.

## Why

The native app model needs a boot-owned readiness marker for future supervisor
exit observation before any live app-exit claim is made. This slice keeps the
boundary status-coded and rooted in `BootSequence` while preserving the current
host-tested-only app-log observer evidence.

## Files changed

- `src/OS.Kernel/Boot/BootSequence.cs`
- `tests/OS.HostTests/NativeAppExitLogObserverTests.cs`
- `docs/boot.md`
- `docs/app-model.md`
- `docs/completion-gates.md`
- `done/step691.md`

## Tests run

- `dotnet build .\tests\OS.HostTests\OS.HostTests.csproj --nologo`
- `dotnet run --project .\tests\OS.HostTests\OS.HostTests.csproj --no-build | Select-String -Pattern "app.exit observer|boot.sequence app exit|host.tests|TEST_FAIL"`
- `git diff --check -- src/OS.Kernel/Boot/BootSequence.cs tests/OS.HostTests/NativeAppExitLogObserverTests.cs docs/boot.md docs/app-model.md docs/completion-gates.md done`

## Evidence

- Focused host-test build passed with 0 errors. MSBuild reported transient copy
  retry warnings because an existing `OS.HostTests` process held the output DLL,
  then completed successfully.
- The marker-filtered no-build host-test run emitted:
  - `TEST_PASS boot.sequence app exit observer ready`
  - `TEST_PASS boot.sequence app exit observer rejects observer failure`
  - `TEST_PASS boot.sequence app exit observer rejects default result`
  - `TEST_PASS boot.sequence app exit observer preserves default markers`
- `git diff --check` over the owned files and `done` exited 0.

## Failures

- The marker-filtered no-build host-test run did not reach `TEST_PASS
  host.tests`; it failed in unrelated QEMU runner coverage with:
  - `TEST_FAIL qemu smoke script dry-run propagates qcow2 disk format`
  - `TEST_FAIL qemu.runner.tests failures=1`
- QEMU runner/tooling files were outside this worker's ownership and were not
  modified for this slice.

## Deferred work

- Wire the app-exit observer readiness boundary into the live boot/app path only
  after app execution, live stdout/stderr routing, supervisor-owned log
  ingestion, and exit capture have evidence.
- Gate 9 remains failed until a native-tier app is loaded, invoked, observed,
  and exited in the guest with preserved QEMU/libvirt evidence.

