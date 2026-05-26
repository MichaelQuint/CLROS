# Step 013 - Ethernet IPv4 UDP Packet Primitives

## What Changed

- Added span-based packet primitives under `src/OS.Kernel/Net`.
- Added fixed-size `MacAddress` and `Ipv4Address` value contracts.
- Added Ethernet II header read/write helpers.
- Added IPv4 header read/write helpers for minimum 20-byte headers without options.
- Added IPv4 header checksum calculation and validation.
- Added UDP header read/write helpers with zero checksum for the current IPv4 helper path.
- Added `EthernetIpv4UdpPacket` composition helpers for deterministic Ethernet + IPv4 + UDP frames.
- Added `PacketParseStatus` for malformed packet results without exceptions.
- Added host tests for round-trip and malformed packet cases.
- Updated `docs/networking.md`.

## Why It Changed

Virtio-net, DHCP, DNS, TCP, HTTP, and Kestrel prerequisites all need deterministic packet encoding and parsing below real device I/O. This slice builds only host-tested packet buffer primitives.

## Files Changed

- `src/OS.Kernel/Net/EthernetHeader.cs`
- `src/OS.Kernel/Net/EthernetIpv4UdpPacket.cs`
- `src/OS.Kernel/Net/EthernetIpv4UdpPacketInfo.cs`
- `src/OS.Kernel/Net/Ipv4Address.cs`
- `src/OS.Kernel/Net/Ipv4Header.cs`
- `src/OS.Kernel/Net/MacAddress.cs`
- `src/OS.Kernel/Net/NetworkByteOrder.cs`
- `src/OS.Kernel/Net/PacketParseStatus.cs`
- `src/OS.Kernel/Net/UdpHeader.cs`
- `tests/OS.HostTests/NetworkPacketTests.cs`
- `docs/networking.md`

## Tests Run

```powershell
dotnet build .\OS.slnx --nologo -m:1
dotnet run --project .\tests\OS.HostTests\OS.HostTests.csproj --no-build
.\scripts\check-naming.ps1
git diff --check
```

## Logs and Evidence

- Managed build passed with 0 warnings and 0 errors.
- Host tests emitted `TEST_PASS host.tests`.
- New packet markers passed:
  - `TEST_PASS network packets round-trip ethernet ipv4 udp bytes`
  - `TEST_PASS network packets reject invalid ethernet frame length`
  - `TEST_PASS network packets reject unsupported ethertype`
  - `TEST_PASS network packets reject unsupported ipv4 protocol`
  - `TEST_PASS network packets reject ipv4 options`
  - `TEST_PASS network packets reject bad ipv4 checksum`
  - `TEST_PASS network packets reject truncated ipv4 payload`
  - `TEST_PASS network packets reject truncated udp payload`
- Naming/source guard emitted `TEST_PASS naming.guard` and `TEST_PASS source.firewall`.
- `git diff --check` exited 0. It printed line-ending normalization warnings only.

## Failures

No failures remain for this slice.

## Deferred Work

- ARP packet primitives.
- virtio-net discovery and RX/TX queues.
- DHCP, DNS, TCP, HTTP client, and socket/PAL layers.
- UDP pseudo-header checksum behavior if needed by later stages.
- Hosted .NET 10, ASP.NET Minimal API, and Kestrel network compatibility.

## Evidence Boundary

This step proves host-tested Ethernet + IPv4 + UDP buffer primitives. It does not prove real packet I/O, virtio-net, ARP, DHCP, DNS, TCP, HTTP, sockets, hosted .NET 10, ASP.NET Minimal API, or Kestrel.
