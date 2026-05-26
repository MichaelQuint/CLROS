# Step 354 - Virtio-net queue setup register-window fake execution

## Summary

Added `VirtioNetQueueMmioSetupRegisterWindowExecutor`, a host-tested fake
byte-span execution bridge from virtio-net RX/TX queue setup commands to checked
common-configuration register-window access execution over caller-owned common
BAR-window bytes.

## Why

The queue setup command stream already had a metadata bridge to generic
register-window access plans. This step proves those planned accesses can be
applied through the generic fake byte-span executor before a live BAR-backed
volatile accessor exists.

## Files changed

- `src/OS.Kernel/Drivers/VirtioNetQueueMmioSetupPlan.cs`
- `tests/OS.HostTests/VirtioNetQueueMmioSetupPlanTests.cs`
- `docs/virtio-net-mmio-boundary.md`
- `docs/drivers.md`
- `docs/networking.md`
- `docs/completion-gates.md`
- `done/step354.md`

## Tests run

- `dotnet build .\tests\OS.HostTests\OS.HostTests.csproj --nologo`
- `dotnet run --project .\tests\OS.HostTests\OS.HostTests.csproj --no-build | Select-String -Pattern 'drivers\.virtionet\.queue\.mmio\.windowexec|TEST_FAIL|TEST_PASS host\.tests'`
- `dotnet run --project .\tests\OS.HostTests\OS.HostTests.csproj --no-build | Select-String -Pattern 'TEST_FAIL|TEST_PASS host\.tests'`
- `dotnet build .\OS.slnx --nologo -m:1`
- `git diff --check`

## Evidence

- `TEST_PASS drivers.virtionet.queue.mmio.windowexec writes planned setup accesses`
- `TEST_PASS drivers.virtionet.queue.mmio.windowexec rejects failed setup plan`
- `TEST_PASS drivers.virtionet.queue.mmio.windowexec rejects short command source`
- `TEST_PASS drivers.virtionet.queue.mmio.windowexec rejects read mismatch`
- `TEST_PASS drivers.virtionet.queue.mmio.windowexec propagates access failure`
- `TEST_PASS drivers.virtionet.queue.mmio.windowexec preserves no-live-mmio boundary`
- `TEST_PASS host.tests`
- Host test build passed.
- Full solution build passed.
- `git diff --check` passed.

## Failures

- None.

## Deferred work

- Add a live BAR-backed volatile accessor that consumes checked setup access
  plans without staging read values.
- Define page mapping, cacheability, and MMIO ownership rules before live BAR
  access.
- Replace explicit metadata/fake byte-span read staging with real
  queue-select-dependent device register reads only after a live accessor exists.
- Prove one live queue setup and notification path under QEMU before claiming
  live virtio-net TX.

## Evidence boundary

This is host-tested fake queue setup register-window execution evidence over
caller-owned common BAR-window bytes. It validates that planned RX/TX queue
setup commands can be translated to checked common-configuration access plans
and applied through the generic fake byte-span executor. Read commands stage
expected queue-size and notify-offset values because a byte span does not model
queue-select-dependent hardware registers. This does not map BARs, dereference
physical addresses, perform volatile/live MMIO, execute real memory barriers,
configure a live device queue, notify a live device, allocate or map DMA
memory, process interrupts, poll hardware, transfer packets, or prove
networking, cloud, hosted .NET 10, ASP.NET Minimal API, Kestrel, or production
readiness.
