# Step 056 - IDT Descriptor And Fault Vector Contracts

## What Changed

- Added `IdtGateDescriptor`, a host-testable x64 interrupt/trap gate descriptor contract.
- Added status-coded descriptor validation for vector, handler address, code selector, IST index, gate type, and descriptor privilege level.
- Added `FaultVectorCatalog`, including required exception vector metadata and error-code policy.
- Added host tests for deterministic descriptor encoding and page-fault metadata.

## Why It Changed

Issue #73 needed the descriptor/vector contract before real IDT setup. This gives future fault-handler work a tested shape without loading an IDT, installing handlers, or triggering faults.

## Files Changed

- `src/OS.Kernel/Diagnostics/IdtGateDescriptor.cs`
- `src/OS.Kernel/Diagnostics/FaultVectorInfo.cs`
- `tests/OS.HostTests/IdtContractTests.cs`
- `docs/diagnostics.md`
- `docs/boot-order.md`
- `docs/completion-gates.md`
- `docs/drivers.md`

## Tests Run

```powershell
dotnet build .\OS.slnx --nologo -m:1
dotnet run --project .\tests\OS.HostTests\OS.HostTests.csproj --no-build
.\scripts\check-naming.ps1
.\scripts\run-qemu-smoke.ps1 -DryRun
git diff --check
```

## Logs And Evidence

- Build passed with 0 warnings and 0 errors.
- Host tests emitted:
  - `TEST_PASS idt descriptor encodes interrupt gate`
  - `TEST_PASS idt descriptor rejects invalid ist index`
  - `TEST_PASS idt descriptor rejects null handler address`
  - `TEST_PASS fault vectors expose error code policy`
  - `TEST_PASS fault vectors include page fault metadata`
  - `TEST_PASS host.tests`
- `scripts/check-naming.ps1` emitted `TEST_PASS naming.guard` and `TEST_PASS source.firewall`.
- `scripts/run-qemu-smoke.ps1 -DryRun` emitted `TEST_PASS qemu.runner.dryrun`.
- `git diff --check` exited 0 with line-ending normalization warnings only.

## Failures

- No remaining test failures.

## Deferred Work

- This does not execute `lidt`.
- This does not allocate a live IDT table, install handlers, set up GDT/TSS/IST, or trigger real CPU faults.
- Page-fault, invalid-opcode, and double-fault QEMU probes remain future work.

## Evidence Boundary

This step proves deterministic host contracts for IDT entries and exception-vector metadata. It is not hardware fault handling evidence.
