# Architecture

## Current Status

The architecture is a staged CloudReady NativeAOT OS plan. The first-class application goal is .NET 10 workload hosting, with ASP.NET Minimal API apps as the primary cloud target. Kestrel support is mandatory for the CloudReady milestone. The implementation is currently at the first EFI/QEMU smoke boundary, an opt-in QEMU-proven pre-`ExitBootServices` ACPI physical discovery probe, and host-tested parser/planning contracts for early diagnostics, ACPI/PCI/virtio, storage, networking, PAL, manifest, and native-app readiness.

`docs/workload-profiles.md` defines the workload profile decision. `SingleMinimalApi` is the near-term default path: one ASP.NET Minimal API workload, no process-table or per-process isolation requirement, but still with the required Kestrel runtime/network prerequisites. `MultiMinimalApi` is a later platform profile that requires process, isolation, scheduler, and supervisor semantics before it can be claimed.

## Implemented Behavior

- First-party project areas exist for `OS.Kernel`, `OS.Std.NoRuntime.Shared`, host tools, tests, and native-tier apps.
- Boot ordering starts in `src/OS.Kernel/Boot/BootSequence.cs` and the no-stdlib EFI shell emits the shared `BootSequencePlan` marker order.
- Diagnostics toggles start in `src/OS.Kernel/Diagnostics/Probes.cs`.
- `src/OS` publishes a no-stdlib EFI application that emits firmware-visible smoke markers, captures raw UEFI memory-map metadata, copies descriptor bytes into an OS-owned static buffer, materializes compact boot memory-region records into static storage, and populates a source-linked `BootHandoffRecord`.
- An explicit `-EnableAcpiPhysicalDiscoveryProbe` EFI build validates live ACPI RSDP/root SDT/MCFG bytes before `ExitBootServices` and emits ordered `ACPI_*` markers under QEMU/OVMF. It is a table-discovery probe, not a driver or PCI-enumeration path.
- `src/OS.Kernel/Boot/BootHandoffInfo.cs` defines a host-tested typed handoff metadata contract and can import the no-stdlib-safe `BootHandoffRecord`.
- `src/OS.Kernel/Boot/UefiMemoryDescriptorReader.cs` defines the host-tested raw descriptor decoding step needed before live owned-map normalization.
- `src/OS.Kernel/Memory` defines host-tested normalized memory-region and memory-map conversion contracts from UEFI descriptors, including the existing allocated `MemoryMapInfo` path, a caller-owned `MemoryRegion` writer, an allocator-facing `BootMemoryRegionPlanner` that classifies compact boot records into usable conventional ranges plus reserved/unusable metadata before general heap allocation is safe, a `BootPageAllocatorPlanner` that reduces selected ranges into caller-owned initial free-segment metadata without allocating, a `BootPageFreeListBuilder` that converts a successful plan into caller-owned free-list node metadata with deterministic next-node indexes, a `BootPageAllocatorStateInitializer` that validates that node chain into value-type allocator state metadata, and a `BootPageAllocatorMutation` boundary that mutates caller-owned free-list metadata for allocation/free tests without touching live pages. `src/OS.Kernel/Runtime/NativeAotHeapBootstrapPlan.cs` consumes that allocator state to validate a future managed-heap page budget without allocating or satisfying object allocation helpers. `BootSequence.RunBootPageAllocatorReadiness` consumes the allocator-state initialization result and emits `BOOT_PAGE_ALLOCATOR_READY` or `BOOT_PAGE_ALLOCATOR_NOT_READY` markers with state, free-list, node, page, byte, and initialization metadata. `src/OS.Kernel/Boot/BootMemoryRegionMaterializer.cs` provides the narrower no-stdlib pointer path that the live EFI artifact uses.
- `FirmwareServicesGuard` and the `UEFI_GONE_SIM_*` host tests establish the first UEFI-gone simulation boundary.
- The EFI build can produce an explicit QEMU-tested UEFI-gone simulation artifact that reaches `UEFI_GONE_SIM_PLATFORM_DENIED`, `POST_FIRMWARE_DIAGNOSTICS_READY`, and `UEFI_GONE_SIM_PASS`.
- `tools/OS.ImageBuilder` creates a raw GPT/FAT32 EFI System Partition image.
- `tools/OS.QemuRunner` boots the raw image through QEMU/OVMF and verifies serial markers.
- ACPI RSDP/RSDT/XSDT/MCFG, PCI ECAM/header/BAR/scanner, virtio PCI/device-init/queue, virtio-blk, virtio-net, storage package, network protocol, PAL socket/config/file, and native-app launch readiness slices are represented as host-tested contracts.
- `OSWorkloadProfile` selects either `SingleMinimalApi` or `MultiMinimalApi` at build time and exposes the selected profile through `OS.Kernel.Build.WorkloadProfile`. This is compile-time metadata only, not runtime support evidence.

## Unsupported Behavior

No real kernel runtime, complete exported PAL, `ExitBootServices` path, owned hardware drivers, scheduler, runtime storage I/O, live network I/O, app launcher execution, hosted .NET 10 runtime, ASP.NET Minimal API path, Kestrel path, or completed workload profile exists. ACPI probe evidence does not imply ECAM mapping, PCI enumeration, APIC/timer use, or post-firmware physical-memory access.

## Tests

Host tests verify boot marker emission, boot event ring-buffer behavior, typed handoff metadata validation, memory-map copy planning, raw descriptor decoding, allocated memory-map conversion, caller-owned memory-region writing, no-stdlib boot memory-region materialization, allocator-facing compact region planning, boot free-list segment planning, caller-owned free-list node construction, boot page allocator-state initialization, caller-owned boot page allocator mutation, boot page-allocator readiness markers, UEFI-gone simulation behavior, host-tool command contracts, ACPI marker preset ordering, and the current parser/planning contracts. QEMU smoke verifies the firmware-visible EFI marker path, UEFI memory-map readiness marker, owned-map copy marker, compact boot-region materialization marker/count, ACPI RSDP handoff marker, ordered boot marker plan, and the opt-in ACPI physical discovery marker chain.

## Known Limitations

The current architecture is documented, not yet enforced by build-time analyzers.

The host-friendly `MemoryMapInfo` region writer, `BootMemoryRegionPlanner`, `BootPageAllocatorPlanner`, `BootPageFreeListBuilder`, `BootPageAllocatorStateInitializer`, `BootPageAllocatorMutation`, `NativeAotHeapBootstrapPlanner`, and `BootSequence.RunBootPageAllocatorReadiness` marker gate are still host-tested only. Live EFI now normalizes a compact `EfiMemoryMapSummary` and materializes compact `BootMemoryRegionRecord` entries, but it does not yet run allocator-facing planning in `EfiMain`, reserve kernel-owned ranges, install allocator-owned mutable free-list state in the live path, reserve managed heap pages, configure paging, enforce NX/W^X, or allocate pages.

## Next Steps

1. Finish UEFI platform interfaces and post-firmware diagnostics before real firmware shutdown.
2. Add post-firmware physical-memory and MMIO ownership rules so the ACPI probe evidence can become durable platform discovery and PCI contracts can move from caller-owned bytes to live platform enumeration.
3. Keep all shared utility implementation in `std/no-runtime/shared` before kernel/app use.
4. Use `docs/workload-profiles.md` to keep the first hosted workload target on `SingleMinimalApi` while tracking the additional process/isolation work needed before `MultiMinimalApi`.
