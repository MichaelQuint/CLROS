# Drivers

## Current Status

No first-party hardware drivers exist. The first ACPI-adjacent boot contracts are host-tested and partly firmware-visible: `EfiConfigurationTableScanner` can locate an ACPI RSDP pointer from UEFI configuration table metadata, the pointer is carried into the typed boot handoff record, and the live EFI path emits `BOOT_ACPI_RSDP_HANDOFF_READY` plus `UEFI_ACPI_RSDP_READY` under QEMU/OVMF. An explicit opt-in ACPI physical discovery EFI probe now validates live RSDP/root SDT/MCFG bytes before `ExitBootServices` and emits the five `ACPI_*` discovery markers under QEMU/OVMF. Diagnostics now also has host-tested IDT descriptor/table-plan/fault-vector contracts, caller-owned panic text buffering/reporting, a serial readiness guard, COM1 configuration planning, an abstract COM1 writer, a COM1 platform serial transport over a fakeable backend, and x64 instruction-byte plans for future port-I/O and descriptor-table shellcode stubs. Storage has host-tested virtio-blk read-request/readiness, queue setup register-window fake execution with explicit setup barrier gating before `queue_enable`, read-session gating that requires that setup-barrier evidence before descriptor publication, read transaction planning, virtqueue split-ring layout, descriptor-chain metadata, caller-owned split-ring writer contracts, caller-owned descriptor ownership/free-list/reclaim state, caller-owned free-list-to-read-publication composition, caller-owned publish-completion-to-reclaim composition, caller-owned queue cursor advancement, range-validated read queue publication/completion, read queue block-result projection, block queue notification metadata, checked notify-relative offset derivation, notify register-window access planning, fake byte-span notify execution, explicit barrier-gated fake notify execution, read-notify readiness composition over read publication, notification metadata, checked notify-offset planning, and barrier-gated fake notify execution, read-notify completion composition over successful readiness plus caller-owned completion/reclaim, read-notify block-result projection into future `BlockReadResult` metadata, read pipeline composition over caller-owned buffers and fake notify bytes, read pipeline adapter preflight validation for caller-owned buffers, and read-session cursor carry-forward across repeated adapter reads, and drivers now have host-tested caller-owned ACPI RSDT/XSDT pointer-list parsing, ACPI table-directory selection over caller-provided SDT byte snapshots, physical-memory-backed MCFG child-table discovery through an injected `IPlatformPhysicalMemory`, ACPI MCFG parsing into PCI ECAM segments, PCI ECAM configuration-address planning, an injected physical-memory-backed PCI ECAM first-64-byte header reader, PCI type-0 configuration-header parsing, BAR metadata decoding, BAR-size derivation from caller-owned sizing readback snapshots, BAR sizing probe command planning for ordered config-space write/read/restore metadata, BAR sizing probe ECAM address binding for caller-provided PCI location metadata, fake config-accessor execution of those address-bound commands into caller-owned BAR sizing readback snapshots, fake-executor-to-derived-size composition through `PciBarSizingPlanner`, fake-derived BAR sizing metadata feeding single-device and bounded-catalog virtio PCI binding-plan composition, read-only PCI bus scanning over injected first-64-byte config-space readers, ECAM segment scan coordination that preserves MCFG segment/base metadata, guarded framebuffer metadata snapshot validation, modern virtio block/network candidate selection from ECAM scanner output, modern virtio PCI capability-list parsing from caller-provided config bytes, a virtio PCI identity/capability discovery contract for modern virtio block and network devices, virtio PCI BAR aperture validation against caller-provided decoded BAR metadata and sizes, virtio PCI binding-plan composition that preserves PCI location plus nested parse/discovery/BAR/aperture statuses, a bounded virtio PCI binding catalog over caller-provided config/BAR-size sources, and a virtio device descriptor catalog that attaches block/network device configuration plus initialization-profile metadata, but no live PCI enumeration or hardware driver is installed yet.

`VirtioPciBarRegisterWindowPlanner` now adds host-tested BAR/MMIO accessor readiness metadata. It validates caller-provided mapped-BAR window records against discovered virtio common, notify, ISR, and device-configuration apertures and plans aligned register-access addresses before a future live accessor is installed. It performs no BAR mapping, volatile MMIO, register reads/writes, queue configuration, device notification, interrupts, DMA mapping, or packet I/O.

`VirtioPciBarRegisterWindowAccessExecutor` now fake-executes already-validated register-window access plans against caller-owned BAR-window byte spans in host tests. It supports little-endian 1, 2, 4, and 8 byte reads/writes and preserves status-coded missing-plan and short-buffer failures, but it still performs no BAR mapping, physical address dereference, volatile/live MMIO, queue configuration, device notification, interrupts, DMA mapping, or packet I/O.

`PciVirtioDescriptorCatalogPlanner` now composes injected PCI ECAM scan metadata into the existing modern virtio candidate selector, fake-derived BAR-size binding catalog, and block/network descriptor catalog. It preserves scanned ECAM segment/BDF provenance, fake BAR-size provenance, feature/queue metadata, nested scan/binding/descriptor failures, and default/zero result-success rejection across caller-owned buffers without live PCI enumeration, live config access, ECAM/BAR mapping, MMIO, DMA, queue setup, packet I/O, or block I/O.

Generic virtio initialization now has a host-tested status/feature negotiation planning contract for the future point where a discovered device exposes feature bits and queue metadata. A device-kind profile layer now binds that generic planner to the current modern virtio block and network baselines without writing MMIO status registers.

Virtio-net queue planning now has a host-tested RX/TX queue contract for caller-owned Ethernet frame buffers and descriptor access direction, the virtio-net header parser can validate caller-owned no-offload RX buffers and prepare zeroed no-offload TX headers, the device-configuration reader can fake-read MAC/status bytes from caller-owned device BAR-window bytes into caller-owned scratch storage, and the TX lifecycle can publish a caller-owned Ethernet frame into caller-owned split-ring memory. `VirtioNetDriverQueueBringUpPlan` now composes a network descriptor/catalog entry with caller-owned RX/TX queue and buffer capacities into queue bring-up readiness metadata, `VirtioNetQueueMmioSetupPlanner` plans future common-configuration queue setup commands, `VirtioNetQueueMmioSetupExecutor` executes those planned commands only through an injected fake register accessor, `VirtioNetQueueNotificationPlanner` composes RX/TX publication metadata into future notify write plans and can now map successful notification plans to checked notify-aperture register-window access metadata, `VirtioNetQueueNotificationRegisterWindowExecutor` fake-executes those checked notify plans over caller-owned notify BAR-window bytes, `VirtioNetQueueNotificationRegisterWindowBarrierExecutor` gates that fake byte-span write on explicit barrier-readiness metadata, `VirtioNetQueueNotificationExecutor` executes those plans only through an injected fake notify sink or fake virtio PCI register-accessor adapter after explicit barrier-readiness metadata, `VirtioNetTransmitCompletionPlanner` maps one caller-owned TX used-ring entry back to the published descriptor chain only after successful fake-sink or register-window TX notification execution provenance, `VirtioNetTransmitDescriptorOwnershipPlan` marks the published TX header/frame descriptors in-flight and frees them only after a successful completion plan, `VirtioNetTransmitDescriptorFreeListPlanner` reads the same caller-owned state to select the first contiguous two-descriptor free TX pair without mutation, `VirtioNetTransmitDescriptorPublishPlanner` composes selection, lifecycle publication, and ownership marking for one caller-owned no-offload TX publish plan, and `VirtioNetTransmitRegisterWindowReclaimPlanner` composes successful register-window TX readiness with one caller-owned used-ring completion and descriptor-state reclaim. No live NIC queue is configured or notified.

`VirtioNetQueueMmioSetupRegisterWindowPlanner` now bridges the virtio-net queue setup commands to the BAR register-window access planner. It writes caller-owned per-command access-plan metadata for the future accessor while preserving setup and register-window failure status, but it still performs no fake or live register reads/writes and configures no hardware queue.

`VirtioNetQueueMmioSetupRegisterWindowExecutor` now bridges those same setup commands to the generic fake register-window access executor over caller-owned common BAR-window bytes. It stages expected queue-size and notify-offset read values because a byte span does not emulate queue-select-dependent hardware registers, and it still performs no BAR mapping, volatile/live MMIO, physical address access, memory barriers, live queue configuration, interrupts, DMA mapping, or packet I/O.

`VirtioNetQueueNotificationPlanner.PlanRegisterWindowAccess` now bridges successful virtio-net queue notification plans to the BAR register-window access planner. It returns caller-owned notify-aperture access-plan metadata for the future accessor while preserving notification and register-window failure status, but it still performs no fake or live register writes and does not notify hardware.

`VirtioNetQueueNotificationRegisterWindowExecutor` now fake-executes checked notify-aperture register-window access plans through the generic fake register-window access executor over caller-owned notify BAR-window bytes. It preserves queue, notify offset, write value, BAR-relative offset, access width, and nested status metadata without exposing BAR physical or virtual addresses, and it still performs no BAR mapping, volatile/live MMIO, memory barriers, interrupts, DMA mapping, packet I/O, or live device notification.

`VirtioNetQueueNotificationRegisterWindowBarrierExecutor` now adds the explicit descriptor-write, available-ring-write, and ordered-before-notify readiness gate before the fake register-window notify write. It rejects failed notification plans, missing barrier readiness, propagated register-window failures, and access-execution failures before or during caller-owned byte-span execution, but it still performs no real memory barrier, BAR mapping, volatile/live MMIO, interrupts, DMA mapping, packet I/O, or live device notification.

`VirtioNetReceiveReadinessPlanner` now composes successful queue setup register-window fake execution, caller-owned RX descriptor publication, explicit barrier-readiness metadata, RX notification planning, and barrier-gated notify register-window fake execution into one host-tested readiness result. This is still caller-owned common/notify BAR-window and queue-memory evidence only; it performs no live MMIO, BAR mapping, DMA allocation/mapping, packet I/O, interrupts, polling, receive completion, or live device notification.

`VirtioNetReceiveRegisterWindowDispatchPlanner` now composes successful RX register-window readiness with one caller-owned used-ring completion, selects the completed buffer from a caller-owned receive-buffer slab, runs receive-frame classification through the existing completion planner, and then feeds the selected caller-owned buffer into network dispatch metadata. This is still caller-owned queue-memory and receive-buffer-slab evidence only; it does not poll hardware, process interrupts, transfer packets, notify live hardware, mutate network state, or prove cloud, hosted .NET 10, ASP.NET Minimal API, or Kestrel behavior.

`VirtioNetDeviceConfigRegisterWindowReader` now fake-reads virtio-net MAC/status device-configuration bytes through checked device-configuration register-window access plans over caller-owned device BAR-window bytes. It copies into caller-owned scratch storage and reuses `VirtioNetDeviceConfig.TryParse`, preserving register-window, access-execution, scratch, and parse failures without live MMIO or device reads.

`VirtioBlockDeviceConfigRegisterWindowReader` now fake-reads the virtio-blk capacity field through the same checked device-configuration register-window executor path over caller-owned device BAR-window bytes. It writes the value into caller-owned scratch storage and reuses `VirtioBlockDeviceConfig.Parse`, preserving register-window, access-execution, scratch, and parse failures without live MMIO, queue configuration, notification, or block I/O.

`VirtioBlockQueueMmioSetupPlanner` now plans the queue-0 virtio-blk common-configuration command sequence from a successful block descriptor, caller-provided queue/ring physical-address metadata, and caller-provided queue metadata. It produces future queue-select, queue-size, descriptor/driver/device address, notify-offset, and queue-enable command records while preserving validation failures, but it still does not map BARs, invoke a register accessor, perform live MMIO, allocate or map DMA memory, publish descriptors, notify hardware, process interrupts, or read blocks.

`VirtioNetTransmitReadinessPlanner` now composes successful fake-accessor queue setup execution, caller-owned TX descriptor publication, explicit barrier-readiness metadata, notification planning, and fake-accessor-backed notification execution into one host-tested readiness result. This is still fake-accessor and caller-owned-buffer evidence only; it performs no live MMIO, BAR mapping, DMA allocation/mapping, packet I/O, interrupts, polling, or live device notification.

`VirtioNetTransmitRegisterWindowReadinessPlanner` now composes successful queue setup register-window fake execution, caller-owned TX descriptor publication/state marking, explicit barrier-readiness metadata, TX notification planning, and barrier-gated notify register-window fake execution into one host-tested readiness result. This is still caller-owned common/notify BAR-window, queue-memory, TX-header, and descriptor-state evidence only; it performs no live MMIO, BAR mapping, DMA allocation/mapping, packet I/O, interrupts, polling, completion waiting, or live device notification.

`VirtioNetTransmitRegisterWindowReclaimPlanner` now composes that successful register-window TX readiness result with one caller-owned used-ring completion validation and caller-owned descriptor-state reclaim. It preserves readiness, notification-window, completion, and ownership status evidence while rejecting failed readiness, missing completion, and ownership-state mismatches before any broader runtime allocator exists.

The first ENA/Nitro slices are also metadata-only. `EnaPciCandidateSelector` can select Amazon ENA VF and LLQ VF candidates from caller-provided PCI ECAM scan metadata while preserving source index, segment, ECAM base, bus range, BDF, identity, class metadata, header type, and capability pointer. `EnaAdminDescriptors` can encode `GET_FEATURE DEVICE_ATTRIBUTES` admin commands into caller-owned AQ descriptor bytes, parse ACQ completion header/status metadata, and reject default/uninitialized completion entries before successful validation. `EnaAdminCommandPlan` composes that descriptor work with caller-buffer completion validation while rejecting default result values. `EnaAdminQueueLayoutPlan` can calculate caller-owned Admin Queue and Admin Completion Queue offsets, lengths, alignment metadata, total required bytes, and initial producer/consumer/phase values from explicit depth, entry-size, alignment, memory-budget, and contiguous-memory inputs, and default/zero plans do not satisfy the budget-fit predicate. `EnaDeviceAttributesParser` can parse a caller-provided successful `GET_FEATURE DEVICE_ATTRIBUTES` response into MAC address, MTU, address-width, supported-feature, and capability metadata while rejecting invalid completions and malformed payloads. `EnaDataQueueDescriptors` can parse caller-owned TX/RX data queue descriptor and completion bytes and rejects default TX completion entries before successful validation. `EnaRegisterMap` and `EnaAdminReadinessPlanner` centralize ENA register offsets, capability/status bits, register aperture validation, and caller-provided readiness metadata rejection. `NvmeRegisterMap` centralizes baseline NVMe controller/admin-queue register offsets, doorbell stride metadata, and default-safe aperture validation for future Nitro NVMe work. `NitroNvmePlanner` composes caller-provided PCI/BAR snapshots plus identify-buffer bytes into a metadata-only namespace plan and rejects default result values. These contracts do not read ECAM, size or map BARs, allocate DMA memory, write ENA or NVMe registers, ring doorbells, poll completions, configure AENQ, move packets, or read blocks.

## Implemented Behavior

