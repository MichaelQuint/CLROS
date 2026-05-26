# Step 408 - UDP transmit checksum generation

## Summary

Implemented host-tested IPv4 UDP pseudo-header checksum generation for
caller-owned outbound Ethernet/IPv4/UDP frames.

## Why

UDP transmit helpers previously emitted checksum zero, which left outbound
fixtures weaker than the receive-side checksum validation added in the prior
slice. This change makes generated UDP datagrams carry a non-zero checksum that
validates against the IPv4 pseudo header while preserving the no-live-NIC and
no-socket boundary.

## Files changed

- `src/OS.Kernel/Net/EthernetIpv4UdpPacket.cs`
- `tests/OS.HostTests/NetworkPacketTests.cs`
- `tests/OS.HostTests/UdpTransmitPlannerTests.cs`
- `tests/OS.HostTests/UdpReceiveDispatcherTests.cs`
- `docs/networking.md`
- `docs/completion-gates.md`
- `done/step408.md`

## Tests run

- `dotnet build .\tests\OS.HostTests\OS.HostTests.csproj --nologo`
- `dotnet run --project .\tests\OS.HostTests\OS.HostTests.csproj --no-build`
- `dotnet build .\OS.slnx --nologo -m:1`
- `git diff --check`

## Evidence

- Focused host build passed with 0 warnings and 0 errors.
- Host test harness emitted:
  - `TEST_PASS net.udp.tx writes nonzero checksum`
  - `TEST_PASS net.udp.tx checksum validates with pseudo header`
  - `TEST_PASS network packets round-trip ethernet ipv4 udp checksum`
  - `TEST_PASS net.udp.rx accepts zero checksum`
  - `TEST_PASS net.udp.rx validates nonzero checksum`
  - `TEST_PASS net.udp.rx rejects bad checksum`
  - `TEST_PASS host.tests`
- Full solution build passed with 0 warnings and 0 errors.
- `git diff --check` passed with only repository line-ending warnings.

## Failures

- The first host-test run after enabling transmit checksums exposed stale UDP
  receive fixtures that attempted to recompute checksums over already checksummed
  datagrams. The fixture was corrected to explicitly request checksum-zero or
  generated-checksum buffers, and the rerun passed.

## Deferred work

- Outbound TCP frame checksum generation remains a separate slice.
- Established TCP payload checksum validation remains a separate slice.
- Live virtio-net TX/RX, UDP sockets, DHCP/DNS over a live NIC, hosted .NET,
  Kestrel, cloud readiness, and production readiness remain unclaimed.

## Evidence boundary

This is host-only caller-owned outbound UDP frame generation. It proves
`EthernetIpv4UdpPacket.TryWrite` and `UdpTransmitPlanner` emit non-zero IPv4
UDP checksums that validate over the pseudo header; it does not prove live NIC
descriptor publication, ARP routing, DHCP/DNS live traffic, UDP sockets, hosted
.NET, Kestrel, cloud readiness, or production readiness.
