# Step 376 - Virtio-blk queue cursor planner

## Summary

Added a host-tested virtio-blk queue cursor planner that carries caller-owned
available-ring and used-ring indexes through the existing publish and reclaim
planners.

## Why

The descriptor publish and reclaim planners now compose the caller-owned queue
memory and descriptor-state transitions, but later runtime queue ownership also
needs deterministic cursor advancement. This slice adds a small value-type
cursor contract without introducing live device access or a runtime allocator.

## Files changed

- `src/OS.Kernel/Storage/VirtioBlockDescriptorOwnershipPlan.cs`
- `tests/OS.HostTests/StorageVirtioBlockDescriptorOwnershipPlanTests.cs`
- `docs/cstorage.md`
- `docs/drivers.md`
- `docs/completion-gates.md`
- `done/step376.md`

## Tests run

- `dotnet build .\tests\OS.HostTests\OS.HostTests.csproj --nologo`
- `dotnet run --project .\tests\OS.HostTests\OS.HostTests.csproj --no-build | Select-String -Pattern 'storage\.virtioblk\.cursor|TEST_FAIL|TEST_PASS host\.tests'`
- `dotnet build .\OS.slnx --nologo -m:1`
- `git diff --check`

## Evidence

Focused host-test marker output:

```text
TEST_PASS storage.virtioblk.cursor publishes and advances available index
TEST_PASS storage.virtioblk.cursor preserves index on publish failure
TEST_PASS storage.virtioblk.cursor reclaims and advances used index
TEST_PASS storage.virtioblk.cursor preserves used index on reclaim failure
TEST_PASS storage.virtioblk.cursor preserves no-live-device boundary
TEST_PASS host.tests
```

The focused host build and full solution build passed with 0 warnings and 0
errors. `git diff --check` reported no whitespace errors; it only emitted
LF-to-CRLF working-copy warnings on the Windows host.

## Failures

None in the focused build, marker scan, full solution build, or diff check.

## Deferred work

- Compose the cursor planner with future live virtio-blk notification/readiness
  and completion-waiting once BAR-backed register access, memory ordering, and
  DMA-safe queue ownership are defined.
- Add a real runtime queue allocator and descriptor ownership model before
  multiple outstanding block requests are allowed.
- Do not treat this as live virtio-blk I/O, cloud readiness, hosted .NET 10
  readiness, ASP.NET Minimal API readiness, or Kestrel evidence.

## Evidence boundary

This proves only host-tested caller-owned available/used cursor advancement
around the existing publish and reclaim planners. It advances the available
index only after successful publication and the used index only after successful
completion/reclaim. It does not allocate DMA-safe memory, map buffers for a live
device, publish descriptors to a live device-owned virtqueue, execute MMIO,
notify hardware, process interrupts, poll or wait for hardware, own a full
runtime descriptor allocator, read sectors, or prove QEMU/cloud, hosted .NET
10, ASP.NET Minimal API, or Kestrel readiness.
