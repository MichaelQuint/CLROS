# Step 409 - Active TCP IPv4 transmit checksum generation

## Summary

Implemented host-tested IPv4 TCP pseudo-header checksum generation for
caller-owned active-connect SYN and final ACK frames.

## Why

The active-connect frame planner already validated inbound SYN-ACK checksums,
but outbound SYN/final ACK frames still carried the configured fixture checksum.
This slice makes active-connect frame generation produce real non-zero TCP
checksums while preserving the caller-owned, host-only packet boundary.

## Files changed

- `src/OS.Kernel/Net/TcpIpv4FramePlanner.cs`
- `tests/OS.HostTests/TcpIpv4FramePlannerTests.cs`
- `docs/networking.md`
- `docs/completion-gates.md`
- `done/step409.md`

## Tests run

- `dotnet build .\tests\OS.HostTests\OS.HostTests.csproj --nologo`
- `dotnet run --project .\tests\OS.HostTests\OS.HostTests.csproj --no-build`
- `dotnet build .\OS.slnx --nologo -m:1`
- `git diff --check`

## Evidence

- Focused host build passed with 0 warnings and 0 errors.
- Host test harness emitted:
  - `TEST_PASS net.tcp.ipv4.frame writes syn checksum`
  - `TEST_PASS net.tcp.ipv4.frame writes final ack checksum`
  - `TEST_PASS net.tcp.ipv4.frame accepts syn ack frame`
  - `TEST_PASS net.tcp.ipv4.frame rejects bad tcp checksum`
  - `TEST_PASS net.http.tcp.get rejects bad synack checksum`
  - `TEST_PASS host.tests`
- Full solution build passed with 0 warnings and 0 errors.
- `git diff --check` passed with only repository line-ending warnings.

## Failures

- None.

## Deferred work

- Passive-open outbound SYN-ACK checksum generation remains a separate slice.
- Established TCP payload checksum generation/validation remains a separate
  slice.
- Live virtio-net TX/RX, TCP retransmission/timers, sockets/PAL exports, hosted
  .NET, Kestrel, cloud readiness, and production readiness remain unclaimed.

## Evidence boundary

This is host-only caller-owned active-connect Ethernet/IPv4/TCP frame
generation. It proves SYN and final ACK frames emitted by
`TcpIpv4FramePlanner` carry non-zero IPv4 TCP pseudo-header checksums that
validate; it does not prove passive SYN-ACK checksum generation, established
payload checksum enforcement, live NIC traffic, retransmission, timers, socket
handles, hosted .NET, Kestrel, cloud readiness, or production readiness.
