# Boot

## Current Status

The M04-020 metadata diagnostic is accepted under #574 as a default-off
child of original TypeManager registration. It retains that registration's
image, storage and handle, uses the original M01 owned stack, and routes lookups
through `BootSequence.RunX64UnwindMetadata`. The original full host gate and
fresh default, TypeManager and 67-record unwind profiles pass at `4625fd7`;
[step1071](../done/step1071.md) binds current native, stack, serial and disk evidence. It
neither walks a stack nor executes handlers and does not cross ExitBootServices.

M03-009 extends the owned post-exit writer with five records ending in
`TEST_PASS m03.009`. Its host checks cover partial writes, the final CRLF,
canary failures and consumed reentry. Integration #572 now has accepted current
native/QEMU evidence at `d5bef46`: first-exit and revocation emit 66 and 78 complete
application records through the original owned COM1 backend. The full host gate,
actual writer/stack review and fresh disk/lifecycle association pass in
[step1063](../done/step1063.md). Development remains in step1054; the older
captures below retain their original source boundary. Continuing post-firmware
allocator/platform work remains #67.

M03-008 adds a default-off typed firmware-capability revocation profile under
integration issue #570. It binds console, memory-map, filesystem, timer, RNG,
configuration-table, physical-memory and framebuffer handles to the original
retained first-exit arena. Every copied handle checks that authority before
examining or entering its backend. The original full host gate and fresh
73-record QEMU/OVMF capture are accepted at `2b5a4b0`, with independent native,
source, lifecycle and disk association in [step1053](../done/step1053.md).
Development history remains in [step1052](../done/step1052.md). Optional firmware-provider functionality and
firmware-page reclamation remain outside this change.

Boot has a firmware-visible QEMU/OVMF smoke proof. `src/OS` publishes a
no-stdlib NativeAOT EFI application, `tools/OS.ImageBuilder` wraps it in a raw
GPT/FAT32 EFI System Partition image, and `scripts/run-qemu-smoke.ps1` boots
that image in QEMU/OVMF and captures serial markers. A separate
`M01_LOADED_IMAGE_STACK_PROBE` build now has source-bound opt-in QEMU proof that
the M01-021 adapter captures the live loaded-image range, allocates and
validates the stack-switch resources, refreshes the map, validates its
four-record reservation ledger, transfers to the owned stack, observes a
contained live `RSP`, and then emits the unchanged default boot plan. The
nested `M01_LIVE_ALLOCATOR_PROBE` profile also has a separately source-bound
M01-024 QEMU tuple: after the proven stage-one transition it observes one
successful two-page `EfiLoaderData` arena allocation, performs a
post-allocation map recapture, validates the exact five-record ledger, and
completes exactly one arena-confined 4 KiB metadata
allocation/free/double-free-rejection cycle before `OS_SMOKE_PASS`. M01-025 now
source-wires the additional default-off `M01_OWNED_PAGING_PROBE` stage-three
adapter. M01-033 supplies the reviewed deterministic host-built AMD64 COFF
serializer object, M01-034 links and independently verifies its exact
executable bytes and unwind coverage, and M01-035 source-wires checked direct
calls at all four current publication epochs. Those leaf results remain
source/host/build evidence in isolation. The later accepted M01-026 tuple
executed the exact integrated all-switch path once under its source-bound local
QEMU/OVMF configuration and observed the cumulative stage markers, reversible
owned-root transition, restoration records, protection probe, page-zero
absence, and aggregate four-epoch cookie success. All three M01 profiles remain
explicit pre-`ExitBootServices` opt-ins, and the default EFI path remains
unchanged.

The separate default-off M03-007 profile has source-bound QEMU/OVMF acceptance
at `e64e9dc1bd1f66ca522bd22aafe8bcd3388428ff` for one real
ExitBootServices transition, permanent owned-CR3/descriptor adoption, retained
ACPI/allocator checks and terminal owned-COM1 reporting ([step1051](../done/step1051.md)).
The default smoke and older reversible M01 profiles retain their existing scopes.
This accepts neither general firmware-adapter revocation nor firmware-page
reclamation, continuing workload execution or broader kernel readiness.

This is still an EFI application smoke, not a functional kernel. The current evidence proves that firmware reaches `EfiMain`, that `EfiMain` can capture the UEFI memory map on the tested QEMU host, copy the raw descriptor bytes into an OS-owned static buffer, validate the live owned handoff record, normalize and accept a live owned-map summary, materialize compact boot memory-region records into OS-owned static storage, carry a discovered ACPI RSDP pointer into the typed handoff record, optionally validate live ACPI RSDP/root SDT/MCFG bytes before `ExitBootServices` in an explicit probe build, and emit the ordered boot phase marker plan.

## Implemented Behavior

- `BootSequence.Run` emits host-tested boot phase markers from `BootSequencePlan`.
- `BootSequence.Run(BootHandoffRecord, ...)` validates the no-stdlib-safe handoff record before later managed boot phases and emits `BOOT_HANDOFF_READY` when the handoff passes.
- `BootSequence.Run(BootHandoffRecord, EfiMemoryMapSummary, ...)` adds a host-tested normalized memory-map precondition and emits `BOOT_MEMORY_MAP_ACCEPTED` only after the summary passes deterministic validation.
- `BootSequencePlan` is an allocation-free byte-indexed marker plan that can be source-linked into the no-stdlib EFI project.
- `BootSequencePlan` also carries a separate exact 11-line M01 stage-one
  success plan. It does not change the existing five non-smoke lines, eight
  default lines, or any default boot byte.
- Behind `M01_LOADED_IMAGE_STACK_PROBE`, `EfiEntry` now binds the running EFI
  image handle, two checked page allocations, a post-allocation static
  memory-map recapture, a four-record ownership ledger, the audited stage-one
  coordinator, and a direct unmanaged continuation. The ordinary all-false
  build retains the previous marker order and does not enter this adapter.
- Behind the additional `M01_LIVE_ALLOCATOR_PROBE`, the final owned-stack
  continuation now performs the M01-023 stage-two source/build boundary. It
  requests one two-page `EfiLoaderData` allocator arena, recaptures the map,
  extends the retained stage-one ledger with exactly one `AllocatorArena`,
  invokes the audited M01-018 coordinator over caller-owned static buffers, and
  emits the nine-line stage-two plan only after exact persisted-state and
  accounting validation. The accepted M01-024 tuple proves that this nested
  adapter reached its terminal stage-two checkpoint under the recorded
  QEMU/OVMF configuration; it does not widen the adapter beyond the exact
  retained arena or metadata-only cycle. This nested probe is also off by
  default.
- `BootSequencePlan` carries a second independent exact nine-line M01
  stage-two success plan. `BootSequence.RunM01LiveAllocatorIntegration`
  presents it only for a completed host result; it is not part of the default
  boot path.
- Behind the additional `M01_OWNED_PAGING_PROBE`, M01-025 source-wires the
  stage-three adapter after the completed stage-two continuation. It binds
  three firmware page-allocation sites, refreshed-map and reservation-ledger
  validation, the accepted preparation/arena/call-gate contracts, final
  readback and pre-transfer checks, the generated transfer call site, returned
  observation audit, and the fixed stage-three marker topology. This profile
  remains default-off and requires both earlier M01 probe symbols. The accepted
  M01-026 tuple proves guest execution of this exact integrated path under its
  recorded local QEMU/OVMF configuration, including the scoped
  CR3/PTE/TLB, restoration, publication-cookie, protection-probe, and page-zero
  observations described below. The isolated source/build contract and every
  dry run remain insufficient on their own.
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
- `RSDPSelectedRSDTXSDTMCFGMADTHPETBootstrap` composes those validators and selectors to retain the full declared RSDP, preferred XSDT or fallback RSDT, MCFG, MADT (`APIC`) and HPET bytes in one caller-owned destination. It validates complete supplied physical ranges and source/destination/scratch aliases before copying, returns five POD descriptors only after all copies succeed, and leaves the whole destination unchanged on rejection. Embedded physical addresses remain original firmware addresses; callers must use the returned offsets for retained access. Its complete host regressions pass, including source mutation retention and warmed allocation checks ([step972](../done/step972.md)). The separate default-off EFI adapter now retains the same five table roles in bounded static storage, with complete numeric host controls and default/enabled NativeAOT publications passing ([step973](../done/step973.md)). The host composition itself remains outside the no-stdlib closure. [Step977](../done/step977.md) records the accepted common suite, fresh ACPI capture and independent native/raw/lifecycle/disk associations for all five retained roles. This accepts the bounded pre-EBS copy; reviewed merge and #59/#539 closure remain pending. No post-exit access or device use is proved, and a late native copy/readback failure invalidates publication without promising rollback of bytes already written.
- `AcpiMcfgDiscovery` is a host-tested composition contract that selects the `MCFG` table through `AcpiTableDirectory`, parses it through `AcpiMcfgParser`, and writes `PciEcamSegment` records into caller-owned storage. It preserves selected table physical address, pointer/source indexes, nested directory status, nested MCFG parse status, segment count, and required segment capacity without physical-memory reads.
- `AcpiMcfgDiscovery.TryDiscoverFromRootSdtSnapshot` bridges the validated root RSDT/XSDT snapshot to child-table reads through `IPlatformPhysicalMemory`. It parses root pointers, reads child SDT headers through the platform boundary, selects `MCFG`, copies the full MCFG into caller-owned storage, validates checksum, parses ECAM segments, and reports all failures with value-type status metadata. It does not map ECAM or enumerate PCI devices.
- `Probes.EnableAcpiPhysicalDiscoveryProbe` defines the disabled-by-default live ACPI physical discovery probe contract. The explicit EFI build validates ACPI RSDP bytes, snapshots and validates the root RSDT/XSDT, scans child SDTs for a valid `MCFG`, parses ECAM segment metadata into fixed scratch storage, and emits the `ACPI_*` marker chain before the normal boot marker plan.
- `UefiExitBootServicesPlan` is a host-tested plan-only contract for fresh memory-map requirements, stale map-key retry, retry exhaustion, invalid handoff failures, and explicit skipped-call probe results before any real firmware shutdown call is added.
- `PlatformFirmwareExitBootServicesResult` is a host-tested firmware-call result contract for the future live `ExitBootServices` attempt. It maps guard denial, missing function pointer, EFI success, EFI invalid-parameter/stale-map-key, and generic firmware errors into status values that feed `UefiExitBootServicesAttemptMapper` and the existing retry plan without calling firmware.
- `Probes.EnableExitBootServicesFreshMapProbe` defines the disabled-by-default probe contract for a future fresh-memory-map `ExitBootServices` attempt. It has a stable `uefi.exitbootservices` probe group, and the explicit EFI probe build emits `UEFI_EBS_FRESH_MAP_READY` plus `UEFI_EBS_CALL_SKIPPED` without calling `ExitBootServices`.
- `UefiMemoryMapCopyPlan` defines a no-stdlib-safe, status-coded validation contract for copying a firmware-owned memory-map buffer into OS-owned storage.
- The copy-plan contract validates source map size, descriptor size/version/count, descriptor-count bounds, destination capacity, and arithmetic overflow before any EFI-boundary copy is attempted.
- `UefiMemoryMapCapture` owns the no-stdlib-safe `GetMemoryMap` sizing call, `AllocatePool` slack buffer allocation, retry capture, copy-plan validation, and copy into caller-owned storage. It returns status-coded capture failures plus the underlying `UefiMemoryMapCopyPlanStatus` when metadata or destination validation rejects the copy.
- [Step961](../done/step961.md) adds M03-001 host regressions against those existing capture/copy helpers: growth within the eight-descriptor slack, exhausted slack, changed stride, firmware failures, count/capacity/overflow boundaries, complete source/destination canary and tail checks, and warmed zero managed allocation. Capture performs one sizing query, one allocation and one recapture; a second `EFI_BUFFER_TOO_SMALL` fails without another allocation. These controlled callbacks do not establish live firmware behavior, arbitrary-pointer mapping/lifetime or map-key freshness for `ExitBootServices`.
- [Step968](../done/step968.md) adds the false-default
  `EnableM03MemoryMapCaptureProbe` observation profile around that same
  four-argument capture operation. It retains the original firmware function
  pointers, forwards each admitted query/allocation/recapture once, and records
  their actual arguments and returns without replacing the firmware table.
  Before emitting output, it compares the entire captured source with the
  owned copy and checks both 16-byte guards and the unused 128 KiB payload
  tail. The query and recapture size variables may occupy distinct, disjoint
  stack slots. The same snapshot and handoff feed the existing boot path;
  observation failure stops before the success markers. Complete focused
  host controls and default/enabled NativeAOT publications pass on the retained
  v8 inputs. Final v9 normalizes only the new authority file line endings and
  three derived source pins. Those artifact results retain their original
  source boundary. [Step970](../done/step970.md) completes the bounded #58/#532
  milestone on `854a335`: the clean common host gate passes 3,670 starts/3,671
  passes/zero failures, and fresh default/enabled captures bind the current
  source, original forwarding targets, full copy/guard/tail checks and ordinary
  lifecycle. The enabled capture reports one query, one allocation and one
  recapture, followed by the unchanged boot path. The earlier common-run
  failure and one-entry fixture repair remain in [step969](../done/step969.md).
  There is no new retry, `ExitBootServices` call, map-key freshness or
  post-firmware ownership claim; the rest of M03 remains incomplete.
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

- Default-path live page allocator/free-list ownership over planned boot
  memory ranges. `BootMemoryRegionPlanner`, `BootPageAllocatorPlanner`,
  `BootPageFreeListBuilder`, `BootPageAllocatorStateMaterializer`, and the
  strict serialized `AllocateOnePage`/`FreeOnePage` exact inverse remain the
  reviewed leaf contracts. M01-020 source-links their M01-018 coordinator;
  M01-023 wires and invokes one arena-confined metadata cycle only when both
  M01 probe symbols are enabled. M01-024 supplies one source-bound guest
  observation that
  firmware granted the exact arena, the post-allocation map and five-record
  ledger validated, and the coordinator completed its arena-confined metadata
  allocation/free/double-free-rejection cycle. This does not prove default-path
  allocator ownership, physical-page content access or zeroing,
  arbitrary-order free, receipt/ABA safety, ownership after
  `ExitBootServices`, a managed heap, live paging, or NX/W^X.
- Default-build live use of the typed address domains, loaded-image capture
  plan, and canonical reservation ledger remains disabled. The exact
  typed-address, reservation-record, pointer-validator, loaded-image range,
  and capture-plan sources are source-linked into `src/OS/OS.csproj`.
  M01-021 wires them into `EfiEntry` only under
  `M01_LOADED_IMAGE_STACK_PROBE`, where the adapter calls `HandleProtocol`,
  requests one `EfiLoaderCode` page and one 34-page `EfiLoaderData` arena,
  recaptures the map, and constructs a four-record ledger. M01-022 now supplies
  one source-bound guest observation that those firmware calls returned
  successfully, the ledger validated, the first-party switch path ran, and
  continuation `RSP` was contained in the usable stack. It does not prove that
  the reserved guard faults, that the switch page transitions RW-to-RX, that
  cache, NX, or W^X policy is enforced, that ownership survives
  `ExitBootServices`, or that these allocations form a reusable allocator.
  The typed wrappers are still not applied retroactively to every raw-`ulong`
  memory, MMIO, PCI, and DMA interface.
