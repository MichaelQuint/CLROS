# Step 462 - ARP resolver default guard

## Summary

Implemented Gitea issue #447 by making `ArpNeighborResolutionResult` reject
default/zero values as successful ARP resolver transition evidence.

## Why

`ArpNeighborResolutionStatus.Success` is zero. The result previously reported
success for `default(ArpNeighborResolutionResult)` because `Succeeded` only
checked status. ARP resolver callers consume state, packet-status,
bytes-written, and resolved-MAC metadata from this result, so uninitialized
result state must not count as a completed ARP resolution step.

## Files changed

- `src/OS.Kernel/Net/ArpNeighborResolutionTransaction.cs`
- `tests/OS.HostTests/ArpNeighborResolutionTransactionTests.cs`
- `docs/networking.md`
- `docs/completion-gates.md`
- `done/step462.md`

## Tests run

- `dotnet build .\tests\OS.HostTests\OS.HostTests.csproj --nologo`
- `dotnet run --project .\tests\OS.HostTests\OS.HostTests.csproj --no-build | Select-String -Pattern 'net\.arp resolver default result is not success|TEST_PASS host\.tests'`
- `dotnet build .\OS.slnx --nologo -m:1`
- `.\scripts\check-naming.ps1`
- `git diff --check`

## Evidence

- Focused host-test build passed with 0 warnings and 0 errors.
- ARP resolver default-result marker evidence:

```text
TEST_START net.arp resolver default result is not success
TEST_PASS net.arp resolver default result is not success
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

- This is host-tested ARP resolver state-machine evidence only. It does not
  implement timers, retransmission, live ARP, live networking, hosted .NET 10,
  ASP.NET Minimal API, Kestrel, cloud readiness, or production readiness.

