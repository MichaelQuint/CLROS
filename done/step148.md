# Step 148 - Virtqueue Split-Ring Layout Contract

## What Changed

- Added `OS.Drivers.VirtqueueSplitRingPlan` as a host-testable split-ring layout contract.
- The planner validates queue sizes as nonzero powers of two with a maximum of 32768 entries.
- The planner maps descriptor-table, available-ring, and used-ring offsets from a caller-owned start offset.
- The planner enforces descriptor/available/used alignment requirements of 16, 2, and 4 bytes.
- The planner reports required byte length and rejects offset arithmetic overflow with status-coded failures.
- Added host tests for the required Gitea issue #167 markers.
- Updated driver, storage, and completion-gate documentation.

## Why It Changed

Gitea issue #167 needs the virtio split-ring memory layout represented before any future virtio-blk queue publication or live device notification work. This keeps queue geometry separate from PCI discovery, feature negotiation, DMA allocation, interrupts, and disk I/O.

## Files Changed

- `src/OS.Kernel/Drivers/VirtqueueSplitRingPlan.cs`
- `tests/OS.HostTests/VirtqueueSplitRingPlanTests.cs`
- `docs/drivers.md`
- `docs/cstorage.md`
- `docs/completion-gates.md`
- `done/step148.md`

## Tests Run

```powershell
dotnet build .\src\OS.Kernel\OS.Kernel.csproj --nologo -m:1
dotnet build .\tests\OS.HostTests\OS.HostTests.csproj --nologo -m:1
dotnet run --project .\artifacts\tmp\virtqueue-focused\VirtqueueFocused.csproj --nologo --no-restore
dotnet run --project .\tests\OS.HostTests\OS.HostTests.csproj --no-build
```

## Logs And Evidence

- `dotnet build .\src\OS.Kernel\OS.Kernel.csproj --nologo -m:1` passed with 0 warnings and 0 errors.
- `dotnet build .\tests\OS.HostTests\OS.HostTests.csproj --nologo -m:1` passed with 0 warnings and 0 errors.
- A temporary ignored focused harness under `artifacts\tmp\virtqueue-focused` was created for marker evidence, run, and removed after verification. It emitted:
  - `TEST_PASS drivers.virtqueue split ring layout`
  - `TEST_PASS drivers.virtqueue rejects invalid queue size`
  - `TEST_PASS drivers.virtqueue maps descriptor avail used offsets`
  - `TEST_PASS drivers.virtqueue rejects overflow`

## Failures

- The first parallel kernel build attempt failed because the host-test build was simultaneously writing `src\OS.Kernel\obj\Debug\net10.0\OS.Kernel.dll`. A sequential retry passed.
- The broad host-test executable currently stops before the new virtqueue tests on unrelated existing ICMP failures: `TEST_FAIL net.icmp echo request round trip` and `TEST_FAIL net.icmp echo reply round trip`.

## Deferred Work

- No virtio PCI discovery was added in this slice.
- No feature negotiation with a live device was added.
- No DMA-safe allocation or queue memory publication was added.
- No queue notify, interrupt handling, or virtio-blk disk reads were added.

## Evidence Boundary

This step is host-tested layout evidence only. It does not prove a PCI device exists, a virtqueue was published, descriptors were DMA-visible, a device was notified, interrupts were handled, or sectors were read from a real disk.
