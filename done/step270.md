# Step 270 - Host-Tested Passive TCP/IPv4 Frame Planner

## Summary

Added the smallest caller-buffer Ethernet/IPv4 wrapper for the listener-side TCP passive-open handshake. The new planner accepts a remote SYN frame, writes a local SYN-ACK frame, accepts the final ACK frame, and exposes endpoint plus sequence metadata without implementing sockets, listener tables, retransmission, timers, ARP, route selection, virtio-net, or Kestrel behavior.

## Why

Gitea issue #270 needed the passive-open TCP segment state machine to be usable at the Ethernet/IPv4 frame boundary, matching the existing active-connect frame planner while preserving the current packet-primitive claim boundary.

## Files changed

- `src/OS.Kernel/Net/TcpPassiveIpv4FramePlanner.cs`
- `tests/OS.HostTests/TcpPassiveIpv4FramePlannerTests.cs`
- `docs/networking.md`
- `docs/kestrel.md`
- `done/step270.md`

## Tests run

- `dotnet build .\src\OS.Kernel\OS.Kernel.csproj --nologo -p:BaseOutputPath=..\..\artifacts\isolated-bin\OS.Kernel\ -p:BaseIntermediateOutputPath=..\..\artifacts\isolated-obj\OS.Kernel\`
- `dotnet build .\OS.slnx --nologo -m:1`
- `dotnet run --project .\tests\OS.HostTests\OS.HostTests.csproj --no-build`
- `.\scripts\check-naming.ps1`
- `git diff --check`

## Evidence

Initial plain solution build hit a transient file lock on `tests/OS.HostTests\bin\Debug\net10.0\OS.Kernel.dll` and `OS.Std.NoRuntime.Shared.dll` from an existing `.NET Host` process. After the process exited, the requested build succeeded.

```text
dotnet build .\OS.slnx --nologo -m:1
Build succeeded.
0 Warning(s)
0 Error(s)
```

Host tests passed and emitted the required issue #270 markers:

```text
TEST_PASS net.tcp.passive.ipv4 writes syn ack frame
TEST_PASS net.tcp.passive.ipv4 accepts final ack frame
TEST_PASS net.tcp.passive.ipv4 rejects wrong listener identity
TEST_PASS net.tcp.passive.ipv4 rejects malformed protocol frames
TEST_PASS net.tcp.passive.ipv4 rejects bad final ack
TEST_PASS net.tcp.passive.ipv4 rejects short output
TEST_PASS host.tests
```

`.\scripts\check-naming.ps1` passed with:

```text
TEST_START naming.guard
TEST_PASS naming.guard
TEST_START source.firewall
TEST_PASS source.firewall
```

`git diff --check` exited `0`. It printed only line-ending normalization warnings for existing dirty files.

## Failures

- The first plain solution build failed because another `.NET Host` process had host-test output DLLs locked. No code failure was observed; a retry after the lock cleared succeeded.

## Deferred work

- No sockets, listener tables, TCP backlog/accept queue, retransmission, timers, ARP resolution, route selection, TCP pseudo-header checksum validation, virtio-net packet I/O, or Kestrel behavior was added.

