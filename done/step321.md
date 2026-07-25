# Step 321 - UEFI-Gone Simulation Denial and Diagnostics Markers

## What Changed

- Extended `BootSequence.RunUefiGoneSimulation` so the host simulation marker path emits:
  - `UEFI_GONE_SIM_START`
  - `UEFI_GONE_SIM_PLATFORM_DENIED`
  - `POST_FIRMWARE_DIAGNOSTICS_READY`
  - `UEFI_GONE_SIM_PASS`
- Added a `BootSequence.RunUefiGoneSimulation` overload that accepts a `SerialDiagnosticsGuardResult`, so missing owned diagnostics emits the existing status-coded `POST_FIRMWARE_DIAGNOSTICS_BLOCKED status=<status>` marker before failing.
- Extended the no-stdlib EFI `UEFI_GONE_SIMULATION_PROBE` artifact to emit the same simulation marker sequence before the normal boot sequence markers.
- Updated host tests for the new marker order and missing-diagnostics failure path.
- Updated boot, diagnostics, QEMU, EFI build, PAL, architecture, and completion-gate docs to state the simulation marker boundary.

## Why

Issue #318 required the opt-in UEFI-gone simulation smoke to prove platform-service denial and owned-serial diagnostics readiness through serial markers without changing the default smoke path or calling real `ExitBootServices`.

This step keeps the claim simulation-only. It does not perform real COM1 port I/O and does not prove post-firmware serial output after a real firmware shutdown.

## Files Changed

- `src/OS.Kernel/Boot/BootSequence.cs`
- `src/OS/Boot/EfiEntry.cs`
- `tests/OS.HostTests/UefiGoneSimulationTests.cs`
- `docs/architecture.md`
- `docs/boot.md`
- `docs/boot-order.md`
- `docs/completion-gates.md`
- `docs/diagnostics.md`
- `docs/efi-build.md`
- `docs/pal.md`
- `docs/qemu.md`
- `docs/repo-audit.md`
- `done/step321.md`

## Evidence

Commands run:

```powershell
.\scripts\new-done-step.ps1 -Reserve
dotnet build .\tests\OS.HostTests\OS.HostTests.csproj --nologo
dotnet run --project .\tests\OS.HostTests\OS.HostTests.csproj --no-build
dotnet build .\OS.slnx --nologo -m:1
.\scripts\build-efi-shell.ps1 -EnableUefiGoneSimulationProbe -EfiOutputPath artifacts/efi/BOOTX64-SIM.EFI -LogPath artifacts/logs/efi-shell-publish-sim.log
.\scripts\run-qemu-smoke.ps1 -EfiArtifact artifacts/efi/BOOTX64-SIM.EFI -Image artifacts/images/os-sim.raw -SerialLog artifacts/logs/qemu-smoke-sim.log -SummaryLog artifacts/logs/qemu-smoke-sim.summary.log -Qemu "C:\Program Files\qemu\qemu-system-x86_64.exe" -OvmfCode "C:\Program Files\qemu\share\edk2-x86_64-code.fd" -TimeoutSeconds 15 -MarkerPreset uefi-gone
git diff --check
rg --files src std apps | Where-Object { $_ -match '\.(c|cpp|h|asm|s)$' }
```

Observed markers:

```text
TEST_START done.step.allocate
NEXT_DONE_STEP done/step321.md
TEST_PASS done.step.allocate
Build succeeded.
TEST_PASS host.tests
TEST_PASS efi.shell.publish
EFI_ARTIFACT artifacts/efi/BOOTX64-SIM.EFI
REQUIRED_MARKERS OS_SMOKE_PASS;UEFI_MEMORY_MAP_OWNED;BOOT_HANDOFF_READY;UEFI_MEMORY_MAP_NORMALIZED;BOOT_MEMORY_MAP_ACCEPTED;BOOT_MEMORY_REGIONS_READY;BOOT_MEMORY_REGION_COUNT;BOOT_ACPI_RSDP_HANDOFF_READY;UEFI_ACPI_RSDP_READY;UEFI_GONE_SIM_START;UEFI_GONE_SIM_PLATFORM_DENIED;POST_FIRMWARE_DIAGNOSTICS_READY;UEFI_GONE_SIM_PASS
TEST_PASS qemu.smoke
QEMU_SMOKE_STATUS pass
MISSING_MARKERS none
FAIL_REASON none
artifacts/logs/qemu-smoke-sim.log:15:UEFI_GONE_SIM_START
artifacts/logs/qemu-smoke-sim.log:16:UEFI_GONE_SIM_PLATFORM_DENIED
artifacts/logs/qemu-smoke-sim.log:17:POST_FIRMWARE_DIAGNOSTICS_READY
artifacts/logs/qemu-smoke-sim.log:18:UEFI_GONE_SIM_PASS
artifacts/logs/qemu-smoke-sim.log:26:OS_SMOKE_PASS
```

`git diff --check` exited 0 with line-ending warnings only. The forbidden source-file scan returned no `.c`, `.cpp`, `.h`, `.asm`, or `.s` files under first-party `src`, `std`, or `apps`.

## Failures

- Gitea write access is still blocked in this task context. The API allows unauthenticated reads, but POSTing an issue comment returns `token is required`, so issue #318 could not be closed or commented with evidence from this session.

## Deferred Work

- No real `ExitBootServices` call is made.
- No real CPU port I/O or live COM1 backend is executed by the EFI artifact.
- No real post-firmware owned serial output is proven.
- The default QEMU smoke marker set remains unchanged.

## Evidence Boundary

This step proves simulation marker wiring and opt-in EFI artifact marker output only. It is not real firmware shutdown, live owned serial, storage, networking, hosted .NET, Kestrel, or cloud readiness evidence.
