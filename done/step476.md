# Step 476 - DHCP UDP write default guard

## Summary

Implemented Gitea issue #461 by making `DhcpUdpFrameWriteResult` reject
default/zero values as successful DHCP client-frame writing evidence.

## Why

`DhcpUdpFrameStatus.Success` is zero. The DHCP UDP write result previously
reported success for `default(DhcpUdpFrameWriteResult)` because `Succeeded`
only checked status. DHCP client-frame writers use DHCP status, packet status,
payload length, and bytes-written metadata, so uninitialized all-zero state must
not count as a written DHCPDISCOVER or DHCPREQUEST Ethernet/IPv4/UDP frame.

## Files changed

- `src/OS.Kernel/Net/DhcpUdpFrame.cs`
- `tests/OS.HostTests/DhcpUdpFrameTests.cs`
- `docs/networking.md`
- `docs/completion-gates.md`
- `done/step476.md`

## Tests run

- `dotnet build .\tests\OS.HostTests\OS.HostTests.csproj --nologo`
- `dotnet run --project .\tests\OS.HostTests\OS.HostTests.csproj --no-build | Select-String -Pattern 'net\.dhcp\.udp write default result is not success|TEST_PASS host\.tests'`
- `dotnet build .\OS.slnx --nologo -m:1`
- `.\scripts\check-naming.ps1`
- `git diff --check`

## Evidence

- Focused host-test build passed with 0 warnings and 0 errors.
- DHCP UDP write default-result marker evidence:

```text
TEST_START net.dhcp.udp write default result is not success
TEST_PASS net.dhcp.udp write default result is not success
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

- This is host-tested DHCP UDP frame-writing evidence only. It does not
  implement live NIC TX, DHCP timers/retries, address assignment, sockets,
  hosted .NET 10, ASP.NET Minimal API, Kestrel, cloud readiness, or production
  readiness.
