# Step 383 - Virtio-blk notify register-window fake executor

## Summary

Added a host-tested virtio-blk notify register-window executor that fake-writes
the planned 16-bit queue notification through the existing caller-owned BAR
window byte-span executor.

## Why

Step 382 produced checked notify BAR access metadata but stopped before any
byte write. This slice proves the access metadata can drive deterministic
caller-owned byte-span execution while still avoiding live MMIO, memory
barriers, DMA, hardware notification, polling, interrupts, and disk reads.

## Files changed

- `src/OS.Kernel/Storage/VirtioBlockReadOnlyDevicePlan.cs`
- `tests/OS.HostTests/StorageVirtioBlockReadOnlyDevicePlanTests.cs`
- `docs/cstorage.md`
- `docs/drivers.md`
- `docs/completion-gates.md`
- `done/step383.md`

## Tests run

- `dotnet build .\tests\OS.HostTests\OS.HostTests.csproj --nologo`
- `dotnet run --project .\tests\OS.HostTests\OS.HostTests.csproj --no-build | Select-String -Pattern 'TEST_(PASS|FAIL) storage\.virtioblk\.notifywindowexec|TEST_FAIL|TEST_PASS host\.tests'`
- `dotnet build .\OS.slnx --nologo -m:1`
- `git diff --check`

## Evidence

Focused host-test marker output:

```text
TEST_PASS storage.virtioblk.notifywindowexec writes planned notify access
TEST_PASS storage.virtioblk.notifywindowexec rejects failed plan
TEST_PASS storage.virtioblk.notifywindowexec propagates access failure
TEST_PASS storage.virtioblk.notifywindowexec preserves no-live-device boundary
TEST_PASS host.tests
```

The focused host build and full solution build passed with 0 warnings and 0
errors. `git diff --check` reported no whitespace errors; it only emitted
LF-to-CRLF working-copy warnings on the Windows host.

## Failures

None in the focused build, marker scan, full solution build, or diff check.

## Deferred work

- Add an explicit barrier-readiness gate before fake virtio-blk notify execution
  so descriptor and available-ring visibility must be represented before any
  notify write path is composed into readiness.
- Keep live virtio-blk notification blocked until live MMIO ownership,
  DMA-safe queue ownership, interrupt/polling policy, and QEMU evidence exist.
- Do not treat this as live virtio-blk, real disk I/O, cloud readiness, hosted
  .NET 10 readiness, ASP.NET Minimal API readiness, or Kestrel evidence.

## Evidence boundary

This proves only host-tested fake execution over caller-owned notify BAR-window
bytes using already-planned access metadata. It does not map BARs, dereference
physical or virtual BAR addresses, perform volatile/live MMIO, execute real
memory barriers, notify hardware, allocate or map DMA, wait for hardware, read
sectors, or prove QEMU/cloud, hosted .NET 10, ASP.NET Minimal API, or Kestrel
readiness.
