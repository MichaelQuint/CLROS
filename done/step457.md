# Step 457 - TCP active-connect default guard

## Summary

Implemented Gitea issue #442 by making
`TcpActiveConnectTransitionResult` reject default/zero values as successful
active-connect transition evidence.

## Why

`TcpActiveConnectStatus.Success` is zero. The transition result previously
reported success for `default(TcpActiveConnectTransitionResult)` because
`Succeeded` only checked status. TCP callers consume active-connect state,
packet status, bytes-written, and sequence-number metadata from this result, so
uninitialized transition state must not count as a completed active-open step.

## Files changed

- `src/OS.Kernel/Net/TcpActiveConnectTransaction.cs`
- `tests/OS.HostTests/TcpActiveConnectTransactionTests.cs`
- `docs/networking.md`
- `docs/completion-gates.md`
- `done/step457.md`

## Tests run

- `dotnet build .\tests\OS.HostTests\OS.HostTests.csproj --nologo`
- `dotnet run --project .\tests\OS.HostTests\OS.HostTests.csproj --no-build | Select-String -Pattern 'net\.tcp active connect default result is not success|TEST_PASS host\.tests'`
- `dotnet build .\OS.slnx --nologo -m:1`
- `.\scripts\check-naming.ps1`
- `git diff --check`

## Evidence

- Focused host-test build passed with 0 warnings and 0 errors.
- TCP active-connect default-result marker evidence:

```text
TEST_START net.tcp active connect default result is not success
TEST_PASS net.tcp active connect default result is not success
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

- This is host-tested TCP active-connect state-machine evidence only. It does
  not implement retransmission, timers, sockets, live NIC I/O, live HTTP,
  hosted .NET 10, ASP.NET Minimal API, Kestrel, cloud readiness, or production
  readiness.