- `EfiConfigurationTableScanner` recognizes UEFI ACPI 2.0 and ACPI 1.0 configuration table GUIDs, prefers ACPI 2.0 when both are present, supports ACPI 1.0 fallback, rejects zero RSDP pointers, and returns status-coded results.
- `EfiMain` source-links the pointer scanner and emits `BOOT_ACPI_RSDP_HANDOFF_READY`, `UEFI_ACPI_RSDP_READY`, `UEFI_ACPI_RSDP_MISSING`, or `UEFI_ACPI_RSDP_INVALID <status-code>` on the default path without dereferencing the RSDP.
- `scripts/build-efi-shell.ps1 -EnableAcpiPhysicalDiscoveryProbe` publishes an opt-in EFI artifact that dereferences the firmware-provided ACPI pointers before `ExitBootServices`, validates live RSDP/root SDT/MCFG bytes into fixed scratch storage, and emits `ACPI_RSDP_SNAPSHOT_READY`, `ACPI_RSDP_VALIDATED`, `ACPI_ROOT_SDT_SNAPSHOT_READY`, `ACPI_ROOT_SDT_VALIDATED`, and `ACPI_MCFG_DISCOVERY_READY`.
- `BootHandoffRecord`, `BootHandoffInfo`, `EfiBootHandoffValidator`, and `BootSequenceHandoffValidator` carry and validate ACPI RSDP pointer provenance as status-coded metadata.
- `AcpiRsdpValidator` validates RSDP byte buffers for ACPI 1.0 and ACPI 2.0+ checksum/length rules in host tests. Equivalent no-stdlib validation is wired only into the opt-in ACPI physical discovery EFI probe, not the default path.
- `AcpiRsdpSnapshotReader` composes `IPlatformPhysicalMemory` with `AcpiRsdpValidator` to snapshot and validate ACPI 1.0 and ACPI 2.0+ RSDP bytes from a caller-provided physical address into caller-owned scratch storage. It distinguishes zero physical addresses, unavailable physical-memory sources, scratch-capacity failures, base and extended physical-read failures, and base and extended validation failures without walking SDTs or mapping physical memory.
- `AcpiRootSdtSnapshotReader` consumes a successful RSDP snapshot and snapshots the root RSDT or XSDT bytes through `IPlatformPhysicalMemory`. It prefers XSDT when available, falls back to RSDT when ACPI 2.0+ provides no XSDT address, validates the expected root-table signature, declared length, checksum, and pointer-array alignment, and leaves child-table parsing to later steps.
- `AcpiSdtParser` parses caller-provided ACPI RSDT/XSDT table bytes using `OS.Std.Binary.EndianPrimitives`. It validates SDT signature, declared length, source length, checksum, pointer-array alignment, zero table pointers, and destination capacity, then writes RSDT 32-bit or XSDT 64-bit physical-address values into caller-owned storage without dereferencing them.
- `AcpiTableDirectory` bridges parsed RSDT/XSDT pointer lists to caller-provided ACPI SDT byte snapshots. It validates the common SDT header, declared length, and checksum for each snapshot, selects the first requested table signature in firmware pointer order, preserves the selected physical address, and reports missing sources, invalid checksums, destination exhaustion, and signature misses without reading physical memory.
- `AcpiMcfgParser` parses caller-provided ACPI MCFG table bytes into `PciEcamSegment` records using `OS.Std.Binary.EndianPrimitives`. It validates the `MCFG` signature, declared minimum length for at least one allocation entry, source length, the 8-byte reserved area after the ACPI SDT header, 16-byte allocation-entry alignment, destination capacity, and inclusive bus ranges without physical memory, UEFI, MMIO, or exceptions in the parser path.
- `AcpiMcfgDiscovery` composes `AcpiTableDirectory.TrySelect` with `AcpiMcfgParser.TryParse`. It accepts either caller-provided RSDT/XSDT bytes plus pointer scratch or a direct caller-provided pointer list, selects the first checked `MCFG` snapshot, parses ECAM allocation entries into caller-owned `PciEcamSegment` storage, and preserves nested status plus selected physical-address/source metadata for diagnostics. It still performs no physical-memory reads.
- `AcpiMcfgDiscovery.TryDiscoverFromRootSdtSnapshot` composes the root SDT snapshot with `IPlatformPhysicalMemory`. It parses RSDT/XSDT child pointers from caller-owned root table bytes, reads each child header through the physical-memory boundary, selects `MCFG`, copies the selected child table into caller-owned storage, validates checksum, parses ECAM allocation entries, and reports header-read, full-read, checksum, missing-table, parse, and capacity failures with value-type status metadata.
- `IPlatformPhysicalMemory` and `GuardedPlatformPhysicalMemory` define the first guarded platform boundary for future ACPI table byte snapshots from firmware-provided physical addresses. The current implementation uses an injected source in host tests, validates caller-owned destination capacity and range metadata, and denies reads after UEFI-gone simulation or `ExitBootServices`; it does not map or dereference live physical memory.
- `IPlatformFramebuffer` and `GuardedPlatformFramebuffer` define a guarded metadata snapshot boundary for future GOP framebuffer handoff. The current implementation uses an injected source in host tests, validates base address, byte length, width, height, stride, supported RGBX/BGRX pixel formats, required byte-length arithmetic, source status, and firmware-services mode, and denies snapshots after UEFI-gone simulation or `ExitBootServices`; it does not call GOP, map framebuffer memory, render text, or provide post-firmware output.
- `IPlatformFileSystem` and `GuardedPlatformFileSystem` define a guarded read-only firmware-filesystem boundary for future UEFI Simple File System work. The current implementation uses an injected source in host tests, validates safe relative ASCII paths and caller-owned destination capacity, and denies reads after UEFI-gone simulation or `ExitBootServices`; it does not wire the UEFI protocol, enumerate directories, write files, load packages, or read live disks.
- `IdtGateDescriptor` and `FaultVectorCatalog` provide host-tested contracts for future interrupt descriptor table setup.
- `IdtTablePlan` provides a host-tested required-fault-vector table plan and deterministic 4096-byte IDT descriptor export into caller-owned storage.
- `SerialDiagnosticsGuard` provides a host-tested readiness contract that distinguishes firmware console output from future OS-owned serial output.
- `SerialPortConfiguration` provides host-tested COM1 defaults, divisor-latch calculation, and initialization port/value write planning without performing port I/O.
- `Com1SerialWriter` provides a host-tested COM1 initialization, transmit-ready polling, ASCII write, timeout, invalid ASCII, and backend-failure contract over a fakeable byte-port backend.
- `Com1PlatformSerialTransport` adapts the COM1 writer into the `IPlatformSerialTransport` owned-serial boundary. It can satisfy `GuardedPlatformSerial` after UEFI-gone simulation when given a fakeable backend, maps initialization/read/write/timeout/partial-write outcomes into platform serial status metadata, and still performs no CPU port I/O by itself.
- `PortIoOperationPlan` provides host-tested one-byte x64 `out dx, al` and `in al, dx` opcode plans for a future C#-emitted COM1 backend.
- `PortIoStubPlan` provides host-tested callable x64 COM1 port-I/O stub byte plans for future unmanaged `Out8` and `In8` delegates.
- `DescriptorTableInstructionPlan` provides host-tested x64 `lidt`, `lgdt`, and `ltr` instruction-byte plans for future descriptor-table installation stubs.
- `DescriptorTableInstallStubPlan` provides host-tested callable x64 descriptor-table install stub byte plans with embedded IDTR/GDTR payloads and TSS selector loading.
- `Com1PortIoBackendPlan` provides a host-tested readiness contract for a future COM1 port-I/O backend, including serial configuration, port operation/stub plans, and executable-stub memory layout capacity.
- `OwnedCom1DiagnosticsReadiness` composes the COM1 backend plan, backend address contract, owned-serial readiness derivation, serial diagnostics guard, and post-firmware diagnostics marker boundary into one host-tested metadata result. It does not invoke generated stubs, execute port I/O, call firmware, or prove live serial output.
- `DescriptorTableInstallReadiness` provides a host-tested readiness contract for future descriptor-table installation prerequisites and callable install-stub buffer sizing.
- `VirtioBlockRequestPlan` provides a host-tested read-only virtio-blk request layout contract for the future block driver: fixed read/write request type values, 16-byte request header metadata, sector/count metadata, header/data/status descriptor directions, read-only feature validation, minimum queue descriptor validation, and status-coded write rejection.
- `VirtioBlockReadTransactionPlan` provides a host-tested read transaction planning contract for the future block driver: fixed 512-byte sector geometry validation, sector-range validation, caller-owned request-header/data/status buffer validation, minimum queue descriptor validation, and deterministic header preparation without DMA mapping, MMIO, interrupts, queue publication, or disk reads.
- `VirtqueueSplitRingPlan` provides a host-tested split-ring layout contract for future virtio drivers: queue-size validation, descriptor-table/available-ring/used-ring offsets, required alignment, required byte length, default/zero result rejection, and status-coded overflow rejection.
- `VirtioNetTransmitLifecycle` provides a host-tested caller-owned virtio-net TX publication contract: zero no-offload header preparation, two device-readable descriptor writes, available-ring head/index updates, caller-provided physical-address metadata, frame-size bounds, descriptor-range validation, queue-memory bounds, physical-range overflow rejection, and default/zero result rejection without DMA allocation, MMIO, device notification, completion waiting, or packet transmission.
- `VirtioBlockDescriptorChainPlan` provides a host-tested read-only virtio-blk descriptor-chain publication metadata contract: exact three-descriptor header/data/status chain planning, descriptor index/flag/length recording, caller-provided physical address placeholders, available-ring index/slot/entry offset calculation, and required memory-range recording without queue-memory writes, device notification, interrupts, or disk reads.
- `VirtioBlockDescriptorChainWriter` provides a host-tested caller-owned split-ring memory writer for future virtio-blk queue publication: it writes descriptor-table entries in little-endian format, writes the planned available-ring head entry, advances `avail.idx`, preserves unrelated queue bytes, rejects failed plans, undersized memory, descriptor index range failures, invalid available-ring slots, and overflow, and still performs no DMA allocation, MMIO, device notification, used-ring waiting, or disk reads.
- `VirtioBlockDescriptorFreeListPlanner` and `VirtioBlockDescriptorOwnershipPlan` provide a host-tested caller-owned descriptor state contract for future virtio-blk read queues. The free-list planner selects the first contiguous three-descriptor `Free` chain and rejects invalid state or capacity exhaustion without mutating state. The ownership planner marks only successfully published read descriptors `InFlight`, reclaims them only after successful lifecycle completion, and rejects failed publish/completion provenance, short state tables, in-flight reuse, not-in-flight completion, and duplicate completion without MMIO, DMA allocation/mapping, live notification, interrupts, polling, or disk reads.
- `VirtioBlockDescriptorPublishPlanner` composes the virtio-blk free-list selector with `VirtioBlockReadLifecycle.PublishReadRequest` and ownership marking. It selects reusable caller-owned descriptors, writes only caller-owned request-header/split-ring memory through the lifecycle path, marks descriptors in flight only after successful publication, preserves nested status provenance and attempted flags, and performs no MMIO, DMA allocation/mapping, live notification, interrupts, polling, completion waiting, or disk reads.
- `VirtioBlockDescriptorReclaimPlanner` composes a successful virtio-blk publish-plan result with one caller-owned used-ring/status-buffer completion and caller-owned descriptor-state reclaim. It rejects failed publish plans before reading queue memory, rejects no-completion before mutating descriptor state, rejects ownership-state mismatches after completion validation, preserves nested publish/completion/ownership status evidence, and performs no live MMIO, BAR mapping, volatile access, DMA allocation/mapping, live notification, interrupts, polling, completion waiting, block I/O, or disk reads.
- `VirtioBlockQueueCursorPlanner` carries caller-owned available/used ring indexes through virtio-blk publication and reclaim. It advances only the available index after successful publication, advances only the used index after successful completion/reclaim, preserves indexes on failed publish or reclaim, and performs no live MMIO, BAR mapping, volatile access, DMA allocation/mapping, live notification, interrupts, polling, hardware waiting, block I/O, or disk reads.
- `VirtioBlockReadQueuePlanner` composes `BlockDeviceInfo` geometry/range validation with the caller-owned virtio-blk cursor publication path. It rejects invalid geometry and out-of-range reads before touching caller-owned request or queue memory, advances only the available index on successful publication, preserves cursor state on publish failure, and performs no live MMIO, BAR mapping, volatile access, DMA allocation/mapping, live notification, interrupts, polling, hardware waiting, block I/O, or disk reads.
- `VirtioBlockReadQueueCompletionPlanner` composes a successful read queue publication result with the cursor reclaim path. It rejects failed publications before reading queue memory, advances only the used index after successful completion/reclaim, preserves cursor state on missing completion, and performs no live MMIO, BAR mapping, volatile access, DMA allocation/mapping, live notification, interrupts, polling, hardware waiting, block I/O, or disk reads.
- `VirtioBlockReadQueueBlockReadProjector` maps read queue completion results into future `IReadOnlyBlockDevice`-style `BlockReadResult` metadata. It reports blocks/bytes on successful completion, preserves validation failures as their existing block-device status, maps completion failure to `IoError`, and performs no queue/status reads, memory writes, cursor mutation, descriptor mutation, MMIO, DMA, notification, polling, interrupts, or disk reads.
- `VirtioBlockQueueNotificationMetadataPlanner` derives metadata for a future virtio-blk queue notify write from successful read queue publication. It records queue index, notify offset, descriptor count, available-ring index transition, descriptor head, write value, and write width while rejecting failed publications and performing no queue/status reads, memory writes, cursor mutation, descriptor mutation, MMIO, BAR mapping, memory barriers, DMA, notification execution, polling, interrupts, or disk reads.
- `VirtioBlockQueueNotifyOffsetPlanner` derives the checked notify-aperture relative offset from queue notification metadata and the virtio notify offset multiplier. It preserves notification metadata, rejects failed notification metadata, reports overflow, and performs no queue/status reads, memory writes, cursor mutation, descriptor mutation, MMIO, BAR mapping, memory barriers, DMA, notification execution, polling, interrupts, or disk reads.
- `VirtioBlockQueueNotificationRegisterWindowPlanner` binds successful virtio-blk notify-offset metadata to the existing virtio PCI BAR register-window planner. It produces checked notify-aperture access metadata for the future 16-bit queue notify write while preserving queue, descriptor, available-ring, write-value, offset, and nested register-window statuses without BAR mapping, volatile MMIO, fake or live register writes, memory barriers, DMA, notification execution, polling, interrupts, or disk reads.
- `VirtioBlockQueueNotificationRegisterWindowExecutor` fake-executes a successful virtio-blk notify register-window access plan through the generic caller-owned BAR-window byte-span executor. It writes only the planned 16-bit queue value to caller-owned notify bytes, preserves nested planner and access-execution statuses, rejects failed plans before touching bytes, and reports short byte spans without BAR mapping, volatile/live MMIO, physical address access, real memory barriers, DMA, live notification, polling, interrupts, or disk reads.
- `VirtioBlockQueueNotificationRegisterWindowBarrierExecutor` gates that fake virtio-blk notify register-window write on explicit descriptor-write, available-ring-write, and ordered-before-notify readiness metadata. It preserves barrier sequence and execution-attempt metadata, rejects failed plans or missing readiness before touching caller-owned bytes, and still performs no real memory barrier, BAR mapping, volatile/live MMIO, physical address access, DMA, live notification, polling, interrupts, or disk reads.
- `VirtioBlockReadQueueNotifyReadinessExecutor` composes a successful read queue publication with notification metadata derivation, checked notify-offset planning, explicit barrier readiness, and barrier-gated fake notify register-window execution. It preserves nested publication, notification metadata, offset, barrier, and notify execution statuses; rejects failed publication and missing barrier before fake notify writes; propagates notify execution failures; and performs no real memory barrier, BAR mapping, volatile/live MMIO, DMA, live notification, polling, interrupts, completion waiting, or disk reads.
- `VirtioBlockReadQueueNotifyCompletionPlanner` gates the existing read queue completion path on successful read-notify readiness. It rejects failed readiness before reading caller-owned used-ring/status bytes or reclaiming descriptors, delegates successful readiness to `VirtioBlockReadQueueCompletionPlanner`, preserves nested readiness/completion/reclaim/ownership statuses, and performs no live MMIO, BAR mapping, real memory barriers, DMA allocation/mapping, hardware polling/waiting, interrupts, payload copying, or disk reads.
- `VirtioBlockReadQueueNotifyBlockReadProjector` maps gated read-notify completion into future `BlockReadResult` metadata. It preserves notify-readiness, notify-completion, read-queue completion, reclaim, completion-reader, and ownership provenance, reports requested/completed block-byte counts on success, maps failed readiness or completion/reclaim failure to `IoError`, and performs no queue/status reads, memory writes, cursor or descriptor-state mutation, BAR mapping, volatile/live MMIO, real memory barriers, DMA, hardware notification, polling, interrupts, block I/O, or disk reads.
- `VirtioBlockReadQueuePipelineComposer` orchestrates one read publication, barrier-gated fake notify byte execution, readiness-gated completion/reclaim, and projection into future `BlockReadResult` metadata. It preserves nested pipeline, publication, readiness, completion, and projection statuses and performs no live MMIO, BAR mapping, real memory barriers, DMA allocation/mapping, hardware notification, polling/waiting, interrupts, runtime descriptor allocation, payload copying, block I/O, or disk reads.
- `VirtioBlockReadQueuePipelineAdapter` adds a preflight boundary over the read pipeline composer. It validates caller-owned request-header, destination, status, queue-memory, descriptor-state, and notify-window capacity before pipeline execution, rejects short buffers without mutating cursor/descriptor/request/queue/status/notify state, and preserves nested pipeline failure metadata after successful preflight.
- `VirtioBlockReadOnlyReadSession` is a ref-struct caller-owned state wrapper over the adapter. It carries queue/cursor/descriptor/request/status/notify buffers across repeated reads, exposes cursor-before/after result metadata, delegates each read to the adapter, and owns no allocations or live driver behavior.
- `PciEcamConfigurationSpace` provides a host-tested PCIe ECAM configuration-space address planning contract. It models an MCFG-style segment base, segment group, inclusive bus range, bus/device/function/register tuple validation, 1/2/4-byte access width and natural alignment checks, the standard 1 MiB per bus / 32 KiB per device / 4 KiB per function offset formula, address-overflow rejection, and default/zero result-success rejection. It does not perform MMIO reads or enumerate devices.
- `OS.Kernel.Platform.PhysicalMmioOwnershipPlan` provides a host-tested post-firmware MMIO ownership planning contract for parsed PCI ECAM segments. It writes caller-owned range metadata, validates ECAM window alignment, bus ranges, physical-address overflow, destination capacity, ECAM-window overlap, overlap with allocator-usable conventional memory, and default/zero result-success rejection, and performs no live ECAM mapping, volatile MMIO, PCI enumeration, BAR mapping, or driver binding.
- `PciEcamPhysicalHeaderReader` uses `PciEcamConfigurationSpace` plus `IPlatformPhysicalMemory` to copy one function's first 64 PCI configuration bytes from an ECAM physical address into caller-owned storage. `PciEcamPhysicalConfigurationSpaceHeaderReader` adapts that result into the existing `PciConfigurationSpaceHeaderReader` delegate used by `PciBusScanner`. The result rejects default/zero values as not successful, and the host tests use an injected physical-memory source only; no ECAM is mapped and no MMIO instruction is executed.
- `PciConfigurationHeader` provides a host-tested read-only parser for the first 64 bytes of PCI type-0 configuration space. It uses `OS.Std.Binary.EndianPrimitives`, returns vendor/device identity, revision/class/subclass/programming-interface metadata, raw header type, raw BAR0-BAR5 values, status-register metadata, and a validated capability pointer when the status register advertises a capabilities list. It reports status-coded failures for short buffers, invalid vendor `0xFFFF`, unsupported non-type-0 headers, invalid capability-pointer alignment, invalid capability-pointer range, and default/zero result-success evidence without walking capabilities or touching ECAM/MMIO.
- `PciBarDecoder` provides a host-tested read-only decoder for the six raw type-0 BAR values already captured by `PciConfigurationHeader`. It records absent zero BARs, I/O apertures, 32-bit MMIO apertures, 64-bit MMIO low/high pairs, prefetchability, masked base-address bits, consumed high slots, deterministic malformed statuses for reserved memory BAR types or a 64-bit memory BAR in slot 5, and default/zero result-success rejection. It does not size BARs, write all-ones, map MMIO, allocate pages, configure MSI/MSI-X, or bind drivers.
- `PciBarSizingPlanner` derives BAR aperture sizes from decoded BAR descriptors plus caller-owned all-ones sizing readback snapshots. It records 32-bit and 64-bit MMIO sizes, absent BARs, I/O BARs, consumed upper halves of 64-bit BARs, malformed readbacks, and base-alignment mismatches, can copy the resulting sizes into the existing virtio aperture-validation input shape, and rejects default/zero plan and descriptor values as not initialized even though their status enum value is `Success`. It performs no live PCI config writes, live config reads, BAR restore, BAR mapping, MMIO, DMA, interrupts, packet I/O, or block I/O.
- `PciBarSizingProbePlanner` plans the ordered config-space write-all-ones, read-sizing-readback, and restore-original commands that a future PCI config accessor would execute for present MMIO BARs, and rejects default/zero plan-result values as not initialized. `PciBarSizingProbeAddressPlanner` binds those planned commands to checked ECAM config-space address metadata for a caller-provided segment and bus/device/function, and rejects default/zero address-plan values as not initialized. `PciBarSizingProbeExecutor` fake-executes the address-bound commands through an injected config-accessor test double, captures readbacks in caller-owned BAR-indexed storage, and rejects default/zero execution and config-read values as not initialized. `PciBarSizingProbeDerivedSizePlanner` composes those fake-captured readbacks into `PciBarSizingPlanner` so derived aperture metadata and virtio aperture-validator input can be tested while preserving nested address-plan, execution, and sizing statuses, and rejects default/zero derived-size values as not initialized. These contracts perform no live PCI config writes, live config reads, hardware BAR restore, ECAM mapping, BAR mapping, MMIO, DMA, interrupts, packet I/O, or block I/O.
- `PciBusScanner` provides a host-tested read-only PCI bus scanner over an injected configuration-space reader delegate. The reader fills caller-owned first-64-byte function buffers, and the scanner walks only caller-specified bus/device bounds plus a caller-bounded maximum function. It skips vendor `0xFFFF` functions, honors the PCI multifunction header bit before scanning functions 1-7, records bus/device/function identity, class/subclass/programming-interface, header type, and capability-list presence, rejects default/zero scan results as successful evidence, and reports destination-capacity, reader, and header-parse failures with deterministic status metadata. It does not perform live ECAM MMIO reads, BAR mapping, capability walking, driver binding, or interrupt configuration.
- `PciEcamSegmentScanner` coordinates caller-provided `PciEcamSegment` values from the MCFG path with `PciBusScanner.Scan`. It uses an injected `PciConfigurationSpaceHeaderReader`, validates each segment bus range before scanning, writes ECAM-aware device records into caller-owned storage, preserves segment index, segment group, ECAM base physical address, and bus range on every output device, rejects default/zero segment scan results as successful evidence, and reports destination-capacity and nested scanner failures with deterministic status metadata. It still does not map ECAM, dereference physical addresses, perform MMIO, size BARs, walk capabilities, configure interrupts, or bind drivers.
- `OS.Drivers.VirtioPciCandidateSelector` filters caller-provided `PciEcamScannedDevice` records into caller-owned modern virtio block/network candidate records. It accepts only vendor `0x1AF4`, modern virtio network/block device IDs `0x1041` and `0x1042`, and devices with the PCI capabilities-list bit plus a nonzero capability pointer. It preserves segment index, segment group, ECAM base address, bus range, bus/device/function, identity, virtio device kind, and capability pointer, reports destination exhaustion, and rejects default/zero result-success values without walking capabilities, validating BARs, mapping MMIO, or binding drivers.
- `OS.Drivers.VirtioPciCapabilityListParser` walks standard PCI capability headers from caller-provided configuration-space bytes and a caller-provided start offset. It recognizes vendor-specific capability ID `0x09`, decodes modern virtio PCI common/notify/ISR/device/PciConfiguration capability records, captures BAR, offset, length, and notify offset multiplier where applicable, rejects loops, unaligned capability offsets, truncated/out-of-range records, invalid BARs, unsupported virtio capability types, overflowing BAR-relative regions, missing required common/notify/ISR/device configuration records, and default/zero result-success values. It does not read config space live or map BARs.
- `OS.Drivers.VirtioPciDeviceDiscovery` provides a host-tested virtio PCI discovery contract over already-decoded PCI identity and virtio capability metadata. It accepts vendor `0x1AF4`, requires modern device IDs in `0x1040..0x107F`, maps virtio device ID `1` to network and `2` to block, records common/notify/ISR/device-configuration capability regions, validates BAR/offset/length metadata, rejects default/zero discovery results as successful evidence, and reports status-coded unsupported or missing/invalid capability failures without exceptions in the kernel-facing path.
- `OS.Drivers.VirtioPciBarApertureValidator` validates a successful `VirtioPciDeviceDiscoveryResult` against decoded PCI BAR metadata and caller-provided BAR aperture sizes. It requires the common, notify, ISR, and device configuration capabilities to target present memory BARs, accepts 32-bit and 64-bit memory BARs, rejects I/O BARs, absent BARs, consumed upper halves of 64-bit BAR pairs, malformed BARs, invalid BAR indexes, missing size entries, offset/length overflow, regions beyond the supplied BAR size, and default/zero validation-result values as successful evidence. It does not size BARs, write PCI config space, map MMIO, negotiate features, or touch device registers.
- `OS.Drivers.VirtioPciIsrStatusParser` parses caller-provided ISR status bytes and creates a one-byte ISR read-plan from a validated ISR aperture. It reports invalid aperture capability, aperture capacity, and default/zero result-success failures while preserving the no-MSI-X/no-APIC/no-IOAPIC/no-queue-drain/no-packet-dispatch boundary.
- `OS.Drivers.VirtioPciQueueNotificationPlanner` validates a notify aperture, queue index/count, selected notification features, queue enabled state, descriptor-publication intent, and notify-offset range before returning one future 16-bit queue notify write plan. It rejects default/zero result-success failures and performs no descriptor publication, MMIO write, interrupt handling, or packet I/O.
- `OS.Drivers.VirtioPciBindingPlan` composes the host-tested virtio PCI parser/discovery/BAR/aperture contracts into one caller-buffer metadata plan for modern virtio block and network devices. It parses caller-provided PCI configuration bytes, walks the modern virtio PCI capability list, discovers supported virtio identity, decodes type-0 BAR metadata, validates required virtio apertures against caller-provided BAR sizes, preserves ECAM segment/bus/device/function location, exposes each nested result so failures remain attributable, and rejects default/zero result-success values. It does not perform live config reads, BAR sizing writes, MMIO mapping, feature negotiation, queue setup, driver attach, or device I/O.
- `OS.Drivers.VirtioPciBindingDerivedBarSizePlanner` composes caller-provided PCI configuration bytes, decoded BAR metadata, fake-derived BAR sizes from `PciBarSizingProbeDerivedSizePlanner`, and `VirtioPciBindingPlan` into one host-tested binding path for modern virtio block and network devices. It copies derived sizes into caller-owned storage, preserves nested header, BAR-decode, fake-execution, sizing, and binding statuses, rejects default/zero derived-binding result values as not initialized, and performs no live config access, BAR restore, ECAM/BAR mapping, MMIO, DMA, driver binding, packet I/O, or block I/O.
- `OS.Drivers.VirtioPciBindingDerivedBarSizeCatalog` builds a bounded catalog of successful modern virtio block/network bindings from caller-provided candidate records, configuration-space snapshots, an injected fake config accessor, and caller-owned BAR sizing scratch buffers. It preserves source index, PCI location, fake-executor BAR-size provenance, and nested header, BAR-decode, fake-execution, sizing, and binding statuses while reporting missing configuration sources, propagated derived-binding failures, destination exhaustion, and default/zero result-success rejection. It performs no live PCI enumeration, live config access, BAR restore, ECAM/BAR mapping, MMIO, DMA, driver binding, packet I/O, or block I/O.
- `OS.Drivers.VirtioPciBarRegisterWindowPlanner` consumes successful virtio PCI binding/aperture metadata plus caller-provided mapped-BAR window records. It binds common, notify, ISR, and device-configuration apertures to mapped windows, validates mapped/MMIO/device/uncached policy metadata, validates physical base matches and aperture bounds, plans aligned 1, 2, 4, or 8 byte register-access addresses, and rejects default/zero binding/access-plan result values without mapping BARs, performing volatile access, reading or writing device registers, configuring queues, notifying hardware, processing interrupts, allocating/mapping DMA memory, or moving packets.
- `OS.Drivers.VirtioPciBarRegisterWindowAccessExecutor` consumes already-validated `VirtioPciBarRegisterWindowAccessPlan` metadata plus caller-owned BAR-window byte spans. It performs host-tested fake little-endian 1, 2, 4, and 8 byte reads/writes, returns access width/value/provenance metadata, rejects missing/default plans, short buffers, and default/zero execution result values, and performs no BAR mapping, volatile/live MMIO, physical-address access, memory barriers, queue configuration, interrupts, DMA mapping/allocation, or packet I/O.
- `OS.Drivers.VirtioPciBindingCatalog` builds a bounded catalog of successful `VirtioPciBindingPlan` results from caller-provided `VirtioPciCandidate` records, configuration-space snapshots, and BAR-size sources keyed by candidate `SourceIndex`. It preserves source index and PCI location in each output entry, reports missing configuration sources, missing BAR-size sources, nested binding failures, destination exhaustion, and default/zero result-success rejection, and does not perform live config reads, MMIO mapping, feature negotiation, queue setup, or driver attach.
- `OS.Drivers.VirtioDeviceInitializationPlan` provides a host-tested pure planning contract for generic virtio device initialization. It records the required status-write sequence (`ACKNOWLEDGE`, `DRIVER`, `FEATURES_OK`, `DRIVER_OK`), failure status writes using `FAILED`, missing required feature masks, `FEATURES_OK` rejection, required/available queue-count validation, selected feature bitset, and default/zero result-success rejection without MMIO, PCI access, queue publication, interrupts, or device I/O.
- `OS.Drivers.VirtioDeviceInitializationProfile` provides host-tested block/network initialization profiles over `VirtioDeviceInitializationPlan`. The current baseline makes the zero-required-feature choice explicit, records block as one required queue with read-only optional feature selection, records network as two required queues with MAC/status optional feature selection, rejects unknown device kinds, preserves nested initialization-plan status for insufficient queues and `FEATURES_OK` rejection, and rejects default/zero result values as successful profile evidence. It still performs no device reset, MMIO status writes, feature-register reads, queue selection, or driver attach.
- `OS.Drivers.VirtioDeviceDescriptorCatalog` composes successful `VirtioPciBindingPlan` or `VirtioPciBindingDerivedBarSizePlan` results, caller-provided device-configuration byte snapshots keyed by source index, and caller-provided feature/queue metadata into bounded block or network descriptor records. For block devices it parses `VirtioBlockDeviceConfig` and carries `BlockDeviceInfo`; for network devices it parses `VirtioNetDeviceConfig` and carries MAC/status metadata; for both it attaches `VirtioDeviceInitializationProfile` output. It reports missing device configuration, unsupported device kind, device-config parse failure, initialization-profile failure, destination exhaustion, failed binding-plan inputs, failed fake-derived binding provenance, and default/zero result-success rejection without MMIO, status writes, queue publication, or driver attach.
- `OS.Drivers.PciVirtioDescriptorCatalogPlanner` composes injected `PciEcamSegment` scanning, modern virtio candidate selection, fake-derived BAR-size binding cataloging, caller-provided feature/queue metadata, and descriptor cataloging into one bounded block/network metadata path. It reports scan, candidate-capacity, derived-binding, descriptor-source-capacity, missing-feature-source, descriptor-catalog, and default/zero result-success failures while preserving fake/injected provenance and performing no live PCI enumeration, config access, ECAM/BAR mapping, MMIO, DMA, queue setup, packet I/O, or block I/O.
- `OS.Drivers.VirtioNetQueuePlan` provides a host-tested pure planning contract for future virtio-net RX/TX queues. It requires two available queues, validates RX/TX queue sizes with `VirtqueueSplitRingPlan`, bounds caller-owned buffers to a virtio-net header plus Ethernet frame without FCS, records RX descriptors as device-writable and TX header/frame descriptors as device-readable, rejects default/zero result values as not successful, and computes required RX buffer bytes plus one TX frame buffer plan without allocating memory, publishing descriptors, notifying the device, or parsing virtio-net headers.
- `OS.Drivers.VirtioNetDriverQueueBringUpPlan` consumes a successful virtio-net descriptor/catalog entry plus queue metadata and caller-owned capacity limits. It validates the descriptor binding, network MAC/status config, network initialization profile, selected feature mask, RX/TX queue sizes, RX/TX split-ring memory bytes, and RX/TX frame-buffer capacities, rejects default/zero result-success values, then returns MAC address, queue indexes, queue sizes, required descriptor counts, memory byte counts, and publish readiness flags without writing MMIO, selecting device queues, publishing descriptors, notifying the device, handling interrupts, or sending/receiving packets.
- `OS.Drivers.VirtioNetQueueMmioSetupPlanner` converts successful queue bring-up metadata plus caller-provided RX/TX physical split-ring addresses and device queue metadata into deterministic common-configuration command plans. It records queue-select, queue-size read/write, descriptor/driver/device address writes, `queue_notify_off` reads, and queue-enable writes for RX/TX queues without invoking a register accessor, mapping BARs, allocating DMA memory, publishing descriptors, notifying the device, or moving packets. `OS.Drivers.VirtioNetQueueMmioSetupExecutor` executes that command plan only through an injected fake `IVirtioPciRegisterAccessor`, preserving setup-plan, short-command-source, missing-accessor, read/write, and mismatch failures without BAR mapping, live MMIO, descriptor publication, notification, DMA allocation/mapping, interrupts, or packet transfer.
- `OS.Drivers.VirtioNetQueueMmioSetupRegisterWindowPlanner` consumes successful setup command metadata plus a bound common-configuration register window and writes caller-owned `VirtioPciBarRegisterWindowAccessPlan` entries for each setup command. It maps command relative offsets and widths to checked register-window access addresses, rejects failed setup plans, short command sources, undersized destinations, unsupported command widths, and propagated register-window failures, and performs no BAR mapping, volatile access, fake or live register reads/writes, hardware queue configuration, interrupts, DMA mapping/allocation, or packet I/O.
- `OS.Drivers.VirtioNetQueueMmioSetupRegisterWindowExecutor` consumes successful setup command metadata plus a bound common-configuration register window and caller-owned common BAR-window bytes. It maps each command to a checked register-window access plan, executes through `VirtioPciBarRegisterWindowAccessExecutor`, stages expected queue-size and notify-offset read values for byte-span tests, rejects failed setup plans and short command sources before touching bytes, reports read mismatches and access-execution failures, and performs no BAR mapping, volatile/live MMIO, physical-address access, memory barriers, live queue configuration, interrupts, DMA mapping/allocation, or packet I/O.
- `OS.Drivers.VirtioNetQueueNotificationPlanner` consumes successful queue setup and RX/TX descriptor-publication results, delegates notify address/range validation to `VirtioPciQueueNotificationPlanner`, and returns future notify write metadata: queue index, `queue_notify_off`, relative offset, 16-bit write value, write width, published descriptor count, and available-ring index provenance. It rejects default/zero result values as not successful and preserves setup, publish, disabled-queue, unsupported notification-data, invalid-aperture, and notify-range failures without memory barriers, MMIO writes, interrupts, completion waiting, or packet I/O.
- `OS.Drivers.VirtioNetQueueNotificationPlanner.PlanRegisterWindowAccess` consumes successful queue notification metadata plus a bound notify register window and returns a checked `VirtioPciBarRegisterWindowAccessPlan` for the future 16-bit notify write. It preserves notification-plan, PCI notification, queue, notify offset, write value, write width, descriptor-count, available-ring, and register-window status metadata while rejecting failed notification plans and propagated register-window failures without BAR mapping, volatile MMIO, fake or live register writes, memory barriers, interrupts, DMA mapping/allocation, or packet I/O.
- `OS.Drivers.VirtioNetQueueNotificationRegisterWindowExecutor` consumes successful queue notification metadata plus a bound notify register window and caller-owned notify BAR-window bytes. It maps the notification to a checked register-window access plan, writes the planned 16-bit queue value through `VirtioPciBarRegisterWindowAccessExecutor`, preserves BAR-relative metadata and nested statuses, rejects failed notification plans, propagated register-window failures, and access-execution failures, and performs no BAR mapping, volatile/live MMIO, physical-address access, memory barriers, live device notification, interrupts, DMA mapping/allocation, or packet I/O.
- `OS.Drivers.VirtioNetQueueNotificationRegisterWindowBarrierExecutor` consumes successful queue notification metadata plus explicit descriptor-write, available-ring-write, and ordered-before-notify readiness metadata before fake-executing the checked notify register-window write over caller-owned notify BAR-window bytes. It preserves barrier metadata, BAR-relative metadata, and nested statuses, rejects missing barrier readiness before touching the byte span, and performs no real memory barrier, BAR mapping, volatile/live MMIO, physical-address access, live device notification, interrupts, DMA mapping/allocation, or packet I/O.
- `OS.Drivers.VirtioNetReceiveReadinessPlanner` composes successful queue setup register-window fake execution, caller-owned RX descriptor publication, explicit barrier-readiness metadata, RX notification planning, and barrier-gated notify register-window fake execution. It preserves nested setup, register-window, publish, barrier, notification-plan, and access-execution status provenance while rejecting failed setup execution before publication, failed publication before notification, notification-plan failures before fake notification writes, missing barriers, and notify window access failures. It performs no live MMIO, BAR mapping, real memory barriers, DMA allocation/mapping, packet I/O, interrupts, polling, receive completion, or live device notification.
- `OS.Drivers.VirtioNetReceiveRegisterWindowCompletionPlanner` composes successful RX register-window readiness, one caller-owned used-ring completion, and receive-frame classification over a caller-provided receive buffer. It preserves readiness, notification-window, completion, classifier, packet, descriptor-head, buffer-index, used-ring, and payload metadata while rejecting failed readiness before queue-memory reads, no-completion before classification, classifier failures, and default/zero result values without live MMIO, BAR mapping, real memory barriers, DMA allocation/mapping, packet I/O, interrupts, polling, or live device notification.
- `OS.Drivers.VirtioNetReceiveRegisterWindowSlabCompletionPlanner` selects the completed caller-owned receive buffer from a caller-owned RX slab before classification. It preserves readiness, slab-selection, completion, classifier, packet, used-ring, selected-buffer, descriptor-head, and payload metadata while rejecting failed readiness, no completion, unexpected used indexes, unexpected descriptor heads, short slabs, classifier failures, and default/zero slab completion results without live MMIO, BAR mapping, real memory barriers, DMA allocation/mapping, packet I/O, interrupts, polling, or live device notification.
- `OS.Drivers.VirtioNetReceiveRegisterWindowDispatchPlanner` feeds the completed caller-owned RX slab buffer into `NetworkReceiveDispatcher` after slab selection and receive-frame classification succeed. It preserves slab-completion, classifier, packet, ARP/IPv4 dispatch, selected-buffer, descriptor-head, and used-ring metadata while rejecting failed slab completion, dispatcher failures, and default/zero result values without live MMIO, BAR mapping, real memory barriers, DMA allocation/mapping, packet I/O, interrupts, polling, live device notification, or network-state mutation.
- `OS.Drivers.VirtioNetQueueNotificationExecutor` consumes a successful queue notification plan plus explicit descriptor-write, available-ring-write, and ordered-before-notify readiness metadata. It writes exactly one planned 16-bit queue notification through an injected fake sink, and `VirtioPciNotifyRegisterAccessorSink` can route that sink write through a fake `IVirtioPciRegisterAccessor.Write16` adapter. These paths reject default/zero result values as not successful, preserve nested planner/barrier/sink or accessor provenance, reject failed plans, missing barriers, sink/accessor not-ready/range/write failures, and perform no live MMIO, BAR mapping, queue-memory reads, descriptor-state mutation, interrupts, DMA mapping/allocation, or packet I/O.
- `OS.Drivers.VirtioNetTransmitCompletionPlanner` consumes successful TX descriptor publication plus successful fake-sink or register-window TX notification execution provenance, reads one caller-owned used-ring entry, validates one-step used-index advancement, descriptor-head match, `used.len == 0`, executed-notification kind/queue/provenance, rejects default/zero completion results before reclaim metadata is trusted, and returns header/frame descriptor indexes plus reclaim count metadata without polling, MMIO, interrupts, DMA mapping, descriptor allocator mutation, or packet I/O.
- `OS.Drivers.VirtioNetTransmitDescriptorOwnershipPlan` consumes a successful TX publication and a caller-owned descriptor ownership state span to mark the TX header and Ethernet frame descriptors `InFlight`, then consumes a successful TX completion plan to return only those descriptors to `Free`. It rejects default/zero result values as success, failed publish/completion inputs, undersized state spans, already-in-flight publication, completion for descriptors not currently in flight, and duplicate completion without MMIO, interrupts, DMA mapping, live device notification, or packet I/O.
- `OS.Drivers.VirtioNetTransmitDescriptorFreeListPlanner` consumes a successful queue plan plus a read-only caller-owned TX descriptor ownership state span. It rejects default/zero result values as success, validates queue/state-table capacity, rejects invalid descriptor state values, skips `InFlight` entries, selects the first contiguous `Free` header/frame descriptor pair, reports no capacity when no pair exists, and performs no MMIO, DMA mapping, packet I/O, queue-memory reads, interrupts, device polling, or state-table mutation.
- `OS.Drivers.VirtioNetTransmitDescriptorPublishPlanner` composes free-list selection, `VirtioNetTransmitLifecycle.Publish`, and `VirtioNetTransmitDescriptorOwnershipPlan.MarkPublished`. It rejects default/zero result values as success, selects the first reusable contiguous TX header/frame descriptor pair, writes caller-owned no-offload TX header bytes plus caller-owned split-ring descriptor and available-ring memory, marks only the selected descriptors `InFlight` after successful publication, preserves state on publish failure, and performs no MMIO, notification, polling, interrupts, DMA mapping/allocation, packet I/O, used-ring waiting, or queue-memory reads.
- `OS.Drivers.VirtioNetTransmitReadinessPlanner` composes successful fake queue setup execution, caller-owned TX descriptor publication, explicit barrier-readiness metadata, TX notification planning, and fake register-accessor-backed notification execution. It preserves nested setup, publish, barrier, notification-plan, notification-execution, and accessor status provenance while rejecting failed setup execution before publication, failed publication before notification, missing barriers before fake writes, and fake notification failures before completion planning. It performs no live MMIO, BAR mapping, DMA allocation/mapping, packet I/O, interrupts, polling, or live device notification.
- `OS.Drivers.VirtioNetTransmitRegisterWindowReadinessPlanner` composes successful queue setup register-window fake execution, caller-owned TX descriptor publication/state marking, explicit barrier-readiness metadata, TX notification planning, and barrier-gated notify register-window fake execution. It preserves nested setup, register-window, publish, ownership, barrier, notification-plan, and access-execution status provenance while rejecting failed setup execution before publication, failed publication before notification, notification-plan failures before fake notification writes, missing barriers, and notify window access failures. It performs no live MMIO, BAR mapping, real memory barriers, DMA allocation/mapping, packet I/O, interrupts, polling, completion waiting, or live device notification.
- `OS.Drivers.VirtioNetTransmitRegisterWindowReclaimPlanner` composes successful TX register-window readiness, one caller-owned TX used-ring completion validation, and descriptor-state reclaim. It preserves register-window notification provenance plus completion and ownership status metadata while rejecting failed readiness, missing completion, and ownership-state mismatches without live MMIO, BAR mapping, real memory barriers, DMA allocation/mapping, packet I/O, interrupts, polling, or live device notification.
- `OS.Drivers.VirtioNetDeviceConfigRegisterWindowReader` consumes a bound device-configuration register window, caller-owned device BAR-window bytes, and caller-owned scratch storage. It reads the MAC address and optional status word through checked register-window plans and `VirtioPciBarRegisterWindowAccessExecutor`, parses the scratch bytes through `VirtioNetDeviceConfig.TryParse`, preserves nested statuses, rejects default/zero parser and register-window read results as successful evidence, and performs no BAR mapping, volatile/live MMIO, physical-address access, feature negotiation, queue configuration, interrupts, DMA mapping/allocation, or packet I/O.
- `OS.Storage.VirtioBlockDeviceConfigRegisterWindowReader` consumes a bound device-configuration register window, caller-owned device BAR-window bytes, and caller-owned scratch storage. It reads the virtio-blk capacity field through a checked register-window plan and `VirtioPciBarRegisterWindowAccessExecutor`, parses the scratch bytes through `VirtioBlockDeviceConfig.Parse`, preserves nested statuses, and performs no BAR mapping, volatile/live MMIO, physical-address access, feature negotiation, queue configuration, notification, interrupts, DMA mapping/allocation, packet I/O, or block I/O.
- `OS.Storage.VirtioBlockQueueMmioSetupPlanner` consumes a successful virtio-blk device descriptor plus caller-provided split-ring physical addresses and queue metadata to plan queue-0 common-configuration access intent. It validates descriptor readiness, block-device kind, split-ring queue size, device maximum queue size, notify-offset availability, physical address alignment/range overflow, and command destination capacity, then emits the future command sequence without invoking a register accessor, mapping BARs, performing volatile/live MMIO, allocating DMA memory, publishing descriptors, configuring a live queue, notifying a device, handling interrupts, packet I/O, or block I/O.
- `OS.Storage.VirtioBlockQueueMmioSetupRegisterWindowPlanner` consumes successful queue setup command metadata plus a bound common-configuration register window and writes caller-owned `VirtioPciBarRegisterWindowAccessPlan` entries for each setup command. It maps command relative offsets and widths to checked register-window access addresses, rejects failed setup plans, short command sources, undersized destinations, unsupported command widths, and propagated register-window failures, and performs no BAR mapping, volatile access, fake or live register reads/writes, hardware queue configuration, interrupts, DMA mapping/allocation, packet I/O, or block I/O.
- `OS.Storage.VirtioBlockQueueMmioSetupRegisterWindowExecutor` consumes successful queue setup command metadata plus a bound common-configuration register window and caller-owned common BAR-window bytes. It maps each setup command to a checked register-window access plan, fake-executes through `VirtioPciBarRegisterWindowAccessExecutor`, stages expected queue-size and notify-offset read values for byte-span tests, rejects failed setup plans and short command sources before touching bytes, reports read mismatches and access-execution failures, and performs no BAR mapping, volatile/live MMIO, physical-address access, memory barriers, live queue configuration, interrupts, DMA mapping/allocation, packet I/O, or block I/O.
- `OS.Storage.VirtioBlockQueueMmioSetupRegisterWindowBarrierExecutor` adds setup-specific barrier readiness before the final `queue_enable` fake write. It preserves queue-select, queue-size, queue-address, ordered-before-enable, barrier-sequence, and queue-enable-attempt metadata; rejects missing readiness before touching `queue_enable`; and performs no real memory barrier, BAR mapping, volatile/live MMIO, physical-address access, live queue configuration, interrupts, DMA mapping/allocation, packet I/O, or block I/O.
- `OS.Storage.VirtioBlockReadOnlyReadSessionSetupGate` composes successful queue setup barrier execution with the read-session path. It refuses to publish read descriptors when setup execution failed, setup-write ordering was not ready, or queue-enable was not attempted, and it preserves caller-owned cursor state before the read-session adapter runs. It performs no BAR mapping, volatile/live MMIO, real memory barriers, DMA mapping/allocation, packet I/O, block I/O, or disk reads.
- `OS.Drivers.VirtioNetHeader` provides a host-tested caller-buffer parser/writer for the baseline no-offload virtio-net header. It parses the 10-byte no-mergeable-buffers header fields, rejects checksum/offload/GSO state while those features are unsupported, verifies that a received buffer includes a complete Ethernet header after the virtio header, exposes frame/payload offset and length metadata without copying bytes, rejects default/zero parse results before metadata is trusted, and writes zeroed no-offload TX headers without descriptor publication or device notification.
- `OS.Drivers.VirtioNetReceiveFrameClassifier` combines that no-offload virtio-net metadata with Ethernet dispatch over caller-owned receive buffers. It preserves classifier, packet, virtio-header, Ethernet-header, payload-kind, offset, and length metadata, rejects default/zero result values as not successful, and performs no body parsing, packet copy, queue ownership, MMIO, DMA mapping/allocation, interrupts, or live packet I/O.
- `OS.Drivers.VirtioNetReceiveLifecycle` publishes caller-owned receive buffers into caller-owned split-ring memory and maps one caller-owned used-ring completion back to a published descriptor before invoking the classifier. It rejects default/zero publish and completion result values as not successful and performs no device notification, live MMIO, DMA mapping/allocation, interrupts, polling, packet transfer, or Kestrel work.
- `OS.Drivers.HyperVPlatformIdentification` provides a host-tested CPUID snapshot parser for future Azure Hyper-V discovery. It validates the hypervisor-present bit, Microsoft Hyper-V vendor signature, maximum Hyper-V leaf, feature-identification snapshot presence, and default/zero result-success values without executing CPUID, reading MSRs, installing a hypercall page, configuring SynIC, negotiating VMBus, attaching NetVSC/StorVSC, or proving Azure support.
- `OS.Drivers.VmbusOfferPlan` provides a host-tested caller-buffer VMBus channel-offer parser/catalog contract for future Azure device discovery. It recognizes NetVSC and StorVSC interface type GUIDs, records bounded offer metadata, rejects malformed offers, unsupported classes, duplicate channel IDs, and default/zero parse/catalog result-success values without live Hyper-V setup, SynIC, VMBus negotiation, ring buffers, GPADL, NetVSC, StorVSC, Azure boot evidence, or provider readiness.
- `OS.Drivers.EnaPciCandidateSelector` provides a host-tested metadata-only ENA PCI candidate contract over caller-provided `PciEcamScannedDevice` records. It centralizes Amazon vendor and ENA PF/VF device IDs, selects only the current VF and LLQ VF baseline, validates Ethernet class/subclass/programming-interface, type-0 header, capability-list metadata, and default/zero result-success values, preserves source and PCI location fields, and reports destination exhaustion without ECAM reads, BAR sizing/mapping, MSI-X walking, DMA allocation, admin queues, or packet I/O.
- `OS.Drivers.EnaPciResourcePlanner` provides a host-tested metadata-only ENA PCI resource boundary over caller-provided device, BAR, aperture, and MSI-X snapshots. It validates the ENA candidate metadata, register BAR kind/base/aperture shape, MSI-X table/PBA BARs and ranges, MSI-X overlap, MSI-X parser defaults, and resource-plan default/zero result-success values without live PCI capability walking, BAR sizing/mapping, MSI-X configuration, DMA allocation, admin queues, or packet I/O.
- `OS.Drivers.EnaAdminDescriptors` provides host-tested ENA Admin Queue and Admin Completion Queue descriptor constants and caller-buffer helpers. It writes `GET_FEATURE DEVICE_ATTRIBUTES` commands into 64-byte AQ entries, parses 64-byte ACQ entries for command ID, phase, status, extended status, and SQ head index, reports short buffers, command ID mismatches, phase mismatches, non-success completion statuses, uninitialized/default completion entries, and 48-bit physical-address bounds without DMA allocation, MMIO, doorbells, polling, interrupts, or live command execution.
- `OS.Drivers.EnaAdminCommandPlan` provides a host-tested metadata-only ENA admin command boundary for `GET_FEATURE DEVICE_ATTRIBUTES`. It validates command IDs, supported feature IDs, timeout budget, caller-owned submission/completion buffer sizes, descriptor-write status, completion command/phase/status metadata, device-attributes parsing status, and default/zero result-success values without DMA allocation, MMIO, doorbells, polling, interrupts, or live admin command execution.
- `OS.Drivers.EnaAdminQueueLayoutPlan` provides a host-tested memory layout planner for future ENA Admin Queue and Admin Completion Queue storage. It validates power-of-two depths, entry-size granularity, base alignment, caller memory budget, arithmetic overflow, default/zero result-success values, and default/zero plan budget-fit predicates while preserving diagnostic input metadata and initial AQ producer / ACQ consumer / ACQ phase values. It does not allocate pages, map physical memory, program BAR registers, ring doorbells, poll completions, configure AENQ, or perform ENA TX/RX packet I/O.
- `OS.Drivers.EnaDmaAddressPlan` provides a host-tested metadata-only DMA address validation boundary for future ENA queue memory. It validates physical address width, range overflow, device-address-width bounds, zero lengths, power-of-two alignment, base alignment, admin queue layout rejection, contiguous memory capacity, and default/zero result-success values while projecting Admin Queue and Admin Completion Queue physical regions from caller-provided addresses. It does not allocate DMA memory, map pages, program an IOMMU, write MMIO registers, ring doorbells, configure interrupts, or perform packet I/O.
- `OS.Drivers.EnaDeviceAttributesParser` provides a host-tested parser for caller-provided ENA `GET_FEATURE DEVICE_ATTRIBUTES` responses. It validates the nested ACQ completion header, command ID, phase, completion status, physical/virtual address widths, max MTU, MAC address, and default/zero result-success values, then exposes implementation ID, device version, supported feature bitmap, capability bitmap, MAC address, address-width metadata, max MTU, extended status, and SQ head index. It does not submit admin commands, allocate DMA memory, ring doorbells, poll completions, read BARs, or synthesize a MAC address from PCI metadata.
- `OS.Drivers.EnaDataQueueDescriptors` provides a host-tested caller-buffer data queue descriptor/parser contract. It writes and reads ENA TX/RX submission descriptor bytes, parses TX/RX completion bytes, validates request ID, phase, queue ID, completion status, single-buffer RX flags, reserved bits, 48-bit physical addresses, and default/uninitialized TX completion entries without queue creation, DMA allocation/mapping, MMIO, doorbells, polling, interrupts, packet I/O, or live ENA hardware access.
- `OS.Drivers.EnaRegisterMap` and `OS.Drivers.EnaAdminReadinessPlanner` provide a host-tested metadata-only ENA register boundary. They centralize version, capability, admin queue, AENQ, interrupt, device control/status, MMIO read-response, RSS update, and PHC doorbell offsets/widths; expose capability/status bit helpers; validate the minimum admin queue and readiness register apertures; reject default/zero result-success values; and reject unsupported contiguous-queue requirements, fatal, reset-in-progress, admin-restart-in-progress, quiescent-in-progress, and not-ready metadata without BAR mapping, MMIO reads/writes, DMA allocation, admin descriptors, interrupts, or packet I/O.
- `OS.Drivers.NvmeRegisterMap` provides a host-tested metadata-only NVMe register boundary for future Nitro storage work. It centralizes controller, admin queue, and doorbell offsets/widths; validates the minimum admin queue register aperture; calculates doorbell stride and offsets from caller-provided capability metadata; and rejects default/zero aperture-validation result-success values without PCI discovery, BAR mapping, live MMIO reads/writes, DMA allocation, queues, interrupts, or block I/O.
- `OS.Drivers.NitroNvmePlanner` provides a host-tested metadata-only Nitro NVMe candidate and namespace planning boundary. It consumes caller-provided PCI scan/BAR/MSI-X snapshots and identify-controller/identify-namespace buffers, validates NVMe class metadata, register BAR shape, identify-buffer sizes, namespace ranges, serial/model text, selected LBA format, logical block size, capacity, overflow, and default/zero result-success values without PCI discovery, BAR mapping, live MMIO, admin queue commands, DMA allocation, interrupts, polling, or block I/O.

