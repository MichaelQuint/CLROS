# Step 005 - Ordered EFI Handoff and UEFI Memory Map Snapshot

## Status

Implemented and verified on 2026-05-21.

This step routes the firmware entry smoke path through the ordered boot marker boundary, captures the UEFI memory map with the firmware `GetMemoryMap` retry pattern, and adds the first host-testable UEFI-gone guard.

## What Changed

- Added `BootSequencePlan`, an allocation-free byte-indexed boot marker plan in `src/OS.Kernel/Boot/BootSequencePlan.cs`.
- Updated `BootSequence.Run` to emit boot markers from `BootSequencePlan` instead of duplicating marker literals.
- Linked `BootSequencePlan.cs` into the freestanding no-stdlib EFI project.
- Updated `EfiMain` to emit:
  - `TEST_START efi.main`
  - `EFI_CONSOLE_READY`
  - `UEFI_MEMORY_MAP_READY` or `UEFI_MEMORY_MAP_FAIL`
  - the ordered `boot.sequence` marker plan.
- Added UEFI memory-map contract types for host validation:
  - `UefiMemoryType`
  - `UefiMemoryDescriptor`
  - `UefiMemoryMapStatus`
  - `UefiMemoryMapSnapshot`
  - `UefiMemoryMapResult`
- Added `FirmwareServicesGuard` and `IPlatformFirmware` as the first UEFI-gone preparation contract.
- Updated boot, EFI, PAL, and completion-gate docs.

## Why It Changed

`EfiMain` previously wrote `OS_BOOT_OK` and `OS_SMOKE_PASS` directly. That proved firmware execution but bypassed the ordered boot boundary.

This step keeps the no-stdlib EFI artifact isolated from the normal managed `OS.Kernel` assembly while still forcing both paths to share the same ordered marker plan. The host tests compare the plan to `BootSequence.Run`, and QEMU proves the firmware artifact emits those ordered markers.

## Files Changed

- `src/OS/Boot/EfiEntry.cs`
- `src/OS/OS.csproj`
- `src/OS.Kernel/Boot/BootSequence.cs`
- `src/OS.Kernel/Boot/BootSequencePlan.cs`
- `src/OS.Kernel/Boot/UefiMemoryDescriptor.cs`
- `src/OS.Kernel/Boot/UefiMemoryMapResult.cs`
- `src/OS.Kernel/Boot/UefiMemoryMapSnapshot.cs`
- `src/OS.Kernel/Boot/UefiMemoryMapStatus.cs`
- `src/OS.Kernel/Boot/UefiMemoryType.cs`
- `src/OS.Kernel/Platform/FirmwareCallResult.cs`
- `src/OS.Kernel/Platform/FirmwareServicesGuard.cs`
- `src/OS.Kernel/Platform/FirmwareServicesMode.cs`
- `src/OS.Kernel/Platform/IPlatformFirmware.cs`
- `tests/OS.HostTests/BootSequencePlanTests.cs`
- `tests/OS.HostTests/FirmwareServicesGuardTests.cs`
- `tests/OS.HostTests/UefiMemoryMapTests.cs`
- `docs/boot.md`
- `docs/boot-order.md`
- `docs/completion-gates.md`
- `docs/efi-build.md`
- `docs/pal.md`

## Tests Run

```powershell
dotnet build .\OS.slnx --nologo -m:1
dotnet run --project .\tests\OS.HostTests\OS.HostTests.csproj --no-build
.\scripts\build-efi-shell.ps1
.\scripts\run-qemu-smoke.ps1 -Qemu "C:\Program Files\qemu\qemu-system-x86_64.exe" -OvmfCode "C:\Program Files\qemu\share\edk2-x86_64-code.fd" -TimeoutSeconds 15
.\scripts\check-naming.ps1
git diff --check
```

## Logs and Evidence

- Managed build passed with 0 warnings and 0 errors.
- Host tests emitted `TEST_PASS host.tests`.
- EFI publish emitted `TEST_PASS efi.shell.publish` and `EFI_ARTIFACT artifacts/efi/BOOTX64.EFI`.
- QEMU smoke emitted `TEST_PASS qemu.smoke`.
- `artifacts/logs/qemu-smoke.log` contained:

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

## Failures

No build, host-test, EFI-publish, naming-guard, diff-check, or QEMU-smoke failures remain for this slice.

`git diff --check` printed line-ending normalization warnings for edited text files, but exited successfully and reported no whitespace errors.

## Deferred Work

- Copy the UEFI memory map into OS-owned memory before any real `ExitBootServices` attempt.
- Expose descriptor details from the firmware snapshot through a typed boot handoff.
- Finish platform interfaces for console, memory map, keyboard, timer, firmware, filesystem, and optional RNG.
- Wire `FirmwareServicesGuard` into real boot phases instead of host tests only.
- Add UEFI-gone simulation to the QEMU smoke path.
- Implement real `ExitBootServices` only after diagnostics, serial/framebuffer output, storage, and required platform services no longer depend on firmware calls.

## Evidence Boundary

This step proves that OVMF can run the EFI artifact, that `EfiMain` captures a UEFI memory-map snapshot on the tested QEMU host, and that the firmware-visible log now includes ordered boot phase markers.

It does not prove `ExitBootServices`, owned drivers, page-table ownership, ACPI/APIC/PCI, storage, networking, app loading, hosted .NET 10, ASP.NET Minimal API, or Kestrel.
