# Step 009 - Boot Phase Catalog

## What Changed

- Added `src/OS.Kernel/Boot/BootPhase.cs`.
- Added stable boot phase IDs:
  - `PhaseA_Audit`
  - `PhaseA_Diagnostics`
  - `PhaseB_Critical`
  - `PhaseC_Memory`
  - `PhaseD_Runtime`
  - `PhaseE_Platform`
  - `PhaseF_Probes`
  - `PhaseG_Apps`
- Added `BootPhaseInfo` with phase name, precondition, postcondition, and marker prefix.
- Added `BootPhaseCatalog` as the managed phase ledger.
- Updated `BootSequence.Run` so implemented managed phase markers come from `BootPhaseCatalog`.
- Kept `BootSequencePlan` unchanged for the no-stdlib EFI byte-output path.
- Added host tests proving:
  - implemented phases have non-empty preconditions and postconditions;
  - future placeholder phases are documented but not emitted;
  - managed phase marker text matches `BootSequencePlan` byte-for-byte;
  - `BootSequence.Run` still emits the same ordered marker stream.
- Updated `docs/boot-order.md` with the current phase table and evidence boundary.

## Why It Changed

Boot ordering must remain explicit before more firmware, diagnostics, memory, runtime, platform, probe, and app-launch phases are added. This creates a structured managed ledger without changing the firmware-visible marker contract.

## Files Changed

- `src/OS.Kernel/Boot/BootPhase.cs`
- `src/OS.Kernel/Boot/BootSequence.cs`
- `tests/OS.HostTests/BootSequencePlanTests.cs`
- `docs/boot-order.md`

## Tests Run

```powershell
dotnet build .\OS.slnx --nologo -m:1
dotnet run --project .\tests\OS.HostTests\OS.HostTests.csproj --no-build
.\scripts\check-naming.ps1
```

## Logs and Evidence

- Managed build passed with 0 warnings and 0 errors.
- Host tests emitted `TEST_PASS host.tests`.
- New host markers passed:
  - `TEST_PASS boot.phase catalog documents implemented phases`
  - `TEST_PASS boot.phase catalog marker text matches plan`
- Existing boot-order markers still passed:
  - `TEST_PASS boot.sequence plan exposes ordered marker bytes`
  - `TEST_PASS boot.sequence run emits plan lines in order`
- Naming/source guard emitted:
  - `TEST_PASS naming.guard`
  - `TEST_PASS source.firewall`

## Failures

No failures remain for this slice.

## Deferred Work

- Promote future placeholder phases to emitted phases only after their subsystem prerequisites are implemented and tested.
- Keep `BootSequence.cs` as the ordering boundary.
- Keep the EFI no-stdlib path on `BootSequencePlan` until a no-stdlib-safe phase table is required.

## Evidence Boundary

This step proves the managed phase ledger and current marker compatibility. It does not implement hardware phases, IDT/fault diagnostics, memory ownership, runtime helpers, platform drivers, storage, networking, app launch, hosted .NET 10, ASP.NET Minimal API, or Kestrel.
