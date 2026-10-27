# AWS Nitro ENA/NVMe PCI Discovery Plan

Status: design note plus host-tested metadata planner evidence. Current driver
code is limited to caller-provided descriptor, BAR, MSI-X, register, and
identify-buffer planning. It does not claim AWS boot, ENA packet I/O, NVMe
block I/O, DHCP, TCP, Minimal API, or Kestrel support.

## Sources Checked

Checked on 2026-05-22:

- AWS EC2 enhanced networking documentation: Nitro-based instances use ENA for
  enhanced networking, and ENA is the Elastic Network Adapter path for supported
  instance networking.
- AWS EC2 ENA enablement documentation: an AMI needs the required ENA driver to
  use enhanced networking, and the AWS docs point to the ENA driver materials.
- AWS EC2 NVMe driver documentation: Nitro exposes EBS and instance-store
  volumes as NVMe block devices.
- Amazon EBS NVMe documentation: EBS NVMe device names can be assigned in an
  order different from the block-device mapping.
- `docs/aws-cloud-requirements.md`, especially the Nitro device contract.
- `docs/drivers.md` and current host-tested ACPI, PCI, BAR, and virtio driver
  contracts in `src/OS.Kernel/Drivers`.

References:

- https://docs.aws.amazon.com/AWSEC2/latest/UserGuide/enhanced-networking.html
- https://docs.aws.amazon.com/AWSEC2/latest/UserGuide/enhanced-networking-ena.html
- https://docs.aws.amazon.com/AWSEC2/latest/UserGuide/aws-nvme-drivers.html
- https://docs.aws.amazon.com/ebs/latest/userguide/nvme-ebs-volumes.html
- https://github.com/amzn/amzn-drivers

## Target Scope

The first AWS device target is x86_64 EC2 Nitro:

```text
x86_64 HVM AMI
ACPI tables
PCIe ECAM discovery
ENA network device
NVMe block controller for EBS root volume
serial diagnostics
```

Graviton/ARM64 is a later track. It still needs ENA and NVMe concepts, but it
adds aarch64 boot, GIC, ARM timer, ARM memory-management, and a validated AMI
creation path. Do not let ARM64 concerns shape the first x86_64 PCI contracts
unless the contract is naturally architecture-neutral.

## Current OS Starting Point

The current driver layer already has useful host-testable metadata contracts:

- ACPI MCFG parsing can produce `PciEcamSegment` records from caller-provided
  ACPI snapshots.
- `PciEcamConfigurationSpace` can calculate ECAM config addresses without
  touching MMIO.
- `PciBusScanner` and `PciEcamSegmentScanner` can scan caller-provided first
  64-byte PCI config-space snapshots through an injected reader.
- `PciConfigurationHeader` can parse PCI type-0 identity, class/subclass/PI,
  BAR raw values, and capability-list presence.
- `PciBarDecoder` can classify raw type-0 BAR values as absent, I/O, 32-bit
  MMIO, 64-bit MMIO, consumed, or malformed.
- Virtio-specific candidate, capability, binding, queue, and descriptor plans
  are host-tested, but they are not generic PCI driver binding and they are not
  usable as ENA or NVMe drivers.

Nitro-specific ownership and live integration still to qualify (existing QEMU/virtio implementations do not supply these AWS device proofs):

- live ACPI physical-memory reads from the firmware RSDP;
- live MCFG-to-ECAM mapping;
- live PCI configuration reads/writes;
- PCI bridge/resource-window handling if Nitro exposes non-root-bus paths;
- BAR sizing writes and safe BAR MMIO mapping;
- PCI command-register ownership for memory space and bus mastering;
- MSI/MSI-X capability parsing and interrupt-vector setup;
- DMA-safe memory allocation, mapping, cache policy, and lifetime tracking;
- admin/I/O queue publication to real devices.

## Device Model Boundaries

### ENA Is Not Virtio-Net

ENA must be treated as an AWS network device reached through PCIe. It is not the
existing virtio-net path:

- do not select it through vendor `0x1AF4` or virtio device IDs;
- do not parse modern virtio vendor-specific PCI capabilities for ENA;
- do not prepend or consume virtio-net packet headers;
- do not use virtqueue split-ring descriptors, feature/status registers, or
  virtio notify semantics.

The reusable pieces are only the surrounding style: status-coded parsers,
caller-owned buffers, descriptor-snapshot host tests, BAR metadata validation,
and explicit evidence boundaries.

### ENA Is Not Azure NetVSC

ENA is also not Azure NetVSC:

- no Hyper-V VMBus channel offers;
- no GPADL/ring-buffer transport;
- no NetVSC protocol negotiation;
- no Azure platform IP or WireServer assumptions in the NIC driver.

