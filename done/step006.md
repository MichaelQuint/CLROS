# Step 006 - Firmware Handoff, MemoryMapInfo, and UEFI-Gone Host Contracts

## Status

Implemented and host/QEMU-verified on 2026-05-21.

This step adds host-testable contracts for the next boot boundary after raw EFI memory-map capture. It also adds a no-stdlib-safe handoff record that the EFI artifact can populate from the live UEFI memory-map snapshot.

## What Changed

- Added `BootHandoffInfo`, `FirmwareBootContext`, and `FirmwareMemoryMapMetadata` in `src/OS.Kernel/Boot/BootHandoffInfo.cs`.
- Added `BootHandoffRecord` and `FirmwareMemoryMapRecord` as no-stdlib-safe value records that can be source-linked into `src/OS`.
- Source-linked `BootHandoffRecord.cs` into the no-stdlib EFI project and populated it from `EfiMain` after memory-map capture.
- Added status-coded handoff validation for missing memory map, invalid map size, invalid descriptor size/version, and invalid descriptor count.
- Added first-party memory map contracts under `src/OS.Kernel/Memory`:
  - `MemoryRegion`
  - `MemoryRegionType`
  - `MemoryRegionResult`
  - `MemoryMapInfo`
  - `MemoryMapInfoResult`
  - `MemoryMapInfoStatus`
- Added UEFI descriptor conversion into normalized memory regions, including deterministic `Unknown` fallback for unrecognized UEFI descriptor types.
- Added total page and byte calculations with overflow checks.
- Added `Probes.EnableUefiGoneSimulationProbe`, defaulted to `false`.
- Added `BootSequence.RunUefiGoneSimulation` with `UEFI_GONE_SIM_START`, `UEFI_GONE_SIM_PASS`, and `UEFI_GONE_SIM_FAIL reason=<code>` markers.
- Added configurable QEMU required-marker support to `OS.QemuRunner` and `scripts/run-qemu-smoke.ps1`.
- Added `scripts/build-efi-shell.ps1 -EnableUefiGoneSimulationProbe` and verified a real QEMU simulation artifact that emits `UEFI_GONE_SIM_START` and `UEFI_GONE_SIM_PASS`.
- Updated docs for boot, boot order, architecture, diagnostics, EFI build, PAL, repo audit, and completion gates.

## Why It Changed

Step 005 proved raw firmware memory-map capture in QEMU. The next dependency is a first-party representation of that firmware state before any attempt to own memory, exit firmware services, or build page tables.

These contracts let future work validate and convert memory-map metadata without relying on raw UEFI structs throughout the kernel.

## Files Changed

- `src/OS.Kernel/Boot/BootHandoffInfo.cs`
- `src/OS.Kernel/Boot/BootHandoffRecord.cs`
- `src/OS.Kernel/Boot/BootSequence.cs`
- `src/OS.Kernel/Diagnostics/Probes.cs`
- `src/OS.Kernel/Memory/MemoryMapInfo.cs`
- `src/OS.Kernel/Memory/MemoryMapInfoResult.cs`
- `src/OS.Kernel/Memory/MemoryMapInfoStatus.cs`
- `src/OS.Kernel/Memory/MemoryRegion.cs`
- `src/OS.Kernel/Memory/MemoryRegionResult.cs`
- `src/OS.Kernel/Memory/MemoryRegionType.cs`
- `src/OS/Boot/EfiEntry.cs`
- `src/OS/OS.csproj`
- `tools/OS.QemuRunner/Program.cs`
- `scripts/build-efi-shell.ps1`
- `scripts/run-qemu-smoke.ps1`
- `tests/OS.HostTests/BootHandoffInfoTests.cs`
- `tests/OS.HostTests/MemoryMapInfoTests.cs`
- `tests/OS.HostTests/QemuRunnerTests.cs`
- `tests/OS.HostTests/UefiGoneSimulationTests.cs`
- `docs/architecture.md`
- `docs/boot.md`
- `docs/boot-order.md`
- `docs/completion-gates.md`
- `docs/diagnostics.md`
- `docs/efi-build.md`
- `docs/host-requirements.md`
- `docs/image-builder.md`
- `docs/pal.md`
- `docs/qemu.md`
- `docs/repo-audit.md`

