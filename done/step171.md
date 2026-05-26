# Step 171 - ARP Neighbor Cache

## What Changed

- Added `ArpNeighborCache` over caller-provided bounded entry storage.
- Added host tests for insert/lookup, update, remove/clear, capacity exhaustion, stale purge, and invalid input.
- Updated networking and completion-gate docs.

## Why

Gitea issue #189 needed a deterministic metadata table for resolved IPv4 peers after the one-target ARP transaction. The cache is intentionally separate from retransmission, timers, NIC packet I/O, and protocol dispatch.

## Files Changed

- `src/OS.Kernel/Net/ArpNeighborCache.cs`
- `tests/OS.HostTests/ArpNeighborCacheTests.cs`
- `docs/networking.md`
- `docs/completion-gates.md`

## Evidence

- `dotnet build .\src\OS.Kernel\OS.Kernel.csproj --nologo -m:1` passed.
- `dotnet build .\tests\OS.HostTests\OS.HostTests.csproj --nologo -m:1` passed.
- `dotnet build .\OS.slnx --nologo -m:1` passed.
- `dotnet run --project .\tests\OS.HostTests\OS.HostTests.csproj --no-build` emitted `TEST_PASS host.tests`.
- Observed markers:
  - `TEST_PASS net.arp.cache insert lookup`
  - `TEST_PASS net.arp.cache update existing`
  - `TEST_PASS net.arp.cache remove clear`
  - `TEST_PASS net.arp.cache capacity exhaustion`
  - `TEST_PASS net.arp.cache purge stale`
  - `TEST_PASS net.arp.cache rejects invalid inputs`
- `scripts/check-naming.ps1` emitted `TEST_PASS naming.guard` and `TEST_PASS source.firewall`.
- `git diff --check` exited 0 with CRLF normalization warnings only.

## Evidence Boundary

This is host-tested ARP cache table evidence only. It does not prove timers, retransmission, packet send/receive, virtio-net I/O, live ARP resolution, hosted .NET, or Kestrel networking.
