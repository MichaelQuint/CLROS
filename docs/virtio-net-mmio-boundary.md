# Virtio-Net MMIO Notification Boundary

## Current Status

This document plans the first live virtio-net PCI MMIO boundary after the host-tested queue bring-up, MMIO setup command/execution, descriptor publication, queue-notification, and TX completion/reclaim planners. It is design and host-test evidence only. No code in this repository currently maps virtio PCI BARs, performs volatile MMIO reads/writes, configures a live virtqueue, notifies a live device, handles interrupts, waits for hardware completion, or proves packet I/O in QEMU.

Existing inputs:

- `VirtioPciBindingCatalog` and `VirtioPciBindingPlan` provide PCI identity, capability, BAR, and aperture metadata from caller-provided snapshots.
- `PciBarSizingProbePlanner` plans the ordered config-space write-all-ones, read-sizing-readback, and restore-original command metadata that could produce the caller-owned readbacks consumed by `PciBarSizingPlanner`.
- `PciBarSizingProbeAddressPlanner` binds those planned write/read/restore commands to checked ECAM config-space address metadata for a known segment and bus/device/function before any live accessor executes them.
- `PciBarSizingProbeExecutor` fake-executes those address-bound commands through an injected config-accessor test double and captures caller-owned sizing readbacks, but does not touch live ECAM/config space.
- `PciBarSizingProbeDerivedSizePlanner` composes that fake execution with `PciBarSizingPlanner` so derived BAR aperture metadata and virtio aperture-validator input can be tested from fake-captured readbacks.
- `VirtioPciBindingDerivedBarSizePlanner` composes caller-provided virtio-net PCI configuration bytes, fake-derived BAR sizing metadata, and `VirtioPciBindingPlan` so network binding metadata can consume fake-captured sizing readbacks without live config access.
- `VirtioPciBindingDerivedBarSizeCatalog` catalogs caller-provided virtio PCI candidates through that fake-derived binding path while preserving source indexes, PCI locations, and nested fake-probe/binding provenance.
- `PciBarSizingPlanner` derives BAR aperture-size metadata from decoded BAR descriptors plus caller-owned all-ones sizing readback snapshots without performing live PCI config writes, live reads, BAR restore, BAR mapping, or MMIO.
- `VirtioPciBarApertureValidator` validates common, notify, ISR, and device-configuration capability regions against caller-provided or `PciBarSizingPlanner`-derived BAR sizes.
- `VirtioPciBarRegisterWindowPlanner` validates caller-provided mapped-BAR window metadata against those apertures and plans aligned register access addresses for a future accessor without mapping BARs or performing volatile MMIO.
- `VirtioPciBarRegisterWindowAccessExecutor` can fake-execute already-validated access plans against caller-owned BAR-window byte spans for host tests without mapping BARs, dereferencing physical addresses, or performing volatile MMIO.
- `VirtioDeviceInitializationPlan` and `VirtioDeviceInitializationProfile` plan status/feature negotiation without touching registers.
- `VirtioPciModernRegisterMap` centralizes the modern virtio PCI common, notify-width, ISR, and virtio-net device-configuration constants needed by the first live queue setup boundary.
- `VirtioDeviceDescriptorCatalog` can consume fake-derived virtio PCI binding provenance plus caller-provided virtio-net device configuration, feature bits, and queue counts before queue bring-up metadata is planned.
- `VirtioNetDeviceConfigRegisterWindowReader` fake-reads the virtio-net MAC/status device-configuration bytes from a caller-owned device BAR-window span into caller-owned scratch storage before parsing with `VirtioNetDeviceConfig`.
- `VirtioNetDriverQueueBringUpPlan` validates network descriptor metadata, selected features, RX/TX queue sizes, required split-ring memory, and buffer capacity without writing MMIO.
- `VirtioNetReceiveLifecycle` and `VirtioNetTransmitLifecycle` publish caller-owned descriptors/rings in memory without notifying a device.
- `VirtioNetQueueMmioSetupPlanner` plans RX/TX common-configuration queue setup command order, physical queue addresses, queue sizes, queue enable writes, and cached `queue_notify_off` metadata without invoking a register accessor.
- `VirtioNetQueueMmioSetupRegisterWindowPlanner` translates those setup commands into checked common-configuration register-window access plans for a future accessor without executing register reads or writes.
- `VirtioNetQueueMmioSetupRegisterWindowExecutor` fake-executes those setup commands through checked common-configuration register-window access plans over caller-owned common BAR-window bytes without performing live MMIO.
- `VirtioNetQueueMmioSetupExecutor` consumes that setup command plan and executes `Read16`, `Write16`, and `Write64` operations through an injected fake virtio PCI register accessor while preserving setup, read, write, mismatch, short-command-source, and missing-accessor failures without live MMIO.
- `VirtioPciQueueNotificationPlanner` and `VirtioNetQueueNotificationPlanner` calculate the notify aperture offset, write width, notify value, and RX/TX publish provenance after successful queue setup and descriptor publication without performing the MMIO write.
- `VirtioNetQueueNotificationPlanner.PlanRegisterWindowAccess` translates successful RX/TX queue notification plans into checked notify-aperture register-window access plans without executing fake or live notification writes.
- `VirtioNetQueueNotificationRegisterWindowExecutor` fake-executes those checked notification access plans over caller-owned notify BAR-window bytes without performing live MMIO or notifying hardware.
- `VirtioNetQueueNotificationRegisterWindowBarrierExecutor` gates that fake register-window notification execution on explicit descriptor-write, available-ring-write, and ordered-before-notify metadata before touching caller-owned notify BAR-window bytes.
- `VirtioNetReceiveReadinessPlanner` composes successful queue setup register-window fake execution, caller-owned RX descriptor publication, explicit barrier-readiness metadata, notification planning, and barrier-gated notify register-window fake execution without live MMIO.
- `VirtioNetReceiveRegisterWindowDispatchPlanner` composes successful RX register-window readiness, one caller-owned RX used-ring completion, caller-owned slab selection for the completed descriptor, receive-frame classification, and network payload dispatch metadata without live MMIO, polling, interrupts, DMA mapping/allocation, packet I/O, or live device notification.
- `VirtioNetQueueNotificationExecutor` consumes successful notification-plan metadata plus explicit barrier-readiness metadata and writes the planned 16-bit queue notification through an injected fake sink, including a fake virtio PCI register-accessor adapter path, preserving failed-plan, missing-barrier, sink/accessor not-ready, range, and write failures without live MMIO.
- `VirtioNetTransmitCompletionPlanner` validates one caller-owned TX used-ring entry against successful TX publication and successful fake-sink or barrier-gated register-window TX notification execution provenance, returning descriptor reclaim metadata without polling a live device.
- `VirtioNetTransmitDescriptorOwnershipPlan` consumes successful TX publication/completion results and mutates only a caller-owned descriptor state span between `Free` and `InFlight`.
- `VirtioNetTransmitDescriptorFreeListPlanner` reads that caller-owned descriptor state span and selects the first contiguous free TX descriptor pair without mutating the table.
- `VirtioNetTransmitDescriptorPublishPlanner` composes free-pair selection, `VirtioNetTransmitLifecycle.Publish`, and ownership `MarkPublished` into one caller-owned publish plan without MMIO, notification, polling, interrupts, DMA mapping/allocation, packet I/O, used-ring waiting, or queue-memory reads.
- `VirtioNetTransmitReadinessPlanner` composes successful fake-accessor queue setup execution, caller-owned TX descriptor publication, explicit barrier-readiness metadata, notification planning, and fake-accessor-backed notification execution into one host-tested readiness boundary without live MMIO, BAR mapping, DMA allocation/mapping, packet I/O, interrupts, polling, or live device notification.
- `VirtioNetTransmitRegisterWindowReadinessPlanner` composes successful queue setup register-window fake execution, caller-owned TX descriptor publication/state marking, explicit barrier-readiness metadata, notification planning, and barrier-gated notify register-window fake execution without live MMIO.
- `VirtioNetTransmitRegisterWindowReclaimPlanner` composes successful TX register-window readiness, one caller-owned used-ring completion validation, and caller-owned descriptor ownership reclaim without live MMIO, polling, interrupts, DMA mapping/allocation, packet I/O, or live device notification.

