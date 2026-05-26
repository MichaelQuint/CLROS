# Step 207 - IPv4 and ICMP shared checksum migration

## What changed

- Migrated `Ipv4Header.ComputeHeaderChecksum` and `Ipv4Header.IsHeaderChecksumValid` to `OS.Std.Net.InternetChecksum`.
- Migrated `IcmpEchoPacket.ComputeChecksum` and `IcmpEchoPacket.IsChecksumValid` to `OS.Std.Net.InternetChecksum`.
- Updated networking documentation to state that IPv4 and ICMP use the shared no-runtime checksum helper while UDP/TCP pseudo-header checksum builders remain deferred.

## Why

Issue #226 required removing duplicated local internet-checksum loops from IPv4 and ICMP packet helpers after the shared no-runtime helper landed in issue #225. This keeps protocol checksum behavior in one reusable implementation before TCP/UDP pseudo-header work expands.

## Files changed

- `src/OS.Kernel/Net/Ipv4Header.cs`
- `src/OS.Kernel/Net/IcmpEchoPacket.cs`
- `docs/networking.md`
- `done/step207.md`

## Tests run

- `dotnet build .\OS.slnx --nologo -m:1`
- `dotnet run --project .\tests\OS.HostTests\OS.HostTests.csproj --no-build *> .\artifacts\logs\issue226-hosttests.log`
- `.\scripts\check-naming.ps1`

## Logs/evidence

- Build passed with `0 Warning(s), 0 Error(s)`.
- Host tests passed with `HOST_TEST_EXIT 0` and `TEST_PASS host.tests`.
- Relevant preserved markers:
  - `TEST_PASS network packets round-trip ethernet ipv4 udp bytes`
  - `TEST_PASS network packets reject bad ipv4 checksum`
  - `TEST_PASS net.icmp echo request round trip`
  - `TEST_PASS net.icmp echo reply round trip`
  - `TEST_PASS net.icmp rejects bad checksum`
- Naming/source guard passed:
  - `TEST_PASS naming.guard`
  - `TEST_PASS source.firewall`

## Failures

- None observed for this slice.

## Deferred work

- UDP checksum generation and validation remain zero/omitted for IPv4.
- TCP and UDP pseudo-header checksum builders remain unimplemented.
- No live virtio-net packet I/O, hosted .NET 10, ASP.NET Minimal API, or Kestrel behavior is claimed.

## Evidence boundary

This step proves code-level migration onto a shared checksum primitive once host tests pass. It does not prove live NIC checksum validation, sockets, HTTP, hosted runtime behavior, Minimal API, or Kestrel support.
