# Step 356 - Barrier-gated virtio-net notification register-window execution

## Summary

Added `VirtioNetQueueNotificationRegisterWindowBarrierExecutor`, a host-tested
barrier-aware fake execution bridge from virtio-net queue notification plans to
checked notify-aperture register-window writes over caller-owned notify
BAR-window bytes.

## Why

The register-window notification executor could already write planned queue
notifications into caller-owned notify BAR-window bytes. This step adds the
explicit descriptor-write, available-ring-write, and ordered-before-notify
metadata gate before that fake byte-span write so later RX/TX readiness
composition can share one ordering-aware boundary.

## Files changed

- `src/OS.Kernel/Drivers/VirtioNetQueueNotificationPlan.cs`
- `tests/OS.HostTests/VirtioNetQueueNotificationPlanTests.cs`
- `docs/virtio-net-mmio-boundary.md`
- `docs/drivers.md`
- `docs/networking.md`
- `docs/completion-gates.md`
- `done/step356.md`

## Tests run

- `dotnet build .\tests\OS.HostTests\OS.HostTests.csproj --nologo`
- `dotnet run --project .\tests\OS.HostTests\OS.HostTests.csproj --no-build | Select-String -Pattern 'drivers\.virtionet\.queue\.notify\.windowbarrier|TEST_FAIL|TEST_PASS host\.tests'`
- `dotnet run --project .\tests\OS.HostTests\OS.HostTests.csproj --no-build | Select-String -Pattern 'TEST_FAIL|TEST_PASS host\.tests'`
- `dotnet build .\OS.slnx --nologo -m:1`
- `git diff --check`

## Evidence

- `TEST_PASS drivers.virtionet.queue.notify.windowbarrier writes planned tx notification`
- `TEST_PASS drivers.virtionet.queue.notify.windowbarrier rejects missing barrier`
- `TEST_PASS drivers.virtionet.queue.notify.windowbarrier rejects failed notification plan`
- `TEST_PASS drivers.virtionet.queue.notify.windowbarrier propagates register window failure`
- `TEST_PASS drivers.virtionet.queue.notify.windowbarrier propagates access failure`
- `TEST_PASS drivers.virtionet.queue.notify.windowbarrier preserves no-live-mmio boundary`
- `TEST_PASS host.tests`
- Host test build passed.
- Full solution build passed.
- `git diff --check` passed.

## Failures

- None.

## Deferred work

- Compose RX and TX readiness paths through this barrier-gated register-window
  notification executor.
- Replace explicit barrier-readiness metadata with a real memory-ordering
  primitive before live notification.
- Add a live BAR-backed volatile accessor and QEMU packet evidence before
  claiming live virtio-net TX/RX.

## Evidence boundary

This is host-tested barrier-gated fake queue notification register-window
execution evidence over caller-owned notify BAR-window bytes. The barrier is
explicit metadata only; no real memory-ordering instruction is executed. This
does not map BARs, dereference physical addresses, perform volatile/live MMIO,
notify a live device, allocate or map DMA memory, process interrupts, poll
hardware, transfer packets, or prove networking, cloud, hosted .NET 10,
ASP.NET Minimal API, Kestrel, or production readiness.
