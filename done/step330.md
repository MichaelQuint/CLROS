# Step 330 - NativeAOT Heap Bootstrap Page-Budget Plan

## Summary

Added `NativeAotHeapBootstrapPlanner`, a metadata-only runtime prerequisite that validates an initialized boot page allocator state, requested future heap page budget, object alignment, remaining free-page metadata, and no-managed-reference result shape while keeping object allocation helper completion false.

## Why

The runtime ledger already recorded object allocation as blocked behind allocator and GC ownership. After boot page allocator-state metadata existed, the smallest next runtime slice was to define the page-budget boundary for a future managed heap without reserving pages, mutating allocator state, registering GC roots, adding write barriers, or claiming object allocation support.

## Files changed

- `src/OS.Kernel/Runtime/NativeAotHeapBootstrapPlan.cs`
- `tests/OS.HostTests/NativeAotHeapBootstrapPlanTests.cs`
- `src/OS.Kernel/Runtime/NativeAotHelperLedger.cs`
- `tests/OS.HostTests/NativeAotHelperLedgerTests.cs`
- `docs/boot.md`
- `docs/architecture.md`
- `docs/completion-gates.md`
- `docs/nativeaot-nostdlib-limits.md`
- `done/step330.md`

## Tests run

- `dotnet build .\tests\OS.HostTests\OS.HostTests.csproj --nologo`
- `dotnet run --project .\tests\OS.HostTests\OS.HostTests.csproj --no-build`
- `dotnet build .\OS.slnx --nologo -m:1`
- `.\scripts\check-naming.ps1`
- `.\scripts\run-qemu-smoke.ps1 -DryRun`
- `git diff --check`
- `rg --files src std apps | Where-Object { $_ -match '\.(c|cpp|h|asm|s)$' }`

## Evidence

- Focused host-test project build passed with 0 warnings and 0 errors.
- Full host harness passed and emitted:

```text
TEST_PASS runtime.nativeaot heap bootstrap plans page budget
TEST_PASS runtime.nativeaot heap bootstrap rejects allocator state
TEST_PASS runtime.nativeaot heap bootstrap rejects allocator byte metadata
TEST_PASS runtime.nativeaot heap bootstrap rejects page budget
TEST_PASS runtime.nativeaot heap bootstrap rejects object alignment
TEST_PASS runtime.nativeaot heap bootstrap layout contains no managed references
TEST_PASS runtime.nativeaot helper ledger structured columns
TEST_PASS host.tests
```

- Full solution build passed with 0 warnings and 0 errors.
- Naming guard and source firewall emitted `TEST_PASS naming.guard` and `TEST_PASS source.firewall`.
- QEMU dry-run emitted `TEST_PASS qemu.runner.dryrun` with the default `SingleMinimalApi` workload profile and default required marker list.
- `git diff --check` exited 0 with line-ending warnings only.
- Forbidden first-party source scan produced no `.c`, `.cpp`, `.h`, `.asm`, or `.s` files under `src`, `std`, or `apps`.

## Failures

- Gitea issue updates remain blocked by missing write credentials in the current task context; API write attempts return `token is required`.
- No page reservation, live allocator mutation, managed heap installation, GC root scanning, write-barrier implementation, NativeAOT object/array/string allocation helper, QEMU runtime-helper probe, or hosted .NET evidence is claimed for this slice.

## Deferred work

- Make `BOOT_PAGE_ALLOCATOR_READY` depend on initialized allocator state metadata instead of only planner output.
- Reserve future heap pages from live allocator-owned state before object allocation helper work.
- Define GC root registration/scanning and write-barrier ownership before changing runtime helper status from deferred.

