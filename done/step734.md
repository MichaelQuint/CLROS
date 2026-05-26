# Step 734 - Virtio-Blk Queue Setup Barrier-Gated Enable

## Summary

Added a host-tested setup-barrier fake executor for virtio-blk queue setup. `VirtioBlockQueueMmioSetupRegisterWindowBarrierExecutor` consumes the existing queue setup command stream, a bound common-configuration register window, caller-owned common BAR-window bytes, and explicit setup-write visibility metadata, then gates the final `queue_enable` fake write on ordered-before-enable readiness.

## Why

The previous slice proved the planned queue setup command stream can be fake-executed over caller-owned common-config bytes, including `queue_enable`. Real virtio queue setup needs queue select, queue size, and queue address writes ordered before the enable write. This slice makes that ordering requirement explicit without claiming live MMIO, real memory barriers, DMA-safe queue ownership, device notification, or block I/O.

## Files changed

- `src/OS.Kernel/Storage/VirtioBlockQueueMmioSetupRegisterWindowPlan.cs`
- `tests/OS.HostTests/StorageVirtioBlockQueueMmioSetupRegisterWindowPlanTests.cs`
- `docs/cstorage.md`
- `docs/drivers.md`
- `done/step734.md`

## Tests run

- `dotnet build .\tests\OS.HostTests\OS.HostTests.csproj --nologo`
- `dotnet run --project .\tests\OS.HostTests\OS.HostTests.csproj --no-build`
- `git diff --check`

## Evidence

- `artifacts\logs\issue700-host-build-20260525.log`: host test project build succeeded with 0 warnings and 0 errors.
- `artifacts\logs\issue700-hosttests-20260525.log`: host tests passed with:
  - `TEST_PASS storage.virtioblk.queuesetup.windowbarrier writes queue enable after barrier`
  - `TEST_PASS storage.virtioblk.queuesetup.windowbarrier rejects missing barrier`
  - `TEST_PASS storage.virtioblk.queuesetup.windowbarrier rejects failed setup plan`
  - `TEST_PASS storage.virtioblk.queuesetup.windowbarrier rejects short command source`
  - `TEST_PASS storage.virtioblk.queuesetup.windowbarrier propagates access failure`
  - `TEST_PASS storage.virtioblk.queuesetup.windowbarrier preserves no-live-mmio boundary`
  - `TEST_PASS storage.virtioblk.queuesetup.windowbarrier default result is not success`
  - `TEST_PASS host.tests`
- `artifacts\logs\issue700-git-diff-check-postdone-20260525.log`: `git diff --check` exited 0; output contains only existing LF-to-CRLF working-copy warnings.

## Failures

No test failure is known for this slice.

## Deferred work

- Compose this setup-barrier execution result with a future live virtio-blk queue ownership boundary.
- Replace caller-owned fake common BAR-window bytes with an owned volatile MMIO accessor only after PCI/BAR ownership, page protections, real memory-barrier primitives, and DMA-safe queue allocation are implemented.
- Add live queue notification and completion waiting later; this slice intentionally stops before notify or disk I/O.

## Evidence boundary

This is host-tested fake execution over caller-owned byte spans only. It does not map BARs, perform volatile/live MMIO, access physical addresses, execute real memory barriers, allocate or map DMA memory, configure a live queue, publish descriptors to hardware, notify a device, process interrupts, perform block I/O, read a disk, prove QEMU boot, or prove cloud readiness.

