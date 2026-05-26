# EFI Build

## Current Status

An explicit EFI NativeAOT project shell exists at `src/OS/OS.csproj`. It is intentionally outside `OS.slnx` so normal host builds remain stable while the freestanding path is developed.

As of 2026-05-21, this host has the required Visual Studio Build Tools C++ toolchain and Windows SDK installed. `scripts/build-efi-shell.ps1` publishes a NativeAOT x64 PE with EFI application subsystem and stages it as `artifacts/efi/BOOTX64.EFI`.

The staged artifact emits firmware-visible markers from `EfiMain`, captures the UEFI memory map on the tested QEMU/OVMF host, copies descriptor bytes into an OS-owned static buffer, validates the live owned handoff record, normalizes a compact owned-map summary, materializes compact boot memory-region records into OS-owned static storage, and emits the ordered boot marker plan through the raw image path.

## Implemented Behavior

- The project uses `NoStdLib`, `PublishAot`, `EntryPointSymbol=EfiMain`, and `LinkerSubsystem=EFI_APPLICATION`.
- `EfiEntry` exports `EfiMain`.
- `EfiMain` uses minimal UEFI system-table, console-output, boot-services, and Serial I/O protocol structs.
- `src/OS/Boot/EfiConsoleAdapter.cs` is a no-stdlib byte-output adapter for the live EFI artifact. It encapsulates UEFI console and Serial I/O fallback writes without using managed interface dispatch.
- `OS.Kernel` has a host-tested `IPlatformConsole` guard contract for future firmware-console marker writes. The live EFI artifact now has a separate no-stdlib adapter boundary, but it is still not a full `IPlatformConsole` implementation.
- `EfiMain` delegates UEFI `GetMemoryMap`, firmware-buffer slack sizing, `AllocatePool`, retry capture, copy-plan validation, and descriptor-byte copy into a fixed static OS-owned buffer to the source-linked no-stdlib-safe `UefiMemoryMapCapture` helper. `EfiEntry` records the returned map key, descriptor size/version, map size, source buffer size, owned buffer size, and descriptor count in a value-type snapshot.
- `EfiMain` emits `UEFI_MEMORY_MAP_DESCRIPTOR_COUNT <count>` after a successful capture.
- `EfiMain` emits `UEFI_MEMORY_MAP_OWNED` after the descriptor bytes are copied into the static owned buffer.
- `EfiMain` populates a source-linked no-stdlib-safe `BootHandoffRecord` with source and owned memory-map metadata after successful copy.
- `EfiMain` validates the live `BootHandoffRecord` through the source-linked no-stdlib-safe `EfiBootHandoffValidator` and emits `BOOT_HANDOFF_READY` before `TEST_START boot.sequence`.
- `EfiMain` normalizes a compact live owned-map summary through the source-linked no-stdlib-safe `EfiMemoryMapNormalizer` and emits `UEFI_MEMORY_MAP_NORMALIZED` before `TEST_START boot.sequence`.
- `EfiMain` materializes compact live owned-map region records through the source-linked no-stdlib-safe `BootMemoryRegionMaterializer` and emits `BOOT_MEMORY_REGIONS_READY` plus `BOOT_MEMORY_REGION_COUNT <count>` before ACPI RSDP handoff and `TEST_START boot.sequence`.
- `OS.Kernel` has a host-tested `UefiMemoryMapCopyPlan` contract that validates the copy from firmware-owned memory-map buffer to OS-owned storage before the EFI shell copies bytes.
- `OS.Kernel` has a host-tested `UefiMemoryMapCapture` helper that owns the no-stdlib EFI memory-map capture/copy mechanics and reports copy-plan statuses for rejected metadata.
- `OS.Kernel` has a host-tested `EfiBootHandoffValidator` contract that mirrors the live EFI numeric validation path without string formatting or managed allocation requirements.
- `OS.Kernel` has a host-tested `EfiMemoryMapNormalizer` contract for live no-stdlib summary normalization without arrays or `Span<T>`.
- `OS.Kernel` has a host-tested `BootMemoryRegionMaterializer` contract for live no-stdlib memory-region materialization without arrays, managed allocation, or exception handling.
- `OS.Kernel` has a host-tested `UefiMemoryDescriptorReader` contract that decodes owned raw descriptor bytes with 40-byte and 48-byte descriptor strides into caller-provided storage.
- `EfiMain` source-links `BootSequencePlan.cs` and emits the ordered `boot.sequence` marker plan without referencing the normal `OS.Kernel` assembly.
- Firmware console output is preferred. UEFI Serial I/O is used only as a fallback to avoid duplicate serial output when OVMF maps console output to the serial sink. Both paths are now called through `EfiConsoleAdapter`.
- `Runtime/MinimalRuntime.cs` contains the compiler-required primitive stubs, throw helpers, memory helpers, fail-fast type-cast placeholders, and reverse-P/Invoke exports needed to produce the EFI PE.
- `scripts/build-efi-shell.ps1` runs the explicit publish command and writes a log under `artifacts/logs/`. Before `dotnet publish`, it runs a host-toolchain preflight that resolves the MSVC x64 `link.exe` (`Hostx64\x64`) and the Windows SDK UM x64 `kernel32.lib` (`um\x64`), emits `EFI_PREFLIGHT_START`, `EFI_PREFLIGHT_MSVC_LINKER <path>`, `EFI_PREFLIGHT_WINDOWS_SDK <path>`, and `EFI_PREFLIGHT_OK`, and short-circuits with a deterministic `TEST_FAIL efi.shell.publish` reason when a prerequisite is missing. Search roots are injectable through `-MsvcLinkerSearchRoot`, `-WindowsSdkLibSearchRoot`, and `-VsWherePath`, and `-PreflightOnly` stops after the checks without publishing.
- `scripts/build-efi-shell.ps1 -EnableUefiGoneSimulationProbe` defines `UEFI_GONE_SIMULATION_PROBE`, prints `EFI_PROBE UEFI_GONE_SIMULATION`, and publishes a variant that emits `UEFI_GONE_SIM_START`, `UEFI_GONE_SIM_PLATFORM_DENIED`, `POST_FIRMWARE_DIAGNOSTICS_READY`, and `UEFI_GONE_SIM_PASS`.
- `scripts/build-efi-shell.ps1 -EnableAcpiPhysicalDiscoveryProbe` defines `ACPI_PHYSICAL_DISCOVERY_PROBE`, prints `EFI_PROBE ACPI_PHYSICAL_DISCOVERY`, and publishes a variant that reads firmware-provided ACPI physical addresses before `ExitBootServices`, validates RSDP/root SDT/MCFG bytes into fixed scratch storage, and emits `ACPI_RSDP_SNAPSHOT_READY`, `ACPI_RSDP_VALIDATED`, `ACPI_ROOT_SDT_SNAPSHOT_READY`, `ACPI_ROOT_SDT_VALIDATED`, and `ACPI_MCFG_DISCOVERY_READY`.
- On success, the publish script copies `src/OS/bin/Release/net10.0/win-x64/publish/OS.exe` to `artifacts/efi/BOOTX64.EFI`.

