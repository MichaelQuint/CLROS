# Architecture

## Current Status

The accepted bounded profiles add rethrow and a retained scratch-page
UC/WC/WB transition after owned subsystem survival. Both are explicit diagnostic
profiles; [step1120](../done/step1120.md) records native/guest continuity, the
resumed full-host gate and image association. They do not introduce a scheduler
or continuing workload.

Ordinary verified firmware exit is accepted under #68/#594 in [step1094](../done/step1094.md). The frozen source `3c986e83447e2955efb25566ecd611676f054b30` passes the original full host gate (3,903 paired groups plus the suite summary), three fresh ordinary 79-record QEMU/OVMF captures and five explicit diagnostic regressions. Native, raw transcript, source and post-capture GPT/FAT/EFI associations pass.

CLROS follows a staged CloudReady NativeAOT architecture. The first-class
application goal is .NET 10 workload hosting, with ASP.NET Minimal API apps as
the primary cloud target. Kestrel support is mandatory for that milestone.
Accepted local QEMU/OVMF profiles include the default EFI smoke, opt-in owned
stack/allocator/paging and ACPI discovery, bounded real `ExitBootServices`,
eight-capability adapter revocation, and owned post-exit diagnostics. The exact
source-bound scopes are recorded in [completion gates](completion-gates.md),
including [step1063](../done/step1063.md). Ordinary `VerifiedExit` now requires bounded real firmware exit and owned
post-exit checks before halting. Explicit `-BootPolicy Diagnostic` without
probes retains the firmware-visible compatibility path. The repository also contains bounded runtime slices and host-tested
planning contracts for ACPI/PCI/virtio, storage, networking, PAL, manifest and
native-app readiness; these do not establish a complete kernel or hosted
workload execution.

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
- An additional `-EnableM01LiveAllocatorProbe` fresh build nests after the
  proven owned-stack continuation. The accepted M01-024 QEMU run allocated
  one two-page `EfiLoaderData` arena, recaptured and validated the changed
  memory map, extended the retained ownership ledger to exactly five records,
  and completed one arena-confined 4 KiB metadata allocation, exact inverse
  free, and explicit double-free rejection. The path remains opt-in,
  pre-firmware-exit, and metadata-only.
