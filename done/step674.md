# Step 674 - HTTP Client GET Diagnostic Formatter

## Summary
Added a caller-owned ASCII diagnostic formatter for
`HttpClientGetTransactionResult` metadata and focused host tests for the stable
`HTTP_CLIENT_GET_TRANSACTION` marker contract.

## Why
The networking stack has host-tested HTTP GET request/response state-machine
metadata, but no narrow machine-readable formatter that can be reused by later
QEMU smoke, UART, or boot-sequence diagnostics without allocation or partial
short-buffer writes.

## Files changed
- `src/OS.Kernel/Net/HttpClientGetTransactionDiagnosticFormatter.cs`
- `tests/OS.HostTests/HttpClientGetTransactionTests.cs`
- `docs/networking.md`
- `docs/completion-gates.md`

## Tests run
- `dotnet build .\tests\OS.HostTests\OS.HostTests.csproj --nologo`
- `dotnet run --project .\tests\OS.HostTests\OS.HostTests.csproj --no-build`

## Evidence
- Host-test build passed with 0 warnings and 0 errors after the integrated
  tree included the PAL marker helper fix.
- Full host tests ended with `TEST_PASS host.tests`.
- Formatter tests emitted:
  - `TEST_PASS net.http.clientget formatter formats success marker`
  - `TEST_PASS net.http.clientget formatter reports short destination`
  - `TEST_PASS net.http.clientget formatter default result is not success`
- The success marker contract is:
  - `HTTP_CLIENT_GET_TRANSACTION status=Success state=ResponseReceived request=Success response=Success requestBytes=<n> responseStatus=200 bodyBytes=5`

## Failures
- None in the final integrated verification. An earlier subagent build saw a
  stale dirty-tree compile failure in `BootSequence.cs` while the PAL marker
  helper was mid-edit; the integrated build and full host run passed afterward.

## Deferred work
- Wire the formatter into a future boot/QEMU/UART diagnostic path only after a
  networking phase explicitly owns that integration.
- This formatter does not open sockets, use a live NIC, perform DNS/TCP/HTTP
  over packet I/O, implement TLS, prove FetchApp, hosted .NET 10, ASP.NET
  Minimal API, or Kestrel behavior.

## Evidence Boundary
This is host-tested formatter evidence. It does not prove live NIC, socket,
DNS, TCP, TLS, FetchApp, hosted .NET 10, ASP.NET Minimal API, or Kestrel
behavior.

