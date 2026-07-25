# Step 101 - DNS Resolver Transaction State

## What Changed

- Added `DnsResolverTransaction` for a mock-packet single-question A-record DNS resolver transaction.
- Added status-coded query writing, response validation, resolved IPv4 capture, and explicit timeout transition over caller-provided DNS message buffers.
- Added host tests for successful resolution, mismatched transaction ID rejection, and timeout failure.

## Why It Changed

Issue #118 moves DNS from message encode/decode primitives toward a deterministic resolver state boundary before live UDP packet I/O, retry timers, caching, or the future HTTP client path.

## Files Changed

- `src/OS.Kernel/Net/DnsResolverTransaction.cs`
- `tests/OS.HostTests/DnsResolverTests.cs`
- `docs/networking.md`
- `docs/completion-gates.md`

## Tests Run

```powershell
dotnet build .\OS.slnx --nologo -m:1
dotnet run --project .\tests\OS.HostTests\OS.HostTests.csproj --no-build
```

## Logs And Evidence

- Host tests emitted:
  - `TEST_PASS net.dns resolver writes query and resolves response`
  - `TEST_PASS net.dns resolver rejects mismatched transaction id`
  - `TEST_PASS net.dns resolver times out query`

## Failures

- No build or host-test failures after integration.

## Deferred Work

- No virtio-net or real UDP packet I/O.
- No resolver retransmission timers or retry policy.
- No DNS cache.
- No CNAME, AAAA, TXT, or multi-question support.
- No HTTP GET, hosted .NET 10, or Kestrel evidence.

## Evidence Boundary

This step proves a host-side mock DNS resolver transaction only. It is not live DNS, HTTP, hosted .NET 10, or Kestrel evidence.
