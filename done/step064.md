# Step 064 - TSS And IST Layout Plan Contract

## What Changed

- Added `TssIstPlan`, a host-testable x64 TSS/IST byte-layout contract.
- Added deterministic `RSP0` and IST entry encoding into caller-provided TSS storage.
- Added `TssIstStackPlan` metadata for double-fault IST stack top, stack size, and guard size.
- Added status-coded validation for output capacity, IST index, null stack top, unaligned stack top, invalid stack size, and invalid guard size.
- Added host tests for double-fault IST encoding, `RSP0` encoding, and invalid input handling.

## Why It Changed

Issue #82 needed TSS/IST planning before a real double-fault path can be installed. This establishes the TSS byte layout and stack metadata rules without allocating stacks or executing `ltr`.

## Files Changed

- `src/OS.Kernel/Diagnostics/TssIstPlan.cs`
- `tests/OS.HostTests/TssIstPlanTests.cs`
- `docs/diagnostics.md`
- `docs/boot-order.md`
- `docs/completion-gates.md`

## Tests Run

```powershell
dotnet build .\OS.slnx --nologo -m:1
dotnet run --project .\tests\OS.HostTests\OS.HostTests.csproj --no-build
```

## Logs And Evidence

- Build passed with 0 warnings and 0 errors.
- Host tests emitted:
  - `TEST_PASS tss ist plan encodes double fault ist`
  - `TEST_PASS tss ist plan encodes rsp0`
  - `TEST_PASS tss ist plan rejects invalid inputs`
  - `TEST_PASS host.tests`

## Failures

- No remaining test failures.

## Deferred Work

- This does not allocate guarded stacks.
- This does not execute `ltr`.
- This does not install a live TSS or double-fault IST.
- This does not trigger CPU faults in QEMU.

## Evidence Boundary

This step proves TSS/IST byte-layout and stack metadata validation only. It is not live TSS, task-register, or double-fault handling evidence.
