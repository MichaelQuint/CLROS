# ENA Driver Plan

## Current Status

OS has host-tested Ethernet, ARP, IPv4, UDP, DHCP, DNS, TCP, HTTP planning,
PAL socket mocks, PCI snapshot parsers, BAR metadata parsing, virtio-net
queue/header/lifecycle planning, ENA PCI candidate metadata,
register-map/admin-readiness metadata, an ENA admin command execution boundary
planner, and an ENA DMA address / queue-memory capability planner. It does not
have a live network device driver, live PCI ECAM reader, BAR mapping, MSI-X
setup, DMA allocator, ENA admin queue, ENA TX/RX queues, DHCP over ENA, TCP
over ENA, hosted .NET 10, or Kestrel evidence.

Issue #244 added host-tested ENA admin descriptor constants and caller-buffer
helpers for `GET_FEATURE DEVICE_ATTRIBUTES` command encoding plus ACQ completion
header parsing. Default/uninitialized completion entries are rejected before
successful validation so zeroed caller state cannot masquerade as a valid ACQ
entry. This is still not a live ENA admin queue: it performs no DMA
allocation, MMIO register writes, doorbells, polling, interrupts, or command
execution.

Issue #255 added `EnaAdminCommandPlan`, a host-tested execution-boundary planner
for `GET_FEATURE DEVICE_ATTRIBUTES`. It records the command opcode, feature ID,
command ID, expected completion command ID, expected phase, and timeout budget;
writes the planned AQ descriptor into caller-owned memory; and validates a
caller-provided ACQ/device-attributes response through the existing descriptor
and device-attributes parsers. It still performs no BAR reads/writes, MMIO, DMA
allocation, doorbells, MSI/MSI-X setup, interrupts, packet I/O, or real network
I/O.

Issue #257 added `EnaDmaAddressPlan`, a host-tested capability planner that
validates caller-provided physical DMA regions against parsed ENA
device-attribute address-width metadata, alignment, length, and arithmetic
overflow constraints. It also derives Admin Queue and Admin Completion Queue
physical regions from a successful `EnaAdminQueueLayoutPlanResult`. It still
performs no DMA allocation, page mapping, IOMMU programming, BAR writes,
doorbells, interrupts, packet I/O, or real network I/O.

Issue #258 added `EnaDataQueueDescriptors`, a host-tested caller-buffer contract
for ENA TX submission descriptors, RX submission descriptors, TX completions,
and RX completions. It validates descriptor sizes, length fields, 48-bit buffer
addresses, request IDs, phase bits, queue ID matches, reserved bits, completion
status, and the first/last/buffer flags needed by the first single-buffer RX
path. Default/uninitialized TX completion entries are rejected before successful
validation. It still performs no queue creation, DMA allocation, page mapping,
MMIO, doorbells, MSI-X setup, packet parsing, packet transmit, packet receive,
DHCP, TCP, HTTP, hosted .NET, or Kestrel behavior.

ENA is the AWS Nitro network-device path for this project. It is not
virtio-net, not Azure NetVSC, and not a generic Linux compatibility layer.
Reference source may be used to understand register and descriptor contracts,
but first-party kernel, std, and app source must remain C#.

## Decision

Implement ENA in small host-testable slices before any live EC2 packet I/O:

1. PCI candidate constants and discovery metadata.
2. Admin queue descriptor and completion descriptor parsing/writing.
3. Admin queue memory layout planning.
4. Admin completion parsing for `GET_FEATURE` responses.
5. Admin command execution-boundary planning over caller-owned buffers.
6. DMA address and queue-memory capability planning over caller-provided
   physical region facts.
7. TX/RX data-queue descriptor layout parsing and writing over caller-owned
   byte buffers.
8. Live BAR/MSI-X/DMA integration only after the platform prerequisites exist.
9. TX/RX queue creation and frame send/receive only after the admin queue can
   discover MAC address, queue limits, MTU, address width, and required feature
   boundaries.

No DHCP, DNS, TCP, HTTP, hosted .NET 10, ASP.NET Minimal API, or Kestrel claim
can be made from this ADR or from admin-queue-only code. Those claims require
live ENA Ethernet frame receive/transmit integrated into the existing packet
stack and then separately proven at each protocol layer.

