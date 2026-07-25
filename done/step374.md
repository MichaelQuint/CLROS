# Step 374 - Virtio-blk free-list read publication

## Summary

Added a host-tested virtio-blk publish planner that composes caller-owned
descriptor free-list selection, read lifecycle publication, and descriptor
ownership marking.

## Why

Step 373 added reusable descriptor state primitives, but callers still had to
choose the read chain's first descriptor manually. This slice adds the narrow
composition needed for later queue ownership work while staying inside the
caller-owned memory and metadata boundary.

## Files changed

- `src/OS.Kernel/Storage/VirtioBlockDescriptorOwnershipPlan.cs`
- `tests/OS.HostTests/StorageVirtioBlockDescriptorOwnershipPlanTests.cs`
- `docs/cstorage.md`
- `docs/drivers.md`
- `docs/completion-gates.md`
- `done/step374.md`

## Tests run

- `dotnet build .\tests\OS.HostTests\OS.HostTests.csproj --nologo`
- `dotnet run --project .\tests\OS.HostTests\OS.HostTests.csproj --no-build | Select-String -Pattern 'storage\.virtioblk\.publishplan|TEST_FAIL|TEST_PASS host\.tests'`
- `dotnet build .\OS.slnx --nologo -m:1`
- `git diff --check`

## Evidence

Focused host-test marker output:

```text
TEST_PASS storage.virtioblk.publishplan publishes selected free chain
TEST_PASS storage.virtioblk.publishplan skips in-flight descriptors
TEST_PASS storage.virtioblk.publishplan rejects free-list exhaustion
TEST_PASS storage.virtioblk.publishplan preserves state on publish failure
TEST_PASS storage.virtioblk.publishplan rejects invalid ownership state
TEST_PASS storage.virtioblk.publishplan preserves no-live-device boundary
TEST_PASS host.tests
```

The focused host build and full solution build passed with 0 warnings and 0
errors. `git diff --check` reported no whitespace errors; it only emitted
LF-to-CRLF working-copy warnings on the Windows host.

## Failures

None in the focused build, marker scan, full solution build, or diff check.

## Deferred work

- Compose the publish planner with a future virtio-blk notification/readiness
  path after a live BAR-backed register accessor and real memory-ordering rule
  exist.
- Add live virtio-blk feature negotiation, config BAR reads, DMA-safe queue
  ownership, device notification, and completion waiting before treating this as
  a hardware block driver.
- Do not treat this as live virtio-blk I/O, cloud readiness, hosted .NET 10
  readiness, ASP.NET Minimal API readiness, or Kestrel evidence.

## Evidence boundary

This proves only host-tested virtio-blk free-list-to-read-publication
composition over caller-owned descriptor state, request-header storage,
destination/status buffers, and split-ring memory. It selects reusable
descriptors, publishes through the existing caller-owned lifecycle path, and
marks ownership only after successful publication. It does not allocate DMA-safe
memory, map buffers for a live device, publish descriptors to a live
device-owned virtqueue, execute MMIO, notify hardware, process interrupts, poll
or wait for hardware, own a full runtime descriptor allocator, read sectors, or
prove QEMU/cloud, hosted .NET 10, ASP.NET Minimal API, or Kestrel readiness.

