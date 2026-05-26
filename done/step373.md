# Step 373 - Virtio-blk caller-owned descriptor ownership

## Summary

Added a host-tested virtio-blk descriptor ownership/free-list/reclaim contract
over caller-owned descriptor state.

## Why

The virtio-blk read path could publish a three-descriptor read chain and parse a
caller-owned used-ring completion, but it had no storage-side descriptor state
contract to prevent reusing in-flight descriptor chains. This slice adds that
state boundary without moving to live MMIO, DMA ownership, notification, or disk
I/O.

## Files changed

- `src/OS.Kernel/Storage/VirtioBlockDescriptorOwnershipPlan.cs`
- `tests/OS.HostTests/StorageVirtioBlockDescriptorOwnershipPlanTests.cs`
- `docs/cstorage.md`
- `docs/drivers.md`
- `docs/completion-gates.md`
- `done/step373.md`

## Tests run

- `dotnet build .\tests\OS.HostTests\OS.HostTests.csproj --nologo`
- `dotnet run --project .\tests\OS.HostTests\OS.HostTests.csproj --no-build | Select-String -Pattern 'storage\.virtioblk\.(ownership|freelist)|TEST_FAIL|TEST_PASS host\.tests'`
- `dotnet build .\OS.slnx --nologo -m:1`
- `git diff --check`

## Evidence

Focused host-test marker output:

```text
TEST_PASS storage.virtioblk.ownership marks published descriptors in flight
TEST_PASS storage.virtioblk.ownership reclaims completed descriptors
TEST_PASS storage.virtioblk.ownership rejects failed publish
TEST_PASS storage.virtioblk.ownership rejects failed completion
TEST_PASS storage.virtioblk.ownership rejects short state table
TEST_PASS storage.virtioblk.ownership rejects descriptor already in flight
TEST_PASS storage.virtioblk.ownership rejects descriptor not in flight
TEST_PASS storage.virtioblk.ownership rejects duplicate completion
TEST_PASS storage.virtioblk.ownership preserves no-live-device boundary
TEST_PASS storage.virtioblk.freelist selects first free triple
TEST_PASS storage.virtioblk.freelist skips in-flight descriptors
TEST_PASS storage.virtioblk.freelist reports capacity exhaustion
TEST_PASS storage.virtioblk.freelist rejects invalid state
TEST_PASS storage.virtioblk.freelist preserves no-live-device boundary
TEST_PASS host.tests
```

The focused host build and full solution build passed with 0 warnings and 0
errors. `git diff --check` reported no whitespace errors; it only emitted
LF-to-CRLF working-copy warnings on the Windows host.

## Failures

None in the focused build, marker scan, full solution build, or diff check.

## Deferred work

- Compose the free-list selector directly with `VirtioBlockReadLifecycle` after
  live queue ownership rules define descriptor allocation and reuse policy.
- Add live virtio-blk feature negotiation, config BAR reads, DMA-safe queue
  ownership, device notification, and completion waiting before treating this as
  a hardware block driver.
- Do not treat this as live virtio-blk I/O, cloud readiness, hosted .NET 10
  readiness, ASP.NET Minimal API readiness, or Kestrel evidence.

## Evidence boundary

This proves only host-tested virtio-blk descriptor ownership, free-list
selection, and completion reclaim state over caller-owned descriptor-state
storage and caller-owned split-ring/completion metadata. It proves deterministic
`Free`/`InFlight` transitions, first-free descriptor-chain selection, state
preservation on failed publication or completion, and reclaim after a validated
completion, but it does not allocate DMA-safe memory, map buffers for a live
device, publish descriptors to a live device-owned virtqueue, execute MMIO,
notify hardware, process interrupts, poll or wait for hardware, own a full
runtime descriptor allocator, read sectors, or prove QEMU/cloud, hosted .NET 10,
ASP.NET Minimal API, or Kestrel readiness.

