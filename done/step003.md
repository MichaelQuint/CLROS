# Step 003 - Boot Runtime Boundary Foundation

## What Changed

- Added `src/OS`, an explicit no-stdlib NativeAOT EFI project shell kept outside `OS.slnx`.
- Added an exported stub `EfiMain`.
- Added minimal compiler-required runtime stubs scoped to the EFI shell project.
- Added `BootInfo`, `BootMode`, `BootCapabilities`, and status-coded `BootCapabilityResult`.
- Added host-testable diagnostic logging and panic formatting.
- Added `scripts/run-baseline-evidence.ps1` to save naming, build, host-test, and QEMU dry-run logs.
- Added `scripts/build-efi-shell.ps1` to run and log the EFI publish attempt.
- Installed and verified the host NativeAOT linker prerequisites with Visual Studio Build Tools 2022 C++ tools and Windows SDK 10.0.26100.
- Added `docs/efi-build.md` with the current evidence boundary.

## Why It Changed

Issue #17 needs a concrete boot/runtime boundary before runtime helpers, UEFI console output, image generation, or QEMU boot can be claimed. Keeping the shell outside the normal solution build prevents no-stdlib work from destabilizing the normal host build.

## Files Changed

- `src/OS/*`
- `src/OS.Kernel/Boot/BootInfo.cs`
- `src/OS.Kernel/Boot/BootMode.cs`
- `src/OS.Kernel/Boot/BootCapabilities.cs`
- `src/OS.Kernel/Boot/BootCapabilityResult.cs`
- `src/OS.Kernel/Diagnostics/DiagnosticLog.cs`
- `src/OS.Kernel/Diagnostics/Panic.cs`
- `src/OS.Kernel/Diagnostics/PanicFormatter.cs`
- `scripts/build-efi-shell.ps1`
- `scripts/run-baseline-evidence.ps1`
- `tests/OS.HostTests/BootInfoTests.cs`
- `tests/OS.HostTests/DiagnosticsTests.cs`
- `docs/boot.md`
- `docs/diagnostics.md`
- `docs/efi-build.md`
- `done/step003.md`

## Tests

Passed:

```powershell
dotnet build .\OS.slnx --nologo -m:1
dotnet run --project .\tests\OS.HostTests\OS.HostTests.csproj --no-build
.\scripts\check-naming.ps1
.\scripts\run-qemu-smoke.ps1 -DryRun
.\scripts\run-baseline-evidence.ps1
.\scripts\build-efi-shell.ps1
```

Evidence:

- Sequential `dotnet build .\OS.slnx --nologo -m:1` passed with 0 warnings and 0 errors.
- Host tests emitted `TEST_PASS host.tests`, including boot info and diagnostics tests.
- Naming guard emitted `TEST_PASS naming.guard`.
- QEMU dry-run emitted `TEST_PASS qemu.runner.dryrun`.
- Baseline evidence script emitted `TEST_PASS baseline.naming.guard`, `TEST_PASS baseline.build`, `TEST_PASS baseline.host.tests`, and `TEST_PASS baseline.qemu.dryrun`; summary was written to `artifacts\logs\20260521-170140\summary.json`.
- EFI shell publish emitted `TEST_PASS efi.shell.publish` and `EFI_ARTIFACT artifacts/efi/BOOTX64.EFI`.
- `dumpbin /headers src/OS/bin/Release/net10.0/win-x64/publish/OS.exe` reports `machine (x64)`, `entry point`, and `subsystem (EFI Application)`.
- `artifacts/efi/BOOTX64.EFI` is 6144 bytes.

## Deferred Work

- UEFI console output.
- Real serial marker output from firmware/QEMU.
- QEMU boot proof.
- Allocation-free panic writer.
- IDT/GDT/TSS/IST and fault register dumps.

## Evidence Boundary

This step proves the project shape, publish boundary, and first EFI PE artifact. It does not prove a bootable kernel image or a firmware-visible marker.
