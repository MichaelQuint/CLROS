# Step 459 - TCP passive-open handshake default guard

## Summary

Implemented Gitea issue #444 by making
`TcpPassiveOpenHandshakeTransitionResult` reject default/zero values as
successful passive-open handshake transition evidence.

## Why

`TcpPassiveOpenHandshakeStatus.Success` is zero. The transition result
previously reported success for
`default(TcpPassiveOpenHandshakeTransitionResult)` because `Succeeded` only
checked status. Passive-open callers consume handshake state, packet status,
bytes-written, and sequence-number metadata from this result, so uninitialized
transition state must not count as a completed listener handshake step.

## Files changed

- `src/OS.Kernel/Net/TcpPassiveOpenHandshake.cs`
- `tests/OS.HostTests/TcpPassiveOpenHandshakeTests.cs`
- `docs/networking.md`
- `docs/completion-gates.md`
- `done/step459.md`

## Tests run

- `dotnet build .\tests\OS.HostTests\OS.HostTests.csproj --nologo`
- `dotnet run --project .\tests\OS.HostTests\OS.HostTests.csproj --no-build | Select-String -Pattern 'net\.tcp passive handshake default result is not success|TEST_PASS host\.tests'`
- `dotnet build .\OS.slnx --nologo -m:1`
- `.\scripts\check-naming.ps1`
- `git diff --check`

## Evidence

- Focused host-test build passed with 0 warnings and 0 errors.
- TCP passive-open handshake default-result marker evidence:

```text
TEST_START net.tcp passive handshake default result is not success
TEST_PASS net.tcp passive handshake default result is not success
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

- This is host-tested TCP passive-open handshake state-machine evidence only.
  It does not implement retransmission, timers, sockets, listener backlog
  ownership, live NIC I/O, live HTTP, hosted .NET 10, ASP.NET Minimal API,
  Kestrel, cloud readiness, or production readiness.