Azure remains a separate VMBus/NetVSC/StorVSC provider track.

### NVMe Is Not Virtio-Blk

NVMe storage on Nitro must be treated as a PCI NVMe controller:

- do not select it through virtio block IDs;
- do not publish virtio-blk three-descriptor read chains;
- do not use virtqueue split rings, virtio status writes, or virtio block
  request headers;
- do not treat current virtio-blk read planning as NVMe read evidence.

NVMe needs standard NVMe controller registers, admin queues, identify commands,
I/O submission/completion queues, namespace selection, PRP/SGL-style DMA
buffers, completion status handling, and timeouts.

### NVMe Is Not Azure StorVSC

NVMe is not the Azure StorVSC path:

- no VMBus storage channel discovery;
- no SCSI command transport over StorVSC;
- no Azure disk naming or provisioning assumptions.

AWS EBS root-volume handling must account for NVMe controller/namespace identity
and the fact that OS-visible NVMe device order can differ from AWS block-device
mapping order.

## ACPI + PCIe Discovery Prerequisites

The first Nitro discovery chain should be:

1. Use the existing EFI ACPI RSDP handoff as provenance only.
2. Add safe physical-memory reads for RSDP, RSDT/XSDT, and MCFG tables.
3. Parse MCFG into `PciEcamSegment` records.
4. Map ECAM windows as uncached/MMIO-safe physical memory.
5. Read PCI type-0 config headers through an ECAM reader.
6. Preserve source index, segment index, segment group, ECAM base, bus range,
   bus/device/function, vendor ID, device ID, revision ID, class/subclass/PI,
   header type, command/status, subsystem IDs, capability pointer, and raw BARs.
7. Capture enough config-space bytes to parse standard PCI capabilities, not
   just the first 64 bytes. MSI-X needs capability-list walking and table/PBA
   BAR metadata.
8. Size BARs through controlled all-ones writes only after config-write safety
   exists, then restore original BAR values.
9. Map required BAR apertures only after size, type, and alignment validation.
10. Enable memory space, bus mastering, and MSI/MSI-X only after the driver owns
    DMA buffers and interrupt handlers.

The first host-testable contracts should operate over descriptor snapshots that
simulate outputs from steps 5 through 8. Live EC2 proof comes later.

## Common PCI Candidate Contract

Create a generic candidate record shape before ENA/NVMe binding code diverges.
It should be usable by both drivers:

```text
SourceIndex
SegmentIndex
SegmentGroup
EcamBasePhysicalAddress
SegmentStartBus
SegmentEndBus
Bus
Device
Function
VendorId
DeviceId
RevisionId
ClassCode
Subclass
ProgrammingInterface
HeaderType
CommandRegister
StatusRegister
SubsystemVendorId
SubsystemId
CapabilityPointer
DecodedBars
BarSizes
MsixCapabilityMetadata
```

For host tests, the candidate input can be a struct that combines
`PciEcamScannedDevice`, a full PCI config-space byte snapshot, and caller-provided
BAR aperture sizes. The live reader and BAR sizer can be swapped in later.

## ENA Discovery Contract

First ENA work should stop at candidate selection.

Candidate parser inputs:

- ECAM scanner output with PCI location and first type-0 header metadata;
- full PCI config-space snapshot for subsystem IDs and capabilities;
- BAR size metadata supplied by a fixture or future BAR sizer.

Initial candidate acceptance:

- PCI network controller class should be preserved and validated;
- Amazon/AWS ENA vendor/device IDs should be table-driven and justified by the
  official AWS ENA driver materials and at least one Nitro descriptor snapshot;
- virtio and Azure/VMBus identities must be rejected explicitly;
- candidate metadata must preserve BARs and MSI-X capability location without
  mapping or enabling them.

Driver initialization boundaries after candidate selection:

1. BAR/MSI-X boundary: validate ENA MMIO BARs, parse MSI-X table/PBA metadata,
   map device registers, and assign interrupt vectors.
2. DMA-safe memory boundary: allocate physically addressable admin queue memory,
   completion rings, submission rings, RX buffers, TX buffers, and device-readable
   descriptors with explicit ownership transitions.
3. Admin queue boundary: reset or initialize the device, create admin SQ/CQ,
   query device attributes, get MAC address, discover max queues/features, and
   configure link/interrupt behavior.
4. I/O queue boundary: create TX/RX queue pairs, publish RX buffers, publish TX
   descriptors, ring doorbells, and process completions.
5. Interrupt boundary: process MSI-X vectors for admin completions and queue
   completions, with a polling fallback only as an explicit diagnostic mode.

None of these boundaries should claim Ethernet, DHCP, TCP, or Kestrel support
until packets actually move through the OS network stack.