The current implementation now has both fake register-window and fake
register-accessor notification paths. The register-window path can gate the
caller-owned notify BAR-window write on explicit barrier-readiness metadata and
then compose one caller-owned used-ring completion into descriptor-state
reclaim, but that metadata still is not a real memory-ordering primitive.
Follow-up work must
replace the fake byte-span/accessor paths and metadata-only barrier with real
BAR-backed volatile MMIO ownership, real ordering primitives, and QEMU packet
evidence before any live virtio-net claim exists.

## Reference Basis

The offsets below follow the modern virtio PCI common configuration and notification model documented by the OASIS virtio 1.3 specification:

- Virtio over PCI device layout and common configuration: https://docs.oasis-open.org/virtio/virtio/v1.3/csd01/virtio-v1.3-csd01-diff-from-v1.2-cs01.html
- Common configuration fields, queue-selection fields, and notification fields are documented around sections 4.1.4.3 and 4.1.5.
- Notification address calculation is `notify_cap.offset + queue_notify_off * notify_off_multiplier`.
- ISR status is a single byte; reading it resets it to zero for INTx handling.

## Ownership Model

Keep the boundary split this way:

- Packet/protocol code owns Ethernet, ARP, IPv4, UDP, DHCP, DNS, TCP, and HTTP byte layouts. It must not know BAR addresses or write registers.
- Queue lifecycle code owns caller-provided descriptor-table, available-ring, used-ring, virtio-net header, and frame-buffer memory layout. It must not map BARs or perform device notification.
- Memory/platform code owns DMA-safe page allocation, physical address translation, cacheability, page permissions, and memory barriers.
- Virtio PCI transport code owns volatile MMIO access to common, notify, ISR, and device-specific configuration regions.
- Virtio-net driver coordination owns the sequencing between queue publication, memory barriers, MMIO queue configuration, notification, ISR reads, and dispatch into packet code.

The first live path should still be single-queue-pair and no-offload:

- RX queue index: `0`
- TX queue index: `1`
- No `VIRTIO_F_NOTIFICATION_DATA`
- No `VIRTIO_F_NOTIF_CONFIG_DATA`
- No mergeable buffers
- No checksum offload
- No GSO
- No MSI-X claim unless a separate MSI-X issue implements and tests it

## Register Access Boundary

Introduce a status-coded accessor contract before live MMIO:

```csharp
public interface IVirtioPciRegisterAccessor
{
    VirtioPciRegisterReadResult Read8(VirtioPciBarAperture aperture, uint relativeOffset);
    VirtioPciRegisterReadResult Read16(VirtioPciBarAperture aperture, uint relativeOffset);
    VirtioPciRegisterReadResult Read32(VirtioPciBarAperture aperture, uint relativeOffset);
    VirtioPciRegisterReadResult Read64(VirtioPciBarAperture aperture, uint relativeOffset);
    VirtioPciRegisterWriteStatus Write8(VirtioPciBarAperture aperture, uint relativeOffset, byte value);
    VirtioPciRegisterWriteStatus Write16(VirtioPciBarAperture aperture, uint relativeOffset, ushort value);
    VirtioPciRegisterWriteStatus Write32(VirtioPciBarAperture aperture, uint relativeOffset, uint value);
    VirtioPciRegisterWriteStatus Write64(VirtioPciBarAperture aperture, uint relativeOffset, ulong value);
}
```

The interface name is a design placeholder. The implementation must avoid exceptions in kernel-facing paths and return status codes for:

- missing BAR mapping
- aperture-region overflow
- unaligned access
- unsupported access width
- volatile read failure
- volatile write failure
- write to a read-only field requested by a planner
- write after device failed or needs reset

Host tests should use a fake register accessor over caller-owned byte arrays before any physical BAR mapping exists.

The current implementation includes the queue setup and notification-width
slices of this shape. `VirtioNetQueueMmioSetupExecutor` calls the injected
`IVirtioPciRegisterAccessor` for planned `Read16`, `Write16`, and `Write64`
operations, and `VirtioPciNotifyRegisterAccessorSink` adapts a status-coded
`Write16` call into the existing `IVirtioPciNotifyWriteSink` contract. This is
host-tested fake accessor evidence only; it does not provide a volatile MMIO
accessor or BAR mapping.

## BAR Register-Window Boundary

The register-window contract validates the metadata required before a future
BAR-backed volatile accessor can be installed. It consumes successful virtio PCI
binding/aperture metadata plus caller-provided BAR mapping metadata and reports
whether common, notify, ISR, and device-configuration apertures can be addressed
by a future accessor.

`VirtioPciBarRegisterWindowPlanner` checks that:

- the virtio PCI binding plan and aperture validation succeeded
- common, notify, ISR, and device-configuration apertures are present
- mapped BAR metadata exists for every referenced BAR index
- each mapped window is marked mapped, MMIO/device memory, and uncached
- mapped physical BAR bases match the decoded BAR base addresses
- aperture offset plus length fits within the mapped BAR window length
- planned register accesses use supported 1, 2, 4, or 8 byte widths
- planned register accesses are aligned and stay inside the aperture

The result preserves BAR index, aperture kind, aperture offsets, mapped physical
and virtual base addresses, computed register-access addresses, access width,
and nested binding/aperture failure status. This is readiness metadata only. It
does not map physical BARs, create page mappings, change cacheability
attributes, perform volatile reads or writes, execute memory barriers, configure
queues, notify a device, poll ISR status, process interrupts, allocate or map
DMA memory, or prove packet I/O.

`VirtioPciBarRegisterWindowAccessExecutor` is a fake byte-span executor for
already-validated register-window access plans. It consumes a successful
`VirtioPciBarRegisterWindowAccessPlan` plus caller-owned BAR-window bytes and
performs host-tested little-endian 1, 2, 4, and 8 byte reads/writes with
status-coded missing-plan, unsupported-width, and short-buffer failures. This
is not the live accessor: it does not map BARs, dereference physical or virtual
BAR addresses, perform volatile/live MMIO, execute memory barriers, configure
queues, notify a live device, process interrupts, allocate or map DMA memory,
poll hardware, transfer packets, or prove live PCI behavior.

`VirtioNetQueueMmioSetupRegisterWindowPlanner` is the virtio-net bridge from
queue setup command metadata to those generic register-window access plans. It
consumes a successful `VirtioNetQueueMmioSetupPlanResult`, the planned command
span, a bound common-configuration register window, and caller-owned destination
storage for access plans. The result preserves setup status, command kind,
command index, queue index, relative offset, width, destination capacity, and
nested register-window failure status. It does not execute fake or live register
reads/writes; the existing fake executor remains the only execution-shaped host
boundary.

