# Step 412 - HTTP GET inbound response TCP checksum validation

## Summary

Implemented host-tested IPv4 TCP pseudo-header checksum validation for inbound
HTTP GET response frames handled by `HttpTcpClientGetPlanner`.

## Why

The HTTP TCP GET planner owns the Ethernet/IPv4 frame context for inbound
responses. Validating the response checksum there prevents corrupted payload
frames from advancing the pending established stream or HTTP response parser
without moving IPv4 pseudo-header knowledge into `TcpEstablishedStream`.

## Files changed

- `src/OS.Kernel/Net/HttpTcpClientGetPlanner.cs`
- `tests/OS.HostTests/HttpTcpClientGetPlannerTests.cs`
- `docs/networking.md`
- `docs/completion-gates.md`
- `done/step412.md`

## Tests run

- `dotnet build .\tests\OS.HostTests\OS.HostTests.csproj --nologo`
- `dotnet run --project .\tests\OS.HostTests\OS.HostTests.csproj --no-build`
- `dotnet build .\OS.slnx --nologo -m:1`
- `git diff --check`

## Evidence

- Focused host-test build passed with 0 warnings and 0 errors.
- Host-test run passed and emitted:
  - `TEST_PASS net.http.tcp.get rejects bad response checksum`
  - `TEST_PASS net.http.tcp.get completes request response`
  - `TEST_PASS host.tests`
- Full solution build passed with 0 warnings and 0 errors.
- `git diff --check` passed with line-ending normalization warnings only.

## Failures

None.

## Deferred work

- Generic stream-level checksum enforcement outside the HTTP GET planner.
- Live virtio-net I/O, ARP routing, sockets/PAL exports, retransmission,
  timers, hosted .NET, ASP.NET Minimal API, Kestrel, cloud readiness, and
  production readiness.

## Evidence boundary

This is caller-owned host-test evidence for inbound HTTP GET established TCP
payload checksum validation only. It does not prove generic stream-level
checksum enforcement, sockets/PAL exports, live virtio-net I/O, retransmission,
timers, hosted .NET, ASP.NET Minimal API, Kestrel, cloud readiness, or
production readiness.

