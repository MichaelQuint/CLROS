# Step 815 - HTTP Static TCP IPv4 Response Frame Plan

## Summary

Added `HttpStaticTcpResponseIpv4FramePlan`, a host-tested server-side frame
planning slice that composes the accepted TCP payload -> static HTTP response
contract with `TcpEstablishedStreamIpv4FramePlanner`. The new plan writes one
complete HTTP response as a caller-owned Ethernet/IPv4/TCP PSH/ACK frame and
rejects oversized responses before the lower TCP planner can segment them.

## Why

The static TCP response plan proved request payload slicing and static response
bytes, but it still stopped before producing a response frame. This step moves
the server path one boundary closer to future tiny HTTP server evidence while
keeping the current limitations explicit: no socket table, no live virtio-net
TX, no descriptor publication, no timers, no retransmission, no TLS, no hosted
.NET, and no Kestrel.

## Files changed

- `src/OS.Kernel/Net/HttpStaticTcpResponseIpv4FramePlan.cs`
- `tests/OS.HostTests/HttpStaticTcpResponseIpv4FramePlanTests.cs`
- `docs/networking.md`
- `docs/completion-gates.md`
- `done/step815.md`

## Tests run

- Attempted `dotnet build .\tests\OS.HostTests\OS.HostTests.csproj --nologo *> .\artifacts\logs\issue776-host-build.log`.
- `dotnet build .\src\OS.Kernel\OS.Kernel.csproj --nologo *> .\artifacts\logs\issue776-kernel-build.log`
- `dotnet build .\tests\OS.HostTests\OS.HostTests.csproj --nologo *> .\artifacts\logs\issue776-host-build-r2.log`
- `dotnet .\tests\OS.HostTests\bin\Debug\net10.0\OS.HostTests.dll *> .\artifacts\logs\issue776-host-tests.log`

## Evidence

- `artifacts/logs/issue776-host-build.log` shows `OS.Kernel` built, but the
  host-test project build failed before #776 tests could run because concurrent
  #775 work left `tests\OS.HostTests\CiSmokeScriptTests.cs` referencing missing
  helper `TestIncludesHttpStaticTcpResponseMarkerPreset`.
- `artifacts/logs/issue776-kernel-build.log` passed with
  `issue776-kernel-build-exit=0`.
- `artifacts/logs/issue776-host-build-r2.log` ended with `Build succeeded.`,
  `0 Warning(s)`, and `0 Error(s)`.
- `artifacts/logs/issue776-host-tests.log` ended with `TEST_PASS host.tests`.
- `artifacts/logs/issue776-host-tests.log` emitted:
  - `TEST_PASS net.http.static.tcp.ipv4 writes 200 response frame`
  - `TEST_PASS net.http.static.tcp.ipv4 writes 404 and 405 frames`
  - `TEST_PASS net.http.static.tcp.ipv4 rejects response buffer failure`
  - `TEST_PASS net.http.static.tcp.ipv4 rejects response larger than maximum payload`
  - `TEST_PASS net.http.static.tcp.ipv4 rejects frame write failure`
  - `TEST_PASS net.http.static.tcp.ipv4 default result is not success`
- `artifacts/logs/issue776-diff-check.log` ended with
  `git-diff-check-exit=0`.
- `artifacts/logs/issue776-trailing-whitespace.log` emitted
  `TEST_PASS issue776.trailing_whitespace`.

## Failures

- Full #776 evidence is blocked on the concurrent #775 test helper compile
  error. The #775 worker was notified with the exact compiler error.
- The #775 compile blocker cleared after notification. The unchanged #776 host
  build then passed in `issue776-host-build-r2.log`, and the full host suite
  passed in `issue776-host-tests.log`.

## Deferred work

- This remains host-tested frame planning only. It does not prove live
  virtio-net TX, sockets, a TinyHttpServer loop, hosted .NET 10, ASP.NET
  Minimal API, Kestrel, cloud readiness, or production readiness.

