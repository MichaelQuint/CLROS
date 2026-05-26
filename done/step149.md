# Step 149 - ICMP Echo Packet Contract

## What Changed

- Added `OS.Net.IcmpEchoPacket` for ICMPv4 echo request/reply encode and parse.
- Added checksum generation and validation over caller-owned packet buffers.
- Preserved identifier, sequence number, type/code, checksum, and payload slice metadata.
- Added status-coded failures for short packets, unsupported ICMP type/code, bad checksum, invalid payload length, and too-small destinations.
- Added host tests for request/reply round trips and rejection paths.
- Updated networking and completion-gate docs.

## Why It Changed

Gitea issue #169 needs a deterministic ICMP echo packet primitive before future IPv4 diagnostic ping, virtio-net packet dispatch, or cloud network smoke can use ICMP safely.

## Files Changed

- `src/OS.Kernel/Net/IcmpEchoPacket.cs`
- `src/OS.Kernel/Net/PacketParseStatus.cs`
- `tests/OS.HostTests/IcmpEchoPacketTests.cs`
- `docs/networking.md`
- `docs/completion-gates.md`
- `done/step149.md`

## Tests Run

```powershell
dotnet build .\src\OS.Kernel\OS.Kernel.csproj --nologo -m:1
dotnet build .\tests\OS.HostTests\OS.HostTests.csproj --nologo -m:1
dotnet run --project .\tests\OS.HostTests\OS.HostTests.csproj --no-build
```

## Logs And Evidence

- `dotnet build .\src\OS.Kernel\OS.Kernel.csproj --nologo -m:1` passed with 0 warnings and 0 errors.
- `dotnet build .\tests\OS.HostTests\OS.HostTests.csproj --nologo -m:1` passed with 0 warnings and 0 errors.
- Host-test execution emitted:
  - `TEST_PASS net.icmp echo request round trip`
  - `TEST_PASS net.icmp echo reply round trip`
  - `TEST_PASS net.icmp rejects bad checksum`
  - `TEST_PASS net.icmp rejects short packet`
  - `TEST_PASS net.icmp rejects unsupported type code`
  - `TEST_PASS net.icmp rejects small destination`

## Failures

- Earlier broad host-test attempts in the parallel work loop were interrupted or blocked by unrelated stacked ImageBuilder/test processes. The ICMP markers above were emitted before those interruptions.

## Deferred Work

- No IPv4 frame composer or protocol dispatch was added for ICMP.
- No virtio-net receive/transmit path was added.
- No ping transaction, retry timer, routing, or rate limiting was added.
- No QEMU, DHCP, DNS, TCP, BootSequence, or script behavior was changed.

## Evidence Boundary

This step proves only host-tested ICMPv4 echo packet encode/parse behavior over caller-owned buffers. It does not prove live packet I/O, virtio-net, IPv4 routing, diagnostic ping, cloud network reachability, hosted .NET 10, ASP.NET Minimal API, or Kestrel behavior.
