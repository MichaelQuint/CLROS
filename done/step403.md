# Step 403 - TCP Established Stream Mock Socket Inbound Bridge

## Summary

Added a host-only bridge from successful `TcpEstablishedStream` receive metadata plus caller-owned payload bytes into a connected mock PAL socket's inbound queue.

## Why

Kestrel needs an eventual path where accepted TCP payload bytes become socket receive bytes. This slice adds the smallest testable composition point between the existing established TCP payload primitive and the host-only `OSHostSocketTable` without claiming exported PAL sockets, scheduler waits, live TCP packet delivery, hosted .NET, or Kestrel.

## Files changed

- `src/OS.Kernel/Net/TcpEstablishedStreamSocketBridge.cs`
- `tests/OS.HostTests/TcpEstablishedStreamSocketBridgeTests.cs`
- `docs/networking.md`
- `docs/pal.md`
- `docs/kestrel.md`
- `docs/completion-gates.md`
- `done/step403.md`

## Tests run

- `dotnet build .\tests\OS.HostTests\OS.HostTests.csproj --nologo`
- `dotnet run --project .\tests\OS.HostTests\OS.HostTests.csproj --no-build`
- `dotnet build .\OS.slnx --nologo -m:1`
- `git diff --check`

## Evidence

- Focused host-test build passed with 0 warnings and 0 errors.
- Host-test run emitted:
  - `TEST_PASS net.tcp.stream.socketbridge queues inbound payload`
  - `TEST_PASS net.tcp.stream.socketbridge rejects failed receive result`
  - `TEST_PASS net.tcp.stream.socketbridge rejects payload length mismatch`
  - `TEST_PASS net.tcp.stream.socketbridge propagates mock socket failure`
  - `TEST_PASS net.tcp.stream.socketbridge preserves mock-only boundary`
  - `TEST_PASS host.tests`
- Full solution build passed with 0 warnings and 0 errors.
- `git diff --check` exited successfully.

## Failures

- Initial focused host-test build failed because the new tests referenced `OSHostSocketOperationResult.Status`; the existing PAL result contract exposes `StatusCode`. The tests were corrected before the passing evidence above.

## Deferred work

- Exported `OSHost_SocketReceive`/`OSHost_SocketSend` symbols.
- Scheduler-backed socket waits and wakeups.
- Live TCP receive delivery from a NIC-backed connection table.
- Outbound socket-to-TCP segment bridge.
- Retransmission, timers, receive-window handling, and checksum enforcement in the live path.
- Hosted .NET 10, ASP.NET Minimal API, Kestrel, cloud readiness, and production readiness.

## Evidence boundary

This is host-only mock PAL socket table composition. It proves inbound queueing into an in-process connected mock socket after an already-successful established-stream receive result. It does not export PAL functions, open live sockets, process NIC packets, implement a complete TCP stack, wake scheduler waits, run hosted .NET, start Kestrel, or prove cloud/provider readiness.
