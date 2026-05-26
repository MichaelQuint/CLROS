# Step 384 - Virtio-blk notify barrier-gated fake executor

## Summary

Added a host-tested virtio-blk notify barrier-readiness contract and
barrier-gated fake register-window notify executor.

## Why

Step 383 proved the planned notify register-window write can fake-execute over
caller-owned bytes. A future live queue notification must also be ordered after
descriptor and available-ring writes. This slice makes that ordering requirement
explicit as metadata before byte-span execution, without executing a real memory
barrier or notifying hardware.

## Files changed

- `src/OS.Kernel/Storage/VirtioBlockReadOnlyDevicePlan.cs`
- `tests/OS.HostTests/StorageVirtioBlockReadOnlyDevicePlanTests.cs`
- `docs/cstorage.md`
- `docs/drivers.md`
- `docs/completion-gates.md`
- `done/step384.md`

## Tests run

- `dotnet build .\tests\OS.HostTests\OS.HostTests.csproj --nologo`
- `dotnet run --project .\tests\OS.HostTests\OS.HostTests.csproj --no-build | Select-String -Pattern 'TEST_(PASS|FAIL) storage\.virtioblk\.notifywindowbarrier|TEST_FAIL|TEST_PASS host\.tests'`
- `dotnet build .\OS.slnx --nologo -m:1`
- `git diff --check`

## Evidence

Focused host-test marker output:

```text
TEST_PASS storage.virtioblk.notifywindowbarrier writes planned notify access
TEST_PASS storage.virtioblk.notifywindowbarrier rejects missing barrier
TEST_PASS storage.virtioblk.notifywindowbarrier rejects failed plan
TEST_PASS storage.virtioblk.notifywindowbarrier propagates access failure
TEST_PASS storage.virtioblk.notifywindowbarrier preserves no-live-device boundary
TEST_PASS host.tests
```

The focused host build and full solution build passed with 0 warnings and 0
errors. `git diff --check` reported no whitespace errors; it only emitted
LF-to-CRLF working-copy warnings on the Windows host.

## Failures

None in the focused build, marker scan, full solution build, or diff check.

## Deferred work

- Compose read publication, explicit barrier readiness, barrier-gated notify,
  completion parsing, descriptor reclaim, and `BlockReadResult` projection into
  a single read-operation readiness path.
- Keep live virtio-blk notification blocked until live MMIO ownership,
  DMA-safe queue ownership, interrupt/polling policy, and QEMU evidence exist.
- Do not treat this as live virtio-blk, real disk I/O, cloud readiness, hosted
  .NET 10 readiness, ASP.NET Minimal API readiness, or Kestrel evidence.

## Evidence boundary

This proves only host-tested barrier metadata gating for fake notify
register-window byte-span execution. It does not execute a real memory barrier,
map BARs, dereference physical or virtual BAR addresses, perform volatile/live
MMIO, notify hardware, allocate or map DMA, wait for hardware, read sectors, or
prove QEMU/cloud, hosted .NET 10, ASP.NET Minimal API, or Kestrel readiness.
