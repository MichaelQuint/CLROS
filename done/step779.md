# Step 779 - Issue 737 TCP receive dispatch formatter shared ASCII builder

## Summary

Migrated `TcpReceiveDispatchDiagnosticFormatter` marker emission from local
destination-slice append helpers to shared `OS.Std.Text.AsciiTextBuilder`
append helpers.

## Why

Issue #737 tracks removing formatter-local ASCII copy and decimal-format paths
from the TCP receive dispatch diagnostic marker so networking diagnostics share
the same no-runtime ASCII builder surface used by other packet/app-model
formatters.

## Files changed

- `src/OS.Kernel/Net/TcpReceiveDispatchDiagnosticFormatter.cs`
- `tests/OS.HostTests/TcpReceiveDispatcherTests.cs`
- `docs/networking.md`
- `done/step779.md`

## Tests run

- `dotnet build .\tests\OS.HostTests\OS.HostTests.csproj --nologo -p:UseAppHost=false -o .\artifacts\issue737-tcp-receive-dispatch-formatter\bin\Debug\net10.0`
- `dotnet .\artifacts\issue737-tcp-receive-dispatch-formatter\bin\Debug\net10.0\OS.HostTests.dll`
- `dotnet run --project .\artifacts\issue737-tcp-only\issue737-tcp-only.csproj --no-restore`
- `dotnet .\artifacts\issue739-owned-serial-smoke-marker-builder\bin\Debug\net10.0\OS.HostTests.dll *>&1 | Tee-Object -FilePath .\artifacts\logs\issue739-hosttests-20260525.log`
- `git diff --check -- .\src\OS.Kernel\Net\TcpReceiveDispatchDiagnosticFormatter.cs .\tests\OS.HostTests\TcpReceiveDispatcherTests.cs .\docs\networking.md .\done\step779.md *>&1 | Tee-Object -FilePath .\artifacts\logs\issue737-git-diff-check-scoped-final2-20260525.log`
- `rg -n "[ \t]$" .\src\OS.Kernel\Net\TcpReceiveDispatchDiagnosticFormatter.cs .\tests\OS.HostTests\TcpReceiveDispatcherTests.cs .\docs\networking.md .\done\step779.md`

## Evidence

- Build log: `artifacts/logs/issue737-build.log`
- Full host-test command log: `artifacts/logs/issue737-hosttests.log`
- Issue-scoped TCP-only marker log: `artifacts/logs/issue737-tcp-only-hosttests.log`
- Integrated host-test log after doc integration:
  `artifacts/logs/issue739-hosttests-20260525.log`
- TCP marker evidence from the scoped harness:
  - `TEST_PASS net.tcp.rx dispatch accepts established payload`
  - `TEST_PASS net.tcp.rx dispatch acknowledges fin`
  - `TEST_PASS net.tcp.rx dispatch handles reset`
  - `TEST_PASS net.tcp.rx dispatch accepts empty ack`
  - `TEST_PASS net.tcp.rx dispatch rejects out-of-window control`
  - `TEST_PASS net.tcp.rx dispatch rejects bad checksum`
  - `TEST_PASS net.tcp.rx dispatch reports stream failure`
  - `TEST_PASS net.tcp.rx dispatch rejects non tcp`
  - `TEST_PASS net.tcp.rx dispatch default result is not success`
  - `TEST_PASS net.tcp.rx.dispatch diagnostic formats payload result`
  - `TEST_PASS net.tcp.rx.dispatch diagnostic formats failure result`
  - `TEST_PASS net.tcp.rx.dispatch diagnostic reports short destination`
  - `TEST_PASS net.tcp.rx.dispatch diagnostic rejects default result`
  - `TEST_PASS net.tcp.rx.dispatch diagnostic formatter uses shared ascii text builder`
- Integrated host-test evidence includes the same TCP receive-dispatch formatter
  markers and ends with `TEST_PASS host.tests`.
- `artifacts/logs/issue737-git-diff-check-scoped-final2-20260525.log` exited 0
  while reporting only the known LF-to-CRLF normalization warnings for
  `docs/networking.md` and `tests/OS.HostTests/TcpReceiveDispatcherTests.cs`.
- The scoped trailing-whitespace source scan returned
  `TEST_PASS issue737.scoped_whitespace_source_final2`.

## Failures

- The requested full host-test assembly command failed after unrelated
  `ci.smoke returns nonzero on failed command` timed out waiting for
  `run-ci-smoke.ps1` for 120 seconds. This occurred before the full harness
  reached the TCP receive dispatcher module.
- A later integrated host run in
  `artifacts/logs/issue739-hosttests-20260525.log` reached the TCP receive
  dispatcher module, passed the shared-builder marker, and ended with
  `TEST_PASS host.tests`.

## Deferred work

- `docs/networking.md` was intentionally not edited because another networking
  worker may own that doc in parallel. Suggested doc sentence:
  `TcpReceiveDispatchDiagnosticFormatter now formats TCP_RX_DISPATCH markers through the shared OS.Std.Text.AsciiTextBuilder append path while preserving caller-owned destination sizing, required-byte queries, and short-destination behavior.`

## Evidence boundary

This is host-test and source-guard evidence only. It does not add live NIC I/O,
socket dispatch, timers, retransmission, TLS, hosted .NET, ASP.NET Minimal API,
or Kestrel behavior.

