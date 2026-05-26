# Step 182 - Virtio-blk read lifecycle composition

## Issue

- Gitea issue: `#201`
- Scope: compose one virtio-blk read request lifecycle over caller-owned buffers.

## What Changed

- Added `OS.Storage.VirtioBlockReadLifecycle`.
- Added status-coded publish and completion result types.
- `PublishReadRequest` composes:
  - `VirtioBlockReadTransactionPlan`,
  - `VirtqueueSplitRingPlan`,
  - `VirtioBlockDescriptorChainPlan`,
  - `VirtioBlockDescriptorChainWriter`.
- `CompleteReadRequest` composes the published lifecycle metadata with `VirtioBlockUsedRingCompletionReader`.
- Lifecycle results expose:
  - nested read-plan, request-plan, queue-plan, descriptor-plan, descriptor-writer, and completion-reader statuses,
  - sector range,
  - data byte count,
  - descriptor head,
  - previous and next available-ring indexes,
  - previous, current, and next used-ring indexes,
  - used-ring slot, used-ring element offset, completed descriptor head, completed byte count, and status byte.
- Added host tests for successful publication, successful completion, wrapped ring-index carry-forward, read-plan failure, descriptor-writer failure, and completion-reader failure.
- Updated `docs/cstorage.md` to document the lifecycle contract and its evidence boundary.

## Why

The previous storage slices proved each virtio-blk read component independently. This step provides a narrow composition point for the future live virtio-blk driver without introducing device notification, MMIO polling, DMA ownership, interrupts, payload copying, or real disk reads.

## Files Changed

- `src/OS.Kernel/Storage/VirtioBlockReadLifecycle.cs`
- `tests/OS.HostTests/StorageVirtioBlockReadLifecycleTests.cs`
- `docs/cstorage.md`
- `done/step182.md`

## Tests And Evidence

Commands run:

```powershell
dotnet build .\OS.slnx --nologo -m:1
dotnet run --project .\tests\OS.HostTests\OS.HostTests.csproj --no-build
```

Evidence:

- `dotnet build .\OS.slnx --nologo -m:1` passed with 0 warnings and 0 errors.
- `dotnet run --project .\tests\OS.HostTests\OS.HostTests.csproj --no-build` passed and ended with `TEST_PASS host.tests`.

Host-test markers:

```text
TEST_PASS storage.virtioblk lifecycle publishes read request
TEST_PASS storage.virtioblk lifecycle completes read request
TEST_PASS storage.virtioblk lifecycle carries next ring indexes
TEST_PASS storage.virtioblk lifecycle reports read plan failure
TEST_PASS storage.virtioblk lifecycle reports descriptor write failure
TEST_PASS storage.virtioblk lifecycle reports completion failure
```

## Failures

- No failures were observed in this slice.

## Deferred Work

- Live virtio-blk PCI binding, feature negotiation, DMA-safe queue ownership, MMIO queue notification, interrupt or polling completion wait, payload copying into filesystem buffers, and QEMU disk-read evidence remain deferred.
- This step is host-test evidence only and does not prove that OS can read sectors from a real disk.
