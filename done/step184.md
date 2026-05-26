# Step 184 - ARP Reply Cache Updater

## What Changed

- Added `ArpNeighborCacheUpdater` as the narrow ARP reply receive-path bridge between `ArpNeighborResolutionTransaction.ReceiveReply` and `ArpNeighborCache`.
- Added host tests for validated insert, existing-entry refresh, wrong sender address rejection, wrong target MAC rejection, cache capacity failure, and unchanged cache state after invalid replies.
- Updated `docs/networking.md` with the current ARP reply cache-update status, unsupported live-NIC boundary, and test coverage.

## Why

Gitea issue #203 needed cache mutation after a received ARP reply without introducing NIC RX ownership, packet sending, waits, retries, or timers. The updater centralizes reply validation in the existing ARP transaction state machine and performs the cache mutation only after validation succeeds.

## Files Changed

- `src/OS.Kernel/Net/ArpNeighborCacheUpdater.cs`
- `tests/OS.HostTests/ArpNeighborCacheUpdaterTests.cs`
- `docs/networking.md`
- `done/step184.md`

## Evidence

- `dotnet build .\OS.slnx --nologo -m:1` passed with 0 warnings and 0 errors.
- `dotnet run --project .\tests\OS.HostTests\OS.HostTests.csproj --no-build` emitted `TEST_PASS host.tests`.
- Observed issue #203 markers:
  - `TEST_PASS net.arp.update inserts validated reply`
  - `TEST_PASS net.arp.update refreshes existing entry`
  - `TEST_PASS net.arp.update rejects wrong sender address`
  - `TEST_PASS net.arp.update rejects wrong target mac`
  - `TEST_PASS net.arp.update reports cache capacity failure`
  - `TEST_PASS net.arp.update leaves cache unchanged on invalid reply`
- `scripts/check-naming.ps1` emitted `TEST_PASS naming.guard` and `TEST_PASS source.firewall`.
- `git diff --check` exited 0 with CRLF normalization warnings only.

## Deferred Work

- No NIC RX queue owns or dispatches incoming ARP frames.
- No packets are sent by the updater.
- No blocking wait, retry, timeout, cache aging timer, DHCP, TCP, HTTP, hosted .NET 10, Minimal API, or Kestrel behavior is proven by this step.

## Evidence Boundary

This is host-tested caller-buffer ARP reply validation and cache-mutation evidence only. It proves that a valid reply can update the bounded cache and invalid replies leave the cache unchanged, not live virtio-net packet receive or end-to-end IPv4 transmission.
