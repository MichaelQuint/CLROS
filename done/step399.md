# Step 399 - TCP Listener Backlog Mock Accept Bridge

## Summary
Added a host-tested `TcpListenerBacklogSocketBridge` that moves the oldest caller-owned TCP listener backlog entry into the in-process `OSHostSocketTable` mock accept queue.

## Why
The future Kestrel path needs a clear handoff from established passive-open metadata to a socket accept boundary. Exported PAL functions, scheduler waits, live TCP, and guest sockets are not ready, so this slice proves the metadata handoff against the existing mock socket table only.

## Files changed
- `src/OS.Kernel/Net/TcpListenerBacklogSocketBridge.cs`
- `tests/OS.HostTests/TcpListenerBacklogSocketBridgeTests.cs`
- `docs/networking.md`
- `docs/pal.md`
- `docs/kestrel.md`
- `docs/completion-gates.md`
- `done/step399.md`

## Tests run
- `dotnet build .\tests\OS.HostTests\OS.HostTests.csproj --nologo`
- `dotnet run --project .\tests\OS.HostTests\OS.HostTests.csproj --no-build`
- `dotnet build .\OS.slnx --nologo -m:1`
- `git diff --check`

## Evidence
- Focused host-test build completed with 0 warnings and 0 errors.
- Host-test marker scan emitted:
  - `TEST_PASS net.tcp.listener.socketbridge enqueues accepted connection`
  - `TEST_PASS net.tcp.listener.socketbridge rejects empty backlog`
  - `TEST_PASS net.tcp.listener.socketbridge propagates non-listener failure`
  - `TEST_PASS net.tcp.listener.socketbridge preserves accepted endpoint metadata`
  - `TEST_PASS net.tcp.listener.socketbridge preserves mock-only boundary`
  - `TEST_PASS host.tests`
- Full solution build completed with 0 warnings and 0 errors.
- `git diff --check` exited 0 with only LF-to-CRLF working-copy normalization warnings.

## Failures
- None.

## Deferred work
- Export real `OSHost_SocketAccept` only after the PAL export/runtime boundary exists.
- Connect passive-open TCP control-block state to listener ownership, scheduler waits, and live socket handles.
- Keep hosted .NET 10, ASP.NET Minimal API, Kestrel, cloud readiness, and production readiness claims blocked until in-guest runtime/socket evidence exists.

## Evidence boundary
- This is host-tested composition from caller-owned TCP listener backlog metadata into an in-process mock socket-table accept queue. It does not export PAL functions, open live sockets, create a complete TCP listener, block or wake scheduler waits, transfer payload bytes, start hosted .NET 10, start Kestrel, prove cloud readiness, or prove production readiness.
