# Step 731 - Virtio-Blk Queue Setup Register-Window Planner

## Summary

Added a host-tested register-window bridge for virtio-blk queue setup commands. `VirtioBlockQueueMmioSetupRegisterWindowPlanner` consumes a successful queue setup plan, caller-owned setup commands, and a bound virtio PCI common-configuration register window, then writes checked register-window access plans into caller-owned storage.

## Why

The queue setup command planner produced deterministic future common-configuration operations, but the storage path still lacked a typed bridge from those commands to the generic virtio PCI register-window access metadata used by later fake/live MMIO boundaries. This slice moves the storage stack one step closer to real queue setup without performing live MMIO or mutating hardware state.

## Files changed

- `src/OS.Kernel/Storage/VirtioBlockQueueMmioSetupRegisterWindowPlan.cs`
- `tests/OS.HostTests/StorageVirtioBlockQueueMmioSetupRegisterWindowPlanTests.cs`
- `docs/cstorage.md`
- `docs/drivers.md`
- `done/step731.md`

## Tests run

- `dotnet build .\tests\OS.HostTests\OS.HostTests.csproj --nologo`
- `dotnet run --project .\tests\OS.HostTests\OS.HostTests.csproj --no-build`
- `git diff --check`

## Evidence

- `artifacts\logs\issue696-host-build-20260525.log`: host test project build succeeded.
- `artifacts\logs\issue696-hosttests-20260525.log`: host tests passed with:
  - `TEST_PASS storage.virtioblk.queuesetup.window plans setup command accesses`
  - `TEST_PASS storage.virtioblk.queuesetup.window rejects failed setup plan`
  - `TEST_PASS storage.virtioblk.queuesetup.window rejects short command source`
  - `TEST_PASS storage.virtioblk.queuesetup.window reports destination capacity`
  - `TEST_PASS storage.virtioblk.queuesetup.window propagates register window failure`
  - `TEST_PASS storage.virtioblk.queuesetup.window default result is not success`
  - `TEST_PASS host.tests`
- `artifacts\logs\issue696-git-diff-check-postdone-20260525.log`: `git diff --check` exited 0; output contains only existing LF-to-CRLF working-copy warnings.

## Failures

- No test failure is known for this slice.

## Deferred work

- Execute the planned common-configuration accesses through a fake byte-span executor for storage queue setup.
- Add explicit memory-barrier readiness before any future live queue enable or notify write.
- Wire the queue setup path into a real virtio-blk device bring-up sequence after PCI/BAR ownership and DMA-safe queue allocation exist.

## Evidence boundary

This is host-tested metadata planning only. It does not map BARs, perform volatile/live MMIO, allocate or map DMA memory, configure a live queue, publish descriptors to hardware, notify a device, process interrupts, perform block I/O, read a disk, prove QEMU boot, or prove cloud readiness.