## Unsupported Behavior

The project is not yet a functional kernel. `ExitBootServices`, owned drivers, a full firmware abstraction layer, owned descriptor normalization, and panic-safe diagnostics are pending. `UefiExitBootServicesLiveWiringPlan` documents and host-tests the future live call order, and `PlatformFirmwareExitBootServicesResult` documents the future firmware-call result mapping, but the live `EfiEntry` path keeps firmware shutdown disabled until owned serial diagnostics and the real retry loop are implemented and proven.

## Tests

Expected commands:

```powershell
dotnet build .\OS.slnx --nologo -m:1
dotnet run --project .\tests\OS.HostTests\OS.HostTests.csproj --no-build
.\scripts\build-efi-shell.ps1
.\scripts\run-qemu-smoke.ps1 -Qemu "C:\Program Files\qemu\qemu-system-x86_64.exe" -OvmfCode "C:\Program Files\qemu\share\edk2-x86_64-code.fd" -TimeoutSeconds 15 -RequireMarker OS_SMOKE_PASS,UEFI_MEMORY_MAP_OWNED,BOOT_HANDOFF_READY,UEFI_MEMORY_MAP_NORMALIZED,BOOT_MEMORY_MAP_ACCEPTED,BOOT_MEMORY_REGIONS_READY,BOOT_MEMORY_REGION_COUNT
.\scripts\build-efi-shell.ps1 -EnableUefiGoneSimulationProbe -EfiOutputPath artifacts/efi/BOOTX64-SIM.EFI -LogPath artifacts/logs/efi-shell-publish-sim.log
.\scripts\run-qemu-smoke.ps1 -EfiArtifact artifacts/efi/BOOTX64-SIM.EFI -Image artifacts/images/os-sim.raw -SerialLog artifacts/logs/qemu-smoke-sim.log -Qemu "C:\Program Files\qemu\qemu-system-x86_64.exe" -OvmfCode "C:\Program Files\qemu\share\edk2-x86_64-code.fd" -TimeoutSeconds 15 -MarkerPreset uefi-gone
.\scripts\build-efi-shell.ps1 -EnableAcpiPhysicalDiscoveryProbe -EfiOutputPath artifacts/efi/BOOTX64-ACPI.EFI -LogPath artifacts/logs/efi-shell-publish-acpi.log
.\scripts\run-qemu-smoke.ps1 -EfiArtifact artifacts/efi/BOOTX64-ACPI.EFI -Image artifacts/images/os-acpi.raw -SerialLog artifacts/logs/qemu-smoke-acpi.log -SummaryLog artifacts/logs/qemu-smoke-acpi.summary.log -Qemu "C:\Program Files\qemu\qemu-system-x86_64.exe" -OvmfCode "C:\Program Files\qemu\share\edk2-x86_64-code.fd" -TimeoutSeconds 15 -MarkerPreset acpi
```

