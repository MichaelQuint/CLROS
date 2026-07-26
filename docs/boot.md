# Boot

## Current Status

Boot has a firmware-visible QEMU/OVMF smoke proof. `src/OS` publishes a no-stdlib NativeAOT EFI application, `tools/OS.ImageBuilder` wraps it in a raw GPT/FAT32 EFI System Partition image, and `scripts/run-qemu-smoke.ps1` boots that image in QEMU/OVMF and captures serial markers.

This is still an EFI application smoke, not a functional kernel. The current evidence proves that firmware reaches `EfiMain`, that `EfiMain` can capture the UEFI memory map on the tested QEMU host, copy the raw descriptor bytes into an OS-owned static buffer, validate the live owned handoff record, normalize and accept a live owned-map summary, materialize compact boot memory-region records into OS-owned static storage, carry a discovered ACPI RSDP pointer into the typed handoff record, optionally validate live ACPI RSDP/root SDT/MCFG bytes before `ExitBootServices` in an explicit probe build, and emit the ordered boot phase marker plan.

## Implemented Behavior

- `BootSequence.Run` emits host-tested boot phase markers from `BootSequencePlan`.
- `BootSequence.Run(BootHandoffRecord, ...)` validates the no-stdlib-safe handoff record before later managed boot phases and emits `BOOT_HANDOFF_READY` when the handoff passes.
- `BootSequence.Run(BootHandoffRecord, EfiMemoryMapSummary, ...)` adds a host-tested normalized memory-map precondition and emits `BOOT_MEMORY_MAP_ACCEPTED` only after the summary passes deterministic validation.
- `BootSequencePlan` is an allocation-free byte-indexed marker plan that can be source-linked into the no-stdlib EFI project.
- `OS_BOOT_OK` and `OS_SMOKE_PASS` are defined, host-tested, and QEMU-tested.
- `src/OS/OS.csproj` defines an explicit no-stdlib NativeAOT EFI publish boundary with exported `EfiMain`.
- `EfiMain` writes machine-readable firmware-console markers through the
  no-stdlib `EfiConsoleAdapter`, which encapsulates the current firmware console
  and Serial I/O fallback byte writes without using managed interface dispatch
  in the EFI artifact:
  - `TEST_START efi.main`
  - `EFI_CONSOLE_READY`
  - `UEFI_MEMORY_MAP_READY` or `UEFI_MEMORY_MAP_FAIL`
  - `UEFI_MEMORY_MAP_DESCRIPTOR_COUNT <count>` after a successful capture
  - `UEFI_MEMORY_MAP_OWNED` after the descriptor bytes are copied into the OS-owned static buffer
  - `BOOT_HANDOFF_READY` after the live owned-map handoff validates
  - `UEFI_MEMORY_MAP_NORMALIZED` after the live owned descriptor bytes are summarized
  - `BOOT_MEMORY_MAP_ACCEPTED` after the normalized summary passes the boot-boundary precondition
  - `BOOT_MEMORY_REGIONS_READY` after compact boot memory-region records are written into OS-owned static storage
  - `BOOT_MEMORY_REGION_COUNT <count>` after the live region materializer records the exact region count
  - `BOOT_PAGE_ALLOCATOR_READY status=Ready regions=<count> conventionalPages=<pages> conventionalBytes=<bytes> freeNodes=<nodes> freePages=<pages> freeBytes=<bytes> pageSize=4096 statusCode=0` after the live EFI path validates conventional-memory region metadata into a static free-list metadata chain
  - `BOOT_ACPI_RSDP_HANDOFF_READY` after a discovered ACPI RSDP pointer is copied into the typed handoff record and revalidated
  - `UEFI_ACPI_RSDP_READY` after firmware configuration-table ACPI RSDP discovery succeeds
  - `ACPI_RSDP_SNAPSHOT_READY`, `ACPI_RSDP_VALIDATED`, `ACPI_ROOT_SDT_SNAPSHOT_READY`, `ACPI_ROOT_SDT_VALIDATED`, and `ACPI_MCFG_DISCOVERY_READY` only in the disabled-by-default ACPI physical discovery probe build
  - `TEST_START boot.sequence`
  - ordered `BOOT_PHASE` markers
  - `OS_BOOT_OK`
  - `TEST_PASS boot.sequence`
  - `OS_SMOKE_PASS`
- `EfiMain` delegates the UEFI `GetMemoryMap` buffer-size retry pattern, slack allocation, copy-plan validation, and raw descriptor byte copy to the source-linked no-stdlib-safe `UefiMemoryMapCapture` helper, then records the returned metadata in the live snapshot and handoff record.
- `EfiMain` logs the captured descriptor count through `UEFI_MEMORY_MAP_DESCRIPTOR_COUNT <count>` so QEMU smoke can prove memory-map capability and count without dumping the full map.
- `EfiMain` populates the no-stdlib-safe `BootHandoffRecord` with boot mode, capability flags, firmware-services mode, source memory-map metadata, owned memory-map metadata, and an owned-copy flag after a successful memory-map copy.
- After ACPI RSDP discovery succeeds, `EfiMain` updates the same handoff record with pointer provenance, ACPI table kind, scanner status, and the `AcpiRsdp` capability bit, then validates the record again before emitting `BOOT_ACPI_RSDP_HANDOFF_READY`.
- If firmware console is unavailable, `EfiMain` falls back to UEFI Serial I/O
  through the same no-stdlib console adapter.
