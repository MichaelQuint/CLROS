# Step 724 - HTTP Header OWS and Duplicate Content-Length Guards

## Summary

Implemented Gitea issues #679 through #682 as a networking/std slice:

- #679: `HttpResponseParser` now rejects duplicate `Content-Length` response headers with `DuplicateContentLength`.
- #680: `OS.Std.Text.AsciiString` now exposes `TrimSpaceOrTab` for no-runtime ASCII OWS trimming.
- #681: `HttpRequestParser` now validates raw header values first, then trims semantic request header values for `Content-Length`, `Connection`, and `Transfer-Encoding`.
- #682: `HttpResponseParser` now applies the same raw-validate then semantic-trim behavior for response headers.

## Why

HTTP parser behavior was still carrying local whitespace handling and response-side duplicate `Content-Length` was accepted. CloudReady HTTP parsing needs deterministic, shared no-runtime helpers before FetchApp, server demos, PAL sockets, and future hosted Kestrel paths can trust parsed metadata.

## Files changed

- `std/no-runtime/shared/Text/AsciiString.cs`
- `tests/OS.HostTests/StdAsciiStringTests.cs`
- `src/OS.Kernel/Net/HttpRequestParser.cs`
- `tests/OS.HostTests/HttpRequestParserTests.cs`
- `src/OS.Kernel/Net/HttpResponseParser.cs`
- `tests/OS.HostTests/HttpResponseParserTests.cs`
- `docs/networking.md`
- `done/step724.md`

## Tests run

- `dotnet build .\tests\OS.HostTests\OS.HostTests.csproj --nologo`
- `dotnet run --project .\tests\OS.HostTests\OS.HostTests.csproj --no-build`
- `git diff --check`

## Evidence

- Build log: `artifacts\logs\issues679-682-host-build-20260525.log`
  - Result: `Build succeeded.`
  - Result: `0 Warning(s)`, `0 Error(s)`
- Host test log: `artifacts\logs\issues679-682-hosttests-20260525.log`
  - `TEST_PASS net.http rejects duplicate response content length`
  - `TEST_PASS std.ascii.string trims space and tab`
  - `TEST_PASS net.http.request trims header value OWS`
  - `TEST_PASS net.http.response trims header value OWS`
  - `TEST_PASS host.tests`
- Whitespace log before this done-step: `artifacts\logs\issues679-682-git-diff-check-20260525.log`
  - Exit code: 0
  - Output contained only existing LF-to-CRLF working-copy warnings.
- Whitespace log after docs/done update: `artifacts\logs\issues679-682-git-diff-check-postdone-20260525.log`
  - Exit code: 0
  - Output contained only existing LF-to-CRLF working-copy warnings.

## Failures

- No integrated build, host-test, or `git diff --check` failures for this slice.
- No real QEMU boot was run for this parser/std slice; evidence is host-tested only.

## Deferred work

- Live socket, virtio-net, FetchApp, and Kestrel paths still need to consume these parser guarantees through real guest I/O.
- Chunked transfer, close-delimited bodies, TLS, redirects, and full HTTP server semantics remain unsupported.
- Response duplicate `Content-Length` handling now rejects duplicates outright; no RFC variant that accepts identical duplicate lengths is implemented.