## Reference Facts

- AWS enhanced networking uses SR-IOV. AWS documents ENA as the enhanced
  networking path, and all Nitro-based instances use ENA.
- AWS documents that an AMI must include the required ENA driver before ENA can
  be used for enhanced networking.
- The official AWS driver repository publishes ENA Linux, FreeBSD, and DPDK
  drivers.
- The Linux ENA register definitions expose a small MMIO register set that
  includes version/capability registers, admin queue base/caps, admin
  completion queue base/caps, admin queue doorbell, asynchronous event queue
  registers, device control/status, and MMIO read response registers.
- The ENA management path uses an Admin Queue and Admin Completion Queue. The
  command set includes create/destroy submission/completion queues,
  get/set-feature, and get-statistics operations.
- The ENA data path uses TX and RX submission queues with associated completion
  queues. Queue rings are descriptor rings in contiguous physical memory.
- The `GET_FEATURE DEVICE_ATTRIBUTES` response includes device feature and
  capability metadata, physical and virtual address width, unicast MAC address,
  and max MTU.
- Public ENA device IDs in the official AWS driver table include Amazon vendor
  `0x1d0f`, ENA PF `0x0ec2`, ENA LLQ PF `0x1ec2`, ENA VF `0xec20`, ENA LLQ VF
  `0xec21`, and reserved device `0x0051`. The first EC2 Nitro target should
  bind only the ENA VF path unless later evidence requires PF support.

References:

- https://docs.aws.amazon.com/AWSEC2/latest/UserGuide/enhanced-networking.html
- https://docs.aws.amazon.com/AWSEC2/latest/UserGuide/enhanced-networking-ena.html
- https://docs.aws.amazon.com/AWSEC2/latest/UserGuide/ena-queues.html
- https://github.com/amzn/amzn-drivers
- https://codebrowser.dev/linux/linux/drivers/net/ethernet/amazon/ena/ena_regs_defs.h.html
- https://codebrowser.dev/linux/linux/drivers/net/ethernet/amazon/ena/ena_admin_defs.h.html
- https://codebrowser.dev/linux/linux/drivers/net/ethernet/amazon/ena/ena_eth_io_defs.h.html
- https://static.dpdk.org/doc/guides/nics/ena.html

## PCI Discovery Prerequisites

The ENA candidate path starts only after the platform layer can produce stable
PCI function snapshots:

- ACPI RSDP/RSDT/XSDT/MCFG discovery must provide ECAM segments.
- A live ECAM reader must read at least PCI type-0 configuration headers.
- The scanner must preserve segment, bus, device, function, vendor ID, device
  ID, class, subclass, programming interface, command/status registers, BAR raw
  values, and capability-list pointer.
- BAR decoding and sizing must identify the ENA register BAR and any optional
  low-latency queue memory BAR without mapping or writing them in the parser.
- PCI command programming must enable memory space and bus mastering only after
  BAR and DMA readiness checks pass.
- MSI-X capability parsing must locate the MSI-X table and PBA BAR metadata
  before any interrupt-backed queue path is enabled.
- The first candidate parser must not assume a fixed Nitro BDF. It must match
  by Amazon vendor/device IDs and preserve PCI location as diagnostic metadata.

Host-testable first slice:

```text
EnaPciCandidate
  input: caller-provided PCI scan record or first-64-byte config snapshot
  output: candidate kind, vendor/device match, PCI location, requires BAR/MSI-X/DMA
  no ECAM reads, no BAR maps, no config writes, no interrupts
```

Suggested markers:

```text
TEST_PASS drivers.ena.pci identifies vf candidate
TEST_PASS drivers.ena.pci identifies llq vf candidate
TEST_PASS drivers.ena.pci rejects non amazon vendor
TEST_PASS drivers.ena.pci rejects unknown amazon device
TEST_PASS drivers.ena.pci preserves pci location
```

## Admin Queue Boundary

The admin queue is the management channel. It is responsible for:

- Reset/readiness negotiation through device control/status registers.
- Programming Admin Queue and Admin Completion Queue physical base addresses.
- Publishing AQ/ACQ depth and entry-size metadata through MMIO registers.
- Submitting management commands through AQ entries.
- Ringing the admin queue doorbell.
- Parsing ACQ completions by command ID, phase, status, extended status, and
  submission-queue head index while rejecting default/uninitialized entries.
