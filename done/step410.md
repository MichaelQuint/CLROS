# Step 410 - Passive TCP IPv4 SYN-ACK checksum generation

## Summary

Implemented host-tested IPv4 TCP pseudo-header checksum generation for
caller-owned passive-open SYN-ACK frames.

## Why

Passive-open frame planning already validated inbound SYN and final ACK
checksums, but the generated SYN-ACK frame still carried the configured fixture
checksum. This slice makes the server-side handshake frame carry a real
non-zero TCP checksum while preserving the passive handshake and caller-owned
frame boundary.

## Files changed

- `src/OS.Kernel/Net/TcpPassiveIpv4FramePlanner.cs`
- `tests/OS.HostTests/TcpPassiveIpv4FramePlannerTests.cs`
- `docs/networking.md`
- `docs/completion-gates.md`
- `done/step410.md`

## Tests run

- `dotnet build .\tests\OS.HostTests\OS.HostTests.csproj --nologo`
- `dotnet run --project .\tests\OS.HostTests\OS.HostTests.csproj --no-build`
- `dotnet build .\OS.slnx --nologo -m:1`
- `git diff --check`

## Evidence

- Focused host build passed with 0 warnings and 0 errors.
- Host test harness emitted:
  - `TEST_PASS net.tcp.passive.ipv4 writes syn ack checksum`
  - `TEST_PASS net.tcp.passive.ipv4 writes syn ack frame`
  - `TEST_PASS net.tcp.passive.ipv4 accepts final ack frame`
  - `TEST_PASS net.tcp.passive.ipv4 rejects bad tcp checksum`
  - `TEST_PASS net.tcp.passive.backlog enqueues established connection`
  - `TEST_PASS host.tests`
- Full solution build passed with 0 warnings and 0 errors.
- `git diff --check` passed with only repository line-ending warnings.

## Failures

- None.

## Deferred work

- Established TCP payload checksum generation and validation remain separate
  slices.
- Live virtio-net TX/RX, TCP retransmission/timers, listener tables, sockets/PAL
  exports, hosted .NET, Kestrel, cloud readiness, and production readiness
  remain unclaimed.

## Evidence boundary

This is host-only caller-owned passive-open Ethernet/IPv4/TCP SYN-ACK frame
generation. It proves SYN-ACK frames emitted by `TcpPassiveIpv4FramePlanner`
carry non-zero IPv4 TCP pseudo-header checksums that validate; it does not prove
established payload checksum enforcement, a live listener, retransmission,
timers, socket handles, hosted .NET, Kestrel, cloud readiness, or production
readiness.