`VirtioNetQueueMmioSetupRegisterWindowExecutor` is the byte-span execution
bridge for that same setup command metadata. It consumes a successful setup
plan, the planned command span, a bound common-configuration register window,
and caller-owned common BAR-window bytes. Each command is translated to a
checked access plan, then executed through
`VirtioPciBarRegisterWindowAccessExecutor`; read commands stage the expected
device value into the byte span before reading it back because a plain byte span
does not model queue-select-dependent hardware registers. The result preserves
setup status, command kind/index, queue index, relative offset, width, expected
and observed values, nested register-window status, and nested access-execution
status. It does not map BARs, dereference physical addresses, perform
volatile/live MMIO, execute memory barriers, configure a live queue, notify a
live device, process interrupts, allocate or map DMA memory, or transfer
packets.

`VirtioNetQueueNotificationPlanner.PlanRegisterWindowAccess` is the
notification-side bridge from successful RX/TX queue notification plans to those
generic register-window access plans. It consumes a successful
`VirtioNetQueueNotificationPlanResult` plus a bound notify register window and
records the future 16-bit notify write address, width, value, queue index,
notify offset, and nested notification/register-window statuses in caller-owned
metadata. It does not execute fake or live register writes, map BARs, perform
volatile MMIO, execute memory barriers, notify a live device, poll hardware,
process interrupts, allocate or map DMA memory, or transfer packets.

`VirtioNetQueueNotificationRegisterWindowExecutor` is the fake byte-span
execution bridge for that notification access plan. It first calls
`PlanRegisterWindowAccess`, then writes the planned 16-bit queue notification
through `VirtioPciBarRegisterWindowAccessExecutor` into a caller-owned notify
BAR-window byte span. The result preserves queue, notify offset, write value,
BAR-relative offset, access width, nested notification/register-window status,
and nested access-execution status without exposing physical or virtual BAR
addresses. It does not map BARs, dereference physical addresses, perform
volatile/live MMIO, execute memory barriers, notify a live device, poll
hardware, process interrupts, allocate or map DMA memory, or transfer packets.

`VirtioNetQueueNotificationRegisterWindowBarrierExecutor` adds the same explicit
publish-before-notify readiness gate used by the fake-sink path before the
register-window byte-span write. It plans the register-window access first,
rejects failed notification plans or register-window plans without touching the
caller-owned notify bytes, rejects missing descriptor-write/available-ring-write/
ordered-before-notify metadata before the fake byte-span write, and then writes
through `VirtioPciBarRegisterWindowAccessExecutor`. It does not execute a real
memory barrier, map BARs, perform volatile/live MMIO, notify a live device,
poll hardware, process interrupts, allocate or map DMA memory, or transfer
packets.

`VirtioNetReceiveReadinessPlanner` composes the RX side of that fake
register-window boundary. It consumes a successful queue setup register-window
execution result, publishes caller-owned RX descriptors into caller-owned queue
memory, plans the RX notification, and calls
`VirtioNetQueueNotificationRegisterWindowBarrierExecutor` with explicit
barrier-readiness metadata. The result preserves nested setup, register-window,
publish, barrier, notification-plan, and access-execution statuses. It does not
execute live queue setup, map BARs, perform volatile/live MMIO, execute a real
memory barrier, notify a live device, poll hardware, process interrupts,
allocate or map DMA memory, complete receives, or transfer packets.

`VirtioNetReceiveRegisterWindowSlabCompletionPlanner` composes a successful RX
register-window readiness result with one caller-owned RX used-ring completion,
selects the completed buffer from a caller-owned receive-buffer slab, then
delegates to `VirtioNetReceiveRegisterWindowCompletionPlanner` and
`VirtioNetReceiveFrameClassifier`. It preserves readiness, notification-window,
slab-selection, completion, and classification status provenance while
rejecting failed readiness, missing or malformed completion, short slabs, and
classifier failures before any live-device claim. It does not poll hardware,
process interrupts, map BARs, perform volatile/live MMIO, execute real memory
barriers, allocate or map DMA memory, transfer packets, notify a live device, or
prove live networking.

`VirtioNetReceiveRegisterWindowDispatchPlanner` adds the next caller-owned
metadata handoff after slab completion. It invokes
`NetworkReceiveDispatcher.Dispatch` only after slab selection and receive-frame
classification succeed, slices the selected buffer to the used byte count, and
preserves slab-completion, classifier, packet, ARP/IPv4 dispatch, selected
buffer, descriptor, and used-ring provenance. It does not poll hardware, process
interrupts, map BARs, perform volatile/live MMIO, execute real memory barriers,
allocate or map DMA memory, transfer packets, notify a live device, own network
state, or prove live networking.

`VirtioNetTransmitRegisterWindowReadinessPlanner` composes the TX side of that
fake register-window boundary. It consumes a successful queue setup
register-window execution result, selects and publishes a caller-owned TX
descriptor pair through the existing TX publish-plan composer, plans the TX
notification, and calls
`VirtioNetQueueNotificationRegisterWindowBarrierExecutor` with explicit
barrier-readiness metadata. The result preserves nested setup, register-window,
publish, ownership, barrier, notification-plan, and access-execution statuses.
It does not execute live queue setup, map BARs, perform volatile/live MMIO,
execute a real memory barrier, notify a live device, poll hardware, process
interrupts, allocate or map DMA memory, wait for completion, or transfer
packets.

## Common Configuration Offsets

Modern virtio PCI common configuration is little-endian. The first live queue setup and notification path needs these offsets:

| Offset | Width | Field | First live use |
| --- | ---: | --- | --- |
| `0x00` | 32 | `device_feature_select` | Select feature bits 0-31 or 32-63 before reading `device_feature`. |
| `0x04` | 32 | `device_feature` | Read device feature bits for negotiation. |
| `0x08` | 32 | `driver_feature_select` | Select feature bits 0-31 or 32-63 before writing `driver_feature`. |
| `0x0C` | 32 | `driver_feature` | Write accepted feature bits. |
| `0x10` | 16 | `config_msix_vector` | Defer; write/read only after MSI-X issue. |
| `0x12` | 16 | `num_queues` | Verify at least two queues for virtio-net baseline. |
| `0x14` | 8 | `device_status` | Reset and write status sequence. |
| `0x15` | 8 | `config_generation` | Defer except for device-config reread stabilization. |
| `0x16` | 16 | `queue_select` | Select RX/TX queue before queue fields below. |
| `0x18` | 16 | `queue_size` | Read max size, then write chosen queue size. |
| `0x1A` | 16 | `queue_msix_vector` | Defer; write/read only after MSI-X issue. |
| `0x1C` | 16 | `queue_enable` | Write `1` after queue addresses are installed. |
| `0x1E` | 16 | `queue_notify_off` | Read after selecting a queue to calculate notify address. |
| `0x20` | 64 | `queue_desc` | Write descriptor-table physical address. |
| `0x28` | 64 | `queue_driver` | Write driver/available-ring physical address. |
| `0x30` | 64 | `queue_device` | Write device/used-ring physical address. |

The current baseline does not negotiate the optional notification-data fields beyond `0x38`. `VirtioPciModernRegisterMap` now names the common configuration offsets above, and `VirtioPciDeviceDiscovery.CommonConfigurationMinimumLength` requires the common capability to cover through `queue_device`, so the first live queue setup cannot accept a common aperture that omits offsets `0x20..0x37`.

Status bits needed at `device_status`:

| Bit | Value | Name | First live use |
| ---: | ---: | --- | --- |
| 0 | `0x01` | `ACKNOWLEDGE` | Device recognized. |
| 1 | `0x02` | `DRIVER` | Driver can operate this device kind. |
| 2 | `0x04` | `DRIVER_OK` | Queue setup is complete and buffers are available. |
| 3 | `0x08` | `FEATURES_OK` | Feature negotiation completed; must be read back. |
| 6 | `0x40` | `DEVICE_NEEDS_RESET` | Diagnose and stop. |
| 7 | `0x80` | `FAILED` | Controlled failure path. |