- A third `-EnableM01OwnedPagingProbe` switch source-wires the M01-025
  stage-three adapter only when both earlier switches are enabled. M01-035
  adds checked direct calls to M01-034's immutable linked serializer at the
  stack-probe, stack-switch, architectural-preflight/PAT, and final call-gate
   publication epochs. One EfiEntry-owned primitive mask survives the one-way
   stack switch and may advance only `0 -> 1 -> 3 -> 7 -> 15`; the aggregate
   verified marker is gated after the post-return audit and canary and before
   `M01_POST_CR3`. The isolated M01-035 result is source, host-test, and
   fresh-build evidence only. The later accepted M01-026 tuple executed the
   exact integrated all-switch path once under its recorded local QEMU/OVMF
   configuration.
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
- `src/OS.Kernel/Memory/PageProtection.cs`, `PageCachePolicy.cs`, and `X64PageTableEntry.cs` define one no-stdlib-compatible x64 entry-value contract used by the bounded M01 integration and future paging work. The canonical `X64PageTableLevel` distinguishes PTE/PDE/PDPTE/PML4E once; leaf mappings accept only R, RW, or RX and bind WB/UC/WC to an explicitly validated IA32_PAT value/index; nonleaf table pointers use a separate canonical supervisor `P|RW, NX=0` kind and validate raw PAT index zero as WB independently of the selected leaf-WB index. Level-specific encode/decode validates 4 KiB/2 MiB/capability-gated 1 GiB alignment, supplied 36-52-bit physical width, NX, PAT placement, large-page/reserved bits, malformed POD state, and hardware Accessed/Dirty observations. A protection-only value rewrite explicitly preserves or clears A/D without live mutation. The two-pass unsafe batch core validates actual POD sizes, complete stable caller-owned pointer/count/capacity spans, and every request before any destination write, without fixed page-sized stack scratch.
- `src/OS.Kernel/Boot/X64BootIdentityMappingPlan.cs` defines a caller-owned, no-allocation identity-mapping planner for the M01-011 4 KiB leaf policy. It requires explicit executing-image, owned-stack, boot-handoff, owned-serial-stub, ACPI-scratch, and framebuffer-metadata mappings plus a validated non-emitted stack guard; permits explicit additional required mappings for later owned arenas; preserves split image permissions; excludes page zero and the high/noncanonical identity half; deterministically unions only compatible policy ranges; and calculates exact fresh PML4/PDPT/PD/PT page capacity. The result contains typed physical-equals-virtual ranges and value evidence only.
- `src/OS.Kernel/Memory/X64PagingModePreflightStubPlan.cs`, `X64ReversibleCr3TransitionStubPlan.cs`, and `X64Cr3LoadStubPlan.cs` define the host-audited M01-013 reversible-paging contract. The 111-byte observation stub records entry RFLAGS plus raw CR0, CR3, CR4, IA32_EFER, and CPUID physical-width values into caller-owned POD without writing a control register. Its evaluator requires paging and write protection, PAE, long-mode enable/active, NXE, a 36-52-bit physical width, four-level non-PCID mode, and an aligned owned root wholly inside the supplied table arena. The separate 1,981-byte transition plan is an independently encoded, relocation-free Windows-x64 byte stream: it captures entry RFLAGS before flag-changing validation; validates low-canonical, overflow-safe, mutually disjoint state/canary/stack storage before its first state write; restores entry RFLAGS on every pre-CLI failure; executes CLI; requires IF clear while permitting validation-induced arithmetic-flag changes; preserves exact raw firmware CR3/CR4; rejects a no-op same-root transition; clears and reads back CR4.PGE; loads and reads back the owned root; updates a shared nonzero canary; restores and reads back firmware CR3 while PGE remains clear; checks the canary again through the restored firmware root; restores and reads back CR4; restores exact entry RFLAGS; records status/RSP; and performs a final flag restoration immediately before returning the status. Every post-owned failure uses the same recovery path. The composite requires explicit one-shot opt-in, one distinct LoaderCode page, a nonoverlapping LoaderData table/state arena, physical disjointness from the M01-005 stack-switch code/backing allocations, exact modeled state-address binding, RX code and RW+NX data/stack mappings, exact sibling materialization/readback/evidence, 64 bytes of lower stack headroom, a 40-byte caller frame above the observed RSP, callback-free transcript ordering, raw-register/canary/RSP agreement, and timeout/reset/triple-fault classification. All three boundaries are host-only and execute no privileged byte in tests.
- `src/OS.Kernel/Memory/X64PageMap.cs` and `X64TlbInvalidationPlan.cs` define the no-stdlib-compatible M01-014 mutation boundary over a flat, caller-owned image of existing four-level x64 table pages. The map core accepts explicit POD pointers, logical counts, declared capacities, a typed root, one aligned low-canonical target page, the exact M01-011 encoding preconditions, and an optional sorted list of essential virtual ranges. It validates every complete declared storage span, disjointness, page-record order and physical uniqueness, root and child-table membership, entry form, request policy, and the exact target invalidation before its sole aligned 64-bit leaf write. Every success retains the validated page-image set's typed first-to-last physical envelope. It supports one 4 KiB map, unmap, or protection-only rewrite; preserves M01-011 physical/cache and selected Accessed/Dirty state on protection change; rejects page zero, W+X, missing tables, malformed entries, 1 GiB/2 MiB collisions, duplicate map, double unmap, unchanged protection, and removal or reprotection of an essential mapping. It deliberately allocates no table page, splits no large page, reloads no CR3, and performs no live invalidation.
- `src/OS.Kernel/Memory/X64InvlpgStubPlan.cs` and `X64ProtectionProbeStubPlan.cs` bind that value mutation to two independently authored C# byte plans consumed by the reviewed live integration. The first materializes and reads back the exact four bytes `0F 01 39 C3` (`invlpg [rcx]; ret`) inside one modeled page, then permits a one-shot invocation plan only for the exact RX/non-writable entry and exactly one matching invalidation record; the aggregate separately binds that page to a caller-supplied one-page `LoaderCode` allocation. The second materializes and reads back the exact six bytes `B8 4B 4F 53 4F C3` (`mov eax, 0x4F534F4B; ret`) inside a caller-attested separate identity-mapped one-page `LoaderCode` allocation whose canonical reservation kind is `PageProtectionProbe`. Its aggregate audit requires caller-record coherence and disjoint transition-code, probe-code, and table/state allocations; physical containment of the mutation result's complete page-image envelope inside the caller-attested RW+NX `LoaderData` table/state allocation; an exact RW+NX-to-RX leaf mutation with unchanged physical page and WB cache policy; exact leaf readback; exact target-only invalidation and invlpg plan; a modeled single invocation, expected return, unchanged nonzero canary; and nine ordered events that make execute-before-protect impossible. Both isolated boundary results retain explicit `false` live-evidence flags, and host tests materialize bytes as data and invoke neither stream; only the accepted M01-026 tuple supplies the scoped integrated execution evidence.
- `src/OS.Kernel/Memory/PageZeroMappingPolicy.cs` defines the M01-015 no-stdlib-compatible page-zero policy over raw mapping intents and a caller-owned four-level table image. Raw intent is narrowly permitted because `VirtualAddress` deliberately cannot represent zero; present ranges intersecting `[0, 4096)`, ranges wrapping through zero, malformed alignment, and noncanonical bounds fail closed. The read-only image audit validates complete pointer/count/capacity spans, record order and physical uniqueness, walks index zero from PML4E through PTE, recognizes canonical raw-zero absence at any level, rejects nonzero non-present encodings, rejects 1 GiB, 2 MiB, or 4 KiB leaves covering zero, and requires every child table traversed by the index-zero walk to belong to the supplied image. It mutates no entry and does not claim the supplied image is the installed CR3 root.
- `src/OS.Kernel/Boot/M01LivePagingIntegration.cs` separates the first-class M01-029 preparation-only boundary from the historical M01-019 execution model. `Prepare(...)` returns caller-owned POD preparation evidence/state without accepting synthetic execution transcripts. M01-030 gives that path a seven-record exact live-interval profile: six present code/gate, stack, probe, table/state, state/canary, and root purposes plus one absent stack-guard sentinel. Historical `SerialStub`, ACPI, framebuffer, handoff, allocator-arena, loaded-image, and `AdditionalRequired` purposes cannot promote preparation. M01-031 adds sibling pointer-free arena-plan and materialization result PODs without changing those frozen preparation surfaces. `PlanOwnedTableArena(...)` retains the four fixed requirements and derives one exact table-pages-plus-state-page request for a caller-selected physical candidate. Before `MaterializeOwnedTableArena(...)` writes or verifies through a distinct caller-owned backing pointer, it rederives the exact seven-requirement range plan, compares every nested plan field, audits every hierarchy leaf for virtual/physical identity, protection, and cache policy, validates the exact `LoaderData` attribute, and requires a disjoint caller-owned 4096-byte scratch snapshot for byte-exact state-page preservation; FNV values remain diagnostic only. The retained `Execute(...)` and `ExecuteModelOnly(...)` paths preserve their historical alias contract and remain non-promotable model evidence. Preparation remains one-shot and fail-closed, and architectural-preflight rejection still occurs before caller output is written.
- `src/OS.Kernel/Boot/M01OwnedCr3CompositeTransition.cs` defines the M01-027 C#-authored, relocation-free Windows-x64 composite stream, the M01-030 exact host binding contract, and the M01-032 audited same-page call gate. Six present bindings cover the combined composite/call-gate RX page, RW+NX stack, initially RW+NX probe, complete RW+NX table/state arena, contained state/canary page, and contained root page. The frozen 3,830-byte/732-instruction composite prefix is followed, after ten zero bytes, by an exact 256-byte/74-instruction gate filling `[0xF00, 0x1000)`; its direct `call rel32` at `0xFC4` fixes the exact call-next return site at `0xFC9` without an external relocation. A 160-byte version-1 observation POD records lifecycle, raw post-return RFLAGS, restored CR3/CR4, composite result, exact call/observation counts, canary, and a last-written completion signature; the pointer-free result POD is 776 bytes. Before any write or call, the gate checks low-canonical aligned state/observation pointers, packed state/self/arena identity, exact stack geometry, state-page observation containment, packed `Armed` observation/self/count-signature state, and its RIP-derived expected return address through one no-write/no-call reject path. The guarded body preserves Windows-x64 alignment/shadow-space and nonvolatile-register rules, uses the fixed `observation + 127` bias and 48-byte entry-to-composite stack delta, restores the prepared RFLAGS snapshot with consecutive `push [RDX+48]; popfq; call` instructions, touches no XMM6-XMM15, and writes the completion signature last. Production materialization/readback performs exact comparison; host tests independently decode the bytes and reject success transcripts that cannot satisfy the gate's own stack window. Pointer mapping, page-aligned state-root provenance, exact live RX/non-writable/WB mapping, broader observation expectations and non-count output pristineness, full live-entry RFLAGS equality with the prepared snapshot, TF/CET transfer safety, LA57/PCIDE/IF/PGE policy, and NMI/SMI risk remain external live-boundary responsibilities. A loaded-image continuation is not an owned-root dependency. Every binding cross-checks typed geometry, allocation/reservation provenance, a containing declared identity range, exact purpose bit, WB cache, and protection, while the canonical guard must be absent from the supplied table image. Same-policy contained or adjacent roles may share a normalized mapping only when each exact bit remains present. In isolation this class supplies host byte/data and metadata evidence, and host tests invoke neither stream. The accepted M01-026 tuple is the scoped exception: its source-wired `EfiEntry` adapter materializes and invokes the exact composite/gate path, observes the bounded CR3/PTE/TLB/RFLAGS effects, and restores firmware state. That tuple still does not establish general hardware W^X/guard policy or advance a numbered completion gate.
- `src/OS.Kernel/Diagnostics/NullPageFaultProbePlan.cs` defines the companion default-off metadata contract for a future supervisor read of address zero. A ready-disabled result requires successful page-zero absence evidence from the supplied table image plus caller-attested owned diagnostics and page-fault-handler prerequisites. It fixes vector 14, error-code value 0 under mask `0x1f`, CR2 value 0, supervisor privilege, and read access. Any default-on, requested-execution, or attempted-execution state is rejected. The contract emits no bytes, carries no callable address, dereferences nothing, and explicitly defers execution to M02-014.
- `FirmwareServicesGuard` and the `UEFI_GONE_SIM_*` host tests establish the first UEFI-gone simulation boundary.
- The EFI build can produce an explicit QEMU-tested UEFI-gone simulation artifact that reaches `UEFI_GONE_SIM_PLATFORM_DENIED`, `POST_FIRMWARE_DIAGNOSTICS_READY`, and `UEFI_GONE_SIM_PASS`.
- `tools/OS.ImageBuilder` creates a raw GPT/FAT32 EFI System Partition image.
- `tools/OS.QemuRunner` boots the raw image through QEMU/OVMF and verifies serial markers.
- ACPI RSDP/RSDT/XSDT/MCFG, PCI ECAM/header/BAR/scanner, virtio PCI/device-init/queue, virtio-blk, virtio-net, storage package, network protocol, PAL socket/config/file, and native-app launch readiness slices are represented as host-tested contracts.
- `OSWorkloadProfile` selects either `SingleMinimalApi` or `MultiMinimalApi` at build time and exposes the selected profile through `OS.Kernel.Build.WorkloadProfile`. This is compile-time metadata only, not runtime support evidence.

