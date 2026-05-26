# Step 351 - Virtio-net queue setup register-window access planning

## Summary

Added `VirtioNetQueueMmioSetupRegisterWindowPlanner`, a host-tested metadata-only
bridge from virtio-net RX/TX queue setup commands to checked virtio PCI common
configuration register-window access plans.

## Why

After adding generic BAR register-window address planning, the virtio-net queue
setup command stream needed a narrow bridge into that generic planner. This step
proves each planned queue setup command can produce caller-owned access-plan
metadata before any future BAR-backed volatile accessor exists.

## Files changed

- `src/OS.Kernel/Drivers/VirtioNetQueueMmioSetupPlan.cs`
- `tests/OS.HostTests/VirtioNetQueueMmioSetupPlanTests.cs`
- `docs/virtio-net-mmio-boundary.md`
- `docs/drivers.md`
- `docs/networking.md`
- `docs/completion-gates.md`
- `done/step351.md`

## Tests run

- `dotnet build .\tests\OS.HostTests\OS.HostTests.csproj --nologo`
- `dotnet run --project .\tests\OS.HostTests\OS.HostTests.csproj --no-build | Select-String -Pattern 'drivers\.virtionet\.queue\.mmio\.window|TEST_FAIL|TEST_PASS host\.tests'`
- `dotnet run --project .\tests\OS.HostTests\OS.HostTests.csproj --no-build | Select-String -Pattern 'TEST_FAIL|TEST_PASS host\.tests'`
- `dotnet build .\OS.slnx --nologo -m:1`
- `git diff --check`

## Evidence

- `TEST_PASS drivers.virtionet.queue.mmio.window plans setup command accesses`
- `TEST_PASS drivers.virtionet.queue.mmio.window rejects failed setup plan`
- `TEST_PASS drivers.virtionet.queue.mmio.window rejects short command source`
- `TEST_PASS drivers.virtionet.queue.mmio.window rejects short destination`
- `TEST_PASS drivers.virtionet.queue.mmio.window rejects unsupported command width`
- `TEST_PASS drivers.virtionet.queue.mmio.window propagates register window failure`
- `TEST_PASS drivers.virtionet.queue.mmio.window preserves no-live-mmio boundary`
- `TEST_PASS host.tests`
- Host test build passed.
- Full solution build passed.
- `git diff --check` passed.

## Failures

- None.

## Deferred work

- Implement the live BAR-backed volatile accessor that consumes the register
  window and queue setup access plans.
- Define post-firmware MMIO ownership and cacheability/page mapping rules.
- Replace explicit barrier-readiness metadata with a real kernel-safe ordering
  primitive.
- Add DMA ownership/allocation rules before live virtio-net packet I/O.
- Prove one live notification/completion path under QEMU before claiming live
  virtio-net TX.

## Evidence boundary

This is host-tested metadata-only bridge evidence from queue setup commands to
register-window access plans. It does not map BARs, perform volatile/live MMIO,
execute memory barriers, execute fake or live register reads/writes, configure a
live queue, notify a live device, allocate or map DMA memory, process
interrupts, poll hardware, transfer packets, or prove networking, cloud, hosted
.NET 10, ASP.NET Minimal API, Kestrel, or production readiness.
