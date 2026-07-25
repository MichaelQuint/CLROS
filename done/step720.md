# Step 720 - Shared storage/app helpers and PAL export smoke preset

## Summary

Closed the next four small CloudReady backlog slices:

- #663 moved `GptParser` signature and zero-GUID checks onto shared
  `OS.Std.MemoryExtensions.SequenceEqual`.
- #664 moved `GptPartitionTableParser` signature and zero-GUID checks onto the
  same shared helper.
- #665 moved native app launch-plan dot-segment validation onto shared memory
  equality helpers.
- #666 added a `pal-export` QEMU marker preset and CI dry-run lane for
  `PAL_EXPORT_CATALOG_READY`.

## Why

The storage and app-model code still had local byte-comparison logic where a
shared no-runtime helper already exists. Migrating those callers keeps the
kernel surface aligned with the rule that BCL-like memory/string behavior lives
under `std/no-runtime/shared` first.

The PAL export catalog already has host-tested boot-sequence coverage, but it
did not have a selectable QEMU/CI smoke preset. Adding the preset makes future
QEMU evidence for the PAL ABI catalog explicit and machine-readable.

## Files changed

- `src/OS.Kernel/Storage/GptParser.cs`
- `src/OS.Kernel/Storage/GptPartitionTableParser.cs`
- `src/OS.Kernel/Manifest/NativeAppLaunchPlan.cs`
- `tools/OS.QemuRunner/SmokeMarkerPreset.cs`
- `scripts/run-qemu-smoke.ps1`
- `scripts/run-ci-smoke.ps1`
- `tests/OS.HostTests/StorageGptParserTests.cs`
- `tests/OS.HostTests/GptPartitionTableParserTests.cs`
- `tests/OS.HostTests/NativeAppLaunchPlanTests.cs`
- `tests/OS.HostTests/QemuRunnerTests.cs`
- `tests/OS.HostTests/CiSmokeScriptTests.cs`

## Tests run

- `dotnet build .\tests\OS.HostTests\OS.HostTests.csproj --nologo`
- `dotnet run --project .\tests\OS.HostTests\OS.HostTests.csproj --no-build`
- `.\scripts\run-qemu-smoke.ps1 -DryRun -MarkerPreset pal-export`
- `.\scripts\run-ci-smoke.ps1 -DryRun -LogRoot artifacts\logs\issue666-ci-dryrun`
- `git diff --check`

## Evidence

- `artifacts\logs\issues663-666-host-build-20260525.log`
  - Build succeeded with 0 warnings and 0 errors.
  - `EXIT_CODE 0`
- `artifacts\logs\issues663-666-hosttests-20260525.log`
  - `TEST_PASS storage gpt parser uses shared memory helpers`
  - `TEST_PASS storage.gpt.parser uses shared memory helpers`
  - `TEST_PASS appmodel launch plan uses shared memory dot segment helper`
  - `TEST_PASS qemu.smoke.preset pal export adds PAL catalog marker`
  - `TEST_PASS qemu.smoke.preset pal export aliases normalize`
  - `TEST_PASS qemu.smoke.preset script dry-run includes pal export preset`
  - `TEST_PASS ci.smoke.dryrun includes pal-export marker preset`
  - `TEST_PASS host.tests`
  - `EXIT_CODE 0`
- `artifacts\logs\issue666-qemu-pal-export-dryrun-20260525.log`
  - Dry-run output includes `QEMU_MARKER_PRESETS pal-export`.
  - Dry-run output includes `PAL_EXPORT_CATALOG_READY`.
  - Dry-run output includes `TEST_PASS qemu.runner.dryrun`.
  - `EXIT_CODE 0`
- `artifacts\logs\issue666-ci-dryrun-20260525.log`
  - Dry-run output includes `TEST_PASS ci.smoke.dryrun includes pal-export marker preset`.
  - Dry-run output includes `TEST_PASS ci.smoke.dryrun`.
  - `EXIT_CODE 0`
- `artifacts\logs\issues663-666-git-diff-check-20260525.log`
  - `EXIT_CODE 0`
  - Git reported only existing LF/CRLF working-copy warnings.

## Failures

- An artifact-path host-suite attempt was rejected as complete evidence because
  image-builder tests intentionally locate sibling host tools under the normal
  project `bin` layout. The normal-layout build and host suite were rerun and
  passed.
- A transient default-output lock from a worker-owned `OS.HostTests.exe` process
  blocked an earlier worker build. The lock cleared, and the combined parent
  normal-layout build/test pass succeeded.

## Deferred work

- This is host and dry-run QEMU evidence only. It does not prove a real QEMU
  boot, live PAL export invocation from a hosted CoreCLR guest, or cloud image
  readiness.
- The remaining open Gitea issues are still broad epics. Continue splitting
  them into issue-sized implementation slices before claiming gate progress.

