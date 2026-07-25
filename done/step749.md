# Step 749 - CI FetchApp HTTP TCP Exchange Dry-Run Lane

## Summary

Added a CI smoke dry-run lane for the FetchApp HTTP-over-TCP exchange marker
preset.

## Why

Issue #709 tracks the Phase H tooling slice that makes the
`fetch-http-tcp-exchange` QEMU marker preset visible in CI artifact planning.
The earlier QEMU runner preset and boot-sequence readiness work were not enough
for CI: the smoke script needed a dedicated lane with serial/summary artifacts,
machine-readable marker-preset metadata, and host tests proving the lane remains
dry-run only.

## Files changed

- `scripts/run-ci-smoke.ps1`
  - Added `ci.smoke.fetch_http_tcp_exchange.dryrun`.
  - Wires `-MarkerPreset fetch-http-tcp-exchange`, per-lane serial and summary
    log paths, and `markerPresets` metadata into the artifact plan and summary.
  - Emits `TEST_PASS ci.smoke.dryrun includes fetch-http-tcp-exchange marker
    preset` in dry-run mode.
- `tests/OS.HostTests/CiSmokeScriptTests.cs`
  - Validates the new lane in the artifact plan and summary.
  - Verifies the lane carries `-DryRun`, `-WorkloadProfile`, serial/summary log
    artifacts, and marker-preset metadata.
  - Verifies the executed failure-path summary keeps the new dry-run lane green
    and captures `FETCH_HTTP_TCP_EXCHANGE` in its log.
- `docs/qemu.md`
  - Documents the CI smoke lane, dry-run marker output, and evidence boundary.
- `done/step749.md`
  - Records this implementation slice and evidence.

## Tests run

- `dotnet build .\tests\OS.HostTests\OS.HostTests.csproj --nologo -p:UseAppHost=false -o .\artifacts\issue709-ci-fetch-exchange\bin\Debug\net10.0`
- `dotnet .\artifacts\issue709-ci-fetch-exchange\bin\Debug\net10.0\OS.HostTests.dll`
- `.\scripts\run-ci-smoke.ps1 -DryRun -LogRoot artifacts\logs\issue709-ci-fetch-exchange-dryrun`
- `git diff --check`

## Evidence

- `artifacts/logs/issue709-host-build-20260525.log`
  - Host-test project build succeeded with 0 warnings and 0 errors.
- `artifacts/logs/issue709-hosttests-20260525.log`
  - Full shaped host harness ended with `TEST_PASS host.tests`.
  - Focused marker passed:
    - `TEST_PASS ci.smoke.dryrun includes fetch-http-tcp-exchange marker preset`
- `artifacts/logs/issue709-ci-dryrun-20260525.log`
  - Direct CI dry-run emitted `CI_SMOKE_ARTIFACT_PLAN
    artifacts\logs\issue709-ci-fetch-exchange-dryrun\ci-smoke.artifacts.json`.
  - Direct CI dry-run emitted `TEST_PASS ci.smoke.dryrun includes
    fetch-http-tcp-exchange marker preset`.
  - Direct CI dry-run ended with `TEST_PASS ci.smoke.dryrun`.
- `artifacts/logs/issue709-ci-fetch-exchange-dryrun/ci-smoke.artifacts.json`
  - Contains `ci.smoke.fetch_http_tcp_exchange.dryrun`.
  - The command includes `-MarkerPreset fetch-http-tcp-exchange`.
  - The lane records `qemu-fetch-http-tcp-exchange-dry-run.log`,
    `qemu-fetch-http-tcp-exchange-serial.log`,
    `qemu-fetch-http-tcp-exchange.summary.log`, and marker preset
    `fetch-http-tcp-exchange`.
- `artifacts/logs/issue709-git-diff-check-final-20260525.log`
  - `git diff --check` exited 0. Output contains only existing LF/CRLF
    normalization warnings.

## Failures

- None for this slice.

## Deferred work

- This is a CI artifact-contract dry-run lane only. It does not launch QEMU and
  does not prove live FetchApp execution, virtio-net, DHCP, DNS, TCP
  connect/read/write/close, HTTP GET, hosted .NET, Minimal API, Kestrel, or
  guest app completion.
- A future real QEMU smoke image must emit `FETCH_HTTP_TCP_EXCHANGE` from guest
  code before this lane becomes boot/runtime evidence.

## Evidence boundary

Host-tested CI smoke planning evidence only. This does not complete QEMU boot
coverage, networking, FetchApp, storage-backed manifest loading, hosted .NET 10,
Kestrel, cloud readiness, or production readiness.
