# Step 678 - HTTP TCP GET plan diagnostic formatter

## Summary
Added a caller-owned ASCII diagnostic formatter for
`HttpTcpClientGetPlanResult` with stable `HTTP_TCP_GET_PLAN` marker text,
required-byte querying, short-destination reporting, and default-result
rejection.

## Why
Issue #600 needs the HTTP TCP GET planner slice to expose machine-readable
plan diagnostics without allocating or inventing metadata beyond the existing
planner result.

## Files changed
- `src/OS.Kernel/Net/HttpTcpClientGetPlanDiagnosticFormatter.cs`
- `tests/OS.HostTests/HttpTcpClientGetPlannerTests.cs`
- `docs/networking.md`
- `docs/completion-gates.md`
- `done/step678.md`

## Tests run
- `dotnet build .\tests\OS.HostTests\OS.HostTests.csproj --nologo`
  - Result: pass.
- `dotnet run --project .\tests\OS.HostTests\OS.HostTests.csproj --no-build`
  - Result: pass.
- `git diff --check -- .\src\OS.Kernel\Net .\tests\OS.HostTests\HttpTcpClientGetPlannerTests.cs .\docs\networking.md .\docs\completion-gates.md .\done`
  - Result: pass, with CRLF normalization warnings on existing tracked files.

## Evidence
- Host tests emitted `TEST_PASS net.http.tcp.get formatter formats success marker`.
- Host tests emitted `TEST_PASS net.http.tcp.get formatter reports short destination`.
- Host tests emitted `TEST_PASS net.http.tcp.get formatter default result is not success`.
- Host tests emitted `TEST_PASS host.tests`.
- The formatter writes stable caller-owned ASCII beginning with
  `HTTP_TCP_GET_PLAN status=` and includes only existing plan result metadata:
  planner state, nested TCP frame/TCP stream/HTTP request/HTTP response/packet
  statuses, byte counts, TCP segment metadata, HTTP status/body metadata, and
  local/remote next sequence numbers.
- Short destination formatting reports required and available byte counts with
  zero bytes written and no partial marker success.

## Failures
- None in the final required build and no-build host-test commands.

## Deferred work
- No live NIC I/O, sockets, retransmission, timers, TLS, FetchApp, hosted
  .NET 10, ASP.NET Minimal API, Kestrel, cloud readiness, or production
  readiness is proven by this slice.

## Evidence boundary
- This is host-tested formatter and planner-result metadata evidence only over
  caller-owned buffers and synthetic TCP/HTTP frames. It does not prove live
  virtio-net, DHCP/DNS, full TCP state, live HTTP GET, PAL socket exports,
  hosted .NET, or Kestrel behavior.

