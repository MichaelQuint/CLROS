# Step 345 - Virtio-net queue notification fake-sink executor

## Summary

Added a host-tested virtio-net queue notification execution boundary that
consumes a successful RX/TX notification plan, requires explicit barrier
readiness metadata, and writes the planned 16-bit queue notification through an
injected fake sink.

## Why

Queue notification planning could calculate the notify aperture offset and
value, but the next driver slice needed a status-coded execution-shaped
boundary before any live BAR mapping or MMIO write exists. This keeps the
planner pure while proving ordering, sink failure propagation, and
no-live-MMIO boundaries.

## Files changed

- `src/OS.Kernel/Drivers/VirtioNetQueueNotificationPlan.cs`
- `tests/OS.HostTests/VirtioNetQueueNotificationPlanTests.cs`
- `docs/virtio-net-mmio-boundary.md`
- `docs/networking.md`
- `docs/drivers.md`
- `docs/completion-gates.md`
- `done/step345.md`

## Tests run

- `dotnet build .\tests\OS.HostTests\OS.HostTests.csproj --nologo`
- `dotnet run --project .\tests\OS.HostTests\OS.HostTests.csproj --no-build | Select-String -Pattern 'drivers\.virtionet\.queue\.notify\.exec|TEST_FAIL|TEST_PASS host\.tests'`
- `dotnet run --project .\tests\OS.HostTests\OS.HostTests.csproj --no-build`
- `dotnet build .\OS.slnx --nologo -m:1`
- `git diff --check`

## Evidence

- Focused host build completed with 0 warnings and 0 errors.
- Filtered host run emitted:
  - `TEST_PASS drivers.virtionet.queue.notify.exec writes planned tx notification`
  - `TEST_PASS drivers.virtionet.queue.notify.exec writes planned rx notification`
  - `TEST_PASS drivers.virtionet.queue.notify.exec rejects missing barrier`
  - `TEST_PASS drivers.virtionet.queue.notify.exec rejects failed notification plan`
  - `TEST_PASS drivers.virtionet.queue.notify.exec propagates sink failure`
  - `TEST_PASS drivers.virtionet.queue.notify.exec preserves no-live-mmio boundary`
  - `TEST_PASS host.tests`
- Full host run completed with `TEST_PASS host.tests` and included the six
  `drivers.virtionet.queue.notify.exec` markers above.
- Full solution build completed with 0 warnings and 0 errors.
- `git diff --check` completed with exit code 0; Git reported existing CRLF
  normalization warnings only.
- `VirtioNetQueueNotificationExecutor` exposes explicit no-live-MMIO,
  no-BAR-mapping, no-DMA, no-interrupt, no-packet-I/O, no-queue-read, and
  no-descriptor-state-mutation boundary constants.

## Failures

- None in the commands listed above.

## Deferred work

- Replace or wrap the fake sink with a broader host-tested virtio PCI register
  accessor contract.
- Add real memory-barrier execution only after the platform memory-ordering
  contract exists.
- Prove live virtio-net notification/completion in QEMU only after BAR mapping,
  volatile register access, DMA ownership, and interrupt/polling rules exist.

## Evidence boundary

This is host-tested fake-sink execution evidence only. It writes a caller-owned
test byte array through an injected sink and preserves status provenance for
failed plans, missing barrier readiness, sink not-ready, sink range mismatch,
and sink write failure. It does not map BARs, perform live MMIO, notify
hardware, poll or handle interrupts, allocate or map DMA memory, mutate
descriptor ownership state, read queue memory, move Ethernet frames, prove
packet transmission, prove networking readiness, or satisfy cloud readiness.
