# Step 198 - DHCP client transaction planner

## What changed

- Added `OS.Net.DhcpClientTransactionPlanner`, a stateful DHCPv4 planner that composes the existing DHCP message state machine with DHCP-over-UDP frame helpers.
- Added status-coded results for invalid client identity, invalid transitions, malformed packets, wrong UDP ports, wrong transaction ID, missing server identifiers, missing/offered address mismatches, lease-time failures, and invalid materialized lease configuration.
- Added host tests for deterministic DHCPDISCOVER frame writing, full discover/offer/request/ack planning, wrong transaction ID rejection, invalid transition rejection, missing server-id rejection, malformed packet rejection, and invalid lease configuration rejection.

## Why it changed

This closes the next host-testable networking gap between raw DHCP payload parsing and future NIC-backed UDP packet flow. The planner still owns no timers, sockets, retries, route selection, queue publication, or interface address application.

## Files changed

- `src/OS.Kernel/Net/DhcpClientTransactionPlanner.cs`
- `tests/OS.HostTests/DhcpClientTransactionPlannerTests.cs`
- `done/step198.md`

## Tests run

```text
dotnet build .\OS.slnx --nologo -m:1
Build succeeded. 8 Warning(s), 0 Error(s)
```

The build warnings were MSBuild copy retries because another worker still had `tests/OS.HostTests/bin/Debug/net10.0/OS.HostTests.exe` open.

```text
dotnet build .\tests\OS.HostTests\OS.HostTests.csproj --nologo --no-restore -m:1 /p:UseAppHost=false
Build succeeded. 0 Warning(s), 0 Error(s)
```

```text
dotnet exec .\tests\OS.HostTests\bin\Debug\net10.0\OS.HostTests.dll
TEST_PASS net.dhcp.client.transaction writes discover frame
TEST_PASS net.dhcp.client.transaction completes offer request ack
TEST_PASS net.dhcp.client.transaction rejects wrong xid
TEST_PASS net.dhcp.client.transaction rejects invalid transition
TEST_PASS net.dhcp.client.transaction rejects missing server id
TEST_PASS net.dhcp.client.transaction rejects malformed packet
TEST_PASS net.dhcp.client.transaction rejects invalid lease config
```

The full host-test process later failed in an unrelated storage test:

```text
TEST_FAIL storage.gpt.parser rejects entry array out of range
Unhandled exception. System.InvalidOperationException: GPT partition table parser host tests failed: 1
```

## Evidence boundary

This is host-only packet/planner evidence. It proves deterministic DHCP transaction planning over caller-owned Ethernet or virtio-net receive buffers and materialization into `DhcpLeaseConfiguration`. It does not prove live virtio-net TX/RX, ARP-backed route selection, UDP socket delivery, retransmission, lease renewal, interface address assignment, or QEMU/libvirt DHCP success.

## Deferred work

- Wire the planner to future virtio-net TX/RX lifecycle helpers.
- Add explicit retry/timeout policy once kernel timers are available.
- Apply a successful lease to a network interface configuration object once interface state exists.
- Route DNS server metadata into the DNS UDP planner once the interface configuration path exists.
