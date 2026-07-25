# Step 395 - IPv4 transport pseudo-header checksum

## Summary
Added a host-tested IPv4 UDP/TCP pseudo-header checksum helper over caller-owned transport buffers.

## Why
The shared `InternetChecksum` primitive supported segmented accumulation, but UDP/TCP pseudo-header checksum computation and validation were still a documented networking gap.

## Files changed
- `src/OS.Kernel/Net/Ipv4TransportChecksum.cs`
- `tests/OS.HostTests/Ipv4TransportChecksumTests.cs`
- `docs/networking.md`
- `docs/completion-gates.md`
- `done/step395.md`

## Tests run
- `dotnet build .\tests\OS.HostTests\OS.HostTests.csproj --nologo`
- `dotnet run --project .\tests\OS.HostTests\OS.HostTests.csproj --no-build`
- `dotnet build .\OS.slnx --nologo -m:1`
- `git diff --check`

## Evidence
- Focused host build passed with 0 warnings and 0 errors.
- Focused marker scan emitted:
  - `TEST_PASS net.transport.checksum computes udp ipv4 pseudo header`
  - `TEST_PASS net.transport.checksum computes tcp ipv4 pseudo header`
  - `TEST_PASS net.transport.checksum validates segmented payload`
  - `TEST_PASS net.transport.checksum rejects corrupted checksum`
  - `TEST_PASS net.transport.checksum rejects invalid inputs`
  - `TEST_PASS net.transport.checksum preserves primitive boundary`
  - `TEST_PASS host.tests`
- Full solution build passed with 0 warnings and 0 errors.
- `git diff --check` exited 0 with only LF-to-CRLF working-copy normalization warnings.

## Failures
None.

## Deferred work
- Integrate `Ipv4TransportChecksum` into UDP/TCP frame writers and receive validation when those paths are ready to enforce checksums.
- Preserve explicit virtio-net offload negotiation before relying on hardware checksum behavior.

## Evidence boundary
This is host-tested checksum primitive evidence only. It computes and validates pseudo-header checksums over caller-owned buffers, but it does not enforce checksums in packet receive/transmit paths, validate live NIC packets, negotiate checksum offload, create sockets, transfer data, start hosted .NET 10, start ASP.NET Minimal API/Kestrel, prove cloud readiness, or prove production readiness.
