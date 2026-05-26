# Step 304 - Boot Page Allocator Readiness Marker

## What Changed

- Added `BootSequence.RunBootPageAllocatorReadiness` as a host-test-only ordered boot boundary over `BootPageAllocatorPlanResult`.
- Added stable marker creation for:
  - `BOOT_PAGE_ALLOCATOR_READY status=Ready plan=Success ...`
  - `BOOT_PAGE_ALLOCATOR_NOT_READY status=<reason> plan=<planner-status> ...`
- Added planner metadata to both marker forms: usable/reserved range counts, free-segment counts, required/available free-segment capacity, total free pages/bytes, page size, and invalid range indexes.
- Added focused host tests for ready, planner-failed, empty-success, and default boot-marker preservation behavior.
- Updated boot, architecture, and completion-gate documentation to record the new readiness boundary and its host-only evidence status.

## Why It Changed

The boot path already had host-tested region planning and free-segment planning, but there was no explicit `BootSequence` readiness marker that a future ordered boot stage could consume before moving toward a live boot page allocator. This step adds that boundary without allocating pages, changing page tables, reserving kernel ranges, wiring `EfiMain`, or mutating a real free list.

## Files Changed

- `src/OS.Kernel/Boot/BootSequence.cs`
- `tests/OS.HostTests/BootSequencePageAllocatorReadinessTests.cs`
- `docs/boot.md`
- `docs/architecture.md`
- `docs/completion-gates.md`
- `done/step304.md`

## Tests Run

```powershell
dotnet build .\OS.slnx --nologo -m:1
dotnet run --project .\tests\OS.HostTests\OS.HostTests.csproj --no-build
```

Both commands passed.

## Evidence

Build evidence:

- `dotnet build .\OS.slnx --nologo -m:1` completed with `0 Warning(s), 0 Error(s)`.

Host-test evidence:

```text
TEST_PASS boot.sequence page allocator readiness emits ready marker
TEST_PASS boot.sequence page allocator readiness rejects planner failure
TEST_PASS boot.sequence page allocator readiness rejects empty success
TEST_PASS boot.sequence page allocator readiness preserves default markers
TEST_PASS host.tests
```

The ready marker includes `status=Ready plan=Success freeSegments=<n> requiredFreeSegments=<n> availableFreeSegments=<n> totalFreePages=<n> totalFreeBytes=<n>`.

The not-ready marker includes `status=PlannerFailed` or `status=NoFreeSegments` plus the original planner status and capacity metadata.

## Failures

No failures were observed in this slice.

## Deferred Work

- Wire the readiness gate into the live ordered boot path after allocator-facing planning runs from live compact boot memory regions.
- Reserve kernel-owned ranges before marking the allocator ready in the live path.
- Build a mutable allocator free list.
- Implement page allocation/freeing, page-table integration, NX/W^X policy enforcement, and post-`ExitBootServices` memory ownership.
- Add QEMU evidence only after the live EFI path reaches the allocator-facing planner without using managed allocation.
