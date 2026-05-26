# Step 072 - Controlled Fault Probe Stub Byte Plan

## What Changed

- Added `FaultProbeStubPlan`, a host-tested C# byte-plan contract for future destructive QEMU fault probes.
- Added deterministic non-executed byte plans for:
  - invalid opcode probe: `ud2; ret`;
  - null-write probe: byte write to address zero followed by `ret`.
- Tied probe metadata to the existing disabled-by-default destructive panic probe toggles.
- Added status-coded validation for unknown probe kind and too-small caller-owned buffers.
- Added host tests for both probe stubs, disabled defaults, and invalid inputs.

## Why It Changed

Issue #89 needed a safe intermediate step toward null-pointer and invalid-opcode QEMU probes. The byte plans let the project review and test destructive probe payloads without executing them or wiring them into the boot path.

## Files Changed

- `src/OS.Kernel/Diagnostics/FaultProbeStubPlan.cs`
- `tests/OS.HostTests/FaultProbeStubPlanTests.cs`
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
  - `TEST_PASS fault probe stub plan encodes invalid opcode probe`
  - `TEST_PASS fault probe stub plan encodes null write probe`
  - `TEST_PASS fault probe stub plan keeps destructive probes disabled by default`
  - `TEST_PASS fault probe stub plan rejects invalid inputs`
  - `TEST_PASS host.tests`

## Failures

- No remaining test failures.

## Deferred Work

- This does not execute `ud2`.
- This does not write to address zero.
- This does not wire destructive probes into the live boot path.
- This does not prove readable QEMU panic diagnostics for faults.

## Evidence Boundary

This step proves deterministic destructive fault-probe byte planning only. It is not QEMU fault, panic-handler, IDT, or register-capture evidence.
