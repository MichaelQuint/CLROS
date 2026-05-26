# Repository Audit

## Current Status

Audit date: 2026-05-22.

The first-party workspace began as an empty baseline: `OS.slnx` contained only `<Solution />`, `docs/` was empty, and `C:\Projects\OS` was not a Git worktree. The Gitea repository at `michael.quint/OS` reported `empty: true` through the API during the Phase A audit. The local `references/` directory contains reference repositories but they are not build inputs.

The modern SharpOS reference and libvirt reference are now present:

- `references/daniilvaino-SharpOS-main`
- `references/libvirt-csharp-main`

## Implemented Behavior

- `OS.slnx` now contains first-party project slots for kernel, std/no-runtime shared code, host tests, host QEMU runner, and native-tier example apps.
- `src/OS.Kernel/Boot/BootSequence.cs` is the first source-of-truth boot ordering file.
- `src/OS.Kernel/Diagnostics/Probes.cs` exposes compile-time probe toggles.
- `SerialSmokeMarkers` defines `TEST_START`, `TEST_PASS`, `TEST_FAIL`, `OS_BOOT_OK`, `OS_SMOKE_PASS`, and `OS_SMOKE_FAIL`.
- `src/OS/OS.csproj` is an explicit no-stdlib NativeAOT EFI project shell kept outside the normal solution build.
- `src/OS.Kernel/Boot/BootInfo.cs` defines the host-tested boot information and capability contract.
- `src/OS.Kernel/Boot/BootHandoffInfo.cs` defines the host-tested firmware handoff metadata contract for boot mode, capabilities, firmware-services mode, and memory-map metadata.
- `src/OS.Kernel/Memory` defines the host-tested first-party memory-map conversion contracts.
- `src/OS.Kernel/Diagnostics` includes host-tested diagnostic logging and panic formatting.
- `tests/OS.HostTests` verifies marker emission, boot event ring-buffer behavior, boot info, typed handoff metadata, UEFI memory-map contracts, normalized memory-map conversion, UEFI-gone simulation behavior, diagnostics, manifest validation, libvirt XML generation, source firewall behavior, storage/network parser primitives, ACPI/PCI/virtio planning contracts, and the current `OS.Std.Ascii`, endian, and `OS.Std.MemoryExtensions` helper slices.
- `tools/OS.QemuRunner` can dry-run and run a QEMU/OVMF smoke, capture serial logs, and require `OS_SMOKE_PASS`.
- `tools/OS.ManifestTool` validates manifest v0 JSON with machine-readable markers.
- `tools/OS.LibvirtRunner` generates deterministic host-side domain XML in dry-run mode without requiring libvirt.
- `tools/OS.ImageBuilder` creates a deterministic raw GPT/FAT32 EFI System Partition image containing `\EFI\BOOT\BOOTX64.EFI`.
- `scripts/new-done-step.ps1` allocates the next `done/stepNNN.md` path with machine-readable markers and can reserve a template file for parallel-agent work without overwriting an existing path.

## Build Status

- SDK observed locally: .NET SDK `10.0.300`.
- Before scaffolding, `dotnet build OS.slnx` was a no-op because the solution had no projects.
- The current baseline build command passes with 0 warnings and 0 errors:

```powershell
dotnet build .\OS.slnx
```

## Boot Status

The first firmware-visible boot smoke exists. `scripts/build-efi-shell.ps1` publishes `artifacts/efi/BOOTX64.EFI`; `OS.ImageBuilder` wraps it into `artifacts/images/os.raw`; and `scripts/run-qemu-smoke.ps1` boots that image through QEMU/OVMF. The serial log reaches `TEST_START efi.main`, `EFI_CONSOLE_READY`, `UEFI_MEMORY_MAP_READY`, `UEFI_MEMORY_MAP_DESCRIPTOR_COUNT <count>`, `UEFI_MEMORY_MAP_OWNED`, `BOOT_HANDOFF_READY`, `UEFI_MEMORY_MAP_NORMALIZED`, `BOOT_MEMORY_MAP_ACCEPTED`, `BOOT_MEMORY_REGIONS_READY`, `BOOT_MEMORY_REGION_COUNT <count>`, `BOOT_ACPI_RSDP_HANDOFF_READY`, `UEFI_ACPI_RSDP_READY`, ordered `boot.sequence` phase markers, `OS_BOOT_OK`, `TEST_PASS boot.sequence`, and `OS_SMOKE_PASS`. An explicit opt-in ACPI physical discovery EFI artifact also boots under QEMU/OVMF and emits `ACPI_RSDP_SNAPSHOT_READY`, `ACPI_RSDP_VALIDATED`, `ACPI_ROOT_SDT_SNAPSHOT_READY`, `ACPI_ROOT_SDT_VALIDATED`, and `ACPI_MCFG_DISCOVERY_READY`.

