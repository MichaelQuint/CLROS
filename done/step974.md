# Step 974 - Explain disabled-profile geometry fields

## Summary

Add a local CS0649 annotation around the seven `CheckedReferenceGeometry`
fields. The enabled profile and mapped core tests assign them; the normal
solution build compiles the disabled profile and cannot observe those writes.

## Why

The combined build at `825fcd8dfe0d04b0c76b164b601da3d3f0dbbb48`
succeeded with seven warnings. The annotation records why these fields remain
valid without altering field order, types, generated operations, or probe policy.

## Files changed

- `src/OS/Runtime/CheckedReferenceBarrierAdapter.cs`: scope the warning annotation
  to the seven fields.
- `tests/OS.HostTests/M02IdtrContinuedExecutionEfiIntegrationTests.cs`,
  `M02OwnedIdtGatePopulationEfiIntegrationTests.cs`, and
  `M02Ud2VectorStateEfiIntegrationTests.cs`: regenerate the exact raw-source hash
  for the changed source bytes. Membership and all other assertions remain intact.
- `done/step974.md`: this evidence record.

## Tests run

The preceding combined solution build ran from 08:47:02.811475 to
08:47:26.994640 UTC on 2026-10-04, exited zero, and left source and HEAD
unchanged. This follow-up requires a new clean build and complete host suite
at its committed HEAD; neither is claimed here before execution.

## Evidence

- Build: `artifacts/orchestration-20261001/issue537-539-common-gate/build-825fcd8`.
  Standard output SHA-256:
  `CBBFBD0E26D986E485E6E32DF13373A99B120E3C175662432CFF725EDB7C595C`.
- Raw-source read-back: all 102 source rows were rehashed, with only the adapter
  changed. New aggregate:
  `c12f9888be8d5644dbe16a27fc744e226d3ec215c88f4282bb6a2357abc83d32`.
  Local records: `artifacts/orchestration-20261001/issue537-539-warning-fix`.
- Reserved with `scripts/new-done-step.ps1 -Reserve`, native exit zero at
  08:55:52.261853 UTC. Earlier step records are unchanged.

## Failures

No failed build is hidden by this change. The prior successful build's seven
CS0649 warnings are retained in its original log.

## Deferred work

Complete common validation, actual native review, fresh guest smoke evidence,
and the PR/issue completion transaction remain pending.

## Evidence boundary

This is a warning and exact-source-pin maintenance change. It adds no runtime,
guest, stack-canary, ExitBootServices, hosted .NET, or cloud-readiness claim.