- Issuing `GET_FEATURE` commands for device attributes, max queues, hardware
  hints, LLQ capabilities, offload configuration, link configuration, and AENQ
  configuration as the implementation grows.
- Creating and destroying I/O completion queues and TX/RX submission queues.
- Configuring asynchronous event notifications after basic admin completion
  handling exists.

The first implementation must be pollable from a host test over caller-owned
byte buffers. It should not allocate DMA memory, write MMIO, or wait on real
interrupts. It should expose constants and parsers that later live code can
reuse without changing byte layout.

Host-testable descriptor slice:

```text
EnaAdminAqCommonDescriptor
EnaAdminAqEntry
EnaAdminAcqCommonDescriptor
EnaAdminAcqEntry
EnaAdminGetFeatureCommand
EnaAdminGetFeatureResponse
```

First command/response scope:

- `GET_FEATURE DEVICE_ATTRIBUTES`
- `GET_FEATURE MAX_QUEUES_NUM` or `MAX_QUEUES_EXT`
- `GET_FEATURE HW_HINTS` only as parsed metadata, not as timeout policy
- completion statuses: success, resource allocation failure, bad opcode,
  unsupported opcode, malformed request, illegal parameter, unknown error,
  resource busy

Suggested markers:

```text
TEST_PASS drivers.ena.admin.desc writes get feature command
TEST_PASS drivers.ena.admin.desc parses successful completion
TEST_PASS drivers.ena.admin.desc reports command id mismatch
TEST_PASS drivers.ena.admin.desc reports completion status
TEST_PASS drivers.ena.admin.desc rejects default completion entry
TEST_PASS drivers.ena.admin.desc parses device attributes
```

## Admin Command Execution Boundary Planner

`EnaAdminCommandPlan` is the first boundary between descriptor encoding and
future live command execution. It is intentionally still a planner: host tests
provide caller-owned submission and completion buffers, and the code does not
read or write ENA registers, allocate DMA memory, ring the admin doorbell, poll
hardware, configure MSI/MSI-X, or move packets.

Current scope:

- plan `GET_FEATURE DEVICE_ATTRIBUTES`;
- preserve opcode, feature ID, command ID, expected completion command ID,
  submission phase, expected completion phase, descriptor sizes, and timeout
  tick budget;
- reject command IDs outside ENA's 12-bit command-ID mask;
- reject unsupported feature IDs until their response parsers exist;
- reject zero or unreasonably small timeout budgets using
  `EnaAdminCommandPlanStatus.TimeoutBudgetTooSmall`;
- write the planned submission descriptor through `EnaAdminDescriptors`;
- validate caller-provided `DEVICE_ATTRIBUTES` completions through
  `EnaDeviceAttributesParser`;
- return status-coded failures for short submission buffers, short completion
  buffers, command-ID mismatch, phase mismatch, non-success completion status,
  and rejected device-attribute payloads.

Host-tested markers:

```text
TEST_PASS drivers.ena.admin.command plans get feature
TEST_PASS drivers.ena.admin.command validates completion
TEST_PASS drivers.ena.admin.command rejects timeout budget
TEST_PASS drivers.ena.admin.command rejects invalid inputs
TEST_PASS drivers.ena.admin.command rejects completion failures
```

Next live steps remain outside this boundary. A future command executor must
take a successful plan, DMA-backed AQ/ACQ memory, mapped register apertures,
ownership barriers, a doorbell writer, and a bounded completion wait policy
before any result can be treated as hardware evidence.

## Admin Queue Memory Layout Planner

ENA admin queue memory is a prerequisite for live management commands. The
current host-tested slice is a planner only: `EnaAdminQueueLayoutPlan`
calculates Admin Queue and Admin Completion Queue offsets, lengths, total
required bytes, base alignment, memory-budget diagnostics, contiguous-memory
intent, and initial phase/index metadata without allocating DMA memory or
touching ENA registers.

Inputs:

