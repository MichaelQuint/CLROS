# Step 667 - CI smoke git diff check lane (#585)

## Summary

Added a `git diff --check` lane to `scripts/run-ci-smoke.ps1`. The dry-run
artifact plan and summary now include `ci.smoke.git.diffcheck`, and host tests
verify the planned command, log file, and marker coverage.

## Why

Issue #585: CI smoke should make whitespace checking part of the same planned
evidence bundle as the build, EFI preflight, QEMU dry-run, qcow2 intent, and
libvirt intent lanes. This keeps local and runner evidence aligned before a
future real CI execution issue.

## Files changed

- `scripts/run-ci-smoke.ps1`
  - Added the `ci.smoke.git.diffcheck` step that runs `git diff --check`.
  - Added the dry-run marker
    `TEST_PASS ci.smoke.dryrun includes git diff check`.
- `tests/OS.HostTests/CiSmokeScriptTests.cs`
  - Verified the artifact plan and summary include the git diff check command
    and `git-diff-check.log`.
- `docs/host-requirements.md`
  - Documented the CI smoke git diff check lane and dry-run marker.
- `docs/qemu.md`
  - Documented the dry-run marker and that the CI smoke wrapper captures the
    git diff check lane.
- `docs/completion-gates.md`
  - Updated Gate 12 CI workflow evidence wording to mention the git diff check
    lane.
- `done/step667.md`
  - This record.

## Tests run

- `dotnet build .\tests\OS.HostTests\OS.HostTests.csproj --nologo`
- `dotnet run --project .\tests\OS.HostTests\OS.HostTests.csproj --no-build`
- `dotnet build .\OS.slnx --nologo -m:1`
- `.\scripts\run-ci-smoke.ps1 -DryRun`
- `git diff --check`

## Evidence

- Focused host build passed with 0 warnings and 0 errors.
- Host tests passed and emitted:
  - `TEST_PASS ci.smoke.dryrun includes git diff check`
  - `TEST_PASS host.tests`
- Full solution build passed with 0 warnings and 0 errors.
- CI smoke dry-run passed and emitted:
  - `TEST_START ci.smoke.qemu_img.discovery`
  - `CI_SMOKE_QEMU_IMG_DISCOVERY requested=auto source=well-known available=true resolved=C:\Program Files\qemu\qemu-img.exe`
  - `CI_SMOKE_QCOW2_CONVERSION_INTENT mode=dry-run status=planned qemuImgRequired=false qemuImgAvailable=true`
  - `TEST_PASS ci.smoke.qemu_img.discovery`
  - `TEST_PASS ci.smoke.dryrun includes git diff check`
  - `TEST_PASS ci.smoke.dryrun`
- `git diff --check` reported no whitespace errors; Git only printed
  LF-to-CRLF normalization warnings for edited files.

## Failures

- None.

## Deferred work

- Execute the `.gitea/workflows/ci-smoke.yml` lane on a compatible Windows
  Gitea runner and attach the captured artifact/log evidence.
- Add real QEMU/qcow2 CI lanes only through explicit opt-in issues after dry-run
  runner evidence exists.

## Evidence boundary

This proves the local CI smoke wrapper plans and host-tests a `git diff --check`
lane. It does not prove repository CI execution, QEMU boot, qcow2 conversion,
libvirt real boot, cloud-provider import, hosted .NET, Minimal API, Kestrel, or
cloud readiness.
