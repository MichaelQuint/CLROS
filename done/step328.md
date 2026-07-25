# Step 328 - Boot Page Allocator State Metadata

## Summary

Added `BootPageAllocatorStateInitializer`, a host-tested metadata-only boundary that validates a successful boot page free-list node chain and returns value-type allocator state metadata.

## Why

The boot memory path had planner output and caller-owned free-list nodes, but no explicit state shape for the future allocator to install. This slice adds that state-validation boundary without allocating pages, mutating the node chain, wiring live EFI boot, changing paging, or claiming NativeAOT object allocation support.

## Files changed

- `src/OS.Kernel/Memory/BootPageAllocatorState.cs`
- `tests/OS.HostTests/BootPageAllocatorStateTests.cs`
- `src/OS.Kernel/Runtime/NativeAotHelperLedger.cs`
- `tests/OS.HostTests/NativeAotHelperLedgerTests.cs`
- `docs/boot.md`
- `docs/architecture.md`
- `docs/completion-gates.md`
- `docs/nativeaot-nostdlib-limits.md`
- `done/step328.md`

## Tests run

- `dotnet build .\tests\OS.HostTests\OS.HostTests.csproj --nologo`
- `dotnet run --project .\tests\OS.HostTests\OS.HostTests.csproj --no-build`
- `dotnet build .\OS.slnx --nologo -m:1`
- `git diff --check`
- `rg --files src std apps | Where-Object { $_ -match '\.(c|cpp|h|asm|s)$' }`

## Evidence

- Focused host-test project build passed with 0 warnings and 0 errors.
- Full host harness passed and emitted:

```text
TEST_PASS memory.boot allocator state initializes
TEST_PASS memory.boot allocator state rejects failed free-list plan
TEST_PASS memory.boot allocator state rejects empty free-list
TEST_PASS memory.boot allocator state rejects source capacity
TEST_PASS memory.boot allocator state rejects corrupted node shape
TEST_PASS memory.boot allocator state rejects invalid chain
TEST_PASS memory.boot allocator state layouts contain no managed references
TEST_PASS runtime.nativeaot helper ledger structured columns
TEST_PASS host.tests
```

- Full solution build passed with 0 warnings and 0 errors.
- `git diff --check` exited 0 with line-ending warnings only.
- Forbidden first-party source scan produced no `.c`, `.cpp`, `.h`, `.asm`, or `.s` files under `src`, `std`, or `apps`.

## Failures

- Gitea issue updates remain blocked by missing write credentials in the current task context; API write attempts return `token is required`.
- No QEMU, live EFI, real page allocation, paging, GC, or NativeAOT allocation-helper evidence is claimed for this slice.

## Deferred work

- Wire allocator-facing planning and state initialization into the live EFI boot path.
- Reserve kernel-owned ranges before installing mutable allocator state.
- Add a real mutable page allocate/free API over the state metadata.
- Add NativeAOT heap bootstrap planning, then object/array/string allocation helpers only after allocator and GC ownership are explicit.

