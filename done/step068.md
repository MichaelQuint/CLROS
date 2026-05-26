# Step 068 - Descriptor Table Instruction Byte Plan

## What Changed

- Added `DescriptorTableInstructionPlan`, a host-tested C# contract for emitting x64 descriptor-table instruction bytes.
- Added deterministic byte plans for:
  - `lidt [rip+disp32]`
  - `lgdt [rip+disp32]`
  - `ltr ax`
- Used `OS.Std.Binary.EndianPrimitives` for little-endian displacement encoding.
- Added status-coded validation for unknown operations and too-small caller-owned output buffers.
- Added host tests for all emitted instruction forms and invalid inputs.

## Why It Changed

Issue #85 needed the future IDT/GDT/TSS installation path to have a reviewed, host-tested instruction-byte contract before any privileged execution is attempted. This keeps the next implementation slice small and preserves the project rule that low-level mechanisms remain C#-emitted rather than committed assembly.

## Files Changed

- `src/OS.Kernel/Diagnostics/DescriptorTableInstructionPlan.cs`
- `tests/OS.HostTests/DescriptorTableInstructionPlanTests.cs`
- `docs/diagnostics.md`
- `docs/drivers.md`
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
  - `TEST_PASS descriptor instruction plan encodes lidt`
  - `TEST_PASS descriptor instruction plan encodes lgdt`
  - `TEST_PASS descriptor instruction plan encodes ltr`
  - `TEST_PASS descriptor instruction plan rejects invalid inputs`
  - `TEST_PASS host.tests`

## Failures

- No remaining test failures.

## Deferred Work

- This does not execute `lidt`, `lgdt`, or `ltr`.
- This does not install a live IDT, GDT, TSS, or IST.
- This does not prove CPU fault handling in QEMU.

## Evidence Boundary

This step proves deterministic instruction-byte planning only. It is not descriptor-table installation, interrupt-handler, or hardware-fault evidence.
