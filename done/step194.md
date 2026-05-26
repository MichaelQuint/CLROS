# Step 194 - Virtio Block Read-Only Device Planner

## What Changed

- Added `VirtioBlockReadOnlyDevicePlan` for issue #213.
- Validated `BlockDeviceInfo` geometry before publishing a virtio-blk read request. The planner accepts only the current virtio 512-byte sector geometry and rejects empty/default geometry.
- Rejected zero, at-end, and crossing-end sector ranges before queue publication.
- Composed valid read requests through `VirtioBlockReadLifecycle.PublishReadRequest` over caller-owned request-header, data, status, and split-ring memory spans.
- Preserved nested lifecycle statuses and request metadata: block status, lifecycle publish status, read/request/queue/descriptor/writer statuses, sector range, byte counts, descriptor head, and ring-index carry-forward.
- Added host tests for success, geometry rejection, range rejection, lifecycle publish failure, and metadata preservation.
- Updated `docs/cstorage.md` with the narrow read-only planner status and evidence boundary.

## Why

The storage path had block-device geometry contracts and virtio-blk lifecycle publication, but no small adapter that treated a parsed virtio-blk capacity as a read-only block device request source. This step provides that bridge without introducing live device notification, completion polling, payload copying, or real disk I/O.

## Files Changed

- `src/OS.Kernel/Storage/VirtioBlockReadOnlyDevicePlan.cs`
- `tests/OS.HostTests/StorageVirtioBlockReadOnlyDevicePlanTests.cs`
- `docs/cstorage.md`
- `done/step194.md`

## Evidence

Verification commands for this slice:

- `dotnet build .\OS.slnx --nologo -m:1`
- `dotnet run --project .\tests\OS.HostTests\OS.HostTests.csproj --no-build`
- `.\scripts\check-naming.ps1`
- `git diff --check`

Required issue #213 markers:

- `TEST_PASS storage.virtioblk.readonly publishes sector read`
- `TEST_PASS storage.virtioblk.readonly rejects invalid block geometry`
- `TEST_PASS storage.virtioblk.readonly rejects out-of-range sector`
- `TEST_PASS storage.virtioblk.readonly reports lifecycle publish failure`
- `TEST_PASS storage.virtioblk.readonly preserves request metadata`

## Failures

- No failures are currently recorded for this slice.

## Deferred Work

- No device notification is performed.
- No used-ring polling or completion wait is performed.
- No payload bytes are copied out of the virtio data buffer.
- No live PCI/MMIO virtio-blk device is touched.
- No QEMU disk-read, filesystem read, hosted .NET 10, Minimal API, or Kestrel behavior is proven by this step.

## Evidence Boundary

This is host-tested planning evidence over caller-owned spans only. It proves that OS can validate a read-only virtio-blk sector request against `BlockDeviceInfo` and publish one request through the existing lifecycle coordinator. It does not prove that a virtio-blk device received, completed, or served the request.
