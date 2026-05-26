# Step 480 - DHCP client planner default guard

## Summary

Implemented Gitea issue #465 by making
`DhcpClientTransactionPlannerResult` reject default/zero values as successful
DHCP client transaction planner evidence.

## Why

`DhcpClientTransactionPlannerStatus.Success` is zero. The DHCP client planner
result previously reported success for `default(DhcpClientTransactionPlannerResult)`
because `Succeeded` only checked status. Planner evidence depends on initialized
frame/client/message/packet/UDP/lease statuses plus state, transaction identity,
lease metadata, and payload offsets, so uninitialized all-zero state must not
count as a planned DHCP transaction.

## Files changed

- `src/OS.Kernel/Net/DhcpClientTransactionPlanner.cs`
- `tests/OS.HostTests/DhcpClientTransactionPlannerTests.cs`
- `docs/networking.md`
- `docs/completion-gates.md`
- `done/step480.md`

## Tests run

- `dotnet build .\tests\OS.HostTests\OS.HostTests.csproj --nologo`
- `dotnet run --project .\tests\OS.HostTests\OS.HostTests.csproj --no-build`
  filtered for `net.dhcp.client.transaction default result is not success` and
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

TEST_START net.dhcp.client.transaction default result is not success
TEST_PASS net.dhcp.client.transaction default result is not success
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

- This is host-tested DHCP client transaction planner evidence only. It does
  not implement live NIC I/O, DHCP retransmission timers, interface address
  assignment, lease renewal, sockets, hosted .NET 10, ASP.NET Minimal API,
  Kestrel, cloud readiness, or production readiness.
