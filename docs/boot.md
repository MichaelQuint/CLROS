# Boot

## Current Status

Boot has a firmware-visible QEMU/OVMF smoke proof. `src/OS` publishes a no-stdlib NativeAOT EFI application, `tools/OS.ImageBuilder` wraps it in a raw GPT/FAT32 EFI System Partition image, and `scripts/run-qemu-smoke.ps1` boots that image in QEMU/OVMF and captures serial markers.

This is still an EFI application smoke, not a functional kernel. The current evidence proves that firmware reaches `EfiMain`, that `EfiMain` can capture the UEFI memory map on the tested QEMU host, copy the raw descriptor bytes into an OS-owned static buffer, validate the live owned handoff record, normalize and accept a live owned-map summary, materialize compact boot memory-region records into OS-owned static storage, carry a discovered ACPI RSDP pointer into the typed handoff record, optionally validate live ACPI RSDP/root SDT/MCFG bytes before `ExitBootServices` in an explicit probe build, and emit the ordered boot phase marker plan.

## Implemented Behavior

- `BootSequence.Run` emits host-tested boot phase markers from `BootSequencePlan`.
- `BootSequence.Run(BootHandoffRecord, ...)` validates the no-stdlib-safe handoff record before later managed boot phases and emits `BOOT_HANDOFF_READY` when the handoff passes.
- `BootSequence.Run(BootHandoffRecord, EfiMemoryMapSummary, ...)` adds a host-tested normalized memory-map precondition and emits `BOOT_MEMORY_MAP_ACCEPTED` only after the summary passes deterministic validation.
- `BootSequencePlan` is an allocation-free byte-indexed marker plan that can be source-linked into the no-stdlib EFI project.
- `BootSequencePlan` also carries a separate exact 11-line M01 stage-one
  success plan. It does not change the existing five non-smoke lines, eight
  default lines, or any default boot byte.
- `BootSequencePlan` carries a second independent exact nine-line M01
  stage-two success plan. `BootSequence.RunM01LiveAllocatorIntegration`
  presents it only for a completed host result; it is not part of the default
  boot path.
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
- `BootReservationSourceRange` is the exact 40-byte readonly unmanaged source/fragment POD for reservation subtraction. It records typed nonzero half-open physical bounds, checked page and byte totals, and an exact zero-based `SourceRangeIndex`; a split fragment preserves the source ordinal instead of inventing new provenance.
- `BootReservationSubtractor.Subtract` is the self-contained host-tested, no-stdlib-compatible unsafe pointer/count/capacity transform over those source ranges and canonical `BootReservedRange` records. It accepts only 4096-byte pages, from one through 4096 source ranges, zero through 256 reservations, and at most 4096 emitted fragments. The caller must keep every declared storage span valid, exclusively owned, and stable for the complete validation, dry-run, and emission call; the arithmetic preflight cannot prove an unsafe address is mapped or prevent concurrent mutation. Before reading either input table the subtractor validates counts against capacities, 8-byte pointer alignment, compiler-sized full declared-capacity spans for wrap, and source/reservation non-aliasing. Sources require exact ordinal provenance and sorted disjoint geometry; adjacency is accepted. Reservations may be absent as a null/zero identity input, but every supplied reservation must be structurally valid, sorted/disjoint with adjacency accepted, and wholly contained by exactly one source rather than crossing a gap or source boundary.
- Reservation subtraction is a three-phase atomic transform: validate every input, perform a write-free dry run that derives exact required fragments and source/removed/remaining page and byte totals, then preflight the destination's compiler-sized full declared-capacity span and its pairwise disjointness before emitting. Identity produces the original ordered ranges with provenance intact. Complete reservation coverage succeeds with zero required/written fragments and permits a null zero-capacity destination. Head, tail, middle, and repeated splits remain ordered and disjoint. Capacity, overflow, malformed geometry, containment, provenance, or alias failures leave all destination bytes unchanged, and successful accounting must satisfy source equals removed plus remaining in both pages and bytes.
- `BootReservationSourceRangeStatus`, `BootReservationSubtractionStatus`, and `BootReservationSubtractionResult` keep every kernel-facing outcome value-only and fail closed. The default result is not successful; successful evidence binds the exact input counts and declared capacities, required and written fragment counts, fixed page size, nested range-validation outcomes, deterministic invalid/conflicting indexes, and exact source/removed/remaining totals. No result retains a caller pointer.
- `EfiLoadedImageProtocolObservation`, `EfiLoadedImageRange`, and `EfiLoadedImageRangeCapturePlan` define a host-tested, no-stdlib-compatible loaded-image capture contract. One 40-byte observation atomically records the firmware status, input image handle, transient protocol address, image base, and image size. The exact-one pointer/count/capacity evaluator rejects malformed metadata and an invalid 4 KiB page-size contract before its single observation read, then validates handle/status/pointer/base/size in deterministic order, checks exact-end and outward-rounding arithmetic, and constructs the canonical `LoadedImage` reservation. Its 144-byte value result contains an 80-byte exact range and canonical reservation but does not retain the protocol pointer. This compact observation is not ABI-compatible with the native x64 `EFI_LOADED_IMAGE_PROTOCOL` and must never be cast over firmware memory.
- `OwnedBootStackLayout` and `OwnedBootStackLayoutResult` define a host-tested, no-stdlib-compatible ownership and lifetime contract for one future boot stack. The factory checks an exact-two pointer/count/capacity table before dereference, validates typed backing and physical-top scalars, delegates canonical record/topology checks to `BootReservationTableValidator`, and accepts only a guard plus usable-stack union that exactly covers the backing arena. Guard and usable extents may each occupy one or more pages; the physical exclusive top must be 16-byte aligned and must equal the usable reservation end. The immutable `Reserved -> Ready -> Active` transition planner rejects same-state requests, skips, reversal, default targets, and malformed source layouts. These states are caller attestations, not page-table or processor observations; because layouts are copyable values, a serialized live coordinator must reject replay from a stale snapshot.
- `BootSequenceHandoffValidator` validates the boot source, required text-output and memory-map capabilities, firmware-services mode, owned memory-map copy flag, source-buffer provenance, descriptor size, descriptor count, map-size arithmetic, descriptor-count limits, and ACPI RSDP metadata consistency without exceptions.
- `EfiBootHandoffValidator` is the source-linked no-stdlib-safe numeric validator used by the live EFI artifact before it emits `BOOT_HANDOFF_READY`.
- `EfiMemoryMapNormalizer` is the source-linked no-stdlib-safe live normalizer that walks owned raw descriptor bytes by pointer, validates page/address arithmetic, and records a compact summary without allocating.
- `BootMemoryMapAcceptance` is the source-linked no-stdlib-safe acceptance contract for the normalized summary. It rejects missing, zero-descriptor, zero-page, zero-byte, invalid-range, and no-conventional-memory summaries before managed phase markers.
- `BootMemoryRegionMaterializer` is the source-linked no-stdlib-safe live materializer that walks the owned raw descriptor bytes, validates descriptor metadata, page counts, address ranges, totals, destination capacity, and 40-byte or 48-byte descriptor strides, then writes compact `BootMemoryRegionRecord` entries into caller-owned storage without allocation.
- `BootMemoryRegionPlanner` is the host-tested allocator-facing planning boundary after compact region materialization. It consumes caller-provided `BootMemoryRegionRecord` values, validates page counts, byte lengths, physical ends, sorted/non-overlapping order, destination capacity, and arithmetic overflow, then writes conventional-memory `BootMemoryUsableRange` entries and reserved/unusable `BootMemoryReservedRange` metadata into caller-owned spans without allocating.
- `BootPageAllocatorPlanner` is the host-tested free-list planning boundary after usable/reserved range selection. It consumes caller-owned `BootMemoryUsableRange` and `BootMemoryReservedRange` spans, validates page size, range alignment, range shape, sorted/non-overlapping usable ranges, non-overlapping reserved ranges, destination capacity, and arithmetic overflow, then writes `BootPageFreeSegment` metadata into caller-owned storage without allocation.
- `BootPageFreeListBuilder` is the host-tested free-list node construction boundary after successful free-segment planning. It consumes `BootPageAllocatorPlanResult` plus caller-owned `BootPageFreeSegment` storage, rejects failed plans, insufficient source/destination capacity, invalid page size, unaligned or malformed segment metadata, unsorted or overlapping segments, arithmetic overflow, and total-count mismatches, then writes `BootPageFreeListNode` metadata with deterministic next-node indexes into caller-owned storage only after validation succeeds.
- `BootPageAllocatorCoreContracts.cs` is the extracted no-stdlib allocator metadata closure shared by the existing host adapters and the new pointer materializer. It contains the exact unmanaged 48-byte `BootPageFreeListNode` and 56-byte `BootPageAllocatorState` PODs without spans, strings, exceptions, reflection, managed-reference fields, planner-result dependencies, or framework-library dependencies or references. The typed node is not wire-compatible with `EfiEntry`'s legacy six-`ulong` slot table: its two 32-bit provenance indexes and signed 32-bit `-1` terminal link require an explicit checked transcode in later live wiring, never a reinterpret cast.
- `BootPageAllocatorStateInitializer` is the host-tested metadata installation boundary after free-list node construction. It consumes a successful `BootPageFreeListPlanResult` plus caller-owned `BootPageFreeListNode` storage, rejects failed or empty free lists, insufficient node capacity, invalid head/next indexes, malformed node ranges, cycles, node-count mismatches, and total-count mismatches, then returns value-type allocator state metadata without allocating pages or mutating the free list.
- `BootPageAllocatorStateMaterializer` is the host-tested, no-stdlib-compatible pointer/count/capacity boundary for those extracted PODs. It accepts a source node pointer, explicit node count and capacity, explicit `sourceRangeCount`, head index, expected page/byte totals, and a caller-owned destination-state pointer and capacity. It requires exactly 4096-byte pages, at most 4096 nodes and source ranges, 8-byte-aligned non-overlapping source/destination spans, capacity for at least one destination-state slot, visitation-ordered source segment indexes, in-range monotonic source-range indexes, sorted non-overlapping physical ranges, exact node shapes and totals, a bounded acyclic chain that visits every declared node exactly once, and checked table/address/count arithmetic. It performs its sole destination write only after the complete input and constructed state validate, so every failure leaves source and destination storage unchanged and returns status-coded value-only evidence.
- `BootPageAllocatorMutator.AllocateOnePage` is the no-stdlib-compatible unsafe mutation core after allocator-state materialization. It accepts `BootPageAllocatorState` by value, caller-owned `BootPageFreeListNode` pointer/count/capacity storage, a caller-owned read-only canonical `BootReservedRange` pointer/count/capacity table, and explicit page-count/alignment inputs. It requires exactly one 4096-byte page, bounds logical mutable node slots at 4096 and reservations at 256, validates compiler-sized declared spans and table disjointness, and rejects malformed state, chains, node geometry, totals, indexes, cycles, arithmetic, reservations, or capacity before mutation.
- The one-page core treats every active-free-range/canonical-reservation overlap as a contract failure; reservations must already have been removed by the M01-007 subtraction boundary rather than skipped during allocation. A successful first aligned fit consumes, shortens, or splits one free node, mutates only the declared logical node prefix, leaves the reservation table and capacity tail unchanged, and returns a `BootPageAllocatorOnePageResult` with typed `PhysicalAddress` start/end, exact one-page/4096-byte metadata, before/after allocator states and free/allocated accounting, capacities, indexes, and nested range/address evidence. A middle split may consume only an exact default/all-zero inactive logical slot; stale nondefault unlinked metadata is never overwritten and instead produces atomic metadata-exhaustion failure. Every failure leaves caller storage byte-identical.
- A successful real `AllocateOnePage` result is also the only accepted evidence for `FreeOnePage`: it has `CanRestore=true` and embeds an initialized `BootPageAllocatorOnePageRestoreReceipt`. The receipt retains the exact pre-allocation source node, source-free-segment and source-usable-range provenance, and predecessor evidence; the enclosing allocation result retains the before/after allocator states, node and reservation counts/capacities, typed physical range, indexes, and other status evidence. `FreeOnePage` accepts no independent raw physical address and is deliberately an exact inverse rather than a general free-list insertion operation. It requires the caller's current state and node/reservation counts/capacities to match the allocation result's exact post-allocation snapshot, revalidates the current chain and receipt, and restores the original logical node topology byte-for-byte. Exact-node consumption reports `None`; a tail allocation reports `Previous`; a head allocation reports `Next`; and a middle split reports `PreviousAndNext`.
- This receipt-bound free contract rejects default or unsuccessful evidence, inconsistent forged evidence, foreign receipts whose recorded evidence does not match, altered source/provenance/predecessor evidence, replay and double free, stale or out-of-order state, mismatched count/capacity, malformed current chains, and arithmetic or pointer-span failures before mutation. The caller must keep the declared node storage mapped and stable, exclusively serialize every allocation/free transition, and publish each returned state exactly once. Consequently, successful freeing is limited to immediate restoration or caller-managed LIFO order; arbitrary-order freeing requires a later allocation ledger or generation-bearing authority. The copyable receipt is not an unforgeable capability: byte-identical receipts from indistinguishable allocator copies cannot be rejected, and the current contract cannot solve ABA after an address is freed and reallocated.
- `M01LiveAllocatorIntegration` composes the final M01-017 static state,
  explicit LoaderData arena allocation evidence, refreshed owned-map/canonical
  ledger evidence, reservation validation, arena-source subtraction,
  allocator-state materialization, one contained 4 KiB allocation, exact
  receipt-bound restoration, and deterministic double-free rejection. It
  writes caller-owned completion state only after the full host-modeled cycle
  succeeds. It does not call firmware or dereference the reported physical
  page.
- Unsafe pointer validation cannot prove that caller-supplied addresses are mapped or stable. `BootPageAllocatorMutator.PerformsLiveAllocation=false` remains explicit: allocation and exact-inverse restoration mutate caller-owned metadata only and neither operation owns, frees, maps, zeros, protects, or accesses a physical page. The pre-existing Span-shaped general `Allocate`/`Free` compatibility API and result compile only under `#if !NOSTDLIB_EFI`; they remain historical host-test compatibility helpers, are not interoperable authoritative transitions for the pointer core, and do not establish safe or live free semantics.
- `BootPageAllocatorStressTests` is the host-harness-only deterministic oracle over that existing pointer API. It drives a bounded synthetic arena through fixed-seed one-page allocation and strict-LIFO restoration, independently derives provenance-aware ownership and free extents, compares allocator metadata after every operation, preserves full node/reservation byte snapshots and capacity canaries, and verifies separate host-owned content tags for every live synthetic allocation. The content store is never passed to the allocator, so its corruption self-test proves the oracle can detect a bad host canary rather than proving physical-page access. This test source is not production or `NOSTDLIB_EFI` code and is excluded from the canonical EFI project.
- `PageProtection`, `PageCachePolicy`, and `X64PageTableEntry` define the host-tested, no-stdlib-compatible x64 entry-value boundary. Leaf mappings accept only R, RW, or RX; reject W-only, X-only, unknown, and every W+X combination; encode NX whenever execute is absent; bind WB/UC/WC to an explicitly validated IA32_PAT value and selected index; and validate PTE, 2 MiB PDE, and capability-gated 1 GiB PDPTE address widths, alignments, reserved bits, and PAT-bit placement. Nonleaf PML4E/PDPTE/PDE table pointers are a distinct kind with canonical supervisor `P|RW, NX=0` bits and validated WB at raw PAT index zero, independent of the selected leaf-WB index. An explicit protection-only rewrite either preserves or clears caller-supplied Accessed/Dirty observations without writing live memory. The bounded two-pass batch encoder validates POD sizes, complete caller-owned source/destination spans, and every request before writing any destination byte.
- `X64BootIdentityMappingPlan` defines the next host-tested, no-stdlib-compatible value-planning boundary over those entry policies. It accepts explicit caller-owned boot mapping requirements, requires the executing image, owned boot stack, boot handoff, owned serial stub, ACPI scratch, and framebuffer-metadata purposes, requires a boot-stack guard as a non-emitted exclusion, and permits an `AdditionalRequired` purpose for future caller-owned page-table-arena and boot resources. It emits only typed physical-equals-virtual 4 KiB mapping ranges, sorted deterministically and unioned only when protection/cache policies are identical, while separately calculating the exact fresh four-level table-page requirement. This is a plan over supplied metadata; it allocates, populates, installs, and activates nothing.
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

