# Step 701 - CI cctor-preflight Dry-Run Lane

## Summary

Added a CI smoke dry-run lane for the `cctor-preflight` QEMU marker preset.

## Why

Issue #625 needed the CI artifact plan and summary to carry the future
NativeAOT class-constructor runner preflight marker preset alongside the other
dry-run marker-preset lanes. This proves command shape and artifact metadata
only; it does not prove class-constructor execution or runtime-helper
completion.

## Files changed

- `scripts/run-ci-smoke.ps1`
- `tests/OS.HostTests/CiSmokeScriptTests.cs`
- `docs/qemu.md`
- `docs/completion-gates.md`
- `done/step701.md`

## Tests run

- `.\scripts\run-ci-smoke.ps1 -DryRun -LogRoot artifacts\logs\ci-smoke-issue625-check`
- `dotnet build .\tests\OS.HostTests\OS.HostTests.csproj --nologo`
- `dotnet run --project .\tests\OS.HostTests\OS.HostTests.csproj --no-build | Select-String -Pattern "ci.smoke|cctor-preflight|host.tests|TEST_FAIL"`
- `git diff --check -- scripts/run-ci-smoke.ps1 tests/OS.HostTests/CiSmokeScriptTests.cs docs/qemu.md docs/completion-gates.md done`

## Evidence

- CI smoke dry-run exited 0 and emitted:
  - `TEST_PASS ci.smoke.dryrun includes cctor-preflight marker preset`
  - `TEST_PASS ci.smoke.dryrun`
- `artifacts/logs/ci-smoke-issue625-check/ci-smoke.artifacts.json` and
  `artifacts/logs/ci-smoke-issue625-check/ci-smoke.summary.json` both record
  `ci.smoke.cctor_preflight.dryrun` with:
  - `-MarkerPreset cctor-preflight`
  - `qemu-cctor-preflight-dry-run.log`
  - `qemu-cctor-preflight-serial.log`
  - `qemu-cctor-preflight.summary.log`
  - `markerPresets` containing `cctor-preflight`
- Host-test build passed with 0 warnings and 0 errors.
- Filtered host-test output included:
  - `TEST_PASS ci.smoke.dryrun includes cctor-preflight marker preset`
  - `TEST_PASS ci.smoke returns nonzero on failed command`
  - `TEST_PASS host.tests`
- `git diff --check` passed for the owned paths.

## Failures

None. `git diff --check` printed only existing line-ending normalization
warnings for edited text files.

## Deferred work

- This is dry-run CI artifact-contract evidence only.
- No live QEMU runtime-helper, class-constructor execution, hosted .NET,
  Minimal API, or Kestrel behavior is claimed.

