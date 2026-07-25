# Step 434 - ARP cache result default guard

## Summary

Implemented Gitea issue #419 by making ARP neighbor-cache lookup and mutation
results reject default/zero values as successful cache evidence.

## Why

`ArpNeighborCacheStatus.Success` is zero. `default(ArpNeighborCacheLookupResult)`
and `default(ArpNeighborCacheMutationResult)` previously reported success
because `Succeeded` only checked the status field. Higher networking paths must
not treat zero/default ARP cache metadata as initialized lookup or mutation
evidence.

## Files changed

- `src/OS.Kernel/Net/ArpNeighborCache.cs`
- `tests/OS.HostTests/ArpNeighborCacheTests.cs`
- `docs/networking.md`
- `docs/completion-gates.md`
- `done/step434.md`

## Tests run

- `dotnet build .\tests\OS.HostTests\OS.HostTests.csproj --nologo`
- `dotnet run --project .\tests\OS.HostTests\OS.HostTests.csproj --no-build | Select-String -Pattern 'net\.arp\.cache default results are not success|TEST_PASS host\.tests'`
- `dotnet build .\OS.slnx --nologo -m:1`
- `.\scripts\check-naming.ps1`

## Evidence

- Focused host-test build passed with 0 warnings and 0 errors.
- ARP cache default-result marker evidence:

```text
TEST_START net.arp.cache default results are not success
TEST_PASS net.arp.cache default results are not success
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

## Failures

- None.

## Deferred work

Other networking and storage result structs still need separate default-result
hardening slices. This step is host-tested ARP cache contract evidence only; it
does not implement live NIC I/O, hosted .NET, Kestrel, cloud readiness, or
production readiness.