- Live page allocator/free-list ownership over planned boot memory ranges.
  `BootMemoryRegionPlanner` classifies compact records into usable conventional
  ranges and reserved/unusable metadata, `BootPageAllocatorPlanner` produces
  host-tested initial free-segment metadata, `BootPageFreeListBuilder`
  constructs caller-owned node metadata, and
  `BootPageAllocatorStateMaterializer` validates supplied POD storage through
  a pointer/count/capacity contract. `BootPageAllocatorMutator.AllocateOnePage`
  performs one reservation-aware metadata mutation, and its strict serialized
  `FreeOnePage` exact inverse accepts only the matching successful receipt and
  exact post-allocation state. `M01LiveAllocatorIntegration` now composes one
  host-modeled arena-confined cycle, while
  `BootPageAllocatorReadinessComposer` and
  `BootSequence.RunBootPageAllocatorReadiness` retain the older host readiness
  path. The extracted PODs, materializer, mutator, and M01-018 coordinator
  compile in isolated `NOSTDLIB_EFI`/no-framework-reference closures, but they
  are not source-linked into `src/OS/OS.csproj`, invoked by `EfiEntry`, or
  exercised by QEMU. None establishes live allocator ownership, reserves a
  page against firmware or another allocator, zeroes/maps/accesses the typed
  result, allocates or frees a physical guest page, supports arbitrary-order
  frees, prevents receipt ABA, configures paging, or enforces NX/W^X.
- Live use of the typed address domains, loaded-image capture plan, or canonical reservation ledger. The exact typed-address, reservation-record, pointer-validator, loaded-image range, and capture-plan sources have retained isolated `NOSTDLIB_EFI` compiler tests, but they are not source-linked into `src/OS/OS.csproj`, invoked by `EfiMain`, or applied retroactively to existing raw-`ulong` memory, MMIO, PCI, and DMA interfaces. Named boundary methods make raw-value intent auditable; they are not a capability boundary. The loaded-image planner validates one caller-owned compact transcript; it does not call `HandleProtocol`/`OpenProtocol`, dereference the observed protocol address, or prove that the transcript came from the running image. The host validator proves range shape, rounding, canonical reservation construction, ordering, overlap, capacity, totals, and modeled guard/stack-union policy only for supplied values. It does not populate the ledger, prove that omitted metadata is actually contained in the loaded image, prove EFI `LoaderCode`/`LoaderData` allocation provenance or a refreshed map generation, reserve/subtract/reclaim live memory, establish allocator ownership, switch stacks, map pages, load CR3, or enforce cache, NX, or W^X policy.
- Live page-table allocation, population, mutation, ownership, or processor enforcement. The M01-011 codec validates and encodes only caller-supplied values. Its precondition POD does not read or prove IA32_EFER.NXE, CR0.WP, CR4.LA57, CPUID physical width or 1 GiB-page capability, or IA32_PAT; its permissive nonleaf pointers do not by themselves prove effective descendant permissions; and its Accessed/Dirty decoder and protection-only re-encoder do not read or write a live table. No code in this slice installs a table hierarchy, maps the table arena RW+NX, proves page zero absent, performs a live RW-to-RX transition, loads CR3, executes `invlpg`, handles a page fault, or establishes the NX/W^X completion gate.
- Live boot identity mappings or table construction. M01-012 consumes caller-attested range, role, protection, cache, PAT, and processor-precondition values and emits caller-owned planning records only. It does not discover the executing-image sections, allocate table pages, assign table-page addresses, write a PML4/PDPT/PD/PT entry, map its own table arena, protect the stack guard in hardware, establish framebuffer MMIO cache correctness, read CR0/CR4/EFER/CPUID/IA32_PAT, load CR3, invalidate a TLB, or execute under EFI or QEMU/OVMF.
- Real `ExitBootServices`.
- Complete platform implementations for console, memory map, keyboard, hardware timer, firmware, filesystem, physical-memory mapping, framebuffer/GOP binding, and post-firmware RNG.
- IDT/GDT/TSS/IST setup.
- Fault handlers.
- Real owned serial driver after firmware shutdown. The post-firmware diagnostics gate and `IPlatformSerial` boundary are host-tested only and are not wired into the live EFI path.
- Default-build live ACPI RSDT/XSDT discovery from the firmware-provided RSDP pointer or from real physical memory.
- Boot-time app package loading or app launch through the default `BootSequence.Run` or live `EfiMain` path.

## Tests

Host tests verify the marker plan, `BootSequence.Run`, handoff-aware `BOOT_HANDOFF_READY` validation, `BOOT_MEMORY_MAP_ACCEPTED` validation, boot capability contract, typed handoff metadata including owned-map and ACPI RSDP records, typed physical/virtual/device address POD layout and checked arithmetic, UEFI memory-map contract, memory-map capture-helper success/failure paths, memory-map copy-plan validation, raw descriptor decoding, normalized memory-map conversion, caller-owned `MemoryMapInfo` region writing, no-stdlib boot memory-region materialization, allocator-facing boot memory-region planning, typed reservation subtraction with exact provenance/accounting and atomic failures, boot free-list segment planning, caller-owned boot free-list node construction, boot page allocator-state initialization, bounded pointer/count/capacity allocator-state materialization with explicit source-range cardinality and atomic failure behavior, caller-owned boot page allocator allocation plus receipt-bound exact-inverse restoration, deterministic seeded shadow ownership/provenance/content-tag replay, replay/double-free/nonmatching-foreign/stale/out-of-order rejection, exact x64 page-entry enum/POD layouts, all protection-byte combinations, level-aware 4 KiB/2 MiB/capability-gated 1 GiB encoding and strict decode, explicit PAT-index WB/UC/WC binding, NXE/WP/LA57/physical-width/1 GiB-capability precondition rejection, reserved-bit and malformed-POD rejection, Accessed/Dirty classification and explicit protection-rewrite preservation policy, failure-atomic two-pass pointer/count/capacity batch encoding, aggregate boot page allocator-readiness composition, boot page-allocator readiness markers, NativeAOT heap bootstrap readiness markers, NativeAOT cctor runner preflight readiness markers, PAL export catalog readiness markers, app package-file staging preflight markers, app argument-vector readiness markers, app argument-handoff readiness markers, app-exit observer readiness markers, ACPI RSDP configuration-table scanning and byte validation, guarded platform configuration-table scanning, guarded physical-memory-backed RSDP snapshot validation, caller-owned ACPI RSDT/XSDT pointer-list parsing, caller-provided ACPI table directory selection, ACPI MCFG discovery composition, ACPI physical discovery probe metadata, firmware guard contract, guarded platform adapter denial, and UEFI-gone simulation markers:

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

M01-004 owned boot-stack layout coverage is host-only.
`OwnedBootStackLayout.Create` consumes exactly two caller-owned canonical
reservation records through an unsafe pointer/count/capacity boundary. It
checks count, capacity, null, alignment, and the complete table-address span
before dereference; validates an explicit typed physical backing start, page
count, checked byte length/end, and an explicit typed physical stack top; and
then delegates structural, ordering, overlap, adjacency, and arena accounting
to `BootReservationTableValidator`. A successful 128-byte layout contains the
exact backing extent, canonical `BootStackGuard` and `BootStack` records, the
exclusive physical top, and an initial `Reserved` lifetime. The 176-byte result
also retains nested table/range statuses, capacity, invalid/conflicting
indexes, and previous/requested/current lifetime evidence, but never retains
the caller's table pointer.

The guard is the lower extent because the stack will grow downward. Both guard
and usable extents may contain one or more pages in this generic contract.
Their checked union must equal the entire backing extent; the guard end must
equal the usable start; and the supplied exclusive physical top must equal the
usable/backing end and be 16-byte aligned. This is deliberately a
`PhysicalAddress`, not a `VirtualAddress` or a call-ready `RSP`.

The immutable lifetime model permits only `Reserved -> Ready -> Active`.
`Reserved` means the supplied physical ownership geometry passed this value
contract. `Ready` and `Active` are higher-layer caller attestations. A
same-state request is classified as `RepeatedLifetimeTransition`, while skips
and reversals are rejected. Because the layout is an immutable, copyable value,
the pure leaf cannot detect two callers replaying the same stale `Reserved`
snapshot; M01-017 must place transitions behind authoritative serialized
coordination. This leaf does not inspect whether allocation, mapping, guard
protection, switch-stub installation, or a processor transition actually
occurred, so `Active` is not live stack proof.

`CLROS_M01_004_READY` is emitted only after the focused layout, malformed
input, nested-ledger, transition, purity, source-firewall, and exact compiler
closure tests pass:

```text
TEST_START m01.004
TEST_PASS m01.004 lifetime statuses and pod layouts are stable
TEST_PASS m01.004 default values fail closed
TEST_PASS m01.004 minimum representative and multi guard layouts succeed
TEST_PASS m01.004 high address layout remains checked
TEST_PASS m01.004 pointer count and capacity fail before dereference
TEST_PASS m01.004 backing range and stack top failures are classified
TEST_PASS m01.004 canonical table failures retain nested evidence
TEST_PASS m01.004 backing topology and accounting are exact
TEST_PASS m01.004 canonical reservations remain ledger compatible
TEST_PASS m01.004 reserved ready active transitions succeed
TEST_PASS m01.004 repeated and illegal transitions fail closed
TEST_PASS m01.004 malformed layouts and results cannot report success
TEST_PASS m01.004 validation precedence is deterministic
TEST_PASS m01.004 evaluation is nonmutating deterministic and allocation free
TEST_PASS m01.004 source excludes hosted live memory and stack switch dependencies
TEST_PASS m01.004 exact sources compile under nostdlib efi
CLROS_M01_004_READY
TEST_PASS m01.004
```

The isolated compiler fixture includes only the typed-address source,
`BootReservedRange`, `BootReservationTableValidator`, the owned-stack source,
a minimal primitive shim, and a positive pointer-core/lifetime probe. It
defines `NOSTDLIB_EFI`, removes all framework references, and verifies that the
emitted PE contains zero assembly references.

The implementation was independently authored after selectively reviewing the
following local reference evidence:

- `references/SharpOS-main/OS/src/Boot/BootStackPool.cs` (local snapshot
  SHA-256
  `B7C80FDD76BB0D17CF87AB358CCC50F413601D8A1B8C0A6ABB22EE8D40D90E7C`)
  influenced the need for explicit owned backing before old firmware stack
  memory can eventually be abandoned. Its static 4 MiB pool, raw pointers,
  implicit image ownership, and missing guard/lifetime model were not copied.
- `references/SharpOS-main/OS/src/Boot/BootStackSwitch.cs` (local snapshot
  SHA-256
  `637DCFC91E3B9647A0F8FBD4800E6C973ED78C6E5F73E2F7C10770C000234808`)
  influenced the one-shot ordering and 16-byte exclusive-top requirements.
  Its mutable global flag, raw stack pointers, live switch, unmanaged function
  pointer, continuation, and silently masked alignment were not copied.
- `references/RoseOS-master/Loader/src/MemoryMap.cs` (local snapshot SHA-256
  `CE90AE629B538E558B57B0BD315ED8B11DD559ED1A841368D109DE28D70B8B28`)
  was used only as a test-only reminder for fixed-capacity, overflow,
  ownership, and failure-continuation hazards. No RoseOS source or API was
  copied.

This evidence does not allocate EFI pages, establish `LoaderData` or
loaded-image provenance, insert records into a live ledger, map or unmap the
guard, construct page tables, enforce NX/W^X, derive a virtual switch target,
reserve the Windows x64 home space, read or write `RSP`, emit or invoke a
switch stub, preserve a continuation, abandon the firmware stack, link the
leaf into `src/OS/OS.csproj`, call it from `EfiEntry`, coordinate it through
`BootSequence`, run it under QEMU/OVMF, or change a completion-gate state.
M01-005 owns the switch contract; M01-017 owns cross-leaf coordination;
M01-020 owns explicit EFI linkage; M01-021 owns live allocation/wiring; and
M01-022 owns real-QEMU evidence.

M01-005 owned boot-stack switch planning and audit coverage is host-only.
`OwnedBootStackSwitchPlan.Create` consumes exactly two checked
`BootMemoryRegionRecord` values after validating count, capacity, pointer
alignment, and the complete allocation-table address span. Record zero must be
one page of `LoaderCode`; it is also represented by a canonical
`BootReservedRange` of kind `BootStackSwitchStub`. Record one must be
`LoaderData` and must match the complete physical backing range of an M01-004
layout in `Ready`. The physical and virtual allocations must be disjoint.
The data allocation's virtual prefix is the guard and its remaining exact
extent is the usable downward-growing stack. The plan rejects a continuation
inside either allocation or the handoff range, and rejects handoff storage
that overlaps the code page, guard, or usable stack.

The handoff is an exact 88-byte, versioned, sequential unmanaged value. Its
storage is 8-byte aligned and is accepted only with
`CallerAttestedStaticOwnedData` provenance. Likewise,
`StaticNonReturningRspAndHandoff` records the only accepted continuation
contract. Both properties are caller attestations: this leaf cannot inspect
whether either address is really static, owned, executable, or nonreturning.
M01-017 must serialize the live ownership decision.

The first switch byte plan is the following fixed 14-byte C# sequence:

```text
48 89 CC       mov rsp, rcx
48 31 ED       xor rbp, rbp
6A 00          push 0
48 89 E1       mov rcx, rsp
41 FF E0       jmp r8
```

The future caller supplies the aligned pre-transfer stack pointer in `RCX`,
the static handoff pointer in `RDX`, and the nonreturning continuation in
`R8`. The stub moves to the owned stack, clears the abandoned frame pointer,
pushes a zero fake return, publishes the actual continuation-entry `RSP` back
through `RCX`, and jumps rather than calls. `RDX` is unchanged. An actual
return therefore targets address zero; it faults only when the later
null-page-unmapped policy is live, which M01-005 does not prove.
`ReportUnexpectedReturn` is consequently only a host-side
state-classification model. It does not prove that a live return can be
intercepted.

Emission and verification are materialization-bound. They accept the base
pointer and capacity for the complete code page, require that pointer to be
4 KiB aligned and numerically identical to the plan's virtual code-page
start, require a materialized length equal to the complete one-page
allocation, and derive the write/read address as
`pageBase + StubEntryOffset`. The fixed bytes never operate on a detached
14-byte buffer. Successful evidence preserves the actual page-buffer address,
page length, capacity, stub offset, stub length, byte count, mismatch offset,
operation, and the complete immutable plan snapshot. Arming accepts only
successful `Verify` evidence whose exact plan snapshot matches; `Emit`
evidence, an alternate page, a different offset, or another plan fails
closed.

For a page-aligned usable-stack end `E`, the ABI arithmetic is:

```text
pre-transfer RSP       = E - 32
continuation-entry RSP = E - 40
fake return slot       = [E - 40, E - 32)
32-byte home space     = [E - 32, E)
```

