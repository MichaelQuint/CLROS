# Step 076 - Early Diagnostics Readiness Marker Boundary

## What Changed

- Added `EarlyDiagnosticsReadinessStatus`.
- Added `BootSequence.RunEarlyDiagnosticsReadiness`, a host-tested readiness-marker helper.
- Added stable marker constants:
  - `EARLY_DIAGNOSTICS_READY`
  - `EARLY_DIAGNOSTICS_NOT_READY status=<status>`
- Added host tests proving ready, serial-backend failure, descriptor-readiness failure, and default boot marker stability.

## Why It Changed

Issue #96 needed a boot-order boundary that combines COM1 backend readiness with descriptor-table install readiness before future privileged diagnostics installation. This keeps the next boot phase explicit and prevents generated-stub work from bypassing `BootSequence`.

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
  - `TEST_PASS boot.sequence early diagnostics readiness emits ready marker`
  - `TEST_PASS boot.sequence early diagnostics readiness rejects serial backend failure`
  - `TEST_PASS boot.sequence early diagnostics readiness rejects descriptor readiness failure`
  - `TEST_PASS boot.sequence early diagnostics readiness leaves default markers unchanged`
  - `TEST_PASS host.tests`

## Failures

- No build failures after this change.

## Deferred Work

- This helper is not called from the live EFI path.
- This does not copy or execute generated stubs.
- This does not install COM1, IDT, GDT, TSS, IST, or fault handlers.

## Evidence Boundary

This step proves host-side marker/readiness behavior only. It is not live diagnostics installation, COM1, descriptor-table, or CPU fault evidence.