## Device-Specific Configuration Offsets

Virtio-net device configuration is read-only for this milestone:

| Offset | Width | Field | First live use |
| --- | ---: | --- | --- |
| `0x00` | 6 | `mac` | Read when `VIRTIO_NET_F_MAC` is negotiated. |
| `0x06` | 16 | `status` | Read when `VIRTIO_NET_F_STATUS` is negotiated. Bit 0 means link up; bit 1 means announce. |

The live reader should reuse `VirtioNetDeviceConfig` by copying the required bytes from device-config MMIO into a caller-owned fixed buffer, then parsing that buffer. Packet code must not read device config.

`VirtioNetDeviceConfigRegisterWindowReader` now proves the fake version of that
copy/parse boundary. It plans checked reads against a bound device-configuration
register window, reads the MAC prefix/suffix and optional status word through
`VirtioPciBarRegisterWindowAccessExecutor` over caller-owned device BAR-window
bytes, writes only caller-owned scratch bytes, and then delegates validation to
`VirtioNetDeviceConfig.TryParse`. It preserves scratch-length, register-window,
access-execution, and config-parse failures without mapping BARs or reading a
live device.

## Queue Setup Sequence

For each virtio-net queue selected by `VirtioNetDriverQueueBringUpPlan`:

1. Write queue index to `queue_select` (`0` RX, `1` TX).
2. Read `queue_size`; reject `0` as unavailable.
3. Verify planned queue size is not greater than device queue size.
4. Write chosen queue size to `queue_size`.
5. Write descriptor-table physical address to `queue_desc`.
6. Write available-ring physical address to `queue_driver`.
7. Write used-ring physical address to `queue_device`.
8. Read `queue_notify_off` and cache it with the queue state.
9. Write `1` to `queue_enable`.
10. Read back queue state where useful for diagnostics, but do not depend on undocumented side effects.

First live implementation should leave `config_msix_vector` and `queue_msix_vector` at `NO_VECTOR` or untouched until a separate interrupt-controller/MSI-X issue exists. INTx ISR-status polling can be added independently.

`VirtioNetQueueMmioSetupExecutor` now executes the existing planned RX/TX
sequence through an injected fake register accessor. It validates a successful
setup plan, rejects short command sources and missing accessors before touching
the accessor, dispatches supported widths from the command metadata, compares
read values against the planned queue-size and notify-offset values, preserves
raw accessor read/write statuses, and maps accessor `NotReady` to an
accessor-not-ready execution status. It does not map BARs, perform volatile or
live MMIO, publish descriptors, notify a device, allocate or map DMA memory,
handle interrupts, or move packets.

`VirtioNetQueueMmioSetupRegisterWindowPlanner` now also proves that every setup
command can be translated into a checked common-configuration register-window
access plan before any future BAR-backed accessor exists. It rejects failed
setup plans, short command sources, undersized access-plan destinations,
unsupported command widths, and propagated register-window failures without
configuring queues or touching registers.

`VirtioNetQueueMmioSetupRegisterWindowExecutor` now proves the next fake
byte-span boundary over those same setup commands. It rejects failed setup plans
and short command sources before touching caller-owned bytes, uses the generic
register-window planner and fake byte-span executor for each command, compares
read commands against the command's expected queue-size or notify-offset value,
and propagates access failures without performing live queue configuration.

## Notification Boundary

The notify aperture comes from `VirtioPciBarApertureSet.NotifyConfiguration`.

For this baseline, because notification data features are not negotiated, notify by writing a 16-bit value:

```text
notifyOffset = queue_notify_off * notify_off_multiplier
notifyAddress = notifyAperture.BarBaseAddressBits + notifyAperture.Offset + notifyOffset
notifyValue = queueIndex
write16(notifyAperture, notifyOffset, notifyValue)
```

Validation before write:

- queue setup succeeded
- queue was enabled
- queue has at least one newly published descriptor head
- memory barrier completed after descriptor/available-ring writes
- `queue_notify_off * notify_off_multiplier` does not overflow
- notify write range is within the notify aperture
- notify value is a queue index because notification-data features are not negotiated
- write width is exactly 16 bits

RX notification is needed after publishing empty RX buffers if notifications are
not suppressed. TX notification is needed after publishing a transmit frame.
`VirtioNetQueueNotificationPlanner` proves this coordinator boundary over
host-tested RX/TX publish results and the generic virtio PCI notify planner. It
records queue index, `queue_notify_off`, notify relative offset, 16-bit write
value, previous/next available-ring indexes, and descriptor count, while
preserving failures from queue setup, descriptor publication, disabled queues,
unsupported notification-data features, invalid notify apertures, and
notify-range validation.

`VirtioNetQueueNotificationPlanner.PlanRegisterWindowAccess` now additionally
proves that a successful notification plan can be translated into a checked
notify-aperture register-window access plan before any future BAR-backed
accessor exists. It rejects failed notification plans and propagated
register-window failures while preserving queue, notify offset, write value,
write width, descriptor-count, available-ring, nested notification, and nested
register-window status metadata without touching registers.

`VirtioNetQueueNotificationRegisterWindowExecutor` now proves that the checked
notify access plan can be applied through the generic fake register-window
executor over caller-owned notify BAR-window bytes. It rejects failed
notification plans, propagated register-window failures, and propagated
byte-span access failures while preserving only BAR-relative access metadata and
the written queue value.

`VirtioNetQueueNotificationRegisterWindowBarrierExecutor` now proves the
barrier-aware version of that fake byte-span write. It preserves barrier
metadata and nested notification/register-window/access-execution status, rejects
missing barrier readiness before writing caller-owned notify bytes, and remains a
metadata-gated host-test path rather than a real memory-ordering or live MMIO
boundary.

`VirtioNetQueueNotificationExecutor` is now the first execution-shaped boundary
for that plan. It requires explicit descriptor-write, available-ring-write, and
ordered-before-notify readiness metadata before calling an injected fake notify
sink. On success it issues exactly one `Write16(notifyAperture,
notifyRelativeOffset, queueIndex)` through that sink and returns the same queue,
offset, value, descriptor-count, available-ring, barrier, nested planner, and
sink status provenance. It rejects failed notification plans before checking the
barrier, rejects missing barrier readiness before touching the sink, rejects
unsupported write widths, maps sink not-ready/range/write failures into
status-coded results, and never maps BARs, performs live MMIO, polls a device,
handles interrupts, mutates descriptor state, reads queue memory, allocates or
maps DMA memory, or transfers packets.

`VirtioPciNotifyRegisterAccessorSink` routes that same planned `Write16` call
through a fake virtio PCI register-accessor-shaped contract. It maps accessor
range failures to notification range failures and accessor not-ready,
unsupported-width, or write failures to existing sink/executor failure statuses.
The adapter contains no live MMIO implementation, does not map BARs, and does
not notify a live device.

`VirtioNetTransmitReadinessPlanner` consumes successful fake queue-setup
execution, publishes a caller-owned no-offload TX descriptor pair, requires
explicit barrier-readiness metadata, creates the TX notification plan, and
executes that notification through `VirtioPciNotifyRegisterAccessorSink`. The
result preserves nested setup, publish, barrier, notification-plan, notification
execution, and accessor/sink status provenance while rejecting failed setup
execution before publication, failed publication before notification, missing
barriers before fake writes, and fake notification failures before any completion
planning.

Suppression, event index, packed rings, notification data, config data, real
memory-barrier execution, physical register writes, and live device
notification are still deferred.

## TX Completion Boundary