## Unsupported Behavior

Default-path ACPI table walking, post-`ExitBootServices` physical-memory access, APIC, IOAPIC, HPET/timer use, MSI/MSI-X, ECAM mapping, live PCI ECAM enumeration, live PCI configuration reads/writes, BAR sizing probe execution, BAR mapping, driver binding, serial hardware, live GOP/framebuffer binding, framebuffer text rendering, real virtio-blk I/O, live virtio-net RX/TX queue publication, live virtio-net device notification, packet transfer, virtio-net mergeable buffers, virtio-net checksum offload, virtio-net GSO, page tables, real IDT load, TSS/IST setup, and handler installation are all pending.

ENA support is limited to host-tested metadata, descriptor, register, layout, and caller-buffer device-attributes parsing contracts. It does not allocate DMA-safe queue memory, program ENA BAR registers, execute admin commands, retrieve a MAC address from hardware, create I/O queues, send or receive Ethernet frames, acquire DHCP over ENA, or prove AWS Nitro networking.

## Tests

Host tests cover the UEFI configuration-table RSDP scanner, guarded platform configuration-table contract, typed handoff metadata, and byte-level RSDP validation:

```powershell
dotnet run --project .\tests\OS.HostTests\OS.HostTests.csproj --no-build
```

Expected scanner markers:

