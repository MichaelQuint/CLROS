# Step 807 - Virtio-Net RX TCP Dispatch Diagnostic Formatter

## Summary

Added a caller-buffer diagnostic formatter for the host-tested virtio-net RX
register-window TCP dispatch result.

## Why

The RX TCP dispatch composition slice now preserves useful nested metadata, but
it needed a stable allocation-free marker format so future boot, runner, and
panic diagnostics can report that metadata without opening sockets or touching a
live NIC.

## Files changed

- `src/OS.Kernel/Drivers/VirtioNetReceiveRegisterWindowTcpDispatchDiagnosticFormatter.cs`
- `tests/OS.HostTests/VirtioNetReceiveRegisterWindowTcpDispatchPlanTests.cs`
- `docs/networking.md`
- `docs/completion-gates.md`
- `done/step807.md`

## Tests run

- `dotnet build .\tests\OS.HostTests\OS.HostTests.csproj --nologo`
  - Result: pass after a transient external `OS.HostTests.exe` file lock cleared.
- `dotnet .\tests\OS.HostTests\bin\Debug\net10.0\OS.HostTests.dll`
  - Result: pass.
- Scoped `git diff --check` plus trailing-whitespace scan for the changed
  files.
  - Result: pass; Git reported LF/CRLF conversion warnings only.

## Evidence

- Build log: `artifacts/logs/issue764-host-build-r2.log`
  - `Build succeeded.`
  - `0 Warning(s)`
  - `0 Error(s)`
- Host-test log: `artifacts/logs/issue764-host-tests.log`
  - `TEST_PASS drivers.virtionet.rx.tcp.dispatch formatter formats success marker`
  - `TEST_PASS drivers.virtionet.rx.tcp.dispatch formatter reports short destination`
  - `TEST_PASS drivers.virtionet.rx.tcp.dispatch formatter rejects default result`
  - `TEST_PASS drivers.virtionet.rx.tcp.dispatch formatter uses shared ascii text builder`
  - `TEST_PASS host.tests`
- `artifacts/logs/issue764-diff-check-r2.log` records
  `git-diff-check-exit=0`.
- `artifacts/logs/issue764-trailing-whitespace-r2.log` records
  `TEST_PASS issue764.trailing_whitespace`.

## Failures

The first build attempt, logged at `artifacts/logs/issue764-host-build.log`,
failed because another `OS.HostTests.exe` process held the generated apphost.
No compile failure was observed. The required build command passed after that
process exited.

## Deferred work

This is host-tested diagnostic formatting only. It does not implement or prove
live virtio-net RX, packet I/O, socket/PAL receive, hosted .NET, ASP.NET
Minimal API, Kestrel, cloud readiness, or production readiness.
