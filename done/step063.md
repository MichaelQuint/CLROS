# Step 063 - GDT Descriptor Plan Contract

## What Changed

- Added `GdtDescriptorPlan`, a host-testable x64 GDT byte-layout contract.
- Added deterministic descriptors for null, kernel code, kernel data, and available 64-bit TSS entries.
- Added selector constants for kernel code, kernel data, and TSS entries.
- Added status-coded validation for too-small output buffers, null TSS base, and invalid TSS limits.
- Added host tests for kernel segment encoding, TSS descriptor encoding, and invalid inputs.

## Why It Changed

Issue #81 needed GDT layout planning before any real IDT/GDT/TSS/IST installation work. This gives the future privileged path deterministic descriptor bytes without executing `lgdt`, `ltr`, or segment reloads.

## Files Changed

- `src/OS.Kernel/Diagnostics/GdtDescriptorPlan.cs`
- `tests/OS.HostTests/GdtDescriptorPlanTests.cs`
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
  - `TEST_PASS gdt descriptor plan encodes kernel segments`
  - `TEST_PASS gdt descriptor plan encodes tss descriptor`
  - `TEST_PASS gdt descriptor plan rejects invalid inputs`
  - `TEST_PASS host.tests`

## Failures

- No remaining test failures.

## Deferred Work

- This does not execute `lgdt`.
- This does not reload segment registers.
- This does not execute `ltr`.
- This does not install a live TSS/IST or fault handlers.

## Evidence Boundary

This step proves GDT descriptor byte layout only. It is not real descriptor-table installation or CPU fault handling evidence.
