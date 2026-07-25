# Step 066 - Boot Fault Infrastructure Readiness Marker

## What Changed

- Added `BootSequence.RunFaultInfrastructureReadiness`, a host-tested readiness-marker helper.
- Added stable marker constants:
  - `FAULT_INFRA_READY`
  - `FAULT_INFRA_NOT_READY status=<status>`
- Added host tests proving ready and failure marker behavior.
- Added a host test proving the default `BootSequence.Run` marker order remains unchanged.

## Why It Changed

Issue #86 needed the readiness gate to appear at the boot-order boundary without claiming that descriptor tables are installed. This keeps `BootSequence` as the source of truth for ordered markers and gives future privileged-install work a stable status hook.

## Files Changed

- `src/OS.Kernel/Boot/BootSequence.cs`
- `tests/OS.HostTests/BootSequencePlanTests.cs`
- `docs/boot-order.md`
- `docs/diagnostics.md`
- `docs/completion-gates.md`

## Tests Run

```powershell
dotnet build .\OS.slnx --nologo -m:1
dotnet run --project .\tests\OS.HostTests\OS.HostTests.csproj --no-build
```

## Logs And Evidence

- Build passed with 0 warnings and 0 errors.
- Host tests emitted:
  - `TEST_PASS boot.sequence fault infra readiness emits ready marker`
  - `TEST_PASS boot.sequence fault infra readiness emits failure marker`
  - `TEST_PASS boot.sequence default markers unchanged by fault readiness helper`
  - `TEST_PASS host.tests`

## Failures

- No remaining test failures.

## Deferred Work

- This helper is not called from the live EFI path.
- This does not execute `lidt`, `lgdt`, or `ltr`.
- This does not install handlers or prove CPU fault behavior.

## Evidence Boundary

This step proves boot marker/readiness behavior only. It is not descriptor-table installation or hardware fault handling evidence.
