# Step 176 - Virtio-net RX Frame Classifier

## What Changed

- Added `VirtioNetReceiveFrameClassifier`, a caller-buffer classifier for virtio-net RX buffers that contain the baseline 10-byte no-mergeable-buffers virtio-net header followed by one Ethernet II frame.
- Added host tests for ARP, IPv4, truncated virtio header, unsupported offload, missing Ethernet header, and unsupported EtherType classification.
- Updated `docs/networking.md` with the narrow virtio-net RX status.

## Why

Gitea issue #195 needed the next host-tested boundary after virtio-net header parsing: classify a used RX buffer into ARP, IPv4, or unsupported Ethernet payload metadata without parsing ARP/IPv4 deeply and without adding queue ownership, interrupts, allocation, DHCP/TCP, or Kestrel claims.

## Files Changed

- `src/OS.Kernel/Drivers/VirtioNetReceiveFrameClassifier.cs`
- `tests/OS.HostTests/VirtioNetReceiveFrameClassifierTests.cs`
- `docs/networking.md`
- `done/step176.md`

## Evidence

- `dotnet build .\src\OS.Kernel\OS.Kernel.csproj --nologo -m:1` passed with 0 warnings and 0 errors.
- `.\scripts\check-naming.ps1` emitted `TEST_PASS naming.guard` and `TEST_PASS source.firewall`.
- `git diff --check` exited 0 with CRLF normalization warnings only.

Expected markers once the unrelated host-test compile blocker is cleared:

- `TEST_PASS drivers.virtionet.rx classifies arp frame`
- `TEST_PASS drivers.virtionet.rx classifies ipv4 frame`
- `TEST_PASS drivers.virtionet.rx rejects truncated virtio header`
- `TEST_PASS drivers.virtionet.rx rejects unsupported offload`
- `TEST_PASS drivers.virtionet.rx rejects missing ethernet header`
- `TEST_PASS drivers.virtionet.rx reports unsupported payload`

## Failures

- `dotnet build .\OS.slnx --nologo -m:1` is currently blocked outside this issue by `tests/OS.HostTests/Ipv4NeighborRoutePlannerTests.cs` ref-safety errors (`CS8350`, `CS8352`) in another worker's area.
- `dotnet run --project .\tests\OS.HostTests\OS.HostTests.csproj --no-build` was not used as evidence because the host-test assembly could not be rebuilt; running with `--no-build` would use a stale binary that cannot include this issue's new tests.

## Evidence Boundary

This is host-tested RX buffer classification only. It does not allocate buffers, publish virtqueue descriptors, read a used ring, process interrupts, parse ARP/IPv4 bodies, run DHCP, run TCP, expose sockets, host .NET 10, or prove ASP.NET Minimal API/Kestrel support.
