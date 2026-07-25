# Step 093 - HTTP GET Request Builder

## What Changed

- Added `HttpRequestBuilder` for deterministic HTTP/1.1 GET request bytes.
- Writes `Host`, `User-Agent`, `Accept`, and `Connection: close` headers into caller-owned storage.
- Rejects empty/unsafe hosts, empty paths, paths without a leading slash, CR/LF/control bytes, and too-small destinations.

## Why It Changed

Issue #115 adds the request half of the future native-tier HTTP GET path before TCP sockets or FetchApp integration exist.

## Files Changed

- `src/OS.Kernel/Net/HttpRequestBuilder.cs`
- `tests/OS.HostTests/HttpRequestBuilderTests.cs`
- `docs/networking.md`
- `docs/completion-gates.md`

## Tests Run

```powershell
dotnet build .\OS.slnx --nologo -m:1
dotnet run --project .\tests\OS.HostTests\OS.HostTests.csproj --no-build
```

## Logs And Evidence

- Host tests emitted:
  - `TEST_PASS net.http writes get request`
  - `TEST_PASS net.http rejects unsafe request path`
  - `TEST_PASS net.http rejects request buffer too small`

## Failures

- No build or host-test failures after integration.

## Deferred Work

- No URL parser.
- No TCP connection.
- No chunked transfer.
- No TLS.
- No FetchApp or Kestrel evidence.

## Evidence Boundary

This step proves host-side HTTP request byte generation only. It is not HTTP GET, network stack, hosted .NET 10, or Kestrel evidence.
