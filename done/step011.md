# Step 011 - Ethernet IPv4 UDP Packet Primitives

## What Changed

- Added `OS.Net` packet primitives for Ethernet II, IPv4 without options, and UDP.
- Added fixed-size `MacAddress` and `Ipv4Address` value contracts.
- Added status-coded packet parsing with `PacketParseStatus`.
- Added deterministic host tests for Ethernet + IPv4 + UDP encode/decode and malformed packet handling.
- Updated `docs/networking.md` with the new packet-primitive status, tests, limitations, and next steps.

## Why It Changed

Issue #31 needs a small, testable networking foundation before virtio-net, DHCP, DNS, TCP, HTTP, or hosted Kestrel work can be credible. This step keeps the scope at protocol-buffer primitives only and avoids real packet I/O.

## Files Changed

- `src/OS.Kernel/Net/PacketParseStatus.cs`
- `src/OS.Kernel/Net/NetworkByteOrder.cs`
- `src/OS.Kernel/Net/MacAddress.cs`
- `src/OS.Kernel/Net/Ipv4Address.cs`
- `src/OS.Kernel/Net/EthernetHeader.cs`
- `src/OS.Kernel/Net/Ipv4Header.cs`
- `src/OS.Kernel/Net/UdpHeader.cs`
- `src/OS.Kernel/Net/EthernetIpv4UdpPacketInfo.cs`
- `src/OS.Kernel/Net/EthernetIpv4UdpPacket.cs`
- `tests/OS.HostTests/NetworkPacketTests.cs`
- `docs/networking.md`
- `done/step011.md`

## Tests Run

```powershell
dotnet build .\OS.slnx --nologo -m:1
dotnet run --project .\tests\OS.HostTests\OS.HostTests.csproj --no-build
```

## Logs and Evidence

- `dotnet build .\OS.slnx --nologo -m:1` passed with `0 Warning(s), 0 Error(s)`.
- `dotnet run --project .\tests\OS.HostTests\OS.HostTests.csproj --no-build` passed with `TEST_PASS host.tests`.
- Packet-specific markers passed:
  - `TEST_PASS network packets round-trip ethernet ipv4 udp bytes`
  - `TEST_PASS network packets reject invalid ethernet frame length`
  - `TEST_PASS network packets reject unsupported ethertype`
  - `TEST_PASS network packets reject unsupported ipv4 protocol`
  - `TEST_PASS network packets reject bad ipv4 checksum`
  - `TEST_PASS network packets reject truncated ipv4 payload`
  - `TEST_PASS network packets reject truncated udp payload`

## Failures

No failures remain for this packet-primitive slice.

## Deferred Work

- ARP packet primitives.
- virtio-net discovery and RX/TX queues.
- DHCP, DNS, TCP, HTTP client, and HTTP server stages.
- Socket/PAL and hosted .NET 10/Kestrel integration.
- UDP pseudo-header checksum generation/validation if required by a future milestone.

## Evidence Boundary

This step proves deterministic host-side packet encoding and decoding for Ethernet + IPv4 + UDP buffers. It does not prove virtio-net, real packet I/O, DHCP, DNS, TCP, HTTP, sockets, ASP.NET Minimal API, or Kestrel support.
