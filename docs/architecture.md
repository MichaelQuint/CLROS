# Architecture

## Current Status

The architecture is a staged CloudReady NativeAOT OS plan. The first-class application goal is .NET 10 workload hosting, with ASP.NET Minimal API apps as the primary cloud target. Kestrel support is mandatory for the CloudReady milestone. The implementation is currently at the first EFI/QEMU smoke boundary, an opt-in QEMU-proven pre-`ExitBootServices` ACPI physical discovery probe, a separate opt-in QEMU-proven loaded-image/owned-stack transition boundary, and host-tested parser/planning contracts for early diagnostics, ACPI/PCI/virtio, storage, networking, PAL, manifest, and native-app readiness.

`docs/workload-profiles.md` defines the workload profile decision. `SingleMinimalApi` is the near-term default path: one ASP.NET Minimal API workload, no process-table or per-process isolation requirement, but still with the required Kestrel runtime/network prerequisites. `MultiMinimalApi` is a later platform profile that requires process, isolation, scheduler, and supervisor semantics before it can be claimed.

## Implemented Behavior

- First-party project areas exist for `OS.Kernel`, `OS.Std.NoRuntime.Shared`, host tools, tests, and native-tier apps.
- Boot ordering starts in `src/OS.Kernel/Boot/BootSequence.cs` and the no-stdlib EFI shell emits the shared `BootSequencePlan` marker order.
- Diagnostics toggles start in `src/OS.Kernel/Diagnostics/Probes.cs`.
- `src/OS` publishes a no-stdlib EFI application that emits firmware-visible smoke markers, captures raw UEFI memory-map metadata, copies descriptor bytes into an OS-owned static buffer, materializes compact boot memory-region records into static storage, and populates a source-linked `BootHandoffRecord`.
- An explicit `-EnableAcpiPhysicalDiscoveryProbe` EFI build validates live ACPI RSDP/root SDT/MCFG bytes before `ExitBootServices` and emits ordered `ACPI_*` markers under QEMU/OVMF. It is a table-discovery probe, not a driver or PCI-enumeration path.
- An explicit `-EnableM01LoadedImageStackProbe` fresh build source-links the
  loaded-image capture, canonical reservation ledger, owned-stack layout,
  first-party x64 switch/probe plans, and static one-shot coordinator into
  `EfiEntry`. The accepted M01-022 QEMU run opened the live loaded-image
  protocol, allocated one `LoaderCode` page plus the guard/stack
  `LoaderData` arena, recaptured the map, validated the four-record ledger,
  transferred to the owned stack, observed contained `RSP`, and emitted the
  unchanged default boot plan. This path is opt-in and pre-firmware-exit.
