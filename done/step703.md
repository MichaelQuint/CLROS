# Step 703 - FetchApp Request Endpoint Diagnostic Formatter

## Summary
Added a caller-owned diagnostic formatter for
`FetchAppHttpRequestEndpointResult`. The formatter emits a stable
`FETCH_HTTP_ENDPOINT` marker with status, initialization, port, host/path byte
counts, required byte counts, and config-source flags without logging the
configured host or path values.

## Why
FetchApp request endpoint parsing now produces status-coded metadata, but
future boot/app handoff diagnostics need a compact machine-readable marker that
does not leak endpoint strings. This fills the diagnostic gap before any live
DNS, TCP, HTTP, or FetchApp execution path exists.

## Files changed
- `src/OS.Kernel/Net/FetchAppHttpRequestEndpointDiagnosticFormatter.cs`
- `tests/OS.HostTests/FetchAppHttpCompletionPlanTests.cs`
- `docs/networking.md`
- `docs/app-model.md`
- `docs/manifest.md`
- `docs/completion-gates.md`
- `done/step703.md`

## Tests run
- `dotnet build .\tests\OS.HostTests\OS.HostTests.csproj --nologo`
- `dotnet run --project .\tests\OS.HostTests\OS.HostTests.csproj --no-build | Select-String -Pattern "fetch.http request endpoint formatter|host.tests|TEST_FAIL"`

## Evidence
- Host-test build passed with 0 warnings and 0 errors.
- Focused host-test output emitted:
  - `TEST_PASS fetch.http request endpoint formatter formats success marker`
  - `TEST_PASS fetch.http request endpoint formatter reports short destination`
  - `TEST_PASS fetch.http request endpoint formatter rejects default result`
  - `TEST_PASS host.tests`

## Failures
- None in the focused commands above.

## Deferred work
- This is host-tested formatter evidence only. It does not resolve DNS, open
  sockets, perform live NIC I/O, send HTTP requests, execute FetchApp, launch
  hosted .NET, run ASP.NET Minimal API, or prove Kestrel behavior.

