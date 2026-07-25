# Step 675 - NativeAOT Helper Ledger Allocator Mutation Boundary

## Summary
Aligned the NativeAOT helper ledger with the host-tested
`BootPageAllocatorMutation` evidence boundary.

## Why
Issue #598 needed the runtime ledger to acknowledge the new caller-owned boot
allocator mutation slice without overstating NativeAOT runtime progress. This
keeps object, array, and string allocation helpers deferred while preserving the
mutation evidence as prerequisite metadata only.

## Files changed
- `src/OS.Kernel/Runtime/NativeAotHelperLedger.cs`
- `tests/OS.HostTests/NativeAotHelperLedgerTests.cs`
- `docs/nativeaot-nostdlib-limits.md`
- `docs/completion-gates.md`
- `done/step675.md`

## Tests run
- `dotnet build .\tests\OS.HostTests\OS.HostTests.csproj --nologo`
- `dotnet run --project .\artifacts\tmp\NativeAotLedgerProbe\NativeAotLedgerProbe.csproj --nologo`
- `dotnet run --project .\tests\OS.HostTests\OS.HostTests.csproj --no-build`

## Evidence
- Host-test project build passed with 0 warnings and 0 errors.
- Full host tests ended with `TEST_PASS host.tests`.
- Committed host tests emitted:
  - `TEST_PASS runtime.nativeaot helper ledger allocator mutation boundary`
- The focused temporary ledger probe emitted:
  - `TEST_START runtime.nativeaot helper ledger allocator mutation boundary`
  - `TEST_PASS runtime.nativeaot helper ledger allocator mutation boundary`
- `tests/OS.HostTests/NativeAotHelperLedgerTests.cs` now contains the same
  stable marker and asserts that `BootPageAllocatorMutation` is evidence for
  the object-allocation prerequisite boundary while object, array, and string
  helpers remain deferred and non-completing.
- The probe also asserted the public mutation boundary constants:
  `MutatesCallerOwnedFreeList=true`, `PerformsLiveAllocation=false`,
  `ConfiguresPaging=false`, and `EnforcesNxOrWx=false`.

## Failures
- None in the final integrated verification. An earlier full host attempt saw
  a stale dirty-tree compile failure in `BootSequence.cs` while the PAL marker
  helper was mid-edit; the integrated build and full host run passed afterward.

## Deferred work
- This is ledger/status alignment only. It does not implement NativeAOT object
  allocation, array allocation, string allocation, GC integration, write
  barriers, class constructors, exceptions/unwind, thread statics, hosted .NET,
  or Kestrel.
- `BootPageAllocatorMutation` remains caller-owned metadata mutation evidence.
  It does not reserve live pages, expose a global page allocator, configure
  paging, enforce NX/W^X, install a managed heap, or run in the live EFI path.

## Evidence Boundary
This is host-tested ledger/status-boundary evidence. It does not implement
NativeAOT object allocation, array allocation, string allocation, GC
integration, write barriers, class constructors, exception handling, thread
statics, hosted .NET, or Kestrel.

