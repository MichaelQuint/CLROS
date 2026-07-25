# Step 004 - First Firmware-Visible QEMU Marker

## What Changed

- Added minimal UEFI system-table, console-output, boot-services, GUID, and Serial I/O protocol structs to the no-stdlib EFI artifact.
- Changed `EfiMain` from a stub into an early marker emitter.
- Added firmware-visible markers:
  - `TEST_START efi.main`
  - `EFI_CONSOLE_READY`
  - `OS_BOOT_OK`
  - `OS_SMOKE_PASS`
- Added a deterministic raw GPT/FAT32 image builder that places `BOOTX64.EFI` at `\EFI\BOOT\BOOTX64.EFI`.
- Updated QEMU smoke tooling to boot the raw image through OVMF, capture serial output, poll for a required marker, and stop QEMU after the marker appears.
- Installed QEMU 11.0.0 on the host through winget for real smoke evidence.

## Why It Changed

The previous step proved only that a NativeAOT no-stdlib EFI PE could be published. This step proves the next boot/runtime boundary: firmware can load the generated EFI artifact from a raw image, `EfiMain` can call UEFI output services, and the marker contract reaches the QEMU serial log.

## Files Changed

- `src/OS/Boot/EfiEntry.cs`
- `tools/OS.ImageBuilder/Program.cs`
- `tools/OS.QemuRunner/Program.cs`
- `scripts/run-qemu-smoke.ps1`
- `tests/OS.HostTests/ImageBuilderTests.cs`
- `tests/OS.HostTests/QemuRunnerTests.cs`
- `README.md`
- `docs/architecture.md`
- `docs/boot.md`
- `docs/completion-gates.md`
- `docs/efi-build.md`
- `docs/fork-diff.md`
- `docs/host-requirements.md`
- `docs/image-builder.md`
- `docs/qemu.md`
- `docs/repo-audit.md`
- `done/step004.md`

## Tests

Passed:

```powershell
dotnet build .\OS.slnx --nologo -m:1
dotnet run --project .\tests\OS.HostTests\OS.HostTests.csproj --no-build
.\scripts\build-efi-shell.ps1
.\scripts\run-qemu-smoke.ps1 -DryRun
.\scripts\run-qemu-smoke.ps1 -Qemu "C:\Program Files\qemu\qemu-system-x86_64.exe" -OvmfCode "C:\Program Files\qemu\share\edk2-x86_64-code.fd" -TimeoutSeconds 15
.\scripts\run-baseline-evidence.ps1
```

Evidence:

- `dotnet build .\OS.slnx --nologo -m:1` passed with 0 warnings and 0 errors.
- Host tests emitted `TEST_PASS host.tests`.
- EFI publish emitted `TEST_PASS efi.shell.publish` and `EFI_ARTIFACT artifacts/efi/BOOTX64.EFI`.
- QEMU dry-run emitted `TEST_PASS qemu.runner.dryrun`.
- Image builder emitted `TEST_PASS imagebuilder.raw`.
- Real QEMU smoke emitted `TEST_PASS qemu.smoke`.
- Baseline evidence emitted `TEST_PASS baseline.naming.guard`, `TEST_PASS baseline.build`, `TEST_PASS baseline.host.tests`, and `TEST_PASS baseline.qemu.dryrun`; summary was written to `artifacts\logs\20260521-174947\summary.json`.
- `artifacts/efi/BOOTX64.EFI` is 8704 bytes.
- `artifacts/images/os.raw` is 67,108,864 bytes.
- `artifacts/logs/qemu-smoke.log` contains:

```text
TEST_START efi.main
EFI_CONSOLE_READY
OS_BOOT_OK
OS_SMOKE_PASS
```

## Failures And Fixes

- Initial `EfiMain` stack-buffer marker construction pulled in the MSVC `__security_cookie` symbol. The marker writer was changed to emit one character at a time without stackalloc buffers.
- Writing to both UEFI console and Serial I/O duplicated every character under QEMU because OVMF maps console output to the serial sink. Serial I/O is now a fallback only when firmware console is unavailable.
- The first QEMU runner attempted to read the serial log after QEMU still held the file. The runner now reads with sharing and polls the serial log while QEMU is alive.

## Deferred Work

- `EfiMain` does not delegate into `BootSequence` yet.
- UEFI services are not encapsulated behind platform interfaces yet.
- UEFI memory-map capture and `ExitBootServices` are not implemented.
- No owned serial driver exists after firmware shutdown.
- qcow2, ISO, libvirt real boot, and cloud import artifacts are pending.

## Evidence Boundary

This step proves the first firmware-visible marker path from `EfiMain` through OVMF/QEMU serial capture. It does not prove a functional kernel, ExitBootServices, hardware drivers, storage, networking, native-tier app launch, hosted .NET 10, ASP.NET Minimal API, or Kestrel support.