The pre-transfer value is `0 mod 16`. After the eight-byte push, the
continuation-entry value is `8 mod 16`, as required by the Windows x64
function-entry contract. Both the fake return and all 32 home-space bytes
remain inside the usable stack. The continuation audit requires the observed
value to equal exactly `E - 40`; an address merely somewhere inside the stack
is insufficient.

The immutable plan carries no transition state. Instead it binds exactly one
caller-attested static 536-byte state-storage range. That range must be
8-byte aligned and disjoint from the code page, guard, usable stack, handoff,
and continuation. Every state API validates null, byte capacity, alignment,
the complete pointer span, and equality with the plan's canonical address
before its first dereference. Initialization then verifies all 536 bytes are
zero before writing an `OwnedBootStackSwitchStateRecord`. The record stores
`Size`, `Version`, its initialized storage address, the complete plan, a
transition count, the observed `RSP`, the current state, a terminal
cause/status, and reserved-zero bytes. Its stored address must continue to
equal both the actual pointer and the plan address, so copying valid state
bytes elsewhere or initializing a second record for the same plan is
rejected while the canonical bytes remain intact. All copies of a plan using
that intact canonical record therefore observe one authoritative host-mode
sequence:

```text
Uninitialized -> Planned -> Armed -> TransferRecorded
TransferRecorded -> ContinuationObserved
TransferRecorded -> Failed
```

Repeated arm, transfer, observation, and synthetic unexpected-return reports
are rejected. A malformed RSP audit moves the record to `Failed` with
`ContinuationRspAuditFailed` and the precise audit status, while a synthetic
return report records `UnexpectedReturnReported`; the two terminal cases are
not conflated. The leaf is designed for serialized early-boot use and
intentionally has no hidden static state or runtime lock. Restoring stale
bytes over the same canonical address, concurrent mutation, and live
cross-leaf ordering remain M01-017 responsibilities.

Some defensive status values describe invariant-corruption fallbacks that a
valid current page-granular M01-004 layout cannot normally reach, including
guard/ABI arithmetic failure, reservation-factory rejection, and
shadow-space overflow after containment. They remain fail-closed
revalidation outcomes, not claims that ordinary valid factory inputs exercise
those paths.

`CLROS_M01_005_READY` is a host-harness marker only. The focused suite covers
stable values and exact POD layouts; defaults; pointer-before-dereference
preflight; malformed LoaderCode/LoaderData records; physical and virtual
overlap; continuation/handoff aliasing; exact arena accounting; nonzero
stub-offset page materialization with untouched surrounding bytes; corrupted
readback; cross-page and cross-plan evidence rejection; state-record
self-binding; repeat and illegal transitions; exact RSP containment;
allocation-free deterministic behavior; live-execution source firewalls; and
the retained no-stdlib compiler closure:

```text
TEST_START m01.005
CLROS_M01_005_READY
TEST_PASS m01.005
```

The isolated compiler fixture includes the typed-address source,
`BootReservedRange`, `BootReservationTableValidator`,
`OwnedBootStackLayout`, `UefiMemoryType`,
`BootMemoryRegionMaterializer`, this switch-plan source, a minimal primitive
shim, and a positive pointer/state/page-evidence probe. It defines
`NOSTDLIB_EFI`, removes every framework reference, and verifies that the
emitted PE contains zero assembly references.

The implementation was independently authored after selectively reviewing
the following owner-authorized local SharpOS reference evidence:

- `references/SharpOS-main/OS/src/Boot/BootStackSwitch.cs` (local snapshot
  SHA-256
  `637DCFC91E3B9647A0F8FBD4800E6C973ED78C6E5F73E2F7C10770C000234808`)
  informed one-shot ordering, ABI alignment, and a nonreturning continuation.
- `references/SharpOS-main/OS/src/Boot/BootStackSwitchStub.cs` (local snapshot
  SHA-256
  `82BD871E197F5666E97B7A6124E360E9FDFBAD478E8579CBA2993700E96A3B02`)
  and
  `references/SharpOS-main/OS/src/Boot/BootStackSwitchPatcher.cs` (local
  snapshot SHA-256
  `4D889735C1507FB7F4DE53E616AB96929F49348531559BFD967A14F585063315`)
  informed the minimal stub shape and the need for exact byte/bounds
  readback. No in-place method-body patching or writable-executable assumption
  was adopted.
- `references/SharpOS-main/bootasm/BootAsm.Generator/BootAsmGenerator.cs`
  (local snapshot SHA-256
  `B24C79DCD38757DCBE243DC6C4C63C1AF628525F06C4F5D296B6AFB0C9387500`)
  informed deterministic byte validation only. CLROS does not import their
  generator, Iced dependency, patcher, or generated assembly.
- `references/SharpOS-main/OS/src/Boot/BootStackPool.cs` (local snapshot
  SHA-256
  `B7C80FDD76BB0D17CF87AB358CCC50F413601D8A1B8C0A6ABB22EE8D40D90E7C`)
  supplied guard/containment test ideas. Its mutable global state, fixed 4 MiB
  pool, raw pointers, and implicit ownership were not copied.

This proves only a host-audited stack-switch byte plan and RSP-containment
contract. It does not execute `mov rsp`, call or jump to the continuation,
alter the processor stack, materialize or protect a guest code page, prove
the handoff/continuation attestations, link the source into the EFI artifact,
run under QEMU, or prove a live owned-stack transition. It does not change a
completion-gate state. M01-017 owns authoritative cross-leaf serialization,
M01-020 owns explicit EFI linkage, M01-021 owns live allocation/wiring, and
M01-022 owns real-QEMU evidence.

### Loaded-image and owned-stack stage-one marker contract (M01-017)

M01-017 defines a host-tested stage-one composition boundary over the reviewed
M01-003 loaded-image, M01-004 owned-stack layout, and M01-005 stack-switch
leaves. The no-stdlib-compatible marker portion is deliberately separate from
the default boot plan. `BootSequencePlan.M01StageOneSuccessLineCount`,
`GetM01StageOneSuccessLineLength`, and `GetM01StageOneSuccessLineByte` expose
this exact success order:

`M01LoadedImageStackIntegration` is the value-only core of that boundary. Its
`Prepare`, `RecordTransfer`, `ObserveContinuationRsp`, and
`ReportUnexpectedReturn` calls serialize the reviewed leaves without invoking
firmware or the switch entry. `M01StageOneAllocationEvidence` binds exactly one
LoaderCode page and one separate LoaderData guard-plus-stack arena to ordered,
successful caller observations. `M01StageOneMapLedgerEvidence` requires a
changed opaque map key, an exactly advanced CLROS-owned generation, a
well-formed owned map copy, and later map-capture and ledger-build ordinals.
Those values are attestations supplied by the future live adapter; they are
not proof that firmware performed either allocation or map capture.

`M01LoadedImageVirtualMappingEvidence` keeps the loaded-image address domains
explicit. `M01StageOneAddressMappingEvidence` separately binds the LoaderCode,
LoaderData, and owned-map destination physical ranges to caller-attested
pre-hierarchy identity mappings before physical reservations are compared with
dereferenceable virtual pointers. Physical loaded-image bounds remain the
reservation-ledger authority; static-state and continuation containment
compares only virtual addresses against the attested virtual image range.
Full code and data virtual ranges must be disjoint from the loaded image,
owned map, allocation table, and ledger. The owned map destination must be
disjoint from its source, code, data, static state, continuation, allocation
table, and ledger; the table and ledger must also be disjoint. The map must be
covered by the exact loaded-image extent or by one canonical `OwnedMemoryMap`
reservation in the attested physical domain. The LoaderData arena contains
exactly one guard page followed by the usable stack.

The canonical path emits and reads back the exact 14-byte M01-005 stub before
arming one caller-owned `M01LoadedImageStackStaticState`. A separate
`VerifyExisting` audit mode permits deterministic rejection of a malformed
caller-materialized page without a callback or test-only production flag.
Handoff and one-shot state fields, plus the nonreturning continuation address,
must lie inside the exact loaded-image extent. `RecordTransfer` returns a
checked value-only target descriptor but does not turn its address into a
function pointer. Only an exact continuation `RSP` audit permits the
caller-attested stack layout to move from `Ready` to `Active`; a reported
return is terminal failure.

The large `M01LoadedImageStackIntegrationResult` is a pre-transfer
audit/report POD and must be discarded before a live transfer. The convenience
`RecordTransfer`, `ObserveContinuationRsp`, and `ReportUnexpectedReturn`
overloads that carry that aggregate are compiled only when
`NOSTDLIB_EFI` is absent. The live-facing transition
surface is the caller-owned `M01LoadedImageStackStaticState` pointer plus
`RecordTransferFromStaticState`, `ObserveContinuationRspFromStaticState`, and
`ReportUnexpectedReturnFromStaticState`. That static context contains copied
plan, layout, handoff, and one-shot state only; it does not contain transient
allocation-table or reservation-ledger addresses.

```text
TEST_START m01.stage1
M01_LOADED_IMAGE_CAPTURED
M01_STACK_SWITCH_STUB_READY
M01_STACK_ARENA_ALLOCATED
M01_STACK_GUARD_RESERVED
M01_STAGE1_MAP_RECAPTURED
M01_STAGE1_RESERVATIONS_VALIDATED
M01_OWNED_STACK_READY
M01_OWNED_STACK_SWITCHED
M01_RSP_CONTAINED
TEST_PASS m01.stage1
```

The plan contains fixed success bytes only. In M01-017 those lines are a
host-contract presentation emitted only after the final modeled result; their
names are reserved for the later live adapter and are not guest observations.
A host orchestration failure uses the separate status-bearing
`TEST_FAIL m01.stage1 status=<status> checkpoint=<checkpoint>` and
`M01_STAGE1_FAIL status=<status> checkpoint=<checkpoint>` paths and must
suppress every later checkpoint and the success terminal. A non-final
coordinator result whose operation status is `Success` is classified as
`IllegalCoordinatorState`; the failure path never emits `status=Success`.
The existing default
`BootSequencePlan.NonSmokeLineCount == 5` and `LineCount == 8` remain
byte-for-byte stable. `BootSequence.Run` does not emit any M01 stage-one line
on its default path.

Focused host coverage locks every success-line length and byte, invalid line
and offset behavior, default boot-plan stability, and the explicit EFI project
boundary. The EFI project currently links `BootSequencePlan.cs` because it
already consumes the default plan, but continues to exclude the managed
`BootSequence.cs` orchestration surface and compile globs. The aggregate
M01-017 host evidence markers are:

```text
TEST_START m01.017
CLROS_M01_017_READY
TEST_PASS m01.017
```

The M01-017 sequencing model was independently authored after selectively
reviewing owner-authorized
`references/SharpOS-main/OS/src/Boot/BootStackSwitch.cs` as a
`reference:adapt` influence. The local snapshot SHA-256 is
`637DCFC91E3B9647A0F8FBD4800E6C973ED78C6E5F73E2F7C10770C000234808`.
Only one-shot ordering, static handoff state, a nonreturning continuation, and
post-switch stack-bound validation informed the CLROS design. The mutable
global completion flag, raw-pointer interface, fixed-pool assumptions,
direct invocation/platform coupling, and return-to-zero behavior were not
copied. The canonical upstream is
`https://github.com/daniilvaino/SharpOS`; the owner grant and upstream license
evidence are tracked separately from this local snapshot, whose exact upstream
revision remains unknown.

This is synthetic host evidence only. It does not call UEFI `AllocatePages`,
prove allocation or memory-map provenance, recapture a live map, perform a
stack switch, inspect live `RSP`, leave a guard page unmapped, change
protections, enforce W^X/NX, wire `EfiEntry`, build or execute an M01 guest
probe, run QEMU/OVMF, or change a completion-gate state. M01-020 owns explicit
EFI core linkage, M01-021 owns live allocation and entry wiring, and M01-022
owns real-QEMU evidence.

### Firmware-arena allocator stage-two marker contract (M01-018)

M01-018 adds the second host-tested M01 coordinator boundary. It accepts only
the final live-facing M01-017 static state: the stage-one checkpoint must be
`ContinuationRspContained`, its one-shot switch state must be
`ContinuationObserved`, and the owned stack lifetime must be `Active`. A
prepared or transfer-recorded state is insufficient. The coordinator then
binds exactly one caller-attested successful EFI `LoaderData` allocation to a
changed owned-map key, an exactly incremented CLROS map generation, and
strictly ordered allocation, map-capture, and ledger-build ordinals.
The previous key/generation and the association between the refreshed map and
ledger remain caller attestations: M01-017 intentionally excludes transient
map/ledger pointers and epoch values from its post-switch static state, and
M01-018 does not parse the refreshed descriptor bytes. The future live adapter
must derive these values from one serialized capture; this host contract
detects malformed, stale, nonincrementing, reordered, or mismatched supplied
evidence but does not independently observe firmware.

`M01LiveAllocatorIntegration.Execute` is an unmanaged, no-stdlib-compatible
unsafe pointer/count/capacity core. Before dereferencing or writing, it checks
the complete declared spans of the stage-one state, one-record allocation
table, canonical reservation ledger, refreshed map source and owned-copy
buffers, and caller-owned stage-two output. Those metadata regions must be
pairwise disjoint and must not lie inside the arena. The arena is bounded to
2 through 4096 aligned 4 KiB pages, carries exact redundant byte/end
accounting, and is accepted only when the canonical sorted ledger contains
one matching `AllocatorArena`, one loaded-image record, and the exact
switch-stub, guard, and active stack ownership records retained by stage one.
The unique loaded-image record must contain both the complete self-bound
stage-one static state and its planned continuation address, preventing an
unrelated replacement image range from authorizing arena reuse.

For the two single-record static-state pointers, the declared capacity is part
of the alias contract rather than a minimum-size hint: the full capacity tail
must be representable and disjoint from every other metadata span, map buffer,
and arena. A wrapping capacity is rejected before dereference. This prevents a
valid first record from concealing an overlapping or unaddressable declared
tail.

The `AllocatorArena` record is ownership metadata, not an operational
exclusion. Passing it back to `BootReservationSubtractor` or
`BootPageAllocatorMutator` would subtract or reject the allocator's entire
source. The coordinator therefore validates the complete canonical ledger
first, proves every other reservation is disjoint from the sole exact arena,
then synthesizes one source range from that arena and calls the reviewed
subtractor with an empty exclusion table. `M01_RESERVATIONS_APPLIED` means
that this canonical ownership validation and exact arena selection succeeded;
it does not mean the arena self-excluded itself.

The identity subtraction must report one unchanged arena range, zero removed
pages/bytes, and exact remaining totals. From that result the coordinator
materializes one bounded free-list node, performs one first-fit 4 KiB metadata
allocation, verifies the complete returned range is inside the arena, frees
the exact allocation receipt, verifies byte-for-byte accounting restoration,
and requires a second free to fail specifically with
`FreeRangeNotAllocated` without mutating state. Caller-owned stage-two state
is written atomically only after this complete cycle passes. A second
execution against the completed state is rejected as a duplicate phase.

Five value-only phase auditors expose the same source/subtraction,
materialization, allocation/containment, free/accounting, and double-free
postconditions used by `Execute`. Host tests feed them real leaf-operation
results, including failed results and a successful allocation produced from a
different arena. They make late failure and cross-arena rejection directly
testable without fault injection. Allocation and free audits bind the exact
one-node, zero-reservation call metadata and restore receipt; the terminal
double-free audit additionally binds the original allocation, arena, unchanged
restored state/node, and exact rejection metadata. The auditors perform no
mutation themselves and do not widen this issue's host-only evidence boundary.

The independent packed success plan is:

