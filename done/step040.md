# Step 040 - EFI Owned Memory Map Copy

## What Changed

- Source-linked `UefiMemoryMapCopyPlan` into the no-stdlib EFI project.
- Extended `FirmwareMemoryMapRecord` and `BootHandoffRecord` with source-buffer metadata and an owned-copy flag.
- Added `BootHandoffRecord.CreateUefiWithOwnedMemoryMap`.
- Updated `EfiMain` to validate the captured UEFI memory-map metadata, copy descriptor bytes into a fixed static OS-owned buffer, and emit `UEFI_MEMORY_MAP_OWNED`.
- Added host coverage for importing an owned memory-map handoff record.
- Updated boot, EFI build, QEMU, and completion-gate docs with the new evidence boundary.

## Why It Changed

The next `ExitBootServices` prerequisite is to stop relying on a firmware-owned memory-map buffer. This step keeps the live descriptor bytes in OS-owned static storage while preserving source-buffer provenance for diagnostics and later map-key retry work.

## Files Changed

- `src/OS.Kernel/Boot/BootHandoffRecord.cs`
- `src/OS.Kernel/Boot/UefiMemoryMapCopyPlan.cs`
- `src/OS/OS.csproj`
- `src/OS/Boot/EfiEntry.cs`
- `tests/OS.HostTests/BootHandoffInfoTests.cs`
- `tests/OS.HostTests/UefiMemoryMapTests.cs`
- `docs/boot.md`
- `docs/efi-build.md`
- `docs/qemu.md`
- `docs/completion-gates.md`
- `done/step040.md`

## Tests Run

```powershell
dotnet build .\OS.slnx --nologo -m:1
dotnet run --project .\tests\OS.HostTests\OS.HostTests.csproj --no-build
.\scripts\build-efi-shell.ps1
.\scripts\run-qemu-smoke.ps1 -Qemu "C:\Program Files\qemu\qemu-system-x86_64.exe" -OvmfCode "C:\Program Files\qemu\share\edk2-x86_64-code.fd" -TimeoutSeconds 15 -RequireMarker OS_SMOKE_PASS,UEFI_MEMORY_MAP_OWNED
```

## Evidence

- Solution build passed with 0 warnings and 0 errors.
- Host test marker: `TEST_PASS boothandoff imports owned memory-map handoff record`
- Host test marker: `TEST_PASS uefi.memorymap.copyplan validates metadata`
- Full host test marker: `TEST_PASS host.tests`
- EFI publish marker: `TEST_PASS efi.shell.publish`
- EFI artifact marker: `EFI_ARTIFACT artifacts/efi/BOOTX64.EFI`
- QEMU marker: `TEST_PASS qemu.smoke`
- Serial log marker: `UEFI_MEMORY_MAP_OWNED`
- Summary artifact: `MISSING_MARKERS none` for `OS_SMOKE_PASS;UEFI_MEMORY_MAP_OWNED`

## Failures

- The first QEMU smoke attempt failed with `TEST_FAIL qemu.smoke qemu_not_found` because `qemu-system-x86_64` was not on `PATH`.
- Re-running with explicit `-Qemu "C:\Program Files\qemu\qemu-system-x86_64.exe"` and explicit OVMF path passed.

## Deferred Work

- Decode the owned raw descriptor bytes into normalized memory-map structures.
- Add `BOOT_HANDOFF_READY` validation in `BootSequence`.
- Preserve a real map-key retry contract before `ExitBootServices`.
- Implement real `ExitBootServices` only after owned diagnostics and drivers exist.

## Evidence Boundary

This step proves the EFI path copies raw UEFI memory-map descriptor bytes into an OS-owned static buffer and exposes a firmware-visible marker. It does not normalize descriptors into `MemoryMapInfo`, does not call `ExitBootServices`, does not implement owned drivers, and does not claim hosted .NET 10, ASP.NET Minimal API, or Kestrel support.
