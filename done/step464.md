# Step 464 - TCP listener backlog default guard

## Summary

Implemented Gitea issue #449 by making `TcpListenerBacklogResult` reject
default/zero values as successful caller-owned listener backlog evidence.

## Why

`TcpListenerBacklogStatus.Success` is zero. The result previously reported
success for `default(TcpListenerBacklogResult)` because `Succeeded` only checked
status. Listener backlog callers consume count, capacity, required-capacity,
entry-index, and accepted-connection metadata from this result, so uninitialized
result state must not count as a valid enqueue, accept, or inspect result.

## Files changed

- `src/OS.Kernel/Net/TcpListenerBacklog.cs`
- `tests/OS.HostTests/TcpListenerBacklogTests.cs`
- `docs/networking.md`
- `docs/completion-gates.md`
- `done/step464.md`

## Tests run

- `dotnet build .\tests\OS.HostTests\OS.HostTests.csproj --nologo`
- `dotnet run --project .\tests\OS.HostTests\OS.HostTests.csproj --no-build | Select-String -Pattern 'net\.tcp\.listener default result is not success|TEST_PASS host\.tests'`
- `dotnet build .\OS.slnx --nologo -m:1`
- `.\scripts\check-naming.ps1`
- `git diff --check`

## Evidence

- Focused host-test build passed with 0 warnings and 0 errors.
- TCP listener backlog default-result marker evidence:

```text
TEST_START net.tcp.listener default result is not success
TEST_PASS net.tcp.listener default result is not success
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

- This is host-tested caller-owned listener backlog metadata evidence only. It
  does not implement live TCP listeners, sockets, hosted .NET 10, ASP.NET
  Minimal API, Kestrel, cloud readiness, or production readiness.

