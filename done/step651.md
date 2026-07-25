# Step 651 - Baseline Cloud Intent Artifact Summary

## Summary

Added a plan-only artifact summary path to `scripts/run-baseline-evidence.ps1`
and recorded QEMU diagnostics, qcow2, and libvirt intent artifacts in
`summary.json` with stable step names and host-test coverage.

## Why

Issue #7 needs a small cloud-tooling slice that improves artifact summaries
without claiming real QEMU/libvirt/cloud boot evidence. The previous baseline
script had qcow2 and libvirt lanes, but the summary only recorded generic step
metadata and did not expose diagnostics marker-preset intent or per-lane
serial/summary/domain/image artifacts.

## Files changed

- `scripts/run-baseline-evidence.ps1`
  - Adds `-PlanOnly` and optional `-LogRoot` for lightweight summary-shape
    validation without executing the baseline build/test/QEMU/libvirt loop.
  - Adds `schema=os.baseline.evidence.summary.v0` and `mode` fields.
  - Records `serialLog`, `summaryLog`, `image`, `diskFormat`, `domainXml`, and
    `markerPresets` fields where relevant.
  - Adds `baseline.qemu.diagnostics.dryrun` with `-MarkerPreset diagnostics`.
  - Keeps qcow2 and libvirt lanes as host-side dry-run/missing-tool intent
    unless the existing real qcow2 switch is used.
- `tests/OS.HostTests/CiSmokeScriptTests.cs`
  - Executes the new baseline `-PlanOnly` path and validates diagnostics,
    qcow2, and libvirt artifact-summary fields.
  - Emits `TEST_PASS baseline evidence script records artifact summary lanes`.
- `docs/qemu.md`
  - Documents the plan-only baseline summary boundary and test coverage.
- `docs/libvirt.md`
  - Documents libvirt intent fields recorded by the plan-only summary.
- `done/step651.md`
  - Records this evidence slice.

## Tests run

- `.\scripts\run-baseline-evidence.ps1 -PlanOnly -LogRoot artifacts\logs\baseline-plan-smoke -WorkloadProfile MultiMinimalApi`
- `dotnet build .\tests\OS.HostTests\OS.HostTests.csproj --nologo -m:1 /p:OSWorkloadProfile=SingleMinimalApi /p:UseSharedCompilation=false -nr:false`
- `dotnet run --project .\tests\OS.HostTests\OS.HostTests.csproj --no-build`
- `git diff --check -- .\scripts\run-baseline-evidence.ps1 .\tests\OS.HostTests\CiSmokeScriptTests.cs .\docs\qemu.md .\docs\libvirt.md .\done\step651.md`

## Evidence

- Plan-only baseline summary emitted:
  - `WORKLOAD_PROFILE MultiMinimalApi`
  - `TEST_PASS workload.profile.tooling propagates multi`
  - `TEST_START baseline.artifact_summary`
  - `SUMMARY artifacts\logs\baseline-plan-smoke\summary.json`
  - `TEST_PASS baseline.artifact_summary`
- `artifacts\logs\baseline-plan-smoke\summary.json` records:
  - `schema`: `os.baseline.evidence.summary.v0`
  - `mode`: `plan-only`
  - `baseline.qemu.diagnostics.dryrun` with `markerPresets=["diagnostics"]`,
    `serialLog`, and `summaryLog`
  - `baseline.qemu.qcow2.dryrun` with `image`, `diskFormat=qcow2`,
    `serialLog`, and `summaryLog`
  - `baseline.libvirt.dryrun` and `baseline.libvirt.missingtool` with
    `image`, `diskFormat=qcow2`, `serialLog`, `summaryLog`, and `domainXml`
- Host-test build passed with 0 warnings and 0 errors.
- Full host-test output reached:
  - `TEST_PASS baseline evidence script wires qcow2 and libvirt intent`
  - `TEST_PASS baseline evidence script records artifact summary lanes`
- `git diff --check` passed for the touched files with existing LF-to-CRLF
  warnings only.

## Failures

- `dotnet run --project .\tests\OS.HostTests\OS.HostTests.csproj --no-build`
  returned exit code 1 after the new baseline test passed. The visible output
  proceeded into the existing Kestrel smoke fixture area and ended after
  `TEST_START kestrel.smoke_app host healthz returns configured body` before
  the failure text could be captured in the interactive output.
- A logged rerun to `artifacts\logs\host-tests-issue7-rerun.log` was stopped
  after it stalled in existing QEMU script dry-run coverage; the rerun had not
  reached the new baseline test again when it was stopped.

## Deferred work

This is artifact-summary and intent validation only. It does not launch real
libvirt, start a libvirt domain, prove cloud-provider import, prove QEMU boot
beyond existing evidence, or change the kernel/runtime/storage/networking
implementation.
