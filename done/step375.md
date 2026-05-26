# Step 375 - Virtio-blk publish completion reclaim planner

## Summary

Added a host-tested virtio-blk reclaim planner that composes a successful
free-list publish plan with caller-owned used-ring/status-buffer completion
parsing and descriptor ownership reclaim.

## Why

Step 374 could select reusable descriptors and publish a read request into
caller-owned queue memory, but completion and descriptor reclaim still required
callers to manually stitch lifecycle completion and ownership state together.
This slice adds that small deterministic composer while staying inside
caller-owned memory and metadata.

## Files changed

- `src/OS.Kernel/Storage/VirtioBlockDescriptorOwnershipPlan.cs`
- `tests/OS.HostTests/StorageVirtioBlockDescriptorOwnershipPlanTests.cs`
- `docs/cstorage.md`
- `docs/drivers.md`
- `docs/completion-gates.md`
- `done/step375.md`

## Tests run

- `dotnet build .\tests\OS.HostTests\OS.HostTests.csproj --nologo`
- `dotnet run --project .\tests\OS.HostTests\OS.HostTests.csproj --no-build | Select-String -Pattern 'storage\.virtioblk\.reclaimplan|TEST_FAIL|TEST_PASS host\.tests'`
- `dotnet build .\OS.slnx --nologo -m:1`

## Evidence

Focused host-test marker output:

```text
TEST_PASS storage.virtioblk.reclaimplan composes publish completion ownership
TEST_PASS storage.virtioblk.reclaimplan rejects failed publish
TEST_PASS storage.virtioblk.reclaimplan rejects no completion
TEST_PASS storage.virtioblk.reclaimplan rejects ownership mismatch
TEST_PASS storage.virtioblk.reclaimplan preserves no-live-device boundary
TEST_PASS host.tests
```

The focused host build and full solution build passed with 0 warnings and 0
errors.

## Failures

An initial no-completion test fixture left `used.idx` at zero while the
published request used a nonzero previous used index, so the completion reader
correctly reported an unexpected used index instead of no completion. The test
fixture now writes the previous used index explicitly before verifying the
no-completion path.

## Deferred work

- Compose the caller-owned publish/reclaim planners with future live virtio-blk
  notification/readiness and completion-waiting once BAR-backed register access,
  memory ordering, and DMA-safe queue ownership are defined.
- Add live virtio-blk feature negotiation, config BAR reads, DMA-safe queue
  ownership, device notification, completion waiting, and boot-time package
  reads before treating this as a hardware block driver.
- Do not treat this as live virtio-blk I/O, cloud readiness, hosted .NET 10
  readiness, ASP.NET Minimal API readiness, or Kestrel evidence.

## Evidence boundary

This proves only host-tested virtio-blk publish-to-completion-to-reclaim
composition over caller-owned descriptor state, status buffer, and split-ring
memory. It rejects failed publish plans before reading completion state, rejects
missing completion before mutating descriptor state, and reclaims descriptors
only after successful completion parsing. It does not allocate DMA-safe memory,
map buffers for a live device, publish descriptors to a live device-owned
virtqueue, execute MMIO, notify hardware, process interrupts, poll or wait for
hardware, own a full runtime descriptor allocator, read sectors, or prove
QEMU/cloud, hosted .NET 10, ASP.NET Minimal API, or Kestrel readiness.
