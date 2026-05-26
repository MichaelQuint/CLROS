# Step 543 - Virtio Block Descriptor Ownership Default Guards

## Summary
- Added explicit initialized-state gating to six virtio-blk descriptor ownership
  result structs:
  `VirtioBlockDescriptorFreeListResult`,
  `VirtioBlockDescriptorOwnershipPlanResult`,
  `VirtioBlockDescriptorPublishPlanResult`,
  `VirtioBlockDescriptorReclaimPlanResult`,
  `VirtioBlockQueueCursorPublishResult`, and
  `VirtioBlockQueueCursorReclaimResult`.
- Updated their success predicates so default/zero results cannot report
  success.
- Added host marker coverage for default result rejection across the ownership,
  free-list, publish, reclaim, and cursor path.
- Updated storage and completion-gate docs.

## Why
The scoped result enums use `Success = 0`, so default result structs could
previously satisfy status-only success checks without any caller-owned
descriptor-state operation running. The initialized-state guards prevent empty
caller state from becoming virtio-blk descriptor ownership evidence.

## Files changed
- `src/OS.Kernel/Storage/VirtioBlockDescriptorOwnershipPlan.cs`
- `tests/OS.HostTests/StorageVirtioBlockDescriptorOwnershipPlanTests.cs`
- `docs/cstorage.md`
- `docs/completion-gates.md`
- `done/step543.md`

## Tests run
- `dotnet build .\tests\OS.HostTests\OS.HostTests.csproj --nologo`
- `dotnet run --project .\tests\OS.HostTests\OS.HostTests.csproj --no-build`
  filtered for `storage.virtioblk.ownership`, `storage.virtioblk.freelist`,
  `storage.virtioblk.publishplan`, `storage.virtioblk.reclaimplan`,
  `storage.virtioblk.cursor`, `TEST_PASS host.tests`, and failures.
- `dotnet build .\OS.slnx --nologo -m:1`
- `.\scripts\check-naming.ps1`
- `git diff --check`

## Evidence
```text
dotnet build .\tests\OS.HostTests\OS.HostTests.csproj --nologo

Build succeeded.
    0 Warning(s)
    0 Error(s)
```

```text
TEST_START storage.virtioblk.ownership marks published descriptors in flight
TEST_PASS storage.virtioblk.ownership marks published descriptors in flight
TEST_START storage.virtioblk.ownership reclaims completed descriptors
TEST_PASS storage.virtioblk.ownership reclaims completed descriptors
TEST_START storage.virtioblk.freelist selects first free triple
TEST_PASS storage.virtioblk.freelist selects first free triple
TEST_START storage.virtioblk.publishplan publishes selected free chain
TEST_PASS storage.virtioblk.publishplan publishes selected free chain
TEST_START storage.virtioblk.reclaimplan composes publish completion ownership
TEST_PASS storage.virtioblk.reclaimplan composes publish completion ownership
TEST_START storage.virtioblk.cursor publishes and advances available index
TEST_PASS storage.virtioblk.cursor publishes and advances available index
TEST_START storage.virtioblk.ownership default results are not success
TEST_PASS storage.virtioblk.ownership default results are not success
TEST_PASS host.tests
```

The filtered host run also emitted the existing rejection and boundary markers
for failed publish/completion, short state tables, in-flight reuse,
not-in-flight reclaim, duplicate completion, free-list capacity exhaustion,
publish/reclaim failures, cursor failure preservation, and no-live-device
boundaries.

```text
dotnet build .\OS.slnx --nologo -m:1

Build succeeded.
    0 Warning(s)
    0 Error(s)
```

```text
TEST_START naming.guard
TEST_PASS naming.guard
TEST_START source.firewall
TEST_PASS source.firewall
```

```text
git diff --check
ExitCode: 0; existing LF-to-CRLF working-copy warnings only.
```

## Failures
- None for this slice.

## Deferred work
- This is host-tested caller-buffer descriptor ownership/planning evidence only.
- No live virtio-blk device, queue notification, MMIO, DMA, sector read,
  package loading from hardware, cloud readiness, Kestrel support, or production
  readiness is claimed.

