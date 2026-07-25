# Step 730 - TCP Receive-Dispatch Diagnostic Formatter

## Summary

Added a caller-owned diagnostic formatter for `TcpReceiveDispatchResult`. The formatter emits a stable `TCP_RX_DISPATCH` ASCII marker with dispatch/action/network/TCP packet/stream/close/checksum status names, payload and ACK byte counts, TCP segment range metadata, and local/remote sequence numbers.

## Why

TCP receive dispatch already had host-tested payload, FIN, RST, ACK, checksum, and failure behavior, but it lacked a stable marker formatter comparable to the HTTP/TCP GET and FetchApp completion diagnostics. This slice makes receive-side TCP evidence easier to log and parse without adding sockets, live NIC I/O, timers, retransmission, TLS, hosted .NET, ASP.NET Minimal API, or Kestrel claims.

## Files changed

- `src/OS.Kernel/Net/TcpReceiveDispatchDiagnosticFormatter.cs`
- `tests/OS.HostTests/TcpReceiveDispatcherTests.cs`
- `docs/networking.md`
- `done/step730.md`

## Tests run

- `dotnet build .\tests\OS.HostTests\OS.HostTests.csproj --nologo`
- `dotnet run --project .\tests\OS.HostTests\OS.HostTests.csproj --no-build`
- `git diff --check`

## Evidence

- `artifacts\logs\issue698-host-build-20260525.log`: host test project build succeeded with 0 warnings and 0 errors.
- `artifacts\logs\issue698-hosttests-20260525.log`: host tests passed with:
  - `TEST_PASS net.tcp.rx.dispatch diagnostic formats payload result`
  - `TEST_PASS net.tcp.rx.dispatch diagnostic formats failure result`
  - `TEST_PASS net.tcp.rx.dispatch diagnostic reports short destination`
  - `TEST_PASS net.tcp.rx.dispatch diagnostic rejects default result`
  - `TEST_PASS host.tests`
- `artifacts\logs\issue698-git-diff-check-postdone-20260525.log`: `git diff --check` exited 0 after docs and done-step updates; output contains only existing LF-to-CRLF working-copy warnings.

## Failures

- No remaining test failure is known for this slice.

## Deferred work

- Wire `TCP_RX_DISPATCH` diagnostics into future boot/serial logging after a real receive path exists.
- Connect TCP receive dispatch to future TCP control-block and socket ownership.
- Add live virtio-net RX, timers, retransmission, receive windows, and socket/PAL delivery in later issues.

## Evidence boundary

This is host-tested diagnostic formatting only. It does not prove live virtio-net RX, socket/PAL delivery, TCP retransmission, receive-window management, timers, TLS, QEMU boot, cloud-provider support, hosted .NET, ASP.NET Minimal API, Kestrel behavior, or production readiness.