- `BootInfo` records boot mode, firmware vendor/revision, and initial capability flags for text output, shutdown, and memory map availability.
- `BootHandoffInfo` defines a host-tested typed metadata contract for boot mode, capabilities, firmware-services mode, memory-map metadata, and ACPI RSDP pointer provenance.
- `PhysicalAddress`, `VirtualAddress`, and `DeviceAddress` define three distinct host-tested address domains backed by one private `ulong` each and treat zero/default as invalid. Raw ingress and egress exist only through status-coded methods whose names identify a UEFI or hardware-register boundary; there is no generic `Value`, `TryCreate`, cast, or conversion escape. Checked arithmetic, alignment, range-end, and same-domain comparison operations reject default/zero input, zero-length ranges, invalid or unmet power-of-two alignment, and addition/align-up wrap while leaving every failure output deterministic.
- `BootReservationKind` and `BootReservedRange` define a separate host-tested CLROS ownership ledger rather than reusing `BootMemoryReservedRange`, which classifies non-conventional UEFI descriptors and may legitimately describe page zero. The new 40-byte readonly unmanaged record stores an explicit append-only kind value, zeroed reserved bytes, typed nonzero `PhysicalAddress` start/end values, page count, and byte length. Its status-coded factory and structural validator require 4 KiB alignment, checked page-to-byte multiplication, and a nonwrapping half-open end.
- `BootReservationTableValidator` is the host-tested, no-stdlib-compatible pointer/count/capacity core for an already-sorted caller-owned ownership ledger. It bounds the table at 256 records and rejects insufficient declared capacity, missing or misaligned pointers, and a wrapping full table-address span before dereferencing. It then validates every redundant range field, preserves the source byte-for-byte, permits adjacency, and reports unsorted, exact-duplicate, nested, partial-overlap, and loaded-image duplicate-coverage failures with deterministic invalid/conflicting indexes and prefix totals. If either `BootStackGuard` or `BootStack` is present, exactly one of each is required, the guard must immediately precede an adjacent stack, and their checked union is reported as the modeled arena. A table with neither record remains valid for earlier partial-ledger stages.
- `EfiLoadedImageProtocolObservation`, `EfiLoadedImageRange`, and `EfiLoadedImageRangeCapturePlan` define a host-tested, no-stdlib-compatible loaded-image capture contract. One 40-byte observation atomically records the firmware status, input image handle, transient protocol address, image base, and image size. The exact-one pointer/count/capacity evaluator rejects malformed metadata and an invalid 4 KiB page-size contract before its single observation read, then validates handle/status/pointer/base/size in deterministic order, checks exact-end and outward-rounding arithmetic, and constructs the canonical `LoadedImage` reservation. Its 144-byte value result contains an 80-byte exact range and canonical reservation but does not retain the protocol pointer. This compact observation is not ABI-compatible with the native x64 `EFI_LOADED_IMAGE_PROTOCOL` and must never be cast over firmware memory.
- `BootSequenceHandoffValidator` validates the boot source, required text-output and memory-map capabilities, firmware-services mode, owned memory-map copy flag, source-buffer provenance, descriptor size, descriptor count, map-size arithmetic, descriptor-count limits, and ACPI RSDP metadata consistency without exceptions.
- `EfiBootHandoffValidator` is the source-linked no-stdlib-safe numeric validator used by the live EFI artifact before it emits `BOOT_HANDOFF_READY`.
- `EfiMemoryMapNormalizer` is the source-linked no-stdlib-safe live normalizer that walks owned raw descriptor bytes by pointer, validates page/address arithmetic, and records a compact summary without allocating.
- `BootMemoryMapAcceptance` is the source-linked no-stdlib-safe acceptance contract for the normalized summary. It rejects missing, zero-descriptor, zero-page, zero-byte, invalid-range, and no-conventional-memory summaries before managed phase markers.
- `BootMemoryRegionMaterializer` is the source-linked no-stdlib-safe live materializer that walks the owned raw descriptor bytes, validates descriptor metadata, page counts, address ranges, totals, destination capacity, and 40-byte or 48-byte descriptor strides, then writes compact `BootMemoryRegionRecord` entries into caller-owned storage without allocation.
- `BootMemoryRegionPlanner` is the host-tested allocator-facing planning boundary after compact region materialization. It consumes caller-provided `BootMemoryRegionRecord` values, validates page counts, byte lengths, physical ends, sorted/non-overlapping order, destination capacity, and arithmetic overflow, then writes conventional-memory `BootMemoryUsableRange` entries and reserved/unusable `BootMemoryReservedRange` metadata into caller-owned spans without allocating.
- `BootPageAllocatorPlanner` is the host-tested free-list planning boundary after usable/reserved range selection. It consumes caller-owned `BootMemoryUsableRange` and `BootMemoryReservedRange` spans, validates page size, range alignment, range shape, sorted/non-overlapping usable ranges, non-overlapping reserved ranges, destination capacity, and arithmetic overflow, then writes `BootPageFreeSegment` metadata into caller-owned storage without allocation.
- `BootPageFreeListBuilder` is the host-tested free-list node construction boundary after successful free-segment planning. It consumes `BootPageAllocatorPlanResult` plus caller-owned `BootPageFreeSegment` storage, rejects failed plans, insufficient source/destination capacity, invalid page size, unaligned or malformed segment metadata, unsorted or overlapping segments, arithmetic overflow, and total-count mismatches, then writes `BootPageFreeListNode` metadata with deterministic next-node indexes into caller-owned storage only after validation succeeds.
- `BootPageAllocatorStateInitializer` is the host-tested metadata installation boundary after free-list node construction. It consumes a successful `BootPageFreeListPlanResult` plus caller-owned `BootPageFreeListNode` storage, rejects failed or empty free lists, insufficient node capacity, invalid head/next indexes, malformed node ranges, cycles, node-count mismatches, and total-count mismatches, then returns value-type allocator state metadata without allocating pages or mutating the free list.
- `BootPageAllocatorMutation` is the host-tested caller-owned free-list mutation boundary after allocator-state initialization. It can allocate aligned page ranges from caller-owned `BootPageFreeListNode` storage, free previously allocated ranges back into the sorted free-list, split and coalesce nodes, reject malformed chain metadata, and report status-coded allocation/free failures without configuring paging, enforcing NX/W^X, touching live EFI memory, or allocating managed objects.
- `BootPageAllocatorReadinessComposer` is the host-tested aggregate boundary from compact boot memory-region records through usable/reserved range planning, free-segment planning, caller-owned free-list node construction, allocator-state initialization, and `BootSequence.RunBootPageAllocatorReadiness`. It returns nested status and capacity metadata and emits the ordered readiness marker only after the region-planning precondition succeeds.
- `NativeAotHeapBootstrapPlanner` is the host-tested runtime-facing boundary after allocator-state initialization. It consumes `BootPageAllocatorState` metadata, validates a future managed-heap page budget and object alignment, and keeps object allocation helper completion false; it does not reserve pages, install a heap, run GC root scanning, or perform write barriers.
- `BootSequence.RunNativeAotHeapBootstrapReadiness` is the host-tested ordered-boot marker boundary after heap bootstrap planning. It consumes `NativeAotHeapBootstrapPlanResult`; emits `NATIVEAOT_HEAP_BOOTSTRAP_READY status=Ready ... satisfiesObjectAllocationHelpers=false` only when the plan succeeds; and emits `NATIVEAOT_HEAP_BOOTSTRAP_NOT_READY status=<reason> ...` for default/uninitialized or failed plans while preserving allocator, requested-heap, remaining-free, alignment, GC-root, write-barrier, and object-allocation-helper metadata. It does not install a heap or implement allocation helpers.
- `BootSequence.RunNativeAotClassConstructorRunnerPreflightReadiness` is the host-tested ordered-boot marker boundary for the existing class-constructor runner preflight. It consumes `NativeAotClassConstructorRunnerPreflightResult`; emits `NATIVEAOT_CCTOR_RUNNER_PREFLIGHT_READY status=Ready ...` only when the preflight result is initialized and ready; and emits `NATIVEAOT_CCTOR_RUNNER_PREFLIGHT_NOT_READY status=<reason> ...` for default/uninitialized or failed preflight results while preserving type count, target metadata count, occupied/capacity metadata, first failing index/state/failure code, and the deferred invocation flags. It does not execute constructors, discover metadata, invoke delegates, or run module initializers.
- `NativeAotClassConstructorWorkItemPlanner` is the next host-tested cctor-runner prerequisite below that marker boundary. It consumes an already-successful preflight result, caller-owned state entries, caller-supplied target tokens, and caller-owned work-item storage, then plans which uninitialized entries would need future constructor dispatch. `NativeAotClassConstructorWorkItemPlanDiagnosticFormatter` formats those initialized plan results into caller-owned `NATIVEAOT_CCTOR_WORK_ITEMS status=` diagnostics, including no-execution/no-reflection/no-exception-flow/no-delegate/no-metadata-discovery flags. `BootSequence.RunNativeAotClassConstructorWorkItemReadiness` now exposes the same already-produced plan metadata through host-tested `NATIVEAOT_CCTOR_WORK_ITEMS_READY` / `NATIVEAOT_CCTOR_WORK_ITEMS_NOT_READY` markers while preserving plan status, initialized state, capacity, first-problem, and no-constructor/no-reflection/no-exception-flow/no-delegate/no-metadata-discovery flags. It is not wired into `BootSequence.Run`, emits no QEMU marker, and does not invoke constructors, delegates, reflection, metadata discovery, module initializers, exception handling, or lazy static reference fields.
- `BootSequence.RunBootPageAllocatorReadiness` is the host-tested ordered-boot boundary after allocator-state initialization. It consumes `BootPageAllocatorStateInitResult`, emits `BOOT_PAGE_ALLOCATOR_READY status=Ready state=Success freeList=Success ...` only when initialized state metadata contains free nodes and pages, and emits `BOOT_PAGE_ALLOCATOR_NOT_READY status=<reason> state=<state-status> freeList=<free-list-status> ...` with node, initialization, page, byte, and allocated-page metadata when state initialization fails, default state is uninitialized, or the initialized state has no free nodes/pages.
- `BootSequence.RunPalExportCatalogReadiness` is the host-tested PAL export-catalog readiness boundary. It consumes `OSHostExportCatalogValidationResult`; emits `PAL_EXPORT_CATALOG_READY status=Ready ...` only when the catalog is initialized and validates successfully; and emits `PAL_EXPORT_CATALOG_NOT_READY status=<reason> ...` for default/uninitialized or validation failures while preserving entry-count, failing-index, duplicate-index, domain, and signature metadata. It does not export unmanaged symbols.
- `EfiConfigurationTableScanner` defines a host-tested, no-stdlib-friendly contract for locating the ACPI RSDP pointer in UEFI configuration-table metadata. It recognizes ACPI 2.0 and ACPI 1.0 table GUIDs, prefers ACPI 2.0 when both are present, supports ACPI 1.0 fallback, rejects zero RSDP pointers, and returns status-coded results without parsing or dereferencing the RSDP.
- `IPlatformConfigurationTables` and `GuardedPlatformConfigurationTables` wrap ACPI RSDP configuration-table discovery behind a firmware-services guard. Host tests prove the scan is allowed while boot services are available and denied after UEFI-gone simulation or marked `ExitBootServices`.
- `IPlatformTimer` and `GuardedPlatformTimer` wrap monotonic tick/frequency metadata reads behind the same firmware-services guard. Host tests prove reads are allowed while boot services are available, denied after UEFI-gone simulation or marked `ExitBootServices`, and invalid zero-frequency metadata is rejected with a status.
- `IPlatformRandom` and `GuardedPlatformRandom` wrap firmware RNG byte fills behind the same firmware-services guard. Host tests prove caller-owned buffers can be filled while boot services are available, calls are denied after UEFI-gone simulation or marked `ExitBootServices`, empty destinations are rejected without touching the source, and firmware-source failures remain status-coded.
- `IPlatformPhysicalMemory` and `GuardedPlatformPhysicalMemory` wrap firmware/physical-memory snapshot reads behind the same firmware-services guard. Host tests prove caller-owned buffers can receive byte snapshots while boot services are available, calls are denied after UEFI-gone simulation or marked `ExitBootServices`, invalid ranges and undersized destinations are rejected before source reads, and source failures remain status-coded.
- `IPlatformFramebuffer` and `GuardedPlatformFramebuffer` wrap firmware framebuffer metadata snapshots behind the same firmware-services guard. Host tests prove injected metadata can be captured while boot services are available, calls are denied after UEFI-gone simulation or marked `ExitBootServices`, zero base/length, zero dimensions, invalid stride, unsupported pixel formats, byte-length mismatch, and arithmetic overflow remain status-coded, and failed sources do not become hidden success.
- `EfiMain` source-links the configuration-table scanner and emits `UEFI_ACPI_RSDP_READY`, `UEFI_ACPI_RSDP_MISSING`, or `UEFI_ACPI_RSDP_INVALID <status-code>`.
- `AcpiRsdpValidator` is a host-tested, allocation-free contract for validating RSDP bytes. It validates ACPI 1.0 signatures/checksums and ACPI 2.0+ declared length plus extended checksum, and the explicit ACPI physical discovery EFI probe uses the same rules through no-stdlib local validation before `ExitBootServices`.
- `AcpiRsdpSnapshotReader` composes `IPlatformPhysicalMemory` with `AcpiRsdpValidator` for host-tested RSDP byte snapshots. It reads the base header into caller-owned scratch storage, re-reads the extended header/full declared length when needed, and reports invalid physical addresses, missing physical-memory sources, insufficient scratch capacity, physical-read failures, and validation failures without exceptions or managed-reference result fields.
- `AcpiRootSdtSnapshotReader` composes a successful `AcpiRsdpSnapshotResult` with `IPlatformPhysicalMemory` to snapshot the root RSDT or XSDT bytes into caller-owned storage. It prefers XSDT when the validated RSDP supplies one, falls back to RSDT when ACPI 2.0+ has no XSDT address, validates root SDT signature, declared length, checksum, and pointer-array alignment, and returns status-coded failures without parsing child tables.
- `AcpiSdtParser` is a host-tested, allocation-light parser for caller-owned ACPI RSDT/XSDT table bytes. It decodes the common SDT header, validates declared length and checksum over the declared byte range, reads RSDT 32-bit and XSDT 64-bit pointer arrays into caller-provided storage, rejects zero pointers, and does not dereference physical addresses.
- `AcpiTableDirectory` is a host-tested selector that bridges parsed RSDT/XSDT physical-address lists to caller-provided SDT byte snapshots. It validates each snapshot's common SDT header, declared length, and checksum, then selects the first requested table signature in firmware pointer order while preserving the selected physical address for diagnostics. It does not read physical memory.
- `AcpiMcfgDiscovery` is a host-tested composition contract that selects the `MCFG` table through `AcpiTableDirectory`, parses it through `AcpiMcfgParser`, and writes `PciEcamSegment` records into caller-owned storage. It preserves selected table physical address, pointer/source indexes, nested directory status, nested MCFG parse status, segment count, and required segment capacity without physical-memory reads.
- `AcpiMcfgDiscovery.TryDiscoverFromRootSdtSnapshot` bridges the validated root RSDT/XSDT snapshot to child-table reads through `IPlatformPhysicalMemory`. It parses root pointers, reads child SDT headers through the platform boundary, selects `MCFG`, copies the full MCFG into caller-owned storage, validates checksum, parses ECAM segments, and reports all failures with value-type status metadata. It does not map ECAM or enumerate PCI devices.
- `Probes.EnableAcpiPhysicalDiscoveryProbe` defines the disabled-by-default live ACPI physical discovery probe contract. The explicit EFI build validates ACPI RSDP bytes, snapshots and validates the root RSDT/XSDT, scans child SDTs for a valid `MCFG`, parses ECAM segment metadata into fixed scratch storage, and emits the `ACPI_*` marker chain before the normal boot marker plan.
- `UefiExitBootServicesPlan` is a host-tested plan-only contract for fresh memory-map requirements, stale map-key retry, retry exhaustion, invalid handoff failures, and explicit skipped-call probe results before any real firmware shutdown call is added.
- `PlatformFirmwareExitBootServicesResult` is a host-tested firmware-call result contract for the future live `ExitBootServices` attempt. It maps guard denial, missing function pointer, EFI success, EFI invalid-parameter/stale-map-key, and generic firmware errors into status values that feed `UefiExitBootServicesAttemptMapper` and the existing retry plan without calling firmware.
- `Probes.EnableExitBootServicesFreshMapProbe` defines the disabled-by-default probe contract for a future fresh-memory-map `ExitBootServices` attempt. It has a stable `uefi.exitbootservices` probe group, and the explicit EFI probe build emits `UEFI_EBS_FRESH_MAP_READY` plus `UEFI_EBS_CALL_SKIPPED` without calling `ExitBootServices`.
- `UefiMemoryMapCopyPlan` defines a no-stdlib-safe, status-coded validation contract for copying a firmware-owned memory-map buffer into OS-owned storage.
- The copy-plan contract validates source map size, descriptor size/version/count, descriptor-count bounds, destination capacity, and arithmetic overflow before any EFI-boundary copy is attempted.
- `UefiMemoryMapCapture` owns the no-stdlib-safe `GetMemoryMap` sizing call, `AllocatePool` slack buffer allocation, retry capture, copy-plan validation, and copy into caller-owned storage. It returns status-coded capture failures plus the underlying `UefiMemoryMapCopyPlanStatus` when metadata or destination validation rejects the copy.
- `UefiMemoryDescriptorReader` decodes owned raw UEFI descriptor bytes into `UefiMemoryDescriptor` values with caller-provided storage, status-coded failures, and explicit 40-byte and 48-byte descriptor-stride support.
- `MemoryMapInfo` and `MemoryRegion` convert host-side UEFI descriptors into first-party normalized memory-map contracts with status-coded validation; the allocated host-friendly API remains available, and a caller-owned region writer now writes `MemoryRegion` records into supplied storage while reporting capacity, totals, conventional-memory totals, and invalid descriptor indexes without throwing.
- Missing or unknown boot capabilities return status-coded `BootCapabilityResult` values; they do not rely on managed exception handling.
- `FirmwareServicesGuard` begins UEFI-gone preparation by denying firmware-backed calls after simulated or real boot-services shutdown modes.
- `GuardedPlatformConsole`, `GuardedPlatformMemoryMap`, `GuardedPlatformConfigurationTables`, `GuardedPlatformTimer`, `GuardedPlatformRandom`, `GuardedPlatformPhysicalMemory`, and `GuardedPlatformFramebuffer` provide host-tested adapter boundaries for UEFI-gone simulation. `PlatformUefiGoneSimulation` now verifies that all seven guarded adapters deny firmware-backed calls after the simulated boundary before any real firmware shutdown is attempted, and includes allowance-detection tests for the older console path and the newer timer path.
- `BootSequence.RunUefiGoneSimulation` emits host-tested `UEFI_GONE_SIM_START`, `UEFI_GONE_SIM_PLATFORM_DENIED`, `POST_FIRMWARE_DIAGNOSTICS_READY`, `UEFI_GONE_SIM_PASS`, and status-coded failure markers. The compile-time probe is present and defaults off.
- `scripts/build-efi-shell.ps1 -EnableUefiGoneSimulationProbe` builds a QEMU-testable EFI variant that emits `UEFI_GONE_SIM_START`, `UEFI_GONE_SIM_PLATFORM_DENIED`, `POST_FIRMWARE_DIAGNOSTICS_READY`, and `UEFI_GONE_SIM_PASS` without calling real `ExitBootServices`.
- `BootSequence.RunPostFirmwareDiagnosticsReadiness` defines the post-firmware diagnostics gate that must pass before real firmware shutdown work advances. It consumes `SerialDiagnosticsGuardResult`, emits `POST_FIRMWARE_DIAGNOSTICS_READY` only when OS-owned serial diagnostics are explicitly ready, and emits `POST_FIRMWARE_DIAGNOSTICS_BLOCKED status=<status>` for firmware-console-only, firmware-services-unavailable, serial-unavailable, or invalid-transition states.
- `GuardedPlatformSerial`, `Com1PlatformSerialTransport`, and `OwnedSerialDiagnostics` provide the host-testable serial diagnostics boundary behind that gate: firmware-backed serial output can be used before UEFI-gone, UEFI-gone simulation requires an owned serial transport, the COM1 transport can satisfy that owned boundary over a fakeable byte-port backend, and marker/panic preamble lines are formatted into caller-owned byte storage.
- `BootSequence.RunExitBootServicesPreflight` defines the combined preflight gate before the future real firmware shutdown call. It consumes post-firmware diagnostics readiness, `UefiExitBootServicesPlanResult`, and `FirmwareServicesGuard`; emits `UEFI_EBS_PREFLIGHT_READY` only when diagnostics are ready and the plan succeeded; emits `UEFI_EBS_PREFLIGHT_BLOCKED diagnostics=<status> plan=<status>` otherwise; and does not call firmware.
- `UefiExitBootServicesLiveWiringPlan` records the intended live call order without enabling it: capture a fresh memory map, confirm owned serial diagnostics, emit the post-firmware diagnostics marker, validate the transition plan, call firmware only in a future enabled path, mark firmware exited only after success, then reject firmware-backed calls.
- `BootSequence.RunCpuFaultPanicReadiness` defines a host-testable diagnostics
  marker boundary for the future fault-entry panic path. It consumes
  `CpuFaultPanicReadinessResult`, emits `CPU_FAULT_PANIC_READY` only after
  caller-owned frame bytes decode and flush through the panic serial reporter,
  emits `CPU_FAULT_PANIC_NOT_READY ...` with nested frame/reporter status on
  failure, and does not install handlers or trigger CPU faults.
