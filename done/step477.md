# Step 477 - DHCP UDP read default guard

## Summary

Implemented Gitea issue #462 by making `DhcpUdpFrameReadResult` reject
default/zero values as successful DHCP server-reply frame-read evidence.

## Why

`DhcpUdpFrameStatus.Success` is zero. The DHCP UDP read result previously
reported success for `default(DhcpUdpFrameReadResult)` because `Succeeded` only
checked status. DHCP reply readers use nested packet/UDP/DHCP status, packet
identity, parsed message metadata, payload offset, and payload length, so
uninitialized all-zero state must not count as a parsed DHCP server reply.

## Files changed

- `src/OS.Kernel/Net/DhcpUdpFrame.cs`
- `tests/OS.HostTests/DhcpUdpFrameTests.cs`
- `docs/networking.md`
- `docs/completion-gates.md`
- `done/step477.md`

## Tests run

- `dotnet build .\tests\OS.HostTests\OS.HostTests.csproj --nologo`
- `dotnet run --project .\tests\OS.HostTests\OS.HostTests.csproj --no-build`
  filtered for `net.dhcp.udp read default result is not success` and
  `TEST_PASS host.tests`
- `dotnet build .\OS.slnx --nologo -m:1`
- `.\scripts\check-naming.ps1`
- `git diff --check`

## Evidence

```text
dotnet build .\tests\OS.HostTests\OS.HostTests.csproj --nologo
Build succeeded.
0 Warning(s)
0 Error(s)

TEST_START net.dhcp.udp read default result is not success
TEST_PASS net.dhcp.udp read default result is not success
TEST_PASS host.tests

dotnet build .\OS.slnx --nologo -m:1
Build succeeded.
0 Warning(s)
0 Error(s)

TEST_START naming.guard
TEST_PASS naming.guard
TEST_START source.firewall
TEST_PASS source.firewall

git diff --check
exit 0; existing LF-to-CRLF working-copy warnings only
```

## Failures

- None.

## Deferred work

- This is host-tested DHCP UDP frame-read evidence only. It does not implement
  live NIC RX, DHCP timers/retries, address assignment, sockets, hosted .NET
  10, ASP.NET Minimal API, Kestrel, cloud readiness, or production readiness.
