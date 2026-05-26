# Step 668 - CI smoke execute-summary git diff coverage (#586)

## Summary

Extended the CI smoke executed failure-path host test so it verifies the
`ci.smoke.git.diffcheck` result in `ci-smoke.summary.json`. The test now
requires the git diff check lane to report `exitCode` 0 and log
`TEST_PASS ci.smoke.git.diffcheck` even when earlier fake build and host-test
commands fail.

## Why

Issue #586: after adding the git diff check lane to `scripts/run-ci-smoke.ps1`,
the dry-run shape was covered but the execute-mode failure summary still only
asserted the older lanes. This closes that coverage gap without claiming any
runner or real boot evidence.

## Files changed

- `tests/OS.HostTests/CiSmokeScriptTests.cs`
  - Added execute-summary assertions for `ci.smoke.git.diffcheck`.
- `docs/qemu.md`
  - Documented that the executed CI failure-path host test now verifies the
    passing git diff check lane.
- `done/step668.md`
  - This record.

## Tests run

- `dotnet build .\tests\OS.HostTests\OS.HostTests.csproj --nologo`
- `dotnet run --project .\tests\OS.HostTests\OS.HostTests.csproj --no-build`
- `dotnet build .\OS.slnx --nologo -m:1`
- `git diff --check`

## Evidence

- Focused host build passed with 0 warnings and 0 errors.
- Host tests passed and emitted:
  - `TEST_PASS ci.smoke returns nonzero on failed command`
  - `TEST_PASS host.tests`
- Full solution build passed with 0 warnings and 0 errors.
- `git diff --check` reported no whitespace errors; Git only printed
  LF-to-CRLF normalization warnings for edited files.

## Failures

- None.

## Deferred work

- Execute the `.gitea/workflows/ci-smoke.yml` lane on a compatible Windows
  Gitea runner and attach captured log/artifact evidence.

## Evidence boundary

This is host-test coverage for CI smoke execute-summary shape only. It does not
prove repository CI execution, QEMU boot, qcow2 conversion, libvirt real boot,
cloud-provider import, hosted .NET, Minimal API, Kestrel, or cloud readiness.