## Unsupported Behavior

A complete kernel runtime, exported hosted-tier PAL, general owned hardware
drivers, scheduler, runtime storage I/O, live network I/O, app launcher,
hosted .NET 10 runtime, ASP.NET Minimal API path, Kestrel path and completed
workload profile remain unsupported. The accepted opt-in firmware-exit and
owned-diagnostics profiles retain their bounded terminal scopes. ACPI probe
evidence does not imply ECAM mapping, PCI enumeration, APIC/timer use or a
general post-firmware physical-memory interface.

The source-bound M01-022 opt-in run narrows the earlier loaded-image/stack
nonclaim: on the recorded OVMF tuple, the EFI adapter opened the native
loaded-image protocol, converted that observation through the compact CLROS
capture contract, allocated the switch page and guard-plus-stack arena,
validated a four-record canonical ledger, executed the first-party switch
path, and observed continuation `RSP` inside the usable stack. It did not map
the guard inaccessible, transition the code page RW-to-RX, prove NX/W^X,
install an owned CR3, exit firmware, or make this the default path.

The source-bound M01-024 tuple narrows the allocator guest-execution nonclaim
only for the exact two-page arena. It proves execution through the M01-007
subtractor, M01-006 allocator-state materializer, M01-008 one-page allocator,
and M01-009 exact inverse and double-free-rejection checkpoints under the
recorded firmware tuple. These leaves mutate caller-owned metadata; they do not
reserve, release, zero, map, protect, or access physical-page bytes. The
default path, general typed-address migration, other arenas, arbitrary-order
free, content tagging, concurrency, indistinguishable-copy/ABA authority, and
global allocator use remain host-only or unsupported. The M01-010 seeded
stress oracle remains host-test source and is excluded from the EFI closure;
its synthetic ownership map and content tags can detect corruption only in the
independent host model. Existing raw-`ulong` allocator, PCI, MMIO, and DMA
contracts have not yet been migrated to the typed domains.

The M01-011 entry codec is value-model evidence when considered in isolation.
Its caller-supplied preconditions do not themselves read or prove
IA32_EFER.NXE, CR0.WP, CR4.LA57, CPUID physical width or 1 GiB-page
capability, or live IA32_PAT contents. Its isolated API allocates no table
pages, owns no page-table arena, loads no CR3, invalidates no TLB, and triggers
or handles no page fault. Its no-stdlib/zero-reference fixture proves
restricted compilation. Closed M01-020 links the source into the opt-in EFI
closure, and the accepted M01-026 tuple executes its exact integrated encoding
path over the live allocated table arena after the surrounding adapter captures
the required preconditions. That scoped use does not turn the codec into a
general pager, advance the systemwide NX/W^X completion gate, or define the M05
`DmaMapping` contract.

The M01-012 identity planner is likewise metadata-only in isolation. Its range
roles, processor/PAT preconditions, and table-page capacity are caller
attestations. Its isolated API does not inspect PE sections, firmware mappings,
linker output, ACPI ownership, framebuffer MMIO, or current CR3; allocate or
zero firmware pages; install an inaccessible guard; map the table arena; or
change processor state. M01-025 source-wires the planner into the default-off
stage-three adapter, and the accepted M01-026 tuple executes the resulting
integrated plan/materialization path once. The standalone plan still does not
prove general live page-zero policy, systemwide NX/W^X enforcement, cache
correctness, arbitrary identity reachability, SMP TLB behavior, or a completion
gate.

