# Step 287 - UEFI Memory-Map Capture Helper

## What Changed

- Added `UefiMemoryMapCapture` as a source-linkable no-stdlib-safe helper for
  the EFI memory-map capture path.
- Moved the live `GetMemoryMap` sizing call, slack `AllocatePool`, retry
  capture, `UefiMemoryMapCopyPlan` validation, and descriptor-byte copy into
  the helper.
- Kept `EfiEntry` responsible for firmware-visible markers, snapshot fields,
  and `BootHandoffRecord` population.
- Source-linked the helper into `src/OS/OS.csproj`.
- Added focused host coverage for helper copy success, missing firmware
  pointers, missing owned destination, allocation-size overflow, and rejected
  copy-plan status propagation.
- Updated boot, boot-order, EFI build, and completion-gate documentation.

## Why It Changed

`EfiEntry` was mixing marker orchestration, handoff assembly, firmware
`GetMemoryMap` mechanics, allocation sizing, and owned-copy validation. The new
helper isolates the capture/copy mechanics so later UEFI platform work can
reuse and test that boundary without changing marker order or adding a real
`ExitBootServices` call.

## Files Changed

- `src/OS.Kernel/Boot/UefiMemoryMapCapture.cs`
- `src/OS/Boot/EfiEntry.cs`
- `src/OS/OS.csproj`
- `tests/OS.HostTests/UefiMemoryMapTests.cs`
- `docs/boot.md`
- `docs/boot-order.md`
- `docs/efi-build.md`
- `docs/completion-gates.md`
- `done/step287.md`

## Tests Run

```powershell
dotnet build .\tests\OS.HostTests\OS.HostTests.csproj --nologo -m:1
dotnet run --project .\artifacts\focused-step287\FocusedStep287.csproj
dotnet build .\OS.slnx --nologo -m:1
.\scripts\build-efi-shell.ps1
.\scripts\run-qemu-smoke.ps1 -Qemu "C:\Program Files\qemu\qemu-system-x86_64.exe" -OvmfCode "C:\Program Files\qemu\share\edk2-x86_64-code.fd" -TimeoutSeconds 15 -RequireMarker OS_SMOKE_PASS,UEFI_MEMORY_MAP_READY,UEFI_MEMORY_MAP_DESCRIPTOR_COUNT,UEFI_MEMORY_MAP_OWNED,BOOT_HANDOFF_READY
```

I also ran:

```powershell
dotnet run --project .\tests\OS.HostTests\OS.HostTests.csproj --no-build
```

The initial full host-test run was interrupted by an unrelated concurrent
platform-interface mismatch. After the platform configuration-table contract was
reconciled, the full solution build and full host-test project both passed.

## Logs And Evidence

Focused helper markers:

```text
TEST_PASS uefi.memorymap.capture helper copies owned map
TEST_PASS uefi.memorymap.capture helper rejects missing firmware pointers
TEST_PASS uefi.memorymap.capture helper rejects missing owned destination
TEST_PASS uefi.memorymap.capture helper reports allocation overflow
TEST_PASS uefi.memorymap.capture helper reports copy-plan status
TEST_PASS focused.uefi.memorymap.step287
```

Publish evidence:

```text
TEST_PASS efi.shell.publish
EFI_ARTIFACT artifacts/efi/BOOTX64.EFI
```

QEMU smoke evidence:

```text
REQUIRED_MARKERS OS_SMOKE_PASS;UEFI_MEMORY_MAP_READY;UEFI_MEMORY_MAP_DESCRIPTOR_COUNT;UEFI_MEMORY_MAP_OWNED;BOOT_HANDOFF_READY
TEST_PASS qemu.smoke
```

Observed serial markers:

```text
UEFI_MEMORY_MAP_READY
UEFI_MEMORY_MAP_DESCRIPTOR_COUNT 102
UEFI_MEMORY_MAP_OWNED
BOOT_HANDOFF_READY
OS_SMOKE_PASS
```

## Failures

No failures remain for this slice after the platform configuration-table
contract was reconciled. The focused helper run, EFI publish, QEMU smoke, full
solution build, and full host-test run all passed in the final verification
pass.

## Evidence Boundary

This step proves that the UEFI memory-map capture/copy mechanics are factored
into a source-linked no-stdlib helper, host-tested for key success/failure
paths, and still produce the existing live QEMU marker sequence. It does not
call `ExitBootServices`, prove a post-firmware memory manager, wire
`MemoryMapInfo` into live `EfiEntry`, implement owned drivers, or prove
storage, networking, hosted .NET 10, ASP.NET Minimal API, or Kestrel support.

## Deferred Work

- Keep the future real `ExitBootServices` path on a fresh-map retry contract.
- Wire full `MemoryMapInfo` materialization into live boot only after the
  owning issue updates the EFI boundary and evidence gates.
- Continue UEFI platform-interface extraction without changing the marker order
  unless a focused issue owns that change.