```text
TEST_START m01.stage2
M01_ALLOCATOR_ARENA_ALLOCATED
M01_STAGE2_MAP_RECAPTURED
M01_RESERVATIONS_APPLIED
M01_LIVE_ALLOCATED_4K
M01_LIVE_PAGE_WITHIN_ARENA
M01_LIVE_FREED_4K
M01_DOUBLE_FREE_REJECTED
TEST_PASS m01.stage2
```

`BootSequence.RunM01LiveAllocatorIntegration` presents those lines
retrospectively only after the complete result reports success. Failure emits
only the stage-two start plus
`TEST_FAIL m01.stage2 status=<status> checkpoint=<checkpoint>` and
`M01_STAGE2_FAIL status=<status> checkpoint=<checkpoint>`. A non-final result
whose status is `Success` is reclassified as `IllegalCoordinatorState`.
The default five non-smoke/eight total boot lines remain byte-for-byte
unchanged, and `BootSequence.Run` never invokes this presenter.

The aggregate host-harness markers are:

```text
TEST_START m01.018
CLROS_M01_018_READY
TEST_PASS m01.018
```

The ordering model was independently authored after selectively reviewing
the owner-authorized local
`references/SharpOS-main/OS/src/Boot/ExitBootServicesProbe.cs` snapshot
(SHA-256
`65B892A8BD752C95077E17386FD127F040A3A7F013E968485ACCB2EC4FF24DA1`)
as a test-only influence. Only its allocation-before-map-recapture ordering
informed this host contract. No firmware call adapter, map retry loop,
platform coupling, console routing, or post-EBS behavior was copied.

This remains host evidence over caller-supplied pre-EBS metadata. It does not
call EFI `AllocatePages`, recapture a firmware map, execute allocator-backed
physical memory access, transfer ownership of arbitrary conventional pages,
wire or link the coordinator into `src/OS`, change `EfiEntry`, execute a guest
phase, establish a post-EBS allocator, configure paging, enforce NX/W^X, run
QEMU/OVMF, or change a completion-gate state. M01-020 owns explicit EFI core
linkage, M01-021 owns live allocation and entry wiring, and M01-022 owns
real-QEMU evidence.

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

M01-006 allocator-state materialization coverage is host-test only. It proves that the exact extracted 48-byte node POD and 56-byte state POD compile with `NOSTDLIB_EFI`, no framework references, and zero assembly references, and that a caller-owned node table can be validated and reduced atomically to one caller-owned state record. Coverage includes exact 4096-byte page policy; pointer/count/capacity, alignment, full-span overflow, and source/destination non-overlap checks; explicit nonzero bounded `sourceRangeCount`; source segment/range indexes; sorted ranges; malformed nodes; invalid heads/links; premature termination and cycles; exact page/byte totals; destination capacity; unchanged source order; unchanged destination bytes on every failure; unmanaged layouts; default-result failure; and stable issue markers. It does not source-link the contracts into the EFI project, invoke `EfiEntry`, install allocator ownership, reserve or mutate physical memory, allocate or free a page, configure paging, enforce NX/W^X, or provide QEMU/OVMF evidence:

```text
TEST_START m01.006
CLROS_M01_006_READY
TEST_PASS m01.006
```

M01-007 typed reservation-subtraction coverage is host-test only. It proves the
exact 40-byte source/fragment POD, status/result metadata, and unsafe
pointer/count/capacity core over caller-owned ranges. Positive coverage locks
zero-reservation identity, exact full-cover zero output, head/tail/middle
splits, multiple reservations, adjacency, no-overlap behavior, preserved exact
source-ordinal provenance, sorted/disjoint output, and exact
source/removed/remaining page and byte accounting. Negative coverage locks
default and malformed values; zero, unaligned, unsorted, overlapping, and
out-of-source geometry; a reservation that crosses a gap or source boundary;
count/capacity, pointer alignment, complete declared-span wrap and pairwise
alias rejection; arithmetic and output-fragment overflow; insufficient
destination capacity; deterministic failure indexes/statuses; unchanged
destination bytes on every failure; allocation-free repeated execution; and
source firewalls against the span-shaped host planners.

The isolated compiler closure contains only the typed-address contract,
`BootReservedRange`, `BootReservationSubtractor`, a minimal primitive shim,
and a positive unsafe subtraction probe. It defines `NOSTDLIB_EFI`, removes
all framework references under `NoStdLib=true`, `NoConfig=true`, and
`DisableImplicitFrameworkReferences=true`, and verifies that the emitted PE
contains zero assembly references. The issue marker remains host-harness
evidence only:

```text
TEST_START m01.007
CLROS_M01_007_READY
TEST_PASS m01.007
```

This leaf does not source-link into `src/OS/OS.csproj`, execute from
`EfiEntry`, consume the live firmware map, prove that a supplied source is an
explicitly firmware-allocated CLROS arena, install allocator state, reserve or
mutate a physical page, allocate or free memory, configure paging, enforce
NX/W^X, run under QEMU/OVMF, or change a completion-gate state. M01-023 owns
later application inside the firmware-allocated arena, and M01-024 owns its
guest proof.

M01-008 one-page allocator-metadata coverage is host-test only. It proves the
unsafe `AllocateOnePage` pointer/count/capacity surface over the exact
M01-006 node/state PODs and canonical reservation records. The result is a
readonly unmanaged value that fails closed by default and binds typed
`PhysicalAddress` start/end values, exact one-page/4096-byte geometry,
before/after allocator states, free/allocated page and byte accounting,
logical counts, declared capacities, visited/changed/inserted indexes, and
nested reservation/address evidence.

Positive coverage locks first-fit allocation at a range head or aligned tail,
exact-node consumption, middle splitting through an exact all-zero inactive
slot, rejection of stale nondefault unlinked split metadata, exact
typed-result accounting, canonical-reservation adjacency and immutability,
mutation of only the logical node prefix, untouched capacity canaries,
deterministic repeated execution, and compatibility of the host-only legacy
Span surface. Negative coverage
locks default/corrupt states; wrong page count, page size, or alignment;
malformed/unsorted/overlapping nodes; invalid indexes, broken chains, cycles,
and total mismatches; malformed/unsorted/overlapping reservations; every
reservation/free-range overlap; exhaustion; split-slot exhaustion; pointer,
count, capacity, alignment, full declared-span, and table-alias failures;
arithmetic overflow; forged success evidence; byte-for-byte failure atomicity;
and source/evaluated-closure firewalls.

With the exact aggregate page/byte invariant, hostile near-overflow allocator
states fail earlier as `InvalidStateAccounting`. The appended
`AllocatedPageCountOverflow` and `AllocatedByteCountOverflow` outcomes remain
defensive guards for a future contract change; they are not represented as
ordinary reachable success-path predecessors in this slice.

The exact no-stdlib compiler closure contains a minimal primitive shim, the
typed-address source, `BootReservedRange`,
`BootPageAllocatorCoreContracts`, `BootPageAllocatorMutation`, and a positive
unsafe probe. It defines `NOSTDLIB_EFI`, removes framework references under
`NoStdLib=true`, `NoConfig=true`, and
`DisableImplicitFrameworkReferences=true`, excludes the host Span
compatibility code, and verifies that the emitted PE contains zero assembly
references. The issue marker is host-harness evidence only:

```text
TEST_START m01.008
CLROS_M01_008_READY
TEST_PASS m01.008
```

This leaf preserves `PerformsLiveAllocation=false`. It does not source-link
into `src/OS/OS.csproj`, execute from `EfiEntry`, allocate from an
authoritative firmware-owned arena, reserve the reported page, zero it, map
it, set protections, access its bytes, run under QEMU/OVMF, or change a
completion-gate state. M01-023 owns the first metadata allocation/free cycle
over the explicitly firmware-allocated pre-EBS arena, and M01-024 owns the
guest proof.

M01-009 one-page exact-inverse coverage is also host-test only. A successful
real M01-008 `AllocateOnePage` result has `CanRestore=true` and embeds an
initialized `BootPageAllocatorOnePageRestoreReceipt` retaining the complete
original source node, source-free-segment and source-usable-range provenance,
and predecessor evidence. The enclosing allocation result retains the exact
pre/post-allocation allocator states plus node/reservation counts and
capacities. `FreeOnePage` consumes that successful result and embedded receipt
rather than a caller-selected raw address. It requires the supplied current
state and node/reservation count/capacity snapshot to equal the allocation
result's recorded post-allocation snapshot and then restores the exact
pre-allocation state and logical node topology byte-for-byte.

Positive coverage locks every allocation shape and its deterministic
coalescence classification: whole-node allocation restores with `None`, a
tail allocation restores with `Previous`, a head allocation restores with
`Next`, and a middle split restores with `PreviousAndNext`. The tests verify
the original head/predecessor links, source node, split-slot clearing,
source-free-segment and source-usable-range provenance, free and allocated
page/byte totals, logical count, declared capacity, and capacity-tail canaries.
All 48 bytes of a split candidate slot, including the four trailing padding
bytes, must be zero before allocation; exact-inverse clearing therefore cannot
erase caller-owned padding hidden behind otherwise default-valued fields.
Each success is an exact inverse of its own allocation receipt; it is not a
general adjacent-range insertion or arbitrary-address free.

Negative coverage locks default and unsuccessful receipts; altered typed
ranges, source nodes, provenance, predecessor evidence, states, counts, and
capacities; receipts from a different allocation whose recorded evidence
differs; replay and double free;
stale and out-of-order transitions; malformed current node geometry, links,
indexes, cycles, and accounting; zero, unaligned, and wrapping receipt
geometry; pointer/count/capacity and declared-span overflow; metadata capacity
failures inherited from allocation; nonzero inactive-slot tail padding;
source/successor order reversal; and byte-for-byte failure atomicity.
Bounded chain traversal rejects malformed cycles deterministically and has no
timer-dependent success path. A default `BootPageAllocatorOnePageFreeResult`
is never successful, and failure markers retain a stable status reason.

This strict snapshot rule deliberately supports only caller-serialized
immediate restoration or LIFO restoration. It does not maintain a general
allocation ledger, permit arbitrary-order freeing, or provide an unforgeable
capability. Because the receipt is a copyable POD and no generation-bearing
live ownership table exists, a byte-identical receipt from an indistinguishable
allocator copy cannot be rejected. The same identity-free boundary cannot
eliminate ABA if an address is freed, reallocated, and then presented through
a stale copied receipt. Those limits are explicit and must not be promoted into
broader allocator-ownership claims.

The M01-009 no-stdlib firewall evaluates the exact typed-address,
`BootReservedRange`, `BootPageAllocatorCoreContracts`, and
`BootPageAllocatorMutation` closure with `NOSTDLIB_EFI`, `NoStdLib=true`,
`NoConfig=true`, `DisableImplicitFrameworkReferences=true`, and zero assembly
references. The production closure contains no `Span`, strings, exceptions,
reflection, managed-reference fields, planner/result dependencies, or
MinimalRuntime-unavailable BCL constants. The historical Span-shaped
`Allocate`/`Free` compatibility implementation remains excluded behind
`#if !NOSTDLIB_EFI` and remains non-authoritative.

Reference use is test-only and records exact snapshot evidence:

- `references/abanu-master/src/Abanu.Kernel.Core/MemoryManagement/PageAllocators/BuddyAllocatorImplementation.cs`, SHA-256 `11913EDC30E2E4210A4F69BD352F9389CB7BEECAEF108CCF9E9A67272E6F4A3E`, supplies independently recreated zero/one/two-neighbor restoration and fragmentation vectors.
- `references/nativeaot-patcher-main/src/Cosmos.Kernel.Core/Memory/PageAllocator.cs`, SHA-256 `EC216B54C61765DC83F7CD6945614C2D97BFBDA6D2925668B12C4239D2E2368B`, supplies independently recreated ownership, double-free, boundary, and exhaustion failure categories.

No reference allocator implementation is imported. A successful M01-009
host-harness transcript is:

```text
TEST_START m01.009
CLROS_M01_009_READY
TEST_PASS m01.009
```

Any failing case terminates with `TEST_FAIL m01.009 <reason>` instead of the
ready/pass pair. None of these markers is guest evidence.

This leaf preserves `PerformsLiveAllocation=false`. Neither
`AllocateOnePage` nor `FreeOnePage` owns, frees, zeros, maps, protects, or
accesses a physical page. The exact-inverse source is not linked into
`src/OS/OS.csproj`, is not invoked from `EfiEntry`, has no QEMU/OVMF evidence,
and changes no completion gate. M01-024 exclusively owns guest evidence for
the pre-EBS arena allocation/free/double-free-rejection cycle.

M01-010 adds a deterministic host-only shadow-model stress oracle without
changing the production allocator. The canonical arena represents 128
synthetic physical page ordinals at base `0x00200000`: four provenance-distinct
free extents contain 10, 32, 40, and 32 pages, while three canonical
reservations cover the 14 intervening pages. Active nodes begin at logical
indexes 0, 7, 19, and 31 in a 40-slot table with a 44-slot declared capacity;
the four capacity-tail nodes are nondefault byte canaries. The independent
model records usable/allocated ownership, source-free-segment and
source-usable-range provenance, ordered free extents, a receipt stack, and
separate expected/actual 64-bit content tags for every synthetic page.
Expected first fit is derived from the page model, never from the allocator's
mutated node chain. After every operation, the test separately traverses the
active chain and validates chain shape, active geometry/order/provenance,
node/page/byte totals, ownership, content tags, reservation bytes, inactive
logical slots, and capacity-tail bytes. Failed operations and matching
exact-inverse frees additionally require byte-identical full storage
snapshots.

The generated stream uses a test-local xorshift64-star PRNG with fixed unsigned
64-bit seeds. Bounded choices use rejection sampling instead of modulo-biased
selection. Each churn step consumes a selector, alignment input, and content
word before applying a documented 65-percent allocate / 35-percent strict-LIFO
free mix. State-dependent fallback allocates when the stack is empty and frees
when depth reaches 24. Alignments are powers of two from 4 KiB through 256 KiB.
After each generated phase the receipt stack drains, every modeled free page is
then allocated at 4 KiB alignment, one extra allocation proves
`NoSuitableRange`, and the complete stack drains back to the byte-identical
baseline. Successful free operations pop only the newest receipt because the
production boundary is an exact inverse, not an arbitrary-order allocator.

The canonical replay uses seed `0x6A09E667F3BCC909` and 1,024 generated churn
steps. Including initial/final transcript records and the forced
exhaustion/unwind phase, it produces 1,278 canonical records, 549 successful
allocations, 549 successful frees, and 178 expected no-range failures. Two
fresh replays must match the checked-in SHA-256 transcript:

```text
12F1D263FC34D423E628574705A22F426BF43BACBAA38F53BEF886F5AB5D8D96
```

Three additional 384-step streams freeze independent seed control and
cross-seed diversity:

```text
seed=0x3C6EF372FE94F82B stream=2 hash=A74D2B2234A669DE8FDD7A478A3D12BF6601378E3557961B4EE1ADAAD20DB4D7
seed=0xA54FF53A5F1D36F1 stream=3 hash=83AD88944AC8DB83A235E41A981F99EE02B652BC8DB2D596A0CF8AF585FC88B9
seed=0x510E527FADE682D1 stream=4 hash=767149D6F2EEC88CDC524E96C5C2F2E2024B92B90E1D92A200C353E1EB1231C8
```

Transcript fields use explicit little-endian fixed-width encoding and include
the seed/stream, generated words, operation and outcome, typed raw page
address, allocator state, normalized node/reservation fields, receipt depth,
coverage counters, and every page's ownership, provenance, and content tags.
Host pointers, timestamps, process data, object hashes, unordered collection
enumeration, culture-sensitive text, and raw padding are excluded.

