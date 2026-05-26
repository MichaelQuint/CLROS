# Step 043 - Firmware-Visible BOOT_HANDOFF_READY

## What Changed

- Added `EfiBootHandoffValidator`, a no-stdlib-safe numeric validator for live EFI handoff records.
- Source-linked `EfiBootHandoffValidator` into the no-stdlib EFI project.
- Updated `EfiMain` to validate `s_bootHandoff` after `UEFI_MEMORY_MAP_OWNED`.
- Added firmware-visible `BOOT_HANDOFF_READY` before `TEST_START boot.sequence`.
- Added firmware-visible `BOOT_HANDOFF_FAIL <status-code>` if live validation fails.
- Added host tests for the EFI validator acceptance and rejection paths.
- Updated boot, boot-order, EFI build, and completion-gate docs with QEMU evidence.

## Why It Changed

`BOOT_HANDOFF_READY` was previously only proven through managed host tests. The firmware-visible path now proves that the live EFI handoff record has valid boot source, capabilities, owned-map metadata, source provenance, descriptor metadata, and size arithmetic before later boot phase markers execute.

## Files Changed

- `src/OS.Kernel/Boot/EfiBootHandoffValidator.cs`
- `src/OS.Kernel/Boot/BootSequenceHandoff.cs`
- `src/OS/OS.csproj`
- `src/OS/Boot/EfiEntry.cs`
- `tests/OS.HostTests/EfiBootHandoffValidatorTests.cs`
- `docs/boot.md`
- `docs/boot-order.md`
- `docs/efi-build.md`
- `docs/completion-gates.md`
- `done/step043.md`

## Tests Run

```powershell
dotnet build .\OS.slnx --nologo -m:1
dotnet build .\tests\OS.HostTests\OS.HostTests.csproj --nologo -m:1
dotnet run --project .\tests\OS.HostTests\OS.HostTests.csproj --no-build
.\scripts\build-efi-shell.ps1
.\scripts\run-qemu-smoke.ps1 -Qemu "C:\Program Files\qemu\qemu-system-x86_64.exe" -OvmfCode "C:\Program Files\qemu\share\edk2-x86_64-code.fd" -TimeoutSeconds 15 -RequireMarker OS_SMOKE_PASS,UEFI_MEMORY_MAP_OWNED,BOOT_HANDOFF_READY
.\scripts\check-naming.ps1
git diff --check
```

## Evidence

- Solution build passed with 0 warnings and 0 errors.
- Host-test build passed with 0 warnings and 0 errors.
- Host test marker: `TEST_PASS efi.handoff.validator accepts owned memory-map handoff`
- Host test marker: `TEST_PASS efi.handoff.validator rejects invalid boot source and capabilities`
- Host test marker: `TEST_PASS efi.handoff.validator rejects memory-map ownership issues`
- Host test marker: `TEST_PASS efi.handoff.validator rejects descriptor metadata issues`
- Full host test marker: `TEST_PASS host.tests`
- EFI publish marker: `TEST_PASS efi.shell.publish`
- QEMU marker: `TEST_PASS qemu.smoke`
- Serial log marker: `BOOT_HANDOFF_READY`
- Summary artifact: `MISSING_MARKERS none` for `OS_SMOKE_PASS;UEFI_MEMORY_MAP_OWNED;BOOT_HANDOFF_READY`
- Naming guard marker: `TEST_PASS naming.guard`
- Source firewall marker: `TEST_PASS source.firewall`
- `git diff --check` exited 0 and reported only existing LF-to-CRLF warnings.

## Failures

- A first `--no-build` host-test run used a stale test binary and failed the memory-map ownership test after the test fixture changed. Rebuilding `OS.HostTests` fixed the stale binary issue and the rerun passed.

## Deferred Work

- Wire raw descriptor decoding into the live owned-map path and normalize descriptors into `MemoryMapInfo`.
- Preserve a map-key retry contract before real `ExitBootServices`.
- Keep `ExitBootServices` blocked until descriptor normalization and post-firmware diagnostics have evidence.

## Evidence Boundary

This step proves live EFI handoff validation and the `BOOT_HANDOFF_READY` serial marker in QEMU. It does not prove descriptor normalization, `ExitBootServices`, owned drivers, storage, networking, hosted .NET 10, ASP.NET Minimal API, or Kestrel support.
