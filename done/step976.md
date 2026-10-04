# Step 976 - Keep M02 profile fixtures exact after reference-barrier integration

## Summary

Update eight M02 integration fixtures for the new checked-reference-barrier
profile conflicts. Preserve their original assertions, complete initializers
and production guards; assert each new conflict explicitly.

## Why

The complete run at `76bc39380cdadd4f57cf187fa87363957323754a` failed because
the checked-port fixture still expected 23 matching Error conditions, while
the project correctly contained 25. Seven neighboring selectors had the same
stale count. The repair changes only tests and documentation.

## Files changed

- `tests/OS.HostTests/M02CheckedPortIoFunctionPointerEfiIntegrationTests.cs`:
  require 25 conditions and both reciprocal reference-profile conflicts.
- `tests/OS.HostTests/M02SafeStubPageEfiEntryTests.cs`: require 12 conditions.
- `tests/OS.HostTests/M02Com1OwnedPortIoBackendEfiIntegrationTests.cs`:
  require 10 conditions.
- `tests/OS.HostTests/M02OwnedCom1ReadyEfiIntegrationTests.cs`: require 10.
- `tests/OS.HostTests/M02OwnedX64GdtEfiIntegrationTests.cs`: require 11.
- `tests/OS.HostTests/M02GuardedIstStacksEfiIntegrationTests.cs`: require 11.
- `tests/OS.HostTests/M02PanicSafeOwnedCom1EfiIntegrationTests.cs`: require 10.
- `tests/OS.HostTests/M02IdtrContinuedExecutionEfiIntegrationTests.cs`:
  require 13 exclusions and the exact new condition/diagnostic pair once.
- `docs/efi-build.md`: record the repair and evidence boundary.
- `done/step976.md`: this record, reserved with the repository helper at
  10:56:55.733137 UTC on 2026-10-04, native exit zero.

Each single-prefix selector also explicitly asserts its new reference conflict.
The 88 Compile items, 102 raw publication inputs and 38 probe properties are
unchanged. No runtime, thunk, ABI or production-profile source changed.

## Tests run

- Normal HostTests build: 10:52:47.322060-10:53:07.131028 UTC, native exit zero,
  zero warnings/errors and empty stderr.
- Isolated harness build: 10:53:07.135489-10:53:10.073445 UTC, same result.
- All eight original complete initializers:
  10:53:10.078054-10:55:35.150875 UTC, native exit zero, 40 starts, 41 passes,
  zero failures, two READY lines and one `host.tests` terminal. All 37 cases
  execute; SafeStub's five cases emit a combined envelope. Both real default
  and enabled IDTR NativeAOT publication checks remain included.
- `git diff --check`: passed for the eight-file source correction.

Exact commands and raw output are under
`artifacts/orchestration-20261001/issue537-m02-profile-count-repair/attempt1`.
The parent verified the complete ordered marker sequence and rehashed all 921
source/harness inputs and 14 run binaries against their unchanged snapshots.

## Evidence

- Focused result SHA-256:
  `35F3C7A0B9A452A18CED945D8231BFDFFA3D3975CD56BF16A7E973370D78907D`.
- Focused stdout SHA-256:
  `0D8D2DFD8D10C7146B3F7576287177AFE67D4F8F800B04144995C19CDF05553A`.
- Independent source review:
  `issue537-m02-profile-count-independent-review/review.md`, SHA-256
  `AAD1AF7EF6FBF52BCAB99192B9F5E53FBF71761B2D53DFB201AF45013FAC256C`.
- Independent complete-harness review in the same directory:
  `coverage-addendum.md`, SHA-256
  `7E33455BE53F98D41DE98484EADE7B01E1C9E212BAFCF3706AB8A308061F3704`.

## Failures

The earlier complete host run ended at 10:37:45.593671 UTC with native exit
3762504530 and the M02-003 exact-profile assertion failure. Its source, binaries
and HEAD were unchanged. Original streams remain under
`issue537-539-common-gate/run-76bc393`; this run is not accepted as passing.

The initial focused plan overcounted SafeStub's emitted marker pairs (45/46).
Source review corrected that estimate to 40/41 before execution; the initial
plan is preserved. No initializer or test case was removed. Direct invocation
of the step reservation helper was rejected by the shell execution policy;
the existing process-scoped recorder then ran it successfully. No machine
execution policy was changed.

## Deferred work

Commit this reviewed correction, then run the clean combined solution build and
full original suite. Fresh default/reference/ACPI guest captures and final
native/raw/disk reviews remain required before merging PR538 and closing its
five related issues. The focused pass does not replace those checks.

## Evidence boundary

These results establish host fixture correctness and native publication only.
They do not establish guest execution, real ExitBootServices, general GC,
hosted .NET/Kestrel, cloud readiness or production readiness.