- `src/OS.Kernel/Boot/BootHandoffInfo.cs` defines a host-tested typed handoff metadata contract and can import the no-stdlib-safe `BootHandoffRecord`.
- `src/OS.Kernel/Boot/TypedPhysicalVirtualDeviceAddressPODWrappers.cs` defines distinct host-tested `PhysicalAddress`, `VirtualAddress`, and `DeviceAddress` value domains. Each wrapper is one unmanaged `ulong`, treats zero/default as invalid, exposes raw ingress and egress only through explicitly named UEFI or hardware-register boundary methods, rejects malformed alignment and arithmetic wrap, and returns only its own address domain from checked operations. Same-domain `TryCompareTo` supports later range validation without returning to raw integers. The wrappers deliberately do not encode a fixed CPU physical width, x64 canonical-address policy, device/IOMMU width, mapping state, or ownership state.
- `src/OS.Kernel/Memory/BootReservedRange.cs` and `BootReservationTableValidator.cs` define the separate host-tested CLROS boot-ownership ledger. Explicit append-only reservation kinds and typed 4 KiB half-open physical ranges feed a bounded, nonmutating pointer/count/capacity core that rejects a wrapping full table span before dereference and validates redundant range metadata, sorted/disjoint order, adjacency, exact totals, deterministic conflicts, loaded-image duplicate coverage, and an optional but exact guard-plus-stack modeled arena. This is not the older UEFI descriptor-classification `BootMemoryReservedRange` contract.
- `src/OS.Kernel/Memory/BootReservationSubtractor.cs` defines the self-contained host-tested subtraction boundary over that typed ledger. Its exact 40-byte `BootReservationSourceRange` POD carries typed half-open physical bounds, exact page/byte metadata, and a zero-based source ordinal that every emitted fragment preserves. The unsafe `Subtract` API accepts a caller-owned source pointer/count/capacity, reservation pointer/count/capacity, explicit page size, and destination pointer/capacity; accepts only 4096-byte pages; and bounds nonempty sources at 4096, reservations at 256, and output fragments at 4096. The caller must keep every declared storage span valid, exclusively owned, and stable for the entire validation, dry-run, and emission call; arithmetic checks cannot prove that an asserted unsafe address is mapped or serialize a concurrent writer. The subtractor validates 8-byte alignment, compiler-sized full declared-capacity spans, arithmetic wrap, and source/reservation aliasing before reading either input table. Sources must have exact ordinal provenance and sorted disjoint geometry, while canonical reservations must be sorted/disjoint and wholly contained in exactly one source; adjacency is valid in both tables. The transform then performs a write-free dry run for exact fragment and accounting requirements, validates the destination's complete declared span and its pairwise disjointness, and emits only after every output precondition succeeds. Zero reservations form an identity transform, complete coverage succeeds with zero output and permits a null zero-capacity destination, and every failure leaves destination storage unchanged. Its value-only status/result evidence fails closed by default, binds counts, capacities, nested range outcomes, deterministic problem indexes, and exact totals, and retains no caller pointer.
- `src/OS.Kernel/Boot/EfiLoadedImageRange.cs` and `EfiLoadedImageRangeCapturePlan.cs` define the host-tested bridge from one caller-owned loaded-image acquisition transcript to that canonical ledger. The exact 40-byte observation binds firmware status, image handle, transient protocol address, image base, and image size; the checked pointer/count/capacity core validates metadata before its single read, rejects every nonzero firmware status, derives the exact typed image extent, rounds outward to 4 KiB without wrap, and returns an 80-byte range containing one canonical `BootReservedRange` of kind `LoadedImage`. The 144-byte result retains status evidence and the input handle but deliberately does not retain the firmware protocol pointer.
- `src/OS.Kernel/Boot/OwnedBootStackLayout.cs` defines the host-tested physical ownership contract for one future boot stack. Its bounded pointer/count/capacity factory requires exactly two canonical records, delegates record/order/overlap/guard-stack validation to `BootReservationTableValidator`, and checks that the guard plus usable-stack reservations exactly partition an explicit typed physical backing range. The resulting 128-byte unmanaged layout preserves the exclusive physical stack top and requires 16-byte alignment without converting that address into a virtual pointer. Its immutable `Reserved -> Ready -> Active` transitions are caller-attested metadata only; the 176-byte creation result preserves status, nested ledger evidence, lifetime evidence, capacity, and conflict indexes without retaining the caller's pointer. Transition-only results explicitly carry no table/capacity evidence, and the copyable value cannot reject replay from a stale snapshot without the later serialized live coordinator.
- `src/OS.Kernel/Boot/OwnedBootStackSwitchPlan.cs` defines the separate host-tested x64 switch contract without executing it. An immutable plan binds one exact `LoaderCode` page and canonical `BootStackSwitchStub` reservation to the M01-004 `LoaderData` guard/stack arena, a caller-attested static 88-byte handoff, one disjoint caller-attested static 536-byte state-storage range, a caller-attested static nonreturning continuation, and a fixed 14-byte C#-authored stub. Full-page emission and readback operate only at the plan's exact virtual code-page address and stub offset, and their evidence retains the complete plan snapshot. The mutable state record must occupy the plan's single canonical address, starts only from exact all-zero bytes, binds itself to that address and the complete plan, and records a terminal cause/status; all plan copies therefore share one caller-serialized `Planned -> Armed -> TransferRecorded -> ContinuationObserved` authority. Failed RSP audits and the synthetic unexpected-return classifier move it to distinguishable `Failed` states. The leaf derives and audits the exact Windows-x64 entry frame (`E - 32` before transfer, `E - 40` after the fake return push) but never writes `RSP`, invokes the bytes, or proves that any supplied address is executable, mapped, static, or nonreturning.
- `src/OS.Kernel/Boot/UefiMemoryDescriptorReader.cs` defines the host-tested raw descriptor decoding step needed before live owned-map normalization.
- `src/OS.Kernel/Memory` defines host-tested normalized memory-region and memory-map conversion contracts from UEFI descriptors, including the existing allocated `MemoryMapInfo` path, a caller-owned `MemoryRegion` writer, an allocator-facing `BootMemoryRegionPlanner` that classifies compact boot records into usable conventional ranges plus reserved/unusable metadata before general heap allocation is safe, a `BootPageAllocatorPlanner` that reduces selected ranges into caller-owned initial free-segment metadata without allocating, and a `BootPageFreeListBuilder` that converts a successful plan into caller-owned free-list node metadata with deterministic next-node indexes. `BootPageAllocatorCoreContracts.cs` extracts the exact unmanaged 48-byte `BootPageFreeListNode` and 56-byte `BootPageAllocatorState` PODs from their host-oriented planner/initializer files. The typed node is deliberately not wire-compatible with the current EFI six-`ulong` slot table: its packed 32-bit provenance fields and signed 32-bit terminal link require a checked future transcode, not a raw cast. `BootPageAllocatorStateMaterializer` is the no-stdlib-compatible pointer/count/capacity boundary over those PODs: in addition to source-node count and capacity it requires an explicit nonzero `sourceRangeCount`, accepts only the exact 4096-byte page policy, bounds node/range counts at 4096, and validates pointer alignment, complete non-overlapping source/destination spans, and pointer-table arithmetic before its first dereference. It then validates visitation-ordered source segment indexes, in-range monotonic range provenance, sorted/disjoint physical ranges, node shapes, links, cycles, exact visitation, and page/byte totals before its single destination write. Every failure is status-coded and leaves both caller-owned storages unchanged. The existing `BootPageAllocatorStateInitializer` remains the host-friendly plan-result adapter. `src/OS.Kernel/Runtime/NativeAotHeapBootstrapPlan.cs` consumes allocator state to validate a future managed-heap page budget without allocating or satisfying object allocation helpers. `BootSequence.RunBootPageAllocatorReadiness` consumes the older allocator-state initialization result and emits `BOOT_PAGE_ALLOCATOR_READY` or `BOOT_PAGE_ALLOCATOR_NOT_READY` markers with state, free-list, node, page, byte, and initialization metadata. `src/OS.Kernel/Boot/BootMemoryRegionMaterializer.cs` provides the separate no-stdlib pointer path that the live EFI artifact currently uses.
- `BootPageAllocatorMutator.AllocateOnePage` is the host-tested no-stdlib-compatible mutation boundary over the extracted allocator PODs. Its unsafe API accepts allocator state by value, a caller-owned node pointer/count/capacity, a read-only canonical-reservation pointer/count/capacity, and explicit page-count/alignment inputs. It accepts only one 4096-byte page, bounds logical node slots at 4096 and reservations at 256, validates compiler-sized declared spans and table disjointness, validates the complete active free-list chain and state accounting, and rejects any canonical reservation that overlaps any active free range. The caller must keep the node and reservation spans mapped and stable for the whole call and must exclusively serialize node mutation and authoritative state replacement. After complete preflight, a successful first-fit mutation edits only the logical node prefix; a middle split may consume only an exact all-zero inactive logical slot and fails atomically rather than overwriting stale unlinked metadata. A successful real `BootPageAllocatorOnePageResult` has `CanRestore=true` and embeds an initialized `BootPageAllocatorOnePageRestoreReceipt` carrying the original source-node/provenance/predecessor evidence required by the exact inverse, in addition to typed `PhysicalAddress` bounds, exact page/byte metadata, before/after states and accounting, capacities, indexes, and nested status evidence. Every failure leaves caller storage byte-identical.
- `BootPageAllocatorMutator.FreeOnePage` is the corresponding host-tested strict serialized exact inverse. It accepts only a successful real `AllocateOnePage` result and embedded receipt plus the exact current post-allocation state and node/reservation counts/capacities; it accepts no independent raw address and does not implement general free-list insertion. It revalidates the receipt and current chain, then restores the original state and logical node topology byte-for-byte. Whole-node, tail, head, and middle allocation shapes deterministically report `None`, `Previous`, `Next`, and `PreviousAndNext`. Default, unsuccessful, inconsistent forged, nonmatching foreign, replayed, stale, double-free, or out-of-order evidence and altered state/count/capacity/provenance/predecessor metadata fail before mutation. This permits immediate or caller-managed LIFO restoration only. It has no allocator identity, generation, or arbitrary-order allocation ledger; its copyable receipt is not an unforgeable capability, so a byte-identical receipt from an indistinguishable allocator copy cannot be rejected and ABA after free/reallocation cannot be eliminated. `PerformsLiveAllocation=false` remains explicit for both transitions: they mutate caller-owned metadata and do not own, free, map, zero, protect, or access physical memory. The legacy Span-shaped `Allocate`/`Free` API and result remain host-only behind `#if !NOSTDLIB_EFI`; they are historical non-authoritative compatibility helpers.
- `tests/OS.HostTests/BootPageAllocatorStressTests.cs` adds a deterministic host-only oracle over the existing one-page allocator and exact-inverse free boundary without modifying production code. Fixed-seed generated churn and scripted whole/head/tail/middle cases independently model page ownership, source provenance, free extents, strict-LIFO receipts, and separate host-owned content tags. Every transition validates active-chain shape and fields, accounting, ownership/content, inactive slots, reservation bytes, and capacity-tail canaries, then contributes to a platform-independent transcript whose final hash is frozen; failed transitions and matching inverse frees additionally require byte-identical full snapshots. The synthetic content store never crosses into the allocator, so this verifies test-oracle sensitivity rather than physical-page contents or live ownership.
- `src/OS.Kernel/Memory/PageProtection.cs`, `PageCachePolicy.cs`, and `X64PageTableEntry.cs` define one no-stdlib-compatible x64 entry-value contract for later paging work. The canonical `X64PageTableLevel` distinguishes PTE/PDE/PDPTE/PML4E once; leaf mappings accept only R, RW, or RX and bind WB/UC/WC to an explicitly validated IA32_PAT value/index; nonleaf table pointers use a separate canonical supervisor `P|RW, NX=0` kind and validate raw PAT index zero as WB independently of the selected leaf-WB index. Level-specific encode/decode validates 4 KiB/2 MiB/capability-gated 1 GiB alignment, supplied 36-52-bit physical width, NX, PAT placement, large-page/reserved bits, malformed POD state, and hardware Accessed/Dirty observations. A protection-only value rewrite explicitly preserves or clears A/D without live mutation. The two-pass unsafe batch core validates actual POD sizes, complete stable caller-owned pointer/count/capacity spans, and every request before any destination write, without fixed page-sized stack scratch.
- `src/OS.Kernel/Boot/X64BootIdentityMappingPlan.cs` defines a caller-owned, no-allocation identity-mapping planner for the M01-011 4 KiB leaf policy. It requires explicit executing-image, owned-stack, boot-handoff, owned-serial-stub, ACPI-scratch, and framebuffer-metadata mappings plus a validated non-emitted stack guard; permits explicit additional required mappings for later owned arenas; preserves split image permissions; excludes page zero and the high/noncanonical identity half; deterministically unions only compatible policy ranges; and calculates exact fresh PML4/PDPT/PD/PT page capacity. The result contains typed physical-equals-virtual ranges and value evidence only.
- `src/OS.Kernel/Memory/X64PagingModePreflightStubPlan.cs`, `X64ReversibleCr3TransitionStubPlan.cs`, and `X64Cr3LoadStubPlan.cs` define the host-audited M01-013 reversible-paging contract. The 111-byte observation stub records entry RFLAGS plus raw CR0, CR3, CR4, IA32_EFER, and CPUID physical-width values into caller-owned POD without writing a control register. Its evaluator requires paging and write protection, PAE, long-mode enable/active, NXE, a 36-52-bit physical width, four-level non-PCID mode, and an aligned owned root wholly inside the supplied table arena. The separate 1,981-byte transition plan is an independently encoded, relocation-free Windows-x64 byte stream: it captures entry RFLAGS before flag-changing validation; validates low-canonical, overflow-safe, mutually disjoint state/canary/stack storage before its first state write; restores entry RFLAGS on every pre-CLI failure; executes CLI; requires IF clear while permitting validation-induced arithmetic-flag changes; preserves exact raw firmware CR3/CR4; rejects a no-op same-root transition; clears and reads back CR4.PGE; loads and reads back the owned root; updates a shared nonzero canary; restores and reads back firmware CR3 while PGE remains clear; checks the canary again through the restored firmware root; restores and reads back CR4; restores exact entry RFLAGS; records status/RSP; and performs a final flag restoration immediately before returning the status. Every post-owned failure uses the same recovery path. The composite requires explicit one-shot opt-in, one distinct LoaderCode page, a nonoverlapping LoaderData table/state arena, physical disjointness from the M01-005 stack-switch code/backing allocations, exact modeled state-address binding, RX code and RW+NX data/stack mappings, exact sibling materialization/readback/evidence, 64 bytes of lower stack headroom, a 40-byte caller frame above the observed RSP, callback-free transcript ordering, raw-register/canary/RSP agreement, and timeout/reset/triple-fault classification. All three boundaries are host-only and execute no privileged byte in tests.
- `src/OS.Kernel/Memory/X64PageMap.cs` and `X64TlbInvalidationPlan.cs` define the no-stdlib-compatible M01-014 mutation boundary over a flat, caller-owned image of existing four-level x64 table pages. The map core accepts explicit POD pointers, logical counts, declared capacities, a typed root, one aligned low-canonical target page, the exact M01-011 encoding preconditions, and an optional sorted list of essential virtual ranges. It validates every complete declared storage span, disjointness, page-record order and physical uniqueness, root and child-table membership, entry form, request policy, and the exact target invalidation before its sole aligned 64-bit leaf write. Every success retains the validated page-image set's typed first-to-last physical envelope. It supports one 4 KiB map, unmap, or protection-only rewrite; preserves M01-011 physical/cache and selected Accessed/Dirty state on protection change; rejects page zero, W+X, missing tables, malformed entries, 1 GiB/2 MiB collisions, duplicate map, double unmap, unchanged protection, and removal or reprotection of an essential mapping. It deliberately allocates no table page, splits no large page, reloads no CR3, and performs no live invalidation.
- `src/OS.Kernel/Memory/X64InvlpgStubPlan.cs` and `X64ProtectionProbeStubPlan.cs` bind that value mutation to two independently authored C# byte plans for later live integration. The first materializes and reads back the exact four bytes `0F 01 39 C3` (`invlpg [rcx]; ret`) inside one modeled page, then permits a one-shot invocation plan only for the exact RX/non-writable entry and exactly one matching invalidation record; the aggregate separately binds that page to a caller-supplied one-page `LoaderCode` allocation. The second materializes and reads back the exact six bytes `B8 4B 4F 53 4F C3` (`mov eax, 0x4F534F4B; ret`) inside a caller-attested separate identity-mapped one-page `LoaderCode` allocation whose canonical reservation kind is `PageProtectionProbe`. Its aggregate audit requires caller-record coherence and disjoint transition-code, probe-code, and table/state allocations; physical containment of the mutation result's complete page-image envelope inside the caller-attested RW+NX `LoaderData` table/state allocation; an exact RW+NX-to-RX leaf mutation with unchanged physical page and WB cache policy; exact leaf readback; exact target-only invalidation and invlpg plan; a modeled single invocation, expected return, unchanged nonzero canary; and nine ordered events that make execute-before-protect impossible. Both boundaries retain explicit `false` live-evidence flags; host tests materialize bytes as data and invoke neither stream.
- `src/OS.Kernel/Memory/PageZeroMappingPolicy.cs` defines the M01-015 no-stdlib-compatible page-zero policy over raw mapping intents and a caller-owned four-level table image. Raw intent is narrowly permitted because `VirtualAddress` deliberately cannot represent zero; present ranges intersecting `[0, 4096)`, ranges wrapping through zero, malformed alignment, and noncanonical bounds fail closed. The read-only image audit validates complete pointer/count/capacity spans, record order and physical uniqueness, walks index zero from PML4E through PTE, recognizes canonical raw-zero absence at any level, rejects nonzero non-present encodings, rejects 1 GiB, 2 MiB, or 4 KiB leaves covering zero, and requires every child table traversed by the index-zero walk to belong to the supplied image. It mutates no entry and does not claim the supplied image is the installed CR3 root.
- `src/OS.Kernel/Boot/M01LivePagingIntegration.cs` defines the host-tested third M01 composition boundary over the final M01-018 static state and reviewed identity-map, reversible-CR3, table-image mutation, invalidation, protection-probe, and page-zero audit contracts. It requires three disjoint caller-attested allocation records, refreshed map/ledger evidence, exact four-plan materialization/readback, strict owned-hierarchy permissions, architectural preflight, separately modeled callback/interrupt and restoration discipline, an audited PGE cycle, canary validation, and retrospective host presentation. Every accepted identity-map range is cross-bound to the loaded-image page tiling, stage-one plan, stage-two allocator reservation, or an exact same-kind ledger reservation; the full table/state arena is audited RW+NX. The frozen `SerialStub` purpose is only a compatibility alias for the stage-one switch-stub reservation, and the source explicitly reports that it does not prove owned serial, map, diagnostic-state, ACPI-content, framebuffer-content, or PE-section provenance. `BootSequencePlan` freezes the corresponding 20-line stage-three order while `BootSequence.RunM01LivePagingIntegration` presents only a final successful host result. `ProvesCompositeOwnedCr3MachineCodeInterval` remains `false`: the reversible transition and protection/invalidation/probe leaves are mutually cross-checked models, not temporal proof that one executable interval contained them. These remain value/POD and byte-plan audits; no host path executes privileged instructions. Only the modeled probe-leaf rewrite is restored to its initial RW+NX value before `Execute` returns; the newly constructed caller-owned table image is retained.
- `src/OS.Kernel/Boot/M01OwnedCr3CompositeTransition.cs` defines the M01-027 replacement for that temporal gap: one C#-authored, relocation-free Windows-x64 byte stream and caller-owned unmanaged state contract containing architectural preflight, exact raw-register preservation, `cli`/PGE discipline, owned-root entry, continuation-canary update, the exact probe-leaf RW+NX-to-RO+X commit, one target `invlpg`, one audited constant-return probe transfer, a bounded page-zero hierarchy audit, and the shared firmware-CR3/CR4/RFLAGS recovery tail. CR3 and CR4 restoration retain hardware readbacks. RFLAGS restoration is represented honestly by the saved operand and a frozen final `popfq; ret` sequence after the nonvolatile unwind; there is no post-`popfq` hardware readback claim. The constant-return transfer is an internal, byte-verified part of the interval rather than a caller-selected callback; every firmware, console, serial, allocator, ACPI, framebuffer, diagnostic, or arbitrary callback remains forbidden. Host planning cross-binds every dereferenced code, stack, continuation, probe, table/state, runtime-state/canary, and owned-root range to typed allocation, reservation, and identity-map evidence before materialization. Once the probe PTE commit is verified it remains RO+X on success and on a later recoverable failure; a failure before committed readback makes no final-leaf claim. The stream encodes exactly one invalidation and never reopens a verified RO+X code page. Its in-stream TF/CET guards are defense-in-depth only: M01-025 must prove TF clear and supervisor CET/IBT inactive before any indirect entry because the transfer can fault before those guards and the exact six-byte probe lacks `endbr64`. This is still byte/data and control-flow evidence only: the source deliberately reports that it is not linked or invoked, does not perform a host-side control-register or PTE effect, is not guest evidence, and does not prove owned serial, live-map identity, diagnostic-state identity, ACPI/framebuffer provenance, CET/IBT entry safety, NMI/SMI safety, or any completion gate.
- `src/OS.Kernel/Diagnostics/NullPageFaultProbePlan.cs` defines the companion default-off metadata contract for a future supervisor read of address zero. A ready-disabled result requires successful page-zero absence evidence from the supplied table image plus caller-attested owned diagnostics and page-fault-handler prerequisites. It fixes vector 14, error-code value 0 under mask `0x1f`, CR2 value 0, supervisor privilege, and read access. Any default-on, requested-execution, or attempted-execution state is rejected. The contract emits no bytes, carries no callable address, dereferences nothing, and explicitly defers execution to M02-014.
- `FirmwareServicesGuard` and the `UEFI_GONE_SIM_*` host tests establish the first UEFI-gone simulation boundary.
- The EFI build can produce an explicit QEMU-tested UEFI-gone simulation artifact that reaches `UEFI_GONE_SIM_PLATFORM_DENIED`, `POST_FIRMWARE_DIAGNOSTICS_READY`, and `UEFI_GONE_SIM_PASS`.
- `tools/OS.ImageBuilder` creates a raw GPT/FAT32 EFI System Partition image.
- `tools/OS.QemuRunner` boots the raw image through QEMU/OVMF and verifies serial markers.
- ACPI RSDP/RSDT/XSDT/MCFG, PCI ECAM/header/BAR/scanner, virtio PCI/device-init/queue, virtio-blk, virtio-net, storage package, network protocol, PAL socket/config/file, and native-app launch readiness slices are represented as host-tested contracts.
- `OSWorkloadProfile` selects either `SingleMinimalApi` or `MultiMinimalApi` at build time and exposes the selected profile through `OS.Kernel.Build.WorkloadProfile`. This is compile-time metadata only, not runtime support evidence.

