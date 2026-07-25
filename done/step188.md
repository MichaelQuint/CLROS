# Step 188 - UDP Receive Dispatcher

## What Changed

- Added `UdpReceiveDispatcher` as a narrow receive-path bridge over `NetworkReceiveDispatcher`.
- Added host tests for IPv4/UDP metadata parsing, classifier failure propagation, non-IPv4 rejection, non-UDP protocol rejection, truncated UDP header rejection, and truncated UDP payload rejection.
- Updated `docs/networking.md` with the current UDP receive dispatch status, unsupported live-NIC boundary, and test coverage.

## Why

Gitea issue #207 needed a caller-buffer UDP receive dispatcher that can consume future virtio-net RX buffers after Ethernet and IPv4 classification without copying packet payloads or introducing sockets, DHCP state integration, TCP, hosted .NET, or Kestrel claims.

## Files Changed

- `src/OS.Kernel/Net/UdpReceiveDispatcher.cs`
- `tests/OS.HostTests/UdpReceiveDispatcherTests.cs`
- `docs/networking.md`
- `done/step188.md`

## Evidence

- `dotnet build .\OS.slnx --nologo -m:1` passed with 0 warnings and 0 errors.
- `dotnet run --project .\tests\OS.HostTests\OS.HostTests.csproj --no-build` emitted `TEST_PASS host.tests`.
- Observed issue #207 markers:
  - `TEST_PASS net.udp.rx parses udp payload`
  - `TEST_PASS net.udp.rx reports classifier failure`
  - `TEST_PASS net.udp.rx rejects non-ipv4 payload`
  - `TEST_PASS net.udp.rx rejects non-udp protocol`
  - `TEST_PASS net.udp.rx reports truncated udp header`
  - `TEST_PASS net.udp.rx reports truncated udp payload`
- `scripts/check-naming.ps1` emitted `TEST_PASS naming.guard` and `TEST_PASS source.firewall`.
- `git diff --check` exited 0 with CRLF normalization warnings only.

## Deferred Work

- No live virtio-net RX queue ownership.
- No payload copy into owned protocol buffers.
- No UDP sockets or socket/PAL exports.
- No DHCP or DNS state-machine integration.
- No TCP, hosted .NET 10, ASP.NET Minimal API, or Kestrel behavior is proven by this step.

## Evidence Boundary

This is host-tested caller-owned RX-buffer parsing evidence only. It proves that an already-received no-offload virtio-net buffer containing Ethernet + IPv4 + UDP can expose address, port, and UDP payload offset/length metadata with deterministic failure statuses. It does not prove live network packet receive, DHCP, DNS, sockets, or Kestrel.