The M01-013 preflight/transition plans and M01-027 composite interval remain
host-audited contracts when considered in isolation. Host tests materialize and
decode the exact x64 bytes as ordinary data without invoking them. The accepted
M01-026 tuple is the scoped exception: its integrated adapter allocates the
required firmware pages, populates the M01-012 hierarchy, invokes the reviewed
transition/call-gate bytes under EFI/QEMU, temporarily loads the owned root,
and restores the captured firmware state.
LoaderCode/LoaderData provenance, identity reachability, firmware-root
reachability of the recovery code/state, and callback exclusion are validated
caller evidence rather than processor observations. PCID and LA57 fail closed.
Maskable interrupts are disabled only inside the modeled transition; NMI/SMI
safety is not claimed before the M02 descriptor/fault work. Closed M01-020
source-links the exact production sources into the evaluated opt-in `src/OS`
compile closure. M01-025 now source-wires their default-off stage-three
adapter, and M01-035 places checked immutable-text publication barriers before
its current generated-code entries; source linkage and build success still
add no live EFI or guest evidence. M01-019 supplies only host-side
preparation and synthetic transcript composition, M01-027 supplies the reviewed
single-stream leaf, M01-029 owns the preparation/execution-transcript split,
M01-031 supplies the separate host-only physical-arena contract, and M01-032
supplies the host-only call-gate and post-return observation contract. The
accepted M01-026 tuple now supplies the bounded cumulative real-QEMU execution
evidence for their exact integrated source path.

The M01-014 page-map, invalidation, and protection-probe contracts are host-only
when considered in isolation. Their host table image is ordinary caller-owned
memory, allocation and reservation records are caller assertions, and the host
probe observation is a modeled transcript. The isolated API does not allocate
firmware pages, synchronize a concurrent writer, inspect effective permissions
inherited from an arbitrary live parent hierarchy, split large mappings, or
establish SMP invalidation. Closed M01-020 source-links the exact production
sources into the evaluated opt-in `src/OS` compile closure. M01-025 now
source-wires their default-off stage-three adapter, and M01-035 places its
final publication barrier before the audited call-gate invocation; linkage,
source ordering, and build success add no live EFI or guest evidence. M01-019
supplies only host-side preparation and synthetic
transcript composition, M01-027 supplies the reviewed composite leaf, M01-029
owns the preparation/execution-transcript split, M01-031 supplies the separate
host-only physical-arena contract, and M01-032 supplies the host-only call-gate
and post-return observation contract. The accepted M01-026 tuple now supplies
the bounded cumulative real-QEMU proof for their exact integrated source path,
including live hierarchy population, temporary owned-root execution, targeted
`invlpg`, the protection probe, and page-zero absence during that interval.

## Tests

Host tests verify boot marker emission, boot event ring-buffer behavior, typed handoff metadata validation, typed physical/virtual/device address POD layout, named raw-boundary API allowlists, same-domain ordering, checked arithmetic, default-failing raw extraction, explicit boot-reservation kind values, typed reservation POD layout, malformed range rejection, bounded pointer/count/capacity handling, sorted/disjoint/adjacent table validation, deterministic overlap indexes and totals, loaded-image duplicate-coverage rejection, guard/stack union accounting, typed reservation-subtraction PODs and statuses, exact source-ordinal provenance, source/reservation containment, identity and complete-cover behavior, head/tail/middle/multiple fragmentation, exact removed/remaining accounting, declared-span alias and overflow rejection, output-capacity preflight, atomic failure behavior, typed one-page allocator results, exact before/after free and allocated accounting, first-fit head/tail/exact/split mutations, strict receipt-bound exact-inverse restoration with `None`/`Previous`/`Next`/`PreviousAndNext` classifications, original topology and provenance restoration, replay/double-free/nonmatching-foreign/stale/out-of-order rejection, logical-prefix and canary preservation, canonical reservation exclusion, stable pointer/count/capacity preflight, corrupt-chain and arithmetic rejection, failure atomicity, legacy host Span compatibility, loaded-image transcript validation and deterministic error precedence, exact and outward-rounded image extents, canonical loaded-image reservation construction, owned-stack backing/guard/usable accounting, explicit physical-top alignment, nested ledger failure propagation, immutable lifetime transitions, exact LoaderCode/LoaderData switch allocation binding, full-page/stub-offset byte materialization and readback, complete-plan evidence binding, storage-address-bound caller-owned one-shot state, repeated-transition rejection, exact continuation-entry RSP containment, malformed-layout revalidation, high-address and overflow boundaries, transient pointer nonretention, nonmutation and allocation-free validation, exact `NOSTDLIB_EFI`/no-framework-reference compile closures with zero assembly references, compiler rejection of unnamed raw access and cross-domain expressions, memory-map copy planning, raw descriptor decoding, allocated memory-map conversion, caller-owned memory-region writing, no-stdlib boot memory-region materialization, allocator-facing compact region planning, boot free-list segment planning, caller-owned free-list node construction, boot page allocator-state initialization, exact allocator POD layouts, bounded allocator-state materialization with explicit `sourceRangeCount`, exact 4096-byte page policy, pointer alignment/span/non-overlap validation, malformed node/index/chain/order rejection, capacity and arithmetic-overflow rejection, exact totals, unchanged source order, default-result failure, caller-owned boot page allocator mutation, deterministic fixed-seed allocator replay against an independent ownership/provenance/content-tag oracle, scripted restore-shape coverage, golden transcript hashes, full exhaustion, and corruption-sensitivity self-tests, boot page-allocator readiness markers, UEFI-gone simulation behavior, host-tool command contracts, ACPI marker preset ordering, and the current parser/planning contracts. `CLROS_M01_009_READY` and `CLROS_M01_010_READY` are host-harness markers only. QEMU smoke verifies the firmware-visible EFI marker path, UEFI memory-map readiness marker, owned-map copy marker, compact boot-region materialization marker/count, ACPI RSDP handoff marker, ordered boot marker plan, and the opt-in ACPI physical discovery marker chain. The M01-022 opt-in proof additionally executes the source-linked loaded-image capture, stack layout, canonical ledger, and stack-switch paths through continuation with contained `RSP`. The M01-024 tuple additionally executes the source-linked stage-two coordinator and its M01-007 subtraction, M01-006 state-materialization, M01-008 one-page allocation, and M01-009 exact-inverse/double-free-rejection leaves over the explicit pre-EBS arena. It does not execute the M01-010 stress oracle or establish any broader allocator authority. The accepted M01-026 tuple executes the exact integrated stage-one-through-stage-three source path once, including the reversible owned-root transition, restoration audit, targeted invalidation, protection probe, page-zero absence audit, and immutable-publication epochs. Its evidence boundary is the source-bound tuple documented below, not the default EFI profile or a general paging claim.

