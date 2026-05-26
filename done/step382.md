# Step 382 - Virtio-blk notify register-window planner

## Summary

Added a host-tested virtio-blk notify register-window planner that binds a
checked notify-relative offset to the existing virtio PCI BAR register-window
access planner.

## Why

Step 381 derived the notify-aperture relative offset, but future live storage
notification needs a value-type access plan that points at the mapped notify
BAR aperture. This slice adds the metadata bridge without mapping BARs,
performing volatile MMIO, writing a fake byte span, or notifying hardware.

## Files changed

- `src/OS.Kernel/Storage/VirtioBlockReadOnlyDevicePlan.cs`
- `tests/OS.HostTests/StorageVirtioBlockReadOnlyDevicePlanTests.cs`
- `docs/cstorage.md`
- `docs/drivers.md`
- `docs/completion-gates.md`
- `done/step382.md`

## Tests run

- `dotnet build .\tests\OS.HostTests\OS.HostTests.csproj --nologo`
- `dotnet run --project .\tests\OS.HostTests\OS.HostTests.csproj --no-build | Select-String -Pattern 'TEST_(PASS|FAIL) storage\.virtioblk\.notifywindow|TEST_FAIL|TEST_PASS host\.tests'`
- `dotnet build .\OS.slnx --nologo -m:1`
- `git diff --check`

## Evidence

Focused host-test marker output:

```text
TEST_PASS storage.virtioblk.notifywindow plans access
TEST_PASS storage.virtioblk.notifywindow rejects failed offset plan
TEST_PASS storage.virtioblk.notifywindow propagates register window failure
TEST_PASS storage.virtioblk.notifywindow preserves no-live-device boundary
TEST_PASS host.tests
```

The focused host build and full solution build passed with 0 warnings and 0
errors. `git diff --check` reported no whitespace errors; it only emitted
LF-to-CRLF working-copy warnings on the Windows host.

## Failures

None in the focused build, marker scan, full solution build, or diff check.

## Deferred work

- Add a barrier-gated fake register-window notify executor for virtio-blk only
  after memory-ordering readiness metadata is represented.
- Keep live virtio-blk notification blocked until live MMIO ownership,
  DMA-safe queue ownership, interrupt/polling policy, and QEMU evidence exist.
- Do not treat this as live virtio-blk, real disk I/O, cloud readiness, hosted
  .NET 10 readiness, ASP.NET Minimal API readiness, or Kestrel evidence.

## Evidence boundary

This proves only host-tested metadata composition from a successful block
read-queue publication through notify metadata, checked notify offset, and
future notify BAR access planning. It does not read queue/status memory, mutate
cursor or descriptor state, allocate DMA-safe memory, map BARs, perform fake or
live register writes, execute memory barriers, notify hardware, process
interrupts, poll or wait for hardware, own a full runtime descriptor allocator,
read sectors, or prove QEMU/cloud, hosted .NET 10, ASP.NET Minimal API, or
Kestrel readiness.