This is not a full kernel boot. `EfiMain` emits the shared ordered boot marker plan, captures raw UEFI memory-map metadata, copies descriptor bytes into OS-owned static storage, normalizes a compact owned-map summary for the boot boundary, materializes compact boot memory-region records into static storage, carries ACPI RSDP provenance into a no-stdlib-safe `BootHandoffRecord`, and reaches `BOOT_MEMORY_REGIONS_READY` plus `BOOT_ACPI_RSDP_HANDOFF_READY`. The ACPI physical discovery probe proves only pre-`ExitBootServices` table bytes on the tested QEMU/OVMF host. It does not call `ExitBootServices`, run owned drivers, map ECAM, enumerate PCI, read storage through virtio-blk, move packets through virtio-net, or launch an app.

## Runtime Status

No functional no-stdlib NativeAOT runtime helper implementation exists yet. `src/OS/Runtime/MinimalRuntime.cs` contains only minimal compiler-required shell stubs and placeholders for the EFI publish boundary, while `std/no-runtime/shared` contains small host-tested `OS.Std.Ascii`, byte-span `OS.Std.MemoryExtensions`, and endian helper slices that must be expanded before broader kernel or app usage.

## App Status

The example app projects exist and currently emit deferred-status markers only:

- `HelloSharpFs`
- `FetchApp`
- `TinyHttpServer`
- `DiagnosticApp`

## Driver Status

No first-party APIC, serial port hardware backend, framebuffer, live PCI, virtio-blk, or virtio-net driver implementation exists yet. Host-tested ACPI RSDP discovery/validation, RSDT/XSDT pointer-list parsing, MCFG-to-ECAM parsing, PCI ECAM address planning, PCI type-0 header parsing, PCI BAR metadata decoding, injected-reader PCI scanning, virtio PCI capability parsing/discovery, virtio initialization planning, virtio-blk split-ring descriptor materialization, and virtio-net queue/header metadata contracts exist. The opt-in ACPI physical discovery probe has live QEMU table-discovery markers, but these are not live hardware enumeration or driver evidence.

## Networking Status

Host-tested Ethernet, ARP, ARP neighbor resolution, bounded ARP neighbor cache, IPv4, ICMP echo, UDP, DHCP message/client transaction, DNS message/resolver transaction, TCP header, active/passive TCP handshake, active-connect, established payload stream, close/reset, HTTP request/response parsing, mock HTTP GET transaction, and host-only PAL socket/config/file contracts exist under `src/OS.Kernel`. No virtio-net live RX/TX, NIC-backed DHCP/DNS/TCP/HTTP, exported socket PAL, hosted .NET 10 networking, or Kestrel behavior exists yet.

## Storage Status

Host-tested read-only block-device contracts, GPT ESP parsing/selection, partition-relative devices, FAT32 BPB/root and first-level directory parsing, VFAT long-name lookup, FAT-chain-aware content reads, package parser/filesystem APIs, `OS/APP.PKG` loader over an `IReadOnlyBlockDevice`, virtio-blk request/read transaction planning, split-ring layout, descriptor-chain planning, and caller-owned descriptor-chain writer exist. No runtime virtio-blk driver, live disk read, boot-time package load from hardware, executable app mapping, or app loader execution exists yet.

## Packaging Status

`OS.ImageBuilder` generates `artifacts/images/os.raw` as a raw GPT/FAT32 image, can place `\OS\MANIFEST.JSON` and `\OS\APP.PKG` payloads, can build deterministic `OSPKGv0` packages, can validate manifest-selected native payload packages, and can wrap raw native app bytes into the current `OSAPPv0` payload. qcow2 conversion depends on host `qemu-img`; ISO and cloud import validation are not implemented yet.

