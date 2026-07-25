# Step 665 - Dry-run CI smoke workflow skeleton (#583)

## Summary

Added the first repository CI workflow skeleton at
`.gitea/workflows/ci-smoke.yml`. The workflow targets a Windows runner and runs
the existing non-boot validation lane: .NET SDK info, solution build, EFI
preflight, CI smoke dry-run, baseline plan-only evidence, and `git diff
--check`, with best-effort log artifact upload.

## Why

Issue #583: Gate 12 needed an initial repository workflow file after the CI
agent prerequisite documentation was added. The workflow is intentionally
dry-run/preflight/plan-only by default so it does not claim real QEMU boot,
qcow2 conversion, libvirt real boot, hosted .NET, Minimal API, or Kestrel.

## Files changed

- `.gitea/workflows/ci-smoke.yml`
  - Added a Windows dry-run smoke job using `actions/checkout@v4`.
  - Runs `dotnet --info`, `dotnet build .\OS.slnx --nologo -m:1`,
    `.\scripts\build-efi-shell.ps1 -PreflightOnly`,
    `.\scripts\run-ci-smoke.ps1 -DryRun`,
    `.\scripts\run-baseline-evidence.ps1 -PlanOnly`, and `git diff --check`.
  - Attempts to upload `artifacts/logs/**` with `actions/upload-artifact@v4`.
- `docs/host-requirements.md`
  - Documented the workflow file, Windows runner label, artifact upload
    expectation, and dry-run default boundary.
- `docs/completion-gates.md`
  - Updated Gate 12 to record that a workflow skeleton exists while keeping the
    gate status at `Fail` until executed CI evidence exists.
- `done/step665.md`
  - This record.

## Tests run

- `.\scripts\run-ci-smoke.ps1 -DryRun`
- `git diff --check`

## Evidence

- CI smoke dry-run emitted:
  - `TEST_START ci.smoke.qemu_img.discovery`
  - `CI_SMOKE_QEMU_IMG_DISCOVERY requested=auto source=well-known available=true resolved=C:\Program Files\qemu\qemu-img.exe`
  - `CI_SMOKE_QCOW2_CONVERSION_INTENT mode=dry-run status=planned qemuImgRequired=false qemuImgAvailable=true`
  - `TEST_PASS ci.smoke.qemu_img.discovery`
  - `TEST_PASS ci.smoke.dryrun`
- `git diff --check` reported no whitespace errors; Git only printed existing
  LF-to-CRLF normalization warnings for edited files.

## Failures

- None locally.
- The workflow has not been executed by Gitea Actions yet.

## Deferred work

- Run `.gitea/workflows/ci-smoke.yml` on a compatible Windows Gitea runner and
  post the captured logs/artifact links as CI evidence.
- Add a separate opt-in real QEMU/qcow2 CI lane only after the dry-run lane has
  clean runner evidence.

## Evidence boundary

This is a workflow-file and local dry-run validation slice only. It does not
prove repository CI execution, QEMU boot, qcow2 conversion, libvirt real boot,
cloud-provider import, hosted .NET, Minimal API, Kestrel, or cloud readiness.
