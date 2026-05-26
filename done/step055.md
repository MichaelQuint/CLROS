# Step 055 - Serial Diagnostics Readiness Guard

## What Changed

- Added `SerialDiagnosticsGuard`, a host-testable readiness contract for post-firmware diagnostics.
- Added status-coded transitions for firmware-console-only output, firmware services unavailable, serial unavailable, owned serial ready, and invalid transitions.
- Added host tests that prove firmware console output is not treated as post-firmware-safe diagnostics.

## Why It Changed

Issue #72 needed a guard before real `ExitBootServices` work can rely on diagnostics. The project must not accidentally claim post-firmware diagnostics while it still depends on firmware console services.

## Files Changed

- `src/OS.Kernel/Diagnostics/SerialDiagnosticsGuard.cs`
- `tests/OS.HostTests/SerialDiagnosticsGuardTests.cs`
- `docs/diagnostics.md`
- `docs/boot-order.md`
- `docs/completion-gates.md`
- `docs/drivers.md`

## Tests Run

```powershell
dotnet build .\OS.slnx --nologo -m:1
dotnet run --project .\tests\OS.HostTests\OS.HostTests.csproj --no-build
.\scripts\check-naming.ps1
.\scripts\run-qemu-smoke.ps1 -DryRun
git diff --check
```

## Logs And Evidence

- Build passed with 0 warnings and 0 errors.
- Host tests emitted:
  - `TEST_PASS diagnostics serial guard starts firmware only`
  - `TEST_PASS diagnostics serial guard blocks post firmware without owned serial`
  - `TEST_PASS diagnostics serial guard allows post firmware with owned serial`
  - `TEST_PASS diagnostics serial guard rejects invalid transition`
  - `TEST_PASS diagnostics serial guard reports serial unavailable`
  - `TEST_PASS host.tests`
- `scripts/check-naming.ps1` emitted `TEST_PASS naming.guard` and `TEST_PASS source.firewall`.
- `scripts/run-qemu-smoke.ps1 -DryRun` emitted `TEST_PASS qemu.runner.dryrun`.
- `git diff --check` exited 0 with line-ending normalization warnings only.

## Failures

- No remaining test failures.

## Deferred Work

- This does not implement a COM1 driver.
- This does not prove serial output survives real `ExitBootServices`.
- Future boot code still needs to mark owned serial readiness from a real driver before post-firmware diagnostics can be considered available.

## Evidence Boundary

This step proves only a host-side readiness/state contract. It does not call `ExitBootServices`, install an owned serial writer, or prove panic-safe output after firmware services are gone.
