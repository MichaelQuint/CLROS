# Step 652 - HTTP request duplicate content-length rejection

## Summary

Added a small host-tested HTTP request parser hardening path that rejects
duplicate `Content-Length` headers with an explicit parser status before body
metadata is trusted.

## Why

Server-side HTTP request metadata must not accept ambiguous fixed-length body
metadata. This keeps the current parser/writer boundary deterministic without
claiming live socket, TCP receive-loop, TLS, Minimal API, or Kestrel behavior.

## Files changed

- `src/OS.Kernel/Net/HttpRequestParser.cs`
- `tests/OS.HostTests/HttpRequestParserTests.cs`
- `docs/networking.md`
- `done/step652.md`

## Tests run

- `dotnet build .\tests\OS.HostTests\OS.HostTests.csproj --nologo -m:1 /p:OSWorkloadProfile=SingleMinimalApi /p:UseSharedCompilation=false -nr:false`
- `dotnet run --project .\tests\OS.HostTests\OS.HostTests.csproj --no-build`
- `git diff --check -- src/OS.Kernel/Net/HttpRequestParser.cs tests/OS.HostTests/HttpRequestParserTests.cs docs/networking.md done/step652.md`

## Evidence

- Added `HttpRequestParseStatus.DuplicateContentLength`.
- The parser now rejects a second case-insensitive `Content-Length` header
  before parsing or trusting later body metadata.
- Added host-test source markers:
  - `TEST_PASS net.http.request accepts single content length`
  - `TEST_PASS net.http.request rejects duplicate content length`
- `dotnet run --project .\tests\OS.HostTests\OS.HostTests.csproj --no-build`
  emitted `TEST_PASS host.tests`, but it ran the pre-existing assembly because
  the fresh build was blocked before producing updated binaries.
- `git diff --check` for the touched files completed successfully, with only
  the existing `docs/networking.md` LF-to-CRLF warning.

## Failures

- Fresh host-test build failed before this HTTP slice compiled:
  `std/no-runtime/shared/ArrayHelpers.cs(176,24): error CS0029: Cannot implicitly convert type 'int' to 'OS.Std.ArraySearchResult'`.
  That file is outside this issue ownership and was left untouched.

## Deferred work

- Re-run the requested fresh build and host-test command after the unrelated
  `ArrayHelpers.cs` compile break is resolved so the new HTTP request parser
  markers are emitted from an updated assembly.
