# Step 780 - Issue 738 FetchApp formatter AsciiTextBuilder migration

## Summary
Migrated the FetchApp HTTP completion, completion-expectation, and TCP
exchange diagnostic formatters from local destination-slice copy and decimal
append helpers to the shared `OS.Std.Text.AsciiTextBuilder` append APIs.

Added host-test source guards proving the owned FetchApp formatters use the
shared builder and no longer contain the direct
`value.CopyTo(destination[offset..])` helper pattern.

## Why
Issue #738 requires Phase F networking FetchApp completion/exchange marker
formatters to use the shared no-runtime ASCII text builder while preserving
stable marker text, short-destination behavior, and allocation-free diagnostic
formatting boundaries.

## Files changed
- `src/OS.Kernel/Net/FetchAppHttpCompletionPlanDiagnosticFormatter.cs`
- `src/OS.Kernel/Net/FetchAppHttpCompletionExpectationDiagnosticFormatter.cs`
- `src/OS.Kernel/Net/FetchAppHttpTcpExchangePlanDiagnosticFormatter.cs`
- `tests/OS.HostTests/FetchAppHttpCompletionPlanTests.cs`
- `tests/OS.HostTests/FetchAppHttpTcpExchangePlanTests.cs`
- `done/step780.md`

## Tests run
- `dotnet build .\tests\OS.HostTests\OS.HostTests.csproj --nologo -p:UseAppHost=false -o .\artifacts\issue738-fetchapp-completion-formatters\bin\Debug\net10.0`
- `dotnet .\artifacts\issue738-fetchapp-completion-formatters\bin\Debug\net10.0\OS.HostTests.dll`

## Evidence
- Build log: `artifacts/logs/issue738-build.log`
- Host-test log: `artifacts/logs/issue738-hosttests.log`
- Relevant host-test markers:
  - `TEST_PASS fetch.http completion expectation formatter formats success marker`
  - `TEST_PASS fetch.http completion expectation formatter reports short destination`
  - `TEST_PASS fetch.http completion expectation formatter rejects default result`
  - `TEST_PASS fetch.http completion formatters use shared ascii text builder`
  - `TEST_PASS fetch.http completion formatter formats success marker`
  - `TEST_PASS fetch.http completion formatter reports short destination`
  - `TEST_PASS fetch.http completion formatter rejects default result`
  - `TEST_PASS fetch.http tcp exchange formatter formats success marker`
  - `TEST_PASS fetch.http tcp exchange formatter reports short destination`
  - `TEST_PASS fetch.http tcp exchange formatter rejects default result`
  - `TEST_PASS fetch.http tcp exchange formatter uses shared ascii text builder`
  - `TEST_PASS host.tests`

## Failures
None.

## Deferred work
`docs/networking.md` was intentionally not edited to avoid colliding with
another networking worker. Suggested documentation text:

`Current #738 evidence covers only FetchApp HTTP completion, completion-expectation, and TCP exchange diagnostic formatter internals. It proves those stable caller-owned ASCII markers now route byte and decimal appends through OS.Std.Text.AsciiTextBuilder while preserving exact marker text, short-destination no-write behavior, and default-result rejection; it does not prove live virtio-net, DNS, TCP, HTTP over a NIC, FetchApp execution, hosted .NET, ASP.NET Minimal API, or Kestrel.`