## Tests

Current baseline tests passed:

```powershell
dotnet run --project .\tests\OS.HostTests\OS.HostTests.csproj
.\scripts\run-qemu-smoke.ps1 -DryRun
.\scripts\run-qemu-smoke.ps1 -Qemu "C:\Program Files\qemu\qemu-system-x86_64.exe" -OvmfCode "C:\Program Files\qemu\share\edk2-x86_64-code.fd" -TimeoutSeconds 15
```

Evidence:

- Host tests emitted `TEST_PASS host.tests`.
- QEMU dry-run emitted `TEST_PASS qemu.runner.dryrun`.
- Source firewall emitted `TEST_PASS source.firewall`.
- EFI publish emitted `TEST_PASS efi.shell.publish` and `EFI_ARTIFACT artifacts/efi/BOOTX64.EFI`.
- Real QEMU smoke emitted `TEST_PASS qemu.smoke`.
- UEFI-gone simulation QEMU smoke emitted `TEST_PASS qemu.smoke` while requiring `OS_SMOKE_PASS`, `UEFI_GONE_SIM_START`, `UEFI_GONE_SIM_PLATFORM_DENIED`, `POST_FIRMWARE_DIAGNOSTICS_READY`, and `UEFI_GONE_SIM_PASS`.
- ACPI physical discovery probe QEMU smoke emitted `TEST_PASS qemu.smoke` while requiring `ACPI_RSDP_SNAPSHOT_READY`, `ACPI_RSDP_VALIDATED`, `ACPI_ROOT_SDT_SNAPSHOT_READY`, `ACPI_ROOT_SDT_VALIDATED`, and `ACPI_MCFG_DISCOVERY_READY`.
- `artifacts/logs/qemu-smoke.log` contains `TEST_START efi.main`, `EFI_CONSOLE_READY`, `UEFI_MEMORY_MAP_READY`, `UEFI_MEMORY_MAP_DESCRIPTOR_COUNT <count>`, `UEFI_MEMORY_MAP_OWNED`, `BOOT_HANDOFF_READY`, `UEFI_MEMORY_MAP_NORMALIZED`, `BOOT_MEMORY_MAP_ACCEPTED`, `BOOT_MEMORY_REGIONS_READY`, `BOOT_MEMORY_REGION_COUNT <count>`, `BOOT_ACPI_RSDP_HANDOFF_READY`, `UEFI_ACPI_RSDP_READY`, ordered `boot.sequence` phase markers, `OS_BOOT_OK`, `TEST_PASS boot.sequence`, and `OS_SMOKE_PASS`.
- `artifacts/logs/qemu-smoke-sim.log` additionally contains `UEFI_GONE_SIM_START`, `UEFI_GONE_SIM_PLATFORM_DENIED`, `POST_FIRMWARE_DIAGNOSTICS_READY`, and `UEFI_GONE_SIM_PASS`.
- `artifacts/logs/qemu-smoke-acpi.log` additionally contains the five ordered ACPI physical discovery markers.
- `dumpbin /headers src/OS/bin/Release/net10.0/win-x64/publish/OS.exe` reports `machine (x64)`, `entry point`, and `subsystem (EFI Application)`.

## Known Limitations

- This audit cannot claim runtime, live driver, live storage, live network, app execution, or hosted-workload readiness.
- The default solution projects are plain `net10.0` scaffolds. The explicit `src/OS` EFI shell is no-stdlib and now has first-marker QEMU proof, but it is not a functional kernel artifact.
- Modern SharpOS behavior in `references/daniilvaino-SharpOS-main` is reference evidence only until it is adapted into first-party source and tested here.

## Next Steps

1. Finish UEFI platform interfaces and post-firmware diagnostics before real `ExitBootServices`.
2. Convert the opt-in ACPI SDT/MCFG probe evidence into a durable platform discovery handoff, then feed parsed ECAM metadata into a live PCI reader after physical-memory/MMIO ownership rules are defined.
3. Build live virtio-blk and virtio-net drivers on top of the existing planning contracts.
4. Add libvirt real-smoke proof and cloud image validation when host tools and live device paths are ready.