`tests/OS.HostTests/M01InstructionPublicationEfiEntryTests.cs` emits
`TEST_START m01.035`, `CLROS_M01_035_READY`, and `TEST_PASS m01.035` only after
its structural and mutation oracles accept the source. It checks call
cardinality across the disabled, stage-one, stage-two, and all-switch profiles;
the four exact owner methods and write/readback/binding order; direct immutable
serializer binding and exact success-cookie consumption; the shared root-state
lifetime and `0 -> 1 -> 3 -> 7 -> 15` sequence; the continuation observation;
failure-marker domination; post-return aggregate-marker placement; and hostile
missing, duplicate, reordered, branch-only, synthetic, wrong-binding,
substitute-barrier, reset, reconstruction, and partial-mask variants. These are
source/host assertions, not observations of a serializer call in a guest.

M01-011 host tests additionally freeze every enum value and POD size/offset,
exercise all 256 raw protection byte values, compare exact R/RW/RX and
PML4E/PDPTE/PDE/PTE bit patterns, round-trip typed physical addresses, place
PTE versus large-page PAT selectors, validate explicit WB/UC/WC indexes,
classify Accessed/Dirty and hostile reserved or unsafe-copied POD bytes, cover
36/48/52-bit boundaries plus zero/default/misaligned/above-width addresses,
gate 1 GiB leaves on explicit capability evidence, preserve or clear A/D during
protection-only value rewrites, and prove failure-atomic two-pass pointer
batching with layout/alias/span/capacity canaries. The exact sources also
compile with `NOSTDLIB_EFI` and zero assembly references. Closed M01-020
source-links those production files into both evaluated opt-in EFI
workload-profile closures; their host tests remain excluded, and no `EfiEntry`
path invokes them. `CLROS_M01_011_READY` is a host-harness marker only and is
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
`NOSTDLIB_EFI` and zero assembly references. Closed M01-020 source-links the
production planner into the evaluated opt-in `SingleMinimalApi` and
`MultiMinimalApi` EFI closures; its host test remains excluded, and no
`EfiEntry` path invokes it. `CLROS_M01_012_READY` is a host-harness marker only,
not guest, EFI, QEMU, live-mapping, or completion-gate evidence.

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
references. At M01-020 closure, `PageZeroMappingPolicy` was source-linked into
both evaluated opt-in EFI workload-profile closures; `NullPageFaultProbePlan`
and the host test were excluded. The later #517 source integration includes
`NullPageFaultProbePlan` in the explicit EFI closure; the host test stays out.
At M01-015 closure no `EfiEntry` path invoked the policy and no live EFI or QEMU
execution had occurred. M01-025 later source-wired the policy into the
default-off stage-three adapter, and the accepted M01-026 tuple executes the
exact integrated page-zero-absence audit during its owned-root interval.
`CLROS_M01_015_READY` itself remains a host-harness readiness marker only.

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

The M01-024 dual-probe artifact supplies the first guest
allocation/free/double-free-rejection proof over the exact pre-EBS allocator
arena. It invokes the M01-007 subtractor, M01-006 state materializer, M01-008
one-page mutation, and M01-009 receipt-bound inverse through the reviewed
coordinator, restores the original accounting, and leaves the arena retained
only in that opt-in firmware-alive state. The default path still does not
invoke the chain. The broader typed-address migration, host-friendly
`MemoryMapInfo` writer and planners, other arenas, M01-010 seeded stress
oracle, `NativeAotHeapBootstrapPlanner`, and allocator-readiness marker gate
remain host-only or unsupported. There is still no page-content access or
zeroing, authoritative general free list, arbitrary free order, ABA-safe
allocation authority, managed-heap reservation, post-EBS ownership, live
paging, or NX/W^X enforcement. The finite stress corpus does not prove
concurrency, arbitrary-order free, allocator identity/generation authority,
indistinguishable-copy rejection, or ABA safety.

The `PageProtection`/`PageCachePolicy`/`X64PageTableEntry` boundary remains
host-tested in isolation. Closed M01-020 source-links it into the current
opt-in EFI compile closure, and M01-025's third opt-in source path consumes it
while preparing the owned table image. The accepted M01-026 tuple executes that
exact integrated source path once, but no default boot phase constructs tables
from it and isolated linkage or build success adds no live paging evidence. PAT
selection does not account for MTRR interactions or
prove a live IA32_PAT value or 1 GiB-page capability; decoded or re-encoded
Accessed/Dirty bits are caller bytes, not processor observations; table-pointer
permissions have no effective meaning until a complete hierarchy is built;
and the current strict format intentionally defers user/global/software/key
bits, LA57, PCID, live table synchronization, TLB shootdown, and `DmaMapping`.
M01-014 owns caller-owned table-image mutation and invalidation planning;
M01-019 now supplies the separate-leaf host coordinator and frozen stage-three
marker plan. M01-027 supplies the reviewed composite leaf, M01-020 owns
explicit EFI source linkage, M01-031 supplies the host-only physical-arena
contract, M01-032 supplies the host-only gate/observer contract, M01-025
source-wires the default-off adapter, and M01-035 adds checked immutable-text
publication ordering. The accepted M01-026 tuple owns the resulting bounded
cumulative QEMU claim.

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
M01-027 supplies the single-stream host encoding, M01-020 links it, M01-031
supplies the host-only physical-arena contract, M01-032 supplies the host-only
same-page gate and observation semantics, M01-025 source-wires the default-off
adapter, and M01-035 adds the final checked publication boundary. M01-026
retains the first cumulative QEMU evidence. That exact source-bound tuple
observes table construction, one temporary CR3 transition and restoration,
targeted TLB invalidation, one RW-to-RX protection probe, and page-zero absence
during the owned-root interval. It does not prove a permanent owned root,
restored-firmware or systemwide W^X/page-zero policy, PCID/LA57 support,
NMI/SMI safety, or a deliberate null dereference.

### M01-028 live-paging feasibility boundary

