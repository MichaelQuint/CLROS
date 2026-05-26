# Step 775 - FetchApp endpoint formatter shared ASCII builder

## Summary

Migrated the FetchApp HTTP request endpoint diagnostic formatter from
formatter-local decimal sizing and append helpers to the shared no-runtime
ASCII text helpers.

## Why

The endpoint formatter already emits a stable `FETCH_HTTP_ENDPOINT` marker in
caller-owned storage. Reusing `OS.Std.Text.AsciiDecimalFormatter` and
`OS.Std.Text.AsciiTextBuilder` keeps its byte counting and writes aligned with
the shared no-runtime formatting surface without changing marker text or status
behavior.

## Files changed

- `src/OS.Kernel/Net/FetchAppHttpRequestEndpointDiagnosticFormatter.cs`
- `tests/OS.HostTests/FetchAppHttpRequestEndpointBootSequenceTests.cs`
- `docs/networking.md`
- `done/step775.md`

## Tests run

- `dotnet build .\tests\OS.HostTests\OS.HostTests.csproj --nologo -p:UseAppHost=false -o .\artifacts\issue733-fetch-http-endpoint-formatter\bin\Debug\net10.0 *>&1 | Tee-Object -FilePath .\artifacts\logs\issue733-host-build-20260525.log`
- `dotnet .\artifacts\issue733-fetch-http-endpoint-formatter\bin\Debug\net10.0\OS.HostTests.dll *>&1 | Tee-Object -FilePath .\artifacts\logs\issue733-hosttests-20260525.log`
- `git diff --check *>&1 | Tee-Object -FilePath .\artifacts\logs\issue733-git-diff-check-20260525.log`

## Evidence

- Build succeeded with `0 Warning(s)` and `0 Error(s)`.
- The full host-test harness started and reached the CI smoke-script slice, but
  stopped before the FetchApp HTTP request endpoint module-initializer tests
  could run.
- Integrated rerun evidence from
  `artifacts/logs/issue734-hosttests-20260525.log` later exercised the same
  worktree and passed:
  - `TEST_PASS fetch.http request endpoint formatter formats success marker`
  - `TEST_PASS fetch.http request endpoint formatter reports short destination`
  - `TEST_PASS fetch.http request endpoint formatter rejects default result`
  - `TEST_PASS fetch.http request endpoint formatter uses shared ascii text builder`
  - `TEST_PASS host.tests`
- The requested source guard was added as
  `fetch.http request endpoint formatter uses shared ascii text builder`.
- The formatter source now calls
  `AsciiDecimalFormatter.CountUInt64Digits(result.Port)`,
  `AsciiDecimalFormatter.CountInt64Digits(...)`,
  `AsciiTextBuilder.AppendBytes(...)`,
  `AsciiTextBuilder.AppendInt64Decimal(...)`, and
  `AsciiTextBuilder.AppendUInt64Decimal(...)`.
- The existing formatter tests still encode the exact success marker text,
  short-destination behavior, required-byte query path, invalid/default result
  rejection, and no-partial-write behavior.
- `git diff --check` emitted LF-to-CRLF working-copy warnings across existing
  modified files. No trailing-whitespace, space-before-tab, or blank-at-EOF
  error lines were present in the captured output.

## Failures

- `dotnet .\artifacts\issue733-fetch-http-endpoint-formatter\bin\Debug\net10.0\OS.HostTests.dll *>&1 | Tee-Object -FilePath .\artifacts\logs\issue733-hosttests-20260525.log`
  exited nonzero on `TEST_FAIL ci.smoke returns nonzero on failed command`.
  The failure is in `tests/OS.HostTests/CiSmokeScriptTests.cs` and prevented
  later module-initializer tests from running in that host-test pass.
- The later integrated host-test run in
  `artifacts/logs/issue734-hosttests-20260525.log` passed this formatter slice
  and the full harness after the unrelated CI marker-preset issue was repaired.
- `git diff --check *>&1 | Tee-Object -FilePath .\artifacts\logs\issue733-git-diff-check-20260525.log`
  exited nonzero because Git wrote LF-to-CRLF warnings for the already-dirty
  working tree.

## Deferred work

- Live virtio-net, DNS, TCP, HTTP over a NIC, FetchApp execution, FetchApp QEMU
  output, hosted .NET, ASP.NET Minimal API, and Kestrel remain outside this
  formatter migration.

## Evidence boundary

This step provides source/build evidence for the FetchApp HTTP request endpoint
diagnostic formatter migration and records a blocked full host-test run. It is
not evidence of live virtio-net, DNS, TCP, HTTP over a NIC, FetchApp execution,
FetchApp QEMU output, hosted .NET, ASP.NET Minimal API, or Kestrel behavior.
