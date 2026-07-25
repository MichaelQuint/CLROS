# Step 411 - HTTP GET outbound TCP checksum generation

## Summary

Implemented host-tested IPv4 TCP pseudo-header checksum generation for the
outbound established HTTP GET request frame.

## Why

`HttpTcpClientGetPlanner` owns the Ethernet/IPv4 context for the request frame,
so it is the narrow boundary where the established TCP payload segment can gain
a real checksum without moving IPv4 address knowledge into
`TcpEstablishedStream`.

## Files changed

- `src/OS.Kernel/Net/HttpTcpClientGetPlanner.cs`
- `tests/OS.HostTests/HttpTcpClientGetPlannerTests.cs`
- `docs/networking.md`
- `docs/completion-gates.md`
- `done/step411.md`

## Tests run

- `dotnet build .\tests\OS.HostTests\OS.HostTests.csproj --nologo`
- `dotnet run --project .\tests\OS.HostTests\OS.HostTests.csproj --no-build`
- `dotnet build .\OS.slnx --nologo -m:1`
- `git diff --check`

## Evidence

- Focused host-test build passed with 0 warnings and 0 errors.
- Host-test run passed and emitted:
  - `TEST_PASS net.http.tcp.get writes request checksum`
  - `TEST_PASS net.http.tcp.get completes request response`
  - `TEST_PASS host.tests`
- Full solution build passed with 0 warnings and 0 errors.
- `git diff --check` passed with line-ending normalization warnings only.

## Failures

None.

## Deferred work

- Inbound established response checksum validation.
- Generic stream-level checksum enforcement.
- Live virtio-net I/O, ARP routing, sockets/PAL exports, retransmission,
  timers, hosted .NET, ASP.NET Minimal API, Kestrel, cloud readiness, and
  production readiness.

## Evidence boundary

This is caller-owned host-test evidence for outbound HTTP GET established TCP
payload checksum generation only. It does not prove inbound established response
checksum validation, generic stream-level checksum enforcement, sockets/PAL
exports, live virtio-net I/O, retransmission, timers, hosted .NET, ASP.NET
Minimal API, Kestrel, cloud readiness, or production readiness.

