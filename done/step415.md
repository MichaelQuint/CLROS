# Step 415 - Boot Page Allocator Readiness Composition

## Summary

Added a host-tested `BootPageAllocatorReadinessComposer` that composes compact
boot memory-region records through the existing usable/reserved range planner,
free-segment planner, caller-owned free-list node builder, allocator-state
initializer, and `BootSequence.RunBootPageAllocatorReadiness`.

The slice is issue #400 evidence only. It is host-tested composition over
caller-owned buffers, not live page allocation, paging, managed heap readiness,
real `ExitBootServices`, cloud readiness, or production readiness.

## Why

The boot/runtime path already had individually tested planner and readiness
pieces. This step proves they can be wired together behind one status-coded
boundary before attempting to run allocator readiness from the live EFI path.

## Files changed

- `.gitignore`
- `scripts/check-naming.ps1`
- `src/OS.Kernel/Memory/BootPageAllocatorReadinessComposer.cs`
- `tests/OS.HostTests/BootPageAllocatorReadinessComposerTests.cs`
- `docs/boot.md`
- `docs/nativeaot-nostdlib-limits.md`
- `docs/completion-gates.md`
- `done/step415.md`

## Tests run

- `dotnet build .\tests\OS.HostTests\OS.HostTests.csproj --nologo`
- `dotnet run --project .\tests\OS.HostTests\OS.HostTests.csproj --no-build`
- `dotnet build .\OS.slnx --nologo -m:1`
- `.\scripts\check-naming.ps1`
- `git diff --check`

## Evidence

- Host build passed with 0 warnings and 0 errors.
- Full host harness ended with `TEST_PASS host.tests`.
- New #400 markers passed:
  - `TEST_PASS memory.boot allocator readiness composes`
  - `TEST_PASS memory.boot allocator readiness reports scratch capacity`
  - `TEST_PASS memory.boot allocator readiness rejects invalid regions`
  - `TEST_PASS memory.boot allocator readiness emits boot marker metadata`
- Full solution build passed with 0 warnings and 0 errors.
- `.\scripts\check-naming.ps1` emitted `TEST_PASS naming.guard` and
  `TEST_PASS source.firewall`.
- `git diff --check` exited 0 with CRLF normalization warnings only.

## Failures

- The first full host run failed at `source firewall accepts current tree`
  because the local Claude worktree under `.claude\worktrees\issue-401`
  was being scanned by `scripts/check-naming.ps1`. `.claude/` is now ignored
  by Git and excluded by the naming/source firewall because it is local
  subagent scratch state, not repository source.
- The first focused build during this slice failed before final verification
  because the new composer file used diagnostics types without importing
  `OS.Kernel.Diagnostics`. The missing using was added before the successful
  build/test runs above.

## Deferred work

- Wire allocator readiness into the live EFI boot path after owned serial and
  post-firmware preflight requirements are ready.
- Reserve kernel-owned ranges before installing allocator-owned mutable state.
- Add live allocation/free, paging, NX/W^X, managed heap reservation, and
  NativeAOT allocation-helper evidence in later runtime slices.

## Evidence boundary

This step proves only host-tested composition and marker propagation over
caller-owned buffers. It does not prove live boot allocator ownership, live
page allocation, heap bootstrap, GC, write barriers, hosted .NET 10, Minimal
API/Kestrel, Azure/AWS readiness, or production readiness.
