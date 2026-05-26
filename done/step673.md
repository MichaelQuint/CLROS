# Step 673 - Caller-Owned Boot Page Allocator Mutation

## Summary
Added a host-tested boot page allocator mutation boundary over caller-owned
free-list node storage.

## Why
The boot memory path had planning and state-initialization evidence, but no
small mutation contract for allocation/free behavior after the initial
caller-owned free-list state exists. This slice adds that boundary without
claiming live page ownership, paging, NX/W^X, or managed heap installation.

## Files changed
- `src/OS.Kernel/Memory/BootPageAllocatorMutation.cs`
- `tests/OS.HostTests/BootPageAllocatorMutationTests.cs`
- `docs/architecture.md`
- `docs/boot.md`
- `docs/completion-gates.md`

## Tests run
- `dotnet build .\tests\OS.HostTests\OS.HostTests.csproj --nologo`
- `dotnet run --project .\tests\OS.HostTests\OS.HostTests.csproj --no-build`
- `dotnet build .\OS.slnx --nologo -m:1`
- `.\scripts\check-naming.ps1`
- `.\scripts\run-qemu-smoke.ps1 -DryRun`
- `.\scripts\build-efi-shell.ps1`
- `git diff --check`

## Evidence
- Host-test build passed with 0 warnings and 0 errors.
- Full host tests ended with `TEST_PASS host.tests`.
- New memory markers include:
  - `TEST_PASS memory.boot allocator mutates allocation`
  - `TEST_PASS memory.boot allocator splits aligned range`
  - `TEST_PASS memory.boot allocator frees and coalesces`
  - `TEST_PASS memory.boot allocator frees empty list`
  - `TEST_PASS memory.boot allocator rejects invalid mutation`
  - `TEST_PASS memory.boot allocator mutation default result is not success`
  - `TEST_PASS memory.boot allocator mutation layouts contain no managed references`
- `dotnet build .\OS.slnx --nologo -m:1` passed with 0 warnings and 0 errors.
- `.\scripts\check-naming.ps1` emitted `TEST_PASS naming.guard` and
  `TEST_PASS source.firewall`.
- `.\scripts\run-qemu-smoke.ps1 -DryRun` emitted `TEST_PASS qemu.runner.dryrun`
  with the current hardened default required marker set.
- `.\scripts\build-efi-shell.ps1` emitted `TEST_PASS efi.shell.publish` and
  produced `artifacts/efi/BOOTX64.EFI`.
- `git diff --check` returned exit code 0 with only LF-to-CRLF warnings.

## Failures
- None in final verification.

## Deferred work
- `BootPageAllocatorMutation` mutates caller-owned `BootPageFreeListNode`
  metadata only. It does not reserve kernel-owned ranges from the live EFI map,
  expose a global page allocator, allocate/free live physical pages, configure
  paging, enforce NX/W^X, reserve managed heap pages, or satisfy NativeAOT
  object allocation helpers.

## Evidence Boundary
This step is host-tested memory/runtime contract evidence. It does not change
any completion gate to pass.
