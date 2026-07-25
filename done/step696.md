# Step 696 - FetchApp HTTP Completion Expectation Diagnostic Formatter

## Summary
- Added `OS.Net.FetchAppHttpCompletionExpectationDiagnosticFormatter`, a
  caller-owned ASCII formatter for initialized FetchApp HTTP expectation
  results.
- The formatter emits stable `FETCH_HTTP_EXPECTATION` metadata with status,
  initialization, expected HTTP status, expected marker length, and config
  source flags without dumping configured marker or body bytes.
- Added host tests for success formatting, short-destination reporting, and
  default-result rejection.
- Updated focused networking, app-model, manifest, and completion-gate docs.

## Why
FetchApp completion now has manifest/config-derived expectations. This slice
adds deterministic diagnostic formatting for those expectations before the
completion plan consumes marker bytes, while preserving the no-live-network and
no-secret-body evidence boundary.

## Files changed
- `src/OS.Kernel/Net/FetchAppHttpCompletionExpectationDiagnosticFormatter.cs`
- `tests/OS.HostTests/FetchAppHttpCompletionPlanTests.cs`
- `docs/networking.md`
- `docs/app-model.md`
- `docs/manifest.md`
- `docs/completion-gates.md`
- `done/step696.md`

## Tests run
- `dotnet build .\tests\OS.HostTests\OS.HostTests.csproj --nologo`
- `dotnet run --project .\tests\OS.HostTests\OS.HostTests.csproj --no-build | Select-String -Pattern "fetch.http completion expectation|fetch.http completion config|host.tests|TEST_FAIL"`

## Evidence
- Focused host-test build passed with 0 warnings and 0 errors.
- Filtered host-test run exited 0 and emitted:
  - `TEST_PASS fetch.http completion expectation formatter formats success marker`
  - `TEST_PASS fetch.http completion expectation formatter reports short destination`
  - `TEST_PASS fetch.http completion expectation formatter rejects default result`
  - `TEST_PASS host.tests`
- The filtered host-test output contained no selected `TEST_FAIL` lines.

## Failures
- None.

## Deferred work
- Wire expectation diagnostics into a live FetchApp/app-supervisor path only
  after manifest/package loading, FetchApp execution, DNS/socket/TCP/HTTP I/O,
  and app observation exist in the guest.
- Gate 8 remains failed until live virtio-net, DHCP, DNS, TCP, HTTP GET, and
  FetchApp execution have preserved QEMU/libvirt evidence.

