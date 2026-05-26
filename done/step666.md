# Step 666 - CI workflow contract host tests (#584)

## Summary

Added host-test coverage for `.gitea/workflows/ci-smoke.yml`. The new tests
read the workflow file as a repository contract and verify that the default CI
lane stays Windows-only, dry-run/preflight/plan-only, uploads smoke logs, and
does not accidentally gain real-smoke defaults.

## Why

Issue #584: the workflow skeleton added in #583 should be guarded by host tests
so future edits do not drift from the documented non-boot CI lane or imply real
QEMU/qcow2 evidence without a separate issue and captured run logs.

## Files changed

- `tests/OS.HostTests/CiWorkflowTests.cs`
  - Added module-initialized host tests for expected workflow commands,
    artifact upload wiring, and absence of real-smoke defaults.
- `docs/host-requirements.md`
  - Documented that host tests guard the workflow command contract.
- `docs/completion-gates.md`
  - Updated Gate 12 wording to describe the workflow skeleton as host-tested
    while keeping the gate status at `Fail`.
- `done/step666.md`
  - This record.

## Tests run

- `dotnet build .\tests\OS.HostTests\OS.HostTests.csproj --nologo`
- `dotnet run --project .\tests\OS.HostTests\OS.HostTests.csproj --no-build`
- `dotnet build .\OS.slnx --nologo -m:1`
- `git diff --check`

## Evidence

- Focused host build passed with 0 warnings and 0 errors.
- Host tests passed and emitted:
  - `TEST_PASS ci.workflow includes dry-run validation commands`
  - `TEST_PASS ci.workflow uploads smoke logs`
  - `TEST_PASS ci.workflow excludes real smoke defaults`
  - `TEST_PASS host.tests`
- Full solution build passed with 0 warnings and 0 errors.
- `git diff --check` reported no whitespace errors; Git only printed existing
  LF-to-CRLF normalization warnings for edited files.

## Failures

- None.

## Deferred work

- Run `.gitea/workflows/ci-smoke.yml` on a compatible Windows Gitea runner and
  post captured logs/artifact links as CI evidence.
- Add real QEMU/qcow2 CI lanes only through explicit opt-in issues after the
  dry-run lane has executed-run evidence.

## Evidence boundary

This is host-test coverage for the workflow file only. It does not prove
repository CI execution, QEMU boot, qcow2 conversion, libvirt real boot,
cloud-provider import, hosted .NET, Minimal API, Kestrel, or cloud readiness.
