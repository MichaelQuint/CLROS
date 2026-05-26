# Step 810 - Virtio RX TCP Dispatch QEMU Marker Preset

## Summary

Added the opt-in `virtio-rx-tcp-dispatch` QEMU marker preset across the runner,
PowerShell smoke wrapper, and CI smoke dry-run plan.

## Why

The virtio-net RX TCP dispatch readiness slice emits stable boot markers, but
the host runner did not yet have a focused marker preset for future probe
images. This keeps the default smoke path unchanged while letting issue-specific
QEMU runs require `VIRTIO_NET_RX_TCP_DISPATCH_READY` and
`VIRTIO_NET_RX_TCP_DISPATCH`.

## Files changed

- `tools/OS.QemuRunner/SmokeMarkerPreset.cs`
- `scripts/run-qemu-smoke.ps1`
- `scripts/run-ci-smoke.ps1`
- `tests/OS.HostTests/QemuRunnerTests.cs`
- `tests/OS.HostTests/CiSmokeScriptTests.cs`
- `docs/qemu.md`
- `docs/networking.md`
- `docs/completion-gates.md`
- `done/step810.md`

## Tests run

- `dotnet build .\tests\OS.HostTests\OS.HostTests.csproj --nologo`
- `dotnet .\tests\OS.HostTests\bin\Debug\net10.0\OS.HostTests.dll`
- `.\scripts\run-qemu-smoke.ps1 -DryRun -MarkerPreset virtio_rx_tcp_dispatch`
- `.\scripts\run-ci-smoke.ps1 -DryRun -LogRoot .\artifacts\logs\issue768-ci-smoke-dryrun`
- `git diff --check -- <issue-768-scoped-files>`
- scoped trailing-whitespace scan over the issue #768 write set

## Evidence

- Host build passed on retry with `0 Warning(s)` and `0 Error(s)` in
  `artifacts/logs/issue768-host-build-r2.log`.
- Host tests passed in `artifacts/logs/issue768-host-tests-r2.log`, including:
  - `TEST_PASS qemu.smoke.preset virtio rx tcp dispatch adds marker requirements`
  - `TEST_PASS qemu.smoke.preset virtio rx tcp dispatch aliases normalize`
  - `TEST_PASS qemu.smoke.preset script dry-run includes virtio rx tcp dispatch preset`
  - `TEST_PASS ci.smoke.dryrun includes virtio-rx-tcp-dispatch marker preset`
  - `TEST_PASS host.tests`
- QEMU smoke dry-run passed in
  `artifacts/logs/issue768-qemu-smoke-dryrun.log` and normalized
  `virtio_rx_tcp_dispatch` to `virtio-rx-tcp-dispatch`.
- The dry-run required marker list includes
  `VIRTIO_NET_RX_TCP_DISPATCH_READY` and `VIRTIO_NET_RX_TCP_DISPATCH` after the
  default boot boundary markers.
- CI smoke dry-run passed in `artifacts/logs/issue768-ci-smoke-dryrun.log` and
  emits `TEST_PASS ci.smoke.dryrun includes virtio-rx-tcp-dispatch marker preset`.
- Scoped diff check passed with exit code 0 in
  `artifacts/logs/issue768-diff-check.log`.
- Scoped trailing-whitespace scan emitted
  `TEST_PASS issue768.trailing_whitespace`.

## Failures

- The first host build attempt in `artifacts/logs/issue768-host-build.log`
  failed because a stale `OS.HostTests.dll` process held output files open.
  The stale process was inspected and stopped, then the build passed.
- The first host test run in `artifacts/logs/issue768-host-tests.log` exited
  nonzero while starting the source-firewall module initializer. A direct
  `scripts/check-naming.ps1` run passed, and the full host-test rerun passed.

## Deferred work

- This is marker-preset tooling only. It does not prove live virtio-net RX,
  packet I/O, TCP delivery, sockets, hosted .NET, ASP.NET Minimal API, Kestrel,
  cloud readiness, or production readiness.
