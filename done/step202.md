# Step 202 - TCP IPv4 active-connect frame planner

## What Changed

- Added `OS.Net.TcpIpv4FramePlanner`, a host-testable Ethernet/IPv4 frame wrapper around `TcpActiveConnectTransaction`.
- Added status-coded validation for local/remote MAC identity, IPv4 identity, TCP ports, IPv4 protocol, frame bounds, malformed TCP segments, invalid SYN-ACK flags, wrong acknowledgement numbers, and wrong expected remote sequence numbers.
- Preserved diagnostic metadata for Ethernet, IPv4, TCP, byte offsets, segment lengths, active-connect state, and local/remote sequence numbers.
- Added host tests with `net.tcp.ipv4.frame` markers for SYN write, SYN-ACK receive, final ACK write, wrong peer, unsupported protocol/bounds, bad TCP SYN-ACK, invalid identity, and short output buffers.
- Updated `docs/networking.md` with implemented behavior, tests, limitations, and issue #220 markers.

## Why It Changed

The TCP active-connect transaction previously operated only on TCP segment buffers. The network stack now needs a narrow caller-owned Ethernet/IPv4 frame boundary before it can later connect TCP to virtio-net TX/RX queues, ARP-selected next-hop MAC addresses, PAL sockets, HTTP, hosted .NET 10, and Kestrel smoke tests.

## Files Changed

- `src/OS.Kernel/Net/TcpIpv4FramePlanner.cs`
- `tests/OS.HostTests/TcpIpv4FramePlannerTests.cs`
- `docs/networking.md`
- `done/step202.md`

## Tests Run

```text
dotnet build .\OS.slnx --nologo -m:1
dotnet run --project .\tests\OS.HostTests\OS.HostTests.csproj --no-build
```

## Evidence

The solution build completed with 0 errors. It reported two transient copy retry warnings because another `OS.HostTests` process briefly held `tests\OS.HostTests\bin\Debug\net10.0\OS.Kernel.dll`; the build completed successfully after retries.

The integrated host-test harness exited 0 and emitted the new markers:

```text
TEST_PASS net.tcp.ipv4.frame writes syn frame
TEST_PASS net.tcp.ipv4.frame accepts syn ack frame
TEST_PASS net.tcp.ipv4.frame writes final ack frame
TEST_PASS net.tcp.ipv4.frame rejects wrong peer
TEST_PASS net.tcp.ipv4.frame rejects protocol and bounds
TEST_PASS net.tcp.ipv4.frame rejects bad tcp syn ack
TEST_PASS net.tcp.ipv4.frame rejects identity and short output
TEST_PASS host.tests
```

## Failures

No host-test failure remained after this integrated run. No QEMU or live network smoke was run for this host-only planner slice.

## Deferred Work

- No ARP route selection or next-hop lookup is performed by this planner.
- No virtio-net TX/RX descriptor publication, device notification, interrupts, or polling is implemented.
- No TCP pseudo-header checksum generation or validation is implemented.
- No retransmission, timers, TCP control block, connection table, sockets, PAL exports, hosted .NET 10, ASP.NET Minimal API, or Kestrel behavior is claimed.

## Evidence Boundary

This step proves only deterministic TCP active-connect framing over caller-owned Ethernet/IPv4 buffers in host tests. It is not evidence of live virtio-net packet I/O, DHCP-applied network configuration, DNS-to-TCP routing, HTTP GET over a real network path, FetchApp network behavior, hosted .NET 10, ASP.NET Minimal API, or Kestrel support.