Current host-test status for the first BAR/MSI-X boundary is metadata-only.
`EnaPciResourcePlan` consumes an already discovered ENA PCI candidate, decoded
BAR descriptors, caller-provided BAR aperture sizes, and an MSI-X capability
snapshot. It validates the ENA register MMIO BAR and MSI-X table/PBA regions
without touching live ECAM, mapping BARs, enabling memory space or bus
mastering, enabling MSI-X, allocating interrupt vectors, allocating DMA memory,
or moving packets.

## NVMe Discovery Contract

First NVMe work should split into a PCI candidate parser and register-map
constants.

Candidate parser inputs:

- ECAM scanner output with PCI location and first type-0 header metadata;
- full PCI config-space snapshot;
- BAR size metadata supplied by a fixture or future BAR sizer.

Initial candidate acceptance:

- base class `0x01` mass storage;
- subclass `0x08` non-volatile memory;
- programming interface for NVMe;
- at least one memory BAR suitable for controller registers, normally BAR0;
- MSI-X capability metadata preserved when present;
- vendor/subsystem IDs preserved for AWS-specific root-volume identification
  later, but the generic NVMe class should not require an AWS-only vendor ID.

Register-map constants needed before live reads:

```text
CAP    controller capabilities, offset 0x0000, width 64
VS     version, offset 0x0008, width 32
INTMS  interrupt mask set, offset 0x000C, width 32
INTMC  interrupt mask clear, offset 0x0010, width 32
CC     controller configuration, offset 0x0014, width 32
CSTS   controller status, offset 0x001C, width 32
AQA    admin queue attributes, offset 0x0024, width 32
ASQ    admin submission queue base, offset 0x0028, width 64
ACQ    admin completion queue base, offset 0x0030, width 64
DB     doorbell base, offset 0x1000, stride from CAP.DSTRD
```

Driver initialization boundaries after candidate selection:

1. BAR/MSI-X boundary: validate register BAR size, map controller registers,
   parse MSI-X metadata, and assign vectors.
2. DMA-safe memory boundary: allocate page-aligned admin SQ/CQ, I/O SQ/CQ, PRP
   lists if needed, identify buffers, and data buffers with explicit physical
   addresses.
3. Admin queue boundary: disable controller if needed, program AQA/ASQ/ACQ,
   enable controller, wait for `CSTS.RDY`, submit Identify Controller and
   Identify Namespace commands, and read completion entries.
4. I/O queue boundary: create I/O completion/submission queues, submit read and
   optional write commands, process completions, and expose namespace sector
   geometry to storage.
5. Interrupt boundary: route MSI-X completions to admin/I/O queues, with
   timeouts and a polling diagnostic fallback.

None of these boundaries should claim EBS root-volume reads until an NVMe read
command completes against a live namespace and the returned data is validated.

Current host-test status for the first Nitro NVMe PCI/resource/namespace
boundary is metadata-only. `NitroNvmePlanner` consumes a caller-provided PCI
snapshot, decoded BAR descriptors, caller-provided BAR aperture sizes, optional
MSI-X metadata, a caller-provided Identify Controller buffer, and a
caller-provided Identify Namespace buffer. It validates standard NVMe PCI class
metadata, preserves vendor and subsystem IDs for future AWS EBS root-volume
mapping, validates a controller MMIO BAR without mapping it, decodes namespace
count/id, selected LBA format, logical block size, total logical blocks,
capacity bytes, and bounded serial/model identity fields. It rejects non-NVMe
PCI functions, absent or malformed MMIO BAR facts, malformed identify buffers,
invalid namespace IDs, impossible LBA sizes, zero capacity, capacity overflow,
and malformed serial/model fields.

This planner still performs no live ECAM reads or writes, BAR sizing writes,
BAR mapping, MMIO access, admin queue creation, DMA allocation, interrupt
configuration, NVMe command submission, sector reads or writes, filesystem
access, EBS root-volume selection, or AWS boot proof.

## Host-Testable First Slices

Before live EC2 proof, implement these as pure parser/planner slices over
descriptor snapshots:

1. PCI descriptor snapshot model for a full config-space byte span, decoded
   type-0 header, decoded BAR metadata, caller-provided BAR sizes, and optional
   MSI-X metadata.
2. PCI standard capability-list walker that can find MSI-X without reading live
   ECAM.
3. ENA PCI candidate selector that rejects virtio and Azure assumptions and
   preserves full PCI location and BAR/MSI-X metadata.
4. NVMe PCI candidate selector that recognizes standard NVMe class/subclass/PI
   and preserves AWS vendor/subsystem metadata without requiring AWS-only IDs.
5. NVMe controller register-map constants and CAP-derived doorbell stride
   calculations.
6. Queue-memory layout planners for ENA admin queues and NVMe admin queues,
   using caller-owned physical-address placeholders.
