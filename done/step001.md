# Step 001 - Phase A Baseline Scaffold

## What Changed

- Created first-party solution project entries for kernel, std/no-runtime shared code, host tests, host QEMU runner, and native-tier example apps.
- Added `BootSequence.cs` as the boot order source of truth.
- Added diagnostic probe toggles and machine-readable smoke marker constants.
- Added host tests for marker emission and boot event ring buffer behavior.
- Added a QEMU smoke runner that can dry-run now and require serial `OS_SMOKE_PASS` once an image exists.
- Added placeholder project slots for image builder, manifest tool, and libvirt runner.
- Added required baseline documentation.

## Why It Changed

The repository and remote Gitea project were empty. The first safe step is a reproducible, buildable Phase A baseline with honest status docs and machine-readable pass/fail contracts.

## Files Changed

- `OS.slnx`
- `README.md`
- `src/OS.Kernel/Boot/BootSequence.cs`
- `src/OS.Kernel/Boot/BootResult.cs`
- `src/OS.Kernel/Diagnostics/*`
- `src/OS.Kernel/OS.Kernel.csproj`
- `std/no-runtime/shared/*`
- `tools/OS.QemuRunner/*`
- `tools/OS.ImageBuilder/*`
- `tools/OS.LibvirtRunner/*`
- `tools/OS.ManifestTool/*`
- `scripts/run-qemu-smoke.ps1`
- `tests/OS.HostTests/*`
- `apps/*`
- `docs/*`
- `docs/completion-gates.md`
- `docs/fork-diff.md`
- `work/PAL/pal-design.md`

## Tests

Passed:

```powershell
dotnet build .\OS.slnx
dotnet run --project .\tests\OS.HostTests\OS.HostTests.csproj
.\scripts\run-qemu-smoke.ps1 -DryRun
```

Evidence:

- `dotnet build .\OS.slnx` succeeded with 0 warnings and 0 errors.
- Host tests emitted `TEST_PASS host.tests`.
- QEMU dry-run emitted `TEST_PASS qemu.runner.dryrun`.

## Deferred Work

- EFI image generation.
- NativeAOT no-stdlib runtime helpers.
- QEMU real boot.
- IDT/fault diagnostics.
- UEFI encapsulation and ExitBootServices.
- ACPI/APIC/PCI, storage, networking, manifest loading, native app launch.

## Evidence Boundary

This step proves only that the scaffold builds and marker contracts are host-tested. It does not prove OS bootability.
