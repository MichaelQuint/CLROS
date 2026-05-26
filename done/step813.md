# Step 813 - HTTP Static Response Diagnostic Formatter

## Summary

Added caller-owned ASCII diagnostic formatting for
`HttpStaticResponseTransactionResult`. Initialized transaction results can now
produce a stable `HTTP_STATIC_RESPONSE` marker that records status and byte
metadata without dumping request-body or configured response-body content.

## Why

The static HTTP response transaction already host-tested 200/404/405 response
planning for a future server-side path, but follow-on boot/app/network
diagnostics need a compact marker format that can be emitted from caller-owned
storage. This slice keeps the boundary diagnostic-only: it serializes
already-produced metadata and does not start sockets, a TinyHttpServer loop,
hosted .NET, ASP.NET Minimal API, or Kestrel.

## Files changed

- `src/OS.Kernel/Net/HttpStaticResponseTransactionDiagnosticFormatter.cs`
- `tests/OS.HostTests/HttpStaticResponseTransactionTests.cs`
- `docs/networking.md`
- `docs/completion-gates.md`
- `done/step813.md`

## Tests run

- `dotnet build .\tests\OS.HostTests\OS.HostTests.csproj --nologo *> .\artifacts\logs\issue771-host-build-r2.log`
- `dotnet .\tests\OS.HostTests\bin\Debug\net10.0\OS.HostTests.dll *> .\artifacts\logs\issue771-host-tests-r2.log`
- Scoped `git diff --check` for the changed files.
- Scoped trailing-whitespace scan for the changed files.

## Evidence

- `artifacts/logs/issue771-host-build-r2.log` ended with `Build succeeded.`,
  `0 Warning(s)`, and `0 Error(s)`.
- `artifacts/logs/issue771-host-tests-r2.log` ended with `TEST_PASS host.tests`.
- `artifacts/logs/issue771-host-tests-r2.log` emitted:
  - `TEST_PASS net.http.static.formatter formats 200 marker`
  - `TEST_PASS net.http.static.formatter formats 404 and 405 metadata`
  - `TEST_PASS net.http.static.formatter reports destination capacity`
  - `TEST_PASS net.http.static.formatter rejects default and invalid results`
  - `TEST_PASS net.http.static.formatter uses shared ascii text builder`
- `artifacts/logs/issue771-diff-check.log` ended with
  `git-diff-check-exit=0`.
- `artifacts/logs/issue771-trailing-whitespace.log` emitted
  `TEST_PASS issue771.trailing_whitespace`.

## Failures

- The first host-test run in `artifacts/logs/issue771-host-tests.log` failed two
  new formatter tests because the tests treated `QueryRequiredBytes` as a full
  format success even though local formatter query results intentionally report
  `BytesWritten=0`. The tests were corrected to assert query status and required
  bytes, then the build and host suite were rerun successfully.

## Deferred work

- Wire server-side HTTP response diagnostics into a future live TCP listener
  only after live virtio-net RX/TX, socket/PAL boundaries, scheduler waits, and
  app-supervisor/Kestrel prerequisites exist.
- This step is host-tested diagnostic formatting only. It does not prove live
  sockets, TinyHttpServer, hosted .NET 10, ASP.NET Minimal API, Kestrel, cloud
  readiness, or production readiness.