M01-028 changed only durable backlog intent, synchronization/readiness logic,
host tests, and canonical documentation; it changed no production paging
source and proved no execution. At that audit boundary, the integration built
raw entries in a flat model intentionally disjoint from the physical
`PageTableArena`; it could not supply an installed CR3 root without a separate
bounded materialization and readback boundary. Its mandatory `SerialStub`,
ACPI-scratch, and framebuffer-metadata identity bindings were historical or
synthetic rather than exact composite ranges, and exact table-page count
depended on the fixed candidate address selected before `AllocatePages`.
M01-030 and M01-031 repair those two host-contract gaps. M01-032 repairs the
final host-contract gap by placing a base-independent direct call in the same
page as the composite, fixing its exact call-next RIP, and defining a separate
returned observation POD whose first post-return instruction captures raw
RFLAGS. Caller-supplied success transcripts remain host models and cannot
substitute for returned live POD observations.

M01-029 establishes the first repaired boundary as a caller-owned POD
preparation-only contract. Its successful state is produced without synthetic
CR3 or protection execution transcripts and carries only deterministic
pre-execution evidence: allocation/map/ledger/reservation relationships,
current identity requirements, architectural preflight, byte-plan and
readback requirements, table and entry workspaces, protection and page-zero
plans, and cleanup state. The transition is one-shot and fail-closed: only
complete success commits the final preparation state, while architectural
preflight rejection precedes all caller-output writes. A later validation
failure may leave deterministic workspace construction but cannot publish a
partially prepared result. The preserved `Execute` compatibility entry and
`ExecuteModelOnly` remain historical model-only surfaces; their modeled
runtime events cannot be converted into or used to satisfy preparation for the
M01-027 composite.

The M01-029 harness markers `TEST_START m01.029`,
`CLROS_M01_029_READY`, `TEST_PASS m01.029`, and
`TEST_FAIL m01.029 <reason>` are host-only preparation evidence. They prove no
firmware allocation or call, generated-code invocation, physical page-table
materialization, exact live-identity repair, CR3/PTE/TLB/RFLAGS effect,
QEMU/OVMF or guest execution, or completion-gate change.

M01-030 repairs the second feasibility defect at the host-contract boundary.
Append-only exact purposes and roles enumerate only the combined
composite/call-gate code page, stack, probe, complete table/state arena,
state/canary page, and root page required by the future owned-root interval,
plus the non-emitted guard sentinel. Historical `SerialStub`, ACPI,
framebuffer, handoff, allocator-arena, loaded-image-continuation, and
`AdditionalRequired` aliases cannot satisfy those roles. Each accepted binding
has exact geometry, identity, cache, protection, allocation, reservation, and
containment relationships; the exact post-call return address lies in the RX
code-page tail. Same-policy normalization may combine adjacent or contained
requirements only while retaining every exact purpose bit. The supplied
records and page image remain caller-owned models, not proof that a mapping is
installed or executable.

The M01-030 markers `TEST_START m01.030`, `CLROS_M01_030_READY`,
`TEST_PASS m01.030`, and `TEST_FAIL m01.030 <reason>` prove only host rejection
or acceptance of that declared contract. They do not allocate/map pages,
materialize the physical hierarchy or call gate, invoke the composite,
observe CR3/PTE/TLB/RFLAGS effects, enforce W^X/NX or guard absence in
hardware, execute under EFI/QEMU/OVMF, or change a completion gate.

### M01-031 physical table-arena trust boundary

M01-031 closes the third feasibility defect at the host-contract boundary.
`PlanOwnedTableArena(...)` validates a caller-selected aligned physical
candidate inside one physically coherent `ConventionalMemory` descriptor,
rejects canonical-reservation overlap, and iterates the arena's own exact
identity requirement from the five-page lower bound through a validated
caller-supplied limit of 1 through 65 attempts. With candidate `C` and
converged table-page count `T`, it fixes root `C`, table interval
`[C, C + T * 4096)`, one final state/canary page
`[C + T * 4096, C + (T + 1) * 4096)`, exact allocation demand `T + 1`, exactly
`T` page-image records, and exactly `T * 512` entries. Descriptor capacity may
exceed demand, but the later `LoaderData` allocation record must match the exact
request, including its UEFI `Attribute`. The plan retains the four fixed
requirements; the materializer combines them with the derived arena, state,
and root records, rederives the exact seven-range plan, and requires full
nested-plan equality. Independent host vectors cross 2 MiB, 1 GiB, and 512 GiB
hierarchy boundaries and prove bounded convergence, physical-width/canonical
arithmetic, descriptor containment, and reservation policy.

`MaterializeOwnedTableArena(...)` keeps the reported physical candidate and
CPU-addressable backing pointer as separate trust domains. Every image is
written or verified at
`arenaBacking + (image.PhysicalStart - candidatePhysicalBase)` only after all
pointer/count/capacity spans, aliases, record order, physical uniqueness, exact
entry offsets, root placement, table containment, allocation geometry, and
state-page exclusion validate. A bounded full-hierarchy walk reconstructs each
leaf virtual address and rejects wrong physical identity, protection, cache
policy, strict encoding, or normalized-range coverage before any write.
`M01LivePagingArenaByteMode.EmitAndVerify` copies every 512-entry page and
performs an independent exact readback; `VerifyExisting` is read-only and
detects later corruption. Results report exact mismatch indexes/values and
deterministic source/readback FNV-1a64 aggregates, but per-entry equality
remains authoritative. A disjoint caller-owned 4096-byte scratch buffer holds
the complete pre-write state page and is compared byte-for-byte afterward;
state hashes are diagnostic only. The capacity tail remains outside table
writes.

Hostile host tests cover forged nested-plan evidence; wrong leaf identity,
protection, and cache policy; missing, duplicate, cyclic, unreachable,
misplaced, and malformed table images; exact allocation-attribute mismatch;
snapshot pointer/count/capacity/alias/overflow failures; first- and last-entry
corruption; exact state comparison; source nonmutation; and capacity-tail
preservation.

The host materializer proves exact writes and readback at offsets inside
caller-provided backing. It cannot prove that the backing is the reported
physical range, that the firmware allocation occurred, that the memory is
identity mapped or current, that another writer cannot modify it, or that any
CPU installed the root. The planner and materializer call no firmware
allocator, execute no generated code, change no CR3/PTE/TLB/protection/RFLAGS
state, and supply no EFI/QEMU/OVMF or completion-gate evidence.

