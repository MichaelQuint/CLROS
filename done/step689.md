# Step 689 - QEMU FetchApp HTTP Completion Marker Preset

## Summary
Added the opt-in `fetch-http` QEMU marker preset for future FetchApp smoke
images. The preset requires `FETCH_APP_HTTP_COMPLETION_READY` in addition to
the active baseline markers and is wired through both `OS.QemuRunner` and
`scripts/run-qemu-smoke.ps1`.

## Why
FetchApp HTTP completion now has host-tested planning, formatting, and
BootSequence readiness markers. QEMU tooling needed a targeted preset so future
guest images can require that marker without expanding the default boot smoke
gate or claiming live networking before it exists.

## Files changed
- `tools/OS.QemuRunner/SmokeMarkerPreset.cs`
- `tools/OS.QemuRunner/Program.cs`
- `scripts/run-qemu-smoke.ps1`
- `tests/OS.HostTests/QemuRunnerTests.cs`
- `docs/qemu.md`
- `docs/networking.md`
- `docs/completion-gates.md`
- `done/step689.md`

## Tests run
- `dotnet build .\tests\OS.HostTests\OS.HostTests.csproj --nologo`
- `dotnet run --project .\tests\OS.HostTests\OS.HostTests.csproj --no-build | Select-String -Pattern "qemu.smoke.preset fetch|qemu.smoke.preset script dry-run includes fetch|qemu.runner.http_probe|host.tests|TEST_FAIL"`
- `.\scripts\run-qemu-smoke.ps1 -DryRun -MarkerPreset fetch-http`
- `git diff --check -- tools/OS.QemuRunner/SmokeMarkerPreset.cs tools/OS.QemuRunner/Program.cs scripts/run-qemu-smoke.ps1 tests/OS.HostTests/QemuRunnerTests.cs tests/OS.HostTests/QemuRunnerHttpProbeTests.cs docs/qemu.md docs/networking.md docs/completion-gates.md done/step689.md`

## Evidence
- Host build succeeded with 0 warnings and 0 errors.
- Integrated host-test filter emitted:
  - `TEST_PASS qemu.smoke.preset fetch http adds FetchApp completion marker`
  - `TEST_PASS qemu.smoke.preset script dry-run includes fetch http preset`
  - `TEST_PASS qemu.runner.http_probe accepts expected response`
  - `TEST_PASS qemu.runner.http_probe rejects status mismatch`
  - `TEST_PASS qemu.runner.http_probe rejects body mismatch`
  - `TEST_PASS qemu.runner.http_probe times out deterministically`
  - `TEST_PASS host.tests`
- Script dry-run emitted `QEMU_MARKER_PRESETS fetch-http`,
  `MARKER_PRESETS fetch-http`, and required marker output ending in
  `FETCH_APP_HTTP_COMPLETION_READY`.
- `git diff --check` exited 0. It printed CRLF normalization warnings only.

## Failures
- The first filtered host-test run stopped before `host.tests` while entering
  the new script wrapper test. A rerun after the wrapper dry-run was verified
  completed successfully with the expected `TEST_PASS` markers.

## Deferred work
- This preset is marker-contract tooling only. It does not prove live
  virtio-net, DHCP, DNS, TCP, HTTP GET, FetchApp execution, hosted .NET,
  Kestrel, or cloud readiness. A future real QEMU image must emit
  `FETCH_APP_HTTP_COMPLETION_READY` from the guest before this preset can serve
  as guest networking evidence.

