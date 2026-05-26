# Step 086 - DNS Message Primitives

## What Changed

- Added `DnsMessage` encode/decode primitives.
- Added a caller-owned-buffer writer for a single-question A-record query.
- Added compressed A-record response parsing with transaction ID, response status, label, pointer, and bounds validation.

## Why It Changed

Issue #104 creates the DNS wire-format primitive needed before a UDP resolver can be built on top of the future network stack.

## Files Changed

- `src/OS.Kernel/Net/DnsMessage.cs`
- `tests/OS.HostTests/DnsMessageTests.cs`
- `docs/networking.md`
- `docs/completion-gates.md`

## Tests Run

```powershell
dotnet build .\OS.slnx --nologo -m:1
dotnet run --project .\tests\OS.HostTests\OS.HostTests.csproj --no-build
```

## Logs And Evidence

- Host tests emitted:
  - `TEST_PASS net.dns writes a-record query`
  - `TEST_PASS net.dns parses compressed a-record response`
  - `TEST_PASS net.dns rejects malformed compression`
  - `TEST_PASS net.dns rejects unsupported response`

## Failures

- No build or host-test failures after integration.

## Deferred Work

- No UDP send/receive.
- No resolver state machine, retries, caching, CNAME, or AAAA handling.

## Evidence Boundary

This step proves host-side DNS query/response byte handling only. It is not live DNS resolution evidence.