- `TEST_PASS uefi.config acpi2 preferred`
- `TEST_PASS uefi.config acpi1 fallback`
- `TEST_PASS uefi.config acpi missing`
- `TEST_PASS uefi.config acpi zero pointer rejected`
- `TEST_PASS uefi.config invalid metadata rejected`
- `TEST_PASS platform.config acpi scan allowed before UEFI gone`
- `TEST_PASS platform.config acpi scan denied in UEFI-gone simulation`
- `TEST_PASS platform.config acpi scan denied after ExitBootServices`
- `TEST_PASS platform.config acpi2 preferred`
- `TEST_PASS platform.config acpi1 fallback`
- `TEST_PASS platform.config acpi missing`
- `TEST_PASS platform.physical_memory read allowed before UEFI gone`
- `TEST_PASS platform.physical_memory read denied in UEFI-gone simulation`
- `TEST_PASS platform.physical_memory read denied after ExitBootServices`
- `TEST_PASS platform.physical_memory read rejects invalid range`
- `TEST_PASS platform.physical_memory read reports destination capacity`
- `TEST_PASS platform.physical_memory result contains no managed references`
- `TEST_PASS platform.framebuffer snapshot allowed before UEFI gone`
- `TEST_PASS platform.framebuffer snapshot denied in UEFI-gone simulation`
- `TEST_PASS platform.framebuffer snapshot denied after ExitBootServices`
- `TEST_PASS platform.framebuffer snapshot rejects zero base or length`
- `TEST_PASS platform.framebuffer snapshot rejects zero dimensions`
- `TEST_PASS platform.framebuffer snapshot rejects invalid stride`
- `TEST_PASS platform.framebuffer snapshot rejects unsupported pixel format`
- `TEST_PASS platform.filesystem allows read before UEFI gone`
- `TEST_PASS platform.filesystem rejects read in UEFI-gone simulation`
- `TEST_PASS platform.filesystem rejects read after ExitBootServices`
- `TEST_PASS platform.filesystem rejects unsafe paths`
- `TEST_PASS platform.filesystem reports destination capacity`
- `TEST_PASS platform.filesystem result contains no managed references`
- `TEST_PASS platform.uefi gone simulation denies filesystem adapter`
- `TEST_PASS platform.framebuffer snapshot rejects size mismatch and overflow`
- `TEST_PASS platform.framebuffer snapshot reports source failures`
- `TEST_PASS platform.framebuffer result contains no managed references`
- `TEST_PASS acpi.rsdp.snapshot reads acpi1 base`
- `TEST_PASS acpi.rsdp.snapshot rereads acpi2 extended length`
- `TEST_PASS acpi.rsdp.snapshot reports scratch capacity`
- `TEST_PASS acpi.rsdp.snapshot reports physical read failure`
- `TEST_PASS acpi.rsdp.snapshot reports extended read failure`
- `TEST_PASS acpi.rsdp.snapshot reports validation failure`
- `TEST_PASS acpi.rsdp.snapshot result contains no managed references`
- `TEST_PASS acpi.root_sdt.snapshot reads rsdt from acpi1 rsdp`
- `TEST_PASS acpi.root_sdt.snapshot prefers xsdt from acpi2 rsdp`
- `TEST_PASS acpi.root_sdt.snapshot falls back to rsdt when xsdt address is zero`
- `TEST_PASS acpi.root_sdt.snapshot rejects invalid rsdp input`
- `TEST_PASS acpi.root_sdt.snapshot rejects missing root address`
- `TEST_PASS acpi.root_sdt.snapshot reports scratch and destination capacity`
- `TEST_PASS acpi.root_sdt.snapshot reports physical read failures`
- `TEST_PASS acpi.root_sdt.snapshot rejects invalid signature length and checksum`
- `TEST_PASS acpi.root_sdt.snapshot rejects invalid pointer array length`
- `TEST_PASS acpi.root_sdt.snapshot result contains no managed references`
- `TEST_PASS boothandoff imports ACPI RSDP metadata`
- `TEST_PASS efi.handoff.validator validates ACPI RSDP metadata`
- `TEST_PASS acpi.rsdp validates acpi1 checksum`
- `TEST_PASS acpi.rsdp validates acpi2 extended checksum`
- `TEST_PASS acpi.rsdp rejects extended length failures`
- `TEST_PASS acpi.sdt parses valid rsdt`
- `TEST_PASS acpi.sdt parses valid xsdt`
- `TEST_PASS acpi.sdt rejects checksum failure`
- `TEST_PASS acpi.sdt rejects declared source length mismatch`
- `TEST_PASS acpi.sdt rejects invalid pointer array length`
- `TEST_PASS acpi.sdt rejects zero table pointer`
- `TEST_PASS acpi.sdt rejects destination capacity exhaustion`
- `TEST_PASS acpi.directory selects mcfg from xsdt pointer list`
- `TEST_PASS acpi.directory selects first matching signature deterministically`
- `TEST_PASS acpi.directory rejects missing table source`
- `TEST_PASS acpi.directory rejects invalid table checksum`
- `TEST_PASS acpi.directory reports destination capacity exhaustion`
- `TEST_PASS acpi.directory reports signature not found`
- `TEST_PASS acpi.mcfg parses ecam segment`
- `TEST_PASS acpi.mcfg rejects bad signature`
- `TEST_PASS acpi.mcfg rejects truncated entry`
- `TEST_PASS acpi.mcfg rejects invalid bus range`
- `TEST_PASS acpi.mcfg.discovery selects and parses mcfg`
- `TEST_PASS acpi.mcfg.discovery preserves selected physical address`
- `TEST_PASS acpi.mcfg.discovery reports directory miss`
- `TEST_PASS acpi.mcfg.discovery reports mcfg parse failure`
- `TEST_PASS acpi.mcfg.discovery reports segment destination exhaustion`
- `TEST_PASS acpi.mcfg.discovery supports direct pointer list input`
- `TEST_PASS idt descriptor encodes interrupt gate`
- `TEST_PASS fault vectors expose error code policy`
- `TEST_PASS idt table plan binds required fault vectors`
- `TEST_PASS idt table plan writes descriptor bytes`
- `TEST_PASS diagnostics serial guard allows post firmware with owned serial`
- `TEST_PASS serial port config emits init sequence`
- `TEST_PASS com1 writer initializes through port backend`
- `TEST_PASS com1 writer writes ascii bytes when ready`
- `TEST_PASS com1 writer reports transmit timeout`
- `TEST_PASS port io plan encodes out8 operation`
- `TEST_PASS port io plan encodes in8 operation`
- `TEST_PASS port io plan rejects invalid inputs`
- `TEST_PASS port io stub plan encodes callable out8 stub`
- `TEST_PASS port io stub plan encodes callable in8 stub`
- `TEST_PASS port io stub plan rejects invalid inputs`
- `TEST_PASS descriptor instruction plan encodes lidt`
- `TEST_PASS descriptor instruction plan encodes lgdt`
- `TEST_PASS descriptor instruction plan encodes ltr`
- `TEST_PASS descriptor instruction plan rejects invalid inputs`
- `TEST_PASS descriptor install stub plan encodes lidt stub`
- `TEST_PASS descriptor install stub plan encodes lgdt stub`
- `TEST_PASS descriptor install stub plan encodes ltr stub`
- `TEST_PASS descriptor install stub plan rejects invalid inputs`
- `TEST_PASS com1 port io backend plan accepts ready prerequisites`
- `TEST_PASS com1 port io backend plan rejects invalid serial config`
- `TEST_PASS com1 port io backend plan rejects missing stub readiness`
- `TEST_PASS com1 port io backend plan rejects insufficient stub memory plan`
- `TEST_PASS descriptor install readiness accepts complete prerequisites`
- `TEST_PASS descriptor install readiness rejects missing idt readiness`
- `TEST_PASS descriptor install readiness rejects missing tss ist readiness`
- `TEST_PASS descriptor install readiness rejects insufficient stub buffers`
- `TEST_PASS storage.virtioblk read request layout`
- `TEST_PASS storage.virtioblk readiness accepts read-only queue`
- `TEST_PASS storage.virtioblk readiness rejects missing features`
- `TEST_PASS storage.virtioblk rejects write request in read-only mode`
- `TEST_PASS drivers.virtqueue split ring layout`
- `TEST_PASS drivers.virtqueue rejects invalid queue size`
- `TEST_PASS drivers.virtqueue maps descriptor avail used offsets`
- `TEST_PASS drivers.virtqueue rejects overflow`
- `TEST_PASS drivers.virtqueue default result is not success`
- `TEST_PASS storage.virtioblk descriptor chain plans three descriptors`
- `TEST_PASS storage.virtioblk descriptor chain marks data writable`
- `TEST_PASS storage.virtioblk descriptor chain rejects insufficient descriptors`
- `TEST_PASS storage.virtioblk descriptor chain rejects failed transaction plan`
- `TEST_PASS storage.virtioblk.ownership marks published descriptors in flight`
- `TEST_PASS storage.virtioblk.ownership reclaims completed descriptors`
- `TEST_PASS storage.virtioblk.ownership rejects failed publish`
- `TEST_PASS storage.virtioblk.ownership rejects failed completion`
- `TEST_PASS storage.virtioblk.ownership rejects short state table`
- `TEST_PASS storage.virtioblk.ownership rejects descriptor already in flight`
- `TEST_PASS storage.virtioblk.ownership rejects descriptor not in flight`
- `TEST_PASS storage.virtioblk.ownership rejects duplicate completion`
- `TEST_PASS storage.virtioblk.ownership preserves no-live-device boundary`
- `TEST_PASS storage.virtioblk.freelist selects first free triple`
- `TEST_PASS storage.virtioblk.freelist skips in-flight descriptors`
- `TEST_PASS storage.virtioblk.freelist reports capacity exhaustion`
- `TEST_PASS storage.virtioblk.freelist rejects invalid state`
- `TEST_PASS storage.virtioblk.freelist preserves no-live-device boundary`
- `TEST_PASS storage.virtioblk.publishplan publishes selected free chain`
- `TEST_PASS storage.virtioblk.publishplan skips in-flight descriptors`
- `TEST_PASS storage.virtioblk.publishplan rejects free-list exhaustion`
- `TEST_PASS storage.virtioblk.publishplan preserves state on publish failure`
- `TEST_PASS storage.virtioblk.publishplan rejects invalid ownership state`
- `TEST_PASS storage.virtioblk.publishplan preserves no-live-device boundary`
- `TEST_PASS storage.virtioblk.reclaimplan composes publish completion ownership`
- `TEST_PASS storage.virtioblk.reclaimplan rejects failed publish`
- `TEST_PASS storage.virtioblk.reclaimplan rejects no completion`
- `TEST_PASS storage.virtioblk.reclaimplan rejects ownership mismatch`
- `TEST_PASS storage.virtioblk.reclaimplan preserves no-live-device boundary`
- `TEST_PASS storage.virtioblk.cursor publishes and advances available index`
- `TEST_PASS storage.virtioblk.cursor preserves index on publish failure`
- `TEST_PASS storage.virtioblk.cursor reclaims and advances used index`
- `TEST_PASS storage.virtioblk.cursor preserves used index on reclaim failure`
- `TEST_PASS storage.virtioblk.cursor preserves no-live-device boundary`
- `TEST_PASS storage.virtioblk.readqueue publishes valid block range`
- `TEST_PASS storage.virtioblk.readqueue rejects out-of-range block request`
- `TEST_PASS storage.virtioblk.readqueue preserves cursor on publish failure`
- `TEST_PASS storage.virtioblk.readqueue preserves no-live-device boundary`
- `TEST_PASS storage.virtioblk.readqueue completes and advances used index`
- `TEST_PASS storage.virtioblk.readqueue completion rejects failed publication`
- `TEST_PASS storage.virtioblk.readqueue completion rejects no completion`
- `TEST_PASS storage.virtioblk.readqueue completion preserves no-live-device boundary`
- `TEST_PASS storage.virtioblk.readqueue block result reports completed read`
- `TEST_PASS storage.virtioblk.readqueue block result maps publication failure`
- `TEST_PASS storage.virtioblk.readqueue block result maps completion failure`
- `TEST_PASS storage.virtioblk.readqueue block result preserves no-live-device boundary`
- `TEST_PASS storage.virtioblk.notify plans readqueue notification metadata`
- `TEST_PASS storage.virtioblk.notify rejects failed publication`
- `TEST_PASS storage.virtioblk.notify preserves no-live-device boundary`
- `TEST_PASS storage.virtioblk.notifyoffset derives relative offset`
- `TEST_PASS storage.virtioblk.notifyoffset rejects failed notification metadata`
- `TEST_PASS storage.virtioblk.notifyoffset rejects relative offset overflow`
- `TEST_PASS storage.virtioblk.notifyoffset preserves no-live-device boundary`
- `TEST_PASS storage.virtioblk.readnotify composes publication and barrier notify`
- `TEST_PASS storage.virtioblk.readnotify rejects failed publication`
- `TEST_PASS storage.virtioblk.readnotify rejects missing barrier`
- `TEST_PASS storage.virtioblk.readnotify propagates notify execution failure`
- `TEST_PASS storage.virtioblk.readnotify preserves no-live-device boundary`
- `TEST_PASS storage.virtioblk.readnotifycompletion completes after notify readiness`
- `TEST_PASS storage.virtioblk.readnotifycompletion rejects failed readiness`
- `TEST_PASS storage.virtioblk.readnotifycompletion rejects no completion`
- `TEST_PASS storage.virtioblk.readnotifycompletion propagates ownership failure`
- `TEST_PASS storage.virtioblk.readnotifycompletion preserves no-live-device boundary`
- `TEST_PASS storage.virtioblk.readnotifyblock projects completed read`
- `TEST_PASS storage.virtioblk.readnotifyblock maps readiness failure`
- `TEST_PASS storage.virtioblk.readnotifyblock maps completion failure`
- `TEST_PASS storage.virtioblk.readnotifyblock preserves no-live-device boundary`
- `TEST_PASS storage.virtioblk.readpipeline projects completed read`
- `TEST_PASS storage.virtioblk.readpipeline rejects publication failure`
- `TEST_PASS storage.virtioblk.readpipeline rejects readiness failure`
- `TEST_PASS storage.virtioblk.readpipeline maps completion failure`
- `TEST_PASS storage.virtioblk.readpipeline preserves no-live-device boundary`
- `TEST_PASS drivers.pci.ecam calculates config address`
- `TEST_PASS drivers.pci.ecam rejects bus outside segment`
- `TEST_PASS drivers.pci.ecam rejects invalid device function register`
- `TEST_PASS drivers.pci.ecam rejects address overflow`
- `TEST_PASS drivers.pci.ecam default result is not success`
- `TEST_PASS drivers.pci.header parses identity and class`
- `TEST_PASS drivers.pci.header records bars and capability pointer`
- `TEST_PASS drivers.pci.header rejects invalid vendor`
- `TEST_PASS drivers.pci.header rejects invalid capability pointer`
- `TEST_PASS drivers.pci.header default result is not success`
- `TEST_PASS drivers.pci.scan records single device`
- `TEST_PASS drivers.pci.scan skips absent functions`
- `TEST_PASS drivers.pci.scan honors multifunction bit`
- `TEST_PASS drivers.pci.scan skips single-function extras`
- `TEST_PASS drivers.pci.scan reports capacity exhaustion`
- `TEST_PASS drivers.pci.scan reports reader failure`
- `TEST_PASS drivers.pci.scan default result is not success`
- `TEST_PASS drivers.pci.ecam scans one segment`
- `TEST_PASS drivers.pci.ecam scans multiple segments`
- `TEST_PASS drivers.pci.ecam preserves segment metadata`
- `TEST_PASS drivers.pci.ecam reports destination exhaustion`
- `TEST_PASS drivers.pci.ecam reports nested scanner failure`
- `TEST_PASS drivers.pci.ecam rejects invalid segment range`
- `TEST_PASS drivers.pci.ecam.segment default result is not success`
- `TEST_PASS drivers.pci.bar decodes 32bit mmio`
- `TEST_PASS drivers.pci.bar decodes 64bit mmio pair`
- `TEST_PASS drivers.pci.bar decodes io aperture`
- `TEST_PASS drivers.pci.bar records absent zero`
- `TEST_PASS drivers.pci.bar rejects reserved memory type`
- `TEST_PASS drivers.pci.bar rejects 64bit at index 5`
- `TEST_PASS drivers.pci.bar default result is not success`
- `TEST_PASS drivers.pci.bar.size decodes 32bit mmio aperture`
- `TEST_PASS drivers.pci.bar.size decodes 64bit mmio aperture`
- `TEST_PASS drivers.pci.bar.size records absent and consumed bars`
- `TEST_PASS drivers.pci.bar.size rejects malformed readback`
- `TEST_PASS drivers.pci.bar.size rejects base alignment mismatch`
- `TEST_PASS drivers.pci.bar.size default result is not success`
- `TEST_PASS drivers.pci.bar.size feeds virtio aperture validation`
- `TEST_PASS drivers.pci.bar.size preserves no-live-config-write boundary`
- `TEST_PASS drivers.pci.bar.sizeprobe plans 32bit mmio sequence`
- `TEST_PASS drivers.pci.bar.sizeprobe plans 64bit mmio sequence`
- `TEST_PASS drivers.pci.bar.sizeprobe skips absent io consumed bars`
- `TEST_PASS drivers.pci.bar.sizeprobe rejects malformed descriptor`
- `TEST_PASS drivers.pci.bar.sizeprobe rejects short command destination`
- `TEST_PASS drivers.pci.bar.sizeprobe default result is not success`
- `TEST_PASS drivers.pci.bar.sizeprobe preserves no-live-config-access boundary`
- `TEST_PASS drivers.pci.bar.sizeprobe.address binds 32bit command addresses`
- `TEST_PASS drivers.pci.bar.sizeprobe.address binds 64bit command addresses`
- `TEST_PASS drivers.pci.bar.sizeprobe.address propagates probe plan failure`
- `TEST_PASS drivers.pci.bar.sizeprobe.address propagates ecam address failure`
- `TEST_PASS drivers.pci.bar.sizeprobe.address rejects short destination`
- `TEST_PASS drivers.pci.bar.sizeprobe.address default result is not success`
- `TEST_PASS drivers.pci.bar.sizeprobe.address preserves no-live-config-access boundary`
- `TEST_PASS drivers.pci.bar.sizeprobe.fakeexec captures 32bit readback`
- `TEST_PASS drivers.pci.bar.sizeprobe.fakeexec captures 64bit readbacks`
- `TEST_PASS drivers.pci.bar.sizeprobe.fakeexec rejects failed address plan`
- `TEST_PASS drivers.pci.bar.sizeprobe.fakeexec rejects short command source`
- `TEST_PASS drivers.pci.bar.sizeprobe.fakeexec propagates write failure`
- `TEST_PASS drivers.pci.bar.sizeprobe.fakeexec propagates read failure`
- `TEST_PASS drivers.pci.bar.sizeprobe.fakeexec default result is not success`
- `TEST_PASS drivers.pci.bar.sizeprobe.fakeexec preserves no-live-config-access boundary`
- `TEST_PASS drivers.pci.bar.sizeprobe.derive derives 32bit aperture from fake readback`
- `TEST_PASS drivers.pci.bar.sizeprobe.derive derives 64bit aperture from fake readbacks`
- `TEST_PASS drivers.pci.bar.sizeprobe.derive propagates execution failure`
- `TEST_PASS drivers.pci.bar.sizeprobe.derive propagates sizing failure`
- `TEST_PASS drivers.pci.bar.sizeprobe.derive feeds virtio aperture validation`
- `TEST_PASS drivers.pci.bar.sizeprobe.derive default result is not success`
- `TEST_PASS drivers.pci.bar.sizeprobe.derive preserves no-live-config-access boundary`
- `TEST_PASS drivers.virtio.pci.candidate selects block and network devices`
- `TEST_PASS drivers.virtio.pci.candidate preserves pci location`
- `TEST_PASS drivers.virtio.pci.candidate skips unsupported vendor`
- `TEST_PASS drivers.virtio.pci.candidate skips unsupported virtio device id`
- `TEST_PASS drivers.virtio.pci.candidate requires capability pointer`
- `TEST_PASS drivers.virtio.pci.candidate reports destination exhaustion`
- `TEST_PASS drivers.virtio.pci.candidate default result is not success`
- `TEST_PASS drivers.hyperv identification rejects missing hypervisor bit`
- `TEST_PASS drivers.hyperv identification rejects unsupported vendor`
- `TEST_PASS drivers.hyperv identification rejects insufficient max leaf`
- `TEST_PASS drivers.hyperv identification requires feature leaf snapshot`
- `TEST_PASS drivers.hyperv identification accepts compatible snapshot`
- `TEST_PASS drivers.hyperv identification default result is not success`
- `TEST_PASS drivers.vmbus.offer parses storage offer`
- `TEST_PASS drivers.vmbus.offer parses network offer`
- `TEST_PASS drivers.vmbus.offer rejects malformed offer`
- `TEST_PASS drivers.vmbus.offer rejects unsupported class`
- `TEST_PASS drivers.vmbus.offer rejects duplicate channel ids`
- `TEST_PASS drivers.vmbus.offer rejects metadata bounds`
- `TEST_PASS drivers.vmbus.offer default results are not success`
- `TEST_PASS drivers.ena.pci.candidate recognizes ena vf devices`
- `TEST_PASS drivers.ena.pci.candidate rejects unsupported vendor device`
- `TEST_PASS drivers.ena.pci.candidate rejects invalid pci shape`
- `TEST_PASS drivers.ena.pci.candidate preserves pci source metadata`
- `TEST_PASS drivers.ena.pci.candidate reports destination exhaustion`
- `TEST_PASS drivers.ena.pci.candidate rejects default results`
- `TEST_PASS drivers.ena.pci.resources accepts mmio bar`
- `TEST_PASS drivers.ena.pci.resources parses msix`
- `TEST_PASS drivers.ena.pci.resources rejects malformed aperture`
- `TEST_PASS drivers.ena.pci.resources rejects malformed msix`
- `TEST_PASS drivers.ena.pci.resources rejects default results`
- `TEST_PASS drivers.ena.admin.desc exposes descriptor size constants`
- `TEST_PASS drivers.ena.admin.desc writes get feature device attributes`
- `TEST_PASS drivers.ena.admin.desc parses successful completion`
- `TEST_PASS drivers.ena.admin.desc reports command id mismatch`
- `TEST_PASS drivers.ena.admin.desc reports completion status`
- `TEST_PASS drivers.ena.admin.desc rejects short buffers`
- `TEST_PASS drivers.ena.admin.desc preserves phase status bits`
- `TEST_PASS drivers.ena.admin.desc rejects default completion entry`
- `TEST_PASS drivers.ena.admin.command plans get feature`
- `TEST_PASS drivers.ena.admin.command validates completion`
- `TEST_PASS drivers.ena.admin.command rejects timeout budget`
- `TEST_PASS drivers.ena.admin.command rejects invalid inputs`
- `TEST_PASS drivers.ena.admin.command rejects completion failures`
- `TEST_PASS drivers.ena.admin.command rejects default results`
- `TEST_PASS drivers.ena.admin.queue plans aq acq layout`
- `TEST_PASS drivers.ena.admin.queue rejects invalid depth`
- `TEST_PASS drivers.ena.admin.queue rejects invalid entry size`
- `TEST_PASS drivers.ena.admin.queue rejects insufficient memory`
- `TEST_PASS drivers.ena.admin.queue reports arithmetic overflow`
- `TEST_PASS drivers.ena.admin.queue rejects default result`
- `TEST_PASS drivers.ena.admin.queue default plan does not fit budget`
- `TEST_PASS drivers.ena.dma validates address width`
- `TEST_PASS drivers.ena.dma rejects overflow`
- `TEST_PASS drivers.ena.dma validates queue regions`
- `TEST_PASS drivers.ena.dma rejects invalid regions`
- `TEST_PASS drivers.ena.dma rejects default results`
- `TEST_PASS drivers.ena.deviceattr parses mac and mtu`
- `TEST_PASS drivers.ena.deviceattr preserves address widths`
- `TEST_PASS drivers.ena.deviceattr rejects zero mac`
- `TEST_PASS drivers.ena.deviceattr rejects broadcast mac`
- `TEST_PASS drivers.ena.deviceattr reports completion failure`
- `TEST_PASS drivers.ena.deviceattr rejects malformed responses`
- `TEST_PASS drivers.ena.deviceattr rejects default parse result`
- `TEST_PASS drivers.ena.data.desc writes tx descriptor`
- `TEST_PASS drivers.ena.data.desc writes rx descriptor`
- `TEST_PASS drivers.ena.data.desc parses completions`
- `TEST_PASS drivers.ena.data.desc rejects malformed buffers`
- `TEST_PASS drivers.ena.data.desc rejects completion failures`
- `TEST_PASS drivers.ena.data.desc rejects default tx completion`
- `TEST_PASS drivers.ena.registermap register offsets`
- `TEST_PASS drivers.ena.registermap capability and status bits`
- `TEST_PASS drivers.ena.registermap validates admin aperture`
- `TEST_PASS drivers.ena.registermap rejects default results`
- `TEST_PASS drivers.ena.registermap readiness accepts ready metadata`
- `TEST_PASS drivers.ena.registermap readiness rejects unsupported reset busy states`
- `TEST_PASS drivers.nvme.registermap controller offsets`
- `TEST_PASS drivers.nvme.registermap admin queue offsets`
- `TEST_PASS drivers.nvme.registermap status bits`
- `TEST_PASS drivers.nvme.registermap validates admin queue aperture length`
- `TEST_PASS drivers.nvme.registermap rejects default aperture result`
- `TEST_PASS drivers.nvme.registermap calculates doorbell stride`
- `TEST_PASS drivers.nvme.nitro identifies controller`
- `TEST_PASS drivers.nvme.nitro parses namespace identity`
- `TEST_PASS drivers.nvme.nitro rejects malformed identity`
- `TEST_PASS drivers.nvme.nitro rejects non nvme device`
- `TEST_PASS drivers.nvme.nitro rejects missing mmio bar`
- `TEST_PASS drivers.nvme.nitro rejects default plan result`
- `TEST_PASS drivers.virtiopci.capability parser records required capabilities`
- `TEST_PASS drivers.virtiopci.capability parser rejects truncated capability`
- `TEST_PASS drivers.virtiopci.capability parser rejects capability loop`
- `TEST_PASS drivers.virtiopci.capability parser feeds discovery contract`
- `TEST_PASS drivers.virtiopci.capability parser default result is not success`
- `TEST_PASS drivers.virtiopci identifies virtio block device`
- `TEST_PASS drivers.virtiopci identifies virtio net device`
- `TEST_PASS drivers.virtiopci rejects unsupported device`
- `TEST_PASS drivers.virtiopci records required capabilities`
- `TEST_PASS drivers.virtiopci default result is not success`
- `TEST_PASS drivers.virtio.pci.bar validates required apertures`
- `TEST_PASS drivers.virtio.pci.bar accepts 64bit memory bar`
- `TEST_PASS drivers.virtio.pci.bar rejects io bar capability`
- `TEST_PASS drivers.virtio.pci.bar rejects absent capability bar`
- `TEST_PASS drivers.virtio.pci.bar rejects region beyond bar size`
- `TEST_PASS drivers.virtio.pci.bar rejects failed discovery input`
- `TEST_PASS drivers.virtio.pci.bar default result is not success`
- `TEST_PASS drivers.virtio.pci.binding creates block binding plan`
- `TEST_PASS drivers.virtio.pci.binding creates network binding plan`
- `TEST_PASS drivers.virtio.pci.binding reports capability parse failure`
- `TEST_PASS drivers.virtio.pci.binding reports discovery failure`
- `TEST_PASS drivers.virtio.pci.binding reports bar decode failure`
- `TEST_PASS drivers.virtio.pci.binding reports aperture validation failure`
- `TEST_PASS drivers.virtio.pci.binding default result is not success`
- `TEST_PASS drivers.virtio.pci.binding.derive creates network binding from fake bar probe`
- `TEST_PASS drivers.virtio.pci.binding.derive creates block binding from fake bar probe`
- `TEST_PASS drivers.virtio.pci.binding.derive reports header parse failure`
- `TEST_PASS drivers.virtio.pci.binding.derive reports bar decode failure`
- `TEST_PASS drivers.virtio.pci.binding.derive propagates derived size failure`
- `TEST_PASS drivers.virtio.pci.binding.derive reports short size destination`
- `TEST_PASS drivers.virtio.pci.binding.derive propagates binding failure`
- `TEST_PASS drivers.virtio.pci.binding.derive default result is not success`
- `TEST_PASS drivers.virtio.pci.binding.derive preserves no-live-config-access boundary`
- `TEST_PASS drivers.virtio.pci.binding.derive.catalog binds multiple candidates`
- `TEST_PASS drivers.virtio.pci.binding.derive.catalog preserves source indexes`
- `TEST_PASS drivers.virtio.pci.binding.derive.catalog reports missing config source`
- `TEST_PASS drivers.virtio.pci.binding.derive.catalog propagates header parse failure`
- `TEST_PASS drivers.virtio.pci.binding.derive.catalog propagates bar decode failure`
- `TEST_PASS drivers.virtio.pci.binding.derive.catalog propagates derived size failure`
- `TEST_PASS drivers.virtio.pci.binding.derive.catalog reports short size scratch`
- `TEST_PASS drivers.virtio.pci.binding.derive.catalog propagates binding failure`
- `TEST_PASS drivers.virtio.pci.binding.derive.catalog reports destination exhaustion`
- `TEST_PASS drivers.virtio.pci.binding.derive.catalog default result is not success`
- `TEST_PASS drivers.virtio.pci.binding.derive.catalog preserves no-live-config-access boundary`
- `TEST_PASS drivers.virtiopci.registerwindow binds common notify isr device apertures`
- `TEST_PASS drivers.virtiopci.registerwindow plans aligned register access`
- `TEST_PASS drivers.virtiopci.registerwindow rejects missing mapping`
- `TEST_PASS drivers.virtiopci.registerwindow rejects base mismatch`
- `TEST_PASS drivers.virtiopci.registerwindow rejects range overflow`
- `TEST_PASS drivers.virtiopci.registerwindow rejects unaligned access`
- `TEST_PASS drivers.virtiopci.registerwindow preserves no-live-mmio boundary`
- `TEST_PASS drivers.virtiopci.registerwindow default results are not success`
- `TEST_PASS drivers.virtiopci.registerwindow.fakeexec writes planned word access`
- `TEST_PASS drivers.virtiopci.registerwindow.fakeexec reads planned dword access`
- `TEST_PASS drivers.virtiopci.registerwindow.fakeexec rejects missing access plan`
- `TEST_PASS drivers.virtiopci.registerwindow.fakeexec rejects short window buffer`
- `TEST_PASS drivers.virtiopci.registerwindow.fakeexec preserves no-live-mmio boundary`
- `TEST_PASS drivers.virtio.pci.catalog binds multiple candidates`
- `TEST_PASS drivers.virtio.pci.catalog preserves source indexes`
- `TEST_PASS drivers.virtio.pci.catalog reports missing config source`
- `TEST_PASS drivers.virtio.pci.catalog reports missing bar-size source`
- `TEST_PASS drivers.virtio.pci.catalog reports binding failure`
- `TEST_PASS drivers.virtio.pci.catalog reports destination exhaustion`
- `TEST_PASS drivers.virtio.pci.catalog default result is not success`
- `TEST_PASS drivers.virtio init plans status sequence`
- `TEST_PASS drivers.virtio init rejects missing features`
- `TEST_PASS drivers.virtio init rejects features-ok failure`
- `TEST_PASS drivers.virtio init records selected features`
- `TEST_PASS drivers.virtio init default result is not success`
- `TEST_PASS drivers.virtio.profile creates block initialization plan`
- `TEST_PASS drivers.virtio.profile creates network initialization plan`
- `TEST_PASS drivers.virtio.profile rejects unknown device kind`
- `TEST_PASS drivers.virtio.profile reports insufficient queues`
- `TEST_PASS drivers.virtio.profile reports features-ok rejection`
- `TEST_PASS drivers.virtio.profile default result is not success`
- `TEST_PASS drivers.virtio.descriptor.catalog.derive creates block descriptor`
- `TEST_PASS drivers.virtio.descriptor.catalog.derive creates network descriptor`
- `TEST_PASS drivers.virtio.descriptor.catalog.derive reports derived binding failure`
- `TEST_PASS drivers.virtio.descriptor.catalog.derive reports missing device config`
- `TEST_PASS drivers.virtio.descriptor.catalog.derive reports config parse failure`
- `TEST_PASS drivers.virtio.descriptor.catalog.derive reports initialization failure`
- `TEST_PASS drivers.virtio.descriptor.catalog.derive reports destination exhaustion`
- `TEST_PASS drivers.virtio.descriptor.catalog.derive default result is not success`
- `TEST_PASS drivers.virtio.descriptor.catalog.derive preserves no-live-device boundary`
- `TEST_PASS drivers.virtionet.header writes zero tx header`
- `TEST_PASS drivers.virtionet.header parses rx frame metadata`
- `TEST_PASS drivers.virtionet.header rejects truncated header`
- `TEST_PASS drivers.virtionet.header rejects missing ethernet header`
- `TEST_PASS drivers.virtionet.header rejects offload and gso`
- `TEST_PASS drivers.virtionet.header default results are not success`
- `TEST_PASS drivers.virtionet.rx classifier default result is not success`
- `TEST_PASS drivers.virtionet queue plan accepts rx tx queues`
- `TEST_PASS drivers.virtionet queue plan marks rx writable and tx readable`
- `TEST_PASS drivers.virtionet queue plan rejects insufficient queues`
- `TEST_PASS drivers.virtionet queue plan rejects invalid frame size`
- `TEST_PASS drivers.virtionet queue plan default result is not success`
- `TEST_PASS drivers.virtionet.rx.lifecycle publish default result is not success`
- `TEST_PASS drivers.virtionet.rx.lifecycle completion default result is not success`
- `TEST_PASS drivers.virtionet.queue.bringup creates publish ready plan`
- `TEST_PASS drivers.virtionet.queue.bringup rejects non network descriptor`
- `TEST_PASS drivers.virtionet.queue.bringup rejects invalid queue plan`
- `TEST_PASS drivers.virtionet.queue.bringup rejects split ring memory`
- `TEST_PASS drivers.virtionet.queue.bringup rejects buffer capacities`
- `TEST_PASS drivers.virtionet.queue.bringup rejects byte count overflow`
- `TEST_PASS drivers.virtionet.queue.bringup default result is not success`
- `TEST_PASS storage.virtioblk.queuesetup plans queue zero common config commands`
- `TEST_PASS storage.virtioblk.queuesetup rejects failed block descriptor`
- `TEST_PASS storage.virtioblk.queuesetup rejects queue metadata`
- `TEST_PASS storage.virtioblk.queuesetup rejects physical addresses`
- `TEST_PASS storage.virtioblk.queuesetup reports command destination capacity`
- `TEST_PASS storage.virtioblk.queuesetup default result is not success`
- `TEST_PASS drivers.virtionet.queue.mmio default results are not success`
- `TEST_PASS drivers.virtionet.queue.mmio.window plans setup command accesses`
- `TEST_PASS drivers.virtionet.queue.mmio.window rejects failed setup plan`
- `TEST_PASS drivers.virtionet.queue.mmio.window rejects short command source`
- `TEST_PASS drivers.virtionet.queue.mmio.window rejects short destination`
- `TEST_PASS drivers.virtionet.queue.mmio.window rejects unsupported command width`
- `TEST_PASS drivers.virtionet.queue.mmio.window propagates register window failure`
- `TEST_PASS drivers.virtionet.queue.mmio.window preserves no-live-mmio boundary`
- `TEST_PASS drivers.virtionet.queue.mmio.windowexec writes planned setup accesses`
- `TEST_PASS drivers.virtionet.queue.mmio.windowexec rejects failed setup plan`
- `TEST_PASS drivers.virtionet.queue.mmio.windowexec rejects short command source`
- `TEST_PASS drivers.virtionet.queue.mmio.windowexec rejects read mismatch`
- `TEST_PASS drivers.virtionet.queue.mmio.windowexec propagates access failure`
- `TEST_PASS drivers.virtionet.queue.mmio.windowexec preserves no-live-mmio boundary`
- `TEST_PASS drivers.virtionet.queue.notify.windowexec writes planned tx notification`
- `TEST_PASS drivers.virtionet.queue.notify.windowexec writes planned rx notification`
- `TEST_PASS drivers.virtionet.queue.notify.windowexec rejects failed notification plan`
- `TEST_PASS drivers.virtionet.queue.notify.windowexec propagates register window failure`
- `TEST_PASS drivers.virtionet.queue.notify.windowexec propagates access failure`
- `TEST_PASS drivers.virtionet.queue.notify.windowexec preserves no-live-mmio boundary`
- `TEST_PASS drivers.virtionet.queue.notify.windowbarrier writes planned tx notification`
- `TEST_PASS drivers.virtionet.queue.notify.windowbarrier rejects missing barrier`
- `TEST_PASS drivers.virtionet.queue.notify.windowbarrier rejects failed notification plan`
- `TEST_PASS drivers.virtionet.queue.notify.windowbarrier propagates register window failure`
- `TEST_PASS drivers.virtionet.queue.notify.windowbarrier propagates access failure`
- `TEST_PASS drivers.virtionet.queue.notify.windowbarrier preserves no-live-mmio boundary`
- `TEST_PASS drivers.virtionet.queue.notify rejects default results`
- `TEST_PASS drivers.virtionet.rx.readiness composes setup publish notify windowexec`
- `TEST_PASS drivers.virtionet.rx.readiness rejects failed setup window execution`
- `TEST_PASS drivers.virtionet.rx.readiness rejects failed publish`
- `TEST_PASS drivers.virtionet.rx.readiness rejects missing barrier`
- `TEST_PASS drivers.virtionet.rx.readiness propagates notification plan failure`
- `TEST_PASS drivers.virtionet.rx.readiness propagates notification window failure`
- `TEST_PASS drivers.virtionet.rx.readiness default result is not success`
- `TEST_PASS drivers.virtionet.rx.readiness preserves no-live-mmio boundary`
- `TEST_PASS drivers.virtionet.rx.completion.windowexec composes readiness completion classification`
- `TEST_PASS drivers.virtionet.rx.completion.windowexec rejects failed readiness`
- `TEST_PASS drivers.virtionet.rx.completion.windowexec rejects no completion`
- `TEST_PASS drivers.virtionet.rx.completion.windowexec propagates classifier failure`
- `TEST_PASS drivers.virtionet.rx.completion.windowexec default result is not success`
- `TEST_PASS drivers.virtionet.rx.completion.windowexec preserves no-live-mmio boundary`
- `TEST_PASS drivers.virtionet.rx.completion.slab.windowexec selects completed buffer`
- `TEST_PASS drivers.virtionet.rx.completion.slab.windowexec rejects failed readiness`
- `TEST_PASS drivers.virtionet.rx.completion.slab.windowexec rejects no completion`
- `TEST_PASS drivers.virtionet.rx.completion.slab.windowexec rejects short slab`
- `TEST_PASS drivers.virtionet.rx.completion.slab.windowexec propagates classifier failure`
- `TEST_PASS drivers.virtionet.rx.completion.slab.windowexec preserves no-live-mmio boundary`
- `TEST_PASS drivers.virtionet.rx.completion.slab.windowexec default result is not success`
- `TEST_PASS drivers.virtionet.rx.dispatch.windowexec dispatches arp slab completion`
- `TEST_PASS drivers.virtionet.rx.dispatch.windowexec dispatches ipv4 slab completion`
- `TEST_PASS drivers.virtionet.rx.dispatch.windowexec rejects failed slab completion`
- `TEST_PASS drivers.virtionet.rx.dispatch.windowexec propagates dispatcher failure`
- `TEST_PASS drivers.virtionet.rx.dispatch.windowexec default result is not success`
- `TEST_PASS drivers.virtionet.rx.dispatch.windowexec preserves no-live-mmio boundary`
- `TEST_PASS drivers.virtionet.config.windowexec reads mac address`
- `TEST_PASS drivers.virtionet.config.windowexec reads optional status`
- `TEST_PASS drivers.virtionet.config.windowexec rejects short scratch buffer`
- `TEST_PASS drivers.virtionet.config.windowexec propagates register window failure`
- `TEST_PASS drivers.virtionet.config.windowexec propagates access failure`
- `TEST_PASS drivers.virtionet.config.windowexec propagates config parse failure`
- `TEST_PASS drivers.virtionet.config default results are not success`
- `TEST_PASS drivers.virtionet.config.windowexec preserves no-live-mmio boundary`
- `TEST_PASS storage.virtioblk.config.windowexec reads capacity`
- `TEST_PASS storage.virtioblk.config.windowexec rejects short scratch buffer`
- `TEST_PASS storage.virtioblk.config.windowexec propagates register window failure`
- `TEST_PASS storage.virtioblk.config.windowexec propagates access failure`
- `TEST_PASS storage.virtioblk.config.windowexec propagates config parse failure`
- `TEST_PASS storage.virtioblk.config.windowexec preserves no-live-mmio boundary`
- `TEST_PASS storage.virtioblk.queuesetup.windowbarrier writes queue enable after barrier`
- `TEST_PASS storage.virtioblk.queuesetup.windowbarrier rejects missing barrier`
- `TEST_PASS storage.virtioblk.queuesetup.windowbarrier rejects failed setup plan`
- `TEST_PASS storage.virtioblk.queuesetup.windowbarrier rejects short command source`
- `TEST_PASS storage.virtioblk.queuesetup.windowbarrier propagates access failure`
- `TEST_PASS storage.virtioblk.queuesetup.windowbarrier preserves no-live-mmio boundary`
- `TEST_PASS storage.virtioblk.queuesetup.windowbarrier default result is not success`
- `TEST_PASS storage.virtioblk.readsession setup gate composes setup and read`
- `TEST_PASS storage.virtioblk.readsession setup gate rejects failed setup`
- `TEST_PASS storage.virtioblk.readsession setup gate rejects missing barrier`
- `TEST_PASS storage.virtioblk.readsession setup gate propagates read failure`
- `TEST_PASS storage.virtioblk.readsession setup gate default result is not success`
- `TEST_PASS drivers.virtionet.tx.lifecycle writes header descriptors`
- `TEST_PASS drivers.virtionet.tx.lifecycle writes available ring head`
- `TEST_PASS drivers.virtionet.tx.lifecycle preserves unrelated memory`
- `TEST_PASS drivers.virtionet.tx.lifecycle rejects failed queue plan`
- `TEST_PASS drivers.virtionet.tx.lifecycle rejects header buffer`
- `TEST_PASS drivers.virtionet.tx.lifecycle rejects frame bounds`
- `TEST_PASS drivers.virtionet.tx.lifecycle rejects descriptor range`
- `TEST_PASS drivers.virtionet.tx.lifecycle rejects queue memory and overflow`
- `TEST_PASS drivers.virtionet.tx.lifecycle default result is not success`
- `TEST_PASS drivers.virtionet.tx.ownership marks published descriptors in flight`
- `TEST_PASS drivers.virtionet.tx.ownership reclaims completed descriptors`
- `TEST_PASS drivers.virtionet.tx.ownership rejects failed publish`
- `TEST_PASS drivers.virtionet.tx.ownership rejects failed completion`
- `TEST_PASS drivers.virtionet.tx.ownership rejects short state table`
- `TEST_PASS drivers.virtionet.tx.ownership rejects descriptor already in flight`
- `TEST_PASS drivers.virtionet.tx.ownership rejects descriptor not in flight`
- `TEST_PASS drivers.virtionet.tx.ownership rejects duplicate completion`
- `TEST_PASS drivers.virtionet.tx.ownership preserves no-mmio boundary`
- `TEST_PASS drivers.virtionet.tx.freelist selects first free pair`
- `TEST_PASS drivers.virtionet.tx.freelist skips in-flight descriptors`
- `TEST_PASS drivers.virtionet.tx.freelist reuses reclaimed descriptors`
- `TEST_PASS drivers.virtionet.tx.freelist reports capacity exhaustion`
- `TEST_PASS drivers.virtionet.tx.freelist rejects invalid queue state`
- `TEST_PASS drivers.virtionet.tx.freelist preserves no-mmio boundary`
- `TEST_PASS drivers.virtionet.tx.publishplan publishes selected free pair`
- `TEST_PASS drivers.virtionet.tx.publishplan skips in-flight descriptors`
- `TEST_PASS drivers.virtionet.tx.publishplan rejects free-list exhaustion`
- `TEST_PASS drivers.virtionet.tx.publishplan preserves state on publish failure`
- `TEST_PASS drivers.virtionet.tx.publishplan rejects invalid ownership state`
- `TEST_PASS drivers.virtionet.tx.publishplan preserves no-mmio boundary`
- `TEST_PASS drivers.virtionet.tx.descriptor default results are not success`
- `TEST_PASS drivers.virtionet.tx.readiness composes setup publish notify`
- `TEST_PASS drivers.virtionet.tx.readiness default result is not success`
- `TEST_PASS drivers.virtionet.tx.readiness rejects failed setup execution`
- `TEST_PASS drivers.virtionet.tx.readiness rejects failed publish`
- `TEST_PASS drivers.virtionet.tx.readiness rejects missing barrier`
- `TEST_PASS drivers.virtionet.tx.readiness propagates notification failure`
- `TEST_PASS drivers.virtionet.tx.readiness preserves no-live-mmio boundary`
- `TEST_PASS drivers.virtionet.tx.readiness.windowexec composes setup publish notify`
- `TEST_PASS drivers.virtionet.tx.readiness.windowexec default result is not success`
- `TEST_PASS drivers.virtionet.tx.readiness.windowexec rejects failed setup window execution`
- `TEST_PASS drivers.virtionet.tx.readiness.windowexec rejects failed publish`
- `TEST_PASS drivers.virtionet.tx.readiness.windowexec rejects missing barrier`
- `TEST_PASS drivers.virtionet.tx.readiness.windowexec propagates notification plan failure`
- `TEST_PASS drivers.virtionet.tx.readiness.windowexec propagates notification window failure`
- `TEST_PASS drivers.virtionet.tx.readiness.windowexec preserves no-live-mmio boundary`
- `TEST_PASS drivers.virtionet.tx.reclaim.windowexec composes readiness completion ownership`
- `TEST_PASS drivers.virtionet.tx.reclaim.windowexec default result is not success`
- `TEST_PASS drivers.virtionet.tx.reclaim.windowexec rejects failed readiness`
- `TEST_PASS drivers.virtionet.tx.reclaim.windowexec rejects no completion`
- `TEST_PASS drivers.virtionet.tx.reclaim.windowexec rejects ownership mismatch`
- `TEST_PASS drivers.virtionet.tx.reclaim.windowexec preserves no-live-mmio boundary`

