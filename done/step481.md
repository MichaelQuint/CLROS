# Step 481 - DNS resolver planner default guard

## Summary

Implemented Gitea issue #466 by making
`DnsResolverTransactionPlanResult` reject default/zero values as successful
DNS resolver transaction planner evidence.

## Why

`DnsResolverTransactionPlanStatus.Success` is zero. The DNS resolver transaction
planner result previously reported success for
`default(DnsResolverTransactionPlanResult)` because `Succeeded` only checked
status. Planner evidence depends on initialized query-frame, UDP dispatch,
receive-frame, resolver, packet, query identity, answer, TTL, and source-server
metadata, so uninitialized all-zero state must not count as a successful DNS
transaction plan.

## Files changed

- `src/OS.Kernel/Net/DnsResolverTransactionPlanner.cs`
- `tests/OS.HostTests/DnsResolverTransactionPlannerTests.cs`
- `docs/networking.md`
- `docs/completion-gates.md`
- `done/step481.md`

## Tests run

- `dotnet build .\tests\OS.HostTests\OS.HostTests.csproj --nologo`
- `dotnet run --project .\tests\OS.HostTests\OS.HostTests.csproj --no-build`
  filtered for `net.dns.client.transaction default result is not success` and
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

TEST_START net.dns.client.transaction default result is not success
TEST_PASS net.dns.client.transaction default result is not success
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

- This is host-tested DNS resolver transaction planner evidence only. It does
  not implement live NIC I/O, sockets, DNS retransmission timers, route
  selection, DNS caching, DHCP-applied configuration, hosted .NET 10, ASP.NET
  Minimal API, Kestrel, cloud readiness, or production readiness.