## Unsupported Behavior

No real kernel runtime, complete exported PAL, `ExitBootServices` path, owned
hardware drivers, scheduler, runtime storage I/O, live network I/O, app
launcher execution, hosted .NET 10 runtime, ASP.NET Minimal API path, Kestrel
path, or completed workload profile exists. ACPI probe evidence does not imply
ECAM mapping, PCI enumeration, APIC/timer use, or post-firmware physical-memory
access.

The source-bound M01-022 opt-in run narrows the earlier loaded-image/stack
nonclaim: on the recorded OVMF tuple, the EFI adapter opened the native
loaded-image protocol, converted that observation through the compact CLROS
capture contract, allocated the switch page and guard-plus-stack arena,
validated a four-record canonical ledger, executed the first-party switch
path, and observed continuation `RSP` inside the usable stack. It did not map
the guard inaccessible, transition the code page RW-to-RX, prove NX/W^X,
install an owned CR3, exit firmware, or make this the default path.

Outside that narrow adapter, the general typed-address migration and canonical
reservation fixtures, plus the reservation-subtractor, allocator POD,
allocator-state materializer, and one-page allocate/exact-inverse-free
fixtures, still prove restricted compilation and host behavior only. The
M01-010 seeded stress oracle remains host-test source and is excluded from the
EFI closure. Its synthetic ownership map and content tags can detect corruption
in the independent host model, but cannot establish firmware allocation
authority, access physical page contents, or prove zeroing. The reservation
subtractor mutates only caller-supplied metadata. `AllocateOnePage` and
`FreeOnePage` mutate only caller-owned free-list metadata and an exact matching
receipt; they do not reserve, release, zero, map, protect, or access a live
page. The strict free snapshot remains immediate/LIFO restoration rather than
an arbitrary-order ledger, unforgeable capability, or ABA-safe allocator.
Existing raw-`ulong` allocator, PCI, MMIO, and DMA contracts have not yet been
migrated to the typed domains.