- AQ depth from device register/caller metadata.
- AQ entry size from device register/caller metadata.
- ACQ depth from device register/caller metadata.
- ACQ entry size from device register/caller metadata.
- Required alignment for physical base addresses.
- Caller-provided maximum memory budget.
- Whether the device capability requires contiguous queue memory.

Outputs:

- AQ offset, length, entry count, entry size.
- ACQ offset, length, entry count, entry size.
- Total required bytes.
- Alignment and overflow diagnostics.
- Expected initial phase bits and producer/consumer indexes.

The planner does not allocate pages, map physical memory, write base registers,
ring doorbells, poll a real completion, or configure AENQ. Like the existing
virtio planners, it produces deterministic status-coded failures for invalid
depths, invalid entry sizes, alignment failures, destination-capacity failures,
and arithmetic overflow.

Suggested markers:

```text
TEST_PASS drivers.ena.admin.queue plans aq acq layout
TEST_PASS drivers.ena.admin.queue rejects invalid depth
TEST_PASS drivers.ena.admin.queue rejects invalid entry size
TEST_PASS drivers.ena.admin.queue rejects insufficient memory
TEST_PASS drivers.ena.admin.queue reports arithmetic overflow
```

## DMA Address And Queue-Memory Capability Planner

`EnaDmaAddressPlan` is a host-tested validation layer between parsed ENA device
attributes and future DMA-safe queue allocation. It consumes caller-provided
physical-address facts and a successful admin queue layout; it does not allocate
pages, map memory, program an IOMMU, write BAR registers, ring doorbells,
configure interrupts, or move packets.

Inputs:

- Parsed `EnaDeviceAttributes.PhysicalAddressWidth`.
- Caller-provided physical base address, byte length, and alignment.
- A successful `EnaAdminQueueLayoutPlanResult` when validating Admin Queue and
  Admin Completion Queue memory.

Outputs:

- Explicit status for invalid address width, zero length, invalid alignment,
  unaligned base address, `base + length` overflow, address-width overflow,
  rejected queue layout, and insufficient queue memory.
- Maximum supported DMA address for the device-reported physical address width.
- End-exclusive physical address for valid generic regions.
- Planned contiguous admin queue memory plus derived AQ and ACQ physical
  regions when the layout and caller memory facts are valid.

Host-tested markers:

```text
TEST_PASS drivers.ena.dma validates address width
TEST_PASS drivers.ena.dma rejects overflow
TEST_PASS drivers.ena.dma validates queue regions
TEST_PASS drivers.ena.dma rejects invalid regions
```

This planner is intentionally not a DMA allocator. Live ENA initialization still
needs page allocation, physical mapping ownership, cache/memory ordering rules,
IOMMU policy if present, and register programming before any queue can be used
by hardware.

## BAR, MSI-X, And DMA Prerequisites

Live ENA initialization must wait for these platform pieces:

- BAR sizing and mapping for the ENA register BAR.
- Typed register offsets for `VERSION`, `CONTROLLER_VERSION`, `CAPS`,
  `CAPS_EXT`, `AQ_BASE`, `AQ_CAPS`, `ACQ_BASE`, `ACQ_CAPS`, `AQ_DB`,
  `ACQ_TAIL`, `AENQ_*`, `INTR_MASK`, `DEV_CTL`, `DEV_STS`, and MMIO read
  response registers.
- PCI command register writes for memory-space enable and bus-master enable.
- MSI-X table/PBA discovery, vector allocation, interrupt mask/unmask, and
  queue-to-vector assignment.
- DMA-safe contiguous page allocation with physical addresses that satisfy the
  device-reported DMA/address-width limits.
- Memory-ordering rules around descriptor writes, base-register writes,
  doorbells, completion polling, and interrupt acknowledgement.
- Reset and timeout policy that reports `TEST_FAIL` diagnostics instead of
  hanging or silently retrying forever.

The admin queue may use polling as a first live bring-up step, but data queues
must not claim production behavior until MSI-X, queue completion handling, and
reset/error paths are represented.

### BAR And MSI-X Resource Snapshot Planner

