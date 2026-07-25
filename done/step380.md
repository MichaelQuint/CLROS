# Step 380 - Virtio-blk queue notification metadata planner

## Summary

Added a host-tested virtio-blk queue notification metadata planner that derives
future notify-write metadata from successful readqueue publication evidence.

## Why

The readqueue path can now validate, publish, complete, reclaim, and project a
block-read result over caller-owned memory, but the future live driver also
needs deterministic notification metadata before any BAR/register-window write
is allowed. This slice records that metadata without executing notification.

## Files changed

- `src/OS.Kernel/Storage/VirtioBlockReadOnlyDevicePlan.cs`
- `tests/OS.HostTests/StorageVirtioBlockReadOnlyDevicePlanTests.cs`
- `docs/cstorage.md`
- `docs/drivers.md`
- `docs/completion-gates.md`
- `done/step380.md`

## Tests run

- `dotnet build .\tests\OS.HostTests\OS.HostTests.csproj --nologo`
- `dotnet run --project .\tests\OS.HostTests\OS.HostTests.csproj --no-build | Select-String -Pattern 'storage\.virtioblk\.notify|TEST_FAIL|TEST_PASS host\.tests'`
- `dotnet build .\OS.slnx --nologo -m:1`
- `git diff --check`

## Evidence

Focused host-test marker output:

```text
TEST_PASS storage.virtioblk.notify plans readqueue notification metadata
TEST_PASS storage.virtioblk.notify rejects failed publication
TEST_PASS storage.virtioblk.notify preserves no-live-device boundary
TEST_PASS host.tests
```

The focused host build and full solution build passed with 0 warnings and 0
errors. `git diff --check` reported no whitespace errors; it only emitted
LF-to-CRLF working-copy warnings on the Windows host.

## Failures

None in the focused build, marker scan, full solution build, or diff check.

## Deferred work

- Bridge this metadata into a future BAR/register-window notification plan once
  virtio-blk register-window bindings and real memory-ordering rules are
  represented.
- Keep notification execution separate from metadata derivation until live MMIO
  ownership, DMA-safe queue ownership, and QEMU evidence exist.
- Do not treat this as live virtio-blk notification, live I/O, cloud readiness,
  hosted .NET 10 readiness, ASP.NET Minimal API readiness, or Kestrel evidence.

## Evidence boundary

This proves only host-tested notification metadata derivation from readqueue
publication evidence. It does not read queue/status memory, mutate cursor or
descriptor state, allocate DMA-safe memory, map BARs or buffers for a live
device, execute MMIO, notify hardware, process interrupts, poll or wait for
hardware, own a full runtime descriptor allocator, read sectors, or prove
QEMU/cloud, hosted .NET 10, ASP.NET Minimal API, or Kestrel readiness.
