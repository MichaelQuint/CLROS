# Step 047 - QEMU Smoke Default Boundary Markers

## What Changed

- Updated `scripts/run-qemu-smoke.ps1` so the default smoke marker set requires:
  - `OS_SMOKE_PASS`
  - `UEFI_MEMORY_MAP_OWNED`
  - `BOOT_HANDOFF_READY`
  - `UEFI_MEMORY_MAP_NORMALIZED`
- Preserved explicit `-RequireMarker` override behavior for targeted probes.
- Added host coverage that verifies the script default includes the current boot boundary markers.

## Why It Changed

A plain QEMU smoke should no longer pass on the final `OS_SMOKE_PASS` marker alone. The current boot evidence depends on owned UEFI memory-map bytes, validated handoff, and live owned-map normalization before the ordered boot sequence.

## Files Changed

- `scripts/run-qemu-smoke.ps1`
- `tests/OS.HostTests/QemuRunnerTests.cs`
- `docs/qemu.md`
- `done/step047.md`

## Tests Run

```powershell
dotnet build .\OS.slnx --nologo -m:1
dotnet run --project .\tests\OS.HostTests\OS.HostTests.csproj --no-build
.\scripts\run-qemu-smoke.ps1 -DryRun
.\scripts\run-qemu-smoke.ps1 -DryRun -RequireMarker UEFI_GONE_SIM_PASS
.\scripts\run-qemu-smoke.ps1 -Qemu "C:\Program Files\qemu\qemu-system-x86_64.exe" -OvmfCode "C:\Program Files\qemu\share\edk2-x86_64-code.fd" -TimeoutSeconds 15
.\scripts\check-naming.ps1
git diff --check
```

## Evidence

- Solution build passed with 0 warnings and 0 errors.
- Full host test run passed with `TEST_PASS host.tests`.
- Host test marker passed: `TEST_PASS qemu smoke script defaults require boot boundary markers`.
- Default dry-run emitted `REQUIRED_MARKERS OS_SMOKE_PASS;UEFI_MEMORY_MAP_OWNED;BOOT_HANDOFF_READY;UEFI_MEMORY_MAP_NORMALIZED`.
- Explicit override dry-run emitted `REQUIRED_MARKERS UEFI_GONE_SIM_PASS`, proving `-RequireMarker` replaces the defaults for targeted probes.
- Real QEMU smoke without explicit `-RequireMarker` emitted `TEST_PASS qemu.smoke`.
- `artifacts/logs/qemu-smoke.summary.log` reported `MISSING_MARKERS none` for `OS_SMOKE_PASS;UEFI_MEMORY_MAP_OWNED;BOOT_HANDOFF_READY;UEFI_MEMORY_MAP_NORMALIZED`.
- `artifacts/logs/qemu-smoke.log` contains `UEFI_MEMORY_MAP_OWNED`, `BOOT_HANDOFF_READY`, `UEFI_MEMORY_MAP_NORMALIZED`, and `OS_SMOKE_PASS`.
- Naming/source firewall guard passed with `TEST_PASS naming.guard` and `TEST_PASS source.firewall`.
- `git diff --check` exited 0 with only existing LF-to-CRLF warnings.

## Failures

None so far.

## Deferred Work

- Keep direct `tools/OS.QemuRunner` default marker behavior as `OS_SMOKE_PASS` for lower-level runner tests.
- Add future default markers only when they are stable and firmware-visible in the normal EFI path.

## Evidence Boundary

This step strengthens the script-level QEMU smoke gate. It does not prove `ExitBootServices`, owned drivers, storage, networking, hosted .NET 10, ASP.NET Minimal API, or Kestrel support.
