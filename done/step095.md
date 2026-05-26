# Step 095 - DHCP Client Transaction State Machine

## What Changed

- Added `DhcpClientTransaction` for a deterministic DHCPv4 discover-offer-request-ack state machine over caller-provided message buffers.
- Added status-coded validation for transaction ID, client MAC, server identifier, offered address, lease time, and timeout behavior.
- Added host tests for successful binding, mismatched transaction ID rejection, and discover timeout.

## Why It Changed

Issue #114 moves DHCP from message encode/decode primitives toward a client flow that can later be wired to UDP and virtio-net packet I/O.

## Files Changed

- `src/OS.Kernel/Net/DhcpClientTransaction.cs`
- `tests/OS.HostTests/DhcpClientTests.cs`
- `docs/networking.md`
- `docs/completion-gates.md`

## Tests Run

```powershell
dotnet build .\OS.slnx --nologo -m:1
dotnet run --project .\tests\OS.HostTests\OS.HostTests.csproj --no-build
```

## Logs And Evidence

- Host tests emitted:
  - `TEST_PASS net.dhcp client transitions discover-offer-request-ack`
  - `TEST_PASS net.dhcp client rejects mismatched xid`
  - `TEST_PASS net.dhcp client times out discover`

## Failures

- No build failure after integration.
- Worker-level full host sweep reported unrelated later test instability, so this step requires main-rollout full-suite confirmation before Gitea closure.

## Deferred Work

- No real UDP packet I/O.
- No retransmission timers.
- No lease renewal.
- No interface address assignment.
- No virtio-net or live DHCP evidence.

## Evidence Boundary

This step proves host-side DHCP transaction state only. It is not live DHCP, network driver, TCP/IP stack, hosted .NET 10, or Kestrel evidence.
