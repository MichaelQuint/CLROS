# Step 015 - Firmware Memory-Map Count Marker

## What Changed

- Updated `EfiMain` to emit `UEFI_MEMORY_MAP_DESCRIPTOR_COUNT <count>` after a successful UEFI memory-map capture.
- Added a no-stdlib-safe unsigned decimal writer for the EFI marker path.
- Kept the memory-map buffer firmware-allocated; no allocator, page ownership, or `ExitBootServices` work was added.
- Updated boot, EFI build, QEMU, repo audit, and completion-gates docs with the new marker and evidence boundary.

## Why It Changed

The boot path already captured UEFI memory-map metadata, but QEMU evidence only proved a generic `UEFI_MEMORY_MAP_READY` marker. The descriptor-count marker gives host tooling a firmware-visible count without dumping the full map and without requiring allocator work.

## Files Changed

- `src/OS/Boot/EfiEntry.cs`
- `docs/boot.md`
- `docs/efi-build.md`
- `docs/qemu.md`
- `docs/repo-audit.md`
- `docs/completion-gates.md`

## Tests Run

```powershell
dotnet build .\OS.slnx --nologo -m:1
.\scripts\build-efi-shell.ps1
.\scripts\run-qemu-smoke.ps1 -Qemu "C:\Program Files\qemu\qemu-system-x86_64.exe" -OvmfCode "C:\Program Files\qemu\share\edk2-x86_64-code.fd" -TimeoutSeconds 15 -RequireMarker OS_SMOKE_PASS,UEFI_MEMORY_MAP_DESCRIPTOR_COUNT
dotnet run --project .\tests\OS.HostTests\OS.HostTests.csproj --no-build
.\scripts\check-naming.ps1
git diff --check
```

## Logs and Evidence

- Managed build passed with 0 warnings and 0 errors.
- EFI publish emitted `TEST_PASS efi.shell.publish`.
- QEMU smoke emitted `TEST_PASS qemu.smoke` with `REQUIRED_MARKERS OS_SMOKE_PASS;UEFI_MEMORY_MAP_DESCRIPTOR_COUNT`.
- `artifacts/logs/qemu-smoke.log` contains:

```text
UEFI_MEMORY_MAP_READY
UEFI_MEMORY_MAP_DESCRIPTOR_COUNT 102
OS_SMOKE_PASS
```

- Host tests emitted `TEST_PASS host.tests`.
- Naming/source guard emitted `TEST_PASS naming.guard` and `TEST_PASS source.firewall`.
- `git diff --check` exited 0. It printed line-ending normalization warnings only.

## Failures

No failures remain for this slice.

## Deferred Work

- Copy the firmware memory-map bytes into OS-owned storage.
- Convert the live owned map into `MemoryMapInfo`.
- Keep a map-key retry path for future real `ExitBootServices`; the logged key/count only describes the map captured at this point in the EFI path.
- Avoid full map dumps by default.

## Evidence Boundary

This step proves firmware-visible memory-map capability and descriptor-count evidence in QEMU. It does not prove OS-owned memory-map lifetime, page allocator readiness, real `ExitBootServices`, owned drivers, storage, networking, hosted .NET 10, ASP.NET Minimal API, or Kestrel.
