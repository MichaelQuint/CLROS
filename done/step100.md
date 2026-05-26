# Step 100 - TCP Client Handshake State

## What Changed

- Added `TcpClientHandshake` for a mock-packet client-side TCP three-way handshake.
- Added status-coded SYN writing, SYN-ACK validation, and final ACK writing over caller-provided TCP segment buffers.
- Added host tests for successful transition, wrong acknowledgement number, and missing SYN-ACK flags.

## Why It Changed

Issue #119 moves TCP from header parsing toward a minimal client connection state boundary before full TCP timers, retransmission, sockets, or HTTP GET integration.

## Files Changed

- `src/OS.Kernel/Net/TcpClientHandshake.cs`
- `tests/OS.HostTests/TcpClientHandshakeTests.cs`
- `docs/networking.md`
- `docs/completion-gates.md`

## Tests Run

```powershell
dotnet build .\OS.slnx --nologo -m:1
dotnet run --project .\tests\OS.HostTests\OS.HostTests.csproj --no-build
```

## Logs And Evidence

- Host tests emitted:
  - `TEST_PASS net.tcp handshake transitions syn-synack-ack`
  - `TEST_PASS net.tcp handshake rejects wrong ack number`
  - `TEST_PASS net.tcp handshake rejects missing synack flags`

## Failures

- No build or host-test failures after integration.

## Deferred Work

- No retransmission or timers.
- No checksum/pseudo-header validation.
- No data transfer or close state.
- No sockets/PAL.
- No HTTP GET or Kestrel evidence.

## Evidence Boundary

This step proves a host-side mock TCP handshake only. It is not live TCP, HTTP, hosted .NET 10, or Kestrel evidence.