QEMU smoke currently observes:

- `UEFI_ACPI_RSDP_READY`
- `BOOT_ACPI_RSDP_HANDOFF_READY`
- `ACPI_RSDP_SNAPSHOT_READY` in the opt-in ACPI physical discovery probe artifact
- `ACPI_RSDP_VALIDATED` in the opt-in ACPI physical discovery probe artifact
- `ACPI_ROOT_SDT_SNAPSHOT_READY` in the opt-in ACPI physical discovery probe artifact
- `ACPI_ROOT_SDT_VALIDATED` in the opt-in ACPI physical discovery probe artifact
- `ACPI_MCFG_DISCOVERY_READY` in the opt-in ACPI physical discovery probe artifact

## Known Limitations

Driver work must wait for diagnostics/fault handling so failures do not become silent resets.

The default scanner path is not a driver. It only returns the firmware-provided RSDP address candidate and proves live marker visibility. The handoff record preserves that pointer as provenance, while ACPI byte validation is limited to the explicit ACPI physical discovery probe artifact.

The byte-level RSDP validator is host-tested and mirrored by the opt-in EFI probe. The probe proves pre-`ExitBootServices` table bytes on the tested QEMU/OVMF host; it is not a post-firmware physical-memory policy or driver boundary.

The root SDT snapshot reader is host-tested with a synthetic successful RSDP snapshot and a fake physical-memory source, and the opt-in EFI probe validates a live root RSDT/XSDT before `ExitBootServices`. It does not select MADT/HPET for use, map ECAM, enumerate PCI, or prove the default path walks ACPI tables.