- `BootSequence.RunAppPackageReadiness` defines a host-testable marker boundary for a future app-package handoff. It consumes the native manifest load result, OS package parse result, and native app launch-plan result; emits `APP_PACKAGE_READY` only when all three succeed; and emits `APP_PACKAGE_NOT_READY manifest=<status> package=<status> launch=<status>` when any prerequisite fails.
- `BootSequence.RunAppLaunchDescriptorReadiness` defines the next host-testable marker boundary for future app launch selection. It consumes `NativeAppLaunchDescriptorResult`; emits `APP_LAUNCH_READY` only when manifest launch-plan data resolves to a package file entry; and emits `APP_LAUNCH_NOT_READY status=<status> launch=<status> packageFile=<status> package=<status>` when descriptor creation fails.
- `BootSequence.RunAppPayloadReadiness` defines the selected native payload marker boundary. It consumes `NativeAppLaunchDescriptorResult`; emits `APP_PAYLOAD_READY` only when the descriptor and v0 payload header are valid; and emits `APP_PAYLOAD_NOT_READY descriptor=<status> payload=<status>` when descriptor creation or payload-header validation fails. It does not execute app code.
- `BootSequence.RunAppExecutableMappingReadiness` defines the selected native executable mapping marker boundary. It consumes `NativeAppPayloadMappingPlanResult`; emits `APP_EXEC_MAPPING_READY` only when the host-tested payload range plan succeeded; and emits `APP_EXEC_MAPPING_NOT_READY status=<status> descriptor=<status> payload=<status>` when range planning fails. It does not map or execute app code.
- `BootSequence.RunAppExecutableMemoryReadiness` defines the selected native executable memory marker boundary. It consumes `NativeAppExecutableMemoryPlanResult`; emits `APP_EXEC_MEMORY_READY` only when the host-tested executable-memory plan succeeded; and emits `APP_EXEC_MEMORY_NOT_READY status=<status> mapping=<mapping-status> descriptor=<descriptor-status> payload=<payload-status>` when planning fails. It does not allocate executable pages, change page protections, copy payload bytes, relocate code, or invoke app entry.
- `BootSequence.RunAppInvocationReadiness` defines the selected native app invocation metadata marker boundary. It consumes `NativeAppInvocationPlanResult`; emits `APP_INVOCATION_READY` only when invocation metadata planning succeeded; and emits `APP_INVOCATION_NOT_READY status=<status> memory=<memory-status> launch=<launch-status>` when invocation planning fails. It does not allocate stacks, switch stacks, create threads, define a final ABI, map pages, or execute app code.
- `BootSequence.RunAppLifecycleReadiness` defines the native app lifecycle readiness boundary after invocation metadata and app-log readiness are known. It consumes `NativeAppLifecyclePlanResult`; emits `APP_LIFECYCLE_READY` only when invocation metadata is valid and a bounded app log sink can hold machine-readable exit/status markers; and emits `APP_LIFECYCLE_NOT_READY status=<status> invocation=<status> log=<status>` when invocation or log prerequisites fail. It does not allocate stacks, start a supervisor, route live stdout/stderr, execute app code, capture a real exit, or enforce restart policy.
- `BootSequence.RunAppPackageStagingReadiness` defines the host-testable marker
  boundary after package file staging, payload mapping/staging,
  executable-memory planning, and invocation planning have all been composed.
  It emits `APP_PACKAGE_STAGING_READY` only when
  `NativeAppPackageStagingReadinessResult` succeeded, and emits
  `APP_PACKAGE_STAGING_NOT_READY ...` with nested dependency status otherwise.
  It does not read live storage, allocate staging buffers, map executable pages,
  change permissions, relocate code, or execute app code.
- `BootSequence.RunAppSelectionReadiness` defines the aggregate host-testable boundary for future selected-app handoff. It consumes native manifest load, package parse, launch-plan, and launch-descriptor results; emits `APP_SELECTION_READY` only when all four prerequisites succeed; and emits `APP_SELECTION_NOT_READY status=<status> manifest=<status> package=<status> launch=<status> descriptor=<status> packageFile=<status> descriptorPackage=<status>` when the selection boundary is blocked. `NativeAppSelectionLoader` can now produce those prerequisite status objects from one caller-buffer GPT ESP image in host tests, but the default boot path does not call it yet.
- `BootSequence.RunAppSelectionPreflightReadiness` defines a host-testable marker boundary for the virtio-blk app-selection preflight wrapper. It consumes `VirtioBlockNativeAppSelectionPreflightResult`; emits `APP_SELECTION_PREFLIGHT_READY` only for an initialized successful preflight; and emits `APP_SELECTION_PREFLIGHT_NOT_READY status=<status> initialized=<true|false> preflight=<status> readSession=<status> pipeline=<status> block=<status> scratch=<status> selection=<status> manifest=<status> packageLoad=<status> package=<status> launch=<status> descriptor=<status>` for default/uninitialized, read-session, block-device, scratch, or selection-loader failures. It is not wired into the default boot path.
- `BootSequence.RunAppPackageFileStagingPreflightReadiness` defines a host-testable marker boundary for the virtio-blk package-file staging preflight wrapper. It consumes `VirtioBlockPackageFileStagingPreflightResult`; emits `APP_PACKAGE_FILE_STAGING_PREFLIGHT_READY status=Ready ...` only when read-session package preflight, package file open, and caller-owned staging all succeeded; and emits `APP_PACKAGE_FILE_STAGING_PREFLIGHT_NOT_READY status=<reason> ...` for default/uninitialized, package preflight, package open, or staging failures while preserving package-buffer capacity, staging-destination capacity, file length, and staged-byte metadata. It is not wired into the default boot path.
- `BootSequence.RunFetchAppHttpRequestEndpointReadiness` defines a host-testable marker boundary for future FetchApp request endpoint setup. It consumes an already-produced `FetchAppHttpRequestEndpointResult`; emits `FETCHAPP_HTTP_ENDPOINT_READY status=Ready ...` only when endpoint metadata is initialized and successful; and emits `FETCHAPP_HTTP_ENDPOINT_NOT_READY status=<reason> ...` for default/uninitialized or failed endpoint results while preserving status, port, host/path byte-count, required/available byte-count, and config-source flag metadata without dumping host or path values. It is not wired into the default boot path and does not read live manifests, resolve DNS, open sockets, perform HTTP I/O, or execute FetchApp.
- `BootSequence.RunFetchAppHttpCompletionReadiness` defines a host-testable marker boundary for future FetchApp completion. It consumes `FetchAppHttpCompletionPlanResult`; emits `FETCH_APP_HTTP_COMPLETION_READY status=Ready ...` only when the already-produced HTTP TCP GET completion metadata succeeded; and emits `FETCH_APP_HTTP_COMPLETION_NOT_READY status=<reason> ...` for default/uninitialized or failed completion results while preserving HTTP plan, status-code, response-length, body-range, expected-marker, and match-offset metadata. It is not wired into the default boot path and does not perform DNS, TCP, HTTP, or app execution.
- `BootSequence.RunNativeManifestFetchAppConfigPreflightReadiness` defines a host-testable marker boundary for manifest-derived FetchApp completion configuration. It consumes `NativeManifestFetchAppConfigPreflightResult`; emits `FETCHAPP_CONFIG_PREFLIGHT_READY status=Ready ...` only when the manifest load, expectation planning, and marker-copy preflight succeeded; and emits `FETCHAPP_CONFIG_PREFLIGHT_NOT_READY status=<reason> ...` for default/uninitialized or failed preflight results while preserving initialization, preflight, manifest load/parse, expectation, expected status-code, marker byte-count, configured/default marker, configured status, and manifest byte-count metadata. It is not wired into the default boot path and does not read live storage, execute FetchApp, or perform HTTP I/O.
- `BootSequence.RunNativeManifestEnvironmentReadiness` defines a host-testable marker boundary for manifest environment handoff metadata. It consumes an already-produced `NativeManifestParseResult` plus original manifest bytes; emits `NATIVE_MANIFEST_ENV_READY <formatter-payload>` only when `NativeManifestEnvironmentDiagnosticFormatter` succeeds; and emits `NATIVE_MANIFEST_ENV_NOT_READY ...` for not-initialized, parse-failure, invalid-source-range, or diagnostic-formatting-failure inputs while preserving initialized state, parse status, format status, byte-count metadata, and explicit `processEnvironmentMaterialized=false`, `hostedDotNetStarted=false`, and `kestrelStarted=false` flags. It is not wired into the default boot path and does not read live storage, create process environment variables, start hosted .NET, or start Kestrel.
- `BootSequence.RunNativeAppExitObserverReadiness` defines a host-testable marker boundary for future app-exit observation. It consumes `NativeAppExitLogObserverResult`; emits `APP_EXIT_OBSERVER_READY status=Ready ...` only when an initialized observer result found a valid machine-readable stdout `APP_EXIT` marker; and emits `APP_EXIT_OBSERVER_NOT_READY status=<reason> ...` for default/uninitialized or failed observer results while preserving record scan, stream, flag, byte-count, log status, parse status, exit status, and launcher-failure metadata. It is not wired into the default boot path and does not observe a live app, ingest serial logs, or enforce restart policy.
- `BootSequence.RunNativeAppArgumentVectorReadiness` defines a host-testable marker boundary for future app argument handoff. It consumes `NativeAppArgumentVectorPlanResult`; emits `APP_ARGS_VECTOR_READY status=Ready ...` only when the argument vector was initialized and parsed successfully; and emits `APP_ARGS_VECTOR_NOT_READY status=<reason> ...` for default/uninitialized or failed argument-vector plans while preserving planner status, launch status, argument count, maximum argument count, maximum argument length, error offset, and explicit `executesApp=false` / `startsKestrel=false` flags. It is not wired into the default boot path and does not allocate process strings, define argc/argv ABI storage, execute app code, start hosted .NET, or start Kestrel.
- `BootSequence.RunNativeAppArgumentHandoffReadiness` defines the next host-testable marker boundary after invocation metadata and argument-vector planning. It consumes `NativeAppArgumentHandoffPlanResult`; emits `APP_ARGS_HANDOFF_READY status=Ready ...` only when the result is initialized and successful; and emits `APP_ARGS_HANDOFF_NOT_READY status=<reason> ...` for default/uninitialized or failed handoff results while preserving handoff, invocation, argument-vector, argument-count, parsed/raw argument-byte, and stack-unavailable metadata. It is not wired into the default boot path and does not materialize `argc`/`argv`, allocate stacks, switch stacks, execute app code, start hosted .NET, start ASP.NET Minimal API, or start Kestrel.
- `BootSequence.RunVirtioNetRxTcpDispatchReadiness` defines an opt-in networking diagnostic boundary after a host-tested virtio-net RX TCP dispatch result already exists. It emits `VIRTIO_NET_RX_TCP_DISPATCH_READY` followed by the existing `VIRTIO_NET_RX_TCP_DISPATCH status=...` diagnostic marker only for initialized successful dispatch results whose formatter succeeds. It emits `VIRTIO_NET_RX_TCP_DISPATCH_NOT_READY status=<reason> ...` for default/uninitialized, failed-dispatch, or diagnostic-formatting failures. It is not wired into the default boot path and does not poll a virtio-net queue, process interrupts, transfer packets, expose sockets, run DHCP/DNS/HTTP, start hosted .NET, start ASP.NET Minimal API, or start Kestrel.
- `BootSequence.RunHttpStaticTcpResponseReadiness` defines an opt-in HTTP static TCP response marker boundary after a host-tested `HttpStaticTcpResponsePlanResult` already exists. Successful inputs emit the formatted `HTTP_STATIC_TCP_RESPONSE status=...` diagnostic marker followed by `HTTP_STATIC_TCP_RESPONSE_READY`; default/uninitialized, failed-plan, or diagnostic-formatting failures emit `HTTP_STATIC_TCP_RESPONSE_NOT_READY status=<reason> ...` with nested TCP/HTTP/formatter metadata. It is not wired into the default boot path and does not bind/listen sockets, publish response frames, touch virtio-net, start hosted .NET, start ASP.NET Minimal API, or start Kestrel.
- `BootSequence.RunAppEntryReadiness` defines the final pre-entry host-testable boundary for the current native-tier chain. It consumes manifest-load, package-parse, launch-plan, selected descriptor, payload-mapping, executable-memory, and invocation-plan results; emits `APP_ENTRY_READY` only when all prerequisites succeeded; and emits `APP_ENTRY_NOT_READY status=<status> manifest=<status> package=<status> launch=<status> descriptor=<status> mapping=<status> memory=<status> invocation=<status>` when any dependency blocks the entry boundary. It does not allocate stacks, switch stacks, map pages, relocate code, or invoke app entry.

