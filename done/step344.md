# Step 344 - Virtio-net TX publish-plan composer

## Summary

Added a host-tested virtio-net TX publish-plan composer that selects a reusable
two-descriptor TX pair from caller-owned descriptor state, publishes the
no-offload TX header/frame descriptors into caller-owned split-ring memory, and
marks only the selected descriptors `InFlight` after successful publication.

## Why

The free-list planner could choose a safe descriptor pair, but later TX work
needed a single status-coded boundary that composes selection, caller-owned
queue-memory publication, and descriptor ownership marking without reusing
in-flight descriptors or crossing into live MMIO/device I/O.

## Files changed

- `src/OS.Kernel/Drivers/VirtioNetTransmitDescriptorOwnershipPlan.cs`
- `tests/OS.HostTests/VirtioNetTransmitDescriptorOwnershipPlanTests.cs`
- `docs/virtio-net-mmio-boundary.md`
- `docs/networking.md`
- `docs/drivers.md`
- `docs/completion-gates.md`
- `done/step344.md`

## Tests run

- `dotnet build .\tests\OS.HostTests\OS.HostTests.csproj --nologo`
- `dotnet run --project .\tests\OS.HostTests\OS.HostTests.csproj --no-build | Select-String -Pattern 'tx.publishplan|TEST_FAIL|TEST_PASS host.tests'`
- `dotnet run --project .\tests\OS.HostTests\OS.HostTests.csproj --no-build`

## Evidence

- Focused host build completed with 0 warnings and 0 errors.
- Filtered host run emitted:
  - `TEST_PASS drivers.virtionet.tx.publishplan publishes selected free pair`
  - `TEST_PASS drivers.virtionet.tx.publishplan skips in-flight descriptors`
  - `TEST_PASS drivers.virtionet.tx.publishplan rejects free-list exhaustion`
  - `TEST_PASS drivers.virtionet.tx.publishplan preserves state on publish failure`
  - `TEST_PASS drivers.virtionet.tx.publishplan rejects invalid ownership state`
  - `TEST_PASS drivers.virtionet.tx.publishplan preserves no-mmio boundary`
  - `TEST_PASS host.tests`
- Full host run completed with `TEST_PASS host.tests` and included the six
  `drivers.virtionet.tx.publishplan` markers above.
- `VirtioNetTransmitDescriptorPublishPlanner` reports nested free-list,
  publish, and ownership statuses, preserves state when free-list selection or
  lifecycle publication fails, and exposes explicit no-MMIO/no-DMA/no-packet-I/O
  boundary constants.

## Failures

- None in the commands listed above.

## Deferred work

- Feed successful TX publish-plan results into a fake-accessor notification and
  completion execution boundary.
- Add explicit memory-barrier preconditions before any future notify write.
- Prove live virtio-net notification/completion in QEMU only after memory
  mapping, volatile register access, and DMA ownership rules exist.

## Evidence boundary

This is host-tested planner/composition evidence over caller-owned memory and
state only. It writes caller-owned queue memory and caller-owned TX header
storage, then mutates caller-owned descriptor state only after successful
publication. It does not allocate DMA-safe buffers, publish descriptors to a
live device, execute memory barriers, write MMIO, notify hardware, poll or
handle interrupts, move Ethernet frames on hardware, prove live descriptor
reuse, prove networking readiness, or satisfy cloud readiness.

