# Step 662 - Baseline qemu-img discovery diagnostics (#580)

## Summary

Added baseline evidence diagnostics for `qemu-img` discovery and qcow2
conversion intent. `scripts/run-baseline-evidence.ps1` now emits a
machine-readable discovery marker before the qcow2 lane, records whether the
tool was found, and writes matching `qemuImgDiscovery` and
`qcow2ConversionIntent` objects into `summary.json`.

## Why

Issue #580: qcow2 conversion intent needed to distinguish missing host
`qemu-img` tooling from OS image-builder failures. The baseline plan should be
useful on hosts without `qemu-img` and should not fail unrelated raw QEMU,
host-test, or libvirt intent lanes.

## Files changed

- `scripts/run-baseline-evidence.ps1`
  - Added `qemu-img` discovery for configured paths, `QEMU_IMG`, PATH commands,
    and common install locations.
  - Added `BASELINE_QEMU_IMG_DISCOVERY` and
    `BASELINE_QCOW2_CONVERSION_INTENT` markers.
  - Added top-level and qcow2-step `qemuImgDiscovery` and
    `qcow2ConversionIntent` summary fields.
- `tests/OS.HostTests/CiSmokeScriptTests.cs`
  - Added plan-only tests for explicit missing and found fake `qemu-img` paths.
  - Extended the baseline artifact-summary test to require qcow2 conversion
    intent metadata.
- `docs/host-requirements.md`
  - Documented baseline `qemu-img` discovery markers and removed the completed
    next step.
- `docs/image-builder.md`, `docs/qemu.md`, `docs/completion-gates.md`
  - Documented the new summary fields, test coverage, and host-tool-only
    evidence boundary.
- `scripts/AGENTS.override.md`
  - Added local script guidance for baseline host-tool blocker reporting.
- `done/step662.md`
  - This record.

## Tests run

- `dotnet build .\tests\OS.HostTests\OS.HostTests.csproj --nologo`
- `dotnet run --project .\tests\OS.HostTests\OS.HostTests.csproj --no-build`
- `dotnet build .\OS.slnx --nologo -m:1`
- `.\scripts\run-baseline-evidence.ps1 -PlanOnly`
- `.\scripts\run-baseline-evidence.ps1 -PlanOnly -RunQcow2RealSmoke -QemuImg .\artifacts\logs\missing-qemu-img-for-plan.exe`
- `.\scripts\run-qemu-smoke.ps1 -DryRun`
- `git diff --check`

## Evidence

- Focused host build passed with 0 warnings and 0 errors.
- Host tests passed and emitted:
  - `TEST_PASS baseline evidence script records artifact summary lanes`
  - `TEST_PASS baseline evidence plan records missing qemu-img discovery`
  - `TEST_PASS baseline evidence plan records found qemu-img discovery`
  - `TEST_PASS host.tests`
- Full solution build passed with 0 warnings and 0 errors.
- Default baseline plan-only on this host emitted:
  - `TEST_START baseline.qemu_img.discovery`
  - `BASELINE_QEMU_IMG_DISCOVERY requested=auto source=well-known available=true resolved=C:\Program Files\qemu\qemu-img.exe`
  - `BASELINE_QCOW2_CONVERSION_INTENT mode=dry-run status=planned qemuImgRequired=false qemuImgAvailable=true`
  - `TEST_PASS baseline.qemu_img.discovery`
  - `TEST_PASS baseline.artifact_summary`
- Explicit missing real-qcow2 planning emitted:
  - `BASELINE_QEMU_IMG_DISCOVERY requested=.\artifacts\logs\missing-qemu-img-for-plan.exe source=configured available=false resolved=not_found`
  - `BASELINE_QCOW2_CONVERSION_INTENT mode=real status=blocked_by_missing_qemu_img qemuImgRequired=true qemuImgAvailable=false`
  - `TEST_PASS baseline.artifact_summary`
- QEMU dry-run passed with `TEST_PASS qemu.runner.dryrun`.
- `git diff --check` reported no whitespace errors; Git only printed existing
  LF-to-CRLF normalization warnings for edited files.

## Failures

- An initial host-test run failed because the new found-path assertion expected
  an absolute resolved fake `qemu-img` path, while baseline summaries correctly
  normalize repository-local paths to relative paths. The assertion now follows
  the existing summary path convention, and the rerun passed.

## Deferred work

- CI smoke summaries can get the same explicit qemu-img discovery fields in a
  future slice if issue state calls for it.
- This slice does not run real qcow2 conversion or real QEMU boot.

## Evidence boundary

This is host-tool discovery and planning evidence only. It does not prove
qcow2 conversion success, QEMU boot, libvirt real boot, cloud-provider import,
hosted .NET, Minimal API, or Kestrel.