The M01-011 entry codec is also value-model evidence only. Its caller-supplied
preconditions do not read or prove IA32_EFER.NXE, CR0.WP, CR4.LA57, CPUID
physical width or 1 GiB-page capability, or live IA32_PAT contents. It allocates no table pages, owns no
page-table arena, performs no virtual-address walk, reads or writes no live
entry, maps no page, proves no complete mixed-permission hierarchy, loads no
CR3, invalidates no TLB, and triggers or handles no page fault. Its
no-stdlib/zero-reference fixture proves restricted compilation while evaluated
EFI project items prove the source is not currently linked or executed.
Consequently it does not advance the NX/W^X completion gate and does not define
the M05 `DmaMapping` contract.

The M01-012 identity planner is likewise metadata-only. Its range roles,
processor/PAT preconditions, and table-page capacity are caller attestations.
It does not inspect PE sections, firmware mappings, linker output, ACPI
ownership, framebuffer MMIO, or current CR3; allocate/zero/assign table pages;
write a table image; install an inaccessible guard; map the table arena;
source-link or run inside EFI; or change processor state. Its deterministic
plan therefore does not prove live page-zero absence, NX/W^X enforcement,
cache correctness, identity reachability after CR3 load, TLB behavior, or a
completion gate.

The M01-013 preflight/transition plans and M01-027 composite interval remain
host-audited contracts rather than a live paging transition. The exact x64
bytes are materialized and decoded as ordinary data only; no host test calls them. The
plans do not allocate firmware pages, populate the M01-012 table hierarchy,
change a live page protection, prove page zero absent in hardware, load CR3,
execute a TLB flush, or run in EFI/QEMU. LoaderCode/LoaderData provenance,
identity reachability, firmware-root reachability of the recovery code/state,
and callback exclusion are validated caller evidence rather than processor
observations. PCID and LA57 fail closed. Maskable interrupts are disabled only
inside the modeled transition; NMI/SMI safety is not claimed before the M02
descriptor/fault work. The source is deliberately absent from the evaluated
`src/OS` closure. M01-019 supplies the separate-leaf host coordinator, M01-027
supplies the reviewed single-stream leaf, M01-020 owns explicit EFI source
linking, M01-025 owns live allocation
and `EfiEntry` wiring, and M01-026 owns cumulative real-QEMU evidence.