The MCFG parser and discovery composers are host-tested, and the opt-in EFI probe validates a live `MCFG` before `ExitBootServices` and copies ECAM segment metadata into scratch storage. It still does not map ECAM MMIO, enumerate PCI devices, size BARs, or bind drivers.

The IDT descriptor, table-plan, descriptor instruction-byte, descriptor install-stub, and descriptor install-readiness contracts are not driver evidence. They do not execute `lidt`, `lgdt`, or `ltr`, install handlers, configure TSS/IST or APICs, or trigger CPU faults.

The serial diagnostics guard, serial-port configuration plan, abstract COM1 writer, port-I/O instruction-byte plan, callable port-I/O stub plan, backend readiness plan, and owned COM1 diagnostics-readiness composer are not serial-driver evidence. They only prevent future boot code from treating firmware console output as post-firmware-safe diagnostics and define COM1 initialization/write behavior for a future real port-I/O backend.

The framebuffer platform boundary is metadata snapshot evidence only. It validates caller-injected base address, byte length, dimensions, stride, RGBX/BGRX pixel format, source status, and guard state; it does not call UEFI GOP, map framebuffer memory, draw pixels, render panic text, or prove output after `ExitBootServices`.

The PCI ECAM configuration-space contract is address planning only. It validates segment and tuple metadata and calculates a physical address; it does not scan ECAM, perform MMIO reads or writes, map BARs, configure MSI/MSI-X, or bind devices.

