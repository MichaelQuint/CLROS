# Step 060 - IDTR Descriptor Load Plan Contract

## What Changed

- Added `IdtrDescriptor`, a host-testable x64 IDTR pointer contract.
- Added validation for nonzero base address and canonical 4096-byte IDT table length.
- Added deterministic packed 10-byte IDTR output into caller-provided storage.
- Added host tests for the 4096-byte table pointer, packed bytes, and invalid inputs.

## Why It Changed

Issue #80 needed the next data-layout step after `IdtTablePlan`: a validated IDTR pointer shape for the future privileged `lidt` transition. This keeps the work verifiable without executing privileged instructions.

## Files Changed

- `src/OS.Kernel/Diagnostics/IdtrDescriptor.cs`
- `tests/OS.HostTests/IdtrDescriptorTests.cs`
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
  - `TEST_PASS idtr descriptor creates 4096-byte table pointer`
  - `TEST_PASS idtr descriptor writes packed bytes`
  - `TEST_PASS idtr descriptor rejects invalid inputs`
  - `TEST_PASS host.tests`

## Failures

- No remaining test failures.

## Deferred Work

- This does not execute `lidt`.
- This does not install IDT handlers.
- This does not configure GDT/TSS/IST.
- This does not trigger CPU faults in QEMU.

## Evidence Boundary

This step proves the packed IDTR descriptor/load-plan contract only. It is not real IDT loading or hardware fault handling evidence.
