# Step 456 - TCP client handshake default guard

## Summary

Implemented Gitea issue #441 by making
`TcpClientHandshakeTransitionResult` reject default/zero values as successful
client-handshake transition evidence.

## Why

`TcpClientHandshakeStatus.Success` is zero. The transition result previously
reported success for `default(TcpClientHandshakeTransitionResult)` because
`Succeeded` only checked status. TCP callers consume handshake state, packet
status, bytes-written, and sequence-number metadata from this result, so
uninitialized transition state must not count as a completed handshake step.

## Files changed

- `src/OS.Kernel/Net/TcpClientHandshake.cs`
- `tests/OS.HostTests/TcpClientHandshakeTests.cs`
- `docs/networking.md`
- `docs/completion-gates.md`
- `done/step456.md`

## Tests run

- `dotnet build .\tests\OS.HostTests\OS.HostTests.csproj --nologo`
- `dotnet run --project .\tests\OS.HostTests\OS.HostTests.csproj --no-build | Select-String -Pattern 'net\.tcp handshake default result is not success|TEST_PASS host\.tests'`
- `dotnet build .\OS.slnx --nologo -m:1`
- `.\scripts\check-naming.ps1`
- `git diff --check`

## Evidence

- Focused host-test build passed with 0 warnings and 0 errors.
- TCP client handshake default-result marker evidence:

```text
TEST_START net.tcp handshake default result is not success
TEST_PASS net.tcp handshake default result is not success
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

- This is host-tested TCP client handshake state-machine evidence only. It does
  not implement retransmission, timers, sockets, live NIC I/O, live HTTP,
  hosted .NET 10, ASP.NET Minimal API, Kestrel, cloud readiness, or production
  readiness.
