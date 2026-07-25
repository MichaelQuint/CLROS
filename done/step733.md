# Step 733 - Virtio-Blk Queue Setup Register-Window Executor

## Summary

Added a host-tested fake executor for virtio-blk queue setup register-window commands. `VirtioBlockQueueMmioSetupRegisterWindowExecutor` consumes the setup command stream, a bound common-configuration register window, and caller-owned common BAR-window bytes, then executes each planned read/write through the generic fake register-window access executor.

## Why

The previous storage slice could bind queue setup commands to checked register-window access metadata, but the storage path still lacked an execution boundary that proves the command stream can be applied to caller-owned common-config bytes before live MMIO exists. This slice validates ordering, staged device reads, access failure propagation, and no-live-MMIO boundaries without touching hardware.

## Files changed

- `src/OS.Kernel/Storage/VirtioBlockQueueMmioSetupRegisterWindowPlan.cs`
- `tests/OS.HostTests/StorageVirtioBlockQueueMmioSetupRegisterWindowPlanTests.cs`
- `docs/cstorage.md`
- `docs/drivers.md`
- `done/step733.md`

## Tests run

- `dotnet build .\tests\OS.HostTests\OS.HostTests.csproj --nologo`
- `dotnet run --project .\tests\OS.HostTests\OS.HostTests.csproj --no-build`
- `git diff --check`

## Evidence

- `artifacts\logs\issue699-host-build-20260525.log`: host test project build succeeded with 0 warnings and 0 errors after the test source was corrected to create mismatched command metadata through the planner contract.
- `artifacts\logs\issue699-hosttests-20260525.log`: host tests passed with:
  - `TEST_PASS storage.virtioblk.queuesetup.windowexec writes planned setup accesses`
  - `TEST_PASS storage.virtioblk.queuesetup.windowexec rejects failed setup plan`
  - `TEST_PASS storage.virtioblk.queuesetup.windowexec rejects short command source`
  - `TEST_PASS storage.virtioblk.queuesetup.windowexec rejects read mismatch`
  - `TEST_PASS storage.virtioblk.queuesetup.windowexec propagates access failure`
  - `TEST_PASS storage.virtioblk.queuesetup.windowexec preserves no-live-mmio boundary`
  - `TEST_PASS storage.virtioblk.queuesetup.windowexec default result is not success`
  - `TEST_PASS host.tests`
- `artifacts\logs\issue699-git-diff-check-postdone-20260525.log`: `git diff --check` exited 0; output contains only existing LF-to-CRLF working-copy warnings.

## Failures

- The first host build failed because the test attempted to construct `VirtioBlockQueueMmioSetupCommand` directly. The test now creates mismatched command metadata through `VirtioBlockQueueMmioSetupPlanner.Create`, preserving the public/host-tested contract.
- No remaining test failure is known for this slice.

## Deferred work

- Add explicit barrier-readiness metadata before queue enable can feed a future live setup boundary.
- Compose fake queue setup execution with descriptor publication and notify readiness.
- Replace caller-owned fake common BAR-window bytes with an owned volatile MMIO accessor only after PCI/BAR ownership, page protections, and DMA-safe queue allocation are implemented.

## Evidence boundary

This is host-tested fake execution over caller-owned byte spans only. It does not map BARs, perform volatile/live MMIO, access physical addresses, execute real memory barriers, allocate or map DMA memory, configure a live queue, publish descriptors to hardware, notify a device, process interrupts, perform block I/O, read a disk, prove QEMU boot, or prove cloud readiness.
