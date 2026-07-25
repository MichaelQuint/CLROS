# Step 712 - CI FetchApp Endpoint Marker Dry-Run Lane

## Summary
Added a CI smoke artifact lane for the existing QEMU `fetch-http-endpoint`
marker preset. The lane is separate from the existing `fetch-http` completion
lane and writes distinct log, serial-log, summary-log, artifact-plan, and
summary entries under the CI smoke evidence directory.

## Why
The FetchApp endpoint marker already existed at the QEMU runner/script layer,
but CI smoke planning did not preserve a dedicated lane for it. This step makes
future endpoint-preflight smoke images visible in CI artifacts without claiming
live FetchApp execution, networking, HTTP GET, hosted .NET, Minimal API, or
Kestrel behavior.

## Files changed
- `scripts/run-ci-smoke.ps1`
- `tests/OS.HostTests/CiSmokeScriptTests.cs`
- `docs/qemu.md`
- `docs/completion-gates.md`
- `done/step712.md`

## Tests run
- `dotnet build .\tests\OS.HostTests\OS.HostTests.csproj --nologo`
- `.\scripts\run-ci-smoke.ps1 -DryRun`
- `.\scripts\run-ci-smoke.ps1 -LogRoot artifacts\logs\ci-smoke-issue631-execute -DotNet artifacts\logs\ci-smoke-issue631-fake-dotnet\dotnet-fails.cmd`

## Evidence
- CI dry-run emitted:
  - `TEST_PASS ci.smoke.dryrun includes fetch-http-endpoint marker preset`
  - `CI_SMOKE_ARTIFACT_PLAN artifacts\logs\ci-smoke-20260525-055634\ci-smoke.artifacts.json`
  - `SUMMARY artifacts\logs\ci-smoke-20260525-055634\ci-smoke.summary.json`
- The dry-run artifact plan and summary contain a distinct
  `ci.smoke.fetch_http_endpoint.dryrun` step.
- The intentionally fake-dotnet CI execution left the QEMU dry-run lanes
  runnable and wrote
  `artifacts\logs\ci-smoke-issue631-execute\qemu-fetch-http-endpoint-dry-run.log`.
  That log emitted:
  - `TEST_START ci.smoke.fetch_http_endpoint.dryrun`
  - `QEMU_MARKER_PRESETS fetch-http-endpoint`
  - `MARKER_PRESETS fetch-http-endpoint`
  - `FETCHAPP_HTTP_ENDPOINT_READY` in the required marker list
  - `TEST_PASS ci.smoke.fetch_http_endpoint.dryrun`

## Failures
- `dotnet build .\tests\OS.HostTests\OS.HostTests.csproj --nologo` failed
  before issue #631 host-test evidence could run because unrelated
  `tests/OS.HostTests/NativeManifestParserTests.cs` references missing
  `TestEnvironmentDiagnostic*` methods. This step did not edit that file.
- The fake-dotnet CI execution intentionally returned nonzero with
  `TEST_FAIL ci.smoke failures=2` because the build and host-test lanes used a
  fake `dotnet` command that exits 42. The endpoint QEMU dry-run lane itself
  passed.

## Deferred work
- Re-run the normal host-test build and
  `dotnet run --project .\tests\OS.HostTests\OS.HostTests.csproj --no-build -- ci.smoke`
  after the unrelated NativeManifestParserTests compile blocker is fixed.
- This is CI artifact-contract and required-marker wiring only. It is not real
  QEMU boot proof and does not prove live FetchApp execution, manifest loading
  from disk, virtio-net, DHCP, DNS, TCP, HTTP GET, hosted .NET, Minimal API, or
  Kestrel.

