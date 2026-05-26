# Step 206 - No-runtime internet checksum helper

## What Changed

- Added `OS.Std.Net.InternetChecksum`, a no-allocation 16-bit ones-complement checksum helper for network-order packet buffers.
- Added `InternetChecksumAccumulator` so callers can checksum one or more `ReadOnlySpan<byte>` segments while preserving odd-byte carryover across segment boundaries.
- Added host tests with `std.internet.checksum` markers for empty input, RFC 1071 vector behavior, odd-byte padding, two- and four-segment odd-boundary carryover, accumulator state, inserted-checksum validation, and reset behavior.
- Updated the no-stdlib limits ledger to document the helper, tests, allocation behavior, and current migration boundary.

## Why It Changed

Network packet code currently has duplicated packet-local checksum loops in IPv4 and ICMP paths, and TCP/UDP checksum work will need segmented checksum support for pseudo headers plus header and payload buffers. The shared primitive belongs in `std/no-runtime/shared` before kernel packet code migrates onto it.

## Files Changed

- `std/no-runtime/shared/Net/InternetChecksum.cs`
- `tests/OS.HostTests/StdInternetChecksumTests.cs`
- `docs/nativeaot-nostdlib-limits.md`
- `done/step206.md`

## Tests Run

```text
dotnet build .\OS.slnx --nologo -m:1
dotnet run --project .\tests\OS.HostTests\OS.HostTests.csproj --no-build *> .\artifacts\logs\issue225-hosttests.log
.\scripts\check-naming.ps1
git diff --check -- std/no-runtime/shared/Net/InternetChecksum.cs tests/OS.HostTests/StdInternetChecksumTests.cs docs/nativeaot-nostdlib-limits.md done/step206.md
```

## Evidence

Build completed with 0 warnings and 0 errors.

The integrated host-test harness exited 0 and emitted the new checksum markers:

```text
TEST_PASS std.internet.checksum empty input
TEST_PASS std.internet.checksum rfc1071 vector
TEST_PASS std.internet.checksum odd byte pads high
TEST_PASS std.internet.checksum segmented odd boundary
TEST_PASS std.internet.checksum four segment vector
TEST_PASS std.internet.checksum accumulator preserves pending byte
TEST_PASS std.internet.checksum validates packet checksum
TEST_PASS std.internet.checksum reset clears state
TEST_PASS host.tests
```

The captured log is `artifacts/logs/issue225-hosttests.log`.

The naming/source firewall script emitted:

```text
TEST_PASS naming.guard
TEST_PASS source.firewall
```

## Failures

No issue-local failures were observed. `git diff --check` exited 0 for the owned files and reported only the existing LF/CRLF normalization warning for `docs/nativeaot-nostdlib-limits.md`.

## Deferred Work

- Existing IPv4, ICMP, UDP, and TCP packet code has not been migrated to this shared helper in this issue.
- No UDP/TCP pseudo-header builder is included in this helper slice.
- No vectorized fast path or table-driven optimization is implemented.
- No live virtio-net, DHCP, DNS, TCP, HTTP, hosted .NET 10, ASP.NET Minimal API, or Kestrel evidence is provided by this host-only helper slice.

## Evidence Boundary

This step proves only deterministic internet checksum behavior over host-testable `ReadOnlySpan<byte>` inputs. It is not evidence of live packet transmission, packet receive validation, network stack completion, hosted .NET 10, ASP.NET Minimal API, or Kestrel support.
