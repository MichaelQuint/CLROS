# Step 482 - HTTP TCP GET planner default guard

## Summary

Implemented Gitea issue #467 by making `HttpTcpClientGetPlanResult` reject
default/zero values as successful HTTP-over-TCP GET planner evidence.

## Why

`HttpTcpClientGetPlanStatus.Success` is zero. The HTTP TCP GET planner result
previously reported success for `default(HttpTcpClientGetPlanResult)` because
`Succeeded` only checked status. Planner evidence depends on initialized TCP
frame, active-connect, established-stream, HTTP request/response, packet,
sequence, segment, request, and response metadata, so uninitialized all-zero
state must not count as a successful HTTP GET plan.

## Files changed

- `src/OS.Kernel/Net/HttpTcpClientGetPlanner.cs`
- `tests/OS.HostTests/HttpTcpClientGetPlannerTests.cs`
- `docs/networking.md`
- `docs/completion-gates.md`
- `done/step482.md`

## Tests run

- `dotnet build .\tests\OS.HostTests\OS.HostTests.csproj --nologo`
- `dotnet run --project .\tests\OS.HostTests\OS.HostTests.csproj --no-build`
  filtered for `net.http.tcp.get default result is not success` and
  `TEST_PASS host.tests`
- `dotnet build .\OS.slnx --nologo -m:1`
- `.\scripts\check-naming.ps1`
- `git diff --check`

## Evidence

```text
dotnet build .\tests\OS.HostTests\OS.HostTests.csproj --nologo
Build succeeded.
0 Warning(s)
0 Error(s)

TEST_START net.http.tcp.get default result is not success
TEST_PASS net.http.tcp.get default result is not success
TEST_PASS host.tests

dotnet build .\OS.slnx --nologo -m:1
Build succeeded.
0 Warning(s)
0 Error(s)

TEST_START naming.guard
TEST_PASS naming.guard
TEST_START source.firewall
TEST_PASS source.firewall

git diff --check
exit 0; existing LF-to-CRLF working-copy warnings only
```

## Failures

- None.

## Deferred work

- This is host-tested HTTP TCP GET planner evidence only. It does not implement
  sockets, live NIC I/O, retransmission, timers, TLS, redirects, FetchApp,
  hosted .NET 10, ASP.NET Minimal API, Kestrel, cloud readiness, or production
  readiness.