Issue #256 adds a host-testable resource planner in `EnaPciResourcePlan`. It
accepts caller-provided PCI discovery facts: an ENA PCI candidate snapshot,
decoded BAR descriptors, caller-supplied BAR aperture sizes, and MSI-X
capability metadata. It validates that the ENA candidate check passes before
resource planning succeeds, requires a usable MMIO register BAR, checks base
address, aperture size, power-of-two sizing, alignment, and minimum ENA
register aperture length, and validates MSI-X table/PBA regions against their
referenced BAR apertures.

The same slice includes `PciMsixCapabilityParser`, a caller-buffer parser for
standard PCI MSI-X capability records. It walks a supplied configuration-space
snapshot, finds capability ID `0x11`, extracts table size, table BIR/offset,
PBA BIR/offset, function-mask, and enable metadata, and rejects malformed
capability pointers, loops, truncation, missing MSI-X, and invalid BAR
indicators.

This is still metadata-only. It performs no live PCI config reads or writes, no
BAR sizing writes, no BAR mapping, no MSI-X enable, no interrupt allocation, no
DMA allocation, no ENA reset, no queue creation, and no packet I/O.

## TX/RX Queue Boundary

TX/RX queues are data path queues. They are responsible for moving Ethernet
frames, not for device discovery or device configuration.

### TX/RX Descriptor Layout Contract

`EnaDataQueueDescriptors` is the first host-tested data-path descriptor slice.
It writes and reads ENA TX/RX descriptor bytes from caller-owned spans and parses
TX/RX completion bytes from caller-provided buffers. The contract is deliberately
limited to the no-offload, single-buffer baseline that later live TX/RX queue
bring-up can reuse without changing descriptor byte layout.

Current scope:

- TX submission descriptors:
  - 16-byte descriptor size;
  - request ID split across the high/low request-ID fields;
  - buffer length;
  - 48-bit physical buffer address split into low/high words;
  - phase, first, last, and completion-request flags;
  - reserved/control-bit validation and unsupported offload metadata rejection.
- RX submission descriptors:
  - 16-byte descriptor size;
  - request ID;
  - receive-buffer length, including the ENA zero-encoded 64 KiB case;
  - 48-bit physical buffer address split into low/high words;
  - phase, first, last, completion-request, and required must-be-one control
    bit validation.
- TX completions:
  - request ID, status, phase, submission queue ID, and SQ head index parsing;
  - status, phase, queue ID, request-ID, and default/uninitialized validation.
- RX completions:
  - status flags, frame length, request ID, hash, submission queue ID, and
    offset parsing;
  - reserved-bit validation;
  - first/last/buffer flag validation for the first single-buffer RX path.

Host-tested markers:

```text
TEST_PASS drivers.ena.data.desc writes tx descriptor
TEST_PASS drivers.ena.data.desc writes rx descriptor
TEST_PASS drivers.ena.data.desc parses completions
TEST_PASS drivers.ena.data.desc rejects malformed buffers
TEST_PASS drivers.ena.data.desc rejects completion failures
TEST_PASS drivers.ena.data.desc rejects default tx completion
```

This contract does not allocate DMA buffers, publish queue memory to hardware,
create ENA I/O queues, ring doorbells, process interrupts, parse Ethernet/IP
payloads, or move packets.

TX responsibilities:

- Accept a caller-owned Ethernet frame already produced by the packet stack.
- Publish ENA TX descriptors that reference the frame buffer.
- Request completion according to the selected completion policy.
- Ring the queue doorbell returned by admin queue creation.
- Parse TX completions by request ID, status, phase, queue ID, and SQ head.
- Reclaim caller-owned buffers only after completion.

RX responsibilities:

- Publish device-writable receive buffers.
- Replenish RX descriptors after completions.
- Parse RX completions, validate first/last/buffer/status bits, frame length,
  request ID, and offset metadata.
- Expose only the Ethernet frame span to upper network layers.
- Treat checksum/offload status as metadata. Do not trust offload until
  negotiated and host-tested.

The first data path should be one TX queue and one RX queue for one ENI. RSS,
multi-queue scaling, LLQ, timestamping, and advanced offloads remain later
features.

## MAC And Configuration Discovery Boundary

The driver must not synthesize a MAC address from PCI metadata. The MAC address
boundary is `GET_FEATURE DEVICE_ATTRIBUTES` over a working admin queue.

The first host-tested parser should:

- Extract the six-byte unicast MAC address from the device-attributes response.
- Reject all-zero and broadcast MAC addresses.
- Preserve max MTU, physical address width, virtual address width, supported
  feature bitmap, and capability bitmap.
- Record the nested completion status and feature ID.

Live configuration discovery later adds:

- Max TX/RX SQ and CQ counts/depths.
- Max per-packet descriptors.
- Device offload features and selected no-offload baseline.
- Link configuration and AENQ link-change behavior.
- Hardware hints as bounded timeout recommendations, not silent policy.

## Network Stack Integration

ENA RX/TX should integrate at the Ethernet frame boundary:

- RX completion produces a caller-owned buffer slice containing an Ethernet II
  frame.
- That Ethernet frame is passed into the existing receive classifiers and
  dispatchers such as `EthernetHeader`, `NetworkReceiveDispatcher`, and
  `UdpReceiveDispatcher`.
- ARP, IPv4, ICMP, UDP, DHCP, DNS, TCP, and HTTP planners remain device-neutral
  above the frame boundary.
- TX callers such as UDP/TCP frame planners write Ethernet frames into caller
  buffers. ENA TX publishes those buffers directly through ENA descriptors.
- Unlike virtio-net, ENA must not prepend or parse a virtio-net header.
- Until ENA RX and TX are live, DHCP lease acquisition over ENA, TCP listeners,
  HTTP/Kestrel probes, and EC2 network readiness are unsupported.

## Non-Goals

The first ENA track explicitly does not include:

- ENA Express or ENA SRD.
- Multiple ENIs.
- IPv6.
- Graviton/ARM64.
- Xen compatibility.
- Live EC2 packet I/O.
- DHCP, DNS, TCP, HTTP, ASP.NET Minimal API, or Kestrel evidence.
- AWS SDK calls.
- IMDSv2, user-data, IAM credentials, SSM, CloudWatch, or cloud-init.
- RSS, multi-queue scaling, LLQ, XDP, hardware timestamping, checksum offload,
  TSO, or jumbo-frame enablement.

## Follow-Up Issue Slices

Small issue slices created from this ADR:

1. #243 - Add ENA PCI candidate constants and host tests.
   - Files: `src/OS.Kernel/Drivers/EnaPciCandidate.cs`,
     `tests/OS.HostTests/EnaPciCandidateTests.cs`.
   - Acceptance: candidate constants are centralized; host tests prove VF and
     LLQ VF matches; no BAR mapping, MSI-X, DMA, admin queue, or packet I/O.
   - Status: implemented as a metadata-only candidate selector and validator
     over caller-provided `PciEcamScannedDevice` records. It preserves PCI
     source/location metadata and intentionally accepts only the ENA VF and LLQ
     VF baseline.

2. #244 - Add ENA admin queue descriptor structs and host tests.
   - Files: `src/OS.Kernel/Drivers/EnaAdminDescriptors.cs`,
     `tests/OS.HostTests/EnaAdminDescriptorsTests.cs`.
   - Acceptance: command/completion descriptors round-trip over caller-owned
     spans; status/phase/command ID failures are deterministic; no MMIO or DMA.

3. #245 - Add ENA admin queue memory layout planner.
   - Files: `src/OS.Kernel/Drivers/EnaAdminQueueLayoutPlan.cs`,
     `tests/OS.HostTests/EnaAdminQueueLayoutPlanTests.cs`.
   - Acceptance: AQ/ACQ layout validates depth, entry size, alignment, memory
     budget, and overflow; no allocation, BAR writes, doorbells, or polling.

4. #246 - Add ENA admin completion parser for device attributes.
   - Files: `src/OS.Kernel/Drivers/EnaDeviceAttributes.cs`,
     `tests/OS.HostTests/EnaDeviceAttributesTests.cs`.
   - Acceptance: parses MAC, max MTU, address widths, features, and
     capabilities from caller-provided completion bytes; rejects invalid MACs
     and malformed completions; no live admin command submission.
   - Status: implemented as a caller-buffer parser over 64-byte
     `GET_FEATURE DEVICE_ATTRIBUTES` response bytes. It validates the ACQ
     completion header, command ID, phase, non-success completion status,
     address-width bounds, max MTU, all-zero MAC, and broadcast MAC before
     exposing implementation ID, device version, supported feature bitmap,
     capability bitmap, MAC address, address widths, and max MTU.

