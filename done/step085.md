# Step 085 - DHCPv4 Message Primitives

## What Changed

- Added `DhcpMessage` encode/decode primitives.
- Added caller-owned-buffer writers for discover and request messages.
- Added offer/ack parsing for transaction ID, client MAC, offered address, server identifier, lease time, subnet mask, router, DNS server, and message type.

## Why It Changed

Issue #103 starts DHCP support at the deterministic byte contract level before virtio-net, timers, retransmission, or address assignment exist.

## Files Changed

- `src/OS.Kernel/Net/DhcpMessage.cs`
- `tests/OS.HostTests/DhcpMessageTests.cs`
- `docs/networking.md`
- `docs/completion-gates.md`

## Tests Run

```powershell
dotnet build .\OS.slnx --nologo -m:1
dotnet run --project .\tests\OS.HostTests\OS.HostTests.csproj --no-build
```

## Logs And Evidence

- Host tests emitted:
  - `TEST_PASS net.dhcp writes discover message`
  - `TEST_PASS net.dhcp parses offer options`
  - `TEST_PASS net.dhcp writes request message`
  - `TEST_PASS net.dhcp rejects malformed packets`

## Failures

- No build or host-test failures after integration.

## Deferred Work

- No DHCP client state machine.
- No lease timers, retry policy, address assignment, or NIC I/O.

## Evidence Boundary

This step proves host-side DHCP message encode/decode only. It is not live DHCP evidence.