The M01-014 page-map, invalidation, and protection-probe contracts are also
host-only. The table image is ordinary caller-owned memory, the allocation and
reservation records are caller assertions, and the probe observation is a
modeled transcript. The implementation does not allocate, zero, populate, or
install a live hierarchy; synchronize a concurrent writer; inspect effective
permissions inherited from a live parent hierarchy; split large mappings;
perform an aligned store against processor-owned tables; execute `invlpg`;
invoke either byte stream; reload CR3; prove page zero absent in hardware; or
run in EFI/QEMU. The sources are intentionally absent from the evaluated
`src/OS` closure. M01-019 supplies the separate-leaf host coordinator, M01-027
supplies the reviewed composite leaf, M01-020 owns explicit EFI linking,
M01-025 owns live allocation and
`EfiEntry` wiring, and M01-026 owns cumulative real-QEMU proof.

## Tests

Host tests verify boot marker emission, boot event ring-buffer behavior, typed handoff metadata validation, typed physical/virtual/device address POD layout, named raw-boundary API allowlists, same-domain ordering, checked arithmetic, default-failing raw extraction, explicit boot-reservation kind values, typed reservation POD layout, malformed range rejection, bounded pointer/count/capacity handling, sorted/disjoint/adjacent table validation, deterministic overlap indexes and totals, loaded-image duplicate-coverage rejection, guard/stack union accounting, typed reservation-subtraction PODs and statuses, exact source-ordinal provenance, source/reservation containment, identity and complete-cover behavior, head/tail/middle/multiple fragmentation, exact removed/remaining accounting, declared-span alias and overflow rejection, output-capacity preflight, atomic failure behavior, typed one-page allocator results, exact before/after free and allocated accounting, first-fit head/tail/exact/split mutations, strict receipt-bound exact-inverse restoration with `None`/`Previous`/`Next`/`PreviousAndNext` classifications, original topology and provenance restoration, replay/double-free/nonmatching-foreign/stale/out-of-order rejection, logical-prefix and canary preservation, canonical reservation exclusion, stable pointer/count/capacity preflight, corrupt-chain and arithmetic rejection, failure atomicity, legacy host Span compatibility, loaded-image transcript validation and deterministic error precedence, exact and outward-rounded image extents, canonical loaded-image reservation construction, owned-stack backing/guard/usable accounting, explicit physical-top alignment, nested ledger failure propagation, immutable lifetime transitions, exact LoaderCode/LoaderData switch allocation binding, full-page/stub-offset byte materialization and readback, complete-plan evidence binding, storage-address-bound caller-owned one-shot state, repeated-transition rejection, exact continuation-entry RSP containment, malformed-layout revalidation, high-address and overflow boundaries, transient pointer nonretention, nonmutation and allocation-free validation, exact `NOSTDLIB_EFI`/no-framework-reference compile closures with zero assembly references, compiler rejection of unnamed raw access and cross-domain expressions, memory-map copy planning, raw descriptor decoding, allocated memory-map conversion, caller-owned memory-region writing, no-stdlib boot memory-region materialization, allocator-facing compact region planning, boot free-list segment planning, caller-owned free-list node construction, boot page allocator-state initialization, exact allocator POD layouts, bounded allocator-state materialization with explicit `sourceRangeCount`, exact 4096-byte page policy, pointer alignment/span/non-overlap validation, malformed node/index/chain/order rejection, capacity and arithmetic-overflow rejection, exact totals, unchanged source order, default-result failure, caller-owned boot page allocator mutation, deterministic fixed-seed allocator replay against an independent ownership/provenance/content-tag oracle, scripted restore-shape coverage, golden transcript hashes, full exhaustion, and corruption-sensitivity self-tests, boot page-allocator readiness markers, UEFI-gone simulation behavior, host-tool command contracts, ACPI marker preset ordering, and the current parser/planning contracts. `CLROS_M01_009_READY` and `CLROS_M01_010_READY` are host-harness markers only. QEMU smoke verifies the firmware-visible EFI marker path, UEFI memory-map readiness marker, owned-map copy marker, compact boot-region materialization marker/count, ACPI RSDP handoff marker, ordered boot marker plan, and the opt-in ACPI physical discovery marker chain. The M01-022 opt-in proof additionally executes the source-linked loaded-image capture, stack layout, canonical ledger, and stack-switch paths through continuation with contained `RSP`. It still does not execute the typed-address migration generally, M01-006 allocator-state materializer, M01-007 reservation subtractor, M01-008 live page allocation, M01-009 live inverse free, or M01-010 stress oracle.

