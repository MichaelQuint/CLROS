# Step 071 - Descriptor Table Install Stub Byte Plan

## What Changed

- Added `DescriptorTableInstallStubPlan`, a host-tested C# byte-plan contract for future callable descriptor-table install stubs.
- Added deterministic callable stubs for:
  - `lidt` with an embedded 10-byte IDTR payload;
  - `lgdt` with an embedded 10-byte GDTR payload;
  - `ltr` with a caller-provided TSS selector loaded into `AX`.
- Reused `DescriptorTableInstructionPlan` for the privileged instruction bytes.
- Added status-coded validation for invalid operation kinds, invalid descriptor payload length, invalid selector, and too-small caller-owned buffers.
- Added host tests for all stub forms and invalid inputs.

## Why It Changed

Issue #88 needed the future descriptor-table installation path to have a reviewed callable-stub byte contract before any privileged execution or executable memory work starts. This preserves the C#-only low-level mechanism rule while keeping the evidence limited to deterministic byte planning.

## Files Changed

- `src/OS.Kernel/Diagnostics/DescriptorTableInstallStubPlan.cs`
- `tests/OS.HostTests/DescriptorTableInstallStubPlanTests.cs`
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
  - `TEST_PASS descriptor install stub plan encodes lidt stub`
  - `TEST_PASS descriptor install stub plan encodes lgdt stub`
  - `TEST_PASS descriptor install stub plan encodes ltr stub`
  - `TEST_PASS descriptor install stub plan rejects invalid inputs`
  - `TEST_PASS host.tests`

## Failures

- No remaining test failures.

## Deferred Work

- This does not execute `lidt`, `lgdt`, or `ltr`.
- This does not allocate executable memory or define the W^X transition policy.
- This does not install a live IDT, GDT, TSS, or IST.
- This does not prove CPU fault handling in QEMU.

## Evidence Boundary

This step proves deterministic callable descriptor-table install stub byte planning only. It is not descriptor-table installation, interrupt-handler, executable-memory, or hardware-fault evidence.