The physical PCI ECAM header reader is host-tested through a fake `IPlatformPhysicalMemory` source only. It calculates ECAM addresses and copies the first 64 bytes into caller-owned buffers, but it does not map ECAM MMIO, perform CPU MMIO loads, size or map BARs, configure interrupts, bind drivers, or prove hardware enumeration under QEMU/cloud providers.

`PhysicalMmioOwnershipPlan` is ECAM MMIO ownership planning metadata only. It checks parsed ECAM windows against caller-provided allocator-usable memory ranges and writes caller-owned range records, but it does not reserve those ranges in the live allocator, create page mappings, set cacheability, read or write ECAM, enumerate PCI, or prove post-firmware MMIO access.

The PCI configuration-header parser is byte-buffer parsing evidence only. It requires the caller to provide at least the first 64 bytes of a PCI function's configuration space, supports type-0 headers only, validates the capabilities-list pointer when advertised by the status register, and records raw BAR values. It does not scan ECAM, read MMIO, size or map BARs, walk capability structures, configure MSI/MSI-X, or bind a driver.

The PCI bus scanner is host-tested injected-reader evidence only. It can coordinate bounded bus/device/function scanning over caller-provided first-64-byte config-space snapshots, but the reader is still a test double or future platform boundary. It does not discover MCFG tables, map ECAM, read live MMIO, size or map BARs, walk capability lists, configure interrupts, bind drivers, or prove hardware enumeration under QEMU.

The PCI ECAM segment scanner is host-tested coordination evidence only. It bridges parsed MCFG segment records to the injected bus scanner and annotates output devices with segment/base metadata, but the reader is still a test double or future platform boundary. It does not dereference the ECAM base, map physical memory, issue PCI configuration reads, walk capability lists, bind drivers, or prove hardware enumeration under QEMU.