Current evidence from 2026-05-21:

- `.\scripts\build-efi-shell.ps1` emitted `TEST_PASS efi.shell.publish`.
- The script emitted `EFI_ARTIFACT artifacts/efi/BOOTX64.EFI`.
- `.\scripts\run-qemu-smoke.ps1 ...` emitted `TEST_PASS qemu.smoke`.
- The simulation publish emitted `EFI_PROBE UEFI_GONE_SIMULATION` and `TEST_PASS efi.shell.publish`.
- The simulation QEMU smoke emitted `TEST_PASS qemu.smoke` while requiring the `uefi-gone` marker preset.
- `artifacts/logs/qemu-smoke.log` contains `TEST_START efi.main`, `EFI_CONSOLE_READY`, `UEFI_MEMORY_MAP_READY`, `UEFI_MEMORY_MAP_DESCRIPTOR_COUNT <count>`, `TEST_START boot.sequence`, ordered `BOOT_PHASE` markers, `OS_BOOT_OK`, `TEST_PASS boot.sequence`, and `OS_SMOKE_PASS`.
- After the no-stdlib console adapter was added, `scripts/build-efi-shell.ps1` emitted `TEST_PASS efi.shell.publish`, and real QEMU smoke requiring `OS_SMOKE_PASS,EFI_CONSOLE_READY` emitted `TEST_PASS qemu.smoke`.
- `artifacts/logs/qemu-smoke-sim.log` additionally contains `UEFI_GONE_SIM_START`, `UEFI_GONE_SIM_PLATFORM_DENIED`, `POST_FIRMWARE_DIAGNOSTICS_READY`, and `UEFI_GONE_SIM_PASS`.

Current evidence from 2026-05-22:

