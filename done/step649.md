# Step 649 - Package File Staging Range Overflow Guard

## Summary

Added explicit source-range overflow metadata to the package-file staging
contract and a host-test marker for rejecting staged package-file requests when
the opened package entry's source offset plus byte count overflows.

## Why

The package-file staging planner already rejected empty files, short
destinations, open failures, and invalid package ranges. This step makes the
overflow subcase visible in the status metadata so future boot/app-model
readiness code can distinguish an arithmetic overflow from an ordinary
out-of-bounds package range while preserving the existing
`PackageRangeInvalid` status for compatibility.

## Files changed

- `src/OS.Kernel/Storage/OsPackageFileStagingPlan.cs`
  - Adds `SourceRangeOverflow` to `OsPackageFileStagingResult`.
  - Validates source offset plus length before converting to slice indexes.
  - Preserves no-live-storage/no-PAL/no-map/no-execute boundaries.
- `tests/OS.HostTests/StoragePackageFileStagingPlanTests.cs`
  - Adds `TEST_PASS storage.package staging rejects source range overflow`.
  - Verifies overflow rejection does not modify caller-owned destination bytes.
- `docs/cstorage.md`
  - Documents source-range overflow metadata as host-tested storage evidence.
- `docs/app-model.md`
  - Documents the app-model-facing package staging overflow boundary.
- `done/step649.md`
  - Records this evidence boundary and validation result.

## Tests run

- `dotnet build .\tests\OS.HostTests\OS.HostTests.csproj --nologo -m:1 /p:OSWorkloadProfile=SingleMinimalApi /p:UseSharedCompilation=false -nr:false`
- `dotnet run --project .\tests\OS.HostTests\OS.HostTests.csproj --no-build`
- Temporary focused validation project under `artifacts/step649-focus`
  executed with `dotnet run --project .\artifacts\step649-focus\Step649Focus.csproj --nologo`
  and was removed after the run.
- `git diff --check -- src/OS.Kernel/Storage/OsPackageFileStagingPlan.cs tests/OS.HostTests/StoragePackageFileStagingPlanTests.cs docs/cstorage.md docs/app-model.md done/step649.md`
- `git diff --check --no-index -- /dev/null src/OS.Kernel/Storage/OsPackageFileStagingPlan.cs`
- `git diff --check --no-index -- /dev/null tests/OS.HostTests/StoragePackageFileStagingPlanTests.cs`
- `git diff --check --no-index -- /dev/null done/step649.md`

## Evidence

- New expected host-test marker:
  - `TEST_PASS storage.package staging rejects source range overflow`
- Focused temporary validation over the freshly built `OS.Kernel` emitted:
  - `TEST_START storage.package staging rejects source range overflow`
  - `TEST_PASS storage.package staging rejects source range overflow`
- The requested host-test build passed with 0 warnings and 0 errors.
- `git diff --check` over the touched tracked files passed with LF-to-CRLF
  warnings only for existing docs line-ending behavior.
- `git diff --check --no-index` over the touched untracked code/test/done files
  reported LF-to-CRLF warnings only.

## Failures

- The full host-test run failed in unrelated QEMU runner script coverage before
  the storage package-staging module initialized:
  - `TEST_FAIL qemu smoke script dry-run propagates workload profile`
  - `TEST_FAIL qemu smoke script dry-run propagates qcow2 disk format TimeoutException: run-qemu-smoke.ps1 did not exit within 120 seconds.`
  - `TEST_FAIL qemu.runner.tests failures=2`

## Deferred work

This is host-tested package-staging validation only. It does not add live
virtio-blk reads, boot-time package loading, PAL file exports, boot-owned
staging allocation, executable mapping, page-permission changes, relocation,
native app execution, hosted .NET, Minimal API, or Kestrel evidence.
