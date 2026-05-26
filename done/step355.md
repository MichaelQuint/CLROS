# Step 355 - Virtio-net queue notification register-window fake execution

## Summary

Added `VirtioNetQueueNotificationRegisterWindowExecutor`, a host-tested fake
byte-span execution bridge from virtio-net RX/TX queue notification plans to
checked notify-aperture register-window writes over caller-owned notify
BAR-window bytes.

## Why

Queue notification planning already produced a checked notify register-window
access plan. This step proves that the plan can be applied through the generic
fake register-window byte-span executor before a live BAR-backed volatile MMIO
accessor exists.

## Files changed

- `src/OS.Kernel/Drivers/VirtioNetQueueNotificationPlan.cs`
- `tests/OS.HostTests/VirtioNetQueueNotificationPlanTests.cs`
- `docs/virtio-net-mmio-boundary.md`
- `docs/drivers.md`
- `docs/networking.md`
- `docs/completion-gates.md`
- `done/step355.md`

## Tests run

- `dotnet build .\tests\OS.HostTests\OS.HostTests.csproj --nologo`
- `dotnet run --project .\tests\OS.HostTests\OS.HostTests.csproj --no-build | Select-String -Pattern 'drivers\.virtionet\.queue\.notify\.windowexec|TEST_FAIL|TEST_PASS host\.tests'`
- `dotnet run --project .\tests\OS.HostTests\OS.HostTests.csproj --no-build | Select-String -Pattern 'TEST_FAIL|TEST_PASS host\.tests'`
- `dotnet build .\OS.slnx --nologo -m:1`
- `git diff --check`

## Evidence

- `TEST_PASS drivers.virtionet.queue.notify.windowexec writes planned tx notification`
- `TEST_PASS drivers.virtionet.queue.notify.windowexec writes planned rx notification`
- `TEST_PASS drivers.virtionet.queue.notify.windowexec rejects failed notification plan`
- `TEST_PASS drivers.virtionet.queue.notify.windowexec propagates register window failure`
- `TEST_PASS drivers.virtionet.queue.notify.windowexec propagates access failure`
- `TEST_PASS drivers.virtionet.queue.notify.windowexec preserves no-live-mmio boundary`
- `TEST_PASS host.tests`
- Host test build passed.
- Full solution build passed.
- `git diff --check` passed.

## Failures

- None.

## Deferred work

- Add a live BAR-backed volatile accessor that consumes checked notify access
  plans.
- Define real MMIO ownership, mapping, cacheability, and memory-ordering rules
  before live notification.
- Prove a live queue notification and completion path under QEMU before claiming
  live virtio-net TX.

## Evidence boundary

This is host-tested fake queue notification register-window execution evidence
over caller-owned notify BAR-window bytes. It validates that successful RX/TX
notification metadata can be translated to a checked notify-aperture access plan
and applied through the generic fake byte-span executor. This does not map BARs,
dereference physical addresses, perform volatile/live MMIO, execute real memory
barriers, notify a live device, allocate or map DMA memory, process interrupts,
poll hardware, transfer packets, or prove networking, cloud, hosted .NET 10,
ASP.NET Minimal API, Kestrel, or production readiness.