- `.\scripts\build-efi-shell.ps1` emitted `TEST_PASS efi.shell.publish`.
- `.\scripts\run-qemu-smoke.ps1 -Qemu "C:\Program Files\qemu\qemu-system-x86_64.exe" -OvmfCode "C:\Program Files\qemu\share\edk2-x86_64-code.fd" -TimeoutSeconds 15 -RequireMarker OS_SMOKE_PASS,UEFI_MEMORY_MAP_OWNED` emitted `TEST_PASS qemu.smoke`.
- `.\scripts\run-qemu-smoke.ps1 -Qemu "C:\Program Files\qemu\qemu-system-x86_64.exe" -OvmfCode "C:\Program Files\qemu\share\edk2-x86_64-code.fd" -TimeoutSeconds 15 -RequireMarker OS_SMOKE_PASS,UEFI_MEMORY_MAP_OWNED,BOOT_HANDOFF_READY` emitted `TEST_PASS qemu.smoke`.
- `artifacts/logs/qemu-smoke.log` contains `UEFI_MEMORY_MAP_OWNED` and `BOOT_HANDOFF_READY`.
- `artifacts/logs/qemu-smoke.summary.log` records `MISSING_MARKERS none` for `OS_SMOKE_PASS;UEFI_MEMORY_MAP_OWNED;BOOT_HANDOFF_READY`.
- Host tests emitted `TEST_PASS uefi.descriptor.reader decodes 40-byte stride`, `TEST_PASS uefi.descriptor.reader decodes 48-byte stride`, `TEST_PASS uefi.descriptor.reader rejects invalid metadata`, and `TEST_PASS uefi.descriptor.reader reports source and destination capacity`.
- Host tests emitted `TEST_PASS efi.handoff.validator accepts owned memory-map handoff`, `TEST_PASS efi.handoff.validator rejects invalid boot source and capabilities`, `TEST_PASS efi.handoff.validator rejects memory-map ownership issues`, and `TEST_PASS efi.handoff.validator rejects descriptor metadata issues`.
- `.\scripts\run-qemu-smoke.ps1 -Qemu "C:\Program Files\qemu\qemu-system-x86_64.exe" -OvmfCode "C:\Program Files\qemu\share\edk2-x86_64-code.fd" -TimeoutSeconds 15 -RequireMarker OS_SMOKE_PASS,UEFI_MEMORY_MAP_OWNED,BOOT_HANDOFF_READY,UEFI_MEMORY_MAP_NORMALIZED` emitted `TEST_PASS qemu.smoke`.
- `artifacts/logs/qemu-smoke.log` contains `UEFI_MEMORY_MAP_NORMALIZED`.
- `artifacts/logs/qemu-smoke.summary.log` records `MISSING_MARKERS none` for `OS_SMOKE_PASS;UEFI_MEMORY_MAP_OWNED;BOOT_HANDOFF_READY;UEFI_MEMORY_MAP_NORMALIZED`.
- Host tests emitted `TEST_PASS efi.memorymap.normalizer summarizes owned descriptors`, `TEST_PASS efi.memorymap.normalizer rejects invalid metadata`, and `TEST_PASS efi.memorymap.normalizer rejects descriptor arithmetic issues`.
- Host tests emitted `TEST_PASS uefi.memorymap.capture helper rejects missing firmware pointers` and `TEST_PASS uefi.memorymap.capture helper rejects missing owned destination`.
- Host tests emitted `TEST_PASS platform.firmware exitboot reports success`, `TEST_PASS platform.firmware exitboot reports stale map key`, and `TEST_PASS platform.firmware exitboot rejects unavailable services`.
- Focused host tests emitted `TEST_PASS uefi.memorymap.capture helper copies owned map`, `TEST_PASS uefi.memorymap.capture helper reports allocation overflow`, and `TEST_PASS uefi.memorymap.capture helper reports copy-plan status`.
- Real QEMU smoke requiring `OS_SMOKE_PASS,UEFI_MEMORY_MAP_READY,UEFI_MEMORY_MAP_DESCRIPTOR_COUNT,UEFI_MEMORY_MAP_OWNED,BOOT_HANDOFF_READY` emitted `TEST_PASS qemu.smoke`.
- Host tests emitted `TEST_PASS boot.memory regions materializes descriptors`, `TEST_PASS boot.memory regions reports destination capacity`, `TEST_PASS boot.memory regions rejects invalid descriptors`, and `TEST_PASS boot.memory regions rejects invalid metadata`.
- Real QEMU smoke requiring `OS_SMOKE_PASS,UEFI_MEMORY_MAP_OWNED,BOOT_HANDOFF_READY,UEFI_MEMORY_MAP_NORMALIZED,BOOT_MEMORY_MAP_ACCEPTED,BOOT_MEMORY_REGIONS_READY,BOOT_MEMORY_REGION_COUNT,BOOT_ACPI_RSDP_HANDOFF_READY,UEFI_ACPI_RSDP_READY` emitted `TEST_PASS qemu.smoke`.
- `artifacts/logs/qemu-smoke.log` contains `BOOT_MEMORY_REGIONS_READY` and `BOOT_MEMORY_REGION_COUNT 102`.
- `artifacts/logs/qemu-smoke.summary.log` records `MISSING_MARKERS none` for `OS_SMOKE_PASS;UEFI_MEMORY_MAP_OWNED;BOOT_HANDOFF_READY;UEFI_MEMORY_MAP_NORMALIZED;BOOT_MEMORY_MAP_ACCEPTED;BOOT_MEMORY_REGIONS_READY;BOOT_MEMORY_REGION_COUNT;BOOT_ACPI_RSDP_HANDOFF_READY;UEFI_ACPI_RSDP_READY`.
- The ACPI physical discovery probe publish emitted `EFI_PROBE ACPI_PHYSICAL_DISCOVERY`, `TEST_PASS efi.shell.publish`, and `EFI_ARTIFACT artifacts/efi/BOOTX64-ACPI.EFI`.
- Real QEMU smoke against `artifacts/efi/BOOTX64-ACPI.EFI` with `-MarkerPreset acpi` emitted `TEST_PASS qemu.smoke`.
- `artifacts/logs/qemu-smoke-acpi.log` contains `ACPI_RSDP_SNAPSHOT_READY`, `ACPI_RSDP_VALIDATED`, `ACPI_ROOT_SDT_SNAPSHOT_READY`, `ACPI_ROOT_SDT_VALIDATED`, and `ACPI_MCFG_DISCOVERY_READY` before `OS_SMOKE_PASS`.
- `artifacts/logs/qemu-smoke-acpi.summary.log` records `MISSING_MARKERS none` for the default boot markers plus the five ACPI discovery markers.

