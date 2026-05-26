# Step 406 - Passive TCP IPv4 checksum validation

## Summary

Implemented host-tested TCP IPv4 pseudo-header checksum validation for the
passive-open frame planner's inbound SYN and final ACK paths.

## Why

Passive TCP listener handshakes now reject corrupted caller-owned Ethernet/IPv4
TCP frames before the listener state advances. This keeps the TCP checksum work
incremental while preserving the no-live-NIC, no-socket, no-Kestrel boundary.

## Files changed

- `src/OS.Kernel/Net/TcpPassiveIpv4FramePlanner.cs`
- `tests/OS.HostTests/TcpPassiveIpv4FramePlannerTests.cs`
- `tests/OS.HostTests/TcpPassiveListenerBacklogBridgeTests.cs`
- `docs/networking.md`
- `docs/completion-gates.md`
- `done/step406.md`

## Tests run

- `dotnet build .\tests\OS.HostTests\OS.HostTests.csproj --nologo`
- `dotnet run --project .\tests\OS.HostTests\OS.HostTests.csproj --no-build`
- `dotnet build .\OS.slnx --nologo -m:1`
- `git diff --check`

## Evidence

- Focused host build passed with 0 warnings and 0 errors.
- Host test harness emitted:
  - `TEST_PASS net.tcp.passive.ipv4 rejects bad tcp checksum`
  - `TEST_PASS net.tcp.passive.ipv4 accepts final ack frame`
  - `TEST_PASS net.tcp.passive.backlog enqueues established connection`
  - `TEST_PASS host.tests`
- Full solution build passed with 0 warnings and 0 errors.
- `git diff --check` passed after the final verification, with only
  repository line-ending warnings.

## Failures

- The first full host harness run after adding passive checksum validation
  exposed downstream passive backlog fixtures that still built zero-checksum TCP
  frames. The bridge fixture now writes valid TCP checksums before exercising
  backlog behavior, and the rerun passed.

## Deferred work

- Outbound TCP frame checksum generation remains a separate slice.
- UDP non-zero checksum enforcement remains a separate slice.
- Established TCP payload checksum validation remains a separate slice.
- Live virtio-net packet I/O, sockets/PAL exports, hosted .NET, Kestrel, cloud
  readiness, and production readiness remain unclaimed.

## Evidence boundary

This is host-only caller-owned passive TCP/IPv4 frame validation. It proves bad
SYN and final ACK checksums return `BadTcpChecksum`/`PacketParseStatus.BadChecksum`
without advancing `LISTEN` or `SYN_RECEIVED`; it does not prove a live listener,
outbound SYN-ACK checksums, established payload checksums, live NIC traffic,
sockets, hosted .NET, Kestrel, cloud readiness, or production readiness.

