# Step 078 - Controlled Fault Probe Readiness Plan

## What Changed

- Added `FaultProbeReadiness`, a host-tested readiness contract for future controlled destructive fault probes.
- Validated invalid-opcode and null-write probe stubs can emit into caller-owned buffers.
- Validated destructive probe toggles remain disabled by default.
- Validated an `ExecutableStubMemoryPlanResult` is successful and large enough for both probe stubs.
- Added distinct status codes for enabled destructive defaults, invalid-opcode stub failure, null-write stub failure, unavailable memory plan, too-small memory plan, and ready.

## Why It Changed

Issue #95 needed a safe precondition boundary before any QEMU invalid-opcode or null-write fault probe can be wired into boot. The readiness contract keeps the destructive probe defaults explicit and blocks accidental execution paths.

## Files Changed

- `src/OS.Kernel/Diagnostics/FaultProbeReadiness.cs`
- `tests/OS.HostTests/FaultProbeReadinessTests.cs`
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
  - `TEST_PASS fault probe readiness accepts disabled planned probes`
  - `TEST_PASS fault probe readiness rejects enabled destructive defaults`
  - `TEST_PASS fault probe readiness rejects invalid stub readiness`
  - `TEST_PASS fault probe readiness rejects insufficient executable memory plan`
  - `TEST_PASS host.tests`

## Failures

- No build or host-test failures after integration.

## Deferred Work

- This does not execute `ud2`.
- This does not write to address zero.
- This does not install CPU fault handlers.
- This does not prove QEMU fault behavior.

## Evidence Boundary

This step proves host-side readiness behavior only. It is not controlled-fault execution, IDT/GDT/TSS/IST installation, panic-handler, or QEMU fault evidence.