On hosts without the Visual Studio C++ NativeAOT linker prerequisites, the publish script now fails in the preflight before `dotnet publish`. It reports `TEST_FAIL efi.shell.publish platform_linker_missing` when the MSVC x64 `link.exe` cannot be resolved and `TEST_FAIL efi.shell.publish windows_sdk_um_x64_missing` when the Windows SDK UM x64 `kernel32.lib` cannot be resolved, and it records the log path in both cases. The post-publish `platform_linker_missing` detection remains as a backstop for linker failures that only surface during the real publish. These failures are host toolchain setup problems, not a Hyper-V limitation.

The preflight is host-toolchain evidence only. `EFI_PREFLIGHT_OK` proves that the MSVC linker and Windows SDK library files are discoverable on the host; it does not prove a successful publish, a staged `artifacts/efi/BOOTX64.EFI`, QEMU boot, `ExitBootServices`, or any runtime behavior. `tests/OS.HostTests/BuildEfiShellPreflightTests.cs` covers the missing-linker, missing-SDK, and discoverable-toolchain contracts using injected fake search roots and `-PreflightOnly`, so the contract is verified without uninstalling host toolchains.

## Known Limitations

The current type-cast helpers are fail-fast placeholders for the shell only; real type safety belongs with later runtime/interface-dispatch work. The EFI shim must not return successful type-check results for unsupported runtime behavior.

The current UEFI protocol structs are intentionally minimal and only cover the fields needed for early console output, Serial I/O fallback, `LocateProtocol`, `GetMemoryMap`, and `AllocatePool`. They are not a complete firmware abstraction layer.

`IPlatformConsole` is host-tested as a guardable platform contract only. `EfiEntry` now routes byte writes through a no-stdlib `EfiConsoleAdapter`, but this is still not the full managed `IPlatformConsole` implementation and it does not prove post-ExitBootServices owned serial output.

The memory-map source buffer is firmware-allocated by the source-linked capture helper, but the descriptor bytes are copied into a static OS-owned buffer before `UEFI_MEMORY_MAP_OWNED` is emitted, the owned handoff validates before `BOOT_HANDOFF_READY` is emitted, the compact live summary normalizes before `UEFI_MEMORY_MAP_NORMALIZED` is emitted, and compact boot-region records materialize before `BOOT_MEMORY_REGIONS_READY` is emitted. A later step must still keep a valid map-key retry path for `ExitBootServices` and turn the compact records into allocator-owned usable/reserved page ranges.

The logged memory-map metadata proves the map captured at that point in the EFI path. It does not prove that the map key remains valid after later boot-services calls.

The ACPI physical discovery probe proves only pre-`ExitBootServices` table bytes on the tested QEMU/OVMF host. It does not map ECAM, enumerate PCI, size BARs, bind drivers, discover APIC/HPET for use, or prove post-firmware physical-memory access.

Any next-step EFI build work must keep the freestanding constraints explicit. If a new path introduces NativeAOT helper/linker failures, document the exact failing symbol or helper before adding a runtime stub. Do not stub success for allocation, type, exception, or interface-dispatch behavior that is not actually implemented.

## Next Steps

1. Move UEFI structs behind platform interfaces.
   - Keep the low-level UEFI table pointers isolated to the EFI boundary.
   - Expose only the narrow operations required for console diagnostics, memory-map ownership, firmware shutdown state, and optional filesystem/protocol discovery.
2. Add a deterministic `ExitBootServices` retry-plan contract before any real firmware shutdown.
3. Route additional platform interfaces through the UEFI-gone simulation path as they are introduced.
4. Replace permissive shell-only runtime placeholders with audited runtime helpers as subsystems require them.