M01-011 host tests additionally freeze every enum value and POD size/offset,
exercise all 256 raw protection byte values, compare exact R/RW/RX and
PML4E/PDPTE/PDE/PTE bit patterns, round-trip typed physical addresses, place
PTE versus large-page PAT selectors, validate explicit WB/UC/WC indexes,
classify Accessed/Dirty and hostile reserved or unsafe-copied POD bytes, cover
36/48/52-bit boundaries plus zero/default/misaligned/above-width addresses,
gate 1 GiB leaves on explicit capability evidence, preserve or clear A/D during
protection-only value rewrites, and prove failure-atomic two-pass pointer
batching with layout/alias/span/capacity canaries. The
exact sources also compile with `NOSTDLIB_EFI` and zero assembly references;
both canonical workload-profile evaluations prove they are absent from the
current EFI closure. `CLROS_M01_011_READY` is a host-harness marker only and is
not guest, EFI, QEMU, live-paging, or completion-gate evidence.

M01-012 host tests additionally cover every required mapped purpose, the
required non-emitted guard, split executing-image policies, additional required
resources, input-order-independent normalized output and accounting with
caller-order-relative diagnostic indexes, compatible overlap and adjacency
unions without permission/cache promotion, incompatible overlap,
page-zero and low-canonical bounds, exact fresh four-level table counts at
hierarchy boundaries, destination and table-page capacity, malformed/default
POD state, unsafe pointer/span/alias/overflow cases, whole-destination failure
atomicity, and the inherited M01-011 PAT[0], NXE, WP, physical-width,
protection, and cache preconditions. The exact source also compiles with
`NOSTDLIB_EFI` and zero assembly references; evaluated `SingleMinimalApi` and
`MultiMinimalApi` project closures prove it and its test are absent from the
current EFI build. `CLROS_M01_012_READY` is a host-harness marker only, not
guest, EFI, QEMU, live-mapping, or completion-gate evidence.

