# Step 032 - Network Packet Endian Primitive Migration

## What Changed

- Migrated network packet integer encoding and decoding to `OS.Std.Binary.EndianPrimitives`.
- Removed the packet-local `NetworkByteOrder` helper.
- Added an `OS.Kernel` project reference to `std/no-runtime/shared`.
- Updated networking docs to require shared endian primitives for future packet parsers.

## Why It Changed

Network byte-order helpers belong in the shared no-runtime utility layer so packet, storage, and future protocol parsers do not grow incompatible local byte-shifting code. This keeps BCL-like utility behavior centralized before more networking stages are implemented.

## Files Changed

- `src/OS.Kernel/OS.Kernel.csproj`
- `src/OS.Kernel/Net/EthernetHeader.cs`
- `src/OS.Kernel/Net/Ipv4Address.cs`
- `src/OS.Kernel/Net/Ipv4Header.cs`
- `src/OS.Kernel/Net/UdpHeader.cs`
- `src/OS.Kernel/Net/NetworkByteOrder.cs`
- `docs/networking.md`
- `done/step032.md`

## Tests Run

```powershell
dotnet build .\OS.slnx --nologo -m:1
dotnet run --project .\tests\OS.HostTests\OS.HostTests.csproj --no-build
.\scripts\check-naming.ps1
git diff --check
```

## Evidence

- Host test marker: `TEST_PASS network packets round-trip ethernet ipv4 udp bytes`
- Host test marker: `TEST_PASS network packets reject invalid ethernet frame length`
- Host test marker: `TEST_PASS network packets reject unsupported ethertype`
- Host test marker: `TEST_PASS network packets reject unsupported ipv4 protocol`
- Host test marker: `TEST_PASS network packets reject ipv4 options`
- Host test marker: `TEST_PASS network packets reject bad ipv4 checksum`
- Host test marker: `TEST_PASS network packets reject truncated ipv4 payload`
- Host test marker: `TEST_PASS network packets reject truncated udp payload`
- Full host test marker: `TEST_PASS host.tests`

## Failures

None for this migration slice.

## Deferred Work

- ARP, DHCP, DNS, TCP, and HTTP parser/encoder migrations as those types are introduced.
- Virtio-net device discovery and RX/TX queue work.
- Real packet I/O in QEMU/libvirt.

## Evidence Boundary

This step proves host-tested packet byte serialization and parser rejection behavior after the shared endian migration. It does not prove virtio-net, DHCP, DNS, TCP, HTTP, ASP.NET Minimal API, or Kestrel support.
