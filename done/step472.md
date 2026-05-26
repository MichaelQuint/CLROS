# Step 472 - TCP close IPv4 frame plan default guard

## Summary

Implemented Gitea issue #457 by making
`TcpConnectionCloseIpv4FramePlanResult` reject default/zero values as
successful active-close TCP/IPv4 frame-plan evidence.

## Why

`TcpConnectionCloseIpv4FramePlanStatus.Success` is zero. The close frame-plan
result previously reported success for
`default(TcpConnectionCloseIpv4FramePlanResult)` because `Succeeded` only
checked status. Active-close frame consumers use close state/status, packet
status, frame and TCP-segment bounds, identity metadata, TTL, identification,
and sequence metadata, so uninitialized all-zero state must not count as a
written FIN/ACK frame.

## Files changed

- `src/OS.Kernel/Net/TcpConnectionCloseIpv4FramePlanner.cs`
- `tests/OS.HostTests/TcpConnectionCloseIpv4FramePlannerTests.cs`
- `docs/networking.md`
- `docs/completion-gates.md`
- `done/step472.md`

## Tests run

- `dotnet build .\tests\OS.HostTests\OS.HostTests.csproj --nologo`
- `dotnet run --project .\tests\OS.HostTests\OS.HostTests.csproj --no-build | Select-String -Pattern 'net\.tcp\.close\.ipv4 default result is not success|TEST_PASS host\.tests'`
- `dotnet build .\OS.slnx --nologo -m:1`
- `.\scripts\check-naming.ps1`
- `git diff --check`

## Evidence

- Focused host-test build passed with 0 warnings and 0 errors.
- TCP close IPv4 frame planner default-result marker evidence:

```text
TEST_START net.tcp.close.ipv4 default result is not success
TEST_PASS net.tcp.close.ipv4 default result is not success
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

- This is host-tested active-close TCP/IPv4 frame-planning evidence only. It
  does not implement passive close frame wrapping, inbound close ACK/FIN/RST
  validation, live NIC TX/RX, retransmission, timers, sockets, hosted .NET 10,
  ASP.NET Minimal API, Kestrel, cloud readiness, or production readiness.
