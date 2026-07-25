# Step 473 - ARP receive-dispatch cache update default guard

## Summary

Implemented Gitea issue #458 by making
`ArpReceiveDispatchCacheUpdateResult` reject default/zero values as successful
receive-dispatch cache-update evidence.

## Why

`ArpReceiveDispatchCacheUpdateStatus.Success` is zero. The receive-dispatch
cache-update result previously reported success for
`default(ArpReceiveDispatchCacheUpdateResult)` because `Succeeded` only checked
status. ARP receive-dispatch consumers use nested dispatch, ARP validation,
cache-update, payload-range, resolved-MAC, cache count/capacity, and
update-existing metadata, so uninitialized all-zero state must not count as an
applied caller-owned cache update.

## Files changed

- `src/OS.Kernel/Net/ArpReceiveDispatchCacheUpdater.cs`
- `tests/OS.HostTests/ArpReceiveDispatchCacheUpdaterTests.cs`
- `docs/networking.md`
- `docs/completion-gates.md`
- `done/step473.md`

## Tests run

- `dotnet build .\tests\OS.HostTests\OS.HostTests.csproj --nologo`
- `dotnet run --project .\tests\OS.HostTests\OS.HostTests.csproj --no-build | Select-String -Pattern 'net\.arp\.rxdispatch default result is not success|TEST_PASS host\.tests'`
- `dotnet build .\OS.slnx --nologo -m:1`
- `.\scripts\check-naming.ps1`
- `git diff --check`

## Evidence

- Focused host-test build passed with 0 warnings and 0 errors.
- ARP receive-dispatch cache updater default-result marker evidence:

```text
TEST_START net.arp.rxdispatch default result is not success
TEST_PASS net.arp.rxdispatch default result is not success
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

- This is host-tested ARP receive-dispatch cache-update evidence only. It does
  not implement NIC receive polling, timers, ARP retransmission, live neighbor
  resolution, sockets, hosted .NET 10, ASP.NET Minimal API, Kestrel, cloud
  readiness, or production readiness.
