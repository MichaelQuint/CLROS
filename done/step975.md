# Step 975 - Match reference readiness at a complete token boundary

## Summary

Correct the configured reference READY marker and exercise its actual runner
matching behavior in the existing full reference-EFI transcript group.

## Why

`SerialMarkerStateMachine` matches an exact marker or a marker followed by a
space. The previous `CLROS_M04_010_NATIVE_READY allocations=6 usedBytes=`
anchor could not match the emitted `usedBytes=576` field. A valid guest stream
would fail required, ordered and unique checks. Ending the anchor at
`allocations=6` fixes that mismatch while preserving full numeric validation.

## Files changed

- `scripts/run-qemu-smoke.ps1`: correct the configured READY anchor.
- `tests/OS.HostTests/M04CheckedReferenceBarrierEfiIntegrationTests.cs`: feed
  all four actual dry-run vectors into the production `Feed`/`Complete` matcher;
  assert full 70-record acceptance and old-anchor, missing, duplicate and early
  READY rejection within the original five-group initializer.
- `docs/efi-build.md`: describe the marker contract and evidence boundary.
- `done/step975.md`: this record, reserved with `new-done-step.ps1 -Reserve`
  at 09:43:56.898039 UTC on 2026-10-04, native exit zero.

The raw publication inventory excludes both changed implementation/test files.
All 102 inputs and their aggregate remain unchanged:
`c12f9888be8d5644dbe16a27fc744e226d3ec215c88f4282bb6a2357abc83d32`.
The three M02 aggregate fixtures therefore need no edit.

## Tests run

- Normal HostTests build: 09:38:18.737816-09:38:28.977244 UTC, native exit zero,
  zero warnings/errors, empty stderr.
- Existing isolated whole-initializer harness build:
  09:38:28.981911-09:38:31.530884 UTC, native exit zero, zero warnings/errors.
- Whole `M04CheckedReferenceBarrierEfiIntegrationTests` initializer:
  09:38:31.535278-09:43:06.010928 UTC, native exit zero, five starts, six passes
  including `host.tests`, zero failures, empty stderr. This includes the real
  default and reference-enabled no-stdlib NativeAOT publication checks.
- `git diff --check`: passed for the source correction.

Commands, raw streams and launch/end identities are retained under
`artifacts/orchestration-20261001/issue537-reference-ready-repair-execution/attempt1`.
All 918 source/harness inputs and all 14 run binaries were unchanged. The parent
independently checked their bytes and the complete five-group marker sequence.

## Evidence

- Focused result SHA-256:
  `241AFE2C79F0C28B7D0D7284775110928456DB4266B0BBBEC689FD4AE7EF533A`.
- Whole-initializer stdout SHA-256:
  `8C5C86F5A17204DDAAD1E2FFE2355FCAE0492227235DBD4705565D8BE1717B78`.
- Source checkpoint: `artifacts/orchestration-20261001/issue537-reference-ready-repair`.
- Independent source review:
  `artifacts/orchestration-20261001/issue537-reference-ready-independent-review/review.md`,
  SHA-256 `67623A7E1626FFCE713701E0EE0151C3CC2AE10698BBBF8F04C2068754D4AB0A`.

## Failures

The earlier complete host run at `79dd765a4d31f8b3d6f34b50170099af265b8e34`
was intentionally stopped after this production mismatch was found during
review. Its native exit was one; source and binaries remained unchanged.
The original logs remain under `issue537-539-common-gate/run-79dd765` and are
not accepted as a passing gate. No guest result was fabricated or reclassified.

An initial preparation assumption incorrectly expected smoke to belong to the
102-input publication inventory. That assertion failure is preserved; actual
inventory read-back established that no aggregate update was needed. Two
review-tool selector mistakes were also preserved and corrected without any
product change. The final builds and complete focused run have no failures.

## Deferred work

A clean build and complete host suite on the new committed head, final native
artifact association, fresh default/reference/ACPI QEMU captures and their
reviews remain required before merging PR538 and closing the related issues.

## Evidence boundary

The new matcher controls use synthetic transcripts. Native publication is not
guest execution. This correction proves no real ExitBootServices, general GC,
hosted .NET, Kestrel, cloud readiness or production readiness.
