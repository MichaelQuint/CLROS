# Step 745 - FetchApp HTTP TCP Exchange QEMU Marker Preset

## Summary

Added a host-side QEMU marker preset for the future FetchApp HTTP TCP exchange
diagnostic marker. The new canonical preset is `fetch-http-tcp-exchange`, and
it requires `FETCH_HTTP_TCP_EXCHANGE` in addition to the runner baseline boot
markers.

## Why

Issue #7 needs a small Phase H/QEMU tooling contract so future guest images can
opt into a smoke requirement for the FetchApp HTTP-over-TCP exchange marker
without changing networking kernel code or claiming live packet I/O.

## Files changed

- `tools/OS.QemuRunner/SmokeMarkerPreset.cs`
  - Added `fetch-http-tcp-exchange`.
  - Added aliases `fetch_http_tcp_exchange`, `fetch-http-exchange`, and
    `fetchapp-http-tcp-exchange`.
  - Added required marker `FETCH_HTTP_TCP_EXCHANGE`.
- `tools/OS.QemuRunner/Program.cs`
  - Updated help text to use the preset registry.
- `scripts/run-qemu-smoke.ps1`
  - Normalizes the new preset and aliases before forwarding to the runner.
- `tests/OS.HostTests/QemuRunnerTests.cs`
  - Added dry-run checks for the runner preset, alias normalization, and
    script-level forwarding.
- `docs/qemu.md`
  - Documented the preset, aliases, dry-run output, tests, and evidence
    boundary.
- `done/step745.md`
  - Recorded this implementation slice and evidence.

## Tests run

- `dotnet build .\tests\OS.HostTests\OS.HostTests.csproj --nologo`
- `dotnet build .\tests\OS.HostTests\OS.HostTests.csproj --nologo -p:BaseOutputPath=artifacts\bin\issue7\qemu-fetch-exchange\`
- `dotnet tests\OS.HostTests\artifacts\bin\issue7\qemu-fetch-exchange\Debug\net10.0\OS.HostTests.dll`
- `dotnet .\tools\OS.QemuRunner\bin\Debug\net10.0\OS.QemuRunner.dll --dry-run --image artifacts\images\os.raw --marker-preset fetch-http-tcp-exchange`
- `dotnet .\tools\OS.QemuRunner\bin\Debug\net10.0\OS.QemuRunner.dll --dry-run --image artifacts\images\os.raw --marker-preset fetch_http_tcp_exchange --marker-preset fetch-http-exchange --marker-preset fetchapp-http-tcp-exchange`
- `.\scripts\run-qemu-smoke.ps1 -DryRun -MarkerPreset fetch-http-tcp-exchange -SerialLog artifacts\logs\issue7\qemu-fetch-exchange-serial.log -SummaryLog artifacts\logs\issue7\qemu-fetch-exchange-summary.log`
- `.\scripts\run-qemu-smoke.ps1 -DryRun -MarkerPreset fetch_http_tcp_exchange,fetch-http-exchange,fetchapp-http-tcp-exchange -SerialLog artifacts\logs\issue7\qemu-fetch-exchange-alias-serial.log -SummaryLog artifacts\logs\issue7\qemu-fetch-exchange-alias-summary.log`
- `git diff --check`

## Evidence

- `artifacts/logs/issue7/qemu-fetch-exchange-host-build-20260525-121355.log`
  - Default host-test build was blocked by an existing `OS.HostTests` process
    holding `tests\OS.HostTests\bin\Debug\net10.0\OS.Kernel.dll`.
- `artifacts/logs/issue7/qemu-fetch-exchange-host-build-shaped-20260525-121425.log`
  - Shaped host-test build succeeded with 0 warnings and 0 errors.
- `artifacts/logs/issue7/qemu-fetch-exchange-hosttests-shaped-20260525-121446.log`
  - Full host harness started from the shaped output, but later failed in an
    existing CI smoke failure-path test because nested `dotnet run` could not
    write `tools\OS.QemuRunner\obj\Debug\net10.0\OS.QemuRunner.dll` while it
    was locked.
- `artifacts/logs/issue7/qemu-fetch-exchange-runner-dryrun-20260525-121626.log`
  - `TEST_PASS qemu.runner.dryrun`
  - `MARKER_PRESETS fetch-http-tcp-exchange`
  - `REQUIRED_MARKERS OS_BOOT_OK;TEST_PASS boot.sequence;OS_SMOKE_PASS;FETCH_HTTP_TCP_EXCHANGE`
- `artifacts/logs/issue7/qemu-fetch-exchange-runner-alias-dryrun-20260525-121632.log`
  - `TEST_PASS qemu.runner.dryrun`
  - Aliases normalized to `MARKER_PRESETS fetch-http-tcp-exchange`.
- `artifacts/logs/issue7/qemu-fetch-exchange-script-dryrun-20260525-121639.log`
  - `QEMU_MARKER_PRESETS fetch-http-tcp-exchange`
  - Script dry-run required the default smoke markers plus
    `FETCH_HTTP_TCP_EXCHANGE`.
- `artifacts/logs/issue7/qemu-fetch-exchange-script-alias-dryrun-20260525-121648.log`
  - Script aliases normalized to `QEMU_MARKER_PRESETS fetch-http-tcp-exchange`.
- `artifacts/logs/issue7/qemu-fetch-exchange-git-diff-check-20260525-121659.log`
  - `git diff --check` exited 0; output contains LF/CRLF warnings only.

## Failures

- The default host-test build was blocked by a pre-existing
  `OS.HostTests (1544)` process locking the default output.
- The shaped full host-test run did not finish cleanly because an existing
  CI-smoke failure-path test hit a nested `dotnet run` object-file lock. The
  focused runner and script dry-runs for this preset passed.

## Deferred work

- Add live QEMU proof only after a guest image emits `FETCH_HTTP_TCP_EXCHANGE`
  from the tested boot path.
- Keep CI smoke lanes separate unless issue #7 explicitly asks to add this
  preset to CI artifact planning.

## Evidence boundary

Host-side marker contract/tooling evidence only. This does not prove live
FetchApp execution, virtio-net, DHCP, DNS, TCP connect/read/write/close, HTTP
GET, app completion, hosted .NET, Kestrel, cloud readiness, or production
readiness.
