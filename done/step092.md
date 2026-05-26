# Step 092 - TCP Header Primitive

## What Changed

- Added `TcpHeader` for host-tested TCP header encode/decode.
- Added support for ports, sequence and acknowledgement numbers, header length, flags, window, checksum, urgent pointer, aligned options, and payload slicing.
- Added malformed-header tests for invalid data offsets and truncated options.

## Why It Changed

Issue #113 adds the TCP packet primitive needed before a TCP state machine, sockets/PAL, HTTP GET integration, or Kestrel compatibility work can be credible.

## Files Changed

- `src/OS.Kernel/Net/TcpHeader.cs`
- `tests/OS.HostTests/TcpHeaderTests.cs`
- `docs/networking.md`
- `docs/completion-gates.md`

## Tests Run

```powershell
dotnet build .\OS.slnx --nologo -m:1
dotnet run --project .\tests\OS.HostTests\OS.HostTests.csproj --no-build
```

## Logs And Evidence

- Host tests emitted:
  - `TEST_PASS net.packet.tcp.header-round-trip`
  - `TEST_PASS net.packet.tcp.rejects-invalid-data-offset`
  - `TEST_PASS net.packet.tcp.rejects-truncated-options`

## Failures

- No build or host-test failures after integration.

## Deferred Work

- No TCP checksum over pseudo-header.
- No TCP state machine.
- No retransmission/timers.
- No sockets/PAL.
- No Kestrel evidence.

## Evidence Boundary

This step proves host-side TCP header parsing and writing only. It is not TCP connectivity or HTTP behavior evidence.
