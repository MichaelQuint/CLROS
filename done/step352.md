# Step 352 - Virtio-net queue notification register-window access planning

## Summary

Added `VirtioNetQueueNotificationPlanner.PlanRegisterWindowAccess`, a
host-tested metadata-only bridge from successful RX/TX queue notification plans
to checked virtio PCI notify-aperture register-window access plans.

## Why

The queue setup path already had a bridge from planned common-configuration
commands to generic BAR register-window access metadata. Queue notification
needed the same narrow bridge so a future BAR-backed accessor can consume the
planned 16-bit notify write address, width, value, and provenance without
executing fake or live notification writes in the planner.

## Files changed

- `src/OS.Kernel/Drivers/VirtioNetQueueNotificationPlan.cs`
- `tests/OS.HostTests/VirtioNetQueueNotificationPlanTests.cs`
- `docs/virtio-net-mmio-boundary.md`
- `docs/drivers.md`
- `docs/networking.md`
- `docs/completion-gates.md`
- `done/step352.md`

## Tests run

- `dotnet build .\tests\OS.HostTests\OS.HostTests.csproj --nologo`
- `dotnet run --project .\tests\OS.HostTests\OS.HostTests.csproj --no-build | Select-String -Pattern 'drivers\.virtionet\.queue\.notify\.window|TEST_FAIL|TEST_PASS host\.tests'`
- `dotnet run --project .\tests\OS.HostTests\OS.HostTests.csproj --no-build | Select-String -Pattern 'TEST_FAIL|TEST_PASS host\.tests'`
- `dotnet build .\OS.slnx --nologo -m:1`
- `git diff --check`

## Evidence

- `TEST_PASS drivers.virtionet.queue.notify.window plans tx notification access`
- `TEST_PASS drivers.virtionet.queue.notify.window plans rx notification access`
- `TEST_PASS drivers.virtionet.queue.notify.window rejects failed notification plan`
- `TEST_PASS drivers.virtionet.queue.notify.window propagates register window failure`
- `TEST_PASS drivers.virtionet.queue.notify.window preserves no-live-mmio boundary`
- `TEST_PASS host.tests`
- Host test build passed.
- Full solution build passed.
- `git diff --check` passed.

## Failures

- None.

## Deferred work

- Implement the live BAR-backed volatile accessor that consumes the generic
  register-window, queue setup, and queue notification access plans.
- Define post-firmware MMIO ownership and cacheability/page mapping rules.
- Replace explicit barrier-readiness metadata with a real kernel-safe ordering
  primitive.
- Add DMA ownership/allocation rules before live virtio-net packet I/O.
- Prove one live notification/completion path under QEMU before claiming live
  virtio-net TX.

## Evidence boundary

This is host-tested metadata-only bridge evidence from RX/TX queue notification
plans to notify-aperture BAR register-window access plans. It validates that a
future accessor could address the planned 16-bit notify write through
caller-provided mapped-BAR metadata. It does not map BARs, perform
volatile/live MMIO, execute fake or live register writes, execute memory
barriers, notify a live device, allocate or map DMA memory, process interrupts,
poll hardware, transfer packets, or prove networking, cloud, hosted .NET 10,
ASP.NET Minimal API, Kestrel, or production readiness.
