# Step 177 - IPv4 Neighbor Route Planner

## What Changed

- Added `Ipv4NeighborRoutePlanner` as a caller-buffer send-path decision point for unresolved IPv4 peers.
- Added host tests for cached MAC lookup, ARP request writing on cache miss, cache preservation, undersized request-buffer rejection, cache count/capacity preservation, and invalid target rejection.
- Updated `docs/networking.md` to describe the new ARP/IPv4 planner status and evidence boundary.

## Why

Gitea issue #196 needed the next narrow IPv4 send-path primitive without introducing NIC TX queues, blocking waits, DHCP, TCP, or Kestrel claims. The planner bridges the existing bounded ARP cache and one-target ARP request writer so future IPv4 packet send code can either use a cached destination MAC or ask the caller to transmit a deterministic ARP request frame.

## Files Changed

- `src/OS.Kernel/Net/Ipv4NeighborRoutePlanner.cs`
- `tests/OS.HostTests/Ipv4NeighborRoutePlannerTests.cs`
- `docs/networking.md`
- `done/step177.md`

## Evidence

- `dotnet build .\OS.slnx --nologo -m:1` passed with 0 warnings and 0 errors.
- `dotnet run --project .\tests\OS.HostTests\OS.HostTests.csproj --no-build` emitted `TEST_PASS host.tests`.
- Observed issue #196 markers:
  - `TEST_PASS net.ipv4.neighbor returns cached mac`
  - `TEST_PASS net.ipv4.neighbor writes arp request on cache miss`
  - `TEST_PASS net.ipv4.neighbor preserves cache on request write`
  - `TEST_PASS net.ipv4.neighbor rejects undersized request buffer`
  - `TEST_PASS net.ipv4.neighbor handles cache capacity unchanged`
  - `TEST_PASS net.ipv4.neighbor rejects invalid target input`
- `scripts/check-naming.ps1` emitted `TEST_PASS naming.guard` and `TEST_PASS source.firewall`.
- `git diff --check` exited 0 with CRLF normalization warnings only.

## Deferred Work

- No NIC TX queue is wired to the ARP request frame.
- The planner does not block for ARP replies, retry, run timers, or update the ARP cache from replies.
- No DHCP, TCP, HTTP, hosted .NET 10, Minimal API, or Kestrel behavior is proven by this step.

## Evidence Boundary

This is host-tested caller-buffer route-planning evidence only. It proves deterministic cache lookup and ARP request frame construction, not live virtio-net packet I/O or end-to-end IPv4 transmission.
