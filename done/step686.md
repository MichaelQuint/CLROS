# Step 686 - FetchApp HTTP Completion Diagnostic Formatter

## Summary

Added a caller-owned diagnostic formatter for FetchApp HTTP completion results.
The formatter emits a stable `FETCH_HTTP_COMPLETION` marker containing the
completion status, nested HTTP TCP GET plan status, HTTP status expectations,
response/body byte metadata, expected marker length, and match offset.

## Why

Gitea #608 needed a machine-readable FetchApp completion diagnostic surface
without allocating or throwing for normal kernel formatter failures. The
existing `FetchAppHttpCompletionPlan` could evaluate a successful HTTP TCP GET
result, but there was no stable byte-span formatter for serial/test evidence.

## Files changed

- `src/OS.Kernel/Net/FetchAppHttpCompletionPlanDiagnosticFormatter.cs`
  - Added status-coded required-byte query and format APIs.
  - Added short-destination and default/uninitialized-result rejection.
- `tests/OS.HostTests/FetchAppHttpCompletionPlanTests.cs`
  - Added focused formatter success, short-destination, and default-result
    host markers.
- `docs/networking.md`
  - Documented the `FETCH_HTTP_COMPLETION` marker boundary.
- `docs/app-model.md`
  - Documented FetchApp's host-tested completion marker boundary.
- `docs/completion-gates.md`
  - Preserved Gate 8 evidence and host-tested boundary wording.

## Tests run

- `dotnet build .\tests\OS.HostTests\OS.HostTests.csproj --nologo`
- `dotnet run --project .\tests\OS.HostTests\OS.HostTests.csproj --no-build | Select-String -Pattern "fetch\.http completion formatter|host\.tests|TEST_FAIL"`
- `git diff --check -- .\src\OS.Kernel\Net .\tests\OS.HostTests .\docs\networking.md .\docs\app-model.md .\docs\completion-gates.md .\done`

## Evidence

- Host-test build succeeded with 0 errors. MSBuild emitted transient copy-retry
  warnings because an existing `OS.HostTests.exe` process briefly held copied
  output DLLs.
- Focused filtered host-test run emitted:
  - `TEST_PASS fetch.http completion formatter formats success marker`
  - `TEST_PASS fetch.http completion formatter reports short destination`
  - `TEST_PASS fetch.http completion formatter rejects default result`
  - `TEST_PASS host.tests`

## Failures

- No #608 formatter test failures were observed.
- The host-test build warning boundary was an output-file lock from a concurrent
  host-test process, not a compile error.

## Deferred work

- Wire FetchApp completion diagnostics into a real app/supervisor path after
  live app execution and log routing exist.
- Prove HTTP completion through live virtio-net packet I/O, DNS, TCP, and
  FetchApp execution in a later networking/app-model milestone.

## Evidence boundary

This is host-tested formatting evidence over already-produced FetchApp HTTP
completion metadata. It does not prove DNS, sockets, live NIC I/O, TLS,
redirect handling, live FetchApp execution, hosted .NET 10, Kestrel, or cloud
readiness.

