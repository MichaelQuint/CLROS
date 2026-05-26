# Step 461 - DHCP client default guard

## Summary

Implemented Gitea issue #446 by making
`DhcpClientTransitionResult` reject default/zero values as successful DHCP
client transition evidence.

## Why

`DhcpClientStatus.Success` is zero. The transition result previously reported
success for `default(DhcpClientTransitionResult)` because `Succeeded` only
checked status. DHCP client callers consume state, message-status, and
bytes-written metadata from this result, so uninitialized transition state must
not count as a completed DHCP client step.

## Files changed

- `src/OS.Kernel/Net/DhcpClientTransaction.cs`
- `tests/OS.HostTests/DhcpClientTests.cs`
- `docs/networking.md`
- `docs/completion-gates.md`
- `done/step461.md`

## Tests run

- `dotnet build .\tests\OS.HostTests\OS.HostTests.csproj --nologo`
- `dotnet run --project .\tests\OS.HostTests\OS.HostTests.csproj --no-build | Select-String -Pattern 'net\.dhcp client default result is not success|TEST_PASS host\.tests'`
- `dotnet build .\OS.slnx --nologo -m:1`
- `.\scripts\check-naming.ps1`
- `git diff --check`

## Evidence

- Focused host-test build passed with 0 warnings and 0 errors.
- DHCP client default-result marker evidence:

```text
TEST_START net.dhcp client default result is not success
TEST_PASS net.dhcp client default result is not success
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

- This is host-tested DHCP client state-machine evidence only. It does not
  implement timers, retransmission, sockets, lease renewal/rebind scheduling,
  address assignment to an interface, ARP conflict detection, live DHCP, live
  networking, hosted .NET 10, ASP.NET Minimal API, Kestrel, cloud readiness, or
  production readiness.