The test-only hierarchy influence is
`references/SharpOS-main/OS/src/Kernel/Paging/X64PageTable.cs`, SHA-256
`35503B543628E5A462073D498704050EEEF7D79DEB2B84F01CACA29714200BAC`.
The archive has no recoverable Git revision, and no SharpOS source or algorithm
was copied. Its global allocator/spare list, permanent physical-pointer casts,
firmware-table clone, live-CR3 activation, large-page splitting, permissive
directory policy, and TLB effects remain outside CLROS.

The M01-031 markers `TEST_START m01.031`, `CLROS_M01_031_READY`,
`TEST_PASS m01.031`, and `TEST_FAIL m01.031 <reason>` are host-contract evidence
only. M01-032 supplies the host-only audited call gate and post-return RFLAGS
observation contract, M01-025 source-wires the default-off integration, and
M01-035 adds checked immutable-text publication ordering. M01-026 remains the
real-QEMU/OVMF proof owner and now preserves that bounded proof;
completion-gate status is unchanged.

### M01-032 composite gate trust boundary

M01-032 makes the previously modeled exact post-call address representable by a
deterministic gate in the tail of the same 4 KiB code page as the frozen
composite. The composite remains 3,830 bytes and 732 instructions. The aligned
gate fills `[0xF00, 0x1000)`, is 256 bytes and 74 instructions long, and uses
one direct `call rel32` at `0xFC4` whose next instruction at `0xFC9` is the only
accepted return site. Ten zero bytes remain between the prefix and gate, the
combined stream contains 806 instructions, and there is no external
relocation, absolute-address patch, callback, or second composite call.

Before its first write, push, or call, the gate uses only volatile registers.
It requires low-canonical 8-byte-aligned state and observation pointers; the
exact packed state header, self address, and `TableStateArenaStart`; entry
`RSP mod 16 == 8` with at least 128 lower and 40 upper bytes; the complete
observation to fit within the derived `[state, state + 4096)` page; the packed
`Armed` observation header and self address; a pristine qword spanning both
counts, reserved padding, and the completion signature; and a RIP-derived
`0xFC9` return address matching the state's expected return. Every failure uses
one common `InvalidState` return that writes no observation byte and does not
call the composite.

After those guards, the gate preserves Windows-x64 nonvolatile state, pushes
`RSI`, reserves 32 bytes of shadow space, biases `RSI` to
`observation + 127`, and enters the composite with a fixed 48-byte
gate-entry-to-composite-entry `RSP` delta. The guards, frame setup, lifecycle
write, and call-count increment change arithmetic flags, so the final
pre-transfer sequence is exactly
`push qword [RDX+48]; popfq; call rel32`. This reloads the prepared full-RFLAGS
snapshot immediately before transfer. Its first instruction at `0xFC9` is
`pushfq`; it captures raw RFLAGS before any flag-mutating instruction, then
reads restored CR3 and CR4 and records the composite result. The 160-byte
version-1 `M01OwnedCr3CompositeCallGateObservation` additionally carries its
lifecycle, exact call and observation counts, boundary canary, and a completion
signature written last. The pointer-free
`M01OwnedCr3CompositeCallGateResult` is 776 bytes and retains the exact
preparation, materialization, readback, and observation-audit evidence.

The frozen hashes are `F903C5DE16D3A5B2` (gate FNV-1a64),
`42CAF3B4A6C2119A` (combined-page FNV-1a64), and
`8B2965E741AC40B725B898D7B2BB2FFBDF7CC4C50DF9AEB163C17485270BD88A`
(gate SHA-256). The combined-page SHA-256 is
`FA1B2EC2332CF70A5222B22B672758A1E948FD0F179FB91A16D7DD4280FD5631`;
the unchanged composite prefix FNV-1a64 remains
`F00446F61BE13CDD`. Production materialization/readback preserves the ten-byte
gap and performs exact planned-byte, instruction-count, call/return-position,
and full-page comparisons. Host tests independently decode the emitted stream
to audit instruction boundaries, branches, control flow, and Windows-x64 ABI
discipline. Observation audit derives gate-entry `RSP` from the audited
composite-entry `RSP` with a checked 48-byte addition, revalidates the gate's
alignment and exact 128-byte lower/40-byte upper stack window, cross-binds the
composite-captured original RFLAGS to the prepared observation, and requires
one completed call, one post-return observation, the exact composite result,
exact RFLAGS/IF policy, exact firmware CR3/CR4 and PGE policy, and an unchanged
canary. Timeout, reset, and triple-fault transcripts remain failures rather
than synthetic success.

The in-stream guards are deliberately narrower than the host and live
pre-transfer contracts. They cannot prove either pointer is mapped, the state
root is page aligned, or the gate page has the exact live RX/non-writable/WB
mapping. They do not pre-write revalidate the observation's broader expected
code/composite/gate/return fields or pristine non-count output/reserved fields.
The caller must establish clear TF and disabled supervisor CET before transfer,
plus the intended LA57-disabled, PCIDE-disabled, IF, and PGE policies. The gate
cannot read full live-entry RFLAGS without writing to the not-yet-validated
stack, so it does not self-validate that live gate-entry RFLAGS equal the
prepared snapshot. The caller must establish exact full-qword equality, keep
the state and observation storage mapped and exclusively owned, retain
gate/composite write access, and prevent external or concurrent mutation of
their validated input/header fields through transfer. A mismatch is normalized
by the pre-call `popfq` and can
otherwise yield an apparently valid post-return record. M01-025 source-wires
the trusted invocation thunk and equality check under the default-off profile;
their source and host assertions are not live proof. The gate does not
eliminate NMI or SMI risk. `PrepareCallGate(...)` represents these as
failure-atomic host preflight, and observation/readback audit represents them
as host data evidence; neither is live execution proof.

The selective structural influence is
`references/SharpOS-main/bootasm/BootAsm.Generator/BootAsmGenerator.cs`,
SHA-256
`B24C79DCD38757DCBE243DC6C4C63C1AF628525F06C4F5D296B6AFB0C9387500`.
The archive has no recoverable Git revision; the owner grant and current
upstream CC0-1.0 evidence permit selective adaptation. M01-032 independently
authors its fixed encoder, POD/status surfaces, audit, and hostile tests. It
copies no Roslyn/Iced reflection generator, sentinel-hole implementation,
fallback skeleton, runtime absolute patch, Iced source or binary, or
third-party dependency.

