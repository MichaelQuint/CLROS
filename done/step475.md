# Step 475 - UDP receive dispatch default guard

## Summary

Implemented Gitea issue #460 by making `UdpReceiveDispatchResult` reject
default/zero values as successful UDP receive-dispatch evidence.

## Why

`UdpReceiveDispatchStatus.Success` is zero. The UDP receive result previously
reported success for `default(UdpReceiveDispatchResult)` because `Succeeded`
only checked status. UDP receive consumers use nested network dispatch status,
IPv4/UDP header metadata, UDP packet status, source/destination ports, and UDP
payload ranges, so uninitialized all-zero state must not count as a parsed UDP
datagram.

## Files changed

- `src/OS.Kernel/Net/UdpReceiveDispatcher.cs`
- `tests/OS.HostTests/UdpReceiveDispatcherTests.cs`
- `docs/networking.md`
- `docs/completion-gates.md`
- `done/step475.md`

## Tests run

- `dotnet build .\tests\OS.HostTests\OS.HostTests.csproj --nologo`
- `dotnet run --project .\tests\OS.HostTests\OS.HostTests.csproj --no-build | Select-String -Pattern 'net\.udp\.rx default result is not success|TEST_PASS host\.tests'`
- `dotnet build .\OS.slnx --nologo -m:1`
- `.\scripts\check-naming.ps1`
- `git diff --check`

## Evidence

- Focused host-test build passed with 0 warnings and 0 errors.
- UDP receive dispatcher default-result marker evidence:

```text
TEST_START net.udp.rx default result is not success
TEST_PASS net.udp.rx default result is not success
TEST_PASS host.tests
```

- Full solution build passed with 0 warnings and 0 errors.
- Naming and source firewall guard emitted:

```text
TEST_START naming.guard
TEST_PASS naming.guard
TEST_START source.firewall
TEST_PASS source.firewall
```

- `git diff --check` exited 0. Git reported existing LF-to-CRLF working-copy
  warnings only.

## Failures

- None.

## Deferred work

- This is host-tested UDP receive-dispatch evidence only. It does not implement
  live NIC receive, virtio-net queue polling, UDP sockets, DHCP/DNS live packet
  I/O, hosted .NET 10, ASP.NET Minimal API, Kestrel, cloud readiness, or
  production readiness.
