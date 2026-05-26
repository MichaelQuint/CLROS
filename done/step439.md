# Step 439 - ARP cache updater result default guard

## Summary

Implemented Gitea issue #424 by making the ARP neighbor-cache updater result
reject default/zero values as successful cache-update evidence.

## Why

`ArpNeighborCacheUpdateStatus.Success` is zero.
`default(ArpNeighborCacheUpdateResult)` previously reported success because
`Succeeded` only checked the status field. Receive-dispatch and neighbor-route
paths must not treat zero/default updater metadata as initialized ARP
validation or cache-mutation evidence.

## Files changed

- `src/OS.Kernel/Net/ArpNeighborCacheUpdater.cs`
- `tests/OS.HostTests/ArpNeighborCacheUpdaterTests.cs`
- `docs/networking.md`
- `docs/completion-gates.md`
- `done/step439.md`

## Tests run

- `dotnet build .\tests\OS.HostTests\OS.HostTests.csproj --nologo`
- `dotnet run --project .\tests\OS.HostTests\OS.HostTests.csproj --no-build | Select-String -Pattern 'net\.arp\.update default result is not success|TEST_PASS host\.tests'`
- `dotnet build .\OS.slnx --nologo -m:1`
- `.\scripts\check-naming.ps1`

## Evidence

- Focused host-test build passed with 0 warnings and 0 errors.
- ARP cache-updater default-result marker evidence:

```text
TEST_START net.arp.update default result is not success
TEST_PASS net.arp.update default result is not success
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

Other networking result structs still need separate default-result hardening
slices. This step is host-tested ARP cache-updater contract evidence only; it
does not implement live NIC I/O, hosted .NET, Kestrel, cloud readiness, or
production readiness.
