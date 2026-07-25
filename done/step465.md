# Step 465 - TCP stream socket-bridge default guard

## Summary

Implemented Gitea issue #450 by making
`TcpEstablishedStreamSocketBridgeResult` reject default/zero values as
successful mock socket bridge evidence.

## Why

`TcpEstablishedStreamSocketBridgeStatus.Success` is zero. The bridge result
previously reported success for
`default(TcpEstablishedStreamSocketBridgeResult)` because `Succeeded` only
checked status. Bridge callers consume receive/socket status, socket handle,
payload byte counts, unread bytes, and sequence-number metadata from this
result, so uninitialized result state must not count as a queued inbound payload.

## Files changed

- `src/OS.Kernel/Net/TcpEstablishedStreamSocketBridge.cs`
- `tests/OS.HostTests/TcpEstablishedStreamSocketBridgeTests.cs`
- `docs/networking.md`
- `docs/completion-gates.md`
- `done/step465.md`

## Tests run

- `dotnet build .\tests\OS.HostTests\OS.HostTests.csproj --nologo`
- `dotnet run --project .\tests\OS.HostTests\OS.HostTests.csproj --no-build | Select-String -Pattern 'net\.tcp\.stream\.socketbridge default result is not success|TEST_PASS host\.tests'`
- `dotnet build .\OS.slnx --nologo -m:1`
- `.\scripts\check-naming.ps1`
- `git diff --check`

## Evidence

- Focused host-test build passed with 0 warnings and 0 errors.
- TCP stream socket-bridge default-result marker evidence:

```text
TEST_START net.tcp.stream.socketbridge default result is not success
TEST_PASS net.tcp.stream.socketbridge default result is not success
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

- This is host-tested mock socket bridge evidence only. It does not export PAL
  functions, open live sockets, wait on a scheduler, prove hosted .NET 10,
  ASP.NET Minimal API, Kestrel, cloud readiness, or production readiness.

