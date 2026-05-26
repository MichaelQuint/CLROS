# Step 693 - QEMU App-Exit Observer Marker Preset

## Summary
Added the opt-in `app-exit` QEMU marker preset for future native-app smoke
images. The preset requires `APP_EXIT_OBSERVER_READY` in addition to the active
baseline markers and is wired through both `OS.QemuRunner` and
`scripts/run-qemu-smoke.ps1`.

## Why
The app-exit observer now has host-tested BootSequence readiness markers, but
the QEMU tooling did not have a focused marker preset for future native-app
images that should require observer readiness. This keeps the default boot
smoke gate unchanged while making app-exit marker requirements easy to select.

## Files changed
- `tools/OS.QemuRunner/SmokeMarkerPreset.cs`
- `tools/OS.QemuRunner/Program.cs`
- `scripts/run-qemu-smoke.ps1`
- `tests/OS.HostTests/QemuRunnerTests.cs`
- `docs/qemu.md`
- `docs/app-model.md`
- `docs/completion-gates.md`
- `done/step693.md`

## Tests run
- `dotnet build .\tests\OS.HostTests\OS.HostTests.csproj --nologo`
- `dotnet run --project .\tests\OS.HostTests\OS.HostTests.csproj --no-build | Select-String -Pattern "qemu.smoke.preset app exit|qemu dry-run emits default workload profile|qemu.runner.tests|host.tests|TEST_FAIL"`
- `.\scripts\run-qemu-smoke.ps1 -DryRun -MarkerPreset app-exit`
- `git diff --check -- tools/OS.QemuRunner/SmokeMarkerPreset.cs tools/OS.QemuRunner/Program.cs scripts/run-qemu-smoke.ps1 tests/OS.HostTests/QemuRunnerTests.cs docs/qemu.md docs/app-model.md docs/completion-gates.md done/step693.md`

## Evidence
- Host build passed with 0 warnings and 0 errors after a concurrent
  `OS.HostTests` process released the output DLL lock.
- Integrated host-test rerun emitted:
  - `TEST_PASS qemu.smoke.preset app exit adds observer readiness marker`
  - `TEST_PASS qemu dry-run emits default workload profile`
  - `TEST_PASS host.tests`
- Script dry-run emitted `QEMU_MARKER_PRESETS app-exit`,
  `MARKER_PRESETS app-exit`, and required marker output ending in
  `APP_EXIT_OBSERVER_READY`.
- `git diff --check` exited 0. It printed CRLF normalization warnings only.

## Failures
- An initial build failed because a concurrent `OS.HostTests` process held
  `tests/OS.HostTests/bin/Debug/net10.0/OS.Kernel.dll`. The build was rerun
  after the process exited and passed.
- An initial filtered integrated test run reported an unrelated transient
  `qemu dry-run emits default workload profile` failure. Direct runner output
  and the subsequent integrated rerun both showed `WORKLOAD_PROFILE
  SingleMinimalApi` and passed.

## Deferred work
- This preset is marker-contract tooling only. It does not prove live app
  execution, supervisor-owned serial/log ingestion, restart-policy handling,
  app-exit capture, hosted .NET, Kestrel, or cloud readiness. A future real
  QEMU guest image must emit `APP_EXIT_OBSERVER_READY` before this preset can
  serve as guest app-model evidence.