5. #247 - Add ENA register map and admin-queue readiness planner.
   - Files: `src/OS.Kernel/Drivers/EnaRegisterMap.cs`,
     `tests/OS.HostTests/EnaRegisterMapTests.cs`.
   - Acceptance: register offsets, caps/status bits, base-register spans,
     admin aperture validation, and metadata-only readiness rejection are
     centralized and host-tested; no BAR mapping or hardware access.

6. #256 - Add ENA PCI BAR and MSI-X resource snapshot parser.
   - Files: `src/OS.Kernel/Drivers/EnaPciResourcePlan.cs`,
     `tests/OS.HostTests/EnaPciResourcePlanTests.cs`.
   - Acceptance: ENA candidate metadata is combined with decoded BARs,
     caller-provided BAR aperture lengths, and MSI-X metadata into a
     deterministic resource plan. Host tests cover MMIO register BAR acceptance,
     MSI-X capability parsing, malformed BAR apertures, and malformed MSI-X
     regions. No live PCI access, BAR mapping, MSI-X programming, DMA, or
     packet I/O is performed.

7. #255 - Add ENA admin command execution boundary planner.
   - Files: `src/OS.Kernel/Drivers/EnaAdminCommandPlan.cs`,
     `tests/OS.HostTests/EnaAdminCommandPlanTests.cs`.
   - Acceptance: `GET_FEATURE DEVICE_ATTRIBUTES` command planning and
     caller-buffer submission/completion validation are status-coded and
     host-tested; no BAR access, MMIO, DMA allocation, doorbells, MSI/MSI-X,
     interrupts, packet I/O, DHCP, TCP, HTTP, or real network I/O.
   - Status: implemented as a pure planner over caller-owned buffers. It
     rejects invalid command IDs, unsupported features, insufficient timeout
     budgets, short submission/completion buffers, command-ID mismatch, phase
     mismatch, non-success completion status, and invalid device-attribute
     payloads.

8. #257 - Add ENA DMA address and queue-memory capability planner.
   - Files: `src/OS.Kernel/Drivers/EnaDmaAddressPlan.cs`,
     `tests/OS.HostTests/EnaDmaAddressPlanTests.cs`.
   - Acceptance: caller-provided physical DMA regions are validated against
     device-reported 32/48/64-bit address widths, alignment, zero-length,
     overflow, address-range, and admin queue memory layout constraints.
   - Status: implemented as a status-coded capability planner only. It creates
     derived AQ/ACQ physical regions from a successful admin queue layout and
     rejects invalid DMA facts before any allocator, IOMMU, MMIO, interrupt, or
     packet path exists.

9. #258 - Add ENA TX/RX descriptor layout structs and parser tests.
   - Files: `src/OS.Kernel/Drivers/EnaDataQueueDescriptors.cs`,
     `tests/OS.HostTests/EnaDataQueueDescriptorsTests.cs`.
   - Acceptance: TX/RX descriptor writers round-trip field values through
     caller-owned buffers; TX/RX completion parsers validate request ID, phase,
     queue ID, status, reserved bits, default/uninitialized TX completions, and
     RX single-buffer flags.
   - Status: implemented as a caller-buffer descriptor/parser contract only.
     It validates byte layout, completion metadata, and default/uninitialized
     TX completion entries before any ENA I/O queue creation, DMA allocation,
     doorbell write, interrupt handling, Ethernet parsing, or packet I/O exists.

## Evidence Boundary

This document now has host-test evidence for early ENA metadata and
capability-planning slices only, plus caller-buffer ENA data-queue descriptor
layout validation. It proves no live PCI discovery, no BAR mapping, no MSI-X
programming, no DMA allocation, no IOMMU programming, no admin queue execution,
no MAC retrieval from hardware, no Ethernet frame send/receive, no DHCP, no TCP,
and no Kestrel behavior.

The first valid runtime networking evidence remains an ENA-backed Ethernet
frame RX/TX smoke that feeds the existing network stack and emits stable
markers without claiming higher protocols until each protocol has its own proof.
