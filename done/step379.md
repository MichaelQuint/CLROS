# Step 379 - Virtio-blk read queue block-result projection

## Summary

Added a host-tested virtio-blk read queue block-result projector that maps
readqueue completion evidence into `BlockReadResult` metadata for a future
`IReadOnlyBlockDevice` adapter.

## Why

The range-validated readqueue publish/complete pair now carries enough nested
evidence to describe a completed read, but callers still need a stable
block-device result shape. This slice projects that evidence without touching
queue memory, cursor state, descriptors, or live device state.

## Files changed

- `src/OS.Kernel/Storage/VirtioBlockReadOnlyDevicePlan.cs`
- `tests/OS.HostTests/StorageVirtioBlockReadOnlyDevicePlanTests.cs`
- `docs/cstorage.md`
- `docs/drivers.md`
- `docs/completion-gates.md`
- `done/step379.md`

## Tests run

- `dotnet build .\tests\OS.HostTests\OS.HostTests.csproj --nologo`
- `dotnet run --project .\tests\OS.HostTests\OS.HostTests.csproj --no-build | Select-String -Pattern 'storage\.virtioblk\.readqueue block result|TEST_FAIL|TEST_PASS host\.tests'`
- `dotnet build .\OS.slnx --nologo -m:1`
- `git diff --check`

## Evidence

Focused host-test marker output:

```text
TEST_PASS storage.virtioblk.readqueue block result reports completed read
TEST_PASS storage.virtioblk.readqueue block result maps publication failure
TEST_PASS storage.virtioblk.readqueue block result maps completion failure
TEST_PASS storage.virtioblk.readqueue block result preserves no-live-device boundary
TEST_PASS host.tests
```

The focused host build and full solution build passed with 0 warnings and 0
errors. `git diff --check` reported no whitespace errors; it only emitted
LF-to-CRLF working-copy warnings on the Windows host.

## Failures

None in the focused build, marker scan, full solution build, or diff check.

## Deferred work

- Add a full `IReadOnlyBlockDevice`-shaped virtio-blk adapter only after live
  queue ownership, notification, and completion waiting are represented by
  tested contracts.
- Keep mapping validation failures to existing `BlockDeviceStatus` values and
  reserve `IoError` for completion/reclaim failures until a richer block-driver
  status model exists.
- Do not treat this as live virtio-blk I/O, cloud readiness, hosted .NET 10
  readiness, ASP.NET Minimal API readiness, or Kestrel evidence.

## Evidence boundary

This proves only host-tested projection from readqueue completion metadata into
`BlockReadResult`. It does not read queue/status memory, mutate cursor or
descriptor state, allocate DMA-safe memory, map buffers for a live device,
publish descriptors to a live device-owned virtqueue, execute MMIO, notify
hardware, process interrupts, poll or wait for hardware, own a full runtime
descriptor allocator, read sectors, or prove QEMU/cloud, hosted .NET 10, ASP.NET
Minimal API, or Kestrel readiness.