- Default-path or general-purpose live page-table allocation, population,
  mutation, ownership, or processor enforcement. In isolation the M01-011 codec
  validates and encodes caller-supplied values; its precondition POD does not
  itself read IA32_EFER.NXE, CR0.WP, CR4.LA57, CPUID physical width or 1
  GiB-page capability, or IA32_PAT, and its permissive nonleaf pointers do not
  prove arbitrary effective descendant permissions. The accepted M01-026 tuple
  is the scoped exception: the surrounding all-switch adapter captures the
  preconditions, constructs the fixed hierarchy, temporarily loads it, performs
  the one protection transition and `invlpg`, and restores firmware state. It
  does not handle a page fault, establish a general pager, or pass the
  systemwide NX/W^X completion gate.
- Default-path or general-purpose live boot identity mappings and table
  construction. In isolation M01-012 consumes caller-attested range, role,
  protection, cache, PAT, and processor-precondition values and emits
  caller-owned planning records. The accepted M01-026 tuple executes the exact
  integrated plan/materialization profile once under its source-bound
  QEMU/OVMF configuration; it does not discover arbitrary future image
  sections, establish framebuffer MMIO cache correctness, provide permanent
  owned paging, or make the planner a general mapping authority.
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
Those values remain caller attestations at this host-contract layer. M01-021
now supplies them from its separately gated live adapter, and M01-022 records
one guest observation; the M01-017 values alone are not proof that firmware
performed either allocation or map capture.

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

### Opt-in loaded-image and owned-stack EFI adapter (M01-021, build evidence)

M01-021 wires the M01-017 stage-one contract into the no-stdlib EFI entry
boundary only when `M01_LOADED_IMAGE_STACK_PROBE` is defined. The adapter runs
after the ordinary path has captured and validated its owned memory-map
handoff. The all-false build retains the prior `EfiEntry` flow and byte order;
it calls a compile-time-disabled adapter that returns without emitting any
stage-one marker.

The opt-in adapter uses the incoming EFI image handle with the loaded-image
protocol, retains the resulting image range as loader-owned, and makes exactly
two `AllocateAnyPages` calls:

- one 4 KiB `EfiLoaderCode` page for first-party executable byte plans; and
- one 34-page (139,264-byte) `EfiLoaderData` arena comprising exactly one
  4 KiB reserved guard page followed by 33 usable stack pages (135,168 bytes).

The adapter checks EFI status, nonzero and page-aligned ranges, checked byte
lengths and ends, allocation types, map membership, image/allocation
non-overlap, and exact guard/stack accounting. After both allocations it calls
`GetMemoryMap` into distinct static source storage, validates and copies the
result into the existing static owned-map destination, requires the opaque map
key to change, and advances the CLROS-owned generation from one to two. It then
constructs, sorts, and validates exactly four canonical records:
`LoadedImage`, `BootStackSwitchStub`, `BootStackGuard`, and `BootStack`.
Static map, coordinator, handoff, allocation-table, and ledger storage already
contained by the loaded image are not duplicated in the reservation ledger.
Every pre-transfer failure attempts reverse-order cleanup of the data arena
and then the code page; the installed stack-probe target must be cleared before
the code page can be freed. Allocation ownership follows `EFI_SUCCESS`, not
address truthiness, so an EFI-success/address-zero result is still passed back
to `FreePages(0, pageCount)` after CLROS rejects it as an invalid address.
`HandleProtocol` and `GetMemoryMap` failures preserve the exact returned
`EFI_STATUS` separately from the stable adapter classification.

The code page contains two disjoint, read-back-verified first-party plans. The
existing 14-byte M01-005 stack-switch plan starts at offset 16. A 27-byte x64
page-probe plan starts at offset 64, begins with an indirect-branch landing
prefix, walks downward one 4 KiB page at a time, touches each page, compares
against the special-ABI target in `R11`, and returns without modifying `R11`.
Before either slice is written, the complete 4 KiB page is initialized to
`0xCC` and read back.
`System.Runtime.X64StackProbeRuntime` exports `RhpStackProbe`, fails closed
until that page plan is installed, and clears the target before a
pre-transfer code-page free.

The firmware stack never constructs or returns the 3,264-byte diagnostic
aggregate. `PrepareFromStaticScratch` first validates one exactly sized,
8-byte-aligned, initially zero caller-owned scratch region inside the loaded
image. Before its first write it proves the scratch is disjoint from the full
allocation-table and reservation-ledger capacities, code and data
allocations, source and owned map buffers, static switch state, and
continuation address with checked range arithmetic. The shared preparation
core writes the large POD evidence there and returns only the 8-byte
status/checkpoint result. The existing rich host API wraps that same core so
host and firmware behavior do not fork.