## Unsupported Behavior

- Live page allocator/free-list ownership over planned boot memory ranges. `BootMemoryRegionPlanner` classifies compact records into usable conventional ranges and reserved/unusable metadata, `BootPageAllocatorPlanner` produces host-tested initial free-segment metadata from those selected ranges, `BootPageFreeListBuilder` constructs caller-owned free-list node metadata from the successful plan, `BootPageAllocatorStateInitializer` validates that node chain into value-type allocator state metadata, `BootPageAllocatorMutation` mutates caller-owned free-list metadata in host tests, `BootPageAllocatorReadinessComposer` composes those existing pieces over caller-owned scratch buffers, and `BootSequence.RunBootPageAllocatorReadiness` emits a host-tested readiness marker from initialized allocator-state metadata. None of those pieces reserve kernel ranges, allocate or free live pages, configure paging, enforce NX/W^X, or run in the live EFI path.
- Live use of the typed address domains, loaded-image capture plan, or canonical reservation ledger. The exact typed-address, reservation-record, pointer-validator, loaded-image range, and capture-plan sources have retained isolated `NOSTDLIB_EFI` compiler tests, but they are not source-linked into `src/OS/OS.csproj`, invoked by `EfiMain`, or applied retroactively to existing raw-`ulong` memory, MMIO, PCI, and DMA interfaces. Named boundary methods make raw-value intent auditable; they are not a capability boundary. The loaded-image planner validates one caller-owned compact transcript; it does not call `HandleProtocol`/`OpenProtocol`, dereference the observed protocol address, or prove that the transcript came from the running image. The host validator proves range shape, rounding, canonical reservation construction, ordering, overlap, capacity, totals, and modeled guard/stack-union policy only for supplied values. It does not populate the ledger, prove that omitted metadata is actually contained in the loaded image, prove EFI `LoaderCode`/`LoaderData` allocation provenance or a refreshed map generation, reserve/subtract/reclaim live memory, establish allocator ownership, switch stacks, map pages, load CR3, or enforce cache, NX, or W^X policy.
- Real `ExitBootServices`.
- Complete platform implementations for console, memory map, keyboard, hardware timer, firmware, filesystem, physical-memory mapping, framebuffer/GOP binding, and post-firmware RNG.
- IDT/GDT/TSS/IST setup.
- Fault handlers.
- Real owned serial driver after firmware shutdown. The post-firmware diagnostics gate and `IPlatformSerial` boundary are host-tested only and are not wired into the live EFI path.
- Default-build live ACPI RSDT/XSDT discovery from the firmware-provided RSDP pointer or from real physical memory.
- Boot-time app package loading or app launch through the default `BootSequence.Run` or live `EfiMain` path.

## Tests

Host tests verify the marker plan, `BootSequence.Run`, handoff-aware `BOOT_HANDOFF_READY` validation, `BOOT_MEMORY_MAP_ACCEPTED` validation, boot capability contract, typed handoff metadata including owned-map and ACPI RSDP records, typed physical/virtual/device address POD layout and checked arithmetic, UEFI memory-map contract, memory-map capture-helper success/failure paths, memory-map copy-plan validation, raw descriptor decoding, normalized memory-map conversion, caller-owned `MemoryMapInfo` region writing, no-stdlib boot memory-region materialization, allocator-facing boot memory-region planning, boot free-list segment planning, caller-owned boot free-list node construction, boot page allocator-state initialization, caller-owned boot page allocator mutation, aggregate boot page allocator-readiness composition, boot page-allocator readiness markers, NativeAOT heap bootstrap readiness markers, NativeAOT cctor runner preflight readiness markers, PAL export catalog readiness markers, app package-file staging preflight markers, app argument-vector readiness markers, app argument-handoff readiness markers, app-exit observer readiness markers, ACPI RSDP configuration-table scanning and byte validation, guarded platform configuration-table scanning, guarded physical-memory-backed RSDP snapshot validation, caller-owned ACPI RSDT/XSDT pointer-list parsing, caller-provided ACPI table directory selection, ACPI MCFG discovery composition, ACPI physical discovery probe metadata, firmware guard contract, guarded platform adapter denial, and UEFI-gone simulation markers:

```powershell
dotnet run --project .\tests\OS.HostTests\OS.HostTests.csproj --no-build
```

M01-001 typed-address coverage is host-only. `CLROS_M01_001_READY` is emitted by
the host harness only after every focused layout, named-boundary, domain,
default-value, ordering, alignment, range-end, overflow, source-surface, and
compiler-closure check passes. The compiler fixture links the exact production
source into a temporary project with `NoStdLib=true`, framework references
removed, and `NOSTDLIB_EFI` defined. A minimal core shim deliberately omits
strings, spans, exceptions, reflection, arrays, and runtime helpers; a separate
negative compile requires stable C# diagnostics for unnamed raw access,
raw/typed casts, cross-domain assignment, addition, subtraction, comparison,
and wrong-domain out parameters:

```text
TEST_START m01.001
TEST_PASS m01.001 wrappers are stable pods
TEST_PASS m01.001 wrappers reject default and zero
TEST_PASS m01.001 named boundaries preserve raw values
TEST_PASS m01.001 default raw extraction fails closed
TEST_PASS m01.001 equality is domain local
TEST_PASS m01.001 ordering is domain local
TEST_PASS m01.001 wrappers expose only named raw boundaries
TEST_PASS m01.001 source excludes managed runtime dependencies
TEST_PASS m01.001 exact source compiles under nostdlib efi
TEST_PASS m01.001 prohibited expressions fail compilation
TEST_PASS m01.001 offsets accept nonoverflowing boundaries
TEST_PASS m01.001 offsets reject wrap
TEST_PASS m01.001 end-exclusive arithmetic succeeds
TEST_PASS m01.001 end-exclusive arithmetic rejects invalid ranges
TEST_PASS m01.001 alignment validates inputs
TEST_PASS m01.001 align-up succeeds
TEST_PASS m01.001 align-up rejects wrap
TEST_PASS m01.001 failures leave deterministic default outputs
CLROS_M01_001_READY
TEST_PASS m01.001
```

The wrapper API was independently authored. SharpOS
`OS/src/Boot/BootStackSwitch.cs` (local snapshot SHA-256
`637DCFC91E3B9647A0F8FBD4800E6C973ED78C6E5F73E2F7C10770C000234808`) was
used only for the architectural lesson that owned stack bounds and alignment
must be explicit before a switch. Its raw-pointer API was not copied. RoseOS
was used only as a negative-test reminder for later range sizing, reservation,
and reclamation issues; those policies are outside M01-001.

M01-002 canonical reservation coverage is also host-only.
`CLROS_M01_002_READY` is emitted only after the focused factory, layout,
malformed-record, capacity, ordering, overlap, loaded-image, guard/stack,
immutability, allocation, source-firewall, and exact-source compiler checks
pass. The isolated compiler fixture links the reviewed typed-address source and
both new reservation sources with `NoStdLib=true`, framework references
removed, and `NOSTDLIB_EFI` defined, then verifies that the emitted PE has zero
assembly references:

```text
TEST_START m01.002
TEST_PASS m01.002 reservation kinds are stable and append only
TEST_PASS m01.002 reservation records and results are unmanaged pods
TEST_PASS m01.002 range factory creates typed half-open ranges
TEST_PASS m01.002 range factory rejects default alignment and overflow
TEST_PASS m01.002 validator rejects malformed reservation records
TEST_PASS m01.002 validator accepts canonical sorted disjoint table
TEST_PASS m01.002 validator classifies ordering and overlap conflicts
TEST_PASS m01.002 loaded image rejects duplicate metadata coverage
TEST_PASS m01.002 guard and stack exactly account for modeled arena
TEST_PASS m01.002 pointer count and capacity fail before dereference
TEST_PASS m01.002 validator is deterministic nonmutating and allocation free
TEST_PASS m01.002 source excludes hosted runtime dependencies
TEST_PASS m01.002 exact sources compile under nostdlib efi
CLROS_M01_002_READY
TEST_PASS m01.002
```

The enum values are explicitly fixed from `Uninitialized=0` through
`PageProtectionProbe=14`; later values may only append. Metadata that is
actually covered by `LoadedImage` is omitted from the canonical ledger, while a
separate contained `OwnedMemoryMap`, `AcpiSnapshot`, `AcpiScratch`,
`DiagnosticState`, `BootHandoff`, or `FramebufferMetadata` record is rejected
as duplicate coverage. M01-002 cannot infer the location of omitted metadata,
so later live integration must prove containment before relying on image
coverage.

`references/RoseOS-master/Loader/src/MemoryMap.cs` (local snapshot SHA-256
`CE90AE629B538E558B57B0BD315ED8B11DD559ED1A841368D109DE28D70B8B28`) was
used only as a test-only reminder for unchecked size growth, narrowing,
failure-continuation, and ownership hazards. No RoseOS source, API, string,
status name, raw-pointer design, or EFI call was copied. Those hazards were
independently represented as zero/default, overflow, capacity, arbitrary-range,
ordering, and overlap tests. Firmware map acquisition, retry, reclamation, and
`ExitBootServices` remain outside M01-002.

M01-003 loaded-image range capture coverage is host-only.
`CLROS_M01_003_READY` is emitted only after the exact status/POD layouts,
default-value behavior, aligned and unaligned range vectors, high-address and
overflow boundaries, pointer/count/capacity validation order, firmware
transcript classification, canonical reservation integration, malformed-result
revalidation, nonmutation, zero-allocation hot path, protocol-pointer
nonretention, source firewall, and isolated compiler closure all pass. The
40-byte observation binds one acquisition transcript so status, input handle,
returned protocol address, image base, and image size cannot be mixed between
calls. The 144-byte plan never retains the protocol address; its 80-byte range
embeds the exact canonical `BootReservedRange` created with kind
`LoadedImage`:

