# Step 037 - ARP Packet Primitives

## What Changed

- Added `OS.Net.ArpPacket` for fixed Ethernet/IPv4 ARP request and reply encode/decode.
- Added ARP EtherType support and an Ethernet payload dispatch helper that distinguishes IPv4 and ARP.
- Added status codes for unsupported ARP hardware type, protocol type, address lengths, and operation.
- Added host tests for ARP request/reply round trips, malformed ARP fields, truncated packets, and Ethernet dispatch.
- Updated networking docs to mark ARP packet primitives as host-tested while preserving the no-network-I/O boundary.

## Why It Changed

Gitea issue #57 requires the next Layer 1 networking primitive before real virtio-net RX/TX, DHCP, DNS, TCP, HTTP, PAL sockets, hosted .NET 10, or Kestrel work can be claimed.

## Files Changed

- `src/OS.Kernel/Net/ArpPacket.cs`
- `src/OS.Kernel/Net/EthernetHeader.cs`
- `src/OS.Kernel/Net/EthernetPayloadKind.cs`
- `src/OS.Kernel/Net/PacketParseStatus.cs`
- `tests/OS.HostTests/ArpPacketTests.cs`
- `docs/networking.md`
- `done/step037.md`

## Tests Run

```powershell
dotnet build .\OS.slnx --nologo -m:1
dotnet run --project .\tests\OS.HostTests\OS.HostTests.csproj --no-build
```

## Evidence

- `TEST_PASS net.packet.arp.request-round-trip`
- `TEST_PASS net.packet.arp.reply-round-trip`
- `TEST_PASS net.packet.arp.reject-unsupported-hardware-type`
- `TEST_PASS net.packet.arp.reject-unsupported-protocol-type`
- `TEST_PASS net.packet.arp.reject-invalid-address-lengths`
- `TEST_PASS net.packet.arp.reject-unsupported-opcode`
- `TEST_PASS net.packet.arp.reject-truncated-packet`
- `TEST_PASS net.packet.ethernet.dispatch-arp-and-ipv4`
- Full host test marker: `TEST_PASS host.tests`

## Failures

None for this networking packet slice.

## Deferred Work

- Implement ARP neighbor resolution over a real virtio-net receive/transmit path.
- Add DHCP, DNS, TCP, HTTP GET, PAL sockets, hosted .NET 10, and Kestrel evidence in later issues.
- Keep Gitea issue #57 open for main-thread verification and closure.

## Evidence Boundary

This step proves host-side ARP packet buffer encode/decode and Ethernet dispatch only. It does not implement virtio-net, real packet I/O, ARP cache/resolution over a NIC, DHCP, DNS, TCP, HTTP client/server behavior, PAL sockets, hosted .NET 10, ASP.NET Minimal API, Kestrel, TLS, HTTPS, or CloudReady networking.
