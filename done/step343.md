# Step 343 - Virtio-net TX descriptor free-list planner

## Summary

Added a host-tested virtio-net TX descriptor free-list planner that reads the
caller-owned TX descriptor ownership state table and selects the first
contiguous two-descriptor free pair for a future TX publication path.

## Why

The existing TX ownership plan could mark published descriptors `InFlight` and
return completed descriptors to `Free`, but the next TX submission still lacked
a status-coded selection layer that avoids reusing in-flight descriptors. This
slice adds that selection boundary without crossing into MMIO, DMA mapping,
device notification, interrupts, packet I/O, or a full runtime allocator.

## Files changed

- `src/OS.Kernel/Drivers/VirtioNetTransmitDescriptorOwnershipPlan.cs`
- `tests/OS.HostTests/VirtioNetTransmitDescriptorOwnershipPlanTests.cs`
- `docs/virtio-net-mmio-boundary.md`
- `docs/networking.md`
- `docs/drivers.md`
- `docs/completion-gates.md`
- `done/step343.md`

## Tests run

- `dotnet build .\tests\OS.HostTests\OS.HostTests.csproj --nologo`
- `dotnet run --project .\tests\OS.HostTests\OS.HostTests.csproj --no-build | Select-String -Pattern 'tx.freelist|TEST_FAIL|TEST_PASS host.tests'`
- `dotnet run --project .\tests\OS.HostTests\OS.HostTests.csproj --no-build`

## Evidence

- Focused host build completed with 0 warnings and 0 errors.
- Filtered host run emitted:
  - `TEST_PASS drivers.virtionet.tx.freelist selects first free pair`
  - `TEST_PASS drivers.virtionet.tx.freelist skips in-flight descriptors`
  - `TEST_PASS drivers.virtionet.tx.freelist reuses reclaimed descriptors`
  - `TEST_PASS drivers.virtionet.tx.freelist reports capacity exhaustion`
  - `TEST_PASS drivers.virtionet.tx.freelist rejects invalid queue state`
  - `TEST_PASS drivers.virtionet.tx.freelist preserves no-mmio boundary`
  - `TEST_PASS host.tests`
- Full host run completed with `TEST_PASS host.tests` and included the six
  `drivers.virtionet.tx.freelist` markers above.
- `VirtioNetTransmitDescriptorFreeListPlanner` exposes no-MMIO/no-DMA/no-packet
  boundary constants and does not mutate the caller-owned state table.

## Failures

- None in the commands listed above.

## Deferred work

- Feed the selected descriptor pair into a later TX publication planner.
- Add a fake-accessor execution boundary for queue setup and notification,
  including explicit memory-barrier preconditions before any future notify
  write.
- Prove live virtio-net notification/completion in QEMU only after memory
  mapping, volatile register access, and DMA ownership rules exist.

## Evidence boundary

This is host-tested planner evidence only. It does not allocate DMA-safe
buffers, publish descriptors to a live device, execute memory barriers, write
MMIO, notify hardware, poll or handle interrupts, move Ethernet frames, prove
live descriptor reuse, prove networking readiness, or satisfy cloud readiness.