The `RhpStackProbe` design was independently encoded in C# after reviewing the
official MIT-licensed
[.NET runtime v10.0.8 `MiscStubs.asm`](https://github.com/dotnet/runtime/blob/v10.0.8/src/coreclr/nativeaot/Runtime/amd64/MiscStubs.asm).
That source defines the
special NativeAOT x64 contract in which the generated large-frame prologue
places its proposed stack pointer in `R11`, calls `RhpStackProbe`, and then
commits `RSP`. C# source shape alone cannot prove that special ABI. The emitted
bridge disassembly is therefore binding for each accepted binary. The current
artifact was manually checked to show a frameless bridge that loads and tests
the installed target through `RAX`, follows the compiler's aligned direct
branch, tail-jumps through `RAX`, and loops on a null target without writing
`RSP`, `R11`, or argument registers on the valid direct path. This observation
is binary-specific and must be repeated after any compiler, runtime-pack,
source, or code-generation change.

The continuation is a static method marked with the compiler-recognized
`UnmanagedCallersOnly` contract and is bound directly as
`delegate* unmanaged`. The switch transfers only static POD state and does not
return on the accepted path. `TEST_START m01.stage1` is emitted when the
adapter begins. Every remaining success/checkpoint line in the canonical
11-line plan is withheld until the continuation has called
`ObserveContinuationRspFromStaticState` and verified that the observed `RSP`
is inside the exact usable stack range. The continuation then emits lines 1
through 10 retrospectively in canonical order and proceeds to the unchanged
default boot-sequence marker plan.

Failures use two numeric, allocation-free lines and never emit a success
terminal:

```text
TEST_FAIL m01.stage1 status=<number> adapter=<number> checkpoint=<number> free_data=<number> free_code=<number>
M01_STAGE1_FAIL status=<number> adapter=<number> checkpoint=<number> free_data=<number> free_code=<number>
```

The status fields preserve coordinator or firmware status, the append-only
adapter status, the last coordinator checkpoint, and reverse-order cleanup
outcomes. A continuation-side failure reports and spins because its data arena
contains the active stack and cannot be reclaimed safely.

The accepted 2026-07-27 opt-in build produced a 131,584-byte PE32+ AMD64 EFI
application with executable `.managed` entrypoint RVA `0x00010BD0`, absent
ordinary, bound, IAT, and delay-import directories, and same-build `EfiMain`
association.
The EFI SHA-256 is
`42DC4647AC3EC1B0CAF2A86863834C186E8A0E324DBEE37B879FC401F1E7930F`;
the NativeAOT object, same-build linker-map, and response hashes are
`D3E31151A51ABE783F2638674C8A7A897BDE8A4064A04FA9D9E29446896F8F22`,
`81BABEFFD51033B42014AD5356C11F00A81C503BE1BA50EB668EDD0903AE8323`,
and
`0108286539031918F3576A3C9759803740D9E2DCB1A26E3E244E882C56E82F1F`.
`scripts/test-efi-artifact.ps1` emitted `TEST_PASS efi.artifact.verify`.

Generated-object disassembly is part of the accepted ABI evidence. The
conservative deepest direct pre-transfer chain consumes `0xA268`, or 41,576
bytes, including each generated frame and saved nonvolatile-register
contribution used by the audit. That is 23,960 bytes below the issue's 64 KiB
half-stack ceiling, leaving the other half of the UEFI x64 128 KiB minimum for
`EfiMain`, marker, firmware-call, and interrupt overhead. The emitted
`RhpStackProbe` bridge remains frameless and tail-dispatches through `RAX`
without changing `RSP`, `R11`, or argument registers on the valid path. A
post-clear call-graph audit found no managed path from probe unpublication to
another `RhpStackProbe` call before failure return. These measurements are
binary-specific and must be repeated after relevant source or toolchain
changes.

M01-021 itself accepted build/source evidence only. The following M01-022
record supplies the separately gated guest execution, serial-marker,
live-`RSP`, and failure-classification proof. Neither record proves that the
guard page is inaccessible, that the code page transitions from writable to
executable, that NX/W^X is enforced, that execution survives
`ExitBootServices`, or that any completion gate advances.

### Fresh loaded-image and owned-stack QEMU proof (M01-022)

The authoritative run rebuilt the opt-in artifact from clean commit
`198be8ad12db388fec44d76f9270c8d754430940`, built a fresh 64 MiB raw image,
and booted it with QEMU/OVMF from
`2026-07-27T19:03:19.7090684Z` through
`2026-07-27T19:03:39.0168910Z`. The runner required this exact sequence both
in order and exactly once:

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
OS_SMOKE_PASS
```

The streaming result was
`status=Passed terminal=Success bytes=1286 lines=34 invalidBytes=15
invalidLines=1 orderedSeen=12 outOfOrder=0 uniqueSeen=12
duplicateUnique=0`. The invalid-byte count is one bounded OVMF noise line; it
did not contain or corrupt a marker. The final summary records
`MISSING_MARKERS none`, `OUT_OF_ORDER_MARKERS none`,
`DUPLICATE_UNIQUE_MARKERS none`, `FORBIDDEN_MARKER_HIT none`, and
`FAIL_REASON none`. `TEST_FAIL`, `OS_SMOKE_FAIL`,
`M01_INTEGRATION_FAIL`, and `M01_STAGE1_FAIL` were absent.

The artifact verifier independently reported PE32+ AMD64 machine `0x8664`,
EFI application subsystem `10`, executable/file-backed `.managed` entrypoint
RVA `0x00010BD0`, same-build `EfiMain` association, and absent ordinary,
bound, IAT, and delay-import directories. The accepted tools were .NET SDK
`10.0.302`, MSVC linker `14.51.36252.0`, Windows SDK `10.0.28000.0`,
ILCompiler and NativeAOT runtime pack `10.0.8`, QEMU/qemu-img `11.0.50`
(`v11.0.0-12631-g54e84cdc7a`), and OVMF SHA-256
`33090CC07675BAA5190D9F1E84BF5176B33BCBFA9BACAC522961150CDB6DBB2A`.

| Artifact | Bytes | SHA-256 |
| --- | ---: | --- |
| `BOOTX64.EFI` | 131,584 | `62BCD4902A55DDDACCA8C490C6B5615C1BEEF78BA7DF96038FA1687FD2B8DC60` |
| `BOOTX64.EFI.link.json` | 1,817 | `9011F437D1C108D951E44ED4A4E02D26F059A3F4BA86FEC7585E065FFC0898EB` |
| `BOOTX64.EFI.link.map` | 60,905 | `8836CA390BC6409FD96E966E7FCFF5E9DA013FB6230D1D4D27CF12F859297821` |
| `BOOTX64.EFI.link.rsp` | 217 | `0108286539031918F3576A3C9759803740D9E2DCB1A26E3E244E882C56E82F1F` |
| `clros.raw` | 67,108,864 | `0186D6B11DD31480B42F2ED3013E283ADEF7E425E5281872E58F9921E6F9CC02` |
| `serial.log` | 1,286 | `BF775826807B4B3E61B6E26C1DDD7101E0E16F141AC17384ADBC94C54CD1B1B9` |
| `qemu-smoke.summary.log` | 3,333 | `3EBDC0BCAAC146E2B8BEDBEE5B6C3ADA11413720558E874140975DF16186726A` |
| `qemu-smoke.stdout.log` | 15,056 | `6CCF99739FB05CDAA0FA6F2A15C5ED0A14B156DAE48A8F30C56052F22C481F54` |
| `evidence-manifest.json` | 9,805 | `2EC0D45A229CA749C5F238ED7156DBB29E70F4BBAAA563C9AB6D260B02742968` |

The ignored evidence root is
`artifacts/logs/m01-022/198be8ad12db-20260727T190237Z/`. The manifest is
schema version 1, UTF-8 without BOM, source-dirty `false`, classification
`pass`, exit code `0`, and binds 10 tools, 19 nonempty hashed inputs, 14
required markers, four forbidden markers, and 31 serial-derived observed
markers.

This is a narrow pre-`ExitBootServices` proof. It establishes successful
firmware protocol/allocation calls on this run, refreshed-map/ledger
validation, execution of the first-party switch path, continuation on the
owned stack, and contained `RSP`. It does not establish guard-page
inaccessibility, RW-to-RX transition, NX/W^X, a general live page allocator,
owned COM1, installed CR3/descriptors, firmware exit, PCI, storage,
networking, application execution, hosted .NET, Kestrel, cloud support, or a
completion-gate promotion.

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
M01-018 does not parse the refreshed descriptor bytes. M01-023 now derives
these values from one separately gated serialized capture; the M01-018 host
contract detects malformed, stale, nonincrementing, reordered, or mismatched
supplied evidence but does not independently observe firmware.

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
QEMU/OVMF, or change a completion-gate state. Those statements describe the
M01-018 issue boundary; M01-023 now supplies the separately gated EFI
source/build wiring described below.

### Opt-in live allocator stage-two EFI adapter (M01-023, build evidence)

M01-023 adds a second, nested opt-in boundary. The stage-two adapter is compiled
only when `M01_LIVE_ALLOCATOR_PROBE` is enabled, and the project and publish
preflight require `M01_LOADED_IMAGE_STACK_PROBE` at the same time. The
all-false build and the stage-one-only M01-022 profile retain their prior source
flow and marker order.

The stage-one preparation path captures the live boot-services pointer in
static state immediately before transferring to the owned stack. After the
continuation has observed a contained `RSP` and retrospectively emitted every
stage-one success line, it passes the exact self-bound final stage-one state,
boot-services boundary, console/serial boundaries, and retained four-record
ledger to stage two. Stage two rejects the input before any new firmware call
unless the state has exact size/alignment and is the canonical static state
whose status is `Success`, checkpoint is `ContinuationRspContained`, switch
state is `ContinuationObserved`, and stack lifetime is `Active`.

Before acquiring firmware ownership, the adapter validates its static storage
and requires every byte of the 312-byte durable M01 output state to be zero.
This makes the adapter one-shot: a completed or partially initialized state is
rejected without allocating a second arena or erasing the first arena's
ownership record. It then makes exactly one `AllocateAnyPages` request for two
4 KiB `EfiLoaderData` pages. Ownership follows the returned EFI success status,
while the returned address is independently checked for nonzero value,
alignment, multiplication/addition overflow, exact byte length, and
end-exclusive accounting.

After the allocation, the adapter reuses the audited stage-one static
memory-map source and owned-copy buffers. It performs one fresh recapture,
rejects every non-success result including a grown/too-small buffer, requires a
changed map key, and verifies that the new descriptor set contains the complete
arena with type `EfiLoaderData`. M01-023 intentionally does not retry map
capture; the bounded stale-key retry belongs to the later
`ExitBootServices` work. The adapter copies the four retained canonical
reservations, adds one exact `AllocatorArena`, sorts by physical start, and
validates an exact five-record count and capacity before producing allocation
and map/ledger evidence with ordinals 5, 6, and 7 and map generations 2 and 3.

The live-facing bridge calls `M01LiveAllocatorIntegration.Execute` exactly once
with the final stage-one state, one allocation record, the allocation and
refreshed-map evidence, the five-record ledger, and one caller-owned static
output. Before any stage-two success line after `TEST_START m01.stage2`, the
adapter requires the terminal `DoubleFreeRejected` checkpoint; exact
stage-one/output addresses and capacities; complete arena, map, and ledger
binding; final free-page/byte restoration; zero allocated pages/bytes; an
arena-confined 4 KiB allocation; and the precise `FreeRangeNotAllocated`
double-free rejection. It byte-compares the complete returned and persisted
312-byte states, so a stale or corrupted caller-owned result cannot authorize
success.

All failures converge on these allocation-free numeric diagnostics:

```text
TEST_FAIL m01.stage2 status=<number> adapter=<number> checkpoint=<number> firmware=<number> free_arena=<number>
M01_STAGE2_FAIL status=<number> adapter=<number> checkpoint=<number> firmware=<number> free_arena=<number>
```

If EFI granted the arena, the common failure path makes exactly one matching
`FreePages` attempt and reports its status. Pre-allocation failures do not
free, and success retains the arena for later allocator consumers. A false
stage-two result causes the continuation to spin before the default boot plan,
so `OS_SMOKE_PASS` cannot leak through a failed stage-two probe.

Focused source-contract, mutation, no-stdlib closure, and full host-harness
coverage uses:

```text
TEST_START m01.023
CLROS_M01_023_READY
TEST_PASS m01.023
```

The post-allocation failure cases were independently authored after reviewing
`references/RoseOS-master/Loader/src/MemoryMap.cs` (SHA-256
`CE90AE629B538E558B57B0BD315ED8B11DD559ED1A841368D109DE28D70B8B28`)
as an owner-authorized test-only negative influence. Its exact-size allocation,
single-slack attempt, continue-after-error behavior, unchecked narrowing, and
cleanup hazards became rejection cases; no implementation source was copied.

M01-023 accepts source, host-test, and fresh NativeAOT EFI build evidence only.
It does not claim that the stage-two adapter executed under QEMU/OVMF, that
firmware granted or reclaimed the arena, that the guest completed the
alloc/free/double-free sequence, that page contents were accessed, that the map
key remains valid for firmware exit, or that a post-EBS allocator exists. It
does not prove paging, NX/W^X, owned COM1, descriptors, PCI, storage,
networking, application execution, hosted .NET, Kestrel, cloud support, or any
completion-gate promotion. The following M01-024 record supplies the separate
real-QEMU proof without widening the historical M01-023 build claim.

### Fresh pre-EBS allocator-arena QEMU proof (M01-024)

M01-024 rebuilt the dual-probe profile from clean implementation commit
`f7437521799e9fe974a9de4560db248f94addf6b` and booted it under QEMU
`11.0.50` with the 3,653,632-byte
`C:\Program Files\qemu\share\edk2-x86_64-code.fd` whose SHA-256 is
`33090CC07675BAA5190D9F1E84BF5176B33BCBFA9BACAC522961150CDB6DBB2A`.
The run began at `2026-07-27T21:21:20.2271722Z` and completed at
`2026-07-27T21:21:43.5137043Z`. The guest serial stream contained this exact
cumulative sequence once and in order:

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
TEST_START m01.stage2
M01_ALLOCATOR_ARENA_ALLOCATED
M01_STAGE2_MAP_RECAPTURED
M01_RESERVATIONS_APPLIED
M01_LIVE_ALLOCATED_4K
M01_LIVE_PAGE_WITHIN_ARENA
M01_LIVE_FREED_4K
M01_DOUBLE_FREE_REJECTED
TEST_PASS m01.stage2
OS_SMOKE_PASS
```

The runner additionally emitted host-process markers
`TEST_START qemu.smoke` and `TEST_PASS qemu.smoke`; those are not guest serial
requirements. Its final streaming state was
`status=Passed terminal=Success bytes=1511 lines=43 invalidBytes=15
invalidLines=1 orderedSeen=21 outOfOrder=0 uniqueSeen=21
duplicateUnique=0`. The invalid-byte/line count was bounded firmware noise and
did not contain an accepted marker. The summary reported
`MISSING_MARKERS none`, `OUT_OF_ORDER_MARKERS none`,
`DUPLICATE_UNIQUE_MARKERS none`, `FORBIDDEN_MARKER_HIT none`, and
`FAIL_REASON none`. None of these nine forbidden boundaries appeared:

```text
LIVE_ALLOCATOR_NOT_INSTALLED
BOOT_PAGE_ALLOCATOR_NOT_READY
M01_ARBITRARY_CONVENTIONAL_PAGE_USED
M01_PAGE_OUTSIDE_ALLOCATOR_ARENA
TEST_FAIL
OS_SMOKE_FAIL
M01_INTEGRATION_FAIL
M01_STAGE1_FAIL
M01_STAGE2_FAIL
```

The fresh EFI verifier reported PE32+ x64 machine `0x8664`, EFI application
subsystem `10`, same-build `EfiMain` association at RVA `0x0001e930`, and
absent ordinary, bound, IAT, and delay-import directories. The accepted
toolchain was .NET SDK `10.0.302`, MSVC linker `14.51.36252.0`, Windows SDK
`10.0.28000.0`, ILCompiler `10.0.8` (file `10.0.826.23019`), NativeAOT
runtime pack `10.0.8`, QEMU/qemu-img `11.0.50`, and the OVMF bytes identified
above.

| Evidence artifact | Bytes | SHA-256 |
| --- | ---: | --- |
| `BOOTX64.EFI` | 208,384 | `ED1FDD62FF50ABE9AB30BBFAC85EBB7C095D3F31720C2571F31C5413726EFA13` |
| `BOOTX64.EFI.link.json` | 1,817 | `C3E20E72FAF49C804080E706DA77A91C69B64B24DD89DE991EBCA7CF6AC9F46E` |
| `BOOTX64.EFI.link.map` | 93,728 | `CBFBCA6826373C335B5715A7234D36C9837D254257CD1AFA783F94DABA1F765D` |
| `BOOTX64.EFI.link.rsp` | 217 | `0108286539031918F3576A3C9759803740D9E2DCB1A26E3E244E882C56E82F1F` |
| `clros.raw` | 67,108,864 | `51D00F4B3302461C0FFF2466BAA26A7909C891B9E5434A3B2CE78374D9F6FF4B` |
| `serial.log` | 1,511 | `2C74EE4CC10475D85EAA090A1382BEF08F9C114FD213A5AEB6E487F4405BD1EC` |
| `qemu-smoke.summary.log` | 4,495 | `19925845684DBBEA93962C762B2653BFA2224BBFF9707B3E4C38D882F6E46FF5` |
| `qemu-smoke.stdout.log` | 17,806 | `BC7D34774F8029BABC713E3AB19B0615E5BF9C896A2072965F53B74BDC76739C` |
| `evidence-manifest.json` | 13,621 | `9FDB9B72EB708B2A1184725E009A168FBD0BC3FEA3B8FCB0EA93C7F416031A58` |

The ignored tuple is under
`artifacts/logs/m01-024/f7437521799e-20260727T210930Z/`. Its manifest
independently derived `source.commit` from Git, required
`source.dirty=false`, recorded run exit code `0` and classification `pass`,
hashed 26 artifacts/tool inputs, and derived 40 observed markers from the
hashed serial bytes.

This proof is deliberately narrow. It establishes stage-one continuity plus
one pre-`ExitBootServices` loader-owned EFI allocator arena, post-allocation
memory-map recapture, exact five-record reservation accounting, and exactly one
within-arena 4 KiB metadata allocation/free/double-free-rejection cycle under
the recorded local QEMU/OVMF configuration. `M01_LIVE_ALLOCATED_4K` is
allocator-metadata evidence; the page bytes were not read, written, or zeroed.
The run does not grant ownership over arbitrary conventional memory, establish
a reusable or post-EBS allocator, prove paging, CR3/TLB operations, W^X/NX,
page-zero policy, owned diagnostics/descriptors, firmware exit, devices,
workloads, hosted .NET, Kestrel, cloud support, the M01 exit criterion, or any
completion-gate promotion. The default EFI path remains unchanged.

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

The default EFI profile does not invoke this leaf. M01-020 source-links it;
under the dual opt-in profile, M01-023 makes it reachable only through
`M01LiveAllocatorIntegration`, after the adapter has established the exact
firmware-allocated two-page arena and five-record ledger. The separately
recorded M01-024 tuple proves that narrow guest path reached the reservation
application checkpoint. It does not establish a general source-range import,
grant ownership over arbitrary conventional memory, configure paging, enforce
NX/W^X, or change a completion-gate state.

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

This leaf preserves `PerformsLiveAllocation=false`: it mutates allocator
metadata and does not own, zero, map, protect, or access physical-page bytes.
The default EFI profile does not invoke it. M01-020 source-links it; under the
dual opt-in profile, M01-023 makes it reachable only through
`M01LiveAllocatorIntegration`, and the separately recorded M01-024 tuple
proves exactly one arena-confined metadata allocation reached its guest marker
over the explicit pre-EBS arena. No general allocation API, page-content
access, post-EBS ownership, or completion-gate state follows from that
evidence.

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
accesses a physical page. The default EFI profile does not invoke the leaf.
M01-020 source-links it; under the dual opt-in profile, M01-023 makes it
reachable and invokes it only through `M01LiveAllocatorIntegration`. The
separately recorded M01-024 tuple proves that exact serialized inverse and
`FreeRangeNotAllocated` double-free rejection reached their terminal guest
markers over the explicit pre-EBS arena. The evidence does not broaden the
exact-inverse contract, expose a general free API, or change a completion
gate.

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
`MultiMinimalApi` project closures proved that those sources and the test were
not linked into `src/OS/OS.csproj` when M01-011 closed. Closed M01-020 later
linked the exact production sources into the opt-in EFI closure while keeping
the host test excluded.

This remains host-only value-model evidence when considered in isolation. The
codec itself does not allocate table pages, own a page-table arena, read live
processor preconditions, enable NXE or WP, program live PAT state, load CR3,
invalidate a TLB, trigger a page fault, define `DmaMapping`, or change the
NX/W^X completion gate. M01-014 and M01-019 consumed these exact policy, level,
context, entry, and result types. The accepted M01-026 tuple is the scoped
integrated exception: its surrounding adapter supplies guest-written preflight
observations, materializes the fixed table profile, and executes the bounded
transition/probe path under QEMU/OVMF without turning the isolated codec into
processor or systemwide policy evidence.

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

The retained `Create(...)` compatibility entry requires all six historical
emitted boot purposes:

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

M01-030 adds a separate `CreateLiveInterval(...)` profile. It requires exactly
seven source records: one non-emitted `BootStackGuard` exclusion plus one each
for the composite code/call-gate page, owned composite stack,
protection-probe page, complete table/state arena, runtime-state/canary page,
and owned-root page. Historical mapped purposes and `AdditionalRequired`
cannot satisfy this profile. The historical and live-interval completeness
masks are separate; M01-030 does not reinterpret or renumber an older value.

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
| 9 | `CompositeCodeAndCallGate` | Exact live profile; required and emitted RX/WB |
| 10 | `CompositeOwnedStack` | Exact live profile; required and emitted RW+NX/WB |
| 11 | `ProtectionProbePage` | Exact live profile; required and emitted initially RW+NX/WB |
| 12 | `CompleteTableAndStateArena` | Exact live profile; required and emitted RW+NX/WB |
| 13 | `RuntimeStateAndCanaryPage` | Exact contained live role; required and emitted RW+NX/WB |
| 14 | `OwnedRootPage` | Exact contained live role; required and emitted RW+NX/WB |

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
| `RequiredLiveIntervalPurposeMask` | `0x7e00` | Bits 9 through 14; the six exact present live-interval purposes |
| `RequiredLiveIntervalPurposeCount` | 7 | Six exact present roles plus one absent guard sentinel |
| `UsesOnly4KiBLeaves` | `true` | The planner accounts for and validates only 4 KiB leaf mappings |
| `PerformsLiveMapping` | `false` | The planner never changes a live address space |
| `AllocatesPageTables` | `false` | The planner only reports required table capacity |
| `PopulatesPageTables` | `false` | The planner emits mapping records, not page-table entries |
| `X64BootIdentityMappingPlanResult.NoIndex` | `~0UL` | Initialized result sentinel for no source index |

The retained historical entry point is:

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

The exact M01-030 profile has the same pointer/count/capacity and precondition
shape through `X64BootIdentityMappingPlan.CreateLiveInterval(...)`; only its
allowed and required purpose set differs. Both remain host planners and perform
no live mapping.

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
| 13 | `m01.012 13 canonical efi closures include the planner and exclude host tests` |
| 14 | `m01.012 14 marker ownership and ordering are stable` |

Any aggregate failure emits `TEST_FAIL m01.012 <reason>` and prevents the
ready/pass pair. Group 12 compiles the exact production source in the retained
`NoStdLib=true`, `NOSTDLIB_EFI`, zero-framework-reference fixture. Closed
M01-020 updated group 13 to prove that both evaluated opt-in
`SingleMinimalApi` and `MultiMinimalApi` EFI closures source-link the planner
while excluding its host test. At that evidence boundary no `EfiEntry` path
invoked the planner. M01-025 now source-wires it, but its accepted build did
not execute the path and adds no live EFI or guest evidence.

This is host-only plan evidence. It does not discover or own live boot ranges,
allocate or zero table pages, assign physical table addresses, populate a
table image, protect a real guard, map page zero or prove its hardware absence,
perform a live RW-to-RX transition, load CR3, invalidate a TLB, execute under
EFI or QEMU/OVMF, or advance any completion gate. M01-019 supplies host-only
preparation and synthetic transcript composition, M01-029 owns the
preparation/execution-transcript split, M01-031 supplies the separate host-only
physical-arena sizing/materialization/readback contract, and M01-032 supplies
the host-only audited call gate and post-return observation contract. M01-025
now source-wires those contracts into the default-off stage-three adapter but
adds no guest execution evidence. M01-033 through M01-035 form the
immutable-text serializer prerequisite chain. The later accepted M01-026 tuple
now supplies bounded integrated real-QEMU evidence for this behavior; the
isolated plans and build result remain non-execution evidence.

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
zero-framework-reference fixture with zero assembly references. Closed
M01-020 source-links the production sources into both evaluated opt-in
`SingleMinimalApi` and `MultiMinimalApi` EFI closures while retaining their
host tests outside those closures. At that evidence boundary no `EfiEntry`
path invoked the linked plans. M01-025 now source-wires them, but its accepted
build did not execute the path.

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

The isolated M01-013 issue proved host-audited, default-off planning and byte
identity only. Host tests do not invoke either byte stream. Closed M01-020
source-links the exact production sources into the opt-in EFI compile closure,
but linkage and build success alone add no live EFI or guest evidence. M01-025
then source-wired the default-off stage-three adapter, and the accepted
M01-026 tuple became the scoped current `EfiEntry` exception: it allocates,
zeros, and populates the fixed table profile, invokes the reviewed bytes under
the temporary owned root, performs the bounded protection/TLB work, and
restores firmware state. That tuple does not establish a general pager,
permanent owned-root policy, a completion-gate transition, PCID/LA57 support,
or NMI/SMI safety. NMI/SMI behavior remains an explicit residual risk until
M02 owns descriptors and fault diagnostics. M01-019 supplies host-only preparation and synthetic
transcript composition, M01-029 owns the preparation/execution-transcript
split, M01-031 supplies the host-only physical-arena contract, and M01-032
supplies the host-only audited call gate and post-return observation contract.
M01-033 through M01-035 form the immutable-text serializer prerequisite chain.
The isolated plans and build result remain non-execution evidence; only the
accepted M01-026 tuple supplies the bounded integrated real-QEMU evidence.

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
byte/transcript identity. Closed M01-020 source-links the production sources
into the opt-in EFI compile closure. At that evidence boundary no `EfiEntry`
path invoked them. M01-025 now source-wires them, but its accepted build did
not execute the path and adds no live evidence. It therefore does not prove a
live page allocation or populated table, a processor-owned PTE store, stream
execution, a CR3 reload, CPU TLB invalidation, effective parent permissions,
page-zero absence, SMP shootdown, QEMU/OVMF execution, or completion-gate
advancement. M01-019 supplies host-only preparation and synthetic
transcript composition, M01-029 owns the preparation/execution-transcript
split, M01-031 supplies the host-only physical-arena contract, and M01-032
supplies the host-only audited call gate and post-return observation contract.
M01-025 now source-wires those contracts into the default-off stage-three
adapter but adds no guest execution evidence. M01-033 through M01-035 form the
immutable-text serializer prerequisite chain. The later accepted M01-026 tuple
now supplies bounded integrated real-QEMU evidence for this behavior; the
isolated plans and build result remain non-execution evidence.

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
`NoStdLib=true`, without framework or assembly references. Closed M01-020
source-links `PageZeroMappingPolicy` into both evaluated opt-in
`SingleMinimalApi` and `MultiMinimalApi` EFI closures;
`NullPageFaultProbePlan` and the host test remain excluded. At M01-015 closure,
no `EfiEntry` path invoked the policy and no live EFI or QEMU execution had
occurred. M01-025 later source-wired the policy into the default-off stage-three
adapter, and the accepted M01-026 tuple executes that exact integrated
page-zero-absence audit during its owned-root interval.

The SharpOS `PageFlags.cs` snapshot (SHA-256
`DC565A4B8BFA38CDACFBDB5B53B2D5616BB9C1057DEDE7A1673956D6F3198652`)
was used only to cross-check Present/NX bit meaning.
`PagingValidation.cs` (SHA-256
`B1699D328E49D5FC68B8CF8979B366D72CCA2E3B4AA5B982F91F0A2C9A1C40F6`)
contributed missing-map and malformed-entry test categories. CLROS does not
import its live allocator, global pager, panic/log path, raw-`ulong`
architecture, or skip-on-no-free-slot behavior.

The isolated M01-015 result is not live page-zero proof. Its supplied image may
be stale or unrelated to CR3, and it performs no null read, page fault,
CR2/error-code capture, handler dispatch, panic report, EFI execution, QEMU
run, or completion-gate transition.
The later accepted M01-026 tuple proves page-zero absence only during its exact
owned-root interval. It deliberately does not dereference page zero; M02-014
owns the separately disabled actual null-read `#PF` probe after owned IDT and
diagnostics exist. `Probes.cs`, `BootSequence.cs`, `EfiEntry.cs`, and
`completion-gates.md` were unchanged by M01-015.

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

The retained M01-019 `Execute(...)` and `ExecuteModelOnly(...)` compatibility
surfaces still require the historical `SerialStub` purpose even though owned
port I/O and COM1 belong to M02. They bind that slot to the already reviewed RX
`BootStackSwitchStub` reservation only so an arbitrary executable range cannot
enter the legacy model. It is a compatibility binding, not evidence of a
serial stub or owned diagnostics. M01-030 does not retroactively change this
historical evidence: the M01-029 preparation path uses the separate exact
live-interval profile and cannot be satisfied by this alias.
`ProvesOwnedSerialDiagnostics` remains `false`. These are role-based page
permissions, not PE-section permissions;
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
M01_INSTRUCTION_PUBLICATION_EPOCHS_VERIFIED
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
`RestoresModeledProbeLeafToInitialProtection` is `true` when the historical
model-only `Execute`/`ExecuteModelOnly` path returns.

The aggregate host-harness markers are:

```text
TEST_START m01.019
CLROS_M01_019_READY
TEST_PASS m01.019
```

M01-029 adds `M01LivePagingIntegration.Prepare` as the first-class
preparation-only entry. It consumes explicit caller-owned POD
pointer/count/capacity storage and returns
`M01LivePagingPreparationEvidence`, `M01LivePagingPreparationState`, and
`M01LivePagingPreparationResult`; `M01LivePagingExecutionMode` and
`M01LivePagingProtectionPreparation` keep model-only execution intent and
deterministic protection preparation distinct. Preparation succeeds without
accepting a synthetic
`X64ReversibleCr3TransitionRuntimeState`, `X64Cr3ProbeEvent`,
`X64ProtectionProbeObservation`, or `X64ProtectionProbeEvent`. After M01-030, a
successful preparation deterministically validates the allocation records,
refreshed map and sorted ledger, canonical reservation relationships, the
seven-record exact live-interval identity profile, architectural preflight,
exact byte plans and readback,
table-image and entry workspaces, protection/invalidation plans, page-zero
audit, and cleanup evidence required before a future composite transfer.

Preparation is one-shot and fail-closed. Default, null, zero, short, excess,
overflowing, unaligned, overlapping, aliased, stale-map, unsorted-ledger,
malformed-preflight, malformed-byte-plan, undersized-workspace, and
repeated-use inputs cannot publish a partially prepared state.
Architectural-preflight rejection occurs before any mapping, byte,
page-image, entry, or state output is written; later failures can retain
deterministic caller-workspace construction, but the final preparation state
is committed only after every check succeeds. The preserved `Execute`
compatibility entry and explicit `ExecuteModelOnly` entry remain
historical model-only surfaces over caller-supplied execution transcripts. A
successful legacy model result is neither composite preparation nor execution
evidence and cannot be converted into or used to satisfy the new
preparation-only result/state/evidence contract.

The M01-029 aggregate host-harness markers are:

```text
TEST_START m01.029
CLROS_M01_029_READY
TEST_PASS m01.029
```

Any rejected M01-029 test group emits `TEST_FAIL m01.029 <reason>` instead of
the ready/pass pair. These markers prove only the caller-owned preparation and
anti-promotion boundary. Closed M01-020 source-links the coordinator into the
opt-in EFI compile closure. At that evidence boundary no `EfiEntry` path
invoked it. M01-025 now source-wires it, but its accepted build did not execute
the path and adds no live evidence. Neither preparation nor the legacy model calls
firmware or EFI `AllocatePages`, invokes generated code, changes CR3, a live
PTE, the TLB, or RFLAGS, physically materializes page tables, proves the
caller-owned model is the installed live identity interval, proves page zero
absent in hardware, runs under QEMU/OVMF or in a guest, or advances a completion
gate. PCID/LA57 remain
unsupported, NMI/SMI remains an M02 residual risk, and null dereference/#PF
evidence remains M02-014. M01-030 owns the accepted host-only exact
live-interval identity bindings, M01-031 supplies the completed host-only
physical table-arena sizing/materialization/readback contract, and M01-032
supplies the completed host-only audited call gate and post-return RFLAGS
observation contract. M01-025 now source-wires the live-allocation and
default-off `EfiEntry` invocation boundary but adds no guest execution
evidence. M01-033 through M01-035 form the reviewed and closed immutable-text
serializer prerequisite chain. The later accepted M01-026 tuple now supplies
the bounded cumulative fresh-artifact real-QEMU evidence. In arbitrary logs
and the isolated M01-019 presenter,
`CLROS_M01_LIVE_BOOT_MEMORY_READY` remains only a reserved host-tested contract
line; it is guest evidence only in the source-bound accepted M01-026 tuple.

### Exact future live-interval binding contract (M01-030)

M01-030 replaces historical aliases only in the successful preparation and
composite-state paths. Existing enum values and the legacy M01-019 entry remain
append-only compatibility surfaces. The exact profile accepts six present
bindings:

1. the single composite and call-gate code page;
2. the owned composite stack;
3. the protection-probe page;
4. the complete table/state arena;
5. the exact runtime-state/canary page contained at the end of that arena; and
6. the exact owned-root page contained in that arena.

A seventh identity requirement is the existing boot-stack guard exclusion. It
must agree with the canonical guard reservation and remain absent from both the
normalized mapping set and supplied page-table image. The composite code/gate
is RX, non-writable, and WB; its exact post-call return address must be inside
the same page at or after the frozen 3,830-byte composite prefix. The stack,
table/state arena, state/canary page, and root page are RW+NX/WB. The probe
starts RW+NX/WB and retains the exact planned RO+X mutation. A present W+X
range, historical purpose, `AdditionalRequired`, missing or duplicate role,
wrong allocation/reservation/index, partial containment, cross-role alias, or
guard mapping is rejected.

The planner may normalize adjacent or overlapping same-policy roles into one
containing range and OR their exact purpose bits. Therefore the whole arena,
state page, and root page may share a mapping index, but each binding must
still find its own exact purpose bit and exact allocation/reservation
relationship. Containment does not permit relabeling. `SerialStub`,
`AcpiScratch`, `FramebufferMetadata`, `BootHandoff`, the stage-two allocator
arena, and a loaded-image continuation page are not dereferenced during this
owned-root interval and cannot substitute for an exact role. Their omission
does not prove they are unnecessary to other boot phases.

The M01-030 aggregate host-harness markers are:

```text
TEST_START m01.030
CLROS_M01_030_READY
TEST_PASS m01.030
```

`CLROS_M01_030_READY` and `TEST_PASS m01.030` prove only host validation of
caller-owned role, geometry, containment, allocation, reservation, declared
identity-range, cache, and protection relationships. The source does not
allocate or map firmware pages, establish that modeled workspace bytes occupy
the reported physical arena, install/read CR3, materialize or invoke the
call gate, observe a return site, change CR3/PTE/TLB/RFLAGS state, enforce
W^X/NX or guard absence in hardware, execute under EFI/QEMU/OVMF, or advance a
completion gate. M01-031 supplies the separate host-only physical-arena
contract described below, and M01-032 supplies the host-only audited call gate
and post-return observer. M01-025 now source-wires those contracts into the
default-off stage-three adapter but adds no guest execution evidence. M01-033
through M01-035 form the immutable-text serializer prerequisite chain. The
later accepted M01-026 tuple now supplies bounded integrated real-QEMU evidence
for this behavior; the isolated plan and build result remain non-execution
evidence.

### Physical page-table arena contract (M01-031)

M01-031 closes the host-contract gap between the prepared flat page-table image
and one exact candidate physical arena. It adds sibling
`M01LivePagingArenaPlanStatus`/`M01LivePagingArenaPlanResult` and
`M01LivePagingArenaMaterializationStatus`/
`M01LivePagingArenaMaterializationResult` POD contracts without changing the
frozen M01-029/M01-030 preparation structures. The no-stdlib-compatible
`PlanOwnedTableArena(...)` entry takes a caller-selected, page-aligned candidate
`PhysicalAddress`, one physically valid `ConventionalMemory` descriptor, the
exact live-interval requirements, caller-owned requirement/range workspaces,
reservation evidence, the existing x64 encoding preconditions, and an explicit
iteration limit from 1 through 65. A firmware descriptor is validated from its
physical geometry: its virtual start may still be zero before UEFI
virtual-address-map setup, and valid nonzero UEFI attribute bits are preserved
rather than treated as a failure. The candidate and the complete eventual arena
must be contained by the descriptor and disjoint from every canonical
reservation.

The planner starts from the exact five-page lower bound (four hierarchy pages
plus one state page) and iterates the arena's self-identity mapping through the
validated caller limit of 1 through 65 attempts. For a converged result with
candidate `C` and exact table-page count `T`, the geometry is:

```text
root physical address      C
table interval             [C, C + T * 4096)
state/canary page          [C + T * 4096, C + (T + 1) * 4096)
exact allocation request   T + 1 pages
page-image requirement     T records
entry requirement          T * 512 entries
```

The final page is always exactly one state/canary page and is never a table
page. A descriptor may contain more space than the converged demand, but the
returned request and the separately validated `LoaderData` allocation record
must use the exact `T + 1` pages and preserve exact UEFI `Attribute` equality.
The successful plan retains the four fixed guard, code/call-gate, owned-stack,
and protection-probe requirements. Before materialization, those four records
are combined with the derived arena, state, and root records to rederive the
exact seven-requirement normalized range plan; every nested success field is
compared with the retained plan rather than trusting aggregate counts alone.
Host vectors independently cover an ordinary single-PT fixed point plus
candidates that cross 2 MiB, 1 GiB, and 512 GiB hierarchy boundaries. They
also cover a deliberately insufficient iteration budget, descriptor capacity
below/equal/above exact demand, physical-width and low-canonical limits, wrap,
alignment, descriptor escape, and reservation overlap.

Physical identity and host dereferenceability are separate assertions. The
materializer accepts an explicit caller-owned backing pointer and capacity in
addition to candidate physical address `C`; it writes image `i` at:

```text
arenaBacking + (pageImages[i].PhysicalStart - C)
```

It never casts `C` into a host pointer. Before the first write it validates the
complete page-image, entry, and destination spans; their declared capacities
and pairwise disjointness; exact root placement; strictly ordered, unique,
page-aligned image addresses; `EntryStartIndex == i * 512`; containment inside
the table interval; exclusion of the final state page; and the exact
`LoaderData` record including `Attribute`. A bounded full-hierarchy walk
reconstructs each leaf virtual address and rejects any mismatch in virtual
placement, physical identity, protection, cache policy, strict entry encoding,
or normalized-range cardinality before table bytes are committed. Missing,
duplicate, cyclic, unreachable, out-of-order, outside-arena,
state-page-targeting, aliased, truncated, overflowing, structurally malformed,
or semantically altered storage fails without committing table bytes.

`M01LivePagingArenaByteMode.EmitAndVerify` copies all 512 raw entries for every
accepted image and then performs an independent destination walk that compares
every entry and records deterministic source/readback FNV-1a64 aggregates.
`VerifyExisting` performs the same comparison without writing, which makes
post-emission corruption a first-class readback failure with exact page, entry,
expected-value, and observed-value evidence. Exact entry equality is
authoritative; the aggregate is additional deterministic evidence, not a
substitute for comparison. Both modes require a disjoint, caller-owned,
exactly 4096-byte state-snapshot scratch buffer. The materializer copies the
complete final state/canary page into that scratch space before table writes
and compares all 4096 bytes afterward, reporting the first mismatch index.
State FNV-1a64 values are diagnostic only and cannot substitute for this exact
comparison. Capacity-tail bytes are likewise not part of the table write.

Hostile tests cover forged nested-plan counts, masks, and hierarchy totals;
wrong leaf physical identity, protection, and cache policy; missing, duplicate,
cyclic, unreachable, misplaced, and malformed table images; allocation
attribute drift; null, misaligned, aliased, truncated, oversized, and wrapping
snapshot/storage spans; first- and last-entry readback corruption; exact state
scratch comparison; source-array nonmutation; and capacity-tail preservation.

The SharpOS snapshot supplied the test-only hierarchy-boundary cross-check:

```text
references/SharpOS-main/OS/src/Kernel/Paging/X64PageTable.cs
SHA-256 35503B543628E5A462073D498704050EEEF7D79DEB2B84F01CACA29714200BAC
```

The archive has no recoverable Git revision. M01-031 independently authors its
planner, POD evidence, failure taxonomy, materializer, and tests. It copies
neither SharpOS's global allocator/spare-list behavior nor its permanent
physical-pointer, firmware-table-clone, live-CR3, large-page-split, permissive
directory, or TLB-flush assumptions.

The M01-031 aggregate host-harness markers are:

```text
TEST_START m01.031
CLROS_M01_031_READY
TEST_PASS m01.031
```

A rejected group emits `TEST_FAIL m01.031 <reason>` instead of the ready/pass
pair. These markers prove exact fixed-point geometry, bounded host workspace
validation, offset-correct writes into caller-provided backing, complete
per-entry readback, deterministic aggregate evidence, and state-page
preservation. They do **not** prove that the backing pointer names the reported
physical range, that firmware allocation occurred, that a refreshed map still
owns the range, that the arena is identity mapped or current, that another
writer cannot modify it, or that a CPU installed or walked the root. The
contract calls no firmware allocator, executes no generated code, changes no
CR3/PTE/TLB/protection/RFLAGS state, runs no EFI/QEMU/OVMF path, and advances no
completion gate. M01-032 now supplies the accepted host-only call-gate repair;
M01-025 now source-wires the default-off stage-three adapter but adds no guest
execution evidence. M01-033 through M01-035 form the immutable-text serializer
prerequisite chain. The later accepted M01-026 tuple now supplies bounded
integrated real-QEMU evidence for this behavior; the isolated materialization
and build result remain non-execution evidence.

### Composite owned-CR3 interval contract (M01-027)

M01-027 closes only the host-model temporal gap left by M01-019. Its new
no-stdlib-compatible source materializes and reads back one relocation-free
Windows-x64 byte stream containing the complete future owned-root interval:

```text
validate caller-owned state, address ranges, and architectural mode
capture exact entry RFLAGS plus raw CR3 and CR4
cli; verify IF clear
clear CR4.PGE; verify readback
load the exact owned CR3; verify raw readback
read, update, and verify the continuation canary
verify the exact probe PTE is present RW+NX
write the same physical/cache leaf as the RO+X candidate
invlpg the exact probe virtual page once
verify committed PTE readback
verify and invoke the exact six-byte constant-return probe
verify its return value and canary
audit page zero through a bounded owned-root hierarchy walk
restore and verify exact firmware CR3 and CR4
prepare the exact saved RFLAGS operand for the final popfq; ret
return a status only after the common recovery tail
```

The probe transfer is the one permitted internal transfer in the stream. Its
entry, six bytes, PTE, virtual page, and return value are fixed and
cross-checked before use; it is not an arbitrary callback. The stream contains
no firmware, console, SerialIo, COM1, allocator, ACPI, framebuffer,
diagnostic, caller-selected, or other external call. It has no external
relocation or absolute address patch.

Before materialization, the M01-030 host path requires exact typed bindings for
the combined composite/call-gate code page, owned stack,
protection-probe page, complete page-table/state arena, exact contained
runtime-state/canary page, and exact contained owned-root page. The exact
post-call return address is inside the combined RX/non-writable/WB code page;
a loaded-image continuation page is not an owned-root dereference and cannot
substitute for it. Each dereferenced range must agree with allocation
geometry, canonical reservation, containing declared identity range, exact
purpose bit, cache, and protection; a purpose label alone is insufficient.
The stack and complete table/state arena are RW+NX, the probe begins RW+NX,
and no accepted mapping is writable and executable. The owned root and every
page-zero child table must remain within the arena. Historical `SerialStub`,
ACPI, framebuffer, handoff, allocator-arena, and `AdditionalRequired` aliases
are rejected by this path. `CrossBindsOwnedSerialStub`,
`CrossBindsOwnedMapIdentity`, and `CrossBindsDiagnosticState` remain false.

The success path commits the probe leaf from RW+NX to RO+X and leaves it RO+X.
A recoverable failure after exact committed-PTE readback also leaves that
inactive owned hierarchy's probe leaf RO+X. A failure between the write attempt
and committed readback, including `ProbePteAfterMismatch`, makes no final-leaf
protection claim. This fail-safe policy permits exactly one target `invlpg`; it
does not reopen a verified RO+X code page during recovery. Canary,
PTE-readback, probe-return, and page-zero failures converge on best-effort
firmware-CR3, CR4, and RFLAGS restoration. CR3 and CR4 restoration have
explicit hardware readbacks; a mismatch has a degraded failure status and
cannot be described as proven recovery. RFLAGS is different: while IF remains
clear, the transcript records the exact saved restoration operand, then the
frozen epilogue restores all nonvolatile state and places `popfq` immediately
before `ret`. No instruction after `popfq` reads hardware RFLAGS, and M01-027
does not claim such a readback. One indivisible interval means one stream with
no post-`cli` escape before the appropriate recovery tail; it does not claim
branch-free execution or immunity from NMI/SMI.

The entered stream rejects a captured Trap Flag and an active CR4.CET bit as
unsupported defense-in-depth. Those checks cannot establish safe entry:
single-step or supervisor CET/IBT can fault on the indirect transfer before
the stream reaches either guard, and the frozen six-byte indirect probe target
does not begin with `endbr64`. M01-025 now source-wires the caller-side
pre-transfer validation that TF is clear and supervisor CET/IBT is inactive,
but its source/build evidence does not show that validation executing.
M01-027 explicitly does not establish that prerequisite by itself.

Host materialization preflight is failure-atomic over caller storage, and
readback validation is read-only. The in-stream pre-`cli` checks return a
status without claiming an owned-root entry. Host tests freeze the byte plan,
internal branch targets and critical-operation order, Windows-x64 stack and
shadow-space discipline, nonvolatile/XMM policy, pointer/count/capacity and
alias rejection, provenance and permission failures, PTE/invalidation/probe
relationships, page-zero walk cases, recovery classification, malformed
readback behavior, and an exact zero-reference `NOSTDLIB_EFI` compilation
closure.

The frozen plan contains 3,830 instruction bytes and 732 audited instructions
inside one deterministically zero-filled 4 KiB page. Its 16 semantic records
bind the full-page FNV-1a64 value `F00446F61BE13CDD`. The independently frozen
SHA-256 values are
`C70C7BDD0B4E5F877074C2C881F67B079B5165D6F7D48EA1B8E3E2011B1F24BC`
for the complete page and
`12CADD81A9C5EFB9D47AAFEA49988D271656216140B87FF1AE3E0224567F5387`
for the instruction prefix.

The aggregate host-harness markers are:

```text
TEST_START m01.027
CLROS_M01_027_READY
TEST_PASS m01.027
```

These markers prove only materialization, exact readback, range binding, and
host structural audit. M01-027 itself did not add the leaf to `OS.csproj`;
closed M01-020 source-links it into the opt-in EFI compile closure. At that
evidence boundary no `EfiEntry` path invoked it. M01-025 now source-wires it,
but its accepted build did not execute the path and adds no live evidence. It
does not enable `Probes`, execute a firmware allocation callback or any
generated byte, change CR3/CR4/RFLAGS/PTE/TLB state, execute the probe, prove
hardware page-zero absence, run QEMU/OVMF, provide owned serial, or advance
`docs/completion-gates.md`. M01-019 owns only host-side preparation and
synthetic transcript composition, M01-029 owns the
preparation/execution-transcript split, M01-031 supplies physical-arena host
evidence, and M01-032 supplies the audited call-gate and post-return observation
contract described below. M01-025 now source-wires the live-allocation,
default-off invocation, and pre-transfer TF/CET boundary but adds no guest
execution evidence. M01-033 through M01-035 form the immutable-text serializer
prerequisite chain. The later accepted M01-026 tuple now supplies bounded
integrated real-QEMU evidence for this behavior; the isolated composite source
and build result remain non-execution evidence.

### Audited composite call gate and post-return observer contract (M01-032)

M01-032 closes the final host-contract defect identified by M01-028. The
existing relocation-free composite prefix remains frozen at 3,830 bytes and
732 instructions in `[0x000, 0xEF6)`. Ten zero bytes separate that prefix from
the gate. The independently measured gate occupies the complete 256-byte
(`[0xF00, 0x1000)`) tail of the same 4 KiB `LoaderCode` page and contains 74
instructions, so there is no post-gate tail padding. The combined stream
contains 806 instructions. Its only composite transfer is the base-independent
direct `call rel32` at `0xFC4`; the exact call-next return site is `0xFC9`,
the encoded displacement is derived solely from the composite and return-site
offsets, and no external relocation, absolute-address patch, callback, or
second composite call exists.

`M01OwnedCr3CompositeCallGateObservation` is a 160-byte version-1 caller-owned
POD. `PrepareCallGate(...)` accepts exactly one M01-029 preparation result, one
M01-027 composite preparation, exact allocation/reservation/identity evidence,
one exact M01-030 code/gate binding, a disjoint observation address, and a
nonzero boundary canary. It validates the single RX/non-writable/WB identity
page, checked entry/call/return geometry, four-level non-PCID mode, disabled
LA57 and supervisor CET, clear entry TF, exact IF/PGE policy, the full expected
code/gate/return-address fields, and all output/reserved fields as pristine
before arming the record. Rejection is a failure-atomic preflight: it does not
arm or partially rewrite the caller-owned observation.
`M01OwnedCr3CompositeCallGateResult` is a pointer-free 776-byte POD that carries
the preparation, byte/readback, and observation-audit evidence without
retaining caller pointers.

Before its first write, push, or call, the generated gate uses only volatile
registers and applies one common reject path to all of these in-stream checks:

- `RCX` (state) and `RDX` (observation) must be nonzero, low-canonical, and
  8-byte aligned.
- The packed state size/version/initialized header must be exact,
  `ExpectedSelfAddress` must equal `RCX`, and `TableStateArenaStart` must also
  equal `RCX`.
- Entry `RSP` must satisfy `RSP mod 16 == 8`, the checked lower-stack interval
  must provide at least 128 bytes, and the checked upper interval must provide
  at least 40 bytes.
- Without dereferencing `RDX` first, the gate derives `state + 4096` and proves
  the complete 160-byte observation lies inside that exact state page.
- The packed observation size/version/initialized/`Armed` header and
  `ExpectedSelfAddress` must be exact. Its call-count, observation-count,
  reserved padding, and complete completion-signature qword must still be
  pristine.
- A RIP-relative `lea` derives the actual `0xFC9` call-next address, which must
  equal the state's `ExpectedReturnAddress`.

Every such rejection returns `InvalidState` through the same stub, writes no
observation byte, and does not invoke the composite. These checks are defense
in depth, not a replacement for the broader host/external contract.

After those guards pass, the body preserves `RSI`, reserves 32 bytes of Windows
x64 caller shadow space, and biases `RSI` to `observation + 127`. The guards,
frame setup, lifecycle write, and call-count increment mutate arithmetic flags,
so the final pre-transfer sequence is exactly
`push qword [RDX+48]; popfq; call rel32`. It reloads the prepared full-RFLAGS
snapshot immediately before the direct call at `0xFC4`; the composite-entry `RSP` is
exactly 48 bytes below the gate-entry `RSP` (saved `RSI`, shadow space, and the
call return address). At `0xFC9`, the first instruction is `pushfq`; the
immediately following pop captures raw post-return RFLAGS before any
flag-mutating instruction. The gate then reads raw CR3 and CR4, stores the
composite `uint` result, rereads the boundary canary, records exactly one call
and one post-return observation, marks the lifecycle `Completed`, and writes
the completion signature as the final observation write before restoring its
frame and returning once. It does not touch XMM6-XMM15.

The gate cannot establish that either pointer is mapped, that the state root is
page aligned, or that the code page is currently mapped exactly RX,
non-writable, and WB. The host/execution boundary must also establish clear TF
and disabled supervisor CET before transfer; the intended four-level,
non-PCID, IF, and PGE policies; the observation's broader expected
code/composite/gate/return fields; and pristine non-count output/reserved
fields. The gate cannot read full live-entry RFLAGS without writing to the
not-yet-validated stack, so it does not self-validate that gate-entry RFLAGS
equal the prepared snapshot. The live caller must establish exact full-qword
equality, keep the state and observation storage mapped and exclusively owned,
retain gate/composite write access, and prevent external or concurrent
mutation of their validated input/header fields from readback through
transfer. Otherwise the pre-call `popfq` normalizes the mismatch and the
returned observation can appear internally valid without proving the original
entry state. The gate does not make NMI or
SMI delivery safe. `PrepareCallGate(...)` and later readback/audit represent
those requirements as host data evidence. M01-025 now source-wires the
invocation thunk plus RFLAGS, pre-transfer, and mapping validation, but its
accepted evidence does not execute them. M01-033 provides the reviewed
serializer object and M01-034 proves its exact linked PE/map/unwind identity
without invoking it. M01-035 now source-wires the immutable-text serializer
before every current first generated-stream transfer. The later accepted
M01-026 tuple observes the aggregate success of those four same-core calls in
the guest. It does not empirically exclude speculative code-store bypass or
prove cross-core or DMA-written-code publication.

`TryMaterializeCallGate(...)` materializes the unchanged composite prefix and
its 16 semantic records, emits the gate into the page tail, and requires the
ten-byte inter-stream gap to remain zero.
`ValidateCallGateReadback(...)` performs exact planned-byte, padding,
instruction-count, control-flow-position, and complete-page comparisons. The
host tests separately decode the emitted bytes to audit instruction boundaries,
branches, the direct call and return site, and Windows-x64 ABI discipline. The
frozen hashes are:

```text
composite prefix  F00446F61BE13CDD
call gate FNV-1a64 F903C5DE16D3A5B2
combined FNV-1a64  42CAF3B4A6C2119A
call gate SHA-256  8B2965E741AC40B725B898D7B2BB2FFBDF7CC4C50DF9AEB163C17485270BD88A
combined SHA-256   FA1B2EC2332CF70A5222B22B672758A1E948FD0F179FB91A16D7DD4280FD5631
```

`AuditCallGateObservation(...)` requires canonical readback evidence, a
returned terminal classification, exact prepared-state completion, and a
checked reconstruction of gate-entry `RSP` from composite-entry `RSP + 48`.
The reconstructed value must satisfy the same Win64 alignment and exact
128-byte lower/40-byte upper stack window as the emitted gate; the audit also
cross-binds the composite-captured original RFLAGS to the prepared observation.
It then requires the exact composite return, one call, one observation, the completion signature,
byte-unchanged boundary canary, raw post-return RFLAGS equality including the
expected IF state, and exact firmware CR3/CR4 equality including the selected
PGE policy. Timeout, reset, and triple-fault classifications remain
distinguishable failures. Default, null, short, excess, unaligned, overflowing,
overlapping, stale, forged, corrupted, repeated, policy-incompatible, and
canary-altered inputs fail closed.

The selective structural reference influence is:

```text
references/SharpOS-main/bootasm/BootAsm.Generator/BootAsmGenerator.cs
SHA-256 B24C79DCD38757DCBE243DC6C4C63C1AF628525F06C4F5D296B6AFB0C9387500
```

The local archive has no recoverable Git revision. The owner grant and current
upstream CC0-1.0 evidence permit selective use of SharpOS-authored source, but
M01-032 adapts only the C#-authored deterministic-template and explicit-offset
structure. It independently authors the fixed encoder, statuses, PODs,
control-flow/ABI audit, and hostile tests. It copies neither the Roslyn/Iced
reflection generator nor its sentinel-hole scanner, fallback skeleton, runtime
absolute patches, Iced implementation, binary, or third-party dependency.

The M01-032 aggregate host-harness markers are:

```text
TEST_START m01.032
CLROS_M01_032_READY
TEST_PASS m01.032
```

A rejected group emits `TEST_FAIL m01.032 <reason>` instead of the ready/pass
pair. These markers prove deterministic one-page composite/gate layout, checked
exact call-next binding, failure-atomic preflight, exact compare/readback plus
independent test decoding of the byte stream, full-page hashes, and validation
semantics over the caller-owned observation POD. The host tests invoke neither
the gate nor the composite. They do not observe processor RFLAGS, CR3, or CR4;
change CR3, CR4, PTE, TLB, page-protection, or interrupt state; prove mapped
pointers, a page-aligned state root, an effective RX/non-writable/WB identity
mapping, live TF/CET/LA57/PCIDE/IF/PGE preconditions, or NMI/SMI safety; call
firmware; execute under EFI/QEMU/OVMF; or advance a completion gate. M01-025
now source-wires the default-off live invocation but adds no guest execution
evidence. M01-033 through M01-035 form the reviewed and closed immutable-text
serializer prerequisite chain. The later accepted M01-026 tuple now supplies
bounded cumulative fresh-artifact real-QEMU evidence; the isolated call-gate
host tests remain non-execution evidence.

### Default-off stage-three EFI source wiring (M01-025 build evidence)

M01-025 wires the accepted M01-024, M01-027, M01-029, M01-030, M01-031, and
M01-032 contracts into the third nested opt-in `EfiEntry` path. The source
profile is enabled only when all three symbols are present:

```text
M01_LOADED_IMAGE_STACK_PROBE
M01_LIVE_ALLOCATOR_PROBE
M01_OWNED_PAGING_PROBE
```

The ordinary all-false profile and the two earlier profiles retain their
existing control flow and marker contracts. The new source path is one-shot
and stage-two-gated. It records authoritative firmware ownership immediately
after each `AllocatePages` call: `EFI_SUCCESS`, rather than address
truthiness or later validation, owns the returned code, probe, or table
allocation. This is intentional because firmware can successfully return an
address that CLROS subsequently rejects as zero, misaligned, overflowing,
overlapping, or otherwise outside the issue contract.

Every one of the 21 pre-entry failure sites passes the complete current
ownership tuple into one fail-closed cleanup function. Cleanup attempts the
fixed reverse allocation order, table then probe then code, with the exact
requested page count for each owned range. A failed `FreePages` call does not
suppress either later cleanup attempt. The path does not free those ranges
after the generated interval has been entered because their contents and
returned observation are then part of the terminal execution contract.

The M01-025 host suite freezes the three allocation call sites, immediate
ownership assignments, validation ordering, all 21 failure calls, exact
cleanup arguments and reverse order, marker/failure suppression, one-shot
state, and representative bypass mutations. Its source/control-flow cleanup
oracle covers 16 allocation/map/validation scenarios under all eight
combinations of table, probe, and code free failures: 128 deterministic model
evaluations. These are evaluations of the source and cleanup model. They are
not calls through a fake EFI adapter, not executions of any firmware callback,
and not guest evidence.

The accepted aggregate markers each occurred exactly once:

```text
TEST_START m01.025
CLROS_M01_025_READY
TEST_PASS m01.025
TEST_PASS host.tests
```

The final integrated full harness ran from
`2026-07-28T11:25:45.8776369Z` through
`2026-07-28T11:34:24.2000179Z` and exited zero. Its stdout SHA-256 is
`E9377EECB18F71A8EC329ACAA14F049757A4FD2887B76C85F6F3065B7BE24A1F`;
stderr was empty with SHA-256
`E3B0C44298FC1C149AFBF4C8996FB92427AE41E4649B934CA495991B7852B855`.
There was no `TEST_FAIL` line and none of the named M01-019, M01-021,
M01-030, or M01-031 diagnostic markers:
`M01_021_DIAGNOSTIC`, `M01_019_BINDING_DIAGNOSTIC`,
`M01_019_NOSTDLIB_COMPILE_DIAGNOSTIC`,
`M01_030_PREPARATION_DIAGNOSTIC`, and `M01_031_ARENA_DIAGNOSTIC`. Both the
solution build and direct host-project build also exited zero with zero
warnings, errors, or stderr.

The all-switch NativeAOT EFI publish exited zero with no stderr. Verification
accepted a PE32+ x64 EFI application (subsystem 10), entry RVA `0x49c50` in
`.managed`, the same-build `EfiMain`, and absent ordinary runtime imports.
The accepted hashes were:

| Build output | SHA-256 |
| --- | --- |
| `BOOTX64.EFI` | `0A674D1628D48C478D616F43548DA18BC9673D4BECBEC0D875C7ECDA05F646CB` |
| link map | `B3A9772F07B579986DF4F3B726ADC1AB378FDE4F79E80B9B497DEAA166C11037` |
| linker response | `0108286539031918F3576A3C9759803740D9E2DCB1A26E3E244E882C56E82F1F` |
| link evidence JSON | `405CF854F218423A0283F0DFCF1148FF7E7944F8AAC9F360FC2687A37884AA62` |
| OVMF input | `33090CC07675BAA5190D9F1E84BF5176B33BCBFA9BACAC522961150CDB6DBB2A` |

The recorded toolchain was MSVC linker `14.51.36252.0`, Windows SDK
`10.0.28000.0`, ILCompiler `10.0.8`, and QEMU `11.0.50`. Naming validation and
the QEMU dry run both exited zero with empty stderr.

This evidence proves compilation, source closure, PE shape, stable marker
contracts, and deterministic host validation of the stage-three wiring. It
does **not** prove that firmware granted, described, or reclaimed an
allocation; that an EFI callback or generated byte executed; that page tables
occupy the reported physical addresses; that CR3, CR4, RFLAGS, a PTE, or the
TLB changed; that page zero was absent in hardware; that W^X/NX held live; or
that the path ran under QEMU/OVMF. It does not advance a completion gate.

M01-025 did not claim immutable-text first-publication serialization or closure
of the speculative code store bypass (SCSB) gap. M01-033 provides the
deterministic C# AMD64 COFF executable-text generator and exact object contract,
and M01-034 provides the reviewed canonical linker integration and final
PE/map/unwind verification without executing the symbol.

### Immutable instruction-publication epoch wiring (M01-035 source/build evidence)

M01-035 adds one EfiEntry-owned primitive initialization flag and one shared
publication mask under the existing loaded-image-stack opt-in. The path rejects
stale or repeated initialization, explicitly starts from zero, and permits only
the successful one-shot sequence `0 -> 1 -> 3 -> 7 -> 15`. The state is not a
caller local: the owned-stack continuation reads the same root
`EfiEntry` field at mask `3` before it observes the continuation `RSP` or enters
stage two.

The four checked direct `X64InstructionPublication.Serialize()` calls are:

1. After the stage-one stack-probe stream has been written, read back, and
   installed, before `PrepareAndTransfer` can enter that stream.
2. After the stack-switch stream has been prepared and read back, but before
   `switchEntry` is obtained or invoked; success advances the shared mask from
   `1` to `3` before the one-way transfer.
3. Inside `CaptureArchitecturalPreflight`, after the paging-preflight and PAT
   reader are written and read back and both entry addresses are bound. Success
   advances `3` to `7` and is immediately followed by
   `preflight(observation)`, then the PAT reader; a caller-side barrier after
   this method returns is too late.
4. After the final composite/call-gate/protection-probe materialization,
   readback, and transfer binding and after the existing `M01_PRE_CR3` firmware
   output. Success advances `7` to `15` and is immediately followed by the
   audited call-gate transfer.

Every pre-execution wrong cookie or epoch-state mismatch emits
`M01_INSTRUCTION_PUBLICATION_COOKIE_FAILED` before cleanup, return, or terminal
spin and prevents first execution of that epoch. Only after the call gate
returns, the post-return observation audit and continuation canary pass, and
the shared mask is exactly `15` does the source emit
`M01_INSTRUCTION_PUBLICATION_EPOCHS_VERIFIED`, immediately before the existing
`M01_POST_CR3` line.

`tests/OS.HostTests/M01InstructionPublicationEfiEntryTests.cs` checks the four
compile-profile call counts, exact owner methods and ordering, root-state
lifetime, byte-authored marker text, immediate success windows, and mutation
rejection for missing, duplicate, early, late, branch-only, ignored, synthetic,
wrong-cookie, wrong-binding, dynamically generated, partial-mask, reset,
reconstruction, and substitute-barrier forms. The all-three-switch NativeAOT
build reuses M01-034's immutable serializer and final PE/map/unwind verifier.

This evidence is intentionally static and host-side. No M01-035 QEMU smoke,
guest boot, or QEMU dry-run smoke workflow is run; the mandatory EFI preflight
only queries `qemu-system-x86_64 --version` and `qemu-img --version` for tool
discovery. Neither new marker is claimed from a guest, and no returned runtime
cookie is observed. It does not empirically exclude speculative stale-instruction
execution, SCSB, cross-core or DMA publication hazards; prove hardware
execution, live CR3/PTE/TLB behavior, restored or systemwide W^X; or advance an
M01 completion gate. The later accepted M01-026 tuple now supplies bounded
integrated real-QEMU evidence for the exact four-epoch source path; it does not
retroactively turn this isolated host/build result into processor evidence.

### Cumulative pre-EBS live-paging QEMU proof (M01-026)

M01-026 was executed from clean, pushed source commit
`8f1edea17f2de425520e239af58d37aa3b182146`, tree
`6b5359e36293d770e3dc0d074b1baabf8d4fdbf8`. Its r16 host gate preserves
successful solution and host builds, naming/firewall and exact all-switch M01
dry-run checks, clean diff/status checks, and a complete 3,329-pass,
zero-failure host stream with SHA-256
`996E343DC72C34B6F2521C1E703A58BB98362242A3E894D48647057F139F13F3`.
The fresh real run used QEMU `11.0.50` (SHA-256
`B396EB9B669F6282EC60F0D46E6ADDCA8C669992E67A34365BE44CD3CB97C9A7`)
and OVMF SHA-256
`33090CC07675BAA5190D9F1E84BF5176B33BCBFA9BACAC522961150CDB6DBB2A`,
`-BuildEfi`, all three explicit M01 switches, the exact 42-marker required,
ordered, and unique list, the exact 25-marker issue failure list, and a
30-second guest timeout. The command ran from
`2026-07-30T15:36:47.5722979Z` through
`2026-07-30T15:37:49.8210616Z`.

The guest emitted every stage-one, stage-two, and stage-three line exactly
once and in order. `M01_INSTRUCTION_PUBLICATION_EPOCHS_VERIFIED` occurred
immediately before `M01_POST_CR3`; `CLROS_M01_LIVE_BOOT_MEMORY_READY` and
`OS_SMOKE_PASS` followed only after the post-return protection, invalidation,
constant-return probe, and page-zero checks. No issue failure marker,
`TEST_FAIL`, `OS_SMOKE_FAIL`, or `M01_OWNED_CR3_ENTERED` serial line occurred.
Live-interval evidence instead comes from the separately hashed caller-owned
PODs and stopped-QMP memory scan; post-return marker positions are not treated
as firmware writes while owned CR3 was active.

The source-bound tuple proves:

- the loader-owned image and separate `EfiLoaderCode` stack-switch,
  composite/CR3/invlpg, and protection-probe pages;
- separate `EfiLoaderData` guard/stack, allocator, and page-table/state arenas,
  with post-allocation memory-map recapture and canonical reservation
  validation;
- one arena-confined metadata allocation/free/double-free-rejection cycle;
- exact owned page-table and call-gate materialization;
- validated four-level, non-PCID, non-LA57 paging prerequisites;
- a callback-free reversible owned-root interval with owned CR3
  `0x0000000000100000`, distinct from firmware CR3
  `0x000000000F801000`;
- exact firmware-CR3, raw-CR4 (`0x0000000000000668`), RFLAGS/IF
  (`0x0000000000000246`), and PGE restoration;
- continuation-canary validation and runtime-observed same-core success of all
  four current instruction-publication epochs;
- one scoped RW+NX-to-RO+X transition with exact `invlpg`, PTE readback, and
  constant-return execution; and
- no present writable-and-executable leaf and no virtual page-zero mapping in
  the owned hierarchy during that interval.

The fresh EFI and independent canonical-path verifier both passed the exact
M01-034 PE/map/serializer/unwind contract. The EFI, serial,
raw-observation, QMP-transcript, and evidence-manifest SHA-256 values are
`76F2B404E91EB5E62CAD80B797B936BA3D3D39D5B0577889BB8459AF1B55C47C`,
`CC51100244A8A20865DB9D16CE5D73B0930F43EA98835DFA57027A00B126C661`,
`6FFEDB0B5998A930E614E3EC0EE68E2B444439AE686A9412F3206760D257F933`,
`0A0A6CC6985593D798BBD158F46D5CE9B7D84A57928EC2781BFC3CDD643AC14E`,
and
`94A28FE683FC068C88F42AB806B3E9A06DB5467FD35488B6ADF397AC8F8A8188`.
The manifest binds 47 nonempty artifacts, 10 tool identities, 44 required
markers, 26 forbidden markers, and all 61 observed serial markers; every
artifact was independently rehashed. The stale intermediate run-state ledger
was excluded and replaced by final-state ledger SHA-256
`89A1A89B600698618E9DEDDE91FB1FABFD5B7B7993FA023AE8C5329C9B340F07`.
The generated ignored run-output package is under
`artifacts/logs/m01-026-final/8f1edea1-20260730T145353Z-r16/`; its manifest
additionally binds external build and tool inputs by absolute path and hash.
`done/step890.md` is the durable current-source ledger; `done/step888.md`
remains immutable historical r12 evidence.

This satisfies only the M01 local-QEMU pre-`ExitBootServices` technical exit.
It does not prove restored firmware mapping policy, a permanent or systemwide
W^X/page-zero policy, a general or post-EBS allocator, permanent CR3 adoption,
real `ExitBootServices`, owned COM1 or post-firmware diagnostics,
IDT/GDT/TSS/IST installation, controlled `#PF`/CR2/error-code capture,
NMI/SMI safety, SMP TLB shootdown, cross-core or DMA-written-code publication,
empirical exclusion of speculative code-store bypass, devices, storage,
networking, applications, hosted .NET, Minimal API, Kestrel, libvirt/provider
support, CloudReady status, or production readiness. The deliberate null read
and captured fault remain M02-014.

### Fixed M02 page safe-entry QEMU proof (M02-017)

M02-017 was executed from clean, pushed implementation commit
`cf8ce63c37a9c62a0f8f736adda14b6bd3e19a9a`, tree
`d18f520cee19aa0c89dfb41f326c92afd0284c02`. The default-off integration adds
the fixed M02 page to the existing M01 stage-three allocation and call gate; it
does not add or bypass a `BootSequence` phase. A complete immediately preceding
host stream passed with 3,346 starts, 3,347 passes including the sole terminal
host pass, zero failures, and empty stderr.

The fresh all-four-switch QEMU/OVMF run materialized the exact 4,096-byte page,
verified FNV-1a64 `F85393074A792B1E`, and preserved the accepted M01 marker and
raw-observation envelope. During the inherited temporary owned-root interval,
the call gate targeted only offset zero. The six-byte `mov eax,0x4F534F4B;
ret` entry returned cookie `0x4F534F4B`; no port or value argument was supplied.
The post-return audit emitted
`M02_SAFE_PROBE_EXECUTED offset=0 return=0x4F534F4B` and the final ready line
reported `temporaryOwnedRootRx=true portIoExecuted=false` only after the
inherited restoration and readback checks.

All 48 cumulative M01/M02 markers occurred once and in order; all 34 forbidden
markers were absent. The 44-key QMP sidecar independently confirms firmware
CR3, raw CR4, and RFLAGS before/after equality and sixteen bounded `pmemsave`
chunks. EFI, serial, summary, raw-observation, and QMP-transcript SHA-256 values
are
`D30B508AC06F592756C36ABD2B5CFAE2469ED31DF234BFDCD37134A7DBEB4EA5`,
`6F223E27E4180855DA64F4785C13B9B44649F0C8EF1590AA9B65A89CEF508AD0`,
`B934A1E5C2E2EEB095BDE1EE374A0B129DE99CE203AAA7F992E1F0CAC8137B0C`,
`D764684446C21497EE3816FF170F46E2B73F8228B429242B11BDAB47BCA16EF5`,
and
`9C2C8EBC64BCA601CE6CDB2D231DA784EE9555B546FB7426A46A433B71353965`.
The ignored evidence root is
`artifacts/logs/m02-017-final/cf8ce63c-20260803T211323Z-r1/`, and
`done/step897.md` is the durable ledger.

This proof does not execute the `Out8` bytes at offset 16 or the `In8` bytes at
offset 32. It proves no privileged I/O, COM1 ownership, permanent page-table
adoption, descriptor-table installation, controlled fault, real
`ExitBootServices`, hardware/provider behavior, hosted .NET, Kestrel,
CloudReady status, or production readiness, and it changes no numbered
completion-gate status.

### Out8 ABI repair and COM1 configuration QEMU proof (M02-004)

The historical M02-017 evidence above remains accurate for the old host-only
page image it recorded. The first M02-004 privileged invocation exposed that
the old `66 89 CA 88 D0 EE C3` order copied `CX` into `DX` before preserving
the Windows x64 byte argument in `DL`. The production C# emitter now writes
exactly `88 D0 66 89 CA EE C3`, so `AL` receives the requested byte before
`DX` is repurposed for the port. `In8`, the fixed offsets, lengths,
instruction-byte count, and occupied extent are unchanged. The current page is
bound to FNV-1a64 `982E5B7E3EE58E56` and SHA-256
`B45339EB93D346D9FE6B32700F227CFB367AB1D10970DD6655A173AFE54705E2`.

The default-off M02-004 integration runs only after the accepted safe-entry,
instruction-publication, firmware-CR3, CR4/RFLAGS, W^X, TLB, and page-zero
audits. It reuses the checked M02-003 binding, calls the status-coded COM1
backend's default configuration once, and requires seven successful writes in
order: IER `0x00`, LCR `0x80`, divisor low `0x01`, divisor high `0x00`, LCR
`0x03`, FCR `0xC7`, and MCR `0x0B`.

A fresh real QEMU/OVMF run observed all 50 cumulative markers exactly once and
in order, no forbidden marker, and one exact
`CLROS_M02_004_READY configurationWrites=7 bindingReady=true guestPortIo=true guestIn8=false markerTransport=uefiSerial postExitBootServices=false`.
The 485,888-byte EFI artifact and 2,753-byte serial log SHA-256 values are
`61CA5D6879DA1AE7DC0A6938AA61AD64DCEFBD61AA937C0DF2D8C3BF0809820E`
and
`E452EF725FAF34775EE00C310E86245F217CA2638926B70B3194C711A0F2876D`.

This adds no `BootSequence` phase and proves no `In8`, serial payload written
through the owned backend, post-`ExitBootServices` diagnostics, physical
hardware/provider behavior, hosted .NET, Minimal API, Kestrel, CloudReady
status, production readiness, or numbered completion-gate advancement.

### Live-paging feasibility and prerequisite recovery (M01-028)

M01-028 records a backlog and evidence-boundary repair; it implements no paging
behavior. A source-level audit found that M01-025 cannot safely cross the live
boundary within its current `EfiEntry`-only production scope:

- M01-019 constructs page-image metadata and raw entries in a flat caller-owned
  model whose storage is required to be disjoint from the `PageTableArena`.
  Treating its root address as a loadable CR3 would therefore not establish
  that the modeled entries occupy the physical table pages.
- The historical identity contract still aliases the stage-one stack-switch
  page as `SerialStub` and requires ACPI-scratch and framebuffer-metadata
  records that are not live interval dependencies and are not present as
  separate owned records in the stage-one/stage-two ledger. Synthetic labels or
  overlapping reservations cannot repair that provenance.
- The table allocation must be sized before the firmware call, while its exact
  fresh-hierarchy count depends on the selected fixed physical address and the
  final identity requirements. Allocation therefore needs a bounded-capacity,
  fixed-address sizing contract before physical table materialization.
- The composite stream checks an exact call-return RIP, which an ordinary
  NativeAOT C# function-pointer call cannot name. It also ends with the saved
  RFLAGS operand followed by `popfq; ret`; consequently, an exact hardware
  RFLAGS observation after restoration must occur in an audited immediate
  post-return gate and cannot be claimed as an in-stream readback.
- M01-019 accepts caller-supplied CR3, protection, invalidation, and probe
  transcripts as host-model evidence. A live adapter must consume preparation
  evidence before execution and audit actual returned POD afterward; it must
  not manufacture a successful synthetic transcript to satisfy the host
  aggregate.

The repair is intentionally serialized:

1. M01-029, **Split M01 paging preparation from synthetic execution
   transcripts**, provides the first-class caller-owned POD preparation-only
   result/state/evidence boundary. M01-030, **Replace historical paging
   identity aliases with exact live-interval bindings**, supplies that
   preparation path and the composite validator with exact host-only roles.
   The historical `Execute`/`ExecuteModelOnly` surface remains model-only and
   cannot promote caller-supplied transcripts into that prepared state.
2. M01-031, **Materialize and read back the owned physical page-table arena**,
   now supplies the bounded-capacity/fixed-address sizing contract, exact
   table-pages-plus-one-state-page geometry, caller-backing materialization,
   and independent readback evidence.
3. M01-032, **Add an audited composite call gate and post-return RFLAGS
   observer**, now supplies the exact same-page call-next binding, gate
   materialization/readback, Windows-x64 audit, and returned-observation POD
   semantics after M01-031.

The accepted M01-024, M01-027, M01-029, M01-030, M01-031, and M01-032 host
prerequisites enabled M01-025's default-off source/build integration. M01-025
did not promote that success to execution evidence. M01-033 through M01-035
now form the reviewed and closed P0 immutable-text first-publication
serialization prerequisite chain. The later accepted M01-026 tuple now
supplies the first cumulative fresh-artifact real-QEMU/OVMF proof. No
M01-028 through M01-035 host marker or build result independently advances a
completion gate.

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

Platform firmware RNG guard coverage is host-test only and uses an injected random-byte source. It does not implement a PRNG, entropy pool, TLS, cryptographic API, or post-`ExitBootServices` randomness.

[Step978](../done/step978.md) adds the M03-003 host retention contract on top
of that guard. It accepts one 32- or 64-byte result into disjoint caller-owned
scratch, checks status and exact length, copies and privately compares the
retained bytes, clears the requested scratch slice, then publishes a POD length.
Complete scratch/destination/publication aliases and repeated publication are
rejected before the source call. Admission and source failures preserve the
retained destination and publication; a failed source may change scratch, and
a later copy/readback failure makes no rollback promise. The complete new and
existing RNG/firmware-denial modules pass 27 starts/28 passes/zero failures.
This is injected-provider host evidence only: actual EFI RNG discovery,
`GetRNG`, missing-provider guest behavior and post-firmware use remain pending.
No seed bytes or digest are emitted, and scratch clearing is not a physical
secure-erasure claim. #60 remains open.

The separate default-off EFI entropy profile in [step980](../done/step980.md)
uses the original system-table boot-services pointer to locate
`EFI_RNG_PROTOCOL`. It requests exactly 32 bytes once, with the default
algorithm selector, into a guarded 64-byte scratch payload. EFI `GetRNG`
returns a status, not a returned-byte count. The adapter preserves that
full-width status and reports the number of calls separately.

The adapter admits disjoint complete state and buffer extents before writing.
It copies each requested byte into a separate guarded retained payload, reads
it back, clears that scratch byte, and verifies both observations. Guard and
tail checks precede publication of the retained length. Failed acquisition
leaves retention unpublished; a late copy/readback failure makes no rollback
promise. Zero and repeated payload bytes are accepted without a quality test.
Diagnostics contain status, call counts and lengths, never seed bytes, hashes,
digests or a classification of uncleared requested scratch.

The complete five-module focused run and both actual NativeAOT publications
pass. Independent offline review confirms the emitted firmware ABI, guarded
storage, actual copy/readback/clear sequence, publication order and terminal
failure loop. These results do not establish a real firmware provider or a
missing-provider guest outcome. Full common validation and fresh guest
evidence remain separate. This is pre-firmware-exit retention only; no
post-exit random generator, entropy-quality guarantee or secure-erasure claim
follows. #541/#60 remain open and #61 remains blocked.

[Step986](../done/step986.md) accepts the subsequent clean `8942ab7` full
suite and three fresh QEMU/OVMF captures. The provider-present path discovers
the real protocol, calls GetRNG once successfully, retains 32 bytes, clears 32
requested scratch bytes and publishes last. The same enabled EFI image with
the missing-device configuration observes EFI_NOT_FOUND (9223372036854775822),
zero GetRNG calls, unchanged retained storage and no publication. Its exact
failure suffix is followed by 764.127 ms of measured quiet while QEMU remains
live, then owned termination and EOF. The ordinary runner still classifies
that captured failure as failure.

These results satisfy the bounded pre-exit entropy-retention boundary for
#60/#541 after reviewed integration. They permit #61's typed adapter work;
ordering, retry and the first actual ExitBootServices transition remain later
slices. No seed value or derivative is logged, and no randomness quality,
post-firmware service or physical secure-erasure guarantee follows.


### M03-004 typed one-attempt adapter

[Step988](../done/step988.md) adds the host-tested
`TypedNoStdlibExitBootServicesCallAdapter`. `Prepare` admits only empty
caller-owned POD state, an explicitly available firmware mode, a nonnull
`delegate* unmanaged<void*, ulong, ulong>` and a nonnull opaque image handle.
`Attempt` rechecks admission, stores the complete map key and marks the state
in flight before one indirect call. Every returned UInt64 status consumes the
attempt; adapter refusal remains distinct from firmware success or failure.
Zero and high-bit keys are valid. Refused admission, reuse and callback reentry
leave the retained state unchanged and issue no additional callback.

The complete new module and five existing firmware/map/exit-plan modules pass
53 starts/54 passes/zero failures. Actual host layouts are 40-byte state and
16-byte result; warmed managed attempts allocate zero bytes. The actual
four-source no-stdlib compile has zero AssemblyRefs and is not executed.
Callers must retain and serialize the state and supply valid callable authority;
copying or resetting state, hostile mutation and arbitrary-pointer executability
are outside the contract.

This host evidence alone does not establish the emitted EFI ABI or a firmware
exit. The adapter leaves global firmware mode unchanged. Step1003 below accepts
the separate controlled native/QEMU supplement and common integration gate;
#62, #63 and #64 retain final-map ordering, stale-key retry and the first actual
ExitBootServices transition respectively.

[Step991](../done/step991.md) adds the separately claimed #546 default-off EFI
integration. Four retained states exercise zero, high-bit and all-bit keys,
full-width statuses, consumed-state refusal and same-state reentry. A private
counter covers every callback entry, including an orphan entry before any
active-state check. The actual mapped callback tests and fresh default/enabled
EFI publications pass. Eight focused managed modules pass 115/116/0; the
complete new integration module passes 5/6/0. Full native review, the common
suite and fresh QEMU evidence were pending at that milestone. The READY record explicitly
says `controlled=true realExitBootServices=false`; it never claims a firmware
exit or authorizes a global mode change.

[Step1003](../done/step1003.md) accepts #61/#546's controlled adapter after the
combined full suite (3787 starts/3788 passes/zero failures), emitted ABI review,
and fresh default/typed-EBS QEMU captures. All four exact keys/statuses,
consumed-state rejection and the bounded reentry case appear in the 30-line
controlled transcript. Actual callback and retained storage addresses match
the reviewed native image. This releases #62's final-map window; retry and
the original firmware exit remain #63/#64. No global firmware transition occurs.

Existing guard markers:

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
.\scripts\run-qemu-smoke.ps1 -BuildEfi -EnableAcpiPhysicalDiscoveryProbe -MarkerPreset acpi -EfiArtifact artifacts/efi/BOOTX64-ACPI.EFI -Image artifacts/images/os-acpi.raw -SerialLog artifacts/logs/qemu-smoke-acpi.log -SummaryLog artifacts/logs/qemu-smoke-acpi.summary.log -Qemu "C:\Program Files\qemu\qemu-system-x86_64.exe" -OvmfCode "C:\Program Files\qemu\share\edk2-x86_64-code.fd" -TimeoutSeconds 30
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

The default Gate 2 QEMU tuple proves only that OVMF can load and run the EFI
application, the tested firmware path can return a memory map, and
firmware-visible output reaches the serial log. The separate opt-in M01-022
tuple adds only the loaded-image/owned-stack facts below, while the separate
opt-in M01-024 tuple adds only the explicit allocator-arena metadata cycle. The
accepted all-switch M01-026 tuple adds the exact reversible pre-EBS owned-root,
CR3/PTE/TLB, restoration, immutable-publication, protection-probe, and
page-zero observations bound below; it satisfies only the documented local-QEMU
technical exit for M01. None proves real `ExitBootServices`, owned drivers, a
general memory manager, storage, networking, app loading, hosted .NET 10,
Minimal API, Kestrel, cloud readiness, or production readiness; use
`docs/completion-gates.md` for the full claim boundary.

The M01-017 stage-one plan and coordinator retain extensive host-tested
coverage. M01-021 consumes their source-linked bytes and static transition
surface from `EfiEntry`, but only in an explicit
`M01_LOADED_IMAGE_STACK_PROBE` build; `BootSequence.cs` remains excluded from
the EFI project and the default path is unchanged. M01-022 now adds one
source-bound QEMU/OVMF observation of live `LoaderCode`/`LoaderData`
allocation, refreshed-map and four-record ledger validation, switch-stub
execution, stack transfer, contained `RSP`, stage-one markers, and continuation
into the unchanged boot marker plan. It still does not prove guard-page
inaccessibility, a writable-to-executable protection transition, NX/W^X,
post-`ExitBootServices` ownership, a reusable allocator, or behavior on
firmware other than the recorded OVMF tuple.

The M01-024 dual-probe tuple continues from that exact stage-one state,
acquires one two-page `EfiLoaderData` arena, recaptures the map, validates the
exact five-record ledger, performs one arena-confined 4 KiB metadata
allocation, restores the allocator accounting through the exact inverse, and
rejects the repeated free as `FreeRangeNotAllocated`. All 21 cumulative
markers appeared exactly once and in order. The default path still does not
invoke this chain, and the accepted run does not access or zero page bytes,
grant authority over arbitrary conventional memory, provide arbitrary-order
free or ABA safety, create a managed heap, install paging, enforce W^X/NX, or
promote the retained arena into a general or post-EBS allocator.

The M01-011 page-table entry contract is host-tested when considered in
isolation. Its synthetic no-stdlib compiler fixture proves source compatibility,
and closed M01-020 links the same source into the opt-in EFI closure. The
contract's precondition and PAT tokens remain caller observations rather than
self-read registers, and the codec alone owns no table storage, page authority,
CR3/TLB transition, or page-fault path. The accepted M01-026 tuple is the
scoped integrated exception: its surrounding adapter supplies raw preflight
observations, materializes the fixed owned hierarchy, and executes the bounded
transition/probe path under QEMU. Its canonical table pointers still remain
permissive ancestor masks for the fixed mixed-leaf policy, not proof of
arbitrary or systemwide W^X/NX enforcement.

The memory-map snapshot is now copied from the firmware-allocated buffer into a fixed static OS-owned buffer before the smoke marker is emitted. The firmware source pointer is still retained only as provenance in `BootHandoffRecord`.

`UefiMemoryMapCapture` now owns the EFI boundary's `GetMemoryMap`, `AllocatePool`, copy-plan validation, and owned-copy mechanics. `EfiEntry` still owns firmware-visible markers and handoff record population. The live EFI path materializes compact `BootMemoryRegionRecord` entries into static storage, but this default-path snapshot is not an exit credential. The separate M03-007 profile performs its own final capture and bounded stale-key retry.

`UefiMemoryDescriptorReader` is host-tested against synthetic owned byte buffers. The live EFI artifact uses the narrower no-stdlib-safe `EfiMemoryMapNormalizer` and `BootMemoryRegionMaterializer` pointer paths instead of the span-based host reader.

The captured map key is valid only for the memory-map instance returned by `GetMemoryMap`. Later boot-services calls can invalidate it, so the explicit M03-007 path uses its bounded final-capture/retry window and cannot assume an earlier logged key remains valid after additional firmware calls.

`UefiExitBootServicesPlan` is plan-only. It models when a fresh memory map is required and when stale map-key failure should retry, but it does not call `ExitBootServices`, does not mutate `FirmwareServicesGuard`, and is not firmware-shutdown evidence.

`PlatformFirmwareExitBootServicesResult` is also not firmware-shutdown evidence. It models a firmware-call result after guard and function-pointer checks; the accepted M03-007 transition has its own source-bound retained callback authority. The host result contract alone is not that live proof.

`IPlatformSerial`, `GuardedPlatformSerial`, `Com1PlatformSerialTransport`, and `OwnedSerialDiagnostics` are host-tested boundary/formatter/adapter contracts only. They distinguish firmware-backed and owned transports under UEFI-gone simulation and write to injected or fakeable transports, but they do not perform real COM1 port I/O, call `ExitBootServices`, or prove live serial output after firmware shutdown.

`EfiConfigurationTableScanner` is host-tested and QEMU-marker-tested through `EfiMain`. The default path carries the selected pointer through the typed handoff as provenance only. The explicit ACPI physical discovery probe dereferences and validates table bytes before `ExitBootServices`, but that probe is not part of the default smoke path.

`AcpiRsdpValidator` is host-tested against synthetic byte buffers and is mirrored by no-stdlib validation in the explicit ACPI physical discovery probe. The probe proves pre-`ExitBootServices` RSDP bytes only; it does not establish post-firmware physical-memory rules.

`AcpiMcfgDiscovery` has host-tested table-snapshot and physical-memory child-table surfaces, and the explicit ACPI probe now proves live QEMU/OVMF MCFG byte discovery before `ExitBootServices`. It still does not map ECAM, enumerate PCI devices, size BARs, bind drivers, or prove MMIO access.

`IPlatformFramebuffer` is host-tested through a guarded metadata double only. It records base address, byte length, width, height, stride, pixel format, firmware mode, nested source status, required byte length, and firmware status; it does not query GOP, map or write framebuffer memory, render text, or prove output after firmware services are gone.

`EnableExitBootServicesFreshMapProbe` is intentionally disabled by default. The explicit probe build reacquires and validates a fresh memory map, then emits `UEFI_EBS_CALL_SKIPPED`; it is not a live `ExitBootServices` attempt and does not prove post-firmware execution.

`BootHandoffInfo`, `MemoryMapInfo`, `BootMemoryRegionPlanner`,
`BootReservationSubtractor`, `BootPageAllocatorPlanner`,
`BootPageFreeListBuilder`, `BootPageAllocatorStateInitializer`,
`BootPageAllocatorStateMaterializer`, `BootPageAllocatorMutation`,
`BootPageAllocatorReadinessComposer`, `NativeAotHeapBootstrapPlanner`,
`NativeAotClassConstructorRunnerPreflight`,
`NativeAotClassConstructorWorkItemPlanner`,
`BootSequence.RunBootPageAllocatorReadiness`,
`BootSequence.RunNativeAotHeapBootstrapReadiness`,
`BootSequence.RunNativeAotClassConstructorRunnerPreflightReadiness`, and
`BootSequence.RunNativeAotClassConstructorWorkItemReadiness` retain their
host-tested general contracts. The default live no-stdlib EFI profile
populates and validates `BootHandoffRecord`, normalizes and materializes its
compact map, builds separate conventional-memory readiness metadata, and
emits `BOOT_PAGE_ALLOCATOR_READY`; it does not invoke the M01-006 through
M01-009 mutation chain. The M01-024 dual-probe artifact invokes the reviewed
M01-007 subtractor, M01-006 state materializer, M01-008 one-page mutation, and
M01-009 exact inverse only through `M01LiveAllocatorIntegration`, over the
exact firmware-allocated pre-EBS arena, and restores the allocator metadata
before accepting the specific double-free rejection. It still does not run
the host-friendly planners or M01-010 stress oracle, access or zero page bytes,
grant arbitrary conventional-memory ownership, install a stable global
allocator, expose general allocation/free APIs, support arbitrary-order frees
or ABA-safe authority, reserve managed-heap pages, configure paging, enforce
NX/W^X, emit the NativeAOT/cctor readiness markers in QEMU, install a managed
heap, or invoke class constructors.

`BOOT_HANDOFF_READY`, `BOOT_MEMORY_MAP_ACCEPTED`,
`BOOT_MEMORY_REGIONS_READY`, `BOOT_MEMORY_REGION_COUNT`,
`BOOT_PAGE_ALLOCATOR_READY`, and `BOOT_ACPI_RSDP_HANDOFF_READY` are
QEMU-proven through source-linked EFI-safe numeric validators/materializers.
The five `ACPI_*` table-validation markers are QEMU-proven only through the
explicit ACPI physical discovery probe artifact. The historical M01 stage-one
and stage-two marker chains are QEMU-proven through their separate opt-in
artifacts; the complete stage-one, stage-two, stage-three, and
`CLROS_M01_LIVE_BOOT_MEMORY_READY` chain is QEMU-proven only by the accepted
all-switch M01-026 artifact. None is emitted by the default EFI profile. The managed
`BootSequence.Run(BootHandoffRecord, EfiMemoryMapSummary, ...)` overload
remains host-tested, while the live EFI artifact emits the allocation-free
`BootSequencePlan` after the accepted marker, region materialization,
allocator-readiness metadata marker, and ACPI scan.

The `APP_PACKAGE_READY`, `APP_LAUNCH_READY`, `APP_PAYLOAD_READY`, `APP_EXEC_MAPPING_READY`, `APP_EXEC_MEMORY_READY`, `APP_INVOCATION_READY`, `APP_ARGS_HANDOFF_READY`, `APP_LIFECYCLE_READY`, `APP_ENTRY_READY`, `APP_SELECTION_READY`, `APP_SELECTION_PREFLIGHT_READY`, `APP_PACKAGE_FILE_STAGING_PREFLIGHT_READY`, `NATIVE_MANIFEST_ENV_READY`, `FETCHAPP_CONFIG_PREFLIGHT_READY`, `FETCHAPP_HTTP_ENDPOINT_READY`, `FETCH_APP_HTTP_COMPLETION_READY`, `APP_EXIT_OBSERVER_READY`, `NATIVEAOT_CCTOR_RUNNER_PREFLIGHT_READY`, and `NATIVEAOT_CCTOR_WORK_ITEMS_READY` markers are not QEMU-proven and are not emitted by the default firmware-visible boot path. They are host-tested managed readiness helpers for future app-selection, package-loading, executable-mapping, invocation, argument handoff, lifecycle, final pre-entry, native manifest environment handoff, FetchApp config preflight, FetchApp request endpoint metadata, FetchApp completion, app-exit observation, runtime cctor-runner preflight, and runtime cctor work-item boundaries. They do not prove live package I/O, live virtio-blk reads, boot-time package loading, manifest/config reads from live storage, process environment materialization, executable page allocation, permission changes, payload copying, stack allocation, stack switching, thread creation, final ABI dispatch, stdout/stderr routing, exit capture, restart policy, relocation, hosted .NET, Kestrel, app execution, live networking, live HTTP GET, live app-exit observation, constructor invocation, metadata discovery, or module initialization.

## Next Steps

The M03-007 implementation now has an explicit first real-exit profile, documented
in [EFI build](efi-build.md#first-real-exit-profile-m03-007). `BootSequence` owns
the terminal transition from authenticated firmware callbacks to the retained
owned continuation. Preparation reserves code, map, page tables, descriptors,
main stack and three IST stacks before the final capture/exit window. On success
the continuation adopts owned descriptors and CR3, checks retained ACPI/allocator
state, and emits only through owned COM1. No firmware memory is reclaimed and no
general adapter revocation is claimed. [Step1032](../done/step1032.md) retains
staged preparation; [step1051](../done/step1051.md) records the accepted integrated
common run and fresh source/native/lifecycle/disk association at `e64e9dc`.
The subsequent eight-capability adapter revocation and owned post-exit
diagnostics scopes are accepted in [step1053](../done/step1053.md) and
[step1063](../done/step1063.md). General firmware-page reclamation and continuing
platform/driver/workload services remain later boundaries.

The preparatory M03-005 `NoAllocationOrUnrelatedFirmwareCallOccursBetween`
window accepts an already-owned writable map buffer and one retained state.
It admits the typed exit adapter before a single final `GetMemoryMap`, validates
the returned metadata locally, then forwards that capture's opaque key directly
to the exit attempt. The API exposes neither an allocator nor a logging callback.
It rejects missing/unavailable authority before capture, prevents recursive or
reused windows, and never attempts exit after failed capture or malformed metadata.
The buffer may have changed even when capture fails. Its record describes the
caller-owned capture buffer, not a separate copied map; individual descriptors
are not validated here. Buffer provenance, function-pointer authenticity and
serialization remain caller obligations. No retry or firmware-mode transition
is implemented. [Step999](../done/step999.md) records focused host and no-stdlib
compile evidence. M03-004 is accepted. [Step1004](../done/step1004.md) now
links this window and the bounded retry below into the existing default-off
typed-EBS diagnostic. [Step1010](../done/step1010.md) accepts the complete
batch and fresh default/enabled QEMU evidence for this controlled boundary.

The preparatory M03-006 `BoundedRetryStaleKeyStatus` coordinator composes at
most two such windows with separate retained states and the same owned buffer.
Only a returned exit call with exact `EFI_INVALID_PARAMETER` permits another
capture. Capture/admission/metadata failures and other exit statuses stop;
the loop neither grows the buffer nor exposes allocation or logging callbacks.
The result distinguishes windows started from actual exit calls and retains
the final window result. The outer state prevents reentry/reuse and cannot
overlap the map buffer. [Step1001](../done/step1001.md) records focused host and
no-stdlib compilation. [Step1010](../done/step1010.md) accepts five authentic
GetMemoryMap captures and five controlled exit callbacks, including stale-key
retry and exhaustion. No original firmware exit occurs; firmware access
invalidation remains required after a future real successful exit.

1. Treat the accepted M01-026 tuple as the bounded M01 milestone exit and begin
   M02 diagnostics work after tracker review/synchronization. Keep real
   `ExitBootServices` blocked until owned serial, descriptor tables, fault
   handling, and the retry boundary have matching evidence.
2. Keep extending the QEMU UEFI-gone simulation as new firmware-guarded platform interfaces are added, keeping real `ExitBootServices` blocked until post-firmware diagnostics are implemented.
3. Add a real `ExitBootServices` probe only after the retry plan, diagnostics, and owned output paths have evidence.
4. Begin diagnostics-first IDT/fault panic work after post-UEFI diagnostics requirements are represented by tests and evidence.
   - The host-tested `CpuFaultFrame`, `PanicFormatter.FormatFault`,
     `CpuFaultEntryFrameLayout`, and `CpuFaultPanicReadiness` contracts now
     define the expected register dump field order and caller-owned
     frame-to-panic-output readiness boundary before real handlers are added.

## Bounded throw/catch development

The optional #582 profile executes inside the original pre-exit collecting-GC diagnostic. It reuses the registered module, original allocation receipts, retained static storage and owned M01 stack. `BootSequence` guards the child and the one-frame interpreter entry; it introduces no normal boot phase or ExitBootServices behavior. The first finite development guest reaches the real typed catch and compiled continuation ([step1066](../done/step1066.md)); final integration acceptance remains pending.
