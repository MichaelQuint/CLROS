# Step 634 - DNS compression pointer bounds hardening

## Summary

Hardened the Issue #6 DNS A-record response parser so DNS name-compression
pointers must target earlier bytes in the message. Self-referential, forward,
and out-of-range pointers now fail with deterministic status-coded parser
results before the parser follows the pointer.

## Why

The DNS parser already bounded label lengths and jump count, but accepting
forward pointers meant malformed compression could consume parser work before
eventually failing. CloudReady DNS parsing should reject malformed packet
shapes at the narrow parser boundary before resolver state changes.

## Files changed

- `src/OS.Kernel/Net/DnsMessage.cs`
- `tests/OS.HostTests/DnsMessageTests.cs`
- `docs/networking.md`
- `done/step634.md`

## Tests run

- `dotnet build .\tests\OS.HostTests\OS.HostTests.csproj --nologo -m:1 /p:OSWorkloadProfile=SingleMinimalApi /p:UseSharedCompilation=false -nr:false`
- `dotnet run --project .\artifacts\tmp\step634-dns-tests\Step634DnsTests.csproj`
- `dotnet run --project .\tests\OS.HostTests\OS.HostTests.csproj --no-build`
- `git diff --check -- src/OS.Kernel/Net/DnsMessage.cs tests/OS.HostTests/DnsMessageTests.cs docs/networking.md done/step634.md`

## Evidence

- Focused host build completed with `Build succeeded.`, 0 warnings, and 0
  errors.
- Focused DNS harness compiled and ran the actual
  `tests/OS.HostTests/DnsMessageTests.cs` source against `OS.Kernel`.
- Focused DNS markers:

```text
TEST_PASS net.dns writes a-record query
TEST_PASS net.dns parses compressed a-record response
TEST_PASS net.dns rejects malformed compression
TEST_PASS net.dns rejects forward compression pointer
TEST_PASS net.dns rejects out-of-range compression pointer
TEST_PASS net.dns rejects unsupported response
```

## Failures

- The full `tests/OS.HostTests` executable still aborts before DNS tests because
  `CiSmokeScriptTests.TestReturnsNonzeroOnFailedCommand` currently expects the
  CI smoke failure fixture to report two failures, while the dirty host-tool
  tree reports four after libvirt dry-run failures. This is unrelated to the
  DNS parser slice and was not modified here.

## Deferred work

- Live DNS over virtio-net, DHCP-applied DNS configuration, DNS retry timers,
  caching, CNAME/AAAA/TXT records, multi-answer selection policy, sockets/PAL
  exports, hosted .NET 10, ASP.NET Minimal API, and Kestrel behavior remain out
  of scope.

## Evidence boundary

This is host-tested parser hardening over caller-provided DNS message bytes
only. It does not prove live NIC receive, UDP sockets, DHCP DNS configuration,
DNS resolution over a real network, hosted .NET, Kestrel, cloud readiness, or
production readiness.