TX completion requires a successful transmit descriptor publication, successful
TX notification execution provenance from either `VirtioNetQueueNotificationExecutor`
or `VirtioNetQueueNotificationRegisterWindowBarrierExecutor`, and a
caller-owned used-ring snapshot. A planner-only
`VirtioNetQueueNotificationPlanResult` is not sufficient for completion
planning. `VirtioNetTransmitCompletionPlanner` currently validates one
completion only:

- `used.idx` must advance exactly from `previousUsedIndex` to `previousUsedIndex + 1`, with 16-bit wrapping.
- the used-ring slot is `previousUsedIndex & (queueSize - 1)`.
- `used.id` must match the published TX head descriptor.
- `used.len` must be `0` for the current no-offload TX baseline.
- executed notification provenance must be TX-kind, transmit-queue, and must match the publication's descriptor count and available-ring indexes.

The result records the previous/current/next used indexes, used-ring slot and
element offset, descriptor head, used byte count, header/frame descriptor
indexes, reclaim descriptor count, notification kind/queue/index metadata,
notification execution status, nested notification-plan status, PCI
notification-plan status, fake-sink write status, and register-window
notification execution/access metadata when that provenance shape is used. It
does not poll the device, wait for an interrupt, read ISR status, execute a
memory barrier, mutate a descriptor free list, recycle buffers, or prove packet
transmission.

`VirtioNetTransmitRegisterWindowReclaimPlanner` composes the register-window TX
readiness result with this completion planner and
`VirtioNetTransmitDescriptorOwnershipPlan.ApplyCompletion`. It rejects failed
readiness before reading queue memory, rejects missing or malformed completion
before mutating descriptor state, and applies reclaim only to the caller-owned
`Free`/`InFlight` state span after successful completion validation.

## Descriptor Ownership Boundary

TX descriptor ownership is now represented by a host-tested caller-owned state
table with two states:

- `Free`
- `InFlight`

`VirtioNetTransmitDescriptorOwnershipPlan.MarkPublished` consumes a successful
`VirtioNetTransmitLifecycleResult` and marks the published TX virtio-net header
and Ethernet frame descriptors `InFlight`. It rejects failed publication,
state spans that do not cover both descriptor indexes, and descriptors already
marked `InFlight`.

`VirtioNetTransmitDescriptorOwnershipPlan.ApplyCompletion` consumes a
successful `VirtioNetTransmitCompletionPlanResult` and frees only descriptors
currently marked `InFlight`. It rejects failed completion plans, short state
spans, descriptors not currently in-flight, and duplicate completion after the
descriptors have already been freed.

The register-window reclaim composer uses that same ownership boundary after a
successful register-window readiness result and one validated caller-owned TX
used-ring entry. This keeps reclaim status provenance together with the
register-window notification metadata while still avoiding a runtime allocator
or live device interaction.

`VirtioNetTransmitDescriptorFreeListPlanner.SelectFreeDescriptorPair` consumes
a successful `VirtioNetQueuePlanResult` plus a read-only caller-owned
descriptor state span. It validates that the state table covers the TX queue,
rejects any descriptor state outside `Free` or `InFlight`, skips in-flight
descriptors, selects the first contiguous two-descriptor free pair for the TX
virtio-net header and Ethernet frame, reports capacity exhaustion when no pair
exists, and leaves reclaimed descriptors reusable after `ApplyCompletion`
returns them to `Free`.

`VirtioNetTransmitDescriptorPublishPlanner.PublishFromFreeList` composes that selector with
`VirtioNetTransmitLifecycle.Publish` and
`VirtioNetTransmitDescriptorOwnershipPlan.MarkPublished`. It chooses the first
reusable contiguous two-descriptor pair, writes the no-offload TX header and
frame descriptors into caller-owned split-ring memory, updates the caller-owned
available ring through the lifecycle publication path, and marks only the
selected descriptors `InFlight` after publication succeeds. If selection,
lifecycle publication, or ownership marking fails, the composed result preserves
status provenance and does not mark descriptors in-flight.

This boundary is still not live virtio-net TX and not a full runtime allocator.
The free-list planner chooses a candidate pair from caller-owned state, and the
publish planner writes caller-owned queue memory, writes the caller-owned TX
header, and mutates caller-owned descriptor state only after successful
publication. It does not coordinate concurrent TX producers, execute memory
barriers, read queue memory, write MMIO, notify a live device, poll or wait for
used-ring completion, process interrupts, map or allocate DMA memory, perform
packet I/O, or prove packet transmission.

## ISR Boundary

The ISR aperture comes from `VirtioPciBarApertureSet.IsrStatus`.

For INTx handling, read one byte at offset `0`. The read is destructive and clears the ISR status in the device. Interpret:

- bit 0: at least one virtqueue has used-buffer progress
- bit 1: device configuration changed
- all other bits: reserved for this milestone

If MSI-X is enabled later, queue interrupt handling should not read ISR status for queue events. That is a separate interrupt issue.

`VirtioPciIsrStatusParser` is the current host-tested planning boundary for this section. It parses caller-provided ISR bytes, preserves reserved bits for diagnostics, and returns a one-byte offset-`0` read plan against the validated ISR aperture. It does not perform the MMIO read, configure MSI-X, configure APIC/IOAPIC, drain virtqueues, or dispatch packets.

## Failure Boundaries

The live transport layer should return status-coded failures such as:

- `BindingPlanFailed`
- `ApertureMissing`
- `CommonApertureTooSmall`
- `NotifyApertureTooSmall`
- `DeviceConfigApertureTooSmall`
- `RegisterReadFailed`
- `RegisterWriteFailed`
- `DeviceNeedsReset`
- `FeaturesOkRejected`
- `QueueUnavailable`
- `QueueSizeRejected`
- `QueueAddressInvalid`
- `QueueEnableFailed`
- `NotifyOffsetOverflow`
- `NotifyAddressOutOfRange`
- `UnsupportedNotificationData`
- `IsrReadFailed`

None of these statuses should throw. Panic remains a higher-level policy decision for unrecoverable boot/device failures.

## Tests

Host tests now cover the register-map constants and first-live common configuration length:

- `TEST_PASS drivers.virtiopci.registermap common offsets`
- `TEST_PASS drivers.virtiopci.registermap status bits`
- `TEST_PASS drivers.virtiopci.registermap isr and notify bits`
- `TEST_PASS drivers.virtiopci.registermap validates first live common length`
- `TEST_PASS drivers.virtiopci.registermap net config offsets`

Host tests cover virtio PCI binding from fake-derived BAR sizing metadata:

- `TEST_PASS drivers.virtio.pci.binding.derive creates network binding from fake bar probe`
- `TEST_PASS drivers.virtio.pci.binding.derive creates block binding from fake bar probe`
- `TEST_PASS drivers.virtio.pci.binding.derive reports header parse failure`
- `TEST_PASS drivers.virtio.pci.binding.derive reports bar decode failure`
- `TEST_PASS drivers.virtio.pci.binding.derive propagates derived size failure`
- `TEST_PASS drivers.virtio.pci.binding.derive reports short size destination`
- `TEST_PASS drivers.virtio.pci.binding.derive propagates binding failure`
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
- `TEST_PASS drivers.virtio.pci.binding.derive.catalog preserves no-live-config-access boundary`

Host tests cover virtio descriptor cataloging from fake-derived binding
provenance:

- `TEST_PASS drivers.virtio.descriptor.catalog.derive creates network descriptor`
- `TEST_PASS drivers.virtio.descriptor.catalog.derive reports derived binding failure`
- `TEST_PASS drivers.virtio.descriptor.catalog.derive reports missing device config`
- `TEST_PASS drivers.virtio.descriptor.catalog.derive reports initialization failure`
- `TEST_PASS drivers.virtio.descriptor.catalog.derive preserves no-live-device boundary`

