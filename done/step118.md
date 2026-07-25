# Step 118 - PAL Mock Connected Socket IO

## What Changed

- Extended `OSHostSocketTable` connected handles with deterministic mock inbound and outbound byte buffers.
- Added host-only table methods for send, receive, poll, shutdown, inbound test injection, and outbound test draining.
- Added shutdown constants for receive, send, and both directions.
- Added host tests for the issue #139 acceptance markers:
  - `TEST_PASS pal.socket send receive transfers bytes`
  - `TEST_PASS pal.socket poll reports readiness`
  - `TEST_PASS pal.socket shutdown unblocks io`
  - `TEST_PASS pal.socket send rejects invalid state`

## Why It Changed

Issue #139 needed the next mock PAL socket-table slice after create, bind, listen, and accept. The table now models connected-handle I/O outcomes without exporting `OSHost_*` functions or claiming real TCP, hosted .NET 10, ASP.NET Minimal API, or Kestrel support.

## Files Changed

- `src/OS.Kernel/Pal/OSHostSocketAbi.cs`
- `src/OS.Kernel/Pal/OSHostSocketTable.cs`
- `tests/OS.HostTests/PalSocketTableTests.cs`
- `docs/networking.md`
- `done/step118.md`

## Tests Run

```powershell
dotnet build .\OS.slnx --nologo -m:1
dotnet run --project .\tests\OS.HostTests\OS.HostTests.csproj --no-build
```

## Logs And Evidence

- `dotnet build .\OS.slnx --nologo -m:1` passed with 0 warnings and 0 errors.
- Parent-owned full host tests emitted:
  - `TEST_PASS pal.socket send receive transfers bytes`
  - `TEST_PASS pal.socket poll reports readiness`
  - `TEST_PASS pal.socket shutdown unblocks io`
  - `TEST_PASS pal.socket send rejects invalid state`
  - `TEST_PASS host.tests`

## Failures

- None in the parent-owned verification run.

## Deferred Work

- No exported `OSHost_SocketSend`, `OSHost_SocketReceive`, `OSHost_SocketPoll`, or `OSHost_SocketShutdown` symbols exist yet.
- No guest socket behavior, real TCP stream, virtio-net packet I/O, hosted .NET 10 runtime, ASP.NET Minimal API, or Kestrel behavior is proven.
- Real close semantics, TCP FIN/RST behavior, scheduler wait unblocking, timeouts, and backpressure remain future work.

## Evidence Boundary

This step is managed host-test mock socket-table evidence only. It does not prove real TCP streams, packet I/O, exported PAL ABI behavior, hosted .NET 10, ASP.NET Minimal API, or Kestrel.
