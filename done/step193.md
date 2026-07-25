# Step 193 - DNS UDP Frame Metadata

## Summary

Implemented issue #212 as a host-test-only DNS-over-UDP frame metadata layer over caller-owned packet buffers.

## Why

DNS already had message encoding/parsing and a resolver transaction, and UDP receive metadata already existed. This step composes those pieces so later network stages can write a DNS A-record query frame and validate a received DNS UDP payload without introducing sockets, route selection, retransmission, random transaction ID generation, live NIC I/O, or Kestrel claims.

## Files changed

- `src/OS.Kernel/Net/DnsUdpFrame.cs`
- `tests/OS.HostTests/DnsUdpFrameTests.cs`
- `docs/networking.md`
- `done/step193.md`

## Tests run

- `dotnet build .\OS.slnx --nologo -m:1` passed.
- `dotnet run --project .\tests\OS.HostTests\OS.HostTests.csproj --no-build` passed with `TEST_PASS host.tests`.
- `.\scripts\check-naming.ps1` passed with `TEST_PASS naming.guard` and `TEST_PASS source.firewall`.
- `git diff --check` exited 0 with CRLF normalization warnings only.

## Evidence

Observed host-test markers:

- `TEST_PASS net.dns.udp writes query frame`
- `TEST_PASS net.dns.udp resolves response payload`
- `TEST_PASS net.dns.udp rejects wrong udp ports`
- `TEST_PASS net.dns.udp reports resolver parse failure`
- `TEST_PASS net.dns.udp reports short frame buffer`

## Failures

None for this slice.

## Deferred work

- No sockets.
- No retransmission or DNS retry timers.
- No random transaction ID generation.
- No route selection or ARP integration.
- No live DNS over virtio-net.
- No DNS cache, CNAME, AAAA, TXT, or multiple-question support.
- No hosted .NET, ASP.NET Minimal API, or Kestrel evidence.