```text
TEST_START m01.003
TEST_PASS m01.003 statuses and pod layouts are stable
TEST_PASS m01.003 default values fail closed
TEST_PASS m01.003 aligned image produces canonical reservation
TEST_PASS m01.003 outward rounding preserves exact extent
TEST_PASS m01.003 boundary sized image remains checked
TEST_PASS m01.003 pointer count and capacity fail before dereference
TEST_PASS m01.003 invalid page sizes fail before observation use
TEST_PASS m01.003 firmware transcript failures are classified
TEST_PASS m01.003 image range failures are classified
TEST_PASS m01.003 validation precedence is deterministic
TEST_PASS m01.003 canonical reservation is accepted by ledger validator
TEST_PASS m01.003 malformed result values cannot report success
TEST_PASS m01.003 evaluation is nonmutating deterministic and allocation free
TEST_PASS m01.003 result does not retain firmware protocol pointer
TEST_PASS m01.003 sources exclude hosted and live firmware dependencies
TEST_PASS m01.003 exact sources compile under nostdlib efi
CLROS_M01_003_READY
TEST_PASS m01.003
```

The isolated compiler fixture includes only the typed-address source,
`BootReservedRange`, both M01-003 production files, a minimal primitive shim,
and a positive pointer-core probe. It defines `NOSTDLIB_EFI`, removes all
framework references, and verifies that the emitted PE has zero assembly
references.

The implementation was independently authored after reviewing only the
relevant boundary behavior and field semantics:

- `references/SharpOS-main/OS/src/Boot/UefiFile.cs` (local snapshot SHA-256
  `4A2FC2739DDF294142787A5ED730CA268449B4BF31F153666AB50A69C766C7CB`)
  influenced status, handle, and returned-pointer validation order. No
  filesystem behavior or source was copied.
- `references/SharpOS-main/OS/src/Boot/UefiTypes.cs` (local snapshot SHA-256
  `BE363A53F6DBDE47BAA92EFB67F14A693CDEFA9025BA52B49D1C331323194A87`)
  was used to cross-check that the real x64 loaded-image protocol is a
  96-byte firmware structure whose `ImageBase` and `ImageSize` fields are at
  offsets 64 and 72. The compact CLROS observation is deliberately a
  different type and must be populated only after future live-adapter checks.
- `references/RoseOS-master/Loader/src/EFI.cs` (local snapshot SHA-256
  `1C9EFF3980F370FA008CF7BC50A1D79D1969E7C7D1FBDEEBCAC611B9885CCD1D`)
  supplied test-only reminders for raw handle/protocol outputs and unchecked
  image bounds. No API, wrapper, call machinery, status name, or source was
  copied, and this file is not cited as proof of a reclamation defect.

This evidence does not open a firmware protocol, establish that a supplied
pointer is readable, link the leaf into `src/OS`, reserve the running EFI
image, populate a live reservation table, prove memory-map allocation types,
run under QEMU, or change a completion-gate state. M01-021 owns the future
live loaded-image protocol adapter and M01-022 owns guest proof.

Platform configuration-table guard coverage:

```text
TEST_PASS platform.config acpi scan allowed before UEFI gone
TEST_PASS platform.config acpi scan denied in UEFI-gone simulation
TEST_PASS platform.config acpi scan denied after ExitBootServices
TEST_PASS platform.config acpi scan reports invalid metadata
TEST_PASS platform.config acpi2 preferred
TEST_PASS platform.config acpi1 fallback
TEST_PASS platform.config acpi missing
TEST_PASS platform.config metadata contains no managed references
```

Platform UEFI-gone adapter simulation coverage:

```text
TEST_PASS platform.uefi gone simulation denies console memorymap and config adapters
TEST_PASS platform.uefi gone simulation denies all guarded adapters
TEST_PASS platform.uefi gone simulation reports console allowance
TEST_PASS platform.uefi gone simulation reports timer allowance
TEST_PASS platform.uefi gone simulation rejects already-exited firmware
TEST_PASS platform.config tables scans ACPI before UEFI gone
TEST_PASS platform.config tables rejects scan in UEFI-gone simulation
```

Platform timer guard coverage is host-test only and uses an injected monotonic metadata source. It does not program HPET, APIC, TSC, scheduler waits, `Task.Delay`, or hosted/Kestrel timeout behavior:

```text
TEST_PASS platform.timer monotonic allowed before UEFI gone
TEST_PASS platform.timer monotonic denied in UEFI-gone simulation
TEST_PASS platform.timer monotonic denied after ExitBootServices
TEST_PASS platform.timer monotonic reports invalid metadata
TEST_PASS platform.timer metadata contains no managed references
```

Boot free-list planning and node-construction coverage is host-test only. It proves caller-owned metadata validation, initial free-segment output, and deterministic caller-owned node links, not allocator-owned mutation, allocation, paging, kernel reservation, NX/W^X, or `ExitBootServices` behavior:

```text
TEST_PASS memory.boot freelist plans usable ranges
TEST_PASS memory.boot freelist rejects invalid page size
TEST_PASS memory.boot freelist rejects overlapping reservations
TEST_PASS memory.boot freelist reports destination capacity
TEST_PASS memory.boot freelist rejects arithmetic overflow
TEST_PASS memory.boot freelist nodes constructs
TEST_PASS memory.boot freelist nodes rejects failed planner
TEST_PASS memory.boot freelist nodes reports destination capacity
TEST_PASS memory.boot freelist nodes rejects invalid segment shape
TEST_PASS memory.boot freelist nodes rejects arithmetic overflow
```

Boot page allocator-state initialization coverage is host-test only. It proves a validated caller-owned free-list node chain can be reduced to value-type allocator state metadata. It does not allocate pages, free pages, reserve kernel ranges, mutate the node chain, install a live heap, configure paging, enforce NX/W^X, or run in `EfiMain`:

```text
TEST_PASS memory.boot allocator state initializes
TEST_PASS memory.boot allocator state rejects failed free-list plan
TEST_PASS memory.boot allocator state rejects empty free-list
TEST_PASS memory.boot allocator state rejects source capacity
TEST_PASS memory.boot allocator state rejects corrupted node shape
TEST_PASS memory.boot allocator state rejects invalid chain
TEST_PASS memory.boot allocator state default result is not success
TEST_PASS memory.boot allocator state layouts contain no managed references
```

Boot page allocator-readiness composition coverage is host-test only. It proves existing planner/state contracts can be composed over caller-owned scratch buffers and forwarded into the ordered boot readiness marker boundary. It does not reserve kernel-owned ranges, mutate allocator-owned state, allocate pages, configure paging, or run in `EfiMain`:

```text
TEST_PASS memory.boot allocator readiness composes
TEST_PASS memory.boot allocator readiness reports scratch capacity
TEST_PASS memory.boot allocator readiness rejects invalid regions
TEST_PASS memory.boot allocator readiness emits boot marker metadata
```

NativeAOT heap bootstrap planning coverage is host-test only. It proves that validated allocator-state metadata can be reduced to a future managed-heap page budget, not that any managed object allocation helper, GC root scanner, write barrier, or heap reservation exists:

```text
TEST_PASS runtime.nativeaot heap bootstrap plans page budget
TEST_PASS runtime.nativeaot heap bootstrap rejects allocator state
TEST_PASS runtime.nativeaot heap bootstrap rejects allocator byte metadata
TEST_PASS runtime.nativeaot heap bootstrap rejects page budget
TEST_PASS runtime.nativeaot heap bootstrap rejects object alignment
TEST_PASS runtime.nativeaot heap bootstrap layout contains no managed references
```

Boot page-allocator readiness coverage is host-test only. It proves the ordered boot marker boundary over allocator-state initialization output and metadata formatting, not a live allocator:

```text
TEST_PASS boot.sequence page allocator readiness emits state ready marker
TEST_PASS boot.sequence page allocator readiness rejects state initialization failure
TEST_PASS boot.sequence page allocator readiness rejects default state
TEST_PASS boot.sequence page allocator readiness rejects empty state
TEST_PASS boot.sequence page allocator readiness preserves default markers
```

Platform firmware RNG guard coverage is host-test only and uses an injected random-byte source. It does not implement a PRNG, entropy pool, TLS, cryptographic API, or post-`ExitBootServices` randomness:

```text
TEST_PASS platform.random fill allowed before UEFI gone
TEST_PASS platform.random fill denied in UEFI-gone simulation
TEST_PASS platform.random fill denied after ExitBootServices
TEST_PASS platform.random fill rejects empty destination
TEST_PASS platform.random fill reports source failure
TEST_PASS platform.random fill reports short read
TEST_PASS platform.random result contains no managed references
```

Platform physical-memory guard coverage is host-test only and uses an injected byte snapshot source. It does not map physical memory, dereference live ACPI pointers, handle cacheability attributes, perform MMIO, or provide post-`ExitBootServices` physical-memory access:

```text
TEST_PASS platform.physical_memory read allowed before UEFI gone
TEST_PASS platform.physical_memory read denied in UEFI-gone simulation
TEST_PASS platform.physical_memory read denied after ExitBootServices
TEST_PASS platform.physical_memory read rejects invalid range
TEST_PASS platform.physical_memory read rejects empty destination
TEST_PASS platform.physical_memory read reports destination capacity
TEST_PASS platform.physical_memory read reports source failure
TEST_PASS platform.physical_memory read reports source unavailable
TEST_PASS platform.physical_memory read reports short read
TEST_PASS platform.physical_memory result contains no managed references
```

Platform framebuffer guard coverage is host-test only and uses an injected metadata source. It does not call GOP, render text, map framebuffer memory, allocate framebuffer storage, or prove output after `ExitBootServices`:

```text
TEST_PASS platform.framebuffer snapshot allowed before UEFI gone
TEST_PASS platform.framebuffer snapshot denied in UEFI-gone simulation
TEST_PASS platform.framebuffer snapshot denied after ExitBootServices
TEST_PASS platform.framebuffer snapshot rejects zero base or length
TEST_PASS platform.framebuffer snapshot rejects zero dimensions
TEST_PASS platform.framebuffer snapshot rejects invalid stride
TEST_PASS platform.framebuffer snapshot rejects unsupported pixel format
TEST_PASS platform.framebuffer snapshot rejects size mismatch and overflow
TEST_PASS platform.framebuffer snapshot reports source failures
TEST_PASS platform.framebuffer result contains no managed references
```

ACPI RSDP snapshot reader coverage is host-tested with a fake `IPlatformPhysicalMemory` source. The opt-in EFI ACPI physical discovery probe now also reads and validates live RSDP bytes before `ExitBootServices` on the tested QEMU/OVMF host. The default EFI smoke path still records pointer provenance only, and this does not prove post-firmware physical-memory access or APIC/HPET use:

```text
TEST_PASS acpi.rsdp.snapshot reads acpi1 base
TEST_PASS acpi.rsdp.snapshot rereads acpi2 extended length
TEST_PASS acpi.rsdp.snapshot reports scratch capacity
TEST_PASS acpi.rsdp.snapshot reports physical read failure
TEST_PASS acpi.rsdp.snapshot reports extended read failure
TEST_PASS acpi.rsdp.snapshot reports validation failure
TEST_PASS acpi.rsdp.snapshot result contains no managed references
```

ACPI root SDT snapshot reader coverage is host-tested with a fake `IPlatformPhysicalMemory` source. The opt-in EFI ACPI physical discovery probe now also snapshots and validates the live root RSDT/XSDT before `ExitBootServices` on the tested QEMU/OVMF host. This does not select MADT/HPET for use, map ECAM, enumerate PCI, or prove the default smoke path walks ACPI tables:

```text
TEST_PASS acpi.root_sdt.snapshot reads rsdt from acpi1 rsdp
TEST_PASS acpi.root_sdt.snapshot prefers xsdt from acpi2 rsdp
TEST_PASS acpi.root_sdt.snapshot falls back to rsdt when xsdt address is zero
TEST_PASS acpi.root_sdt.snapshot rejects invalid rsdp input
TEST_PASS acpi.root_sdt.snapshot rejects missing root address
TEST_PASS acpi.root_sdt.snapshot reports scratch and destination capacity
TEST_PASS acpi.root_sdt.snapshot reports physical read failures
TEST_PASS acpi.root_sdt.snapshot rejects invalid signature length and checksum
TEST_PASS acpi.root_sdt.snapshot rejects invalid pointer array length
TEST_PASS acpi.root_sdt.snapshot result contains no managed references
```

ACPI MCFG physical discovery coverage is host-tested with a fake `IPlatformPhysicalMemory` source. The opt-in EFI ACPI physical discovery probe now also scans live root SDT child pointers, validates a live `MCFG`, and parses ECAM segment metadata into fixed scratch storage before `ExitBootServices` on the tested QEMU/OVMF host. This does not map ECAM, enumerate PCI, size BARs, bind drivers, or prove post-firmware MMIO access:

