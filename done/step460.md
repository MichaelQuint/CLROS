# Step 460 - DNS resolver default guard

## Summary

Implemented Gitea issue #445 by making
`DnsResolverTransitionResult` reject default/zero values as successful DNS
resolver transition evidence.

## Why

`DnsResolverStatus.Success` is zero. The transition result previously reported
success for `default(DnsResolverTransitionResult)` because `Succeeded` only
checked status. DNS resolver callers consume state, message-status,
bytes-written, and address metadata from this result, so uninitialized transition
state must not count as a completed resolver step.

## Files changed

- `src/OS.Kernel/Net/DnsResolverTransaction.cs`
- `tests/OS.HostTests/DnsResolverTests.cs`
- `docs/networking.md`
- `docs/completion-gates.md`
- `done/step460.md`

## Tests run

- `dotnet build .\tests\OS.HostTests\OS.HostTests.csproj --nologo`
- `dotnet run --project .\tests\OS.HostTests\OS.HostTests.csproj --no-build | Select-String -Pattern 'net\.dns resolver default result is not success|TEST_PASS host\.tests'`
- `dotnet build .\OS.slnx --nologo -m:1`
- `.\scripts\check-naming.ps1`
- `git diff --check`

## Evidence

- Focused host-test build passed with 0 warnings and 0 errors.
- DNS resolver default-result marker evidence:

```text
TEST_START net.dns resolver default result is not success
TEST_PASS net.dns resolver default result is not success
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

- This is host-tested DNS resolver state-machine evidence only. It does not
  implement retransmission, timers, sockets, DNS caching, DHCP-applied DNS
  configuration, live DNS, live networking, hosted .NET 10, ASP.NET Minimal API,
  Kestrel, cloud readiness, or production readiness.

