# Step 467 - TCP listener socket bridge default guard

## Summary

Implemented Gitea issue #452 by making
`TcpListenerBacklogSocketBridgeResult` reject default/zero values as successful
listener-backlog-to-mock-accept-queue bridge evidence.

## Why

`TcpListenerBacklogSocketBridgeStatus.Success` is zero. The bridge result
previously reported success for
`default(TcpListenerBacklogSocketBridgeResult)` because `Succeeded` only
checked status. Bridge callers consume backlog status/count/capacity,
entry-index, socket status, endpoint metadata, and accepted connection metadata
from this result, so uninitialized result state must not count as a bridged
accepted connection.

## Files changed

- `src/OS.Kernel/Net/TcpListenerBacklogSocketBridge.cs`
- `tests/OS.HostTests/TcpListenerBacklogSocketBridgeTests.cs`
- `docs/networking.md`
- `docs/completion-gates.md`
- `done/step467.md`

## Tests run

- `dotnet build .\tests\OS.HostTests\OS.HostTests.csproj --nologo`
- `dotnet run --project .\tests\OS.HostTests\OS.HostTests.csproj --no-build | Select-String -Pattern 'net\.tcp\.listener\.socketbridge default result is not success|TEST_PASS host\.tests'`
- `dotnet build .\OS.slnx --nologo -m:1`
- `.\scripts\check-naming.ps1`
- `git diff --check`

## Evidence

- Focused host-test build passed with 0 warnings and 0 errors.
- TCP listener socket bridge default-result marker evidence:

```text
TEST_START net.tcp.listener.socketbridge default result is not success
TEST_PASS net.tcp.listener.socketbridge default result is not success
TEST_PASS host.tests
```

- Full solution build passed with 0 warnings and 0 errors.
- Naming and source firewall guard emitted:

```text
TEST_START naming.guard
TEST_PASS naming.guard
TEST_START source.firewall
TEST_PASS source.firewall
```

- `git diff --check` exited 0. Git reported existing LF-to-CRLF working-copy
  warnings only.

## Failures

- None.

## Deferred work

- This is host-tested metadata-bridge evidence only. It does not implement live
  TCP listeners, sockets, scheduler waits, retransmission, hosted .NET 10,
  ASP.NET Minimal API, Kestrel, cloud readiness, or production readiness.
