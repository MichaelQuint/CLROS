# Step 057 - IDT Required Fault Table Plan

## What Changed

- Added `IdtTablePlan`, a host-testable 256-entry IDT table planning contract.
- Bound required early fault vectors `#DE`, `#UD`, `#DF`, `#NM`, `#TS`, `#NP`, `#SS`, `#GP`, and `#PF` into caller-provided descriptor storage.
- Required a nonzero IST index for the double-fault vector.
- Added deterministic 4096-byte descriptor export into caller-provided byte storage.
- Added host tests for valid required vectors, missing required vectors, double-fault IST policy, and descriptor byte export.

## Why It Changed

Issue #75 needed the next step after descriptor metadata: table-level planning for required CPU faults. This keeps the work host-testable while still avoiding `lidt`, handler installation, TSS/IST setup, or destructive QEMU fault probes.

## Files Changed

- `src/OS.Kernel/Diagnostics/IdtTablePlan.cs`
- `tests/OS.HostTests/IdtTablePlanTests.cs`
- `docs/diagnostics.md`
- `docs/boot-order.md`
- `docs/completion-gates.md`
- `docs/drivers.md`

## Tests Run

```powershell
dotnet build .\OS.slnx --nologo -m:1
dotnet run --project .\tests\OS.HostTests\OS.HostTests.csproj --no-build
```

## Logs And Evidence

- Build passed with 0 warnings and 0 errors.
- Host tests emitted:
  - `TEST_PASS idt table plan binds required fault vectors`
  - `TEST_PASS idt table plan rejects missing required vector`
  - `TEST_PASS idt table plan requires double fault ist`
  - `TEST_PASS idt table plan writes descriptor bytes`
  - `TEST_PASS host.tests`

## Failures

- No remaining test failures.

## Deferred Work

- This does not execute `lidt`.
- This does not install CPU fault handlers.
- This does not configure GDT/TSS/IST.
- This does not prove QEMU page-fault, invalid-opcode, or double-fault behavior.

## Evidence Boundary

This step proves IDT table layout planning and descriptor export only. It is not real interrupt or fault handling evidence.
