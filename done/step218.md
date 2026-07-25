# Step 218 - Virtio-Net Queue MMIO Setup Planner

## Summary

Added a host-testable virtio-net queue MMIO setup planner for issue #231.
The planner turns an already validated virtio-net queue bring-up plan plus
caller-owned physical queue addresses into deterministic common-configuration
register command metadata for RX and TX queues.

## Why

The virtio-net path needs a clear boundary between metadata-only queue planning
and future live MMIO writes. This step records the register ordering, queue
selection, queue size, split-ring address publication, notify-offset read, and
queue-enable command metadata without invoking a register accessor or touching
hardware.

## Files changed

- `src/OS.Kernel/Drivers/VirtioNetQueueMmioSetupPlan.cs`
- `tests/OS.HostTests/VirtioNetQueueMmioSetupPlanTests.cs`
- `docs/drivers.md`

## Tests run

- `dotnet build .\OS.slnx --nologo -m:1`
- `dotnet run --project .\tests\OS.HostTests\OS.HostTests.csproj --no-build`
- `.\scripts\check-naming.ps1`
- `.\scripts\run-qemu-smoke.ps1 -DryRun -RequireNativeAppMarkers`
- `git diff --check`

## Evidence

- Host test markers:
  - `TEST_PASS drivers.virtionet.queue.mmio creates setup command plan`
  - `TEST_PASS drivers.virtionet.queue.mmio rejects command destination`
  - `TEST_PASS drivers.virtionet.queue.mmio rejects queue metadata`
  - `TEST_PASS drivers.virtionet.queue.mmio rejects physical addresses`
  - `TEST_PASS drivers.virtionet.queue.mmio rejects failed bringup`
- Checkpoint logs:
  - `artifacts/logs/checkpoint-build-20260522-120158.log`
  - `artifacts/logs/checkpoint-host-tests-20260522-120158.log`
  - `artifacts/logs/checkpoint-guards-20260522-120158.log`

## Failures

None in the final checkpoint run. `git diff --check` emitted only line-ending
normalization warnings and returned exit code 0.

## Deferred work

- No live MMIO accessor is invoked.
- No DMA-safe queue memory allocation or mapping is performed.
- No descriptors are published to a live device.
- No queue notification, interrupt handling, used-ring polling, or packet I/O
  is implemented.

