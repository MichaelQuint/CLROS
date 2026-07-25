# Step 108 - Mock TCP Passive-Open Handshake Documentation

## What Changed

- Documented the `TcpPassiveOpenHandshake` current boundary in networking and completion-gate docs.
- Recorded the #126 host-test markers for listener-side `LISTEN -> SYN_RECEIVED -> ESTABLISHED` handshake behavior.
- Clarified that this is mock TCP segment-buffer evidence only.

## Why It Changed

Issue #126 adds the first listener-side TCP handshake slice needed by future socket, PAL, and Kestrel work. The docs need to prevent agents from treating the mock passive-open state machine as a real TCP listener.

## Files Changed

- `docs/networking.md`
- `docs/completion-gates.md`
- `done/step108.md`

## Tests Run

```powershell
dotnet build .\OS.slnx --nologo -m:1
dotnet run --project .\tests\OS.HostTests\OS.HostTests.csproj --no-build > .\artifacts\logs\host-tests-current.log
```

## Logs And Evidence

- Build passed with 0 warnings and 0 errors.
- Host tests emitted:
  - `TEST_PASS net.tcp passive handshake transitions listen-synack-established`
  - `TEST_PASS net.tcp passive handshake rejects wrong final ack`
  - `TEST_PASS net.tcp passive handshake rejects invalid syn`
  - `TEST_PASS host.tests`

## Failures

- No failures in the final coordinated verification pass.

## Deferred Work

- No listener table.
- No accept queue.
- No retransmission, timers, receive windows, data transfer, close state, sockets, PAL integration, virtio-net packet I/O, hosted .NET, or Kestrel.

## Evidence Boundary

This step documents a mock TCP passive-open handshake only. It is not live networking, real TCP listener, exported PAL socket, ASP.NET Minimal API, or Kestrel evidence.
