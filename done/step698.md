# Step 698 - CI app-exit/fetch-http dry-run lanes

## Summary

Added opt-in CI smoke artifact-contract lanes for the QEMU `app-exit` and
`fetch-http` marker presets. The lanes record separate command, log,
serial-log, summary-log, and marker-preset metadata in the CI smoke artifact
plan and summary.

## Why

Issue #622 needs CI dry-run coverage for the future app-exit observer and
FetchApp HTTP completion marker-preset contracts without claiming real QEMU,
guest app, live networking, HTTP GET, hosted .NET, Minimal API, or Kestrel
evidence.

## Files changed

- `scripts/run-ci-smoke.ps1`
- `tests/OS.HostTests/CiSmokeScriptTests.cs`
- `docs/qemu.md`
- `docs/completion-gates.md`
- `done/step698.md`

## Tests run

- `.\scripts\run-ci-smoke.ps1 -DryRun -LogRoot artifacts\logs\ci-smoke-issue622-check`
- `dotnet build .\tests\OS.HostTests\OS.HostTests.csproj --nologo`
- `dotnet run --project .\tests\OS.HostTests\OS.HostTests.csproj --no-build | Select-String -Pattern "ci.smoke|app-exit|fetch-http|host.tests|TEST_FAIL"`

## Evidence

- CI smoke dry-run emitted:
  - `TEST_PASS ci.smoke.dryrun includes app-exit marker preset`
  - `TEST_PASS ci.smoke.dryrun includes fetch-http marker preset`
- Parent dry-run `.\scripts\run-ci-smoke.ps1 -DryRun -LogRoot artifacts\logs\ci-smoke-parent-619-622`
  emitted:
  - `CI_SMOKE_ARTIFACT_PLAN artifacts\logs\ci-smoke-parent-619-622\ci-smoke.artifacts.json`
  - `SUMMARY artifacts\logs\ci-smoke-parent-619-622\ci-smoke.summary.json`
  - `TEST_PASS ci.smoke.dryrun includes app-exit marker preset`
  - `TEST_PASS ci.smoke.dryrun includes fetch-http marker preset`
  - `TEST_PASS ci.smoke.dryrun`
- Host test filtered output emitted:
  - `TEST_PASS ci.smoke.dryrun includes app-exit marker preset`
  - `TEST_PASS ci.smoke.dryrun includes fetch-http marker preset`
  - `TEST_PASS ci.smoke returns nonzero on failed command`
  - `TEST_PASS host.tests`
- Parent full host-test evidence is preserved in
  `artifacts/logs/parent-hosttests-20260525-050128.log` and exited 0 with
  `TEST_PASS host.tests`.
- Build passed with 0 warnings and 0 errors.

## Failures

None in the focused commands above.

## Deferred work

- These lanes are dry-run artifact-contract evidence only.
- Real app-exit observation still needs a guest image that emits
  `APP_EXIT_OBSERVER_READY` from the tested boot path.
- Real FetchApp HTTP completion still needs live app execution, networking,
  TCP/HTTP behavior, and a guest image that emits
  `FETCH_APP_HTTP_COMPLETION_READY` from the tested boot path.