Host tests cover BAR register-window address planning:

- `TEST_PASS drivers.virtiopci.registerwindow binds common notify isr device apertures`
- `TEST_PASS drivers.virtiopci.registerwindow plans aligned register access`
- `TEST_PASS drivers.virtiopci.registerwindow rejects missing mapping`
- `TEST_PASS drivers.virtiopci.registerwindow rejects base mismatch`
- `TEST_PASS drivers.virtiopci.registerwindow rejects range overflow`
- `TEST_PASS drivers.virtiopci.registerwindow rejects unaligned access`
- `TEST_PASS drivers.virtiopci.registerwindow preserves no-live-mmio boundary`

Host tests cover fake register-window access execution over caller-owned BAR
window bytes:

- `TEST_PASS drivers.virtiopci.registerwindow.fakeexec writes planned word access`
- `TEST_PASS drivers.virtiopci.registerwindow.fakeexec reads planned dword access`
- `TEST_PASS drivers.virtiopci.registerwindow.fakeexec rejects missing access plan`
- `TEST_PASS drivers.virtiopci.registerwindow.fakeexec rejects short window buffer`
- `TEST_PASS drivers.virtiopci.registerwindow.fakeexec preserves no-live-mmio boundary`

Host tests cover queue setup command to register-window access planning:

- `TEST_PASS drivers.virtionet.queue.mmio.window plans setup command accesses`
- `TEST_PASS drivers.virtionet.queue.mmio.window rejects failed setup plan`
- `TEST_PASS drivers.virtionet.queue.mmio.window rejects short command source`
- `TEST_PASS drivers.virtionet.queue.mmio.window rejects short destination`
- `TEST_PASS drivers.virtionet.queue.mmio.window rejects unsupported command width`
- `TEST_PASS drivers.virtionet.queue.mmio.window propagates register window failure`
- `TEST_PASS drivers.virtionet.queue.mmio.window preserves no-live-mmio boundary`

Host tests cover queue setup register-window fake execution over caller-owned
common BAR-window bytes:

- `TEST_PASS drivers.virtionet.queue.mmio.windowexec writes planned setup accesses`
- `TEST_PASS drivers.virtionet.queue.mmio.windowexec rejects failed setup plan`
- `TEST_PASS drivers.virtionet.queue.mmio.windowexec rejects short command source`
- `TEST_PASS drivers.virtionet.queue.mmio.windowexec rejects read mismatch`
- `TEST_PASS drivers.virtionet.queue.mmio.windowexec propagates access failure`
- `TEST_PASS drivers.virtionet.queue.mmio.windowexec preserves no-live-mmio boundary`

Host tests also cover queue notification planning:

- `TEST_PASS drivers.virtionet.queue.notify plans rx notification`
- `TEST_PASS drivers.virtionet.queue.notify plans tx notification`
- `TEST_PASS drivers.virtionet.queue.notify rejects failed setup`
- `TEST_PASS drivers.virtionet.queue.notify rejects failed publish`
- `TEST_PASS drivers.virtionet.queue.notify propagates notify failure`
- `TEST_PASS drivers.virtionet.queue.notify preserves no-mmio boundary`
- `TEST_PASS drivers.virtiopci.queue.notify creates no data write plan`
- `TEST_PASS drivers.virtiopci.queue.notify rejects queue preconditions`
- `TEST_PASS drivers.virtiopci.queue.notify rejects notification data features`
- `TEST_PASS drivers.virtiopci.queue.notify rejects notify offset overflow`
- `TEST_PASS drivers.virtiopci.queue.notify rejects notify range outside aperture`
- `TEST_PASS drivers.virtiopci.queue.notify rejects invalid notify aperture`

Host tests cover queue notification register-window access planning:

- `TEST_PASS drivers.virtionet.queue.notify.window plans tx notification access`
- `TEST_PASS drivers.virtionet.queue.notify.window plans rx notification access`
- `TEST_PASS drivers.virtionet.queue.notify.window rejects failed notification plan`
- `TEST_PASS drivers.virtionet.queue.notify.window propagates register window failure`
- `TEST_PASS drivers.virtionet.queue.notify.window preserves no-live-mmio boundary`

Host tests cover queue notification register-window fake execution over
caller-owned notify BAR-window bytes:

- `TEST_PASS drivers.virtionet.queue.notify.windowexec writes planned tx notification`
- `TEST_PASS drivers.virtionet.queue.notify.windowexec writes planned rx notification`
- `TEST_PASS drivers.virtionet.queue.notify.windowexec rejects failed notification plan`
- `TEST_PASS drivers.virtionet.queue.notify.windowexec propagates register window failure`
- `TEST_PASS drivers.virtionet.queue.notify.windowexec propagates access failure`
- `TEST_PASS drivers.virtionet.queue.notify.windowexec preserves no-live-mmio boundary`

Host tests cover barrier-gated queue notification register-window fake execution
over caller-owned notify BAR-window bytes:

- `TEST_PASS drivers.virtionet.queue.notify.windowbarrier writes planned tx notification`
- `TEST_PASS drivers.virtionet.queue.notify.windowbarrier rejects missing barrier`
- `TEST_PASS drivers.virtionet.queue.notify.windowbarrier rejects failed notification plan`
- `TEST_PASS drivers.virtionet.queue.notify.windowbarrier propagates register window failure`
- `TEST_PASS drivers.virtionet.queue.notify.windowbarrier propagates access failure`
- `TEST_PASS drivers.virtionet.queue.notify.windowbarrier preserves no-live-mmio boundary`

Host tests cover RX readiness composition through queue setup and notify
register-window fake execution:

- `TEST_PASS drivers.virtionet.rx.readiness composes setup publish notify windowexec`
- `TEST_PASS drivers.virtionet.rx.readiness rejects failed setup window execution`
- `TEST_PASS drivers.virtionet.rx.readiness rejects failed publish`
- `TEST_PASS drivers.virtionet.rx.readiness rejects missing barrier`
- `TEST_PASS drivers.virtionet.rx.readiness propagates notification plan failure`
- `TEST_PASS drivers.virtionet.rx.readiness propagates notification window failure`
- `TEST_PASS drivers.virtionet.rx.readiness preserves no-live-mmio boundary`

Host tests cover RX register-window completion/classification composition:

- `TEST_PASS drivers.virtionet.rx.completion.windowexec composes readiness completion classification`
- `TEST_PASS drivers.virtionet.rx.completion.windowexec rejects failed readiness`
- `TEST_PASS drivers.virtionet.rx.completion.windowexec rejects no completion`
- `TEST_PASS drivers.virtionet.rx.completion.windowexec propagates classifier failure`
- `TEST_PASS drivers.virtionet.rx.completion.windowexec preserves no-live-mmio boundary`

Host tests cover RX register-window slab selection before classification:

- `TEST_PASS drivers.virtionet.rx.completion.slab.windowexec selects completed buffer`
- `TEST_PASS drivers.virtionet.rx.completion.slab.windowexec rejects failed readiness`
- `TEST_PASS drivers.virtionet.rx.completion.slab.windowexec rejects no completion`
- `TEST_PASS drivers.virtionet.rx.completion.slab.windowexec rejects short slab`
- `TEST_PASS drivers.virtionet.rx.completion.slab.windowexec propagates classifier failure`
- `TEST_PASS drivers.virtionet.rx.completion.slab.windowexec preserves no-live-mmio boundary`

Host tests cover RX register-window slab completion into network dispatch
metadata:

