# Step 630 - HTTP response parser requires fixed-length bodies

## Summary

Added the Issue #6 native-tier HTTP response parser slice for fixed-length
HTTP/1.0 and HTTP/1.1 responses. The parser now rejects responses without
`Content-Length`, preserves selected response metadata, and returns a payload
span only when the declared body is fully present.

## Why

The native-tier HTTP GET pipeline needs a small, host-testable HTTP/1.1 response
parser before any FetchApp-facing work can consume response bytes. This step
keeps the contract bounded to caller-owned byte spans and fixed-length bodies so
it does not imply live TCP, sockets, chunked transfer, or network-device support.

## Files changed

- `src/OS.Kernel/Net/HttpResponseParser.cs`
- `tests/OS.HostTests/HttpResponseParserTests.cs`
- `docs/networking.md`
- `done/step630.md`

## Tests run

- `dotnet build .\tests\OS.HostTests\OS.HostTests.csproj --nologo -m:1 /p:OSWorkloadProfile=SingleMinimalApi /p:UseSharedCompilation=false -nr:false`
- `dotnet run --project .\tests\OS.HostTests\OS.HostTests.csproj --no-build`
- `git diff --check`

## Evidence

- Build completed with `Build succeeded.`, 0 warnings, and 0 errors.
- Host test run completed with `TEST_PASS host.tests`.
- Focused HTTP response parser markers:

```text
TEST_PASS net.http parses fixed-length 200 response
TEST_PASS net.http parses zero-length response body
TEST_PASS net.http rejects malformed status line
TEST_PASS net.http rejects incomplete headers
TEST_PASS net.http rejects invalid content length
TEST_PASS net.http rejects missing content length
TEST_PASS net.http rejects short body
TEST_PASS net.http rejects unsupported transfer encoding
TEST_PASS net.http response parse default result is not success
```

- Adjacent pipeline markers also passed:

```text
TEST_PASS net.http client writes request and parses response
TEST_PASS net.http client rejects malformed response
TEST_PASS net.http.tcp.get completes request response
TEST_PASS net.http.tcp.get preserves http response failures
```

- `git diff --check` returned success. It printed only CRLF normalization
  warnings from the already-dirty worktree.

## Failures

- An initial build attempt hit a stale completed `OS.HostTests.exe` process
  holding `OS.Kernel.dll` open from prior evidence collection. After confirming
  that log already ended in `TEST_PASS host.tests`, the stale process was
  stopped and the requested build/run commands passed.

## Deferred work

- Chunked transfer, close-delimited bodies, redirects, TLS, retries, live
  sockets, live TCP retransmission/timers, virtio-net packet I/O, FetchApp
  network behavior, hosted .NET 10, ASP.NET Minimal API, and Kestrel behavior
  remain out of scope.

## Evidence boundary

This is host-tested parser and caller-owned-buffer HTTP GET pipeline evidence
only. It proves fixed-length response parsing and failure classification for the
native-tier client path; it is not evidence of TCP/live networking, socket/PAL
exports, live NIC queues, cloud readiness, production readiness, FetchApp
network behavior, hosted .NET 10, ASP.NET Minimal API, or Kestrel.

