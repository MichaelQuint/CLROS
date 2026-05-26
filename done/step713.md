# Step 713 - NativeAOT Cctor Work-Item BootSequence Readiness Marker

## Summary
Added a host-tested BootSequence readiness boundary for already-produced
NativeAOT class-constructor work-item plans. The new boundary emits
`NATIVEAOT_CCTOR_WORK_ITEMS_READY` only for initialized successful work-item
plans and `NATIVEAOT_CCTOR_WORK_ITEMS_NOT_READY` for default or failed plans.

## Why
The class-constructor work-item planner and diagnostic formatter already
provided caller-owned metadata below the future cctor dispatcher, but
BootSequence did not expose that metadata as an ordered boot-readiness
boundary. This step keeps the slice at the marker/planning layer so later
runtime work can depend on a stable readiness contract without executing class
constructors or discovering metadata.

## Files changed
- `src/OS.Kernel/Boot/BootSequence.cs`
- `tests/OS.HostTests/NativeAotClassConstructorWorkItemPlanTests.cs`
- `docs/boot.md`
- `docs/nativeaot-nostdlib-limits.md`
- `docs/completion-gates.md`
- `done/step713.md`

## Tests run
- `dotnet build .\tests\OS.HostTests\OS.HostTests.csproj --nologo`
- `dotnet run --project .\tests\OS.HostTests\OS.HostTests.csproj --no-build -- "runtime.cctor work item|boot.sequence cctor work item"`

## Evidence
- Build log:
  `artifacts\logs\issue636-host-build-20260525-061500.log`
- Host-test log:
  `artifacts\logs\issue636-hosttests-cctor-work-item-20260525-061503.log`
- Observed host-test markers:
  - `TEST_PASS runtime.cctor work item plans uninitialized entries`
  - `TEST_PASS runtime.cctor work item formatter formats success marker`
  - `TEST_PASS boot.sequence cctor work item ready`
  - `TEST_PASS boot.sequence cctor work item rejects failed result`
  - `TEST_PASS boot.sequence cctor work item rejects default result`
  - `TEST_PASS boot.sequence cctor work item preserves default markers`
  - `TEST_PASS host.tests`

## Failures
- None in the commands above.

## Deferred work
- The marker is not wired into the default `BootSequence.Run` path, live EFI,
  or QEMU marker presets.
- This does not execute constructors, invoke delegates, discover metadata, run
  module initializers, support lazy static reference fields, or implement
  exception unwind/finally/catch behavior.