The PCI BAR decoder, sizing planner, sizing probe planner, sizing probe address planner, fake probe executor, and derived-size composer are metadata/test-double evidence only. The decoder consumes raw BAR values from an already parsed type-0 header and returns read-only descriptors for absent, I/O, 32-bit MMIO, 64-bit MMIO, consumed, and malformed slots. The sizing planner consumes those descriptors plus caller-owned all-ones sizing readback snapshots and derives aperture-size metadata for later validators. Default/zero decode, sizing plan, sizing-probe plan, sizing-probe address-plan, sizing-probe execution, config-read, and derived-size values are explicitly not successful until initialized by the relevant planner or accessor. The probe planner records the command sequence a future PCI config-space accessor must execute to write all ones, capture sizing readbacks, and restore original BAR values, including 64-bit low/high BAR pairs. The address planner binds each planned command to checked ECAM config-space address metadata for a caller-provided segment and bus/device/function. The fake executor exercises that command sequence against an injected test accessor and caller-owned readback buffer. The derived-size composer feeds those fake-captured readbacks into `PciBarSizingPlanner` and preserves nested probe/execution/sizing statuses. Its captured readbacks and derived sizes are host-test fixtures, not evidence that hardware accepted an all-ones BAR probe or that BARs were restored on a live device. It does not execute live config writes or reads, restore hardware BARs, validate a live device, map ECAM, map BARs, allocate pages, configure MSI/MSI-X, or bind devices.

The ENA PCI candidate selector and resource planner are metadata evidence only. They consume caller-provided ECAM scanner output, BAR descriptors/apertures, and MSI-X snapshots, validate Amazon ENA VF/LLQ VF identity plus Ethernet PCI class/resource metadata, and preserve PCI location/source fields. They do not read live ECAM, walk live capability lists, size or map BARs, configure MSI-X, allocate DMA memory, create admin queues, retrieve a MAC address, or send/receive Ethernet frames.

The ENA device-attributes parser is caller-buffer evidence only. It can parse bytes that represent a successful `GET_FEATURE DEVICE_ATTRIBUTES` completion, but no code currently submits that admin command to hardware, waits for the completion, or proves the parsed MAC came from a live ENA device.

The ENA register map and admin readiness planner are metadata evidence only. They consume caller-provided BAR aperture length, capability bits, and device-status bits; they do not discover an ENA PCI function, size or map BARs, read or write MMIO registers, allocate DMA-safe queues, publish admin queue bases, ring admin doorbells, parse admin descriptors, configure interrupts, retrieve a MAC address, or move packets.

The virtio PCI capability-list parser is not PCI-enumeration or hardware-driver evidence. It operates only on caller-provided config-space bytes and a start offset. It does not read ECAM, write PCI config registers, map BARs, validate BAR aperture sizes, negotiate features, allocate DMA-safe buffers, publish virtqueues, process interrupts, or perform block/network I/O.

The virtio PCI discovery contract is not PCI-enumeration or hardware-driver evidence. It operates only on caller-provided PCI identity and decoded virtio capability metadata. It does not scan ECAM, map BARs, negotiate features, allocate DMA-safe buffers, publish virtqueues, notify devices, process interrupts, or perform block/network I/O.

The virtio PCI BAR aperture validator is metadata validation only. It requires a prior successful discovery result, decoded BAR descriptors, and caller-provided or `PciBarSizingPlanner`-derived BAR sizes; it does not obtain live BAR sizes itself, perform all-ones sizing writes, restore BARs, map MMIO, allocate pages, negotiate features, or read/write any virtio register.

The virtio PCI binding plan is metadata composition only. It requires caller-provided PCI configuration bytes and BAR sizes, preserves nested failure statuses, and produces a candidate binding record for future block/network attach logic. It does not read live ECAM, obtain BAR sizes, map MMIO, negotiate features, allocate DMA-safe buffers, publish queues, attach a driver, or prove QEMU hardware binding.

The virtio PCI fake-derived BAR sizing binding composer is metadata/test-double composition only. It requires caller-provided PCI configuration bytes and BAR sizes produced from fake-captured BAR sizing readbacks. The BAR sizes carry fake-executor provenance, not live hardware provenance. It does not execute live PCI config-space reads or writes, restore BARs on hardware, validate that a device accepted an all-ones sizing probe, map ECAM or BARs, perform volatile MMIO, allocate or map DMA memory, bind drivers, perform packet or block I/O, or prove QEMU/cloud, hosted .NET 10, ASP.NET Minimal API, or Kestrel readiness.

The virtio PCI fake-derived BAR sizing binding catalog is metadata/test-double composition only. It requires caller-provided candidates, configuration snapshots, a fake config accessor, caller-owned command/readback/size scratch buffers, and destination storage. It preserves fake-executor BAR-size provenance across multiple candidates, but it still does not enumerate live PCI, execute live config-space reads or writes, restore BARs on hardware, map ECAM or BARs, perform volatile MMIO, allocate or map DMA memory, bind drivers, perform packet or block I/O, or prove QEMU/cloud, hosted .NET 10, ASP.NET Minimal API, or Kestrel readiness.

The virtio PCI BAR register-window planner is metadata readiness only. It requires a prior successful binding plan and caller-provided mapped-BAR window records, validates aperture bounds and mapped-window policy metadata, and calculates future register-access addresses. It does not map BARs, create page mappings, change cacheability attributes, perform volatile MMIO, read or write device registers, configure queues, notify hardware, process interrupts, allocate or map DMA memory, or prove live PCI/virtio-net behavior.

The virtio PCI BAR register-window fake executor is caller-owned byte-span
evidence only. It uses already-validated access-plan metadata to read or write
test bytes, but it does not dereference planned physical/virtual addresses, map
BARs, perform volatile/live MMIO, execute memory barriers, configure queues,
notify hardware, process interrupts, allocate or map DMA memory, or prove live
PCI/virtio-net behavior.

The virtio PCI binding catalog is metadata composition only. It requires caller-provided candidate records, configuration snapshots, and BAR-size sources keyed by source index; default/zero catalog result values are explicitly not successful until initialized by the builder. It does not read live ECAM, obtain BAR sizes, probe hardware, attach a driver, or prove QEMU hardware binding.

The virtio device initialization plan and profile layer are not feature negotiation with a live device. Initialization-plan and profile result values are not successful until explicitly initialized by their planners. These contracts prove only the deterministic status-write order, selected feature mask calculation, missing-feature reporting, `FEATURES_OK` rejection handling, queue-count checks, baseline block/network profile constants, and `FAILED` status planning over caller-provided metadata. They do not reset devices, write MMIO status bits, read feature registers, observe live `FEATURES_OK`, select queues, or attach drivers.

The virtio device descriptor catalog is metadata composition only. It requires prior successful direct or fake-derived binding-plan inputs, caller-provided device configuration bytes, and caller-provided feature/queue metadata; default/zero catalog result values are explicitly not successful until initialized by the builder. The fake-derived overload preserves `VirtioPciBindingDerivedBarSizePlanResult` failure provenance, but those BAR sizes still carry fake-executor provenance only. `VirtioNetDeviceConfigRegisterWindowReader` and `VirtioBlockDeviceConfigRegisterWindowReader` can fake-read network/block device configuration from caller-owned BAR-window bytes into caller-owned scratch storage, but they still do not read a live device, stabilize config generation, write status registers, negotiate features with hardware, set up queues, attach drivers, or prove live block/network I/O.

The virtio-net queue plan, driver queue bring-up plan, queue MMIO setup planner/register-window planner/executor, queue notification planner/register-window executor/barrier-gated register-window executor/fake-sink executor, RX readiness planner, RX register-window completion/slab-completion/dispatch planners, transmit lifecycle, TX completion planner, TX descriptor ownership plan, TX descriptor free-list planner, TX publish-plan composer, TX register-window readiness planner, and TX register-window reclaim planner are not live queue setup. They prove only RX/TX queue-size validation, descriptor access direction, feature/profile checks, MAC/status metadata capture, frame-buffer bounds, split-ring memory capacity validation, future common-configuration register command ordering, register-window access planning for those commands, fake register-window byte-span execution with staged read values, fake register-accessor execution of those planned setup commands over caller-owned test storage, notify-offset capture, future notify write metadata, notify-aperture register-window access planning plus fake byte-span execution and explicit metadata gating for that metadata, fake-sink and fake register-accessor-adapter notification execution after explicit barrier-readiness metadata, RX descriptor publication plus barrier-gated register-window notification over caller-owned queue/common/notify byte spans, RX register-window completion/classification/dispatch metadata over caller-owned queue memory plus receive-buffer slab selection, zero no-offload TX header writes, split-ring descriptor publication into caller-owned memory, available-ring updates, one caller-owned TX used-ring completion validation gated by fake-sink or register-window executed-notification provenance, reclaim metadata over caller-provided buffers, caller-owned descriptor state transitions between `Free` and `InFlight`, read-only first-free-pair selection from caller-owned descriptor state, composition that marks only selected descriptors in-flight after successful caller-owned publication, TX register-window readiness composition over caller-owned queue/common/notify byte spans, and TX register-window completion/reclaim composition over caller-owned queue memory and descriptor state; they do not allocate DMA-safe buffers, map buffers for a live device, execute live MMIO queue register writes, execute real memory barriers, notify a live device, process interrupts, wait for hardware, own a full runtime descriptor allocator, mutate state before successful publication or failed completion, or prove packet I/O. The virtio-net header parser is caller-buffer metadata evidence only; it does not negotiate offload features, support mergeable buffers, feed Ethernet frames into protocols, or prove live packet I/O.

`VirtioNetTransmitReadinessPlanner` is host-tested fake-accessor TX readiness composition evidence only. It composes successful queue setup execution, caller-owned descriptor publication, explicit barrier-readiness metadata, notification planning, and fake-accessor notification execution, but it does not map BARs, perform volatile/live MMIO, execute real memory barriers, notify a live device, allocate or map DMA memory, process interrupts, poll hardware, transfer packets, or prove networking, cloud, hosted .NET 10, ASP.NET Minimal API, or Kestrel readiness.

`VirtioNetReceiveReadinessPlanner` is host-tested register-window RX readiness composition evidence only. It composes successful queue setup register-window fake execution, caller-owned RX descriptor publication, explicit barrier-readiness metadata, notification planning, and barrier-gated notify register-window fake execution, but it does not map BARs, perform volatile/live MMIO, execute real memory barriers, notify a live device, allocate or map DMA memory, process interrupts, poll hardware, complete receives, transfer packets, or prove networking, cloud, hosted .NET 10, ASP.NET Minimal API, or Kestrel readiness.

`VirtioNetReceiveRegisterWindowDispatchPlanner` is host-tested register-window RX completion/slab-selection/classification/dispatch composition evidence only. It composes successful readiness with caller-owned used-ring and receive-buffer-slab snapshots, selects the completed buffer before classification, and invokes network payload dispatch metadata, but it does not poll hardware, process interrupts, transfer packets, notify live hardware, allocate or map DMA memory, mutate network state, or prove networking, cloud, hosted .NET 10, ASP.NET Minimal API, or Kestrel readiness.

`VirtioNetTransmitRegisterWindowReadinessPlanner` is host-tested register-window TX readiness composition evidence only. It composes successful queue setup register-window fake execution, caller-owned TX descriptor publication/state marking, explicit barrier-readiness metadata, notification planning, and barrier-gated notify register-window fake execution, but it does not map BARs, perform volatile/live MMIO, execute real memory barriers, notify a live device, allocate or map DMA memory, process interrupts, poll hardware, wait for completion, transfer packets, or prove networking, cloud, hosted .NET 10, ASP.NET Minimal API, or Kestrel readiness.

`VirtioNetTransmitRegisterWindowReclaimPlanner` is host-tested register-window TX completion/reclaim composition evidence only. It composes already-successful readiness with one caller-owned used-ring completion and descriptor-state reclaim, but it does not wait for a live device, process interrupts, own a runtime descriptor allocator, perform volatile/live MMIO, notify hardware, transfer packets, or prove networking, cloud, hosted .NET 10, ASP.NET Minimal API, or Kestrel readiness.

The virtio-blk request layout, device-config parser/register-window fake reader, queue-0 MMIO setup command planner, queue setup register-window planner/fake executor/setup-barrier executor, read-session setup gate, read transaction planning, virtqueue split-ring layout, descriptor-chain publication metadata, descriptor-chain writer, descriptor free-list, descriptor ownership/reclaim, publish-planner, reclaim-planner, queue cursor, read-queue planner, read-queue completion planner, block-result projector, notification metadata, notify-offset, notify register-window, fake notify execution, barrier-gated fake notify execution, read-notify readiness composition, read-notify completion composition, read-notify block-result projection, read pipeline composition, read pipeline adapter, and read-session contracts are not block-driver evidence. The config reader can fake-read capacity from caller-owned BAR-window bytes into caller-owned scratch storage, the queue setup planner can emit future common-configuration command records from caller-provided queue metadata, the queue setup register-window planner can bind those commands to checked common-configuration access metadata, the queue setup register-window executor can fake-execute those accesses over caller-owned common BAR-window bytes, the setup-barrier executor can reject missing setup-write visibility before the fake `queue_enable` write, the read-session setup gate can refuse descriptor publication unless that setup-barrier execution succeeded and attempted `queue_enable`, the writer can materialize planned descriptors into a caller-owned memory span, and the publish/reclaim/ownership/cursor/read-queue planners can validate block ranges, select reusable caller-owned descriptors, publish one read chain, parse one caller-owned completion, advance caller-owned available/used indexes, transition state between `Free` and `InFlight`, project completion to `BlockReadResult` metadata, derive future notify-write metadata, compute the checked notify relative offset, bind that offset to future register-window access metadata, fake-execute that access over caller-owned bytes, require explicit barrier-readiness metadata after successful host-tested publication/completion provenance, compose read publication through barrier-gated fake notify execution, gate completion/reclaim on successful read-notify readiness, map gated completion into future `BlockReadResult` metadata, compose those pieces into one host-tested read pipeline, reject undersized caller-owned buffers before adapter delegation mutates queue/cursor/descriptor state, and carry cursor state across repeated adapter reads, but the stack still does not negotiate features with a live device, map BARs, allocate DMA-safe buffers, configure a live queue, publish descriptors to a live device-owned virtqueue, notify the device, process interrupts, wait for hardware, own a full runtime descriptor allocator, or read sectors from a disk.

## Next Steps

1. Implement serial logging and panic-safe writer using the existing COM1 writer, configuration, port-I/O operation/stub byte-plan, backend readiness, executable-stub memory, and panic reporter contracts.
2. Define the post-firmware physical-memory and MMIO ownership rules needed after the pre-`ExitBootServices` ACPI probe.
3. Replace the fakeable COM1 transport backend with a real OS-owned port-I/O backend and wire that live transport through `SerialDiagnosticsGuard`.
4. Add real IDT/GDT/TSS/IST setup using the host-tested descriptor, table-plan, descriptor-instruction, install-stub, and install-readiness contracts.
5. Convert the opt-in ACPI probe table evidence into a platform discovery handoff that remains valid after firmware shutdown.
6. Add MADT/HPET parsing as a separate metadata-only slice before APIC/timer use.
7. Wire the physical ECAM header reader into `PciEcamSegmentScanner` through an opt-in platform discovery path once live physical-memory/MMIO ownership is defined.
8. Feed validated ECAM scanner snapshots and BAR-sizing metadata through `VirtioPciBindingCatalog` and `VirtioDeviceDescriptorCatalog`, then add driver binding.
9. Implement the live PCI config-space accessor/executor that consumes fake-executed, address-bound BAR sizing write/read/restore command contracts after firmware shutdown.
10. Wire live virtio device features into `VirtioDeviceInitializationPlan`, then feed live virtio-blk queue metadata through the read-notify readiness executor, read-notify completion planner, read-notify block-result projector, and read pipeline composer/adapter/session before hardware notification/completion work.
11. Feed live virtio-net queue metadata and caller-owned buffer policy into `VirtioNetQueuePlan`.
12. Use `VirtioNetDriverQueueBringUpPlan`, `VirtioNetQueueMmioSetupPlanner`, `VirtioNetQueueMmioSetupExecutor`, `VirtioNetQueueNotificationPlanner`, `VirtioNetQueueNotificationExecutor`, `VirtioNetQueueNotificationRegisterWindowBarrierExecutor`, `VirtioNetReceiveReadinessPlanner`, `VirtioNetReceiveRegisterWindowCompletionPlanner`, `VirtioNetReceiveRegisterWindowSlabCompletionPlanner`, `VirtioNetReceiveRegisterWindowDispatchPlanner`, `VirtioNetTransmitDescriptorPublishPlanner`, `VirtioNetTransmitReadinessPlanner`, `VirtioNetTransmitRegisterWindowReadinessPlanner`, `VirtioNetTransmitCompletionPlanner`, and `VirtioNetTransmitRegisterWindowReclaimPlanner` as the boundary between descriptor-catalog output and future MMIO queue selection/publish/notify/completion/reclaim execution.
13. Feed future used virtio-net RX buffers through `VirtioNetHeader` before protocol dispatch, keeping checksum offload, GSO, and mergeable buffers disabled until negotiated and tested.
14. Define the live BAR-backed volatile register accessor that consumes `VirtioPciBarRegisterWindowPlanner` and `VirtioNetQueueMmioSetupRegisterWindowPlanner` output, plus the real memory-ordering primitive, DMA ownership rules, and QEMU proof path needed to replace the current fake RX/TX readiness boundaries.
15. Implement live virtio-blk queue ownership, notification, and completion after the read-only request layout, split-ring writer, descriptor ownership state, publish/reclaim/cursor/read-queue/read-queue-completion planners, discovery, and initialization-planning contracts are wired to live device ownership rules.
