# Step 192 - DHCP UDP frame wrapper

## Summary

Implemented issue #211 as a host-tested DHCP-over-UDP frame wrapper/unwrapper. The slice writes DHCPDISCOVER and DHCPREQUEST payloads into broadcast Ethernet/IPv4/UDP frames and extracts DHCP server replies from either raw Ethernet frames or virtio-net receive buffers.

## Why

The DHCP client state machine already had deterministic DHCP payload encoding and parsing, and the networking layer already had Ethernet/IPv4/UDP and virtio-net receive dispatch primitives. This step connects those layers without adding timers, retry policy, NIC queue ownership, lease application, sockets, hosted .NET, Minimal API, or Kestrel behavior.

## Files changed

- `src/OS.Kernel/Net/DhcpUdpFrame.cs`
- `tests/OS.HostTests/DhcpUdpFrameTests.cs`
- `docs/networking.md`
- `done/step192.md`

## Tests run

- `dotnet build .\OS.slnx --nologo -m:1`
- `dotnet run --project .\tests\OS.HostTests\OS.HostTests.csproj --no-build`
- `.\scripts\check-naming.ps1`
- `git diff --check`

## Evidence

Observed host-test markers:

```text
TEST_PASS net.dhcp.udp writes discover broadcast frame
TEST_PASS net.dhcp.udp writes request broadcast frame
TEST_PASS net.dhcp.udp extracts server reply payload
TEST_PASS net.dhcp.udp rejects wrong udp ports
TEST_PASS net.dhcp.udp reports short frame buffer
TEST_PASS net.dhcp.udp reports malformed reply frame
TEST_PASS host.tests
```

`check-naming.ps1` emitted:

```text
TEST_PASS naming.guard
TEST_PASS source.firewall
```

`git diff --check` exited successfully. It reported CRLF normalization warnings for existing working-copy files only.

## Failures

None for this slice.

## Deferred work

- DHCP retransmission timers and retry policy.
- Lease renewal/rebind/expiry scheduling.
- Applying DHCP lease metadata to a network interface.
- ARP conflict detection.
- Live virtio-net RX/TX queue ownership.
- UDP socket delivery and PAL socket integration.
- Hosted .NET 10, ASP.NET Minimal API, and Kestrel evidence.