M01-013 host tests freeze every status value and POD size/offset; independently
decode the exact 111-byte/32-instruction preflight stream and
1,981-byte/485-instruction transition stream; validate every internal branch
target; reject calls, indirect control flow, nonvolatile-register use, XMM6-15,
STI, external patches, malformed/truncated/oversized bytes, and storage-span
errors; and verify deterministic SHA-256 identities
`71993B5134F7A4936718FD406EBF68CDA7E82476EE15B3F9824A5B6600FF9171`
and
`F1D94771DE8C8436CCDA1358A15F51DDE683AC3C77CBB0DC4A85B084888CA484`.
Positive and negative value tests cover CR0/CR4/EFER/CPUID prerequisites,
all permitted firmware CR3 PWT/PCD combinations, physical-width and
root/arena bounds, PGE initially clear or set, same-root rejection, exact
CR3/CR4/final-RFLAGS restoration, IF-only post-CLI validation with fixed
RFLAGS bit 1, all-return pre-CLI RFLAGS restoration, modeled state-address
rebinding, low-canonical and disjoint state/canary/stack storage, stack-switch
allocation collisions, one-shot stale state, zero/unchanged/corrupt canaries,
callback evidence, stack/RSP bounds, timeout/reset/triple-fault terminals,
marker order, capacity rebinding, hostile POD bytes, and write-free failure
behavior. The
combined exact source closure compiles with `NOSTDLIB_EFI`, `NoStdLib=true`,
zero framework references, and zero assembly references; both canonical
workload-profile evaluations prove the sources/tests are excluded from the
current EFI artifact. `CLROS_M01_013_READY` is a host-harness marker only.

M01-014 host tests freeze its append-only enum names/values and unmanaged POD
sizes/reference-free layouts; exercise map, unmap, remap, and exact
RW+NX-to-RX rewrite against an existing four-level caller-owned hierarchy;
compare complete table snapshots for storage, duplicate, essential-range, and
table-walk failures while successful operations change exactly one leaf; materialize
exact target-only invalidation records; and
cover page zero, unaligned/noncanonical/overflowing ranges, malformed storage,
capacity exhaustion, aliasing, record order, missing tables, out-of-image
children, large-page collisions, duplicate/double operations, unchanged
protection, W+X, essential-range rejection, and physical-envelope retention.
Separate tests reject unrelated or truncated table/state allocations and
verify exact materialization/readback for the four-byte invlpg and six-byte constant-return
streams, RX-only invocation planning, separate caller-attested `LoaderCode`
ownership and `PageProtectionProbe` reservation, a disjoint caller-attested
`LoaderData` table/state allocation with an exact RW+NX policy value that
physically contains the complete page-image envelope, exact
leaf and invalidation binding, one modeled return with an unchanged canary,
and strict execute-after-protect event order. The exact production-source
union compiles with `NOSTDLIB_EFI`, `NoStdLib=true`, zero framework
references, and zero assembly references. No test invokes privileged bytes.
`CLROS_M01_014_READY` is a host-harness marker only and is not guest, EFI,
QEMU, live-paging, or completion-gate evidence.

M01-015 host tests freeze the page-zero and disabled-probe enum/POD contracts;
exercise present and non-present raw mapping intents, partial page-zero
coverage, wraparound, alignment, and canonicality; and audit synthetic
four-level images with absence at PML4E, PDPTE, PDE, and PTE. They reject
present 1 GiB, 2 MiB, and 4 KiB mappings—including raw present entries whose
physical base is zero—plus malformed non-present encodings, missing children,
invalid roots/preconditions, malformed records, hostile counts/capacities,
misaligned/overflowing spans, and aliased storage. Complete caller buffers are
snapshotted around success and rejection. The null-fault plan accepts only a
disabled, non-executing request bound to image-audit absence evidence; vector
14/error-code-zero/CR2-zero are intent, not observations. The exact production
closure compiles with `NOSTDLIB_EFI`, `NoStdLib=true`, and zero assembly
references. Evaluated `SingleMinimalApi` and `MultiMinimalApi` EFI compile
closures exclude both production sources and the host test; no EFI binary was
inspected or executed. `CLROS_M01_015_READY` is host-harness readiness only.

## Known Limitations

