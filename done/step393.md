# Step 393 - ARP receive-dispatch cache updater

## Summary
Added a host-tested ARP receive-dispatch cache updater that consumes successful network receive dispatch metadata and mutates caller-owned ARP cache storage only after ARP reply validation succeeds.

## Why
The networking stack could parse ARP receive metadata and could update an ARP cache from a raw reply frame, but it did not have a small bridge from the receive-dispatch path into the bounded caller-owned cache table.

## Files changed
- `src/OS.Kernel/Net/ArpReceiveDispatchCacheUpdater.cs`
- `tests/OS.HostTests/ArpReceiveDispatchCacheUpdaterTests.cs`
- `docs/networking.md`
- `docs/completion-gates.md`
- `done/step393.md`

## Tests run
- `dotnet build .\tests\OS.HostTests\OS.HostTests.csproj --nologo`
- `dotnet run --project .\tests\OS.HostTests\OS.HostTests.csproj --no-build`
- `dotnet build .\OS.slnx --nologo -m:1`
- `git diff --check`

## Evidence
- Focused host build passed with 0 warnings and 0 errors.
- Focused marker scan emitted:
  - `TEST_PASS net.arp.rxdispatch updates cache from dispatched reply`
  - `TEST_PASS net.arp.rxdispatch refreshes existing cache entry`
  - `TEST_PASS net.arp.rxdispatch rejects non-arp dispatch`
  - `TEST_PASS net.arp.rxdispatch rejects wrong local target`
  - `TEST_PASS net.arp.rxdispatch preserves cache on validation failure`
  - `TEST_PASS net.arp.rxdispatch rejects truncated payload range`
  - `TEST_PASS net.arp.rxdispatch preserves no-nic-io boundary`
  - `TEST_PASS host.tests`
- Full solution build passed with 0 warnings and 0 errors.
- `git diff --check` exited 0 with only LF-to-CRLF working-copy normalization warnings.

## Failures
None.

## Deferred work
- Connect passive TCP handshake success into `TcpListenerBacklog`.
- Add IPv4 UDP/TCP pseudo-header checksum helpers before stricter transport validation.
- Keep live ARP resolution blocked until virtio-net RX polling, interrupts, timers, and queue ownership have evidence.

## Evidence boundary
This is host-tested receive-dispatch metadata and caller-owned ARP cache mutation evidence only. It does not read from a NIC, poll virtio queues, process interrupts, allocate cache storage, own timers, retransmit ARP, process gratuitous ARP, route IPv4 packets, dispatch sockets, start hosted .NET 10, start ASP.NET Minimal API/Kestrel, prove cloud readiness, or prove production readiness.
