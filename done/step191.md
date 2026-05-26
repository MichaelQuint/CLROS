# Step 191 - UDP transmit frame planner

## Summary

Implemented issue #210 as a host-testable UDP transmit planner over caller-owned buffers.

## Why

The networking stack needed a narrow send-path composition layer after ARP resolution. This step lets later DHCP, DNS, and socket work request a deterministic Ethernet/IPv4/UDP frame without owning NIC TX queues, sockets, timers, or retransmission state.

## Files changed

- `src/OS.Kernel/Net/UdpTransmitPlanner.cs`
- `tests/OS.HostTests/UdpTransmitPlannerTests.cs`
- `docs/networking.md`
- `done/step191.md`

## Tests run

- `dotnet build .\OS.slnx --nologo -m:1` - passed with 0 warnings and 0 errors.
- `dotnet run --project .\tests\OS.HostTests\OS.HostTests.csproj --no-build` - passed and ended with `TEST_PASS host.tests`.
- `.\scripts\check-naming.ps1` - passed `TEST_PASS naming.guard` and `TEST_PASS source.firewall`.
- `git diff --check` - passed; only existing CRLF normalization warnings were emitted.

## Evidence

Observed host markers:

```text
TEST_PASS net.udp.tx writes ethernet ipv4 udp frame
TEST_PASS net.udp.tx preserves addressing metadata
TEST_PASS net.udp.tx rejects invalid local identity
TEST_PASS net.udp.tx rejects invalid destination identity
TEST_PASS net.udp.tx rejects short frame buffer
TEST_PASS net.udp.tx rejects oversized payload
```

## Failures

No failures recorded yet for this slice.

## Deferred work

No NIC TX descriptor ownership, virtio-net notification, ARP resolution, UDP socket table, retransmission, DHCP/DNS integration, hosted .NET 10, Minimal API, or Kestrel behavior is implemented here.