The current architecture is documented, not yet enforced by build-time analyzers.

The opt-in M01-022 EFI path now supplies one live composition of the
loaded-image capture plan, canonical boot-reservation ledger/validator, owned
boot-stack layout, and owned stack-switch plan/state record. Its exact
`LoaderCode` and `LoaderData` allocations, refreshed-map generation,
four-record ledger, continuation, and contained `RSP` are source-bound to the
recorded QEMU/OVMF evidence. The default path does not run this adapter, and
the observation does not make the guard inaccessible, establish page
protections, or turn the stage-one allocations into a general allocator.

The typed-address migration outside that narrow adapter, reservation
subtractor, host-friendly `MemoryMapInfo` region writer,
`BootMemoryRegionPlanner`, `BootPageAllocatorPlanner`,
`BootPageFreeListBuilder`, `BootPageAllocatorStateInitializer`, extracted
allocator PODs, `BootPageAllocatorStateMaterializer`,
`BootPageAllocatorMutation`, M01-010 seeded stress oracle,
`NativeAotHeapBootstrapPlanner`, and
`BootSequence.RunBootPageAllocatorReadiness` marker gate remain host-tested
only. Live EFI does not source-link or invoke the M01-006 materializer, M01-007
subtractor, M01-008 one-page allocation, M01-009 receipt-bound inverse free,
or M01-010 test source. It has no authoritative mutable free list, arbitrary
free order, ABA-safe allocation authority, managed heap reservation, live
paging, NX/W^X enforcement, or those issues' QEMU evidence. The finite stress
corpus does not prove concurrency, arbitrary-order free, allocator
identity/generation authority, indistinguishable-copy rejection, or ABA
safety. M01-024 owns the first guest allocation/free/double-free-rejection
proof over the explicit pre-EBS arena.

The `PageProtection`/`PageCachePolicy`/`X64PageTableEntry` boundary remains
host-tested only as well. It is not source-linked into the current EFI
artifact, and no current boot phase constructs tables from it. PAT selection
does not account for MTRR interactions or prove a live IA32_PAT value or
1 GiB-page capability; decoded or re-encoded Accessed/Dirty bits are caller
bytes, not processor observations; table-pointer
permissions have no effective meaning until a complete hierarchy is built;
and the current strict format intentionally defers user/global/software/key
bits, LA57, PCID, live table synchronization, TLB shootdown, and `DmaMapping`.
M01-014 owns caller-owned table-image mutation and invalidation planning;
M01-019 now supplies the separate-leaf host coordinator and frozen stage-three
marker plan. M01-027 supplies the reviewed composite leaf, M01-020 owns
explicit EFI source linkage, M01-025 owns live allocation
and `EfiEntry` wiring, and M01-026 owns any cumulative QEMU claim.

The `X64PageMap`, `X64TlbInvalidationPlan`, `X64InvlpgStubPlan`, and
`X64ProtectionProbeStubPlan` boundaries remain caller-owned models. Their
unsafe span checks cannot establish that supplied host pointers are mapped,
stable, exclusively serialized, or backed by the stated physical pages.
Their table walk intentionally accepts only a pre-existing four-level,
4 KiB-leaf hierarchy and fails on a missing intermediate table or large-page
collision; it neither allocates nor splits. Targeted invalidation is a returned
record and audited byte plan, not a CPU effect, and no SMP shootdown, PCID,
global-page, memory-ordering, or parent-effective-permission policy is proven.
The modeled constant return/canary cannot substitute for guest execution.
M01-019 composes these contracts only as separate-leaf host evidence.
M01-027 supplies the single-stream host encoding; M01-020 and M01-025 must
link and wire it before M01-026 can provide the first cumulative QEMU
evidence. No current path proves a live table construction, CR3 write,
`invlpg`, RW-to-RX processor mutation, restored-firmware or systemwide
W^X/page-zero policy, PCID/LA57 support, NMI/SMI safety, or a deliberate null
dereference.

`PageZeroMappingPolicy` proves absence only in the validated caller-owned
snapshot passed to that call. Pointer arithmetic cannot prove the snapshot is
mapped, stable, current, exclusively owned, or identical to CR3, and the audit
does not cover a concurrent writer, another address space, a future
per-process root, or a later mutation. `NullPageFaultProbePlan` records fixed
future fault intent and caller attestations only. It is not linked into the
canonical EFI artifact, does not use the older null-write byte plan, does not
read address zero, does not load an IDT, and does not capture CR2 or an error
code. M01-026 owns cumulative QEMU evidence for page-zero absence during the
owned-root interval; M02-014 owns the disabled live `#PF` trigger and captured
fault evidence.

The `X64BootIdentityMappingPlan` boundary remains host-tested only. Its exact
fresh-hierarchy count cannot assign physical table pages, prove they are
available, reuse firmware tables, or account for a later implementation that
chooses large pages. Caller-provided ranges can be semantically wrong even
after their geometry passes, and the planner cannot prove that executable
image segments match linker output, the serial stub is immutable, ACPI scratch
is owned, framebuffer metadata excludes MMIO bytes, the guard is inaccessible,
or the eventual page-table arena is represented. Unsafe arithmetic checks
cannot prove caller pointers are mapped or stable. Live construction and
integration remain serialized in M01-019/M01-020/M01-025, with cumulative real
QEMU evidence deferred to M01-026.

## Next Steps

1. Finish UEFI platform interfaces and post-firmware diagnostics before real firmware shutdown.
2. Add post-firmware physical-memory and MMIO ownership rules so the ACPI probe evidence can become durable platform discovery and PCI contracts can move from caller-owned bytes to live platform enumeration.
3. Keep all shared utility implementation in `std/no-runtime/shared` before kernel/app use.
4. Use `docs/workload-profiles.md` to keep the first hosted workload target on `SingleMinimalApi` while tracking the additional process/isolation work needed before `MultiMinimalApi`.
