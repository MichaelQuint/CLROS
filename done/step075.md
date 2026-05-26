# Step 075 - Descriptor Table Install Readiness Plan

## What Changed

- Added `DescriptorTableInstallReadiness`, a host-tested readiness contract for future descriptor-table installation.
- Validates:
  - IDT table readiness;
  - IDTR readiness;
  - GDT descriptor readiness;
  - TSS/IST readiness;
  - double-fault IST readiness;
  - callable `lidt`, `lgdt`, and `ltr` install-stub buffer capacity.
- Added distinct status codes for each readiness failure class.
- Added host tests for complete prerequisites and representative failure paths.

## Why It Changed

Issue #93 needed a single readiness gate that aggregates the descriptor, table, TSS/IST, and install-stub contracts before any live `lidt`, `lgdt`, or `ltr` path is attempted.

## Files Changed

- `src/OS.Kernel/Diagnostics/DescriptorTableInstallReadiness.cs`
- `tests/OS.HostTests/DescriptorTableInstallReadinessTests.cs`
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
  - `TEST_PASS descriptor install readiness accepts complete prerequisites`
  - `TEST_PASS descriptor install readiness rejects missing idt readiness`
  - `TEST_PASS descriptor install readiness rejects missing tss ist readiness`
  - `TEST_PASS descriptor install readiness rejects insufficient stub buffers`
  - `TEST_PASS host.tests`

## Failures

- No remaining test failures.

## Deferred Work

- This does not execute `lidt`, `lgdt`, or `ltr`.
- This does not allocate executable memory.
- This does not install a live IDT, GDT, TSS, or IST.
- This does not prove CPU fault handling in QEMU.

## Evidence Boundary

This step proves host-side descriptor-table installation prerequisite validation only. It is not descriptor-table installation, interrupt-handler, executable-memory, or hardware-fault evidence.
