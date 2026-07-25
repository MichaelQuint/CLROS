# Step 692 - FetchApp HTTP Completion Manifest Config Expectation

## Summary
- Added `OS.Net.FetchAppHttpCompletionExpectation`, a host-tested,
  no-I/O planner that derives FetchApp HTTP completion expectations from
  parsed native manifest environment/config entries.
- The planner defaults to HTTP status `200` and marker `FETCH_OK`, supports
  `FETCH_HTTP_EXPECTED_STATUS`, `FETCH_HTTP_EXPECTED_MARKER`,
  `FETCH_HTTP_EXPECTED_BODY`, and `OS_SMOKE_EXPECTED_BODY`, and reports
  status-coded malformed config cases.
- Extended FetchApp HTTP completion host tests to prove parsed manifest config
  flows into `FetchAppHttpCompletionPlan`.
- Updated networking, app-model, manifest, and completion-gate docs with the
  config-only evidence boundary.

## Why
FetchApp completion needs to compare the HTTP result against configured app
expectations instead of hard-coded test literals. This slice keeps that
derivation deterministic and host-tested while preserving the boundary that no
live networking, TLS, FetchApp execution, hosted .NET, or Kestrel behavior is
proved.

## Files changed
- `src/OS.Kernel/Net/FetchAppHttpCompletionExpectation.cs`
- `tests/OS.HostTests/FetchAppHttpCompletionPlanTests.cs`
- `docs/networking.md`
- `docs/app-model.md`
- `docs/manifest.md`
- `docs/completion-gates.md`
- `done/step692.md`

## Tests run
- `dotnet build .\tests\OS.HostTests\OS.HostTests.csproj --nologo`
- `dotnet run --project .\tests\OS.HostTests\OS.HostTests.csproj --no-build | Select-String -Pattern "fetch.http completion config|fetch.http completion formatter|host.tests|TEST_FAIL"`
- `dotnet run --project .\tests\OS.HostTests\OS.HostTests.csproj --no-build`

## Evidence
- Focused host-test build passed with 0 warnings and 0 errors after stopping a
  stale `OS.HostTests.exe` process that was locking the apphost output.
- The focused filtered host-test command exited 1 in the PowerShell pipeline,
  but it emitted no selected `TEST_FAIL` lines and did emit:
  - `TEST_PASS fetch.http completion config uses deterministic defaults`
  - `TEST_PASS fetch.http completion config flows from manifest`
  - `TEST_PASS fetch.http completion config rejects invalid status`
  - `TEST_PASS fetch.http completion config rejects empty marker`
  - `TEST_PASS fetch.http completion config rejects oversized marker`
  - `TEST_PASS fetch.http completion config requires marker when requested`
  - `TEST_PASS fetch.http completion formatter formats success marker`
  - `TEST_PASS fetch.http completion formatter reports short destination`
  - `TEST_PASS fetch.http completion formatter rejects default result`
- A subsequent unfiltered no-build host-test run exited 0 and emitted
  `TEST_PASS host.tests`.

## Failures
- The first build attempt failed because `OS.HostTests.exe` was locked by an
  older `OS.HostTests` process. The process was stopped and the required build
  command then passed.
- The filtered `Select-String` command exited 1 despite matching the focused
  FetchApp config/formatter markers and no selected `TEST_FAIL` lines. The
  unfiltered host-test command was run afterward and emitted
  `TEST_PASS host.tests`.

## Deferred work
- Wire the expectation planner into a real FetchApp/app-supervisor path only
  after manifest/package loading, FetchApp execution, DNS/socket/TCP/HTTP I/O,
  and app exit/log observation exist in the guest.
- Gate 8 remains failed until live virtio-net, DHCP, DNS, TCP, HTTP GET, and
  FetchApp execution have preserved QEMU/libvirt evidence.

