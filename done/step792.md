# Step 792 - UDP and ICMP shared buffer payload copies

## Summary

Migrated UDP datagram and ICMP echo packet payload writes onto
`OS.Std.Memory.BufferOperations.MemoryCopy` and added a source guard marker for
that contract.

## Why

UDP and ICMP packet staging are caller-owned buffer operations. Routing payload
copies through the shared no-runtime memory helper keeps packet primitives
aligned with the CloudReady shared buffer migration while preserving existing
length checks, packet bytes, and checksum behavior.

## Files changed

- `src/OS.Kernel/Net/UdpHeader.cs`
  - uses `BufferOperations.MemoryCopy` for the UDP payload copy after the
    existing datagram length check.
- `src/OS.Kernel/Net/IcmpEchoPacket.cs`
  - uses `BufferOperations.MemoryCopy` for the ICMP echo payload copy after the
    existing packet length check.
- `tests/OS.HostTests/NetworkPacketTests.cs`
  - adds `TEST_PASS net.packet udp icmp payloads use shared buffer operations`.
- `docs/networking.md`
  - records the #750 host-tested packet primitive evidence boundary.
- `done/step792.md`
  - records this milestone and evidence.

## Tests run

- `dotnet build .\tests\OS.HostTests\OS.HostTests.csproj --nologo`
- `dotnet run --project .\tests\OS.HostTests\OS.HostTests.csproj --no-build`
- scoped `git diff --check` for the changed #750 files.
- scoped trailing-whitespace scan for the changed #750 files.

## Evidence

- `artifacts/logs/issue750-host-build.log`
  - `Build succeeded.`
  - `0 Warning(s)`
  - `0 Error(s)`
- `artifacts/logs/issue750-host-tests.log`
  - `TEST_PASS net.icmp echo request round trip`
  - `TEST_PASS net.icmp echo reply round trip`
  - `TEST_PASS network packets round-trip ethernet ipv4 udp bytes`
  - `TEST_PASS network packets round-trip ethernet ipv4 udp checksum`
  - `TEST_PASS net.packet udp icmp payloads use shared buffer operations`
  - `TEST_PASS host.tests`
- `artifacts/logs/issue750-git-diff-check-scoped.log`
  - scoped `git diff --check` exited 0 with only LF-to-CRLF warnings from
    this Windows checkout.
  - `GIT_DIFF_CHECK_SCOPED_PASS`
- `artifacts/logs/issue750-trailing-whitespace.log`
  - `TRAILING_WHITESPACE_NONE`

## Failures

- No #750 implementation or verification failures were observed.

## Deferred work

- Live virtio-net packet I/O, sockets, hosted .NET, ASP.NET Minimal API, and
  Kestrel remain deferred.

## Evidence boundary

This step proves only host-tested UDP and ICMP packet primitive payload copy
behavior. It is not QEMU, live NIC, socket, hosted .NET, ASP.NET Minimal API,
or Kestrel evidence.