- `TEST_PASS drivers.virtionet.rx.dispatch.windowexec dispatches arp slab completion`
- `TEST_PASS drivers.virtionet.rx.dispatch.windowexec dispatches ipv4 slab completion`
- `TEST_PASS drivers.virtionet.rx.dispatch.windowexec rejects failed slab completion`
- `TEST_PASS drivers.virtionet.rx.dispatch.windowexec propagates dispatcher failure`
- `TEST_PASS drivers.virtionet.rx.dispatch.windowexec preserves no-live-mmio boundary`

Host tests cover virtio-net device-configuration register-window fake reads over
caller-owned device BAR-window bytes:

- `TEST_PASS drivers.virtionet.config.windowexec reads mac address`
- `TEST_PASS drivers.virtionet.config.windowexec reads optional status`
- `TEST_PASS drivers.virtionet.config.windowexec rejects short scratch buffer`
- `TEST_PASS drivers.virtionet.config.windowexec propagates register window failure`
- `TEST_PASS drivers.virtionet.config.windowexec propagates access failure`
- `TEST_PASS drivers.virtionet.config.windowexec propagates config parse failure`
- `TEST_PASS drivers.virtionet.config.windowexec preserves no-live-mmio boundary`

Host tests cover queue notification execution through an injected fake sink:

- `TEST_PASS drivers.virtionet.queue.notify.exec writes planned tx notification`
- `TEST_PASS drivers.virtionet.queue.notify.exec writes planned rx notification`
- `TEST_PASS drivers.virtionet.queue.notify.exec rejects missing barrier`
- `TEST_PASS drivers.virtionet.queue.notify.exec rejects failed notification plan`
- `TEST_PASS drivers.virtionet.queue.notify.exec propagates sink failure`
- `TEST_PASS drivers.virtionet.queue.notify.exec preserves no-live-mmio boundary`

Host tests also cover queue notification execution through the fake register
accessor adapter:

- `TEST_PASS drivers.virtionet.queue.notify.accessor writes planned tx notification`
- `TEST_PASS drivers.virtionet.queue.notify.accessor propagates range failure`
- `TEST_PASS drivers.virtionet.queue.notify.accessor propagates write failure`
- `TEST_PASS drivers.virtionet.queue.notify.accessor preserves no-live-mmio boundary`

Host tests cover queue setup command execution through the fake register
accessor:

- `TEST_PASS drivers.virtionet.queue.mmio.exec writes planned commands`
- `TEST_PASS drivers.virtionet.queue.mmio.exec writes planned rx tx setup`
- `TEST_PASS drivers.virtionet.queue.mmio.exec rejects failed setup plan`
- `TEST_PASS drivers.virtionet.queue.mmio.exec rejects short command source`
- `TEST_PASS drivers.virtionet.queue.mmio.exec rejects read mismatch`
- `TEST_PASS drivers.virtionet.queue.mmio.exec propagates accessor failure`
- `TEST_PASS drivers.virtionet.queue.mmio.exec propagates read failure`
- `TEST_PASS drivers.virtionet.queue.mmio.exec propagates write failure`
- `TEST_PASS drivers.virtionet.queue.mmio.exec rejects missing accessor`
- `TEST_PASS drivers.virtionet.queue.mmio.exec preserves no-live-mmio boundary`

Host tests cover TX used-ring completion/reclaim planning:

- `TEST_PASS drivers.virtionet.tx.completion accepts matching used entry`
- `TEST_PASS drivers.virtionet.tx.completion accepts executed notification`
- `TEST_PASS drivers.virtionet.tx.completion advances wrapped used index`
- `TEST_PASS drivers.virtionet.tx.completion reports no completion`
- `TEST_PASS drivers.virtionet.tx.completion rejects unexpected used index`
- `TEST_PASS drivers.virtionet.tx.completion rejects wrong descriptor head`
- `TEST_PASS drivers.virtionet.tx.completion rejects unexpected byte count`
- `TEST_PASS drivers.virtionet.tx.completion rejects short queue memory`
- `TEST_PASS drivers.virtionet.tx.completion rejects failed publish`
- `TEST_PASS drivers.virtionet.tx.completion rejects failed notification`
- `TEST_PASS drivers.virtionet.tx.completion rejects failed notification execution`
- `TEST_PASS drivers.virtionet.tx.completion rejects wrong executed notification kind`
- `TEST_PASS drivers.virtionet.tx.completion rejects executed notification queue mismatch`
- `TEST_PASS drivers.virtionet.tx.completion rejects executed notification index mismatch`
- `TEST_PASS drivers.virtionet.tx.completion accepts register-window notification`
- `TEST_PASS drivers.virtionet.tx.completion rejects failed register-window notification`
- `TEST_PASS drivers.virtionet.tx.completion rejects wrong register-window notification kind`
- `TEST_PASS drivers.virtionet.tx.completion rejects register-window notification queue mismatch`
- `TEST_PASS drivers.virtionet.tx.completion rejects register-window notification index mismatch`
- `TEST_PASS drivers.virtionet.tx.completion preserves register-window notification boundary`
- `TEST_PASS drivers.virtionet.tx.completion preserves no-mmio boundary`
- `TEST_PASS drivers.virtionet.tx.completion preserves executed-notification boundary`

Host tests cover TX descriptor ownership state-table transitions:

- `TEST_PASS drivers.virtionet.tx.ownership marks published descriptors in flight`
- `TEST_PASS drivers.virtionet.tx.ownership reclaims completed descriptors`
- `TEST_PASS drivers.virtionet.tx.ownership rejects failed publish`
- `TEST_PASS drivers.virtionet.tx.ownership rejects failed completion`
- `TEST_PASS drivers.virtionet.tx.ownership rejects short state table`
- `TEST_PASS drivers.virtionet.tx.ownership rejects descriptor already in flight`
- `TEST_PASS drivers.virtionet.tx.ownership rejects descriptor not in flight`
- `TEST_PASS drivers.virtionet.tx.ownership rejects duplicate completion`
- `TEST_PASS drivers.virtionet.tx.ownership preserves no-mmio boundary`

Host tests also cover read-only TX free-list selection over the ownership state
table:

- `TEST_PASS drivers.virtionet.tx.freelist selects first free pair`
- `TEST_PASS drivers.virtionet.tx.freelist skips in-flight descriptors`
- `TEST_PASS drivers.virtionet.tx.freelist reuses reclaimed descriptors`
- `TEST_PASS drivers.virtionet.tx.freelist reports capacity exhaustion`
- `TEST_PASS drivers.virtionet.tx.freelist rejects invalid queue state`
- `TEST_PASS drivers.virtionet.tx.freelist preserves no-mmio boundary`

Host tests also cover TX publish-plan composition:

- `TEST_PASS drivers.virtionet.tx.publishplan publishes selected free pair`
- `TEST_PASS drivers.virtionet.tx.publishplan skips in-flight descriptors`
- `TEST_PASS drivers.virtionet.tx.publishplan rejects free-list exhaustion`
- `TEST_PASS drivers.virtionet.tx.publishplan preserves state on publish failure`
- `TEST_PASS drivers.virtionet.tx.publishplan rejects invalid ownership state`
- `TEST_PASS drivers.virtionet.tx.publishplan preserves no-mmio boundary`

Host tests also cover TX readiness composition:

- `TEST_PASS drivers.virtionet.tx.readiness composes setup publish notify`
- `TEST_PASS drivers.virtionet.tx.readiness rejects failed setup execution`
- `TEST_PASS drivers.virtionet.tx.readiness rejects failed publish`
- `TEST_PASS drivers.virtionet.tx.readiness rejects missing barrier`
- `TEST_PASS drivers.virtionet.tx.readiness propagates notification failure`
- `TEST_PASS drivers.virtionet.tx.readiness preserves no-live-mmio boundary`

