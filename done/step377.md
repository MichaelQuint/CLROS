# Step 377 - Virtio-blk read queue publication planner

## Summary

Added a host-tested virtio-blk read queue planner that composes block
geometry/range validation with the caller-owned queue cursor publication path.

## Why

The cursor planner added deterministic available/used index state transitions,
but callers still needed a range-validated entry point from block-device
metadata to queue publication. This slice keeps that validation in the storage
contract path before descriptor selection or queue-memory writes.

## Files changed

- `src/OS.Kernel/Storage/VirtioBlockReadOnlyDevicePlan.cs`
- `tests/OS.HostTests/StorageVirtioBlockReadOnlyDevicePlanTests.cs`
- `docs/cstorage.md`
- `docs/drivers.md`
- `docs/completion-gates.md`
- `done/step377.md`

## Tests run

- `dotnet build .\tests\OS.HostTests\OS.HostTests.csproj --nologo`
- `dotnet run --project .\tests\OS.HostTests\OS.HostTests.csproj --no-build | Select-String -Pattern 'storage\.virtioblk\.readqueue|TEST_FAIL|TEST_PASS host\.tests'`
- `dotnet build .\OS.slnx --nologo -m:1`
- `git diff --check`

## Evidence

Focused host-test marker output:

```text
TEST_PASS storage.virtioblk.readqueue publishes valid block range
TEST_PASS storage.virtioblk.readqueue rejects out-of-range block request
TEST_PASS storage.virtioblk.readqueue preserves cursor on publish failure
TEST_PASS storage.virtioblk.readqueue preserves no-live-device boundary
TEST_PASS host.tests
```

The focused host build and full solution build passed with 0 warnings and 0
errors. `git diff --check` reported no whitespace errors; it only emitted
LF-to-CRLF working-copy warnings on the Windows host.

## Failures

None in the focused build, marker scan, full solution build, or diff check.

## Deferred work

- Add the matching read-queue completion path that composes a published
  range-validated read with cursor reclaim.
- Compose the read queue planner with future live virtio-blk notification,
  readiness, and completion waiting once BAR-backed register access, memory
  ordering, and DMA-safe queue ownership are defined.
- Do not treat this as live virtio-blk I/O, cloud readiness, hosted .NET 10
  readiness, ASP.NET Minimal API readiness, or Kestrel evidence.

## Evidence boundary

This proves only host-tested range-validated publication into caller-owned
virtio-blk queue memory, descriptor state, and cursor state. It rejects invalid
geometry and out-of-range block requests before touching request-header or
queue memory, and advances the caller-owned available index only after
successful publication. It does not allocate DMA-safe memory, map buffers for a
live device, publish descriptors to a live device-owned virtqueue, execute
MMIO, notify hardware, process interrupts, poll or wait for hardware, own a
full runtime descriptor allocator, read sectors, or prove QEMU/cloud, hosted
.NET 10, ASP.NET Minimal API, or Kestrel readiness.
