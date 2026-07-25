# Step 407 - UDP receive checksum validation

## Summary

Implemented host-tested non-zero IPv4 UDP checksum validation in the UDP
receive dispatcher while preserving IPv4 checksum-zero acceptance.

## Why

UDP receive dispatch now rejects corrupted caller-owned IPv4 UDP datagrams
before exposing payload metadata to higher DHCP/DNS/socket-adjacent code. The
slice keeps checksum enforcement incremental and does not claim live packet I/O
or socket behavior.

## Files changed

- `src/OS.Kernel/Net/UdpReceiveDispatcher.cs`
- `tests/OS.HostTests/UdpReceiveDispatcherTests.cs`
- `docs/networking.md`
- `docs/completion-gates.md`
- `done/step407.md`

## Tests run

- `dotnet build .\tests\OS.HostTests\OS.HostTests.csproj --nologo`
- `dotnet run --project .\tests\OS.HostTests\OS.HostTests.csproj --no-build`
- `dotnet build .\OS.slnx --nologo -m:1`
- `git diff --check`

## Evidence

- Focused host build passed with 0 warnings and 0 errors.
- Host test harness emitted:
  - `TEST_PASS net.udp.rx accepts zero checksum`
  - `TEST_PASS net.udp.rx validates nonzero checksum`
  - `TEST_PASS net.udp.rx rejects bad checksum`
  - `TEST_PASS host.tests`
- Full solution build passed with 0 warnings and 0 errors.
- `git diff --check` passed with only repository line-ending warnings.

## Failures

- None.

## Deferred work

- UDP transmit still emits checksum zero.
- Outbound TCP frame checksum generation remains a separate slice.
- Established TCP payload checksum validation remains a separate slice.
- Live virtio-net packet I/O, UDP sockets, DHCP/DNS over a live NIC, hosted
  .NET, Kestrel, cloud readiness, and production readiness remain unclaimed.

## Evidence boundary

This is host-only caller-owned UDP receive-buffer validation. It proves IPv4
UDP checksum zero remains accepted, valid non-zero checksums are accepted, and
bad non-zero checksums return `BadUdpChecksum`/`PacketParseStatus.BadChecksum`
without exposing payload metadata; it does not prove outbound UDP checksums,
UDP sockets, live NIC traffic, hosted .NET, Kestrel, cloud readiness, or
production readiness.

