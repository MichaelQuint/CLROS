# Step 771 - HTTP client transaction formatter shared ASCII helpers

## Summary
Migrated `HttpClientGetTransactionDiagnosticFormatter` to the shared
`OS.Std.Text.AsciiDecimalFormatter` and `OS.Std.Text.AsciiTextBuilder` helpers.
The `HTTP_CLIENT_GET_TRANSACTION` marker shape, required-byte calculation,
short-destination no-write behavior, and default-result rejection remain the
same.

## Why
The HTTP client transaction formatter is Phase F networking diagnostic evidence
for already-produced mock-stream HTTP GET transaction results. Its byte-count
and integer formatting behavior should use the shared no-runtime ASCII helper
surface instead of formatter-local decimal digit loops.

## Files changed
- `src/OS.Kernel/Net/HttpClientGetTransactionDiagnosticFormatter.cs`
- `tests/OS.HostTests/HttpClientGetTransactionTests.cs`
- `docs/networking.md`
- `done/step771.md`

## Tests run
- `dotnet build .\tests\OS.HostTests\OS.HostTests.csproj --nologo -p:UseAppHost=false -o .\artifacts\issue726-http-client-transaction-formatter\bin\Debug\net10.0`
- `dotnet .\artifacts\issue726-http-client-transaction-formatter\bin\Debug\net10.0\OS.HostTests.dll`
- `git diff --check`
- Clean integrated rerun:
  `dotnet .\artifacts\issue730-cpu-fault-panic-formatter\bin\Debug\net10.0\OS.HostTests.dll *>&1 | Tee-Object -FilePath .\artifacts\logs\issue730-hosttests-20260525.log`
- Clean integrated source check:
  `git diff --check *>&1 | Tee-Object -FilePath .\artifacts\logs\issue730-git-diff-check-20260525.log`

## Evidence
- Build log: `artifacts/logs/issue726-host-build-20260525.log`
  - `Build succeeded.`
  - `0 Warning(s)`
  - `0 Error(s)`
- Host-test log: `artifacts/logs/issue726-hosttests-20260525.log`
  - The requested full host-test DLL retry cleared the earlier CI smoke timeout
    (`TEST_PASS ci.smoke returns nonzero on failed command`) but failed before
    reaching the HTTP client transaction formatter markers because the unrelated
    diagnostics source guard failed.
  - Failure line:
    `TEST_FAIL diagnostics.cpu_fault_panic formatter uses shared decimal formatter`
- Clean integrated host-test rerun:
  `artifacts/logs/issue730-hosttests-20260525.log`
  - `TEST_PASS diagnostics.cpu_fault_panic formatter uses shared ascii text builder`
  - `TEST_PASS network http client transaction formatter uses shared ascii text builder`
  - `TEST_PASS host.tests`
- Clean integrated source check:
  `artifacts/logs/issue730-git-diff-check-20260525.log`
  exited 0 with only existing LF-to-CRLF working-copy warnings.
- Diff-check log: `artifacts/logs/issue726-git-diff-check-20260525.log`
  - The requested `git diff --check` command emitted existing Git LF-to-CRLF
    working-copy warnings from the dirty shared tree.
  - No whitespace-error diagnostics such as trailing whitespace, space-before-tab,
    or blank-line-at-EOF entries were found in the log.
  - A scoped `git diff --check --` over the issue #726 files returned
    `LASTEXIT:0`.
- Source guard inspection:
  - `HttpClientGetTransactionDiagnosticFormatter.cs` uses
    `AsciiDecimalFormatter.CountInt64Digits`,
    `AsciiDecimalFormatter.CountUInt64Digits`,
    `AsciiTextBuilder.AppendBytes`,
    `AsciiTextBuilder.AppendInt64Decimal`, and
    `AsciiTextBuilder.AppendUInt64Decimal`.
  - The formatter no longer contains
    `GetInt64DecimalByteCount`, `GetUInt64DecimalByteCount`, formatter-local
    `Append`, `AppendInt64`, `AppendUInt64`, direct decimal digit writes, or
    direct `value.CopyTo(destination[offset..])` byte-copy calls.

## Failures
- None in the clean integrated rerun.
- The original full host-test DLL command failed in an unrelated diagnostics
  formatter source-guard test before the networking formatter source-guard
  marker could execute. The clean integrated rerun passed the updated
  diagnostics marker, emitted the networking formatter marker, and ended with
  `TEST_PASS host.tests`, so the original failure is recorded as an
  overlapping-slice ordering artifact rather than #726 evidence.
- The requested full-tree `git diff --check` pipeline returned nonzero in this
  PowerShell run after Git wrote LF-to-CRLF warnings for many already-dirty
  files. No whitespace-error diagnostics were present in the captured log. The
  clean integrated source check exited 0 with the same class of warnings and no
  issue-specific whitespace error.

## Deferred work
- Continue migrating the remaining networking diagnostic formatters that still
  carry local decimal byte-count helpers or append loops.

## Evidence boundary
This step proves the focused source migration and successful focused build for
caller-owned byte-buffer formatting of already-produced
`HttpClientGetTransactionResult` metadata. It does not prove live virtio-net,
DHCP, DNS, TCP, HTTP over a NIC, sockets/PAL exports, retransmission, timers,
hosted .NET, ASP.NET Minimal API, Kestrel, cloud-provider networking, or
production readiness.

