# Step 812 - HTTP Static Response QEMU Marker Preset

## Summary

Added an opt-in `http-static-response` QEMU marker preset for future tiny HTTP
static-response smoke images. The preset requires the current baseline markers
plus `HTTP_STATIC_RESPONSE_READY` and `HTTP_STATIC_RESPONSE`, preserves default
smoke behavior, and normalizes `http_static_response`, `tiny-http-static`, and
`tinyhttp-static` aliases.

## Why

The HTTP response writer/static transaction contracts are still host-tested
networking slices. Targeted smoke images need a narrow marker preset before real
guest wiring exists, without changing the default boot proof boundary or
claiming live HTTP server/Kestrel readiness.

## Files changed

- `tools/OS.QemuRunner/SmokeMarkerPreset.cs`
- `scripts/run-qemu-smoke.ps1`
- `scripts/run-ci-smoke.ps1`
- `tests/OS.HostTests/QemuRunnerTests.cs`
- `tests/OS.HostTests/CiSmokeScriptTests.cs`
- `docs/qemu.md`
- `docs/networking.md`
- `docs/completion-gates.md`
- `done/step812.md`

## Tests run

- `dotnet build .\tests\OS.HostTests\OS.HostTests.csproj --nologo *> .\artifacts\logs\issue772-host-build-r2.log`
- `dotnet .\tests\OS.HostTests\bin\Debug\net10.0\OS.HostTests.dll *> .\artifacts\logs\issue772-host-tests.log`
- `.\scripts\run-qemu-smoke.ps1 -DryRun -MarkerPreset http-static-response *> .\artifacts\logs\issue772-qemu-dryrun.log`
- Scoped `git diff --check` for the #772 files.
- Scoped trailing-whitespace scan for the #772 files.

## Evidence

- `artifacts/logs/issue772-host-build-r2.log` ended with `Build succeeded.`,
  `0 Warning(s)`, and `0 Error(s)`.
- `artifacts/logs/issue772-host-tests.log` ended with `TEST_PASS host.tests`.
- `artifacts/logs/issue772-host-tests.log` emitted:
  - `TEST_PASS qemu.smoke.preset http static response adds marker requirements`
  - `TEST_PASS qemu.smoke.preset http static response aliases normalize`
  - `TEST_PASS qemu.smoke.preset script dry-run includes http static response preset`
  - `TEST_PASS ci.smoke.dryrun includes http-static-response marker preset`
- `artifacts/logs/issue772-qemu-dryrun.log` emitted
  `QEMU_MARKER_PRESETS http-static-response`, `MARKER_PRESETS http-static-response`,
  and required markers ending with `HTTP_STATIC_RESPONSE_READY;HTTP_STATIC_RESPONSE`.
- `artifacts/logs/issue772-diff-check.log` ended with `git-diff-check-exit=0`.
- `artifacts/logs/issue772-trailing-whitespace.log` emitted
  `TEST_PASS issue772.trailing_whitespace`.

## Failures

- The first build attempt failed because a stale `.NET Host` process held
  `tests/OS.HostTests/bin/Debug/net10.0/*.dll` outputs. The lock disappeared
  before process details could be read through WMI, and the unchanged build
  command passed on rerun.

## Deferred work

- Wire a real guest boot path to emit `HTTP_STATIC_RESPONSE_READY` and
  `HTTP_STATIC_RESPONSE` only after live networking, sockets/listen, scheduler
  support, app launch, and static-response server prerequisites exist.
- This is marker-preset tooling only. It does not prove a live HTTP server,
  sockets, hosted .NET, ASP.NET Minimal API, Kestrel, cloud readiness, or
  production readiness.
