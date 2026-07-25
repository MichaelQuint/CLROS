# Step 468 - IPv4 neighbor route default guard

## Summary

Implemented Gitea issue #453 by making `Ipv4NeighborRoutePlanResult`
reject default/zero values as successful IPv4 neighbor route-plan evidence.

## Why

`Ipv4NeighborRoutePlanStatus.Success` is zero. The route-plan result
previously reported success for `default(Ipv4NeighborRoutePlanResult)` because
`Succeeded` only checked status. Route consumers use the action, target MAC,
ARP request byte count, cache status, ARP status, packet status, and cache
count/capacity metadata, so uninitialized all-zero route state must not count
as a cached neighbor or a written ARP request.

## Files changed

- `src/OS.Kernel/Net/Ipv4NeighborRoutePlanner.cs`
- `tests/OS.HostTests/Ipv4NeighborRoutePlannerTests.cs`
- `docs/networking.md`
- `docs/completion-gates.md`
- `done/step468.md`

## Tests run

- `dotnet build .\tests\OS.HostTests\OS.HostTests.csproj --nologo`
- `dotnet run --project .\tests\OS.HostTests\OS.HostTests.csproj --no-build | Select-String -Pattern 'net\.ipv4\.neighbor default result is not success|TEST_PASS host\.tests'`
- `dotnet build .\OS.slnx --nologo -m:1`
- `.\scripts\check-naming.ps1`
- `git diff --check`

## Evidence

- Focused host-test build passed with 0 warnings and 0 errors.
- IPv4 neighbor route default-result marker evidence:

```text
TEST_START net.ipv4.neighbor default result is not success
TEST_PASS net.ipv4.neighbor default result is not success
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

- This is host-tested IPv4 neighbor route-planning evidence only. It does not
  implement live ARP retries, NIC-backed packet I/O, sockets, hosted .NET 10,
  ASP.NET Minimal API, Kestrel, cloud readiness, or production readiness.