## Tests Run

```powershell
dotnet build .\OS.slnx --nologo -m:1
dotnet run --project .\tests\OS.HostTests\OS.HostTests.csproj --no-build
.\scripts\build-efi-shell.ps1
.\scripts\run-qemu-smoke.ps1 -Qemu "C:\Program Files\qemu\qemu-system-x86_64.exe" -OvmfCode "C:\Program Files\qemu\share\edk2-x86_64-code.fd" -TimeoutSeconds 15
.\scripts\build-efi-shell.ps1 -EnableUefiGoneSimulationProbe -EfiOutputPath artifacts/efi/BOOTX64-SIM.EFI -LogPath artifacts/logs/efi-shell-publish-sim.log
.\scripts\run-qemu-smoke.ps1 -EfiArtifact artifacts/efi/BOOTX64-SIM.EFI -Image artifacts/images/os-sim.raw -SerialLog artifacts/logs/qemu-smoke-sim.log -Qemu "C:\Program Files\qemu\qemu-system-x86_64.exe" -OvmfCode "C:\Program Files\qemu\share\edk2-x86_64-code.fd" -TimeoutSeconds 15 -RequireMarker OS_SMOKE_PASS,UEFI_GONE_SIM_PASS
```

## Logs and Evidence

- Managed build passed with 0 warnings and 0 errors.
- Host tests emitted `TEST_PASS host.tests`.
- New host markers include:
  - `TEST_PASS boothandoff validates UEFI memory-map metadata`
  - `TEST_PASS boothandoff imports no-stdlib EFI handoff record`
  - `TEST_PASS boothandoff rejects missing memory map`
  - `TEST_PASS memory.mapinfo converts UEFI descriptors`
  - `TEST_PASS memory.mapinfo maps unknown UEFI descriptor type`
  - `TEST_PASS qemu dry-run emits custom required markers`
  - `TEST_PASS uefi gone simulation emits pass markers and denies firmware calls`
- EFI publish emitted `TEST_PASS efi.shell.publish`.
- QEMU smoke emitted `TEST_PASS qemu.smoke`.
- UEFI-gone simulation EFI publish emitted `EFI_PROBE UEFI_GONE_SIMULATION` and `TEST_PASS efi.shell.publish`.
- UEFI-gone simulation QEMU smoke emitted `TEST_PASS qemu.smoke` with `REQUIRED_MARKERS OS_SMOKE_PASS;UEFI_GONE_SIM_PASS`.
- `artifacts/logs/qemu-smoke.log` still contains:

```text
TEST_START efi.main
EFI_CONSOLE_READY
UEFI_MEMORY_MAP_READY
TEST_START boot.sequence
BOOT_PHASE PhaseA.Audit START
BOOT_PHASE PhaseA.Audit PASS
BOOT_PHASE PhaseA.Diagnostics START
BOOT_PHASE PhaseA.Diagnostics PASS
OS_BOOT_OK
TEST_PASS boot.sequence
OS_SMOKE_PASS
```

- `artifacts/logs/qemu-smoke-sim.log` additionally contains:

```text
UEFI_GONE_SIM_START
UEFI_GONE_SIM_PASS
```

## Failures

No build, host-test, EFI-publish, or QEMU-smoke failures remain for this slice.

## Deferred Work

- Copy the UEFI memory map into OS-owned storage before `ExitBootServices`.
- Convert the live OS-owned map into `MemoryMapInfo`.
- Keep real `ExitBootServices` blocked until OS-owned memory-map lifetime and diagnostics are proven.

## Evidence Boundary

This step proves host-side contracts, live EFI population of a no-stdlib-safe handoff record, configurable QEMU marker requirements, and a QEMU-verified UEFI-gone simulation marker path. It does not prove OS-owned memory lifetime, real `ExitBootServices`, owned drivers, storage, networking, hosted .NET 10, ASP.NET Minimal API, or Kestrel.
