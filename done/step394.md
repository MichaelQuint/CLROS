# Step 394 - Passive TCP backlog bridge

## Summary
Added a host-tested bridge from successful passive TCP IPv4 frame-planner final-ACK metadata into caller-owned listener backlog storage.

## Why
The TCP path had separate evidence for passive-open frame planning and caller-owned listener backlog enqueue/dequeue behavior. Future listener and PAL socket work needs a small bridge that converts established passive-open endpoint metadata into a backlog entry without adding sockets, timers, retransmission, or live NIC behavior.

## Files changed
- `src/OS.Kernel/Net/TcpPassiveListenerBacklogBridge.cs`
- `tests/OS.HostTests/TcpPassiveListenerBacklogBridgeTests.cs`
- `docs/networking.md`
- `docs/kestrel.md`
- `docs/completion-gates.md`
- `done/step394.md`

## Tests run
- `dotnet build .\tests\OS.HostTests\OS.HostTests.csproj --nologo`
- `dotnet run --project .\tests\OS.HostTests\OS.HostTests.csproj --no-build`
- `dotnet build .\OS.slnx --nologo -m:1`
- `git diff --check`

## Evidence
- Focused host build passed with 0 warnings and 0 errors.
- Focused marker scan emitted:
  - `TEST_PASS net.tcp.passive.backlog enqueues established connection`
  - `TEST_PASS net.tcp.passive.backlog rejects failed handshake`
  - `TEST_PASS net.tcp.passive.backlog rejects non-established result`
  - `TEST_PASS net.tcp.passive.backlog propagates backlog full`
  - `TEST_PASS net.tcp.passive.backlog propagates duplicate endpoint`
  - `TEST_PASS net.tcp.passive.backlog preserves no-socket boundary`
  - `TEST_PASS host.tests`
- Full solution build passed with 0 warnings and 0 errors.
- `git diff --check` exited 0 with only LF-to-CRLF working-copy normalization warnings.

## Failures
None.

## Deferred work
- Connect backlog entries to a TCP control-block table and accepted PAL socket handles.
- Add retransmission, timers, checksum validation, and receive-window/backpressure behavior before live listener claims.
- Add manifest-approved bind/listen enforcement before hosted Minimal API/Kestrel binding claims.

## Evidence boundary
This is host-tested passive-open metadata and caller-owned listener backlog enqueue evidence only. It does not run a live listener, parse live NIC packets, validate TCP pseudo-header checksums, allocate TCP control blocks, own timers, retransmit SYN-ACK frames, export socket/PAL handles, transfer payload bytes, use virtio-net, start hosted .NET 10, start ASP.NET Minimal API/Kestrel, prove cloud readiness, or prove production readiness.