7. Admin command descriptor layouts for ENA and NVMe with endian-stable field
   encoding, but no doorbell writes.

Suggested host-test markers:

```text
TEST_PASS drivers.ena.pci.candidate selects nitro ena device
TEST_PASS drivers.ena.pci.candidate rejects virtio net
TEST_PASS drivers.ena.pci.candidate preserves bar msix metadata
TEST_PASS drivers.nvme.pci.candidate selects nvme controller
TEST_PASS drivers.nvme.pci.candidate rejects virtio block
TEST_PASS drivers.nvme.registers exposes admin queue offsets
TEST_PASS drivers.nvme.registers calculates doorbell stride
```

## Follow-Up Issues To Create Or Update

### Follow-up 1: Add ENA PCI Candidate Parser Over Descriptor Snapshots

Labels: `component:aws`, `component:nitro`, `component:ena`,
`component:drivers`, `phase:F-platform-drivers`, `type:implementation`.

Files likely involved:

- `src/OS.Kernel/Drivers/EnaPciCandidateSelector.cs`
- `tests/OS.HostTests/EnaPciCandidateSelectorTests.cs`
- `docs/aws-nitro-device-plan.md` if status changes

Acceptance criteria:

- Accepts caller-provided ECAM scan output plus full PCI config-space snapshots.
- Uses a table-driven Amazon/AWS ENA identity list validated against official
  AWS ENA driver materials and at least one Nitro descriptor snapshot.
- Requires network-controller metadata and preserves subsystem IDs.
- Rejects virtio-net (`0x1AF4` modern virtio IDs) and Azure/VMBus assumptions.
- Preserves source index, segment, ECAM base, BDF, vendor/device, class/subclass,
  capability pointer, decoded BAR metadata, BAR sizes, and MSI-X metadata.
- Does not map BARs, enable bus mastering, allocate DMA memory, create queues,
  configure interrupts, or move packets.
- Adds host tests with stable `TEST_PASS drivers.ena.pci.candidate ...` markers.

### Follow-up 2: Add NVMe PCI Candidate Parser And Controller Register Constants

Labels: `component:aws`, `component:nitro`, `component:nvme`,
`component:drivers`, `component:storage`, `phase:F-platform-drivers`,
`type:implementation`.

Files likely involved:

- `src/OS.Kernel/Drivers/NvmePciCandidateSelector.cs`
- `src/OS.Kernel/Drivers/NvmeControllerRegisterMap.cs`
- `tests/OS.HostTests/NvmePciCandidateSelectorTests.cs`
- `tests/OS.HostTests/NvmeControllerRegisterMapTests.cs`
- `docs/aws-nitro-device-plan.md` if status changes

Acceptance criteria:

- Accepts caller-provided ECAM scan output plus full PCI config-space snapshots.
- Selects standard NVMe PCI controllers by class/subclass/programming-interface,
  while preserving AWS vendor/subsystem identity for later EBS root mapping.
- Requires a memory BAR suitable for controller registers and preserves MSI-X
  metadata when present.
- Rejects virtio-blk (`0x1AF4` modern virtio block IDs) and Azure StorVSC
  assumptions.
- Defines constants for CAP, VS, INTMS, INTMC, CC, CSTS, AQA, ASQ, ACQ, and
  doorbell base/stride calculations.
- Does not map BARs, enable bus mastering, allocate DMA memory, create queues,
  configure interrupts, or read/write sectors.
- Adds host tests with stable `TEST_PASS drivers.nvme...` markers.

### Shared Dependency To Create Or Update If Missing

Add a generic PCI MSI-X capability parser over caller-provided config-space
snapshots if no issue already owns it. ENA and NVMe both need MSI-X table/PBA
metadata before interrupt-capable live drivers.

Acceptance criteria:

- Walks the standard PCI capability list from caller-provided bytes.
- Finds MSI-X capability metadata, table BAR indicator, table offset, table
  size, PBA BAR indicator, and PBA offset.
- Rejects loops, out-of-range offsets, truncated capability records, invalid BAR
  indicators, and arithmetic overflow.
- Performs no ECAM reads, config writes, BAR mapping, interrupt programming, or
  driver binding.

## Evidence Boundary

This document is an input to future driver work. It proves only that the AWS
Nitro ENA/NVMe discovery contracts have been separated from existing virtio and
Azure tracks. It does not prove:

- EC2 or QEMU boot;
- live ACPI table dereferencing;
- live PCI enumeration;
- BAR sizing or mapping;
- MSI-X configuration;
- DMA-safe memory;
- ENA admin/I/O queue setup;
- NVMe admin/I/O queue setup;
- EBS root-volume reads;
- Ethernet, DHCP, TCP, Minimal API, or Kestrel support.