The markers `TEST_START m01.032`, `CLROS_M01_032_READY`,
`TEST_PASS m01.032`, and `TEST_FAIL m01.032 <reason>` are host-only byte-plan
and returned-POD contract evidence. They prove deterministic same-page layout,
exact call-next binding, failure-atomic preflight, exact compare/readback plus
independent test decoding, complete hashes, and modeled observation-validation
semantics. Tests invoke neither the gate nor the composite and do not observe
hardware RFLAGS/CR3/CR4, change CR3/CR4/PTE/TLB/protection/interrupt state,
prove mapped pointers, a page-aligned state root, effective identity mapping or
W^X/WB policy, live TF/CET/LA57/PCIDE/IF/PGE preconditions, or NMI/SMI safety,
call firmware, run under EFI/QEMU/OVMF, or advance a completion gate. M01-025
source-wires the default-off invocation, M01-035 adds checked publication
ordering, and the accepted M01-026 tuple owns the resulting cumulative
real-QEMU evidence.

`PageZeroMappingPolicy` proves absence only in the validated caller-owned
snapshot passed to that call. Pointer arithmetic cannot prove the snapshot is
mapped, stable, current, exclusively owned, or identical to CR3, and the audit
does not cover a concurrent writer, another address space, a future
per-process root, or a later mutation. `NullPageFaultProbePlan` records fixed
fault intent and caller attestations only. The #517 integration includes it in
the explicit EFI closure, but the plan itself contains no executable stream,
does not use the older null-write byte plan, does not read address zero, does
not load an IDT, and does not capture CR2 or an error code. The accepted M01-026
tuple preserves cumulative QEMU evidence for page-zero absence during its
owned-root interval. The separate M02-014 adapter and its own actual guest
evidence determine whether the disabled-by-default live `#PF` trigger and
captured fault have been demonstrated.

The `X64BootIdentityMappingPlan` boundary remains host-tested only. M01-031 can
assign fresh physical table-page addresses inside one exact candidate and copy
the resulting flat image into separate caller-provided backing, but it cannot
prove those pages are firmware allocated or available at call time, reuse
firmware tables, or account for a later implementation that chooses large
pages. Caller-provided ranges can be semantically wrong even after their
geometry passes. The exact profile and backing audit cannot prove pointers are
mapped/stable, the gate bytes occupy the declared RX range, the backing is the
claimed physical arena, effective parent/leaf permissions are installed, the
guard or page zero is absent in hardware, or a CPU ever uses the reported root.
M01-032 supplies the host-only gate/observer repair, M01-025 source-wires the
default-off integration, and M01-035 adds checked immutable-text publication
ordering. The accepted M01-026 tuple supplies the bounded cumulative
real-QEMU evidence for the exact integrated path.

### M01-026 live owned-root evidence boundary

M01-026 was executed from clean, pushed source commit
`8f1edea17f2de425520e239af58d37aa3b182146`, tree
`6b5359e36293d770e3dc0d074b1baabf8d4fdbf8`. Its accepted r16 host package
preserves successful solution and host-test builds, naming/firewall and exact
M01 dry-run checks, clean diff/status checks, and one complete semantic host
run with 3,328 `TEST_START` lines, 3,329 `TEST_PASS` lines including the sole
`TEST_PASS host.tests`, no `TEST_FAIL`, and empty stderr. The host-run log
SHA-256 is
`996E343DC72C34B6F2521C1E703A58BB98362242A3E894D48647057F139F13F3`.

The full fresh `-BuildEfi` QEMU/OVMF acceptance command window started at
`2026-07-30T15:36:47.5722979Z` and completed at
`2026-07-30T15:37:49.8210616Z`. It used QEMU 11.0.50,
SHA-256 `B396EB9B669F6282EC60F0D46E6ADDCA8C669992E67A34365BE44CD3CB97C9A7`,
and OVMF SHA-256
`33090CC07675BAA5190D9F1E84BF5176B33BCBFA9BACAC522961150CDB6DBB2A`.
All 42 issue-required guest markers occurred exactly once and in order, all 25
issue-forbidden markers were absent, `M01_OWNED_CR3_ENTERED` was absent from
serial output, and both `TEST_PASS qemu.smoke` and
`TEST_PASS m01.026.raw-observations` were recorded. The independently verified
raw-observation sidecar binds firmware, before/after, and runtime values:
CR0 `0x80010033`; firmware CR3 `0xF801000`; temporary owned CR3 `0x100000`;
CR4 `0x668`; EFER `0xD00`; physical width 40; and RFLAGS `0x246`.

The EFI application SHA-256 is
`76F2B404E91EB5E62CAD80B797B936BA3D3D39D5B0577889BB8459AF1B55C47C`;
the raw image SHA-256 is
`D7D219AE32CDDA595C8A7685CD4F7733A932918258F1B52AA04C704F13333F82`;
the serial SHA-256 is
`CC51100244A8A20865DB9D16CE5D73B0930F43EA98835DFA57027A00B126C661`;
and the evidence-manifest SHA-256 is
`94A28FE683FC068C88F42AB806B3E9A06DB5467FD35488B6ADF397AC8F8A8188`.
The manifest binds 47 nonempty artifacts, 10 tool identities, 44 required
markers including the two base boot markers, 26 forbidden markers including
the no-serial-owned-interval marker, and 61 observed markers. All 47 artifact
hashes were independently revalidated; the final-state auxiliary ledger
SHA-256 is
`89A1A89B600698618E9DEDDE91FB1FABFD5B7B7993FA023AE8C5329C9B340F07`.
`done/step890.md` is the durable current-source record, while
`done/step888.md` remains immutable historical r12 evidence.

This satisfies only the documented M01 local-QEMU pre-EBS technical exit. It
does not prove real `ExitBootServices`, a permanent owned root, post-firmware
diagnostics, IDT/fault handling, SMP/TLB shootdown, systemwide W^X, physical
hardware, libvirt/provider execution, storage, networking, hosted .NET,
Minimal API, Kestrel, CloudReady status, or production readiness.

## Next Steps

1. Preserve the accepted bounded boot, diagnostics and runtime profiles while
   keeping their exact source and evidence boundaries distinct.
2. Extend reusable post-firmware platform interfaces and continuing execution
   only through the issues that own those remaining boundaries.
3. Add post-firmware physical-memory and MMIO ownership rules so the ACPI probe evidence can become durable platform discovery and PCI contracts can move from caller-owned bytes to live platform enumeration.
4. Keep all shared utility implementation in `std/no-runtime/shared` before kernel/app use.
5. Use `docs/workload-profiles.md` to keep the first hosted workload target on `SingleMinimalApi` while tracking the additional process/isolation work needed before `MultiMinimalApi`.
