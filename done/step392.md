# Step 392 - TCP listener backlog table

## Summary
Added a host-tested TCP listener backlog table over caller-owned entry storage.

## Why
The TCP stack had passive-open handshake and frame-planner metadata, but future listener/socket work needs a bounded queue for already-established passive-open endpoint metadata before PAL socket accept or live NIC paths exist.

## Files changed
- `src/OS.Kernel/Net/TcpListenerBacklog.cs`
- `tests/OS.HostTests/TcpListenerBacklogTests.cs`
- `docs/networking.md`
- `docs/kestrel.md`
- `docs/completion-gates.md`
- `done/step392.md`

## Tests run
- `dotnet build .\tests\OS.HostTests\OS.HostTests.csproj --nologo`
- `dotnet run --project .\tests\OS.HostTests\OS.HostTests.csproj --no-build`
- `dotnet build .\OS.slnx --nologo -m:1`
- `git diff --check`

## Evidence
- Focused host build passed with 0 warnings and 0 errors.
- Focused marker scan emitted:
  - `TEST_PASS net.tcp.listener enqueues passive connection`
  - `TEST_PASS net.tcp.listener dequeues accepted connection`
  - `TEST_PASS net.tcp.listener rejects duplicate endpoint`
  - `TEST_PASS net.tcp.listener reports backlog capacity`
  - `TEST_PASS net.tcp.listener result contains no managed references`
  - `TEST_PASS host.tests`
- Full solution build passed with 0 warnings and 0 errors.
- `git diff --check` exited 0 with only LF-to-CRLF working-copy normalization warnings.

## Failures
None.

## Deferred work
- Connect the backlog to a broader TCP control-block table and listener policy.
- Wire accepted backlog entries into future PAL socket handles after socket exports and scheduler/timer behavior exist.

## Evidence boundary
This is host-tested TCP metadata only. It enqueues and accepts already-established endpoint metadata supplied by tests over caller-owned storage. It does not parse packets, validate TCP checksums, allocate TCP control blocks, retransmit SYN-ACK frames, own timers, export sockets, transfer payload bytes, use virtio-net, start hosted .NET 10, start ASP.NET Minimal API/Kestrel, prove cloud readiness, or prove production readiness.
