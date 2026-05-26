# Step 331 - State-Gated Boot Page Allocator Readiness

## Summary

`BootSequence.RunBootPageAllocatorReadiness` now gates `BOOT_PAGE_ALLOCATOR_READY` on `BootPageAllocatorStateInitResult` instead of the earlier free-segment planner result. The marker metadata records state-initializer status, free-list status, node counts, initialization state, free/allocated page and byte totals, and page size. The default `BootPageAllocatorStateInitResult` no longer reports success unless it also contains initialized allocator-state metadata.

## Why

The boot allocator chain already had host-tested free-segment planning, caller-owned free-list node construction, and allocator-state initialization. The boot-order readiness marker still accepted planner output directly, which let the marker claim readiness before the state initializer proved an initialized allocator state. This slice tightens that boundary without adding allocation/free APIs or live EFI wiring.

## Files changed

- `src/OS.Kernel/Boot/BootSequence.cs`
- `src/OS.Kernel/Memory/BootPageAllocatorState.cs`
- `tests/OS.HostTests/BootSequencePageAllocatorReadinessTests.cs`
- `tests/OS.HostTests/BootPageAllocatorStateTests.cs`
- `docs/boot.md`
- `docs/boot-order.md`
- `docs/architecture.md`
- `docs/completion-gates.md`
- `docs/nativeaot-nostdlib-limits.md`
- `done/step331.md`

## Tests run

- `dotnet build .\tests\OS.HostTests\OS.HostTests.csproj --nologo`
- `dotnet run --project .\tests\OS.HostTests\OS.HostTests.csproj --no-build`
- `dotnet build .\OS.slnx --nologo -m:1`
- `.\scripts\check-naming.ps1`
- `.\scripts\run-qemu-smoke.ps1 -DryRun`
- `git diff --check`
- Forbidden first-party source scan under `src`, `apps`, and `std`

## Evidence

- Focused host-test build passed with 0 warnings and 0 errors.
- Full host-test run emitted `TEST_PASS host.tests`.
- New/updated allocator-state markers emitted:
  - `TEST_PASS memory.boot allocator state default result is not success`
  - `TEST_PASS boot.sequence page allocator readiness emits state ready marker`
  - `TEST_PASS boot.sequence page allocator readiness rejects state initialization failure`
  - `TEST_PASS boot.sequence page allocator readiness rejects default state`
  - `TEST_PASS boot.sequence page allocator readiness rejects empty state`
  - `TEST_PASS boot.sequence page allocator readiness preserves default markers`
- Full solution build passed with 0 warnings and 0 errors.
- Naming/source guard emitted `TEST_PASS naming.guard` and `TEST_PASS source.firewall`.
- QEMU dry run emitted `TEST_PASS qemu.runner.dryrun` with the default marker requirements unchanged.
- `git diff --check` exited 0; output only contained LF-to-CRLF working-copy warnings.
- Forbidden first-party source scan found no `.c`, `.cpp`, `.h`, `.asm`, or `.s` files under first-party source roots.

## Failures

- Gitea write/update remains blocked by missing credentials in the current task context; read-only issue queries work, but write attempts require a token.
- No real QEMU boot was run for this slice. The changed allocator readiness helper is host-tested only and is not wired into live `EfiMain`.

## Deferred work

- Wire boot page allocator readiness into the live boot path after the EFI path runs allocator-facing planning and owns the required storage.
- Install allocator-owned mutable free-list state before adding allocate/free page APIs.
- Reserve managed heap pages from live allocator state before changing any NativeAOT object allocation helper status.
