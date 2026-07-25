# Step 090 - CPU Fault Entry Stub Byte Plan

## What Changed

- Added `FaultEntryStubPlan` for deterministic caller-owned byte plans for future `#UD` and `#PF` entry stubs.
- Added status-coded validation for vector metadata, error-code metadata, and output buffer size.
- Added host tests for invalid-opcode, page-fault, and unsupported-vector paths.

## Why It Changed

Issue #108 creates the next diagnostics byte-plan boundary before real IDT handler installation or CPU register handoff is attempted.

## Files Changed

- `src/OS.Kernel/Diagnostics/FaultEntryStubPlan.cs`
- `tests/OS.HostTests/FaultEntryStubPlanTests.cs`
- `docs/diagnostics.md`
- `docs/boot-order.md`
- `docs/completion-gates.md`

## Tests Run

```powershell
dotnet build .\OS.slnx --nologo -m:1
dotnet run --project .\tests\OS.HostTests\OS.HostTests.csproj --no-build
```

## Logs And Evidence

- Host tests emitted:
  - `TEST_PASS fault entry stub plan encodes invalid opcode frame handoff`
  - `TEST_PASS fault entry stub plan encodes page fault frame handoff`
  - `TEST_PASS fault entry stub plan rejects unsupported vector metadata`

## Failures

- No build or host-test failures after integration.

## Deferred Work

- No live IDT entry points.
- No stack switching.
- No CPU register capture.
- No managed panic handoff.

## Evidence Boundary

This step proves deterministic byte planning only. It is not real CPU exception handling or QEMU panic evidence.