Scripted cases guarantee whole/head/tail/middle allocation and the corresponding
`None`/`Next`/`Previous`/`PreviousAndNext` exact-inverse classifications rather
than relying on random chance. Positive coverage also locks nested LIFO churn,
fragmentation, exact pre-allocation table restoration, full exhaustion, first
and last synthetic boundary pages, the valid high page
`[0xFFFFFFFFFFFFE000,0xFFFFFFFFFFFFF000)`, complete baseline restoration, and
golden replay hashes. Negative coverage locks default and initialized-empty
state, zero/multiple-page and malformed alignment requests, high-address
alignment overflow, logical-slot metadata exhaustion despite spare capacity,
node/accounting corruption, out-of-order receipt use, nonmatching foreign
evidence, replay/double free, and byte-for-byte failure atomicity. Deliberately
flipping one ownership mirror bit and one actual content tag proves that the
independent oracle detects both corruptions; the production allocator cannot
see or reject those host-owned buffers.

Reference use is behavioral/test-only. The CLROS generator, model, transcript,
and assertions were independently authored; no reference source was copied,
built, linked, imported, or used as completion evidence. The local archives
have no recoverable Git revision:

- `references/abanu-master/src/Abanu.Lib/Tests/BinaryBuddyAllocator_TestImplementation.cs`, SHA-256 `22AF15CD46CBAEB28E4C8C1C18FF624C98B1EBFAA3ED0904D187263ACB2AFF7A`, is only a split/merge behavioral cross-check; despite the original manifest note, it does not contain the seeded content-oracle loop.
- `references/abanu-master/src/Abanu.Test.Console/Alloc6Test.cs`, SHA-256 `5FFC32601255779DA00A01503BD4DF08D0516C09424344A9FDB2ABF01D6443E5`, supplies only the fixed-seed churn and live-allocation content-checking test pattern. CLROS does not copy its allocator, `System.Random`, dictionary-order, biased-selection, unchecked-null, or unmanaged-lifetime behavior.
- `references/nativeaot-patcher-main/src/Cosmos.Kernel.Core/Memory/PageAllocator.cs`, SHA-256 `EC216B54C61765DC83F7CD6945614C2D97BFBDA6D2925668B12C4239D2E2368B`, supplies only independently recreated 4 KiB, exhaustion, boundary, ownership, and double-use failure categories; its RAT/HHDM/pointer/exception implementation is not imported.

The successful host transcript boundary is:

```text
TEST_START m01.010
M01_010_REPLAY seed=0x6A09E667F3BCC909 churn=1024 records=1278 allocations=549 frees=549 expected_failures=178 hash=12F1D263FC34D423E628574705A22F426BF43BACBAA38F53BEF886F5AB5D8D96
CLROS_M01_010_READY
TEST_PASS m01.010
```

Any group failure reaches `TEST_FAIL m01.010 <reason>` and throws before the
ready/pass pair. `BootPageAllocatorStressTests.cs` remains under
`tests/OS.HostTests`; the EFI project keeps default compile discovery disabled
and does not include that file or the host-test project. This finite corpus
proves reproducible host agreement between caller-owned metadata and an
independent logical oracle only. It does not prove live physical
allocation/free/ownership, real page contents or zeroing, firmware arena
provenance, mapping, paging, CR3, NX/W^X, DMA, concurrency, arbitrary-order
free, allocator identity, generation authority, indistinguishable-copy
rejection, ABA safety, EFI execution, QEMU/OVMF execution, or any completion
gate.

M01-011 defines a value-only x64 page-table entry contract; it does not build
or touch a page table. The stable one-byte policy values are:

| Type | Value | Meaning |
|---|---:|---|
| `PageProtection.None` | 0 | Only an explicit unmapped entry, or the not-applicable protection field of a table pointer |
| `PageProtection.Read` | 1 | Supervisor read-only, non-executable leaf |
| `PageProtection.Write` | 2 | Bit value only; never a valid leaf state without `Read` |
| `PageProtection.Execute` | 4 | Bit value only; never a valid leaf state without `Read` |
| `PageCachePolicy.Uninitialized` | 0 | No present leaf cache policy; required for an explicit unmapped request |
| `PageCachePolicy.WriteBack` | 1 | WB at the explicitly selected PAT index |
| `PageCachePolicy.Uncached` | 2 | UC at the explicitly selected PAT index |
| `PageCachePolicy.WriteCombining` | 3 | WC at the explicitly selected PAT index |

`PageProtection` is deliberately a plain enum without
`System.FlagsAttribute`; the no-stdlib closure does not supply that attribute.
The codec validates combinations manually. Present leaves accept exactly
`Read`, `Read|Write`, or `Read|Execute`. `Write`, `Execute`,
`Write|Execute`, `Read|Write|Execute`, and values containing any unknown bit
fail closed. `None` plus a valid level and otherwise default fields encodes an
explicit `Unmapped` entry as raw zero. A default request is not the same thing
and fails as uninitialized.

The canonical level and kind values are append-only:

| Token | Value | Form | Physical alignment | Leaf size |
|---|---:|---|---:|---:|
| `X64PageTableLevel.Pte` | 1 | `Leaf` only | 4 KiB | 4 KiB |
| `X64PageTableLevel.Pde` | 2 | `TablePointer` or large `Leaf` | 4 KiB or 2 MiB | 2 MiB when a leaf |
| `X64PageTableLevel.Pdpte` | 3 | `TablePointer` or capability-gated large `Leaf` | 4 KiB or 1 GiB | 1 GiB only when the supplied CPUID observation reports support |
| `X64PageTableLevel.Pml4e` | 4 | `TablePointer` only | 4 KiB | none |
| `X64PageTableEntryKind.Unmapped` | 1 | Raw zero at any valid level | none | none |
| `X64PageTableEntryKind.TablePointer` | 2 | Nonleaf hierarchy pointer | 4 KiB | none |
| `X64PageTableEntryKind.Leaf` | 3 | Final mapping | level-specific | level-specific |

Table-pointer bits deliberately are not forced through the leaf
`PageProtection` contract. A nonleaf entry must allow writable and executable
descendants to coexist under leaf-level policy, so the canonical table pointer
is supervisor `P|RW, NX=0`, `PS=0`, with raw IA32_PAT entry zero explicitly
validated as WB. This page-walk selection is independent of which duplicate WB
entry the caller selected for leaf mappings. It reports `PageProtection.None`
because it does not map its table page as a leaf. The page-table arena itself
must later receive a separate RW+NX leaf mapping. UC/WC page-walk encodings are
rejected rather than silently conflating leaf PAT semantics with paging-
structure caching.

The encoder emits only the following architectural bits:

| Bit | PML4E/nonlarge PDPTE/PDE | 4 KiB PTE leaf | 2 MiB/1 GiB leaf |
|---:|---|---|---|
| 0 | Present | Present | Present |
| 1 | Writable descendant permission, always set | Set only for `Read|Write` | Set only for `Read|Write` |
| 2 | Clear; supervisor only | Clear; supervisor only | Clear; supervisor only |
| 3 | Clear for the WB table-pointer contract | PAT-index PWT selector | PAT-index PWT selector |
| 4 | Clear for the WB table-pointer contract | PAT-index PCD selector | PAT-index PCD selector |
| 5 | Encoder clears; decoder reports hardware Accessed | Encoder clears; decoder reports hardware Accessed | Encoder clears; decoder reports hardware Accessed |
| 6 | Rejected | Encoder clears; decoder reports hardware Dirty | Encoder clears; decoder reports hardware Dirty |
| 7 | Clear | PAT selector | Page-size (`PS`) selector |
| 8 | Clear; global mappings are deferred | Clear | Clear |
| 9-11 | Rejected CLROS software bits | Rejected | Rejected |
| 12 | Physical-address bit | Physical-address bit | PAT selector |
| 13 through address-width minus one | Physical address | Physical address | Physical address above the level-specific alignment |
| 52-58 | Rejected CLROS software bits | Rejected | Rejected |
| 59-62 | Rejected protection-key bits | Rejected | Rejected |
| 63 | Clear for permissive table pointers | NX when `Execute` is absent | NX when `Execute` is absent |

The caller supplies a `X64PageTableEncodingPreconditions` POD containing an
observed CPUID physical-address width and 1 GiB-page capability, NXE
observation, CR0.WP observation, LA57 observation, and
`X64PatMappingToken`. Present-entry encoding and decoding accept only
four-level paging, a physical width from 36 through 52, NXE=true, WP=true, and
an initialized PAT token. A PDPTE leaf additionally requires the observed
1 GiB-page capability; PDE 2 MiB leaves do not. Every byte-backed flag and
reserved byte is revalidated on every use. These are caller-supplied value
observations: the codec does not execute CPUID, read or write CR0/CR4/EFER,
read IA32_PAT, or prove those fields match live processor state.

`X64PatMappingToken.TryCreate` validates all eight bytes of one supplied raw
IA32_PAT value, rejects reserved bits and reserved memory-type encodings, and
binds WB to one required index plus optional UC and WC indexes. The token's raw
value, selected indices, initialization byte, and explicit reserved field are
revalidated whenever it is consumed, so an unsafe-copied malformed POD cannot
silently alias an out-of-range index. Index bits map to entry bits as
`index[0] -> PWT`, `index[1] -> PCD`, and
`index[2] -> PAT`. The PAT bit is bit 7 for a 4 KiB PTE and bit 12 for a large
PDE/PDPTE. Missing UC/WC selection returns `CachePolicyNotSupported`; a bare
boolean or silent assumption about firmware PAT programming is not accepted.

The codec never masks a bad physical address into apparent validity. It
rejects zero, level misalignment, a supplied physical-width value outside
36-52, and any address at or above `1 << physicalAddressWidth`. Strict decode
accepts hardware Accessed on every present entry and Dirty only on leaves,
exposes those observations separately, and rejects user/global/software/key,
level-reserved, large-page-reserved-low, and above-width bits. Newly encoded
entries keep Accessed and Dirty clear. Re-encoding an accessed/dirty decoded
leaf therefore produces the canonical entry with those hardware observations
cleared. `TryReencodeLeafProtection` is the explicit exception: it validates
an existing strict leaf, changes only its protection, and requires
`X64HardwareStatePolicy.Clear` or `Preserve` to determine whether the supplied
Accessed/Dirty observations remain in the returned value. It still performs no
live read, write, synchronization, or TLB invalidation.

`X64PageTableEntry.TryEncodeMany` is the unsafe no-allocation core. It accepts
a request pointer, logical request count, full declared source capacity,
destination pointer, full declared destination capacity, and one encoding
context. It bounds both capacities at 512 records; rejects zero or excessive
count, insufficient capacity, null or non-8-byte-aligned pointers, wrapping
compiler-sized spans, a declared/actual POD layout mismatch, and any overlap
between complete declared source and destination spans before dereferencing
caller data. Its first pass validates every logical request without writing;
only then does a second deterministic pass encode directly to the destination.
There is no fixed page-sized stack scratch. An invalid final request leaves the
whole destination byte-identical, while source bytes and destination capacity
tails are never modified. As with every unsafe pointer contract, arithmetic
validation cannot prove that a supplied address is mapped or stable; callers
must own and exclusively stabilize the immutable source and destination spans
for both passes.

`X64PageTableEntryStatus` is a one-byte append-only result contract:

```text
0 Uninitialized                         20 InvalidPhysicalAddress
1 Success                               21 MisalignedPhysicalAddress
2 RequestNotInitialized                 22 PhysicalAddressOutsideWidth
3 InvalidLevel                          23 NonCanonicalUnmappedEntry
4 InvalidEntryKind                      24 UnsupportedEntryBits
5 InvalidUnmappedEntry                  25 InvalidLargePage
6 InvalidTablePointer                   26 DirtyBitOnTablePointer
7 InvalidProtection                     27 NonCanonicalTablePointer
8 WritableExecutableRejected            28 InvalidBatchCount
9 InvalidCachePolicy                    29 TooManyEntries
10 CachePolicyNotSupported              30 SourceCapacityTooSmall
11 PatMappingNotInitialized             31 DestinationCapacityTooSmall
12 InvalidPatEntry                      32 CapacityExceedsLimit
13 PatIndexOutOfRange                   33 MissingSource
14 PatMemoryTypeMismatch                34 MissingDestination
15 PreconditionsNotInitialized          35 MisalignedSource
16 NxeNotEnabled                        36 MisalignedDestination
17 WriteProtectNotEnabled               37 SourceSpanOverflow
18 FiveLevelPagingNotSupported          38 DestinationSpanOverflow
19 UnsupportedPhysicalAddressWidth      39 SourceDestinationOverlap
40 ContractLayoutMismatch               44 InvalidRequestState
41 InvalidPatMappingState               45 InvalidHardwareStatePolicy
42 InvalidPreconditionState             46 ExistingEntryNotLeaf
43 OneGiBPagesNotSupported
```

The retained reference use is architectural/test influence, not imported
implementation. The snapshot has no recoverable Git revision:

- `references/SharpOS-main/OS/src/Kernel/Paging/PageFlags.cs`, SHA-256 `DC565A4B8BFA38CDACFBDB5B53B2D5616BB9C1057DEDE7A1673956D6F3198652`, cross-checks x64 flag positions.
- `references/SharpOS-main/OS/src/Kernel/Paging/X64PageTable.cs`, SHA-256 `35503B543628E5A462073D498704050EEEF7D79DEB2B84F01CACA29714200BAC`, cross-checks 4 KiB/2 MiB/1 GiB address masks and level structure. CLROS does not copy its live ownership, blind masking, permissive leaf, or TLB behavior.
- `references/SharpOS-main/OS/src/Kernel/Paging/PagingValidation.cs`, SHA-256 `B1699D328E49D5FC68B8CF8979B366D72CCA2E3B4AA5B982F91F0A2C9A1C40F6`, supplies only independently recreated round-trip, duplicate, neighbor, and malformed-entry test categories.

Successful host evidence is:

```text
TEST_START m01.011
CLROS_M01_011_READY
TEST_PASS m01.011
```

Any numbered-group failure emits `TEST_FAIL m01.011 <reason>` and reaches an
aggregate `TEST_FAIL m01.011 failures=<count>` plus a throw before the
ready/pass pair. The exact address/policy/entry sources compile in a synthetic
`NoStdLib=true`, `NOSTDLIB_EFI`, zero-framework-reference closure with zero PE
assembly references. Separate evaluated `SingleMinimalApi` and
`MultiMinimalApi` project closures prove those sources and the test are not
currently linked into `src/OS/OS.csproj`.

This is host-only value-model evidence. It does not allocate table pages, own
or populate a page-table arena, read or write a live entry, enable NXE or WP,
read or program live PAT state, prove effective mixed-subtree permissions,
load CR3, invalidate a TLB, trigger a page fault, define `DmaMapping`, execute
inside EFI, run under QEMU/OVMF, or change the NX/W^X completion gate. M01-014
and M01-019 must consume these exact policy, level, context, entry, and result
types; they may not introduce a second level token or treat the observation
POD as processor proof.

### Boot identity-mapping plan contract (M01-012)

M01-012 adds a caller-owned, value-only planner for the exact identity ranges
that a later owned four-level x64 page-table image must cover. The planner
does not infer ranges from the canonical boot-reservation ledger alone:
`BootReservedRange` has no owned-serial-stub kind and a single loaded-image
reservation cannot describe executable and writable image sections with
different leaf policies. The caller therefore supplies explicit mapping
requirements with typed physical half-open bounds, purpose, protection, and
cache policy. Each emitted record carries equal typed physical and virtual
bounds; the raw integer exists only inside the already-audited typed-address
and M01-011 encoding boundaries.

