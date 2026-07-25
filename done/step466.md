# Step 466 - TCP passive backlog bridge default guard

## Summary

Implemented Gitea issue #451 by making
`TcpPassiveListenerBacklogBridgeResult` reject default/zero values as
successful passive-open-to-listener-backlog bridge evidence.

## Why

`TcpPassiveListenerBacklogBridgeStatus.Success` is zero. The bridge result
previously reported success for
`default(TcpPassiveListenerBacklogBridgeResult)` because `Succeeded` only
checked status. Bridge callers consume passive-frame, TCP, packet, passive
state, backlog status, count/capacity, required-capacity, entry-index, and
connection metadata from this result, so uninitialized result state must not
count as an enqueued established connection.

## Files changed

- `src/OS.Kernel/Net/TcpPassiveListenerBacklogBridge.cs`
- `tests/OS.HostTests/TcpPassiveListenerBacklogBridgeTests.cs`
- `docs/networking.md`
- `docs/completion-gates.md`
- `done/step466.md`

## Tests run

- `dotnet build .\tests\OS.HostTests\OS.HostTests.csproj --nologo`
- `dotnet run --project .\tests\OS.HostTests\OS.HostTests.csproj --no-build | Select-String -Pattern 'net\.tcp\.passive\.backlog default result is not success|TEST_PASS host\.tests'`
- `dotnet build .\OS.slnx --nologo -m:1`
- `.\scripts\check-naming.ps1`
- `git diff --check`

## Evidence

- Focused host-test build passed with 0 warnings and 0 errors.
- TCP passive backlog bridge default-result marker evidence:

```text
TEST_START net.tcp.passive.backlog default result is not success
TEST_PASS net.tcp.passive.backlog default result is not success
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
  TCP listeners, sockets, timers, retransmission, hosted .NET 10, ASP.NET
  Minimal API, Kestrel, cloud readiness, or production readiness.