```text
TEST_PASS acpi.mcfg.discovery reads xsdt child mcfg from physical memory
TEST_PASS acpi.mcfg.discovery reads rsdt child mcfg from physical memory
TEST_PASS acpi.mcfg.discovery reports physical mcfg missing
TEST_PASS acpi.mcfg.discovery reports child header read failure
TEST_PASS acpi.mcfg.discovery reports child full read failure
TEST_PASS acpi.mcfg.discovery rejects invalid physical mcfg checksum
TEST_PASS acpi.mcfg.discovery reports physical mcfg parse failure
TEST_PASS acpi.mcfg.discovery reports physical segment destination exhaustion
TEST_PASS acpi.mcfg.discovery physical result contains no managed references
```

Post-firmware diagnostics readiness coverage is host-test only and checks marker behavior without calling `ExitBootServices` or configuring an owned serial backend:

```text
TEST_PASS boot.sequence post-firmware diagnostics readiness emits ready marker
TEST_PASS boot.sequence post-firmware diagnostics readiness blocks firmware-console-only path
TEST_PASS boot.sequence post-firmware diagnostics readiness blocks firmware-services-unavailable path
TEST_PASS boot.sequence post-firmware diagnostics readiness leaves default markers unchanged
TEST_PASS diagnostics.owned_serial writes firmware serial before uefi gone
TEST_PASS diagnostics.owned_serial writes owned serial after uefi gone
TEST_PASS diagnostics.owned_serial rejects firmware serial after uefi gone
TEST_PASS diagnostics.owned_serial reports missing owned transport
TEST_PASS diagnostics.owned_serial reports owned serial unavailable
TEST_PASS diagnostics.owned_serial reports short write
TEST_PASS diagnostics.owned_serial formats boot and panic markers
TEST_PASS diagnostics.owned_serial results contain no managed references
```

ExitBootServices preflight coverage is host-test only and checks the combined diagnostics/plan gate without calling firmware:

```text
TEST_PASS boot.sequence exitboot preflight emits ready marker
TEST_PASS boot.sequence exitboot preflight blocks missing post-firmware diagnostics
TEST_PASS boot.sequence exitboot preflight blocks non-success transition plan
TEST_PASS boot.sequence exitboot preflight leaves default markers unchanged
```

ExitBootServices live-wiring coverage is host-test only and documents the future call order while keeping firmware calls disabled by default:

```text
TEST_PASS uefi.exitboot live wiring exposes ordered steps
TEST_PASS uefi.exitboot live wiring blocks missing diagnostics
TEST_PASS uefi.exitboot live wiring blocks non-success transition plan
TEST_PASS uefi.exitboot live wiring stops before firmware call when disabled
TEST_PASS uefi.exitboot live wiring can be explicitly enabled for future firmware call
```

App-package readiness coverage is host-test only and checks marker behavior without loading or executing an app:

```text
TEST_PASS boot.sequence app package readiness emits ready marker
TEST_PASS boot.sequence app package readiness rejects manifest failure
TEST_PASS boot.sequence app package readiness rejects package failure
TEST_PASS boot.sequence app package readiness rejects launch-plan failure
```

App launch descriptor coverage is also host-test only and checks marker behavior without loading or executing an app:

```text
TEST_PASS boot.sequence app launch descriptor ready
TEST_PASS boot.sequence app launch descriptor rejects missing binary
TEST_PASS boot.sequence app launch descriptor rejects launch-plan failure
TEST_PASS boot.sequence app payload ready
TEST_PASS boot.sequence app payload rejects descriptor failure
TEST_PASS boot.sequence app payload rejects invalid header
TEST_PASS boot.sequence app payload leaves default markers unchanged
TEST_PASS boot.sequence app executable mapping ready
TEST_PASS boot.sequence app executable mapping rejects mapping failure
TEST_PASS boot.sequence app executable mapping preserves default markers
TEST_PASS boot.sequence app executable memory ready
TEST_PASS boot.sequence app executable memory rejects plan failure
TEST_PASS boot.sequence app executable memory preserves default markers
TEST_PASS boot.sequence app invocation ready
TEST_PASS boot.sequence app invocation rejects plan failure
TEST_PASS boot.sequence app invocation preserves default markers
TEST_PASS boot.sequence app lifecycle ready
TEST_PASS boot.sequence app lifecycle rejects dependency failure
TEST_PASS boot.sequence app lifecycle preserves default markers
TEST_PASS boot.sequence app entry ready
TEST_PASS boot.sequence app entry rejects dependency failure
TEST_PASS boot.sequence app entry preserves default markers
```

FetchApp HTTP request endpoint readiness coverage is host-test only and checks marker behavior over already-produced endpoint metadata without dumping host/path values, reading manifests from live storage, running FetchApp, resolving DNS, or performing live network I/O:

```text
TEST_PASS boot.sequence fetch http endpoint ready
TEST_PASS boot.sequence fetch http endpoint rejects failed result
TEST_PASS boot.sequence fetch http endpoint rejects default result
TEST_PASS boot.sequence fetch http endpoint preserves default markers
```

FetchApp HTTP completion readiness coverage is host-test only and checks marker behavior over already-produced HTTP completion metadata without running FetchApp or live network I/O:

```text
TEST_PASS boot.sequence fetch http completion ready
TEST_PASS boot.sequence fetch http completion rejects failed result
TEST_PASS boot.sequence fetch http completion preserves default markers
```

FetchApp manifest config preflight readiness coverage is host-test only and checks marker behavior over already-produced manifest/config metadata without reading live storage, running FetchApp, or performing HTTP I/O:

```text
TEST_PASS boot.sequence fetchapp config preflight ready
TEST_PASS boot.sequence fetchapp config preflight rejects failed result
TEST_PASS boot.sequence fetchapp config preflight rejects default result
TEST_PASS boot.sequence fetchapp config preflight preserves default markers
```

Virtio-net RX TCP dispatch readiness coverage is host-test only and checks marker behavior over already-produced caller-owned RX/TCP metadata without polling a NIC, transferring packets, dispatching sockets, or changing default boot markers:

```text
TEST_PASS boot.sequence virtio rx tcp dispatch emits ready marker
TEST_PASS boot.sequence virtio rx tcp dispatch rejects failed result
TEST_PASS boot.sequence virtio rx tcp dispatch rejects default result
TEST_PASS boot.sequence virtio rx tcp dispatch preserves default markers
```

NativeAOT cctor runner preflight readiness coverage is host-test only and checks marker behavior over already-produced preflight metadata without invoking constructors:

```text
TEST_PASS boot.sequence cctor runner preflight ready
TEST_PASS boot.sequence cctor runner preflight rejects failed result
TEST_PASS boot.sequence cctor runner preflight rejects default result
TEST_PASS boot.sequence cctor runner preflight preserves default markers
```

NativeAOT cctor work-item readiness coverage is host-test only and checks marker behavior over already-produced work-item plan metadata without invoking constructors, delegates, reflection, metadata discovery, module initializers, exception handling, or lazy static reference fields:

```text
TEST_PASS boot.sequence cctor work item ready
TEST_PASS boot.sequence cctor work item rejects failed result
TEST_PASS boot.sequence cctor work item rejects default result
TEST_PASS boot.sequence cctor work item preserves default markers
```

Native manifest environment readiness coverage is host-test only and checks marker behavior over already-produced native manifest parse results and caller-provided manifest bytes without reading storage, materializing process environment variables, starting hosted .NET, starting Kestrel, or changing default boot markers:

```text
TEST_PASS boot.sequence manifest env ready
TEST_PASS boot.sequence manifest env rejects parse failure
TEST_PASS boot.sequence manifest env rejects invalid source range
TEST_PASS boot.sequence manifest env rejects default result
TEST_PASS boot.sequence manifest env formats diagnostic failure marker
TEST_PASS boot.sequence manifest env preserves default markers
```

App-exit observer readiness coverage is host-test only and checks marker behavior over already-produced app-log observer metadata without running an app or ingesting live serial logs:

```text
TEST_PASS boot.sequence app exit observer ready
TEST_PASS boot.sequence app exit observer rejects observer failure
TEST_PASS boot.sequence app exit observer rejects default result
TEST_PASS boot.sequence app exit observer preserves default markers
```

App selection coverage is host-test only and checks the aggregate selected-app boundary without loading or executing an app:

```text
TEST_PASS boot.sequence app selection ready
TEST_PASS boot.sequence app selection rejects manifest failure
TEST_PASS boot.sequence app selection rejects package failure
TEST_PASS boot.sequence app selection rejects descriptor failure
TEST_PASS appmodel selection loader composes image inputs
TEST_PASS appmodel selection loader rejects manifest load
TEST_PASS appmodel selection loader rejects package load
TEST_PASS appmodel selection loader rejects launch capability
TEST_PASS appmodel selection loader rejects descriptor failure
```

Firmware smoke proof:

```powershell
.\scripts\build-efi-shell.ps1
.\scripts\run-qemu-smoke.ps1 -Qemu "C:\Program Files\qemu\qemu-system-x86_64.exe" -OvmfCode "C:\Program Files\qemu\share\edk2-x86_64-code.fd" -TimeoutSeconds 15 -RequireMarker OS_BOOT_OK,"TEST_PASS boot.sequence",OS_SMOKE_PASS,UEFI_MEMORY_MAP_OWNED,BOOT_HANDOFF_READY,UEFI_MEMORY_MAP_NORMALIZED,BOOT_MEMORY_MAP_ACCEPTED,BOOT_MEMORY_REGIONS_READY,BOOT_MEMORY_REGION_COUNT,BOOT_PAGE_ALLOCATOR_READY,BOOT_ACPI_RSDP_HANDOFF_READY,UEFI_ACPI_RSDP_READY
.\scripts\build-efi-shell.ps1 -EnableUefiGoneSimulationProbe -EfiOutputPath artifacts/efi/BOOTX64-SIM.EFI -LogPath artifacts/logs/efi-shell-publish-sim.log
.\scripts\run-qemu-smoke.ps1 -EfiArtifact artifacts/efi/BOOTX64-SIM.EFI -Image artifacts/images/os-sim.raw -SerialLog artifacts/logs/qemu-smoke-sim.log -Qemu "C:\Program Files\qemu\qemu-system-x86_64.exe" -OvmfCode "C:\Program Files\qemu\share\edk2-x86_64-code.fd" -TimeoutSeconds 15 -MarkerPreset uefi-gone
.\scripts\build-efi-shell.ps1 -EnableAcpiPhysicalDiscoveryProbe -EfiOutputPath artifacts/efi/BOOTX64-ACPI.EFI -LogPath artifacts/logs/efi-shell-publish-acpi.log
.\scripts\run-qemu-smoke.ps1 -EfiArtifact artifacts/efi/BOOTX64-ACPI.EFI -Image artifacts/images/os-acpi.raw -SerialLog artifacts/logs/qemu-smoke-acpi.log -SummaryLog artifacts/logs/qemu-smoke-acpi.summary.log -Qemu "C:\Program Files\qemu\qemu-system-x86_64.exe" -OvmfCode "C:\Program Files\qemu\share\edk2-x86_64-code.fd" -TimeoutSeconds 15 -MarkerPreset acpi
```

Evidence from 2026-05-21:

- Managed build passed with 0 warnings and 0 errors.
- Host tests emitted `TEST_PASS host.tests`.
- EFI publish emitted `TEST_PASS efi.shell.publish`.
- QEMU smoke emitted `TEST_PASS qemu.smoke`.
- UEFI-gone simulation EFI publish emitted `EFI_PROBE UEFI_GONE_SIMULATION` and `TEST_PASS efi.shell.publish`.
- UEFI-gone simulation QEMU smoke emitted `TEST_PASS qemu.smoke` while requiring the `uefi-gone` marker preset.
- `artifacts/logs/qemu-smoke.log` contains `TEST_START efi.main`, `EFI_CONSOLE_READY`, `UEFI_MEMORY_MAP_READY`, `UEFI_MEMORY_MAP_DESCRIPTOR_COUNT <count>`, `TEST_START boot.sequence`, ordered `BOOT_PHASE` markers, `OS_BOOT_OK`, `TEST_PASS boot.sequence`, and `OS_SMOKE_PASS`.
- `artifacts/logs/qemu-smoke-sim.log` additionally contains `UEFI_GONE_SIM_START`, `UEFI_GONE_SIM_PLATFORM_DENIED`, `POST_FIRMWARE_DIAGNOSTICS_READY`, and `UEFI_GONE_SIM_PASS`.

Evidence from 2026-05-22:

- `.\scripts\build-efi-shell.ps1` emitted `TEST_PASS efi.shell.publish` and staged `artifacts/efi/BOOTX64.EFI`.
- After the no-stdlib console adapter was added, `.\scripts\build-efi-shell.ps1`
  emitted `TEST_PASS efi.shell.publish` and staged
  `artifacts/efi/BOOTX64.EFI`.
- Real QEMU smoke requiring `OS_SMOKE_PASS,EFI_CONSOLE_READY` emitted
  `TEST_PASS qemu.smoke`.
- `.\scripts\run-qemu-smoke.ps1 -Qemu "C:\Program Files\qemu\qemu-system-x86_64.exe" -OvmfCode "C:\Program Files\qemu\share\edk2-x86_64-code.fd" -TimeoutSeconds 15 -RequireMarker OS_SMOKE_PASS,UEFI_MEMORY_MAP_OWNED` emitted `TEST_PASS qemu.smoke`.
- `artifacts/logs/qemu-smoke.log` contains `UEFI_MEMORY_MAP_OWNED`.
- `artifacts/logs/qemu-smoke.summary.log` records `MISSING_MARKERS none` for `OS_SMOKE_PASS;UEFI_MEMORY_MAP_OWNED`.
- Host tests emitted `TEST_PASS uefi.descriptor.reader decodes 40-byte stride`.
- Host tests emitted `TEST_PASS uefi.descriptor.reader decodes 48-byte stride`.
- Host tests emitted `TEST_PASS uefi.descriptor.reader rejects invalid metadata`.
- Host tests emitted `TEST_PASS uefi.descriptor.reader reports source and destination capacity`.
- Host tests emitted `TEST_PASS boot.sequence validates handoff and emits ready marker`.
- Host tests emitted `TEST_PASS boot.sequence rejects invalid handoff before phases`.
- Host tests emitted `TEST_PASS boot.sequence handoff validator rejects source capability and count issues`.
- Host tests emitted `TEST_PASS efi.handoff.validator accepts owned memory-map handoff`.
- Host tests emitted `TEST_PASS efi.handoff.validator rejects invalid boot source and capabilities`.
- Host tests emitted `TEST_PASS efi.handoff.validator rejects memory-map ownership issues`.
- Host tests emitted `TEST_PASS efi.handoff.validator rejects descriptor metadata issues`.
- Real QEMU smoke requiring `OS_SMOKE_PASS,UEFI_MEMORY_MAP_OWNED,BOOT_HANDOFF_READY` emitted `TEST_PASS qemu.smoke`.
- `artifacts/logs/qemu-smoke.log` contains `BOOT_HANDOFF_READY` before `TEST_START boot.sequence`.
- `artifacts/logs/qemu-smoke.summary.log` records `MISSING_MARKERS none` for `OS_SMOKE_PASS;UEFI_MEMORY_MAP_OWNED;BOOT_HANDOFF_READY`.
- Host tests emitted `TEST_PASS efi.memorymap.normalizer summarizes owned descriptors`.
- Host tests emitted `TEST_PASS efi.memorymap.normalizer rejects invalid metadata`.
- Host tests emitted `TEST_PASS efi.memorymap.normalizer rejects descriptor arithmetic issues`.
- Focused host tests emitted `TEST_PASS uefi.memorymap.capture helper copies owned map`.
- Focused host tests emitted `TEST_PASS uefi.memorymap.capture helper rejects missing firmware pointers`.
- Focused host tests emitted `TEST_PASS uefi.memorymap.capture helper rejects missing owned destination`.
- Focused host tests emitted `TEST_PASS uefi.memorymap.capture helper reports allocation overflow`.
- Focused host tests emitted `TEST_PASS uefi.memorymap.capture helper reports copy-plan status`.
- Host tests emitted `TEST_PASS memory.mapinfo writes caller-owned regions`.
- Host tests emitted `TEST_PASS memory.mapinfo reports destination capacity`.
- Host tests emitted `TEST_PASS memory.mapinfo rejects invalid caller-owned descriptors`.
- Host tests emitted `TEST_PASS uefi.exitboot plan reports success`.
- Host tests emitted `TEST_PASS uefi.exitboot plan requires fresh memory map`.
- Host tests emitted `TEST_PASS uefi.exitboot plan retries stale map key`.
- Host tests emitted `TEST_PASS uefi.exitboot plan reports retry exhaustion`.
- Host tests emitted `TEST_PASS uefi.exitboot plan rejects missing owned map`.
- Host tests emitted `TEST_PASS uefi.exitboot plan rejects invalid descriptor metadata`.
- Host tests emitted `TEST_PASS probes expose grouped safe defaults`, including the disabled `EnableExitBootServicesFreshMapProbe` and stable `uefi.exitbootservices` group.
- Host tests emitted `TEST_PASS probes expose grouped safe defaults`, including the disabled `EnableAcpiPhysicalDiscoveryProbe` and stable `acpi.discovery` group.
- Real QEMU smoke requiring `OS_SMOKE_PASS,UEFI_MEMORY_MAP_OWNED,BOOT_HANDOFF_READY,UEFI_MEMORY_MAP_NORMALIZED` emitted `TEST_PASS qemu.smoke`.
- `artifacts/logs/qemu-smoke.log` contains `UEFI_MEMORY_MAP_NORMALIZED` before `TEST_START boot.sequence`.
- `artifacts/logs/qemu-smoke.summary.log` records `MISSING_MARKERS none` for `OS_SMOKE_PASS;UEFI_MEMORY_MAP_OWNED;BOOT_HANDOFF_READY;UEFI_MEMORY_MAP_NORMALIZED`.
- Host tests emitted `TEST_PASS boot.sequence memory map accepted`.
- Host tests emitted `TEST_PASS boot.sequence memory map missing rejected`.
- Host tests emitted `TEST_PASS boot.sequence memory map invalid rejected`.
- Real QEMU smoke requiring `OS_SMOKE_PASS,UEFI_MEMORY_MAP_OWNED,BOOT_HANDOFF_READY,UEFI_MEMORY_MAP_NORMALIZED,BOOT_MEMORY_MAP_ACCEPTED` emitted `TEST_PASS qemu.smoke`.
- `artifacts/logs/qemu-smoke.log` contains `BOOT_MEMORY_MAP_ACCEPTED` before `TEST_START boot.sequence`.
- `artifacts/logs/qemu-smoke.summary.log` records `MISSING_MARKERS none` for `OS_SMOKE_PASS;UEFI_MEMORY_MAP_OWNED;BOOT_HANDOFF_READY;UEFI_MEMORY_MAP_NORMALIZED;BOOT_MEMORY_MAP_ACCEPTED`.
- Real QEMU smoke requiring the current default marker set, including `BOOT_ACPI_RSDP_HANDOFF_READY` and `UEFI_ACPI_RSDP_READY`, emitted `TEST_PASS qemu.smoke`.
- `artifacts/logs/qemu-smoke.log` contains `BOOT_ACPI_RSDP_HANDOFF_READY` and `UEFI_ACPI_RSDP_READY` before `TEST_START boot.sequence`.
- `artifacts/logs/qemu-smoke.summary.log` records `MISSING_MARKERS none` for `OS_SMOKE_PASS;UEFI_MEMORY_MAP_OWNED;BOOT_HANDOFF_READY;UEFI_MEMORY_MAP_NORMALIZED;BOOT_MEMORY_MAP_ACCEPTED;BOOT_ACPI_RSDP_HANDOFF_READY;UEFI_ACPI_RSDP_READY`.
- Host tests emitted `TEST_PASS uefi.exitboot fresh map plan accepts current key`.
- Host tests emitted `TEST_PASS uefi.exitboot fresh map plan rejects stale key`.
- Host tests emitted `TEST_PASS platform.firmware exitboot reports success`.
- Host tests emitted `TEST_PASS platform.firmware exitboot reports stale map key`.
- Host tests emitted `TEST_PASS platform.firmware exitboot rejects unavailable services`.
- Fresh-map probe QEMU smoke emitted `TEST_PASS qemu.smoke` while requiring `OS_SMOKE_PASS`, `UEFI_EBS_FRESH_MAP_PROBE_START`, `UEFI_EBS_FRESH_MAP_READY`, and `UEFI_EBS_CALL_SKIPPED`.
- `artifacts/logs/qemu-smoke-ebs-probe.summary.log` records `MISSING_MARKERS none`.
- Host tests emitted `TEST_PASS uefi.config acpi2 preferred`.
- Host tests emitted `TEST_PASS uefi.config acpi1 fallback`.
- The ACPI physical discovery probe publish emitted `EFI_PROBE ACPI_PHYSICAL_DISCOVERY`, `TEST_PASS efi.shell.publish`, and `EFI_ARTIFACT artifacts/efi/BOOTX64-ACPI.EFI`.
- Real QEMU smoke against `artifacts/efi/BOOTX64-ACPI.EFI` with `-MarkerPreset acpi` emitted `TEST_PASS qemu.smoke`.
- `artifacts/logs/qemu-smoke-acpi.log` contains `ACPI_RSDP_SNAPSHOT_READY`, `ACPI_RSDP_VALIDATED`, `ACPI_ROOT_SDT_SNAPSHOT_READY`, `ACPI_ROOT_SDT_VALIDATED`, and `ACPI_MCFG_DISCOVERY_READY` before `OS_SMOKE_PASS`.
- `artifacts/logs/qemu-smoke-acpi.summary.log` records `MISSING_MARKERS none` for the default boot markers plus the five ACPI discovery markers.
- Host tests emitted `TEST_PASS uefi.config acpi missing`.
- Host tests emitted `TEST_PASS uefi.config acpi zero pointer rejected`.
- Host tests emitted `TEST_PASS uefi.config invalid metadata rejected`.
- Host tests emitted `TEST_PASS boothandoff imports ACPI RSDP metadata`.
- Host tests emitted `TEST_PASS boothandoff keeps ACPI RSDP optional`.
- Host tests emitted `TEST_PASS boothandoff rejects inconsistent ACPI RSDP metadata`.
- Host tests emitted `TEST_PASS efi.handoff.validator validates ACPI RSDP metadata`.
- Host tests emitted `TEST_PASS boot.sequence handoff validator validates ACPI RSDP metadata`.
- Host tests emitted `TEST_PASS acpi.rsdp validates acpi1 checksum`.
- Host tests emitted `TEST_PASS acpi.rsdp validates acpi2 extended checksum`.
- Host tests emitted `TEST_PASS acpi.rsdp rejects signature metadata and revision`.
- Host tests emitted `TEST_PASS acpi.rsdp rejects checksum failures`.
- Host tests emitted `TEST_PASS acpi.rsdp rejects extended length failures`.
- Host tests emitted `TEST_PASS acpi.rsdp.snapshot reads acpi1 base`.
- Host tests emitted `TEST_PASS acpi.rsdp.snapshot rereads acpi2 extended length`.
- Host tests emitted `TEST_PASS acpi.rsdp.snapshot reports scratch capacity`.
- Host tests emitted `TEST_PASS acpi.rsdp.snapshot reports physical read failure`.
- Host tests emitted `TEST_PASS acpi.rsdp.snapshot reports extended read failure`.
- Host tests emitted `TEST_PASS acpi.rsdp.snapshot reports validation failure`.
- Host tests emitted `TEST_PASS acpi.rsdp.snapshot result contains no managed references`.
- Host tests emitted `TEST_PASS acpi.root_sdt.snapshot reads rsdt from acpi1 rsdp`.
- Host tests emitted `TEST_PASS acpi.root_sdt.snapshot prefers xsdt from acpi2 rsdp`.
- Host tests emitted `TEST_PASS acpi.root_sdt.snapshot falls back to rsdt when xsdt address is zero`.
- Host tests emitted `TEST_PASS acpi.root_sdt.snapshot rejects invalid rsdp input`.
- Host tests emitted `TEST_PASS acpi.root_sdt.snapshot rejects missing root address`.
- Host tests emitted `TEST_PASS acpi.root_sdt.snapshot reports scratch and destination capacity`.
- Host tests emitted `TEST_PASS acpi.root_sdt.snapshot reports physical read failures`.
- Host tests emitted `TEST_PASS acpi.root_sdt.snapshot rejects invalid signature length and checksum`.
- Host tests emitted `TEST_PASS acpi.root_sdt.snapshot rejects invalid pointer array length`.
- Host tests emitted `TEST_PASS acpi.root_sdt.snapshot result contains no managed references`.
- Host tests emitted `TEST_PASS acpi.mcfg.discovery selects and parses mcfg`.
- Host tests emitted `TEST_PASS acpi.mcfg.discovery preserves selected physical address`.
- Host tests emitted `TEST_PASS acpi.mcfg.discovery reports directory miss`.
- Host tests emitted `TEST_PASS acpi.mcfg.discovery reports mcfg parse failure`.
- Host tests emitted `TEST_PASS acpi.mcfg.discovery reports segment destination exhaustion`.
- Host tests emitted `TEST_PASS acpi.mcfg.discovery supports direct pointer list input`.
- Host tests emitted `TEST_PASS boot.memory regions materializes descriptors`.
- Host tests emitted `TEST_PASS boot.memory regions reports destination capacity`.
- Host tests emitted `TEST_PASS boot.memory regions rejects invalid descriptors`.
- Host tests emitted `TEST_PASS boot.memory regions rejects invalid metadata`.
- Host tests emitted `TEST_PASS memory.boot regions selects conventional ranges`.
- Host tests emitted `TEST_PASS memory.boot regions records reserved ranges`.
- Host tests emitted `TEST_PASS memory.boot regions rejects overlapping ranges`.
- Host tests emitted `TEST_PASS memory.boot regions reports destination capacity`.
- Host tests emitted `TEST_PASS memory.boot regions rejects invalid metadata`.
- Host tests emitted `TEST_PASS memory.boot regions covers overflow failures`.
- Host tests emitted `TEST_PASS memory.boot allocator readiness composes`.
- Host tests emitted `TEST_PASS memory.boot allocator readiness reports scratch capacity`.
- Host tests emitted `TEST_PASS memory.boot allocator readiness rejects invalid regions`.
- Host tests emitted `TEST_PASS memory.boot allocator readiness emits boot marker metadata`.
- Host tests emitted `TEST_PASS memory.boot allocator mutates allocation`.
- Host tests emitted `TEST_PASS memory.boot allocator splits aligned range`.
- Host tests emitted `TEST_PASS memory.boot allocator frees and coalesces`.
- Host tests emitted `TEST_PASS memory.boot allocator rejects invalid mutation`.
- Host tests emitted `TEST_PASS boot.sequence page allocator readiness emits state ready marker`.
- Host tests emitted `TEST_PASS boot.sequence page allocator readiness rejects state initialization failure`.
- Host tests emitted `TEST_PASS boot.sequence page allocator readiness rejects default state`.
- Host tests emitted `TEST_PASS boot.sequence page allocator readiness rejects empty state`.
- Host tests emitted `TEST_PASS boot.sequence page allocator readiness preserves default markers`.
- Real QEMU smoke requiring the current hardened boot marker chain plus `BOOT_PAGE_ALLOCATOR_READY` emitted `TEST_PASS qemu.smoke`.
- `artifacts/logs/qemu-smoke.log` contains `BOOT_MEMORY_REGIONS_READY`, `BOOT_MEMORY_REGION_COUNT 102`, and `BOOT_PAGE_ALLOCATOR_READY status=Ready regions=102 conventionalPages=52245 conventionalBytes=213995520 freeNodes=9 freePages=52245 freeBytes=213995520 pageSize=4096 statusCode=0` before `BOOT_ACPI_RSDP_HANDOFF_READY`.
- `artifacts/logs/qemu-smoke.summary.log` records `MISSING_MARKERS none` and `FORBIDDEN_MARKER_HIT none` for `OS_BOOT_OK;TEST_PASS boot.sequence;OS_SMOKE_PASS;UEFI_MEMORY_MAP_OWNED;BOOT_HANDOFF_READY;UEFI_MEMORY_MAP_NORMALIZED;BOOT_MEMORY_MAP_ACCEPTED;BOOT_MEMORY_REGIONS_READY;BOOT_MEMORY_REGION_COUNT;BOOT_PAGE_ALLOCATOR_READY;BOOT_ACPI_RSDP_HANDOFF_READY;UEFI_ACPI_RSDP_READY`.