The complete input must contain all six emitted boot purposes:

- `ExecutingImage`; multiple disjoint records are allowed so code, read-only
  data, and writable data can retain different policies.
- `OwnedStack`.
- `BootHandoff`.
- `SerialStub`.
- `AcpiScratch`.
- `FramebufferMetadata`.

One `BootStackGuard` requirement is also mandatory, but it is an exclusion:
the planner validates it and rejects every mapped overlap, then emits no
mapping for it. `AdditionalRequired` is an explicitly mapped role for later
caller-owned resources such as the page-table arena; it does not weaken the
six-purpose completeness check or turn an unknown purpose into a valid one.

`X64BootIdentityMappingPurpose` is a one-byte append-only contract:

| Value | Purpose | Plan rule |
|---:|---|---|
| 0 | `Uninitialized` | Invalid/default |
| 1 | `ExecutingImage` | Required and emitted; may appear more than once |
| 2 | `OwnedStack` | Required and emitted |
| 3 | `BootHandoff` | Required and emitted |
| 4 | `SerialStub` | Required and emitted |
| 5 | `AcpiScratch` | Required and emitted |
| 6 | `FramebufferMetadata` | Required and emitted |
| 7 | `BootStackGuard` | Exactly one required; validated but never emitted |
| 8 | `AdditionalRequired` | Optional, repeatable, and emitted |

The exact public constants are:

| Constant | Value | Meaning |
|---|---:|---|
| `X64BootIdentityMappingRequirement.StructureSize` | 40 | Frozen requirement POD size in bytes |
| `X64BootIdentityMappingRange.StructureSize` | 64 | Frozen normalized-range POD size in bytes |
| `X64BootIdentityMappingPlanResult.StructureSize` | 176 | Frozen result POD size in bytes |
| `PageSize` | 4096 | The only leaf size emitted by this plan |
| `MaximumSourceCount` | 256 | Maximum source logical count and capacity |
| `MaximumDestinationCount` | 512 | Maximum destination capacity/output count |
| `RequiredPointerAlignment` | 8 | Required source/destination pointer alignment |
| `LowCanonicalEndExclusive` | `0x0000800000000000` | Exclusive identity-address ceiling |
| `RequiredPurposeMask` | `0x7e` | Bits 1 through 6; the six emitted required purposes |
| `UsesOnly4KiBLeaves` | `true` | The planner accounts for and validates only 4 KiB leaf mappings |
| `PerformsLiveMapping` | `false` | The planner never changes a live address space |
| `AllocatesPageTables` | `false` | The planner only reports required table capacity |
| `PopulatesPageTables` | `false` | The planner emits mapping records, not page-table entries |
| `X64BootIdentityMappingPlanResult.NoIndex` | `~0UL` | Initialized result sentinel for no source index |

The sole production entry point is:

```text
X64BootIdentityMappingPlan.Create(
    X64BootIdentityMappingRequirement* source,
    ulong sourceCount,
    ulong sourceCapacity,
    X64BootIdentityMappingRange* destination,
    ulong destinationCapacity,
    ulong availableTablePageCapacity,
    X64PageTableEncodingPreconditions preconditions)
```

Its three readonly sequential unmanaged POD layouts are frozen exactly:

| POD | Size | Offset | Field |
|---|---:|---:|---|
| `X64BootIdentityMappingRequirement` | 40 | 0 | `PhysicalAddress PhysicalStart` |
|  |  | 8 | `PhysicalAddress PhysicalEndExclusive` |
|  |  | 16 | `ulong PageCount` |
|  |  | 24 | `ulong ByteLength` |
|  |  | 32 | one-byte initialization state |
|  |  | 33 | one-byte `Purpose` |
|  |  | 34 | one-byte `Protection` |
|  |  | 35 | one-byte `CachePolicy` |
|  |  | 36 | four reserved zero bytes |
| `X64BootIdentityMappingRange` | 64 | 0 | `PhysicalAddress PhysicalStart` |
|  |  | 8 | `PhysicalAddress PhysicalEndExclusive` |
|  |  | 16 | `VirtualAddress VirtualStart` |
|  |  | 24 | `VirtualAddress VirtualEndExclusive` |
|  |  | 32 | `ulong PageCount` |
|  |  | 40 | `ulong ByteLength` |
|  |  | 48 | `ulong ContributingPurposeMask` |
|  |  | 56 | one-byte initialization state |
|  |  | 57 | one-byte `Protection` |
|  |  | 58 | one-byte `CachePolicy` |
|  |  | 59 | one reserved zero byte |
|  |  | 60 | four reserved zero bytes |
| `X64BootIdentityMappingPlanResult` | 176 | 0 | one-byte initialization state |
|  |  | 1 | one-byte `Status` |
|  |  | 2 | one-byte nested `AddressStatus` |
|  |  | 3 | one-byte nested `PageTableEntryStatus` |
|  |  | 4 | four reserved zero bytes |
|  |  | 8 | `ulong SourceCount` |
|  |  | 16 | `ulong ValidatedSourceCount` |
|  |  | 24 | `ulong SourceCapacity` |
|  |  | 32 | `ulong DestinationCapacity` |
|  |  | 40 | `ulong RequiredDestinationCapacity` |
|  |  | 48 | `ulong OutputRangeCount` |
|  |  | 56 | `ulong AvailableTablePageCapacity` |
|  |  | 64 | `ulong RequiredPurposeMask` |
|  |  | 72 | `ulong ObservedPurposeMask` |
|  |  | 80 | `ulong MissingPurposeMask` |
|  |  | 88 | `ulong InvalidSourceIndex` |
|  |  | 96 | `ulong ConflictingSourceIndex` |
|  |  | 104 | `ulong GuardSourceIndex` |
|  |  | 112 | `ulong UnionPageCount` |
|  |  | 120 | `ulong UnionByteLength` |
|  |  | 128 | `ulong Pml4PageCount` |
|  |  | 136 | `ulong PdptPageCount` |
|  |  | 144 | `ulong PdPageCount` |
|  |  | 152 | `ulong PtPageCount` |
|  |  | 160 | `ulong TotalTablePageCount` |
|  |  | 168 | `ulong TotalTableByteLength` |

All explicit initialization and reserved bytes are revalidated. Default PODs
remain deterministic failure values; only an initialized result with
`Status=Success` has `Succeeded=true`. Initialized success and non-indexed
failure results use `NoIndex`, while a default result retains zero fields and
must not be mistaken for initialized no-index evidence.

`X64BootIdentityMappingStatus` is a one-byte append-only result contract:

```text
 0 Uninitialized                       24 InvalidCachePolicy
 1 Success                             25 InvalidGuardPolicy
 2 InvalidSourceCount                  26 MultipleGuards
 3 TooManySources                      27 MissingGuard
 4 SourceCapacityTooSmall              28 GuardOverlap
 5 CapacityExceedsLimit                29 MissingRequiredPurpose
 6 MissingSource                       30 PageZeroMappingRejected
 7 MissingDestination                  31 NonCanonicalIdentityRange
 8 MisalignedSource                    32 PhysicalAddressOutsideWidth
 9 MisalignedDestination               33 IncompatibleProtectionOverlap
10 SourceSpanOverflow                  34 IncompatibleCacheOverlap
11 DestinationSpanOverflow             35 DestinationCapacityTooSmall
12 SourceDestinationOverlap             36 TooManyOutputRanges
13 ContractLayoutMismatch               37 UnionPageCountOverflow
14 InvalidRequirementState              38 UnionByteCountOverflow
15 InvalidPurpose                       39 TablePageCountOverflow
16 InvalidPhysicalStart                 40 TableByteCountOverflow
17 InvalidPhysicalEnd                   41 TableCapacityTooSmall
18 EmptyOrReversedRange                 42 InvalidPreconditions
19 MisalignedRange                      43 PageWalkCacheNotWriteBack
20 GeometryOverflow                     44 LeafEncodingRejected
21 GeometryMismatch                     45 OutputConstructionFailure
22 InvalidProtection                    46 AccountingMismatch
23 WritableExecutableRejected
```

Every emitted mapping uses 4 KiB PTE leaves. Page zero is forbidden both as a
requirement and as an emitted mapping, and every identity address must remain
below the exclusive low-canonical boundary
`0x0000_8000_0000_0000`. Requirements are sorted independently of caller
order. Overlapping or adjacent requirements are unioned only when their
protection and cache policies are identical; the planner never promotes an
overlap to broader permissions or a different cache type. Incompatible
overlap fails, while adjacent ranges with different policy remain distinct.
Normalized output bytes and aggregate accounting are deterministic for
equivalent input orderings. Diagnostic source indexes intentionally remain
relative to caller order. A range's `ContributingPurposeMask` summarizes every
source purpose that contributed any bytes to that normalized union; it does not
claim that every page in the range serves every recorded purpose.

For a fresh four-level hierarchy that uses only 4 KiB leaves, table-page
accounting is exact:

```text
PML4 pages = 1
PDPT pages = number of distinct (virtualAddress >> 39) regions
PD pages   = number of distinct (virtualAddress >> 30) regions
PT pages   = number of distinct (virtualAddress >> 21) regions
total      = 1 + PDPT pages + PD pages + PT pages
```

The supplied table-page capacity must cover that total before any destination
record is written. This is a fresh-hierarchy calculation, not a promise that a
later builder can reuse firmware tables or allocate those pages at any
particular address.

`RequiredDestinationCapacity`, union accounting, and table accounting remain
available when analysis reaches a destination- or table-capacity failure.
`OutputRangeCount` counts records actually emitted and therefore remains zero
on every failure, including those capacity failures.

The unsafe planning core is a bounded, no-allocation, multi-pass operation with
two phases: complete preflight followed by emission. Before its first
caller-data read it validates count/capacity relationships, actual POD layouts,
pointer alignment, full compiler-sized span arithmetic, and
source/destination disjointness. Preflight validates every requirement, all
required-purpose and guard rules, page-zero and canonical bounds, policy
conflicts, exact union count, exact table-page requirements, and a separate
output reconstruction without writing. Only after every fallible preflight
step succeeds does a write-only pass emit the deterministically ordered plan.
A malformed final source record or either capacity failure leaves the full
destination byte-identical; callers must keep all unsafe spans mapped,
exclusively owned, and stable for the entire call.

The planner consumes the exact M01-011
`X64PageTableEncodingPreconditions`/`X64PatMappingToken` context and validates
WB at raw IA32_PAT index zero for paging-structure walks. It does not create a
second level, protection, cache, or PAT vocabulary. Its output policies are
checked through the M01-011 4 KiB leaf codec, so W+X, missing NXE/WP,
five-level paging, invalid physical width, malformed PAT state, unsupported
cache selection, and noncanonical physical inputs fail instead of being
normalized.

The retained SharpOS sources are owner-authorized architectural and test
influences, not imported live behavior or CLROS evidence:

- `references/SharpOS-main/OS/src/Kernel/Paging/PageFlags.cs`, SHA-256
  `DC565A4B8BFA38CDACFBDB5B53B2D5616BB9C1057DEDE7A1673956D6F3198652`,
  was used to cross-check x64 flag semantics while CLROS independently keeps
  page zero absent and rejects writable-executable leaves.
- `references/SharpOS-main/OS/src/Kernel/Paging/X64PageTable.cs`, SHA-256
  `35503B543628E5A462073D498704050EEEF7D79DEB2B84F01CACA29714200BAC`,
  influenced hierarchy and table-sharing analysis. CLROS does not adopt its
  live table cloning, physical allocation, direct pointer writes, permissive
  user/global mappings, large-page mutation, CR3/TLB operations, or protection
  loosening.
- `references/SharpOS-main/OS/src/Kernel/Paging/PagingValidation.cs`, SHA-256
  `B1699D328E49D5FC68B8CF8979B366D72CCA2E3B4AA5B982F91F0A2C9A1C40F6`,
  supplies test-only missing/duplicate/neighbor/conflict categories that CLROS
  recreates independently over synthetic caller-owned values.

The canonical upstream is `https://github.com/daniilvaino/SharpOS`.
Owner-supplied permission authorizes reuse of SharpOS-authored source and the
current upstream independently corroborates CC0-1.0 at license-evidence commit
`b80ee3a4175e1522d1a12ba7fe371a49da3207f5`. That commit is not asserted to be
the local snapshot revision: the supplied snapshot has no recoverable
revision, so the exact local paths and hashes above remain the source identity.
File-local third-party terms still remain separate.

Successful host evidence uses:

```text
TEST_START m01.012
CLROS_M01_012_READY
TEST_PASS m01.012
```

The self-registering host suite contains exactly fourteen ordered groups:

| Group | Stable test name |
|---:|---|
| 1 | `m01.012 01 type values layouts and defaults are stable` |
| 2 | `m01.012 02 required roles and the guard are complete` |
| 3 | `m01.012 03 realistic plans are exact and deterministic` |
| 4 | `m01.012 04 compatible ranges merge with exact union accounting` |
| 5 | `m01.012 05 policy conflicts report deterministic source indexes` |
| 6 | `m01.012 06 protection cache and PAT policy fail closed` |
| 7 | `m01.012 07 page zero and guard overlap are excluded` |
| 8 | `m01.012 08 address geometry and physical widths are bounded` |
| 9 | `m01.012 09 four-level table-page accounting is exact` |
| 10 | `m01.012 10 pointer count capacity and aliasing are bounded` |
| 11 | `m01.012 11 hostile POD bytes and failure atomicity hold` |
| 12 | `m01.012 12 source and nostdlib efi firewalls hold` |
| 13 | `m01.012 13 current evaluated efi closures exclude the planner` |
| 14 | `m01.012 14 marker ownership and ordering are stable` |

Any aggregate failure emits `TEST_FAIL m01.012 <reason>` and prevents the
ready/pass pair. Group 12 compiles the exact production source in the retained
`NoStdLib=true`, `NOSTDLIB_EFI`, zero-framework-reference fixture. Group 13
evaluates both `SingleMinimalApi` and `MultiMinimalApi` project closures and
proves the source and its host test are excluded from the current EFI build.

This is host-only plan evidence. It does not discover or own live boot ranges,
allocate or zero table pages, assign physical table addresses, populate a
table image, protect a real guard, map page zero or prove its hardware absence,
perform a live RW-to-RX transition, load CR3, invalidate a TLB, run under EFI
or QEMU/OVMF, or advance any completion gate. M01-019 owns the first
coordinated reversible paging phase, M01-020 owns explicit EFI source linking,
M01-025 owns default boot wiring, and M01-026 owns cumulative QEMU evidence.

### Reversible CR3 probe plan contract (M01-013)

M01-013 adds three no-stdlib-compatible, caller-owned boundaries for a future
one-shot paging probe:

1. `X64PagingModePreflightStubPlan` materializes and validates an exact
   read-only processor-observation stub and evaluates its POD.
2. `X64ReversibleCr3TransitionStubPlan` materializes and validates an exact
   callback-free recovery stub and evaluates a caller-supplied execution
   transcript. Host code never invokes the bytes.
3. `X64Cr3LoadStubPlan.Create` binds those exact sibling results to the M01-012
   identity map, the M01-005 owned stack, explicit firmware allocation records,
   state/canary storage, and post-restoration marker order.

The composite policy is off by default and accepts only an explicit one-shot
opt-in with a nonzero timeout. It requires exactly two physical allocation
records:

- one 4 KiB `EfiLoaderCode` page containing nonoverlapping, 16-byte-aligned
  preflight and transition byte ranges;
- one disjoint `EfiLoaderData` arena containing exactly the table-page capacity
  reported by M01-012 followed by one 4 KiB state page.

