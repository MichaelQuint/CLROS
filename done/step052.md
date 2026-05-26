# Step 052 - Typed ACPI RSDP Boot Handoff

## What Changed

- Added ACPI RSDP pointer provenance to `BootHandoffRecord` through `FirmwareAcpiRsdpRecord`.
- Added the `AcpiRsdp` boot capability bit.
- Extended `BootHandoffInfo` with typed `FirmwareAcpiRsdpMetadata`.
- Extended `EfiBootHandoffValidator` and `BootSequenceHandoffValidator` to reject inconsistent ACPI RSDP metadata.
- Wired `EfiMain` to copy a successful UEFI configuration-table RSDP scan result into the boot handoff record, revalidate the handoff, and emit `BOOT_ACPI_RSDP_HANDOFF_READY`.
- Added `BOOT_ACPI_RSDP_HANDOFF_READY` to the default QEMU smoke marker set.

## Why It Changed

Issue #70 needed the firmware-discovered ACPI RSDP pointer to cross the boot/runtime boundary as typed metadata. The previous scanner emitted `UEFI_ACPI_RSDP_READY`, but the selected pointer remained local to `EfiMain`.

This step keeps the boundary narrow: it carries the pointer, table kind, scanner status, and capability bit only. It does not dereference, checksum, or parse the RSDP.

## Files Changed

- `src/OS.Kernel/Boot/BootCapabilities.cs`
- `src/OS.Kernel/Boot/BootHandoffInfo.cs`
- `src/OS.Kernel/Boot/BootHandoffRecord.cs`
- `src/OS.Kernel/Boot/BootSequenceHandoff.cs`
- `src/OS.Kernel/Boot/EfiBootHandoffValidator.cs`
- `src/OS/Boot/EfiEntry.cs`
- `scripts/run-qemu-smoke.ps1`
- `tests/OS.HostTests/BootHandoffInfoTests.cs`
- `tests/OS.HostTests/BootSequencePlanTests.cs`
- `tests/OS.HostTests/EfiBootHandoffValidatorTests.cs`
- `tests/OS.HostTests/QemuRunnerTests.cs`
- `docs/boot.md`
- `docs/boot-order.md`
- `docs/completion-gates.md`
- `docs/drivers.md`
- `docs/qemu.md`

## Tests Run

```powershell
dotnet build .\OS.slnx --nologo -m:1
dotnet run --project .\tests\OS.HostTests\OS.HostTests.csproj --no-build
.\scripts\run-qemu-smoke.ps1 -DryRun
.\scripts\run-qemu-smoke.ps1 -Qemu "C:\Program Files\qemu\qemu-system-x86_64.exe" -OvmfCode "C:\Program Files\qemu\share\edk2-x86_64-code.fd" -TimeoutSeconds 15
```

## Logs And Evidence

- Build passed with 0 warnings and 0 errors.
- Host tests emitted:
  - `TEST_PASS boothandoff imports ACPI RSDP metadata`
  - `TEST_PASS boothandoff keeps ACPI RSDP optional`
  - `TEST_PASS boothandoff rejects inconsistent ACPI RSDP metadata`
  - `TEST_PASS efi.handoff.validator validates ACPI RSDP metadata`
  - `TEST_PASS boot.sequence handoff validator validates ACPI RSDP metadata`
  - `TEST_PASS qemu smoke script defaults require boot boundary markers`
  - `TEST_PASS host.tests`
- QEMU dry-run emitted `REQUIRED_MARKERS OS_SMOKE_PASS;UEFI_MEMORY_MAP_OWNED;BOOT_HANDOFF_READY;UEFI_MEMORY_MAP_NORMALIZED;BOOT_MEMORY_MAP_ACCEPTED;BOOT_ACPI_RSDP_HANDOFF_READY;UEFI_ACPI_RSDP_READY`.
- Real QEMU smoke emitted `TEST_PASS qemu.smoke`.
- `artifacts/logs/qemu-smoke.summary.log` reported `MISSING_MARKERS none` for the default marker set including `BOOT_ACPI_RSDP_HANDOFF_READY`.
- `artifacts/logs/qemu-smoke.log` contains `BOOT_ACPI_RSDP_HANDOFF_READY` before `UEFI_ACPI_RSDP_READY` and before `TEST_START boot.sequence`.

## Failures

- The first new `BootHandoffInfo` test had an incorrect expectation that no-stdlib EFI handoff records include the `Shutdown` capability. The test was corrected to match the existing EFI capability set: text output plus memory map, with ACPI RSDP added only when present.
- `run-qemu-smoke.ps1 -BuildEfi` exposed two wrapper bugs:
  - native stderr from `OS.QemuRunner` was treated as a PowerShell error record,
  - invoking `build-efi-shell.ps1` in-process could terminate the parent script before image build and QEMU run.
- The wrapper now invokes the EFI publish script in a child PowerShell process, preserves native exit codes, and handles successful discovery diagnostics cleanly.

## Deferred Work

- Safe physical-memory read rules are still needed before dereferencing the RSDP pointer.
- The live EFI path does not run the byte-level RSDP validator yet.
- ACPI SDT parsing, MADT parsing, and APIC setup remain future platform work.

## Evidence Boundary

This step proves typed pointer provenance across the boot handoff and a firmware-visible QEMU marker for that handoff. It does not prove ACPI table parsing, RSDP checksum validation in the live EFI path, or owned post-`ExitBootServices` platform execution.
