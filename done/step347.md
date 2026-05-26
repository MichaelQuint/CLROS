# Step 347 - Queue notification fake register-accessor adapter

## Summary

Added a host-tested fake virtio PCI register-accessor adapter for queue
notification execution. The existing notification executor still writes through
`IVirtioPciNotifyWriteSink`, and `VirtioPciNotifyRegisterAccessorSink` now
routes that planned 16-bit notify write through a status-coded
`IVirtioPciRegisterAccessor.Write16` contract.

## Why

The next live virtio-net work needs a register-accessor-shaped boundary before
BAR-backed MMIO exists. This keeps the current notification executor stable
while proving the first adapter layer, status mapping, and no-live-MMIO
boundary over caller-owned test storage.

## Files changed

- `src/OS.Kernel/Drivers/VirtioNetQueueNotificationPlan.cs`
- `tests/OS.HostTests/VirtioNetQueueNotificationPlanTests.cs`
- `docs/virtio-net-mmio-boundary.md`
- `docs/networking.md`
- `docs/drivers.md`
- `docs/completion-gates.md`
- `done/step347.md`

## Tests run

- `dotnet build .\tests\OS.HostTests\OS.HostTests.csproj --nologo`
- `dotnet run --project .\tests\OS.HostTests\OS.HostTests.csproj --no-build | Select-String -Pattern 'drivers\.virtionet\.queue\.notify\.(exec|accessor)|TEST_FAIL|TEST_PASS host\.tests'`
- `dotnet run --project .\tests\OS.HostTests\OS.HostTests.csproj --no-build`
- `dotnet build .\OS.slnx --nologo -m:1`
- `git diff --check`

## Evidence

- Focused host build completed with 0 warnings and 0 errors.
- Filtered host run emitted the existing queue notification execution markers
  plus:
  - `TEST_PASS drivers.virtionet.queue.notify.accessor writes planned tx notification`
  - `TEST_PASS drivers.virtionet.queue.notify.accessor propagates range failure`
  - `TEST_PASS drivers.virtionet.queue.notify.accessor propagates write failure`
  - `TEST_PASS drivers.virtionet.queue.notify.accessor preserves no-live-mmio boundary`
  - `TEST_PASS host.tests`
- Full host run completed with `TEST_PASS host.tests` and included the queue
  notification accessor markers above.
- Full solution build completed with 0 warnings and 0 errors.
- `git diff --check` completed with exit code 0; Git reported existing CRLF
  normalization warnings only.

## Failures

- None in the commands listed above.

## Deferred work

- Extend the fake register-accessor boundary to execute the queued setup
  command plan.
- Replace fake accessor storage with a BAR-backed volatile accessor only after
  MMIO ownership, BAR mapping, and post-firmware access rules exist.
- Prove live virtio-net notification/completion in QEMU only after DMA
  ownership, interrupt/polling rules, and register access are implemented.

## Evidence boundary

This is host-tested fake virtio PCI register-accessor adapter evidence only. It
routes the existing planned 16-bit queue notify write through a status-coded
fake accessor over caller-owned test storage. It does not map BARs, perform
volatile or live MMIO, notify a live device, allocate or map DMA, process
interrupts, poll hardware, transfer packets, or prove networking, cloud,
hosted .NET, Kestrel, or production readiness.