The first table page is the owned PML4 root. The final data page begins with
the 184-byte transition state and contains a later disjoint aligned 8-byte
canary. The actual M01-013 host fixture places the 111-byte preflight stream at
code-page offset 0 and the transition stream at offset 256, but the production
composite accepts other aligned nonoverlapping offsets that fit completely
inside the single code page. Materialization and validation results are bound
to the exact virtual entry, declared byte count, audited instruction count,
and remaining code-page capacity; a success POD from another span cannot be
rebound.

The probe LoaderCode and LoaderData allocations must also be pairwise disjoint
from both allocations in the M01-005 stack-switch plan. The modeled transition
state address is carried in the 216-byte transition-evidence POD, must equal the
composite storage layout's exact state address, and is independently
revalidated by the 1,016-byte composite plan and 1,072-byte result. A copied
success result cannot be rebound to another otherwise valid state page.

The M01-012 ranges supplied to the composite must reproduce their successful
result exactly and collectively provide:

- RX+WB identity coverage carrying `AdditionalRequired` for the LoaderCode
  page;
- RW+NX+WB identity coverage carrying `AdditionalRequired` for the complete
  LoaderData table/state arena;
- writable, nonexecutable WB identity coverage carrying `OwnedStack` for the
  complete usable stack.

No accepted range may be writable and executable. The composite also retains
the required M01-012 executing-image, handoff, serial, ACPI, framebuffer,
and non-emitted guard evidence. It rejects missing, stale, reordered,
overlapping, aliased, misaligned, overflowing, capacity-rebound, or
accounting-inconsistent caller storage.

The preflight stream is exactly 111 bytes and 32 decoded instructions with
SHA-256:

```text
71993B5134F7A4936718FD406EBF68CDA7E82476EE15B3F9824A5B6600FF9171
```

Its Windows-x64 entry receives one aligned caller-owned 56-byte observation
record. Before any flag-changing instruction, flag-preserving MOVs clear the
completion/reserved fields and PUSHFQ records entry RFLAGS. The stream then
records raw CR0, raw CR3, raw CR4, IA32_EFER, CPUID leaf `0x80000000`, and
CPUID leaf `0x80000008`; the `PLF1` completion signature is its final
observation write. It writes no control register and calls no function.

The host evaluator accepts only:

- CR0.PG and CR0.WP set;
- CR4.PAE set, with CR4.LA57 and CR4.PCIDE clear;
- IA32_EFER.LME, LMA, and NXE set;
- CPUID address-size leaf present and physical width from 36 through 52;
- architectural RFLAGS bit 1 set;
- raw firmware CR3 with only page-aligned address bits plus PWT/PCD low bits;
- a nonzero 4 KiB-aligned owned root inside a nonempty, aligned table arena,
  with the complete root page inside that arena and every address inside the
  reported physical width.

Default, partial, conflicting-completion, missing-bit, unsupported-width,
invalid-low-bit, above-width, null, unaligned, reversed, overflowing, or
out-of-arena observations fail without becoming successful evidence.

The transition stream is exactly 1,981 bytes and 485 decoded instructions with
SHA-256:

```text
F1D94771DE8C8436CCDA1358A15F51DDE683AC3C77CBB0DC4A85B084888CA484
```

It contains no external relocation or patch, call, indirect control transfer,
STI, nonvolatile GPR touch, or XMM6-15 touch. It uses only volatile
RAX/RCX/RDX/R8/R9/R10/R11 plus balanced eight-byte PUSH/POP pairs, preserves
the caller shadow space, requires Win64 entry alignment (`RSP % 16 == 8`), and
returns the append-only runtime status in EAX.

The exact modeled execution order is:

1. PUSHFQ/POP R8 captures caller-entry flags before any TEST/CMP. The stub then
   validates the aligned 184-byte state span, reserved bytes, zero callback
   count, every zeroed output, uninitialized status/checkpoint, owned-root
   geometry, aligned stack range, nonzero distinct canary values, entry RSP,
   and one-shot state. State, canary, and stack spans must be overflow-safe,
   low-canonical, and mutually disjoint; the canary follows the state record.
   No runtime-state byte is written until this preflight succeeds. A null,
   malformed, or overlapping pre-CLI path restores the captured RFLAGS before
   returning its status.
2. The captured entry flags are stored, CLI executes once, and an immediate
   PUSHFQ readback must have IF clear and architectural bit 1 set. Arithmetic
   and status flags may legitimately differ because preflight TEST/CMP
   instructions ran after entry capture.
3. Raw CR3 and raw CR4 are read without normalizing their values. LA57 or PCIDE
   returns `NotSupported` through the recovery tail before any CR3 write. The
   masked firmware root must differ from the owned root, preventing a no-op
   transition from reporting success.
4. CR4.PGE is cleared, written, and read back. The owned aligned CR3 is then
   written and read back while PGE is clear.
5. Through the owned root, the identity-mapped canary must contain the expected
   value, is replaced, and must read back as the replacement.
6. A shared recovery epilogue writes and reads back the exact raw firmware CR3
   while PGE remains clear. Only after a matching firmware-root readback does
   it read the replacement canary again through the restored root.
7. The exact raw CR4 is restored and read back, followed by POPFQ and an
   immediate full-RFLAGS readback that must exactly equal the entry capture.
   Firmware-CR3, CR4, and RFLAGS restoration
   failures override an earlier operational status; a post-restoration canary
   mismatch replaces only an otherwise-successful status, retaining the first
   useful operational failure.
8. The stub records exit RSP, requires exact entry/exit equality, writes the
   final runtime status/checkpoint, restores the captured flags a final time,
   moves the retained status to EAX with a flag-preserving MOV, and returns.

Every control-flow path after the owned CR3 write crosses the firmware-CR3
restore; every path after the PGE write crosses the CR4 restore; and all 51
reachable returns restore the entry RFLAGS, with only a flag-neutral status MOV
permitted after the final POPFQ. The emitted code never invokes a firmware
callback. The composite additionally requires actual transition RSP
to retain 64 bytes below it, the 40-byte return-address/shadow-space caller
frame above it, and all transcript RSP values inside the M01-005 usable stack.

The caller-owned transcript uses exactly nine successful events:

```text
PreMarkerEmitted
TransitionEntered
OwnedRootLoaded
OwnedRootReadBack
CanaryUpdated
FirmwareRootRestored
FirmwareRootReadBack
CanaryValidated
PostMarkerEmitted
```

`M01_POST_CR3` is accepted only after exact raw firmware-root readback and
canary validation. Callback attempts, interrupt re-enable, early/missing
markers, raw-root mismatch, RSP escape, unchanged/corrupt canary, timeout,
reset, triple fault, extra/reordered events, elapsed-time regression, or
timeout overflow fail deterministically.

The stable aggregate host evidence is:

```text
TEST_START m01.013
TEST_START m01.013.01.load
TEST_PASS m01.013.01.load
TEST_START m01.013.02.preflight
TEST_PASS m01.013.02.preflight
TEST_START m01.013.03.transition
TEST_PASS m01.013.03.transition
CLROS_M01_013_READY
TEST_PASS m01.013
```

Any child failure emits its child `TEST_FAIL` plus
`TEST_FAIL m01.013 failures=<count>`, throws from the sole aggregate
coordinator, and suppresses both the ready marker and aggregate pass marker.
The exact union of address, mapping, stack, preflight, transition, and
composite sources compiles in the retained `NoStdLib=true`, `NOSTDLIB_EFI`,
zero-framework-reference fixture with zero assembly references. Evaluated
`SingleMinimalApi` and `MultiMinimalApi` project closures prove the new
production and test sources are absent from the current EFI build.

The retained SharpOS files are owner-authorized architectural/test influences,
not imported live behavior or CLROS execution evidence:

- `references/SharpOS-main/OS/src/Kernel/Paging/Cr3Accessor.cs`, SHA-256
  `088D73C1BF807B652CD2F7ED795C1507C1F28884DD4184A2A28A574CF01E9E47`,
  was used to cross-check the minimal CR3 opcode and the requirement to retain
  the exact raw register value rather than reconstructing it from an address.
- `references/SharpOS-main/bootasm/BootAsm.Generator/BootAsmGenerator.cs`,
  SHA-256
  `B24C79DCD38757DCBE243DC6C4C63C1AF628525F06C4F5D296B6AFB0C9387500`,
  influenced deterministic generate/materialize/readback auditing. CLROS uses
  independently authored first-party bytes and its own bounded encoder.
- `references/SharpOS-main/OS/src/Kernel/Paging/PagingValidation.cs`, SHA-256
  `B1699D328E49D5FC68B8CF8979B366D72CCA2E3B4AA5B982F91F0A2C9A1C40F6`,
  supplied test-only missing-map, alignment, and readback failure categories.

The canonical upstream is `https://github.com/daniilvaino/SharpOS`.
Owner-supplied permission authorizes reuse of SharpOS-authored source and the
upstream independently corroborates CC0-1.0 at evidence commit
`b80ee3a4175e1522d1a12ba7fe371a49da3207f5`; the supplied snapshot itself has
no recoverable revision, so the exact local paths and hashes remain its source
identity. No file-local third-party term is overridden.

This issue proves host-audited, default-off planning and byte identity only.
Neither byte stream is invoked by host tests, source-linked into `src/OS`, or
executed by EFI/QEMU. It does not allocate/zero/populate page tables, change a
live protection, prove page zero absent in hardware, load CR3, execute under an
owned root, prove a TLB effect, establish firmware reachability of recovery
state, or advance a completion gate. PCID and LA57 remain unsupported. NMI/SMI
behavior remains an explicit residual risk until M02 owns descriptors and
fault diagnostics. M01-019 owns coordinated live preparation/invocation,
M01-020 owns EFI linking, M01-025 owns default wiring, and M01-026 owns
cumulative real-QEMU proof.

### Caller-owned protection transition contract (M01-014)

M01-014 adds four no-stdlib-compatible, host-audited boundaries for one future
4 KiB RW+NX-to-RX transition:

1. `X64PageMap.Apply` performs one transactional leaf mutation in an existing
   caller-owned four-level table image.
2. `X64TlbInvalidationPlan` describes and materializes the exact page-aligned
   virtual targets invalidated by a successful mutation.
3. `X64InvlpgStubPlan` materializes and reads back a checked
   `invlpg [rcx]; ret` stream and creates a one-shot RX-only invocation plan.
4. `X64ProtectionProbeStubPlan` materializes and reads back a constant-return
   stream and audits its complete modeled protection transcript.

The page-map core receives a flat array of raw 64-bit entries and a separate
array of `X64PageTableImagePage` records that bind each 512-entry slice to one
typed physical table-page address. It validates the complete declared
pointer/count/capacity spans, their alignment and non-overlap, record order,
physical uniqueness, exact entry cardinality, root membership, and every child
table pointer encountered by the PML4/PDPT/PD/PT walk. The caller must keep all
declared storage mapped, stable, exclusively serialized, and owned for the
whole call; arithmetic validation cannot establish those unsafe properties.

Each request targets one nonzero, 4 KiB-aligned, low-canonical virtual page and
uses the exact M01-011 `PageProtection`, `PageCachePolicy`, and
`X64PageTableEncodingPreconditions` contracts. The supported operations are:

| Operation | Required prior leaf | Result |
| --- | --- | --- |
| `Map` | Unmapped | One new 4 KiB leaf with the requested typed physical page, R/RW/RX protection, and WB/UC/WC policy |
| `Unmap` | Mapped 4 KiB leaf | One zero leaf and one exact invalidation range |
| `Protect` | Mapped 4 KiB leaf | Same physical page and cache policy with protection rewritten through the M01-011 preserve-or-clear A/D policy |

All request, storage, table-walk, essential-range, encoding, and invalidation
conditions succeed before the sole 64-bit leaf store. Therefore a failed
operation changes no table byte. A successful operation changes exactly one
leaf, retains the validated page-image set's typed first-to-last physical
envelope, and returns one initialized `X64TlbInvalidationRange` for the exact
target page. Materialization into `X64TlbInvalidationRecord` is separately
capacity-checked and emits exactly one record per page in ordinal order.

This first mutation slice intentionally requires all intermediate tables to
exist. It returns a distinct status for a missing table, an out-of-image child
pointer, a malformed entry, or a 1 GiB/2 MiB large-page collision. It does not
allocate an intermediate table or split a large mapping. Duplicate map,
double/unmapped unmap or protect, unchanged protection, page zero,
unaligned/noncanonical/overflowing input, W+X, malformed or aliased storage,
and mutation of a caller-declared essential mapping all fail before the store.

The audited invlpg stream is four bytes and two instructions:

```text
0F 01 39 C3
```

Its SHA-256 identity is:

```text
FEF29CD9A7B4B2A9C53BC69C7893BC37FFFACECF2093CD943C5F43139C2CF53F
```

The stream uses the Windows-x64 first integer argument register RCX as the
target address, touches no nonvolatile register, and returns directly. Its
entry must be 16-byte aligned and wholly inside one modeled 4 KiB page;
the aggregate audit separately requires that modeled page to correspond
exactly to its caller-supplied one-page `LoaderCode` allocation. Emission and
readback bind the exact modeled page address, entry offset, capacity, bytes,
and instruction count. An invocation plan succeeds only when the readback was
verified, the code page is RX and non-writable, and exactly one initialized
invalidation record matches both the requested target and the expected entry.
The plan reports `PerformsLiveInvalidation=false`, `ReloadsCr3=false`, and
`InvokesStub=false`.

The separate protection probe is six bytes and two instructions:

```text
B8 4B 4F 53 4F C3
```

This is `mov eax, 0x4F534F4B; ret`; the fixed return value spells the little
endian evidence word `KOSO` in memory order and has SHA-256:

```text
BBA15FCF0EEE9F4112C1ABB203EB833D44A09E9BCEDB29456ACAE9B466EBA374
```

The probe must occupy a separate, identity-mapped, one-page `LoaderCode`
allocation with an exact canonical `BootReservedRange` of kind
`PageProtectionProbe`. The invlpg/transition-code allocation is another
one-page `LoaderCode` record, while the page-table/state arena is a disjoint
nonempty `LoaderData` record. All three allocations must be pairwise
nonoverlapping. The table/state record's physical range must contain the
complete page-image physical envelope retained by the successful mutation;
an otherwise-valid unrelated or truncated `LoaderData` record is rejected.
The aggregate accepts only:

- verified probe and invlpg byte readbacks bound to their allocation starts;
- one successful `Protect` result whose previous leaf is RW+NX+WB and whose
  replacement is RX+WB over the same physical probe page;
- exact replacement-leaf readback;
- one exact target-only invalidation record and matching RX-only invlpg
  invocation plan;
- one modeled probe invocation at the protected entry, the expected return,
  and one unchanged nonzero caller canary; and
- exactly nine zero-based events in this order:

```text
StubMaterialized
StubReadBackVerified
InitialReadWriteNoExecuteVerified
ProtectionChangedReadExecute
LeafReadBackVerified
TargetInvalidationPlanned
InvlpgInvocationPlanned
ProbeInvocationPlanned
ConstantReturnAndCanaryObserved
```

A writable execution stub, executable table/state data, overlapping,
unrelated, truncated, or mis-typed allocation, wrong reservation,
execute-before-protect ordering,
wrong target/opcode/entry/readback, multiple invocation, unexpected return,
changed/zero canary, missing/reordered/extra event, malformed/truncated or
oversized buffer, and default POD all fail closed. The observation exposes
`IsLiveEvidence=false`, and the aggregate carries no field that can promote
modeled host data to guest evidence.

The stable aggregate host evidence is:

