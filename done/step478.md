# Step 478 - DNS UDP receive default guard

## Summary

Implemented Gitea issue #463 by making `DnsUdpReceiveFrameResult` reject
default/zero values as successful DNS UDP response evidence.

## Why

`DnsUdpFrameStatus.Success` is zero. The DNS UDP receive result previously
reported success for `default(DnsUdpReceiveFrameResult)` because `Succeeded`
only checked status. DNS UDP response handling depends on initialized nested
UDP dispatch metadata, DNS resolver transition metadata, payload offsets, and
port identity, so uninitialized all-zero state must not count as a parsed DNS
response.

## Files changed

- `src/OS.Kernel/Net/DnsUdpFrame.cs`
- `tests/OS.HostTests/DnsUdpFrameTests.cs`
- `docs/networking.md`
- `docs/completion-gates.md`
- `done/step478.md`

## Tests run

- `dotnet build .\tests\OS.HostTests\OS.HostTests.csproj --nologo`
- `dotnet run --project .\tests\OS.HostTests\OS.HostTests.csproj --no-build`
  filtered for `net.dns.udp receive default result is not success` and
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

TEST_START net.dns.udp receive default result is not success
TEST_PASS net.dns.udp receive default result is not success
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

- This is host-tested DNS UDP receive-frame evidence only. It does not
  implement live NIC RX, DNS retries, DNS caching, socket delivery, hosted .NET
  10, ASP.NET Minimal API, Kestrel, cloud readiness, or production readiness.
