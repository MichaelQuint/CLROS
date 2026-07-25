# Step 663 - CI smoke qemu-img discovery diagnostics (#581)

## Summary

Added CI smoke diagnostics for `qemu-img` discovery and qcow2 conversion
intent. `scripts/run-ci-smoke.ps1` now emits machine-readable discovery markers
before dry-run or execution, records whether the conversion tool is available,
and writes matching `qemuImgDiscovery` and `qcow2ConversionIntent` objects into
`ci-smoke.artifacts.json`, `ci-smoke.summary.json`, and the qcow2 smoke step.

## Why

Issue #581: CI smoke planning needed the same host-prerequisite separation as
baseline evidence. Missing `qemu-img` should be visible as a qcow2 real-smoke
host-tool blocker without being confused with raw image generation, raw QEMU
dry-run, libvirt intent, or OS failures.

## Files changed

- `scripts/run-ci-smoke.ps1`
  - Added configured-path, `QEMU_IMG`, PATH, and well-known-location discovery
    for `qemu-img`.
  - Added `CI_SMOKE_QEMU_IMG_DISCOVERY` and
    `CI_SMOKE_QCOW2_CONVERSION_INTENT` markers.
  - Added top-level and qcow2-step `qemuImgDiscovery` and
    `qcow2ConversionIntent` fields to dry-run plans and execution summaries.
- `tests/OS.HostTests/CiSmokeScriptTests.cs`
  - Added dry-run coverage for explicit missing and found fake `qemu-img`
    paths.
  - Extended existing qcow2 CI smoke intent checks to require conversion intent
    metadata.
- `docs/qemu.md`, `docs/image-builder.md`, `docs/host-requirements.md`,
  `docs/completion-gates.md`
  - Documented CI smoke discovery markers, summary fields, tests, and evidence
    boundary.
- `scripts/AGENTS.override.md`
  - Updated script-local guidance so baseline and CI evidence planning both
    preserve host-tool blocker boundaries.
- `done/step663.md`
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
  - `TEST_PASS ci.smoke.dryrun includes qcow2 smoke intent`
  - `TEST_PASS ci.smoke.dryrun records missing qemu-img discovery`
  - `TEST_PASS ci.smoke.dryrun records found qemu-img discovery`
  - `TEST_PASS host.tests`
- Full solution build passed with 0 warnings and 0 errors.
- CI smoke dry-run on this host emitted:
  - `TEST_START ci.smoke.qemu_img.discovery`
  - `CI_SMOKE_QEMU_IMG_DISCOVERY requested=auto source=well-known available=true resolved=C:\Program Files\qemu\qemu-img.exe`
  - `CI_SMOKE_QCOW2_CONVERSION_INTENT mode=dry-run status=planned qemuImgRequired=false qemuImgAvailable=true`
  - `TEST_PASS ci.smoke.qemu_img.discovery`
  - `TEST_PASS ci.smoke.dryrun`
- `git diff --check` reported no whitespace errors; Git only printed existing
  LF-to-CRLF normalization warnings for edited files.

## Failures

- None.

## Deferred work

- This slice does not run real qcow2 conversion or real QEMU boot.
- Repository CI workflow files are still not implemented.

## Evidence boundary

This is host-tool discovery and planning evidence only. It does not prove
qcow2 conversion success, QEMU boot, libvirt real boot, cloud-provider import,
hosted .NET, Minimal API, or Kestrel.
