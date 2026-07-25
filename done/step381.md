# Step 381 - Virtio-blk notify relative-offset planner

## Summary

Added a host-tested virtio-blk notify-offset planner that derives a checked
notify-aperture relative offset from queue notification metadata and the virtio
notify offset multiplier.

## Why

Step 380 recorded block queue notification metadata, but a future register
window bridge needs a checked relative offset before it can bind the notify
write to a BAR aperture. This slice adds that arithmetic/provenance step
without mapping BARs or executing MMIO.

## Files changed

- `src/OS.Kernel/Storage/VirtioBlockReadOnlyDevicePlan.cs`
- `tests/OS.HostTests/StorageVirtioBlockReadOnlyDevicePlanTests.cs`
- `docs/cstorage.md`
- `docs/drivers.md`
- `docs/completion-gates.md`
- `done/step381.md`

## Tests run

- `dotnet build .\tests\OS.HostTests\OS.HostTests.csproj --nologo`
- `dotnet run --project .\tests\OS.HostTests\OS.HostTests.csproj --no-build | Select-String -Pattern 'storage\.virtioblk\.notifyoffset|TEST_FAIL|TEST_PASS host\.tests'`
- `dotnet build .\OS.slnx --nologo -m:1`
- `git diff --check`

## Evidence

Focused host-test marker output:

```text
TEST_PASS storage.virtioblk.notifyoffset derives relative offset
TEST_PASS storage.virtioblk.notifyoffset rejects failed notification metadata
TEST_PASS storage.virtioblk.notifyoffset rejects relative offset overflow
TEST_PASS storage.virtioblk.notifyoffset preserves no-live-device boundary
TEST_PASS host.tests
```

The focused host build and full solution build passed with 0 warnings and 0
errors. `git diff --check` reported no whitespace errors; it only emitted
LF-to-CRLF working-copy warnings on the Windows host.

## Failures

None in the focused build, marker scan, full solution build, or diff check.

## Deferred work

- Bridge the checked relative offset into a future BAR/register-window access
  plan once virtio-blk register-window bindings and real memory-ordering rules
  are represented.
- Keep notification execution separate from metadata and offset derivation until
  live MMIO ownership, DMA-safe queue ownership, and QEMU evidence exist.
- Do not treat this as live virtio-blk notification, live I/O, cloud readiness,
  hosted .NET 10 readiness, ASP.NET Minimal API readiness, or Kestrel evidence.

## Evidence boundary

This proves only host-tested arithmetic/provenance for deriving a future notify
relative offset. It does not read queue/status memory, mutate cursor or
descriptor state, allocate DMA-safe memory, map BARs or buffers for a live
device, execute MMIO, notify hardware, process interrupts, poll or wait for
hardware, own a full runtime descriptor allocator, read sectors, or prove
QEMU/cloud, hosted .NET 10, ASP.NET Minimal API, or Kestrel readiness.
