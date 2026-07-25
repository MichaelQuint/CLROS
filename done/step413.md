# Step 413 - Active close FIN checksum frame planner

## Summary

Added a host-tested IPv4/Ethernet wrapper for active TCP close FIN/ACK frame
generation with IPv4 TCP pseudo-header checksum emission.

## Why

`TcpConnectionClose` is intentionally a TCP segment-level close/reset state
primitive. The new wrapper keeps IPv4 pseudo-header knowledge at the
Ethernet/IPv4 frame boundary while proving the outbound active close segment no
longer depends on a caller-supplied literal checksum.

## Files changed

- `src/OS.Kernel/Net/TcpConnectionCloseIpv4FramePlanner.cs`
- `tests/OS.HostTests/TcpConnectionCloseIpv4FramePlannerTests.cs`
- `docs/networking.md`
- `docs/completion-gates.md`
- `done/step413.md`

## Tests run

- `dotnet build .\tests\OS.HostTests\OS.HostTests.csproj --nologo`
- `dotnet run --project .\tests\OS.HostTests\OS.HostTests.csproj --no-build`
- `dotnet build .\OS.slnx --nologo -m:1`
- `git diff --check`

## Evidence

- Focused host-test build passed with 0 warnings and 0 errors.
- Host-test run passed and emitted:
  - `TEST_PASS net.tcp.close.ipv4 writes active fin checksum`
  - `TEST_PASS net.tcp.close.ipv4 rejects identity and short output`
  - `TEST_PASS net.tcp close active fin ack`
  - `TEST_PASS host.tests`
- Full solution build passed with 0 warnings and 0 errors.
- `git diff --check` passed with line-ending normalization warnings only.

## Failures

None.

## Deferred work

- Inbound close ACK/FIN/RST checksum validation.
- Passive close Ethernet/IPv4 frame wrapping and checksum generation.
- Generic stream-level checksum enforcement, live virtio-net I/O, sockets/PAL
  exports, retransmission, timers, hosted .NET, ASP.NET Minimal API, Kestrel,
  cloud readiness, and production readiness.

## Evidence boundary

This is caller-owned host-test evidence for outbound active-close FIN/ACK
Ethernet/IPv4/TCP frame checksum generation only. It does not prove inbound
close ACK/FIN/RST checksum validation, passive close wrapping, live NIC I/O,
sockets/PAL exports, hosted .NET, ASP.NET Minimal API, Kestrel, cloud
readiness, or production readiness.