```text
TEST_START m01.014
TEST_START m01.014.01.map
TEST_PASS m01.014.01.map
TEST_START m01.014.02.invlpg
TEST_PASS m01.014.02.invlpg
TEST_START m01.014.03.protection
TEST_PASS m01.014.03.protection
CLROS_M01_014_READY
TEST_PASS m01.014
```

Any child failure emits its child `TEST_FAIL` plus
`TEST_FAIL m01.014 failures=<count>`, throws from the sole aggregate
coordinator, and suppresses the ready/pass pair. The exact production-source
union compiles in the retained `NoStdLib=true`, `NOSTDLIB_EFI`,
zero-framework-reference fixture with zero assembly references.

The retained SharpOS paths are owner-authorized architectural or test
influences, not imported live behavior or CLROS execution evidence:

- `references/SharpOS-main/OS/src/Kernel/Paging/X64PageTable.cs`, SHA-256
  `35503B543628E5A462073D498704050EEEF7D79DEB2B84F01CACA29714200BAC`,
  supplied the four-level index/walk cross-check and large-page collision
  category. CLROS rejects its global state, blind physical-pointer casts,
  partial mutations, permissive protection handling, and CR3-wide flushing.
- `references/SharpOS-main/OS/src/Kernel/Paging/PageFlags.cs`, SHA-256
  `DC565A4B8BFA38CDACFBDB5B53B2D5616BB9C1057DEDE7A1673956D6F3198652`,
  was a protection-bit cross-check; CLROS instead consumes the strict M01-011
  R/RW/RX and PAT-bound cache vocabulary and rejects every W+X state.
- `references/SharpOS-main/OS/src/Kernel/Paging/PagingValidation.cs`, SHA-256
  `B1699D328E49D5FC68B8CF8979B366D72CCA2E3B4AA5B982F91F0A2C9A1C40F6`,
  supplied test-only duplicate-map, double-unmap, missing-table, and
  large-page scenarios that were independently recreated against the CLROS
  contract.

The canonical upstream is `https://github.com/daniilvaino/SharpOS`.
Owner-supplied permission authorizes reuse of SharpOS-authored source and the
upstream independently corroborates CC0-1.0 at evidence commit
`b80ee3a4175e1522d1a12ba7fe371a49da3207f5`; the supplied snapshot itself has
no recoverable revision, so the exact local paths and hashes remain its source
identity. No file-local third-party term is overridden.

This issue proves only host mutation of caller-owned table bytes and audited
byte/transcript identity. It does not allocate or populate live table pages,
perform a processor-owned PTE store, execute either stream, reload CR3,
invalidate a CPU TLB, prove effective parent permissions or page-zero absence,
provide SMP shootdown, link into the canonical EFI artifact, run under
QEMU/OVMF, or advance a completion gate. M01-019 owns coordinated live
preparation/invocation, M01-020 owns explicit EFI source linking, M01-025 owns
default boot wiring, and M01-026 owns cumulative real-QEMU evidence.

### Page-zero absence and disabled fault-probe contract (M01-015)

`PageZeroMappingPolicy` separates two claims that must not be conflated:

1. `ValidateMappingIntent` validates one raw mapping-boundary interval. The raw
   start is intentional because the typed `VirtualAddress` contract correctly
   rejects zero. A present range that intersects page zero is rejected before
   ordinary alignment classification; zero length, wraparound through zero,
   misalignment, and low-canonical overflow also fail. A valid non-present
   page-zero intent is policy-valid, but it does not prove any table state.
2. `AuditTableImage` performs a read-only index-zero walk over caller-owned
   `X64PageTableImagePage` and raw-entry arrays. It validates exact cardinality,
   full declared pointer spans, alignment, non-overlap, page-record order,
   physical uniqueness, root membership, M01-011 preconditions, and each child
   table before reading the next entry. Canonical raw zero at PML4E, PDPTE,
   PDE, or PTE proves absence only in that supplied image. A nonzero
   present-clear value is malformed; a present 1 GiB, 2 MiB, or 4 KiB leaf
   covering zero is rejected, including raw present encodings whose physical
   base is zero.

The audit writes no caller byte. Its constants state
`PerformsLiveMutation=false`, `WritesTableImage=false`,
`ReadsLivePageTables=false`, and `ProvesInstalledRoot=false`. The caller still
must ensure every declared pointer remains mapped, stable, exclusively owned,
and immutable for the call; the contract cannot establish those properties.

`NullPageFaultProbePlan` consumes only a successful table-image audit, never a
successful range intent alone. A ready-disabled result additionally requires
caller-attested owned-diagnostics and page-fault-handler prerequisites, keeps
the probe disabled, and fixes the future architectural intent as:

```text
access=supervisor read
vector=14
hasHardwareErrorCode=true
checkedErrorCodeMask=0x1f
expectedErrorCode=0
expectedCR2=0
```

The zero error-code intent means non-present, read, supervisor, no reserved-bit
violation, and not an instruction fetch for the checked architectural bits. It
is not an observed CPU value. Default-on, requested-execution, and
attempted-execution inputs are rejected even when the caller attests that
diagnostics are ready. The plan has no byte emitter, pointer target, delegate,
or invocation API and states `PerformsLiveFault=false`,
`DereferencesPageZero=false`, `ExecutesProbe=false`, and
`IsGuestEvidence=false`.

The stable host-only aggregate evidence is:

```text
TEST_START m01.015
CLROS_M01_015_READY
TEST_PASS m01.015
```

Any case failure emits `M01_015_CASE_FAIL <case>` and
`TEST_FAIL m01.015 failures=<count>`, throws, and suppresses the ready/pass
pair. The exact production union compiles under `NOSTDLIB_EFI`,
`NoStdLib=true`, without framework or assembly references. Evaluated
`SingleMinimalApi` and `MultiMinimalApi` EFI compile closures exclude both
production files and the test. No EFI binary inspection or execution occurred.

The SharpOS `PageFlags.cs` snapshot (SHA-256
`DC565A4B8BFA38CDACFBDB5B53B2D5616BB9C1057DEDE7A1673956D6F3198652`)
was used only to cross-check Present/NX bit meaning.
`PagingValidation.cs` (SHA-256
`B1699D328E49D5FC68B8CF8979B366D72CCA2E3B4AA5B982F91F0A2C9A1C40F6`)
contributed missing-map and malformed-entry test categories. CLROS does not
import its live allocator, global pager, panic/log path, raw-`ulong`
architecture, or skip-on-no-free-slot behavior.

This is not live page-zero proof. The supplied image may be stale or unrelated
to CR3, and no null read, page fault, CR2/error-code capture, handler dispatch,
panic report, EFI link, QEMU run, or completion-gate transition occurs.
M01-026 owns cumulative QEMU proof that page zero is absent during the
owned-root interval. M02-014 owns the separately disabled actual null-read
`#PF` probe after owned IDT and diagnostics exist. `Probes.cs`,
`BootSequence.cs`, `EfiEntry.cs`, and `completion-gates.md` are unchanged.

### Reversible owned-CR3 stage-three coordinator contract (M01-019)

M01-019 adds the third host-tested M01 coordinator and fixed marker plan. It
accepts only a complete M01-018 static state and binds three disjoint
caller-attested allocation records: one `EfiLoaderCode` CR3/invlpg page, one
distinct `EfiLoaderCode` protection-probe page represented by
`BootReservationKind.PageProtectionProbe`, and one `EfiLoaderData`
page-table/state arena. It composes the reviewed M01-011 through M01-015 value,
table-image, and byte-plan results without invoking privileged bytes.
The coordinator accepts one identity-map requirement per loaded-image page
plus ten non-image requirements and cross-binds every range instead of trusting
its caller-supplied purpose label. The image requirements must form an exact
one-page, gap-free, duplicate-free tiling of the loaded-image reservation; only
the page containing the continuation is RX, and every other current image page
is read-only/NX. The boot-handoff range is the exact page-aligned envelope of
the handoff POD and must remain contained by that reservation with the same
policy as its overlapping image tile. Stack, guard, stage-one stub, ACPI
scratch, framebuffer metadata, CR3/invlpg code, protection-probe, complete
table/state arena, and stage-two allocator arena must each match their
corresponding ledger reservation and exact protection.
The ACPI-scratch and framebuffer bindings prove only reservation-ledger
coherence at this boundary, not firmware discovery or copied-table provenance.
Every page of the table/state allocation is audited RW+NX in the constructed
image, not merely its root page.

The existing identity-map planner still requires its historical `SerialStub`
purpose even though owned port I/O and COM1 belong to M02. M01-019 binds that
slot to the already reviewed RX `BootStackSwitchStub` reservation only so an
arbitrary executable range cannot enter the model. It is a compatibility
binding, not evidence of a serial stub or owned diagnostics;
`ProvesOwnedSerialDiagnostics` remains `false`, and a dedicated follow-up must
replace the alias before any live owned-CR3 issue can close. These are
role-based page permissions, not PE-section permissions;
`EnforcesPerSectionLoadedImageProtections` remains `false`.

The fixed stage-three success order is:

```text
TEST_START m01.stage3
M01_CR3_STUB_PAGE_ALLOCATED
M01_PROTECTION_PROBE_PAGE_ALLOCATED
M01_PAGE_TABLE_STATE_ARENA_ALLOCATED
M01_STAGE3_MAP_RECAPTURED
M01_CR3_STUB_MATERIALIZED
M01_PAGE_TABLES_BUILT
M01_PAGING_MODE_VALIDATED
M01_PRE_CR3
M01_FIRMWARE_CR3_RESTORED
M01_RFLAGS_RESTORED
M01_PGE_CYCLE_VERIFIED
M01_OWNED_CR3_CANARY_VERIFIED
M01_POST_CR3
M01_RW_TO_RX_VERIFIED
M01_TLB_INVALIDATION_COMPLETE
M01_RX_PROBE_EXECUTED
M01_PAGE_ZERO_ABSENT
TEST_PASS m01.stage3
CLROS_M01_LIVE_BOOT_MEMORY_READY
```

`BootSequence.RunM01LivePagingIntegration` is a retrospective host presenter.
It emits the fixed success plan only after the complete supplied result is
final and successful. A failed or non-final result emits only the stage-three
start plus
`TEST_FAIL m01.stage3 status=<status> checkpoint=<checkpoint>` and
`M01_STAGE3_FAIL status=<status> checkpoint=<checkpoint>`. It suppresses every
later restoration, canary, post, protection, TLB, probe, page-zero, pass, and
cumulative-ready line. A non-final result whose status is `Success` is
reclassified as `IllegalCoordinatorState`. The ordinary
`BootSequence.Run` path and its five non-smoke/eight total lines remain
byte-for-byte unchanged.

No line is emitted while owned CR3 is modeled active.
`M01_OWNED_CR3_ENTERED` remains caller-owned POD state rather than serial
output. The protection, invalidation, constant-return-probe, and page-zero
lines occur after `M01_POST_CR3` because they retrospectively report separate,
mutually cross-checked host models intended for a future composite interval.
They are not temporal evidence that the current reversible-transition bytes
contained those operations, nor should their presentation order be
interpreted as those operations occurring after the firmware root was
restored. `ProvesCompositeOwnedCr3MachineCodeInterval` is explicitly `false`.
The modeled probe leaf is restored to its initial RW+NX value after the
RW+NX-to-RO+X audit, including on a rejected aggregate. The rest of the
caller-owned output image is the newly constructed table model, so
`RestoresCallerOwnedInitialTableImage` is deliberately `false` while
`RestoresModeledProbeLeafToInitialProtection` is `true`.

The aggregate host-harness markers are:

```text
TEST_START m01.019
CLROS_M01_019_READY
TEST_PASS m01.019
```

This is host evidence over caller-supplied allocations, map/ledger
generations, table bytes, register observations, and execution transcripts. It
does not call EFI `AllocatePages`, populate processor-owned tables, execute
preflight/CR3/invlpg/probe bytes, change CR3 or page protections, perform a CPU
TLB invalidation, prove page zero absent in hardware, wire `EfiEntry`, link the
coordinator into `src/OS`, run QEMU/OVMF, or advance a completion gate.
PCID/LA57 remain unsupported, NMI/SMI remains an M02 residual risk, and null
dereference/#PF evidence remains M02-014. M01-020 owns explicit EFI source
linkage, M01-025 owns live allocation and `EfiEntry` wiring, and M01-026 owns
cumulative real-QEMU evidence. Until M01-026, even
`CLROS_M01_LIVE_BOOT_MEMORY_READY` is a reserved host-tested contract line,
not live boot-memory evidence.

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

The M01-017 stage-one success plan and coordinator contract are host-tested
only. The source-linked `BootSequencePlan` bytes are not consumed by
`EfiEntry`; `BootSequence.cs` remains excluded from the explicit EFI project.
No live LoaderCode or LoaderData allocation, refreshed memory map, canonical
guest reservation ledger, switch-stub execution, stack transfer, `RSP`
observation, guard-page absence, protection transition, or QEMU marker evidence
has been established.

The M01-011 page-table entry contract is host-tested only. Its synthetic
no-stdlib compiler fixture proves source compatibility while the evaluated EFI
closure proves the same source is not yet linked. The precondition and PAT
tokens are caller observations, not live register evidence; the codec has no
table storage, virtual-address walk, page ownership, live read/write, CR3/TLB
effect, page-fault path, or QEMU execution. Its canonical table pointers are
permissive ancestor masks for future mixed leaf policy, not proof that a
complete hierarchy has effective W^X/NX enforcement.

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

`BootHandoffInfo`, `MemoryMapInfo`, `BootMemoryRegionPlanner`, `BootReservationSubtractor`, `BootPageAllocatorPlanner`, `BootPageFreeListBuilder`, `BootPageAllocatorStateInitializer`, `BootPageAllocatorStateMaterializer`, `BootPageAllocatorMutation`, `BootPageAllocatorReadinessComposer`, `NativeAotHeapBootstrapPlanner`, `NativeAotClassConstructorRunnerPreflight`, `NativeAotClassConstructorWorkItemPlanner`, `BootSequence.RunBootPageAllocatorReadiness`, `BootSequence.RunNativeAotHeapBootstrapReadiness`, `BootSequence.RunNativeAotClassConstructorRunnerPreflightReadiness`, and `BootSequence.RunNativeAotClassConstructorWorkItemReadiness` are host-tested contracts. The live no-stdlib EFI artifact now populates and validates `BootHandoffRecord` with owned memory-map metadata and ACPI RSDP pointer provenance, normalizes a compact live summary, accepts that summary as a boot precondition, materializes compact `BootMemoryRegionRecord` entries, validates conventional-memory records into a separate static free-list metadata chain, and emits `BOOT_PAGE_ALLOCATOR_READY` with live conventional/free node/page/byte counts. It still does not source-link or invoke the M01-006 allocator-state materializer, M01-007 reservation subtractor, M01-008 one-page allocation mutator, or M01-009 receipt-bound exact-inverse free, run the host-friendly allocator-facing planners, prove that the typed source ranges are explicitly firmware-allocated CLROS arenas, apply canonical reservations to the live ranges, install a stable/exclusive authoritative allocator state, reserve/zero/map/access the reported page, run the allocator-readiness composer or cctor work-item planner in `EfiMain`, reserve kernel-owned ranges, expose live page allocation/free APIs, support arbitrary-order frees or ABA-safe allocation authority, reserve managed heap pages, configure paging, enforce NX/W^X, allocate pages, emit `NATIVEAOT_HEAP_BOOTSTRAP_READY`, `NATIVEAOT_CCTOR_RUNNER_PREFLIGHT_READY`, or `NATIVEAOT_CCTOR_WORK_ITEMS_READY` in QEMU, install a managed heap, or invoke class constructors.

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
