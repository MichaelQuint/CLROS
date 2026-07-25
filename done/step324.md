# Step 324 - Boot Page Free-List Node Plan

## Summary

Added a host-tested boot page free-list node construction boundary after the existing free-segment planner. The new builder validates a successful `BootPageAllocatorPlanResult` plus caller-owned `BootPageFreeSegment` storage before writing deterministic `BootPageFreeListNode` metadata and next-node indexes into caller-owned storage.

## Why

The boot memory path already classifies compact UEFI memory records and plans initial free segments, but future allocator ownership needs one more explicit, allocation-free metadata shape before live heap/page allocation work can begin. This step keeps that boundary status-coded and host-testable without claiming a live allocator, object allocation helper, paging, or GC support.

## Files changed

- `src/OS.Kernel/Memory/BootPageFreeListPlan.cs`
- `tests/OS.HostTests/BootPageFreeListPlanTests.cs`
- `src/OS.Kernel/Runtime/NativeAotHelperLedger.cs`
- `tests/OS.HostTests/NativeAotHelperLedgerTests.cs`
- `docs/boot.md`
- `docs/completion-gates.md`
- `docs/architecture.md`
- `docs/nativeaot-nostdlib-limits.md`
- `done/step324.md`

## Tests run

- `dotnet build .\tests\OS.HostTests\OS.HostTests.csproj --nologo`
- `dotnet run --project .\tests\OS.HostTests\OS.HostTests.csproj --no-build`
- `dotnet build .\OS.slnx --nologo -m:1`
- `git diff --check`
- `rg --files src std apps | Where-Object { $_ -match '\.(c|cpp|h|asm|s)$' }`

## Evidence

- Focused host build passed with 0 warnings and 0 errors.
- Host tests passed and emitted:
  - `TEST_PASS memory.boot freelist nodes constructs`
  - `TEST_PASS memory.boot freelist nodes rejects failed planner`
  - `TEST_PASS memory.boot freelist nodes reports destination capacity`
  - `TEST_PASS memory.boot freelist nodes rejects invalid segment shape`
  - `TEST_PASS memory.boot freelist nodes rejects arithmetic overflow`
  - `TEST_PASS runtime.nativeaot helper ledger structured columns`
  - `TEST_PASS host.tests`
- Full solution build passed with 0 warnings and 0 errors.
- `git diff --check` exited 0 with line-ending warnings only.
- Forbidden first-party source scan produced no `.c`, `.cpp`, `.h`, `.asm`, or `.s` files under `src`, `std`, or `apps`.

## Failures

- None for this slice.
- Gitea issue comment/close updates remain blocked by missing API token in the current task context.

## Deferred work

- Wire allocator-facing planning into the live EFI boot path.
- Reserve kernel-owned ranges before installing allocator state.
- Convert caller-owned free-list node metadata into allocator-owned mutable state.
- Implement real page allocation, paging permissions, NX/W^X policy, and NativeAOT object/array/string allocation helpers with GC ownership.
