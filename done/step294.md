# Step 294 - Live EFI Boot Memory-Region Materialization

## What Changed

- Added `BootMemoryRegionMaterializer` as a no-stdlib-safe pointer contract for turning owned raw UEFI memory-map descriptor bytes into compact `BootMemoryRegionRecord` entries.
- Source-linked the materializer into the no-stdlib EFI project.
- Wired `EfiMain` to materialize the live owned descriptor bytes into static OS-owned region storage after `BOOT_MEMORY_MAP_ACCEPTED` and before ACPI RSDP handoff.
- Added firmware-visible markers:
  - `BOOT_MEMORY_REGIONS_READY`
  - `BOOT_MEMORY_REGION_COUNT <count>`
  - `BOOT_MEMORY_REGIONS_FAIL <status-code>`
- Added host tests for descriptor materialization, destination-capacity failure, invalid descriptor data, and invalid metadata.
- Added the region markers to the default QEMU smoke marker set and native-app marker preset.
- Updated boot, QEMU, EFI build, architecture, repository-audit, and completion-gate documentation.

## Why It Changed

Issue #294 needed the next live boot/runtime boundary after owned UEFI memory-map capture, handoff validation, and compact summary acceptance. The OS now proves that the live EFI artifact can walk the owned descriptor bytes and write first-party region records into OS-owned storage without managed allocation, exceptions, `Span<T>` in the EFI path, or relying on firmware data after the copy.

## Files Changed

- `src/OS.Kernel/Boot/BootMemoryRegionMaterializer.cs`
- `src/OS/Boot/EfiEntry.cs`
- `src/OS/OS.csproj`
- `tests/OS.HostTests/BootMemoryRegionMaterializerTests.cs`
- `tests/OS.HostTests/QemuRunnerTests.cs`
- `scripts/run-qemu-smoke.ps1`
- `docs/boot.md`
- `docs/boot-order.md`
- `docs/qemu.md`
- `docs/efi-build.md`
- `docs/architecture.md`
- `docs/repo-audit.md`
- `docs/completion-gates.md`

## Tests Run

```powershell
dotnet build .\OS.slnx --nologo -m:1
dotnet run --project .\tests\OS.HostTests\OS.HostTests.csproj --no-build
.\scripts\build-efi-shell.ps1
.\scripts\run-qemu-smoke.ps1 -Qemu "C:\Program Files\qemu\qemu-system-x86_64.exe" -OvmfCode "C:\Program Files\qemu\share\edk2-x86_64-code.fd" -TimeoutSeconds 15 -RequireMarker OS_SMOKE_PASS,UEFI_MEMORY_MAP_OWNED,BOOT_HANDOFF_READY,UEFI_MEMORY_MAP_NORMALIZED,BOOT_MEMORY_MAP_ACCEPTED,BOOT_MEMORY_REGIONS_READY,BOOT_MEMORY_REGION_COUNT,BOOT_ACPI_RSDP_HANDOFF_READY,UEFI_ACPI_RSDP_READY
```

## Logs And Evidence

- Managed build passed with 0 warnings and 0 errors.
- Host tests emitted `TEST_PASS host.tests`.
- EFI publish emitted `TEST_PASS efi.shell.publish` and `EFI_ARTIFACT artifacts/efi/BOOTX64.EFI`.
- QEMU smoke emitted `TEST_PASS qemu.smoke`.
- `artifacts/logs/qemu-smoke.summary.log` recorded `MISSING_MARKERS none` for `OS_SMOKE_PASS;UEFI_MEMORY_MAP_OWNED;BOOT_HANDOFF_READY;UEFI_MEMORY_MAP_NORMALIZED;BOOT_MEMORY_MAP_ACCEPTED;BOOT_MEMORY_REGIONS_READY;BOOT_MEMORY_REGION_COUNT;BOOT_ACPI_RSDP_HANDOFF_READY;UEFI_ACPI_RSDP_READY`.
- `artifacts/logs/qemu-smoke.log` contained:

```text
BOOT_MEMORY_MAP_ACCEPTED
BOOT_MEMORY_REGIONS_READY
BOOT_MEMORY_REGION_COUNT 102
BOOT_ACPI_RSDP_HANDOFF_READY
```

- Host tests emitted:

```text
TEST_PASS boot.memory regions materializes descriptors
TEST_PASS boot.memory regions reports destination capacity
TEST_PASS boot.memory regions rejects invalid descriptors
TEST_PASS boot.memory regions rejects invalid metadata
```

## Failures

- The first `dotnet build .\OS.slnx --nologo -m:1` attempt failed because two host-test stackalloc lengths used `ulong` expressions. The tests were corrected to cast the descriptor-stride length to `int`, then the managed build passed.

## Deferred Work

- The materialized records are compact boot records, not the final allocator-owned memory map.
- No page allocator consumes these records yet.
- No `ExitBootServices` call is made.
- No owned serial, IDT/fault handling, ACPI physical-memory walk, PCI enumeration, storage, networking, native app launch, hosted .NET 10, ASP.NET Minimal API, or Kestrel behavior is proven by this step.

## Evidence Boundary

This step proves live EFI can materialize compact memory-region records from the owned UEFI descriptor copy and expose machine-readable serial evidence for that boundary. It does not prove post-firmware execution, page allocation, usable/reserved region policy, driver ownership, or cloud workload hosting.
