# Step 031 - NativeAOT Helper Ledger

## What Changed

- Added a host-testable NativeAOT helper ledger data model in `OS.Kernel`.
- Listed object allocation, array allocation, string allocation, write barriers, interface dispatch, class constructors, thread statics, exception/unwind, module initialization, TypeManager/RTR scans, GC stack roots, EFI-only reverse P/Invoke helpers, and EFI-only fail-fast type-cast helpers.
- Recorded status, evidence, next step, EFI-only scope, and completion-gate applicability for each helper area.
- Added host tests proving incomplete runtime helpers are not marked complete and EFI-only helpers do not satisfy kernel/runtime completion.
- Updated runtime limits and completion-gate docs with the new ledger boundary.

## Why It Changed

Runtime-helper work needs a precise ledger before helper implementation begins. This step makes the current gaps machine-testable without pretending exception handling, GC, class constructors, thread statics, allocation, or interface dispatch are implemented.

## Files Changed

- `src/OS.Kernel/Runtime/NativeAotHelperLedger.cs`
- `tests/OS.HostTests/NativeAotHelperLedgerTests.cs`
- `docs/nativeaot-nostdlib-limits.md`
- `docs/completion-gates.md`
- `done/step031.md`

## Tests Run

```powershell
dotnet build .\OS.slnx --nologo -m:1
dotnet run --project .\tests\OS.HostTests\OS.HostTests.csproj --no-build
git diff --check -- .\src\OS.Kernel\Runtime\NativeAotHelperLedger.cs .\tests\OS.HostTests\NativeAotHelperLedgerTests.cs .\docs\nativeaot-nostdlib-limits.md .\docs\completion-gates.md .\done\step031.md
```

## Evidence

- Host test marker: `TEST_PASS runtime.nativeaot helper ledger`
- Full host test marker: `TEST_PASS host.tests`

## Failures

None for this scoped ledger slice.

## Deferred Work

- Object, array, and string allocation helpers.
- Write barriers and managed GC root scanning.
- Interface dispatch and real type-cast helper behavior.
- Class constructor runner and thread-static support.
- Managed exception catch/finally/unwind support.
- Module initialization and TypeManager/RTR scans.
- QEMU runtime-helper probes once helpers move from ledger to implementation.

## Evidence Boundary

This step proves the NativeAOT helper inventory and current incomplete status only. It does not implement any runtime helper and does not claim exception handling, GC, class constructors, thread statics, allocation, interface dispatch, hosted .NET 10, ASP.NET Minimal API, or Kestrel support.
