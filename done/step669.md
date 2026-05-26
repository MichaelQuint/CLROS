# Step 669 - CI workflow dry-run metadata guard (#587)

## Summary

Added a host test that guards the dry-run CI smoke workflow metadata. The test
now verifies the workflow keeps push, pull request, and manual dispatch
triggers, uses PowerShell, keeps the 45 minute timeout, and uploads smoke logs
with `if: always()`.

## Why

Issue #587: the workflow command contract was already host-tested, but its
metadata was only implicit. This keeps the first CI lane constrained to the
documented Windows dry-run/preflight/plan-only behavior.

## Files changed

- `tests/OS.HostTests/CiWorkflowTests.cs`
  - Added `ci.workflow preserves dry-run metadata`.
- `docs/host-requirements.md`
  - Documented that host tests guard triggers, shell, timeout, upload-on-failure
    behavior, commands, and absence of real-smoke flags.
- `done/step669.md`
  - This record.

## Tests run

- `dotnet build .\tests\OS.HostTests\OS.HostTests.csproj --nologo`
- `dotnet run --project .\tests\OS.HostTests\OS.HostTests.csproj --no-build`
- `dotnet build .\OS.slnx --nologo -m:1`
- `git diff --check`

## Evidence

- Focused host build passed with 0 warnings and 0 errors.
- Host tests passed and emitted:
  - `TEST_PASS ci.workflow preserves dry-run metadata`
  - `TEST_PASS host.tests`
- Full solution build passed with 0 warnings and 0 errors.
- `git diff --check` reported no whitespace errors; Git only printed
  LF-to-CRLF normalization warnings for edited files.

## Failures

- None.

## Deferred work

- Execute `.gitea/workflows/ci-smoke.yml` on a compatible Windows Gitea runner
  and attach captured log/artifact evidence.

## Evidence boundary

This is host-test coverage for workflow metadata only. It does not prove
repository CI execution, QEMU boot, qcow2 conversion, libvirt real boot,
cloud-provider import, hosted .NET, Minimal API, Kestrel, or cloud readiness.
