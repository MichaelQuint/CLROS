# Step 699 - FetchApp HTTP Request Endpoint Config Parser

## Summary
- Added `OS.Net.FetchAppHttpRequestEndpoint`, a status-coded, caller-buffer
  FetchApp HTTP endpoint parser for already-parsed native manifest config.
- The parser accepts `FETCH_HTTP_URL` or explicit `FETCH_HTTP_HOST`,
  `FETCH_HTTP_PORT`, and `FETCH_HTTP_PATH`, applies deterministic
  `example.com:80/` defaults, and copies host/path bytes into caller-owned
  buffers.
- Added focused host tests for defaults, explicit config, invalid scheme,
  invalid port, missing host, and oversized fields.
- Updated focused networking, app-model, manifest, and completion-gate docs.

## Why
FetchApp completion planning already had expected-status/body-marker config.
This slice adds the matching request endpoint metadata boundary before any live
DNS, TCP, or HTTP I/O exists.

## Files changed
- `src/OS.Kernel/Net/FetchAppHttpRequestEndpoint.cs`
- `tests/OS.HostTests/FetchAppHttpCompletionPlanTests.cs`
- `docs/networking.md`
- `docs/app-model.md`
- `docs/manifest.md`
- `docs/completion-gates.md`
- `done/step699.md`

## Tests run
- `dotnet build .\tests\OS.HostTests\OS.HostTests.csproj --nologo`
- `dotnet run --project .\tests\OS.HostTests\OS.HostTests.csproj --no-build | Select-String -Pattern "fetch.http request endpoint|host.tests|TEST_FAIL"`
- `git diff --check -- src/OS.Kernel/Net tests/OS.HostTests/FetchAppHttpCompletionPlanTests.cs docs/networking.md docs/app-model.md docs/manifest.md docs/completion-gates.md done`

## Evidence
- Focused host-test build passed with 0 warnings and 0 errors.
- Parent full host-test evidence is preserved in
  `artifacts/logs/parent-hosttests-20260525-050128.log` and exited 0 with
  `TEST_PASS host.tests`.
- Filtered host-test run exited 0 and emitted:
  - `TEST_PASS fetch.http request endpoint defaults`
  - `TEST_PASS fetch.http request endpoint explicit config`
  - `TEST_PASS fetch.http request endpoint invalid scheme`
  - `TEST_PASS fetch.http request endpoint invalid port`
  - `TEST_PASS fetch.http request endpoint missing host`
  - `TEST_PASS fetch.http request endpoint oversized fields`
  - `TEST_PASS host.tests`
- The filtered host-test output contained no selected `TEST_FAIL` lines.
- `git diff --check` exited 0 for the focused paths, with line-ending
  warnings on touched docs only.

## Failures
- A repeated direct filtered host-test command initially left stale
  `OS.HostTests` processes that locked test output DLLs on Windows. Those stale
  test processes were stopped, the focused build was rerun successfully, and a
  bounded rerun emitted the requested endpoint and `host.tests` markers.

## Deferred work
- Wire the endpoint metadata into a future FetchApp HTTP GET execution path only
  after live manifest/package loading, DNS/socket/TCP/HTTP I/O, and FetchApp
  app execution exist in the guest.
- Gate 8 remains failed until live virtio-net, DHCP, DNS, TCP, HTTP GET, and
  FetchApp execution have preserved QEMU/libvirt evidence.

## Evidence boundary
- This is host-tested parser/config evidence over already-parsed manifests and
  caller-owned buffers only. It does not perform DNS, open sockets, send HTTP
  requests, implement TLS/HTTPS, execute FetchApp, launch hosted .NET, or prove
  Kestrel behavior.