## Known Limitations

The QEMU proof only proves OVMF can load and run the EFI application, the tested firmware path can return a memory map, and firmware-visible output reaches the serial log. It is not completion-gate proof for `ExitBootServices`, owned drivers, memory management, storage, networking, app loading, hosted .NET 10, Minimal API, Kestrel, cloud readiness, or production readiness; use `docs/completion-gates.md` for the full claim boundary.

The memory-map snapshot is now copied from the firmware-allocated buffer into a fixed static OS-owned buffer before the smoke marker is emitted. The firmware source pointer is still retained only as provenance in `BootHandoffRecord`.

`UefiMemoryMapCapture` now owns the EFI boundary's `GetMemoryMap`, `AllocatePool`, copy-plan validation, and owned-copy mechanics. `EfiEntry` still owns firmware-visible markers and handoff record population. The live EFI path materializes compact `BootMemoryRegionRecord` entries into static storage, but the map key still requires a future retry contract before real `ExitBootServices`.

`UefiMemoryDescriptorReader` is host-tested against synthetic owned byte buffers. The live EFI artifact uses the narrower no-stdlib-safe `EfiMemoryMapNormalizer` and `BootMemoryRegionMaterializer` pointer paths instead of the span-based host reader.

The captured map key is valid only for the memory-map instance returned by `GetMemoryMap`. Later boot-services calls can invalidate it, so a future real `ExitBootServices` path must keep a retry loop and cannot assume the logged key remains valid after additional firmware calls.

`UefiExitBootServicesPlan` is plan-only. It models when a fresh memory map is required and when stale map-key failure should retry, but it does not call `ExitBootServices`, does not mutate `FirmwareServicesGuard`, and is not firmware-shutdown evidence.

`PlatformFirmwareExitBootServicesResult` is also not firmware-shutdown evidence. It models the future firmware-call result after the guard and function-pointer checks, but no live EFI artifact calls `ExitBootServices` through this contract yet.

`IPlatformSerial`, `GuardedPlatformSerial`, `Com1PlatformSerialTransport`, and `OwnedSerialDiagnostics` are host-tested boundary/formatter/adapter contracts only. They distinguish firmware-backed and owned transports under UEFI-gone simulation and write to injected or fakeable transports, but they do not perform real COM1 port I/O, call `ExitBootServices`, or prove live serial output after firmware shutdown.

`EfiConfigurationTableScanner` is host-tested and QEMU-marker-tested through `EfiMain`. The default path carries the selected pointer through the typed handoff as provenance only. The explicit ACPI physical discovery probe dereferences and validates table bytes before `ExitBootServices`, but that probe is not part of the default smoke path.

`AcpiRsdpValidator` is host-tested against synthetic byte buffers and is mirrored by no-stdlib validation in the explicit ACPI physical discovery probe. The probe proves pre-`ExitBootServices` RSDP bytes only; it does not establish post-firmware physical-memory rules.

`AcpiMcfgDiscovery` has host-tested table-snapshot and physical-memory child-table surfaces, and the explicit ACPI probe now proves live QEMU/OVMF MCFG byte discovery before `ExitBootServices`. It still does not map ECAM, enumerate PCI devices, size BARs, bind drivers, or prove MMIO access.

`IPlatformFramebuffer` is host-tested through a guarded metadata double only. It records base address, byte length, width, height, stride, pixel format, firmware mode, nested source status, required byte length, and firmware status; it does not query GOP, map or write framebuffer memory, render text, or prove output after firmware services are gone.

`EnableExitBootServicesFreshMapProbe` is intentionally disabled by default. The explicit probe build reacquires and validates a fresh memory map, then emits `UEFI_EBS_CALL_SKIPPED`; it is not a live `ExitBootServices` attempt and does not prove post-firmware execution.

`BootHandoffInfo`, `MemoryMapInfo`, `BootMemoryRegionPlanner`, `BootPageAllocatorPlanner`, `BootPageFreeListBuilder`, `BootPageAllocatorStateInitializer`, `BootPageAllocatorMutation`, `BootPageAllocatorReadinessComposer`, `NativeAotHeapBootstrapPlanner`, `NativeAotClassConstructorRunnerPreflight`, `NativeAotClassConstructorWorkItemPlanner`, `BootSequence.RunBootPageAllocatorReadiness`, `BootSequence.RunNativeAotHeapBootstrapReadiness`, `BootSequence.RunNativeAotClassConstructorRunnerPreflightReadiness`, and `BootSequence.RunNativeAotClassConstructorWorkItemReadiness` are host-tested contracts. The live no-stdlib EFI artifact now populates and validates `BootHandoffRecord` with owned memory-map metadata and ACPI RSDP pointer provenance, normalizes a compact live summary, accepts that summary as a boot precondition, materializes compact `BootMemoryRegionRecord` entries, validates conventional-memory records into a static free-list metadata chain, and emits `BOOT_PAGE_ALLOCATOR_READY` with live conventional/free node/page/byte counts. It still does not run the host-friendly allocator-facing planners, the allocator-readiness composer, or the cctor work-item planner in `EfiMain`, reserve kernel-owned ranges, expose page allocation/free APIs, reserve managed heap pages, configure paging, enforce NX/W^X, allocate pages, emit `NATIVEAOT_HEAP_BOOTSTRAP_READY`, `NATIVEAOT_CCTOR_RUNNER_PREFLIGHT_READY`, or `NATIVEAOT_CCTOR_WORK_ITEMS_READY` in QEMU, install a managed heap, or invoke class constructors.

`BOOT_HANDOFF_READY`, `BOOT_MEMORY_MAP_ACCEPTED`, `BOOT_MEMORY_REGIONS_READY`, `BOOT_MEMORY_REGION_COUNT`, `BOOT_PAGE_ALLOCATOR_READY`, and `BOOT_ACPI_RSDP_HANDOFF_READY` are QEMU-proven through source-linked EFI-safe numeric validators/materializers. The five `ACPI_*` table-validation markers are QEMU-proven only through the explicit ACPI physical discovery probe artifact. The managed `BootSequence.Run(BootHandoffRecord, EfiMemoryMapSummary, ...)` overload remains host-tested, while the live EFI artifact still emits the allocation-free `BootSequencePlan` directly after the accepted marker, region materialization, allocator-readiness metadata marker, and ACPI scan.

The `APP_PACKAGE_READY`, `APP_LAUNCH_READY`, `APP_PAYLOAD_READY`, `APP_EXEC_MAPPING_READY`, `APP_EXEC_MEMORY_READY`, `APP_INVOCATION_READY`, `APP_ARGS_HANDOFF_READY`, `APP_LIFECYCLE_READY`, `APP_ENTRY_READY`, `APP_SELECTION_READY`, `APP_SELECTION_PREFLIGHT_READY`, `APP_PACKAGE_FILE_STAGING_PREFLIGHT_READY`, `NATIVE_MANIFEST_ENV_READY`, `FETCHAPP_CONFIG_PREFLIGHT_READY`, `FETCHAPP_HTTP_ENDPOINT_READY`, `FETCH_APP_HTTP_COMPLETION_READY`, `APP_EXIT_OBSERVER_READY`, `NATIVEAOT_CCTOR_RUNNER_PREFLIGHT_READY`, and `NATIVEAOT_CCTOR_WORK_ITEMS_READY` markers are not QEMU-proven and are not emitted by the default firmware-visible boot path. They are host-tested managed readiness helpers for future app-selection, package-loading, executable-mapping, invocation, argument handoff, lifecycle, final pre-entry, native manifest environment handoff, FetchApp config preflight, FetchApp request endpoint metadata, FetchApp completion, app-exit observation, runtime cctor-runner preflight, and runtime cctor work-item boundaries. They do not prove live package I/O, live virtio-blk reads, boot-time package loading, manifest/config reads from live storage, process environment materialization, executable page allocation, permission changes, payload copying, stack allocation, stack switching, thread creation, final ABI dispatch, stdout/stderr routing, exit capture, restart policy, relocation, hosted .NET, Kestrel, app execution, live networking, live HTTP GET, live app-exit observation, constructor invocation, metadata discovery, or module initialization.

## Next Steps

1. Keep extending the QEMU UEFI-gone simulation as new firmware-guarded platform interfaces are added, keeping real `ExitBootServices` blocked until post-firmware diagnostics are implemented.
2. Add a real `ExitBootServices` probe only after the retry plan, diagnostics, and owned output paths have evidence.
3. Begin diagnostics-first IDT/fault panic work after post-UEFI diagnostics requirements are represented by tests and evidence.
   - The host-tested `CpuFaultFrame`, `PanicFormatter.FormatFault`,
     `CpuFaultEntryFrameLayout`, and `CpuFaultPanicReadiness` contracts now
     define the expected register dump field order and caller-owned
     frame-to-panic-output readiness boundary before real handlers are added.
