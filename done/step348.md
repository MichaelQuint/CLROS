# Step 348 - Queue setup fake register-accessor execution

## Summary

Added a host-tested virtio-net queue setup executor that consumes the existing
RX/TX common-configuration setup command plan and executes it only through an
injected fake virtio PCI register accessor.

## Why

The next live virtio-net boundary needs a register-accessor-shaped execution
slice before any BAR-backed MMIO implementation exists. This proves queue setup
command ordering, readback validation, accessor failure propagation, and the
no-live-MMIO boundary over caller-owned test storage.

## Files changed

- `src/OS.Kernel/Drivers/VirtioNetQueueMmioSetupPlan.cs`
- `src/OS.Kernel/Drivers/VirtioNetQueueNotificationPlan.cs`
- `tests/OS.HostTests/VirtioNetQueueMmioSetupPlanTests.cs`
- `tests/OS.HostTests/VirtioNetQueueNotificationPlanTests.cs`
- `docs/virtio-net-mmio-boundary.md`
- `docs/networking.md`
- `docs/drivers.md`
- `docs/completion-gates.md`
- `done/step348.md`

## Tests run

- `dotnet build .\tests\OS.HostTests\OS.HostTests.csproj --nologo`
- `dotnet run --project .\tests\OS.HostTests\OS.HostTests.csproj --no-build | Select-String -Pattern 'drivers\.virtionet\.queue\.mmio\.exec|TEST_FAIL|TEST_PASS host\.tests'`
- `dotnet run --project .\tests\OS.HostTests\OS.HostTests.csproj --no-build | Select-String -Pattern 'TEST_FAIL|TEST_PASS host\.tests'`
- `dotnet build .\OS.slnx --nologo -m:1`
- `git diff --check`

## Evidence

- Focused host build completed with 0 warnings and 0 errors.
- Filtered host run emitted:
  - `TEST_PASS drivers.virtionet.queue.mmio.exec writes planned commands`
  - `TEST_PASS drivers.virtionet.queue.mmio.exec writes planned rx tx setup`
  - `TEST_PASS drivers.virtionet.queue.mmio.exec rejects failed setup plan`
  - `TEST_PASS drivers.virtionet.queue.mmio.exec rejects short command source`
  - `TEST_PASS drivers.virtionet.queue.mmio.exec rejects read mismatch`
  - `TEST_PASS drivers.virtionet.queue.mmio.exec propagates accessor failure`
  - `TEST_PASS drivers.virtionet.queue.mmio.exec propagates read failure`
  - `TEST_PASS drivers.virtionet.queue.mmio.exec propagates write failure`
  - `TEST_PASS drivers.virtionet.queue.mmio.exec rejects missing accessor`
  - `TEST_PASS drivers.virtionet.queue.mmio.exec preserves no-live-mmio boundary`
  - `TEST_PASS host.tests`
- Full host run completed with `TEST_PASS host.tests` and included the queue
  setup execution markers above.
- Full solution build completed with 0 warnings and 0 errors.
- `git diff --check` completed with exit code 0; Git reported existing CRLF
  normalization warnings only.

## Failures

- None in the commands listed above.

## Deferred work

- Compose successful fake-accessor queue setup, caller-owned descriptor
  publication, explicit memory-barrier readiness, and fake-accessor
  notification execution into one host-tested readiness boundary.
- Replace fake accessor storage with a BAR-backed volatile accessor only after
  MMIO ownership, BAR mapping, and post-firmware access rules exist.
- Prove live virtio-net queue setup, notification, and completion in QEMU only
  after DMA ownership, interrupt/polling rules, and register access are
  implemented.

## Evidence boundary

This is host-tested fake virtio PCI register-accessor queue setup execution
evidence only. It executes planned RX/TX setup commands through injected fake
`Read16`, `Write16`, and `Write64` operations over caller-owned test storage.
It does not map BARs, perform volatile or live MMIO, notify a live device,
publish descriptors, allocate or map DMA memory, process interrupts, poll
hardware, transfer packets, or prove networking, cloud, hosted .NET, Kestrel,
or production readiness.