Host tests also cover TX register-window readiness composition:

- `TEST_PASS drivers.virtionet.tx.readiness.windowexec composes setup publish notify`
- `TEST_PASS drivers.virtionet.tx.readiness.windowexec rejects failed setup window execution`
- `TEST_PASS drivers.virtionet.tx.readiness.windowexec rejects failed publish`
- `TEST_PASS drivers.virtionet.tx.readiness.windowexec rejects missing barrier`
- `TEST_PASS drivers.virtionet.tx.readiness.windowexec propagates notification plan failure`
- `TEST_PASS drivers.virtionet.tx.readiness.windowexec propagates notification window failure`
- `TEST_PASS drivers.virtionet.tx.readiness.windowexec preserves no-live-mmio boundary`

Host tests also cover TX register-window completion/reclaim composition:

- `TEST_PASS drivers.virtionet.tx.reclaim.windowexec composes readiness completion ownership`
- `TEST_PASS drivers.virtionet.tx.reclaim.windowexec rejects failed readiness`
- `TEST_PASS drivers.virtionet.tx.reclaim.windowexec rejects no completion`
- `TEST_PASS drivers.virtionet.tx.reclaim.windowexec rejects ownership mismatch`
- `TEST_PASS drivers.virtionet.tx.reclaim.windowexec preserves no-live-mmio boundary`

## Known Limitations

- No BAR mapping.
- No volatile MMIO accessor exists.
- BAR register-window planning validates caller-provided mapping metadata and
  computes future register addresses only. It does not create page mappings,
  change cacheability attributes, perform volatile access, or read/write live
  registers.
- BAR sizing planning consumes caller-owned all-ones sizing readback snapshots
  only. BAR sizing probe planning records the ordered write-all-ones,
  read-sizing-readback, and restore-original commands that a future PCI config
  accessor must execute. BAR sizing probe address planning binds those commands
  to checked ECAM config-space addresses for a known segment and BDF. BAR
  sizing probe fake execution exercises those address-bound commands through an
  injected config-accessor test double and captures caller-owned readbacks, but
  still stops before live accessor execution. BAR sizing probe derived-size
  planning feeds those fake-captured readbacks through the existing sizing
  planner for host tests only. None of these paths executes the live PCI
  config-space write/read/restore sequence, maps ECAM or BARs, or proves that a
  live device accepted the sizing probe.
- Virtio PCI binding from fake-derived BAR sizes is host-tested metadata
  composition only. The BAR sizes carry fake-executor provenance, not live
  hardware provenance, and the path does not map ECAM/BARs, perform volatile
  MMIO, bind a live network driver, transfer packets, or prove cloud, hosted
  .NET 10, ASP.NET Minimal API, or Kestrel readiness.
- Virtio PCI binding cataloging from fake-derived BAR sizes is host-tested
  metadata composition only over caller-provided candidates, configuration
  snapshots, fake accessors, and caller-owned scratch buffers. It does not
  enumerate live PCI devices, size BARs on hardware, map ECAM/BARs, perform
  volatile MMIO, bind a live network driver, transfer packets, or prove cloud,
  hosted .NET 10, ASP.NET Minimal API, or Kestrel readiness.
- Virtio descriptor cataloging from fake-derived binding provenance consumes
  caller-provided device configuration, feature bits, and queue counts only. It
  does not read a live device configuration, write status registers, negotiate
  features with hardware, configure queues, bind a live network driver,
  transfer packets, or prove cloud, hosted .NET 10, ASP.NET Minimal API, or
  Kestrel readiness.
- Fake register-window execution reads and writes caller-owned byte spans only.
  It does not dereference planned physical/virtual addresses, perform volatile
  access, or prove a live BAR-backed accessor.
- Device-configuration register-window fake reads copy from caller-owned device
  BAR-window bytes into caller-owned scratch storage only. They do not read a
  live device, stabilize config generation, negotiate features, or prove live
  virtio-net discovery.
- Queue setup register-window planning maps setup commands to caller-owned
  access-plan metadata only. It does not configure a live queue or execute fake
  or live register operations.
- Queue setup register-window fake execution uses caller-owned common BAR-window
  bytes and staged read values only. It does not configure a live queue,
  implement queue-select-dependent hardware behavior, execute memory barriers,
  or prove live MMIO.
- Queue notification register-window planning maps successful notification plans
  to caller-owned notify-aperture access-plan metadata only. It does not execute
  fake or live notification writes, memory barriers, or live device
  notification.
- Queue notification register-window fake execution writes only caller-owned
  notify BAR-window bytes through the generic fake byte-span executor. It does
  not execute memory barriers, map BARs, dereference physical addresses, or
  notify a live device.
- Barrier-gated queue notification register-window fake execution requires
  explicit readiness metadata before the fake byte-span write, but it still does
  not execute a real memory-ordering primitive or notify a live device.
- RX readiness composition uses caller-owned queue memory, common BAR-window
  bytes, and notify BAR-window bytes only. It does not configure a live queue,
  execute a real memory barrier, notify a live device, or prove packet
  reception. RX register-window completion/classification/dispatch composition
  selects only caller-owned used-ring and receive-buffer-slab snapshots through
  the existing lifecycle classifier and network dispatcher; it does not process
  live completions, mutate network state, or prove live receive evidence.
- Queue setup and notification execution use only injected fake accessors,
  injected fake sinks, and caller-owned test storage.
- No DMA-safe page allocator or physical address translator exists.
- No live virtio-net queue is configured.
- Barrier readiness is explicit metadata only; no real memory barrier primitive
  is executed in the queue publish-to-notify sequence.
- No interrupt controller or MSI-X support is present.
- TX completion planning reads caller-owned test memory only after successful fake-sink or register-window notification execution provenance; it does not prove hardware observed the notification, wait for live hardware, drain multiple completions, or reclaim descriptors into an allocator.
- TX descriptor ownership planning mutates only caller-owned `Free`/`InFlight` state entries for the published header/frame descriptors. The TX free-list planner reads that state to select the first contiguous free pair and to report capacity exhaustion; the TX publish-plan composer uses that pair to write caller-owned queue memory/header bytes through the existing lifecycle path and marks only selected descriptors in-flight after successful publication; the TX readiness planner composes that fake-accessor setup/publication/barrier/notification path; the TX register-window readiness planner composes the common BAR-window setup plus notify BAR-window notification path for the same caller-owned TX publication; and the TX register-window reclaim planner composes that readiness with one caller-owned used-ring completion and descriptor-state reclaim. The RX readiness planner composes the register-window setup/publication/barrier/notification path for caller-owned RX descriptors. These are not live virtio-net RX/TX, not a full runtime allocator, and not live device notification/completion/reclaim evidence.
- No QEMU packet transmission or reception evidence is claimed.
- No hosted .NET 10, ASP.NET Minimal API, or Kestrel evidence is claimed.

## Next Steps

1. Implement the live PCI config-space accessor/executor that consumes the fake-executed, address-bound planned BAR sizing write/read/restore contracts.
2. Define the live BAR-backed volatile register accessor and MMIO ownership rules needed to consume the register-window, queue setup, and queue notification access plans and replace the fake RX/TX byte-span/accessor paths.
3. Replace explicit barrier-readiness metadata with a real memory-ordering primitive that is safe for the kernel path.
4. Only after memory mapping, volatile access, DMA ownership, and ordering exist, wire the register plans to a real BAR-backed accessor and prove one notification/completion/reclaim path in QEMU.
