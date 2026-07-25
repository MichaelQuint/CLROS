# Step 299 - Boot Free-List Planning Boundary

## What Changed

- Added `BootPageAllocatorPlanner` as the host-tested planning boundary after compact boot memory-region materialization and usable/reserved range selection.
- Added `BootPageFreeSegment` and status-coded `BootPageAllocatorPlanResult` output so future allocator code can consume caller-owned initial free-segment metadata without heap allocation.
- Added validation for page size, page alignment, zero page counts, byte-length and physical-end consistency, usable-range ordering/overlap, reserved-range ordering/overlap, reserved overlap with usable ranges, destination capacity, and arithmetic overflow.
- Added host tests for the required issue #299 free-list planning markers.
- Updated boot, architecture, and completion-gate docs to distinguish free-list planning from a live allocator.

## Why It Changed

Issue #299 needed the next narrow memory-management boundary after `BootMemoryRegionPlanner`: turn selected usable ranges into deterministic initial free-segment metadata without implementing page allocation, page freeing, paging structures, kernel reservation, NX/W^X enforcement, or live EFI integration.

## Files Changed

- `src/OS.Kernel/Memory/BootPageAllocatorPlan.cs`
- `tests/OS.HostTests/BootPageAllocatorPlanTests.cs`
- `docs/boot.md`
- `docs/architecture.md`
- `docs/completion-gates.md`
- `done/step299.md`

## Tests Run

```powershell
dotnet build .\OS.slnx --nologo -m:1
dotnet run --project .\tests\OS.HostTests\OS.HostTests.csproj --no-build
```

## Logs And Evidence

- `dotnet build .\OS.slnx --nologo -m:1` passed with 0 warnings and 0 errors.
- `dotnet run --project .\tests\OS.HostTests\OS.HostTests.csproj --no-build` wrote `artifacts/logs/host-tests-step299.log` and emitted `TEST_PASS host.tests`.
- The host-test run emitted the new free-list planning markers:

```text
TEST_PASS memory.boot freelist plans usable ranges
TEST_PASS memory.boot freelist rejects invalid page size
TEST_PASS memory.boot freelist rejects overlapping reservations
TEST_PASS memory.boot freelist reports destination capacity
TEST_PASS memory.boot freelist rejects arithmetic overflow
```

## Failures

- The first solution build succeeded but produced MSBuild retry warnings because stale parallel `OS.HostTests` processes had locked test output DLLs. Those stale process trees were stopped, and the clean rebuild passed with 0 warnings and 0 errors.
- An earlier broad host-test run emitted all new #299 markers but exited before `TEST_PASS host.tests` during an unrelated QEMU HTTP-probe test while stale test processes were still active. After the stale processes were stopped, the full host-test rerun passed.

## Deferred Work

- No page allocator, free-list mutation, allocation, deallocation, page ownership transfer, kernel image reservation, boot stack reservation, ACPI/framebuffer reservation, direct map, paging, NX/W^X enforcement, or `ExitBootServices` behavior was implemented.
- The live EFI path still materializes compact `BootMemoryRegionRecord` entries only. It does not run `BootMemoryRegionPlanner` or `BootPageAllocatorPlanner` in `EfiMain`.
- Reserved-range subtraction is intentionally not implemented. Any reserved range that overlaps a planned usable range is rejected deterministically until a later allocator reservation model exists.

## Evidence Boundary

This step proves that caller-owned selected memory-region metadata can be reduced into caller-owned initial free-segment metadata with deterministic status reporting and no allocation in the planning path. It does not prove live page allocation, paging, post-firmware memory ownership, driver DMA ownership, storage, networking, hosted .NET 10, ASP.NET Minimal API, or Kestrel behavior.
