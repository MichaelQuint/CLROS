global using System;

#if M09_004_PROBE || M09_005_PROBE || M09_006_PROBE || M09_009_PROBE || M09_010_PROBE || M16_008_PROBE || M07_026_PROBE
using Cooperative = OS.Kernel.Threading.ThreadContextSwitch;
using SavedContext = OS.Kernel.Threading.X64ThreadContext;
#endif
#if M09_005_PROBE || M09_006_PROBE || M09_009_PROBE || M09_010_PROBE
using TimerIrq = OS.Kernel.Threading.X64TimerInterrupt;
using TickTimer = OS.Kernel.Threading.SchedulerTimer;
using Deadlines = OS.Kernel.Threading.DeadlineQueue;
#endif
#if M04_LAYOUT_CONTRACT_PROBE
using OS.Kernel.Runtime;
#endif
#if M04_MANAGED_HEAP_PAGE_RANGE_PROBE || (M03_FIRST_REAL_EXIT_BOOT_SERVICES_PROBE || CLROS_NORMAL_VERIFIED_EXIT)
using OS.Kernel.Memory;
#endif
#if (M03_FIRST_REAL_EXIT_BOOT_SERVICES_PROBE || CLROS_NORMAL_VERIFIED_EXIT)
using OS.Kernel.Diagnostics;
using OS.Kernel.Platform;
#if M16_004_PROBE
using OS.Kernel.Compatibility.LinuxX64;
#endif
#if M05_PCI_ENUMERATION_PROBE || M16_006_PROBE
using OS.Drivers;
#endif
#if M07_ARP_RECEIVE_PROBE
using OS.Net;
#if !M07_VIRTIO_NET_PROBE
#error The ARP receive profile requires the accepted original Virtio-net profile.
#endif
#endif
#if M06_VIRTIO_BLOCK_QUEUE_SETUP_PROBE
using OS.Storage;
#endif
#if M06_FAT_READ_PROBE
using FatChain = OS.Storage.FATClusterChainsLoopBadClusterTruncationDetection;
using FatDirectory = OS.Storage.MultiClusterDirectoryVFATLongNameSupportSeparate;
using FatFile = OS.Storage.KnownFileSHAN256EquivalentKnownFixtureHash;
#endif
#if !(M01_LOADED_IMAGE_STACK_PROBE || CLROS_NORMAL_VERIFIED_EXIT) || !(M01_LIVE_ALLOCATOR_PROBE || CLROS_NORMAL_VERIFIED_EXIT) || !(ACPI_PHYSICAL_DISCOVERY_PROBE || CLROS_NORMAL_VERIFIED_EXIT)
#error The first real exit requires the original loaded-image, live allocator and copied ACPI adapters.
#endif
#if M01_OWNED_PAGING_PROBE || M04_MANAGED_HEAP_PAGE_RANGE_PROBE || M04_INDEPENDENT_ENDIAN_CONFORMANCE_PROBE || M03_TYPED_EXIT_BOOT_SERVICES_CALL_PROBE
#error The first real exit is an exclusive firmware-CR3 path, with no intervening paging, heap or controlled exit probe.
#endif
#if M02_OWNED_GDT_TSS_TERMINAL_PROBE || M02_UD2_VECTOR_STATE_PROBE || M02_PAGE_FAULT_CR2_ERROR_CODE_PROBE || M02_DOUBLE_FAULT_IST_PROBE || M02_UNEXPECTED_VECTOR_PANIC_PROBE
#error The first real exit cannot be combined with another terminal exception probe.
#endif
#endif
namespace OS.Boot;
#pragma warning disable 649
public static unsafe partial class EfiEntry
{
#if (M03_FIRST_REAL_EXIT_BOOT_SERVICES_PROBE || CLROS_NORMAL_VERIFIED_EXIT)

#if M16_008_PROBE

#elif M09_004_PROBE || M09_005_PROBE || M09_006_PROBE || M09_009_PROBE || M09_010_PROBE || M07_026_PROBE

#endif
#if M16_007_PROBE
#if M10_017_PROBE || M10_018_PROBE || M16_027_PROBE

#else

#endif
#endif
#if M16_002_PROBE

#if M16_025_PROBE

#elif M16_011_PROBE

#elif M07_026_PROBE

#elif M16_008_PROBE

#elif M16_007_PROBE

#else
#if M16_006_PROBE

#else
#if M16_005_PROBE

#else
#if M16_004_PROBE

#else

#endif
#endif
#endif
#endif
#endif

#if M10_004_PROBE

#else

#endif

#if M09_004_PROBE || M09_005_PROBE || M09_006_PROBE || M09_009_PROBE || M09_010_PROBE || M16_008_PROBE || M07_026_PROBE

#endif
#if M16_002_PROBE

#endif
#if M16_004_PROBE

#endif
#if M16_005_PROBE

#endif
#if M16_006_PROBE

#if M16_014_PROBE

#endif
#endif
#if M16_007_PROBE

#endif
#if M10_004_PROBE

#endif
#if M05_PHYSICAL_MAPPING_PROBE

#endif
#if M05_OWNED_ACPI_SELECTION_PROBE

#endif
#if M05_PCI_ENUMERATION_PROBE

#if M05_BAR_DMA_PROBE

#endif
#if M05_VIRTIO_NEGOTIATION_PROBE

#endif
#if M06_VIRTIO_BLOCK_QUEUE_SETUP_PROBE

#if M06_VIRTIO_BLOCK_READ_PROBE

#if M06_LIVE_BLOCK_CONTRACT_PROBE

#if M06_GPT_PARTITION_PROBE

#if M06_FAT_READ_PROBE

#if M06_PACKAGE_READ_PROBE

#endif
#endif
#endif

#endif
#endif
#endif
#if M05_VIRTQUEUE_POLLING_PROBE || M05_VIRTQUEUE_INTERRUPT_PROBE

#endif
#if M07_VIRTIO_NET_PROBE

#if M07_ARP_RECEIVE_PROBE

#endif
#endif
#if M05_APIC_SETUP_PROBE

#endif
#if M05_VIRTQUEUE_INTERRUPT_PROBE || M06_BLOCK_MSIX_PROBE

#if M06_BLOCK_MSIX_PROBE

#endif
#endif
#endif
#if M05_MONOTONIC_CLOCK_PROBE || M16_003_PROBE

#if M10_004_PROBE

#if M16_019_UNAVAILABLE_PROBE || M16_019_INVALID_PROBE

#endif
#endif
#endif
#if M16_003_PROBE

#endif
#if M07_026_PROBE

#endif
#if (M03_OWNED_SUBSYSTEM_SURVIVAL_PROBE || CLROS_NORMAL_VERIFIED_EXIT)

#if M03_OWNED_SUBSYSTEM_PANIC_PROBE

#endif
#endif
#if M03_FIRMWARE_ADAPTER_REVOCATION_PROBE || M16_003_PROBE

#endif

#if M16_005_PROBE

#endif
#if M16_004_PROBE

#endif
#if M16_002_PROBE

#endif

#if M16_019_UNAVAILABLE_PROBE || M16_019_INVALID_PROBE

#endif

#if M03_FIRMWARE_ADAPTER_REVOCATION_PROBE

#endif

#if M05_PCI_ENUMERATION_PROBE

#if M05_APIC_SETUP_PROBE

#endif
#if M05_VIRTIO_NEGOTIATION_PROBE || M06_VIRTIO_BLOCK_QUEUE_SETUP_PROBE

#endif
#if M05_VIRTQUEUE_INTERRUPT_PROBE || M06_BLOCK_MSIX_PROBE

#endif
#endif
#if M05_MONOTONIC_CLOCK_PROBE || M16_003_PROBE

#endif
#if (M03_OWNED_SUBSYSTEM_SURVIVAL_PROBE || CLROS_NORMAL_VERIFIED_EXIT)

#endif

#if M09_004_PROBE || M09_005_PROBE || M09_006_PROBE || M09_009_PROBE || M09_010_PROBE || M16_008_PROBE || M07_026_PROBE

#endif
#if M16_002_PROBE

#endif
#if M07_026_PROBE

#endif
#if M16_004_PROBE

#endif
#if M16_005_PROBE

#endif
#if M16_006_PROBE

#endif
#if M16_007_PROBE

#endif
#if M16_027_PROBE

#elif M16_025_PROBE

#endif

#if M16_002_PROBE

#endif

#if M05_PHYSICAL_MAPPING_PROBE

#endif

#if M05_PHYSICAL_MAPPING_PROBE

#endif

#if M16_005_PROBE
#if M16_006_PROBE

#endif

#endif

#if (M03_OWNED_SUBSYSTEM_SURVIVAL_PROBE || CLROS_NORMAL_VERIFIED_EXIT)

#endif

#if M03_FIRMWARE_ADAPTER_REVOCATION_PROBE || M16_003_PROBE

#endif
#if M10_004_PROBE

#endif

#if M03_FIRMWARE_ADAPTER_REVOCATION_PROBE || M16_003_PROBE

#endif

#if M09_004_PROBE || M09_005_PROBE || M09_006_PROBE || M09_009_PROBE || M09_010_PROBE || M16_008_PROBE || M07_026_PROBE

#if M16_008_PROBE

#if M16_009_PROBE

#endif
#endif

#if M16_008_PROBE

#if M16_009_PROBE

#endif
#endif

#if M16_008_PROBE

#else

#endif

#endif

#if M16_002_PROBE

#if M07_026_PROBE

#endif
#if M16_027_PROBE

#elif M16_025_PROBE

#endif
#if M16_004_PROBE

#endif
#if M16_005_PROBE

#endif
#if M16_006_PROBE

#endif
#if M16_007_PROBE

#endif
#if M16_008_PROBE || M07_026_PROBE

#endif

#if M16_008_PROBE || M07_026_PROBE

#else

#endif

#if M16_004_PROBE

#if M16_006_PROBE

#endif

#if M16_006_PROBE

#endif
#if M16_008_PROBE

#endif

#endif

#if M16_005_PROBE

#if M16_007_PROBE

#endif
#if M16_006_PROBE

#endif

#if M16_006_PROBE

#if M16_008_PROBE

#else

#endif

#if M16_014_PROBE

#endif

#if M16_014_PROBE

#else

#endif

#if M16_014_PROBE

#else
#if M10_004_PROBE

#else
#if M16_008_PROBE

#endif

#endif
#endif

#if M16_014_PROBE

#endif

#if M16_014_PROBE

#endif

#if M16_014_PROBE

#else

#endif

#if !M16_014_PROBE

#endif

#if M16_009_PROBE

#endif

#if M16_014_PROBE

#endif
#if M10_004_PROBE

#endif

#if M16_014_PROBE

#endif

#if M16_027_PROBE

#else

#endif

#if M16_027_PROBE

#else

#endif

#if M16_027_PROBE

#else

#endif

#if M16_014_PROBE

#else

#endif
#if M16_014_PROBE

#endif

#if M16_027_PROBE

#else

#endif

#endif

#if M16_007_PROBE

#if M16_027_PROBE

#else

#endif

#if M16_008_PROBE

#else

#endif

#if M16_008_PROBE

#else

#endif

#if M10_004_PROBE

#endif
#if M16_027_PROBE

#endif

#if M10_017_PROBE || M10_018_PROBE || M16_027_PROBE

#else

#endif

#if M16_008_PROBE

#else

#endif

#if M16_027_PROBE

#else

#endif

#if M16_027_PROBE

#else

#endif

#if M16_027_PROBE

#else

#endif

#if M10_004_PROBE

#endif

#if M10_017_PROBE || M10_018_PROBE

#elif M10_004_PROBE

#else

#endif

#if M16_027_PROBE

#else

#endif

#endif

#if M16_009_PROBE

#endif

#if M16_006_PROBE

#endif
#if M16_008_PROBE

#endif
#if M16_009_PROBE

#endif

#if M16_006_PROBE

#endif

#if M09_011_PROBE || M09_012_PROBE || M16_010_PROBE

#if M09_011_PROBE

#elif M09_012_PROBE

#else

#endif

#if M09_011_PROBE

#elif M09_012_PROBE

#else

#endif

#endif

#if M16_009_PROBE

#elif M16_008_PROBE

#elif M16_007_PROBE

#elif M16_006_PROBE

#else

#endif

#if M16_027_PROBE

#endif

#if M16_027_PROBE

#else

#endif

#if M16_008_PROBE

#else

#endif

#if M16_006_PROBE

#endif
#if M16_007_PROBE

#if CLROS_LINUX_SOCKETS

#endif

#endif
#if !M16_008_PROBE

#endif

#if M16_008_PROBE

#if M16_014_PROBE

#else

#endif

#if M16_014_PROBE

#endif
#if M16_009_PROBE

#else

#endif
#else

#if M16_006_PROBE

#endif
#if M16_007_PROBE

#endif

#if M16_007_PROBE

#elif M16_006_PROBE

#else

#endif

#if M16_006_PROBE

#if M16_007_PROBE

#else

#endif
#else

#endif
#endif

#if M16_008_PROBE

#elif M16_007_PROBE

#elif M16_006_PROBE

#else

#endif

#if M16_008_PROBE

#else

#if M16_006_PROBE

#endif

#endif

#if M16_027_PROBE

#else

#endif

#if M16_027_PROBE

#else

#endif

#if M16_006_PROBE

#else

#endif

#if M16_027_PROBE

#else

#endif

#if M16_009_PROBE

#else

#endif

#endif

#endif

#if M09_004_PROBE || M09_005_PROBE || M09_006_PROBE || M09_009_PROBE || M09_010_PROBE || M16_008_PROBE || M07_026_PROBE

#endif

#if M09_005_PROBE || M09_006_PROBE || M09_009_PROBE || M09_010_PROBE

#endif

#if (M03_OWNED_SUBSYSTEM_SURVIVAL_PROBE || CLROS_NORMAL_VERIFIED_EXIT)

#if M09_004_PROBE

#endif
#if M16_002_PROBE

#endif

#if M16_004_PROBE

#endif
#if M16_003_PROBE

#endif
#if M16_005_PROBE && !M16_008_PROBE
#if M16_007_PROBE

#elif M16_006_PROBE

#else

#endif

#endif
#if M05_MONOTONIC_CLOCK_PROBE

#endif
#if M05_PCI_ENUMERATION_PROBE

#if M05_APIC_SETUP_PROBE

#if M16_008_PROBE && !CLROS_LINUX_SOCKETS
#if M09_011_PROBE

#elif M09_012_PROBE

#elif M16_010_PROBE

#elif M16_009_PROBE

#else

#endif

#if M09_011_PROBE || M09_012_PROBE || M16_010_PROBE

#else

#endif
#if M09_011_PROBE

#elif M09_012_PROBE

#elif M16_010_PROBE

#endif
#endif
#if M09_005_PROBE

#endif
#if M09_006_PROBE || M09_009_PROBE || M09_010_PROBE

#endif
#endif
#if M05_BAR_DMA_PROBE

#endif
#if M05_VIRTIO_NEGOTIATION_PROBE

#endif
#if M06_VIRTIO_BLOCK_QUEUE_SETUP_PROBE

#if M06_VIRTIO_BLOCK_READ_PROBE

#if M06_LIVE_BLOCK_CONTRACT_PROBE

#if M06_GPT_PARTITION_PROBE

#if M06_FAT_READ_PROBE

#if M06_PACKAGE_READ_PROBE

#if M06_BLOCK_MSIX_PROBE

#endif
#endif
#endif
#endif
#endif
#endif
#endif
#if M05_VIRTQUEUE_POLLING_PROBE

#endif
#if M07_VIRTIO_NET_PROBE

#if M07_ARP_RECEIVE_PROBE

#if M07_026_PROBE

#endif
#endif
#endif
#if M05_VIRTQUEUE_INTERRUPT_PROBE

#endif

#endif
#if M05_OWNED_ACPI_SELECTION_PROBE

#endif
#if M05_PHYSICAL_MAPPING_PROBE

#endif
#if CLROS_NORMAL_VERIFIED_EXIT

#endif

#elif M03_FIRMWARE_ADAPTER_REVOCATION_PROBE

#else

#endif

#if M05_PCI_ENUMERATION_PROBE
#if M05_APIC_SETUP_PROBE

#endif
#if M05_VIRTQUEUE_INTERRUPT_PROBE || M06_BLOCK_MSIX_PROBE

#if M06_BLOCK_MSIX_PROBE

#else

#endif

#if M05_VIRTQUEUE_INTERRUPT_PROBE

#endif
#endif

#endif

#if M05_VIRTIO_NEGOTIATION_PROBE || M06_VIRTIO_BLOCK_QUEUE_SETUP_PROBE

#if M06_VIRTIO_BLOCK_QUEUE_SETUP_PROBE

#else

#endif

#if M06_VIRTIO_BLOCK_QUEUE_SETUP_PROBE

#else

#endif

#if M05_VIRTIO_NEGOTIATION_PROBE

#if M05_VIRTQUEUE_INTERRUPT_PROBE

#endif

#endif

#endif

#if M06_VIRTIO_BLOCK_QUEUE_SETUP_PROBE

#if M06_BLOCK_MSIX_PROBE

#endif

#if M06_VIRTIO_BLOCK_READ_PROBE

#endif

#if M06_VIRTIO_BLOCK_READ_PROBE

#endif

#if M06_BLOCK_MSIX_PROBE

#elif M06_PACKAGE_READ_PROBE

#else

#endif

#endif

#if M06_VIRTIO_BLOCK_READ_PROBE

#if M06_LIVE_BLOCK_CONTRACT_PROBE

#endif

#if M06_GPT_PARTITION_PROBE

#if M06_FAT_READ_PROBE

#endif

#if M06_PACKAGE_READ_PROBE

#endif

#if M06_PACKAGE_READ_PROBE

#endif

#endif

#if M06_FAT_READ_PROBE

#endif

#if M06_PACKAGE_READ_PROBE

#endif

#if M06_BLOCK_MSIX_PROBE

#if M06_BLOCK_MSIX_POLLING_RECOVERY_PROBE

#else

#endif

#if !M06_BLOCK_MSIX_POLLING_RECOVERY_PROBE

#endif

#endif

#if M06_PACKAGE_READ_PROBE

#endif

#if M06_BLOCK_MSIX_PROBE

#else

#endif

#if M06_PACKAGE_READ_PROBE

#endif
#if M06_FAT_READ_PROBE

#else

#endif
#if M06_GPT_PARTITION_PROBE

#if M06_PACKAGE_READ_PROBE

#endif

#else

#endif

#if M06_BLOCK_MSIX_PROBE

#endif

#if M06_LIVE_BLOCK_CONTRACT_PROBE

#endif

#if M06_LIVE_BLOCK_CONTRACT_PROBE

#endif

#if M06_LIVE_BLOCK_CONTRACT_PROBE

#endif

#if M06_BLOCK_MSIX_PROBE

#endif
#if M06_GPT_PARTITION_PROBE

#endif

#if M06_LIVE_BLOCK_CONTRACT_PROBE

#endif

#if M06_BLOCK_MSIX_PROBE

#else

#endif

#endif

#if M05_VIRTQUEUE_POLLING_PROBE || M05_VIRTQUEUE_INTERRUPT_PROBE
#if M07_VIRTIO_NET_PROBE
#if M07_ARP_RECEIVE_PROBE

#endif

#endif
#if M05_VIRTQUEUE_POLLING_PROBE

#endif

#if M05_VIRTQUEUE_INTERRUPT_PROBE

#endif

#if M05_VIRTQUEUE_INTERRUPT_PROBE

#else

#endif

#if M05_VIRTQUEUE_INTERRUPT_PROBE

#endif

#if M07_VIRTIO_NET_PROBE

#if M07_ARP_RECEIVE_PROBE

#if M07_026_PROBE

#endif
#endif
#endif

#endif

#if M05_BAR_DMA_PROBE

#endif

#if M16_003_PROBE

#endif

#if M05_MONOTONIC_CLOCK_PROBE || M16_003_PROBE
#if M05_MONOTONIC_CLOCK_PROBE

#endif

#if M10_004_PROBE

#else

#endif

#if M05_MONOTONIC_CLOCK_PROBE

#endif
#endif

#if M05_OWNED_ACPI_SELECTION_PROBE

#endif

#if M05_PHYSICAL_MAPPING_PROBE

#endif

#if M03_OWNED_SUBSYSTEM_PANIC_PROBE

#else

#endif

#if (M03_OWNED_SUBSYSTEM_SURVIVAL_PROBE || CLROS_NORMAL_VERIFIED_EXIT)

#if M03_OWNED_SUBSYSTEM_PANIC_PROBE

#else

#endif

#if M03_OWNED_SUBSYSTEM_PANIC_PROBE

#endif

#if CLROS_NORMAL_VERIFIED_EXIT

#endif

#endif

#if M05_MONOTONIC_CLOCK_PROBE || M16_003_PROBE || M05_PCI_ENUMERATION_PROBE

#else

#endif

#endif
#if M03_TYPED_EXIT_BOOT_SERVICES_CALL_PROBE

#endif
#if M03_TYPED_EXIT_BOOT_SERVICES_CALL_PROBE

#endif
#if M03_FIRMWARE_ENTROPY_SEED_PROBE || M16_004_PROBE

#if M10_004_PROBE

#else

#endif

#endif
    private const int OwnedMemoryMapBufferSize = 128 * 1024;
    private const ulong OwnedMemoryRegionCapacity = 4096;
    private const int BootPageAllocatorNodeCapacity = 4096;
    private const int BootPageAllocatorNodeSlotCount = 6;
    private const ulong BootPageAllocatorInvalidNodeIndex = 0xffffffffffffffff;
    private const uint UefiConventionalMemoryType = 7;
    private const int BootPageAllocatorNodeSourceRegionIndexSlot = 0;
    private const int BootPageAllocatorNodePhysicalStartSlot = 1;
    private const int BootPageAllocatorNodePhysicalEndExclusiveSlot = 2;
    private const int BootPageAllocatorNodePageCountSlot = 3;
    private const int BootPageAllocatorNodeByteLengthSlot = 4;
    private const int BootPageAllocatorNodeNextIndexSlot = 5;
#if (ACPI_PHYSICAL_DISCOVERY_PROBE || CLROS_NORMAL_VERIFIED_EXIT)
    private const int AcpiRsdpScratchSize = 4096;
    private const int AcpiRootSdtBufferSize = 65536;
    private const int AcpiChildHeaderScratchSize = 36;
    private const int AcpiChildTableBufferSize = 65536;
    private const int AcpiPointerScratchCapacity = 256;
    private const int AcpiEcamSegmentScratchCapacity = 16;
    private const uint UefiAcpiReclaimMemoryType = 9;
    private const uint UefiAcpiMemoryNvsType = 10;
    private const uint AcpiRsdtSignature = 0x54445352;
    private const uint AcpiXsdtSignature = 0x54445358;
    private const uint AcpiMcfgSignature = 0x4746434d;
    private const uint AcpiMadtSignature = 0x43495041;
    private const uint AcpiHpetSignature = 0x54455048;
    private const int AcpiOwnedPayloadCapacity = 266240;
    private const int AcpiSdtHeaderLength = 36;
    private const int AcpiMcfgAllocationOffset = 44;
    private const int AcpiMcfgAllocationLength = 16;
#endif
#if M02_OWNED_GDT_TSS_TERMINAL_PROBE
    private const ulong M02OwnedGdtTssTerminalEfiSuccess = 0;
    private const uint M02OwnedGdtTssTerminalAllocateAnyPages = 0;
    private const uint M02OwnedGdtTssTerminalEfiLoaderCode = 1;
    private const uint M02OwnedGdtTssTerminalEfiLoaderData = 2;
    private const ulong M02OwnedGdtTssTerminalPageSize = 4096;
    private const ulong M02OwnedGdtTssTerminalPageCount = 1;
    private const byte M02OwnedGdtTssTerminalPreparedState = 1;
    private const byte M02OwnedGdtTssTerminalEnteredState = 2;
    private const byte M02OwnedGdtTssTerminalFailedState = 3;
    private const int M02OwnedGdtTssTerminalStartLine = 0;
    private const int M02OwnedGdtTssTerminalPreEntryFailureLine = 1;
    private const int M02OwnedGdtTssTerminalUnexpectedReturnLine = 2;

    private static byte s_m02OwnedGdtTssTerminalState;
    private static ulong s_m02OwnedGdtTssTerminalCodeAddress;
    private static ulong s_m02OwnedGdtTssTerminalDataAddress;
#endif
#if M02_UD2_VECTOR_STATE_PROBE
    private const ulong M02Ud2VectorStateEfiSuccess = 0;
    private const uint M02Ud2VectorStateAllocateAnyPages = 0;
    private const uint M02Ud2VectorStateEfiLoaderCode = 1;
    private const uint M02Ud2VectorStateEfiLoaderData = 2;
    private const ulong M02Ud2VectorStatePageSize = 4096;
    private const ulong M02Ud2VectorStateCodePageCount = 2;
    private const ulong M02Ud2VectorStateDataPageCount = 5;
    private const ulong M02Ud2VectorStateCodeByteCount =
        M02Ud2VectorStateCodePageCount * M02Ud2VectorStatePageSize;
    private const ulong M02Ud2VectorStateDataByteCount =
        M02Ud2VectorStateDataPageCount * M02Ud2VectorStatePageSize;
    private const byte M02Ud2VectorStatePreparedState = 1;
    private const byte M02Ud2VectorStateEnteredState = 2;
    private const byte M02Ud2VectorStateFailedState = 3;
    private const int M02Ud2VectorStateFailureHeadingLine = 0;
    private const int M02Ud2VectorStateFailureDetailLine = 1;

    private static byte s_m02Ud2VectorStateState;
    private static ulong s_m02Ud2VectorStateCodeAddress;
    private static ulong s_m02Ud2VectorStateDataAddress;
#endif
#if M01_OWNED_PAGING_PROBE

#if M02_SAFE_STUB_PROBE

#endif
#if M02_CHECKED_PORT_IO_BINDING_PROBE

#endif

#if M02_UNEXPECTED_VECTOR_PANIC_PROBE

#endif

#if M02_OWNED_GDT_TSS_TERMINAL_PROBE

#endif

#if M02_UD2_VECTOR_STATE_PROBE

#endif

#if M02_SAFE_STUB_PROBE

#endif

#if M02_SAFE_STUB_PROBE

#endif
#if M02_CHECKED_PORT_IO_BINDING_PROBE

#endif

#if M02_SAFE_STUB_PROBE

#endif

#if M02_SAFE_STUB_PROBE

#endif

#if M02_SAFE_STUB_PROBE

#if M02_CHECKED_PORT_IO_BINDING_PROBE

#endif

#if M02_COM1_OWNED_PORT_IO_PROBE

#endif

#if M02_CHECKED_PORT_IO_BINDING_PROBE

#if M02_COM1_OWNED_PORT_IO_PROBE

#if M02_OWNED_COM1_READY_PROBE

#if M02_PANIC_SAFE_OWNED_COM1_PROBE

#if M02_OWNED_X64_GDT_ENCODING_PROBE

#if M02_GUARDED_IST_STACK_ALLOCATION_PROBE

#endif
#endif
#endif
#endif
#endif
#endif

#endif

#if M02_SAFE_STUB_PROBE

#endif

#if M02_CHECKED_PORT_IO_BINDING_PROBE

#endif

#if M02_COM1_OWNED_PORT_IO_PROBE

#endif

#if M02_OWNED_COM1_READY_PROBE

#endif

#if M02_PANIC_SAFE_OWNED_COM1_PROBE

#endif

#if M02_OWNED_X64_GDT_ENCODING_PROBE

#endif

#if M02_GUARDED_IST_STACK_ALLOCATION_PROBE

#endif

#if M02_OWNED_X64_GDT_ENCODING_PROBE

#endif

#endif
    private static EfiMemoryMapSnapshot s_memoryMapSnapshot;
    private static BootHandoffRecord s_bootHandoff;
    private static OwnedMemoryMapBuffer s_ownedMemoryMap;
    private static OwnedMemoryRegionBuffer s_ownedMemoryRegions;
    private static BootPageAllocatorNodeBuffer s_bootPageAllocatorNodes;
    private static EfiMemoryMapSummary s_memoryMapSummary;
    private static BootMemoryRegionMaterializationResult s_memoryRegionMaterialization;
    private static EfiBootPageAllocatorReadinessResult s_bootPageAllocatorReadiness;
    private static EfiConfigurationTableScanResult s_acpiRsdpScanResult;
    private static UefiMemoryMapCopyPlanStatus s_memoryMapCopyStatus;
#if (ACPI_PHYSICAL_DISCOVERY_PROBE || CLROS_NORMAL_VERIFIED_EXIT)
    private static AcpiBootstrapWorkspace s_acpiBootstrap;
    private static AcpiBootstrapOwnedBuffer s_acpiBootstrapOwned;
#endif
#if M02_OWNED_GDT_TSS_TERMINAL_PROBE
    private static bool PrepareM02OwnedGdtTssTerminalProbe(
        EfiSimpleTextOutputProtocol* console,
        EfiSerialIoProtocol* serial,
        EfiSystemTable* systemTable)
    {
        ulong codeAddress = 0;
        ulong dataAddress = 0;
        bool codeAllocated = false;
        bool dataAllocated = false;
        EfiBootServices* bootServices = systemTable == null
            ? null
            : systemTable->BootServices;

        if (s_m02OwnedGdtTssTerminalState != 0 ||
            bootServices == null ||
            bootServices->AllocatePages == null ||
            bootServices->FreePages == null)
        {
            return FailM02OwnedGdtTssTerminalBeforeEntry(
                console,
                serial,
                bootServices,
                codeAddress,
                codeAllocated,
                dataAddress,
                dataAllocated);
        }

        ulong firmwareStatus = bootServices->AllocatePages(
            M02OwnedGdtTssTerminalAllocateAnyPages,
            M02OwnedGdtTssTerminalEfiLoaderCode,
            M02OwnedGdtTssTerminalPageCount,
            &codeAddress);
        codeAllocated = firmwareStatus == M02OwnedGdtTssTerminalEfiSuccess &&
            codeAddress != 0;
        if (!codeAllocated ||
            !IsValidM02OwnedGdtTssTerminalPage(codeAddress))
        {
            return FailM02OwnedGdtTssTerminalBeforeEntry(
                console,
                serial,
                bootServices,
                codeAddress,
                codeAllocated,
                dataAddress,
                dataAllocated);
        }

        firmwareStatus = bootServices->AllocatePages(
            M02OwnedGdtTssTerminalAllocateAnyPages,
            M02OwnedGdtTssTerminalEfiLoaderData,
            M02OwnedGdtTssTerminalPageCount,
            &dataAddress);
        dataAllocated = firmwareStatus == M02OwnedGdtTssTerminalEfiSuccess &&
            dataAddress != 0;
        if (!dataAllocated ||
            !IsValidM02OwnedGdtTssTerminalPage(dataAddress) ||
            M02OwnedGdtTssTerminalPagesOverlap(codeAddress, dataAddress))
        {
            return FailM02OwnedGdtTssTerminalBeforeEntry(
                console,
                serial,
                bootServices,
                codeAddress,
                codeAllocated,
                dataAddress,
                dataAllocated);
        }

        byte* codePage = (byte*)codeAddress;
        byte* dataPage = (byte*)dataAddress;
        for (ulong offset = 0;
            offset < M02OwnedGdtTssTerminalPageSize;
            offset++)
        {
            codePage[offset] = 0;
            dataPage[offset] = 0;
        }

        if (!IsM02OwnedGdtTssTerminalPageZero(codePage, 0) ||
            !IsM02OwnedGdtTssTerminalPageZero(dataPage, 0))
        {
            return FailM02OwnedGdtTssTerminalBeforeEntry(
                console,
                serial,
                bootServices,
                codeAddress,
                codeAllocated,
                dataAddress,
                dataAllocated);
        }

        OS.Kernel.Diagnostics.OwnedGdtTssTerminalContext* context =
            (OS.Kernel.Diagnostics.OwnedGdtTssTerminalContext*)dataAddress;
        OS.Kernel.Diagnostics.OwnedGdtTssTerminalStubResult prepared =
            OS.Kernel.Diagnostics
                .OwnedGDTTSSGeneratedAuditedCSharpStubBytes
                .PrepareContext(
                    context,
                    dataAddress,
                    M02OwnedGdtTssTerminalPageSize);
        OS.Kernel.Diagnostics.OwnedGdtTssTerminalStubResult emitted =
            OS.Kernel.Diagnostics
                .OwnedGDTTSSGeneratedAuditedCSharpStubBytes
                .Emit(
                    codePage,
                    codeAddress,
                    M02OwnedGdtTssTerminalPageSize,
                    context,
                    dataAddress,
                    M02OwnedGdtTssTerminalPageSize);
        OS.Kernel.Diagnostics.OwnedGdtTssTerminalStubResult validated =
            OS.Kernel.Diagnostics
                .OwnedGDTTSSGeneratedAuditedCSharpStubBytes
                .ValidateReadBack(
                    codePage,
                    codeAddress,
                    M02OwnedGdtTssTerminalPageSize,
                    context,
                    dataAddress,
                    M02OwnedGdtTssTerminalPageSize);

        if (!IsExactM02OwnedGdtTssTerminalPreparation(
                prepared,
                emitted,
                validated) ||
            !IsM02OwnedGdtTssTerminalPageZero(
                codePage,
                (ulong)OS.Kernel.Diagnostics
                    .OwnedGDTTSSGeneratedAuditedCSharpStubBytes
                    .StubByteCount) ||
            !IsM02OwnedGdtTssTerminalPageZero(
                dataPage,
                (ulong)OS.Kernel.Diagnostics
                    .OwnedGDTTSSGeneratedAuditedCSharpStubBytes
                    .ContextByteCount))
        {
            return FailM02OwnedGdtTssTerminalBeforeEntry(
                console,
                serial,
                bootServices,
                codeAddress,
                codeAllocated,
                dataAddress,
                dataAllocated);
        }

        s_m02OwnedGdtTssTerminalCodeAddress = codeAddress;
        s_m02OwnedGdtTssTerminalDataAddress = dataAddress;
        s_m02OwnedGdtTssTerminalState = M02OwnedGdtTssTerminalPreparedState;
        return true;
    }

    private static bool IsExactM02OwnedGdtTssTerminalPreparation(
        OS.Kernel.Diagnostics.OwnedGdtTssTerminalStubResult prepared,
        OS.Kernel.Diagnostics.OwnedGdtTssTerminalStubResult emitted,
        OS.Kernel.Diagnostics.OwnedGdtTssTerminalStubResult validated)
    {
        return OS.Kernel.Diagnostics
                .OwnedGDTTSSGeneratedAuditedCSharpStubBytes
                .ContextByteCount == 168 &&
            OS.Kernel.Diagnostics
                .OwnedGDTTSSGeneratedAuditedCSharpStubBytes
                .StubByteCount == 2987 &&
            OS.Kernel.Diagnostics
                .OwnedGDTTSSGeneratedAuditedCSharpStubBytes
                .StubFnv1A64 == 0xc9c0_4dee_967d_640aUL &&
            prepared.ContextPrepared &&
            prepared.ContextBytesWritten == 168 &&
            prepared.ContextBytesCompared == 168 &&
            prepared.StubBytesWritten == 0 &&
            prepared.StubBytesCompared == 0 &&
            prepared.FirstMismatchOffset == -1 &&
            prepared.ContextFingerprint != 0 &&
            emitted.StubEmitted &&
            emitted.ContextBytesWritten == 0 &&
            emitted.ContextBytesCompared == 168 &&
            emitted.StubBytesWritten == 2987 &&
            emitted.StubBytesCompared == 0 &&
            emitted.FirstMismatchOffset == -1 &&
            emitted.ContextFingerprint == prepared.ContextFingerprint &&
            emitted.StubFingerprint == 0xc9c0_4dee_967d_640aUL &&
            validated.ReadBackValidated &&
            validated.ContextBytesWritten == 0 &&
            validated.ContextBytesCompared == 168 &&
            validated.StubBytesWritten == 0 &&
            validated.StubBytesCompared == 2987 &&
            validated.FirstMismatchOffset == -1 &&
            validated.ContextFingerprint == prepared.ContextFingerprint &&
            validated.StubFingerprint == 0xc9c0_4dee_967d_640aUL;
    }

    private static bool ValidateM02OwnedGdtTssTerminalResources()
    {
        ulong codeAddress = s_m02OwnedGdtTssTerminalCodeAddress;
        ulong dataAddress = s_m02OwnedGdtTssTerminalDataAddress;
        if (s_m02OwnedGdtTssTerminalState !=
                M02OwnedGdtTssTerminalPreparedState ||
            !IsValidM02OwnedGdtTssTerminalPage(codeAddress) ||
            !IsValidM02OwnedGdtTssTerminalPage(dataAddress) ||
            M02OwnedGdtTssTerminalPagesOverlap(codeAddress, dataAddress))
        {
            return false;
        }

        byte* codePage = (byte*)codeAddress;
        byte* dataPage = (byte*)dataAddress;
        OS.Kernel.Diagnostics.OwnedGdtTssTerminalContext* context =
            (OS.Kernel.Diagnostics.OwnedGdtTssTerminalContext*)dataAddress;
        OS.Kernel.Diagnostics.OwnedGdtTssTerminalStubResult validated =
            OS.Kernel.Diagnostics
                .OwnedGDTTSSGeneratedAuditedCSharpStubBytes
                .ValidateReadBack(
                    codePage,
                    codeAddress,
                    M02OwnedGdtTssTerminalPageSize,
                    context,
                    dataAddress,
                    M02OwnedGdtTssTerminalPageSize);
        return validated.ReadBackValidated &&
            validated.ContextBytesCompared == 168 &&
            validated.StubBytesCompared == 2987 &&
            validated.ContextFingerprint != 0 &&
            validated.StubFingerprint == 0xc9c0_4dee_967d_640aUL &&
            IsM02OwnedGdtTssTerminalPageZero(codePage, 2987) &&
            IsM02OwnedGdtTssTerminalPageZero(dataPage, 168);
    }

    private static bool IsValidM02OwnedGdtTssTerminalPage(ulong address)
    {
        return address != 0 &&
            (address & (M02OwnedGdtTssTerminalPageSize - 1)) == 0 &&
            address <= ~0UL - M02OwnedGdtTssTerminalPageSize &&
            OS.Kernel.Diagnostics
                .OwnedGDTTSSGeneratedAuditedCSharpStubBytes
                .IsCanonicalX64Address(address) &&
            OS.Kernel.Diagnostics
                .OwnedGDTTSSGeneratedAuditedCSharpStubBytes
                .IsCanonicalX64Address(
                    address + M02OwnedGdtTssTerminalPageSize - 1);
    }

    private static bool M02OwnedGdtTssTerminalPagesOverlap(
        ulong codeAddress,
        ulong dataAddress)
    {
        ulong codeEnd = codeAddress + M02OwnedGdtTssTerminalPageSize;
        ulong dataEnd = dataAddress + M02OwnedGdtTssTerminalPageSize;
        return codeAddress < dataEnd && dataAddress < codeEnd;
    }

    private static bool IsM02OwnedGdtTssTerminalPageZero(
        byte* page,
        ulong startOffset)
    {
        if (page == null ||
            startOffset > M02OwnedGdtTssTerminalPageSize)
        {
            return false;
        }

        for (ulong offset = startOffset;
            offset < M02OwnedGdtTssTerminalPageSize;
            offset++)
        {
            if (page[offset] != 0)
            {
                return false;
            }
        }

        return true;
    }

    private static void EnterM02OwnedGdtTssTerminalProbe(
        EfiSimpleTextOutputProtocol* console,
        EfiSerialIoProtocol* serial,
        EfiBootServices* bootServices)
    {
        if (!ValidateM02OwnedGdtTssTerminalResources() ||
            !M01StageThreeAdapter
                .TryWriteM02OwnedGdtTssTerminalStartLine() ||
            X64InstructionPublication.Serialize() !=
                X64InstructionPublication.ExpectedSuccessCookie)
        {
            _ = FailM02OwnedGdtTssTerminalBeforeEntry(
                console,
                serial,
                bootServices,
                s_m02OwnedGdtTssTerminalCodeAddress,
                s_m02OwnedGdtTssTerminalCodeAddress != 0,
                s_m02OwnedGdtTssTerminalDataAddress,
                s_m02OwnedGdtTssTerminalDataAddress != 0);
            SpinM02OwnedGdtTssTerminalProbe();
        }

        s_m02OwnedGdtTssTerminalState = M02OwnedGdtTssTerminalEnteredState;
        delegate* unmanaged<
            OS.Kernel.Diagnostics.OwnedGdtTssTerminalContext*,
            void> terminalEntry =
                (delegate* unmanaged<
                    OS.Kernel.Diagnostics.OwnedGdtTssTerminalContext*,
                    void>)s_m02OwnedGdtTssTerminalCodeAddress;
        terminalEntry(
            (OS.Kernel.Diagnostics.OwnedGdtTssTerminalContext*)
                s_m02OwnedGdtTssTerminalDataAddress);

        _ = M01StageThreeAdapter
            .TryWriteM02OwnedGdtTssTerminalUnexpectedReturnLine();
        SpinM02OwnedGdtTssTerminalProbe();
    }

    private static byte GetM02OwnedGdtTssTerminalCleanupPlan(
        bool codeAllocated,
        ulong codeAddress,
        bool dataAllocated,
        ulong dataAddress)
    {
        byte plan = 0;
        if (dataAllocated)
        {
            plan |= 1;
        }

        if (codeAllocated &&
            (!dataAllocated || codeAddress != dataAddress))
        {
            plan |= 2;
        }

        return plan;
    }

    private static bool FailM02OwnedGdtTssTerminalBeforeEntry(
        EfiSimpleTextOutputProtocol* console,
        EfiSerialIoProtocol* serial,
        EfiBootServices* bootServices,
        ulong codeAddress,
        bool codeAllocated,
        ulong dataAddress,
        bool dataAllocated)
    {
        byte cleanupPlan = GetM02OwnedGdtTssTerminalCleanupPlan(
            codeAllocated,
            codeAddress,
            dataAllocated,
            dataAddress);
        if (bootServices != null && bootServices->FreePages != null)
        {
            if ((cleanupPlan & 1) != 0)
            {
                _ = bootServices->FreePages(
                    dataAddress,
                    M02OwnedGdtTssTerminalPageCount);
            }

            if ((cleanupPlan & 2) != 0)
            {
                _ = bootServices->FreePages(
                    codeAddress,
                    M02OwnedGdtTssTerminalPageCount);
            }
        }

        s_m02OwnedGdtTssTerminalCodeAddress = 0;
        s_m02OwnedGdtTssTerminalDataAddress = 0;
        s_m02OwnedGdtTssTerminalState = M02OwnedGdtTssTerminalFailedState;
        WriteM02OwnedGdtTssTerminalFirmwareLine(
            console,
            serial,
            M02OwnedGdtTssTerminalPreEntryFailureLine);
        return false;
    }

    private static void WriteM02OwnedGdtTssTerminalFirmwareLine(
        EfiSimpleTextOutputProtocol* console,
        EfiSerialIoProtocol* serial,
        int lineKind)
    {
        int length = GetM02OwnedGdtTssTerminalLineLength(lineKind);
        for (int index = 0; index < length; index++)
        {
            if (!TryGetM02OwnedGdtTssTerminalLineByte(
                    lineKind,
                    index,
                    out byte value))
            {
                return;
            }

            Write(console, serial, value);
        }

        WriteCrLf(console, serial);
    }

    private static int GetM02OwnedGdtTssTerminalLineLength(int lineKind)
    {
        return lineKind switch
        {
            M02OwnedGdtTssTerminalStartLine => 18,
            M02OwnedGdtTssTerminalPreEntryFailureLine => 33,
            M02OwnedGdtTssTerminalUnexpectedReturnLine => 35,
            _ => 0
        };
    }

    private static bool TryGetM02OwnedGdtTssTerminalLineByte(
        int lineKind,
        int index,
        out byte value)
    {
        if (lineKind == M02OwnedGdtTssTerminalStartLine)
        {
            value = index switch
            {
                0 => (byte)'T',
                1 => (byte)'E',
                2 => (byte)'S',
                3 => (byte)'T',
                4 => (byte)'_',
                5 => (byte)'S',
                6 => (byte)'T',
                7 => (byte)'A',
                8 => (byte)'R',
                9 => (byte)'T',
                10 => (byte)' ',
                11 => (byte)'m',
                12 => (byte)'0',
                13 => (byte)'2',
                14 => (byte)'.',
                15 => (byte)'0',
                16 => (byte)'0',
                17 => (byte)'9',
                _ => 0
            };
            return index >= 0 && index < 18;
        }

        if (index >= 0 && index < 18)
        {
            value = index switch
            {
                0 => (byte)'M',
                1 => (byte)'0',
                2 => (byte)'2',
                3 => (byte)'_',
                4 => (byte)'O',
                5 => (byte)'W',
                6 => (byte)'N',
                7 => (byte)'E',
                8 => (byte)'D',
                9 => (byte)'_',
                10 => (byte)'G',
                11 => (byte)'D',
                12 => (byte)'T',
                13 => (byte)'_',
                14 => (byte)'T',
                15 => (byte)'S',
                16 => (byte)'S',
                17 => (byte)'_',
                _ => 0
            };
            return true;
        }

        if (lineKind == M02OwnedGdtTssTerminalPreEntryFailureLine)
        {
            value = index switch
            {
                18 => (byte)'P',
                19 => (byte)'R',
                20 => (byte)'E',
                21 => (byte)'E',
                22 => (byte)'N',
                23 => (byte)'T',
                24 => (byte)'R',
                25 => (byte)'Y',
                26 => (byte)'_',
                27 => (byte)'F',
                28 => (byte)'A',
                29 => (byte)'I',
                30 => (byte)'L',
                31 => (byte)'E',
                32 => (byte)'D',
                _ => 0
            };
            return index >= 18 && index < 33;
        }

        if (lineKind == M02OwnedGdtTssTerminalUnexpectedReturnLine)
        {
            value = index switch
            {
                18 => (byte)'U',
                19 => (byte)'N',
                20 => (byte)'E',
                21 => (byte)'X',
                22 => (byte)'P',
                23 => (byte)'E',
                24 => (byte)'C',
                25 => (byte)'T',
                26 => (byte)'E',
                27 => (byte)'D',
                28 => (byte)'_',
                29 => (byte)'R',
                30 => (byte)'E',
                31 => (byte)'T',
                32 => (byte)'U',
                33 => (byte)'R',
                34 => (byte)'N',
                _ => 0
            };
            return index >= 18 && index < 35;
        }

        value = 0;
        return false;
    }

    private static void SpinM02OwnedGdtTssTerminalProbe()
    {
        while (true)
        {
        }
    }
#endif
#if M02_UNEXPECTED_VECTOR_PANIC_PROBE

#endif
#if M02_PAGE_FAULT_CR2_ERROR_CODE_PROBE

#endif
#if M02_DOUBLE_FAULT_IST_PROBE

#endif
#if M02_UD2_VECTOR_STATE_PROBE
    private static bool PrepareM02Ud2VectorStateProbe(
        EfiSimpleTextOutputProtocol* console,
        EfiSerialIoProtocol* serial,
        EfiBootServices* bootServices)
    {
        ulong codeAddress = 0;
        ulong dataAddress = 0;
        bool codeAllocated = false;
        bool dataAllocated = false;

        OS.Kernel.Diagnostics.DisabledDefaultUd2VectorStatePreTerminalResult
            serviceEvaluation = OS.Kernel.Diagnostics
                .DisabledDefaultUd2VectorStatePreTerminalContract
                .EvaluateServices(
                    s_m02Ud2VectorStateState,
                    bootServices != null,
                    bootServices != null &&
                        bootServices->AllocatePages != null,
                    bootServices != null &&
                        bootServices->FreePages != null);
        if (serviceEvaluation.Status == OS.Kernel.Diagnostics
                .DisabledDefaultUd2VectorStatePreTerminalStatus
                .ExistingState)
        {
            if (s_m02Ud2VectorStateState ==
                    M02Ud2VectorStatePreparedState &&
                bootServices != null &&
                bootServices->FreePages != null)
            {
                return FailM02Ud2VectorStateBeforeEntry(
                    console,
                    serial,
                    bootServices,
                    s_m02Ud2VectorStateCodeAddress,
                    true,
                    s_m02Ud2VectorStateDataAddress,
                    true);
            }

            return false;
        }

        if (!serviceEvaluation.Succeeded)
        {
            return FailM02Ud2VectorStateBeforeEntry(
                console,
                serial,
                bootServices,
                codeAddress,
                codeAllocated,
                dataAddress,
                dataAllocated);
        }

        ulong firmwareStatus = bootServices->AllocatePages(
            M02Ud2VectorStateAllocateAnyPages,
            M02Ud2VectorStateEfiLoaderCode,
            M02Ud2VectorStateCodePageCount,
            &codeAddress);
        codeAllocated = firmwareStatus == M02Ud2VectorStateEfiSuccess;
        OS.Kernel.Diagnostics.DisabledDefaultUd2VectorStatePreTerminalResult
            codeEvaluation = OS.Kernel.Diagnostics
                .DisabledDefaultUd2VectorStatePreTerminalContract
                .EvaluateCodeAllocation(firmwareStatus, codeAddress);
        if (!codeEvaluation.Succeeded)
        {
            return FailM02Ud2VectorStateBeforeEntry(
                console,
                serial,
                bootServices,
                codeAddress,
                codeAllocated,
                dataAddress,
                dataAllocated);
        }

        firmwareStatus = bootServices->AllocatePages(
            M02Ud2VectorStateAllocateAnyPages,
            M02Ud2VectorStateEfiLoaderData,
            M02Ud2VectorStateDataPageCount,
            &dataAddress);
        dataAllocated = firmwareStatus == M02Ud2VectorStateEfiSuccess;
        OS.Kernel.Diagnostics.DisabledDefaultUd2VectorStatePreTerminalResult
            dataEvaluation = OS.Kernel.Diagnostics
                .DisabledDefaultUd2VectorStatePreTerminalContract
                .EvaluateDataAllocation(
                    codeAddress,
                    firmwareStatus,
                    dataAddress);
        if (!dataEvaluation.Succeeded)
        {
            return FailM02Ud2VectorStateBeforeEntry(
                console,
                serial,
                bootServices,
                codeAddress,
                codeAllocated,
                dataAddress,
                dataAllocated);
        }

        byte* code = (byte*)codeAddress;
        byte* data = (byte*)dataAddress;
        for (ulong offset = 0;
            offset < M02Ud2VectorStateCodeByteCount;
            offset++)
        {
            code[offset] = 0;
        }
        for (ulong offset = 0;
            offset < M02Ud2VectorStateDataByteCount;
            offset++)
        {
            data[offset] = 0;
        }

        if (!IsM02Ud2VectorStateRangeZero(
                code,
                0,
                M02Ud2VectorStateCodeByteCount) ||
            !IsM02Ud2VectorStateRangeZero(
                data,
                0,
                M02Ud2VectorStateDataByteCount))
        {
            return FailM02Ud2VectorStateBeforeEntry(
                console,
                serial,
                bootServices,
                codeAddress,
                codeAllocated,
                dataAddress,
                dataAllocated);
        }

        ulong commonTargetAddress =
            codeAddress + M02Ud2VectorStatePageSize;
        ulong idtAddress = dataAddress;
        ulong contextAddress =
            dataAddress + M02Ud2VectorStatePageSize;
        ulong ist1StackTop =
            dataAddress + (3 * M02Ud2VectorStatePageSize);
        ulong ist2StackTop =
            dataAddress + (4 * M02Ud2VectorStatePageSize);
        ulong ist3StackTop =
            dataAddress + (5 * M02Ud2VectorStatePageSize);

        OS.Kernel.Diagnostics.NormalizeInterruptFramePageResult
            normalizedMaterialized =
                OS.Kernel.Diagnostics
                    .NormalizeErrorCodeNonErrorCodeInterruptFrame
                    .Materialize(
                        code,
                        M02Ud2VectorStatePageSize,
                        codeAddress,
                        commonTargetAddress);
        OS.Kernel.Diagnostics.NormalizeInterruptFramePageResult
            normalizedReadBack =
                OS.Kernel.Diagnostics
                    .NormalizeErrorCodeNonErrorCodeInterruptFrame
                    .ValidateReadBack(
                        code,
                        M02Ud2VectorStatePageSize,
                        codeAddress,
                        commonTargetAddress);
        OS.Kernel.Diagnostics.OwnedIdtGateTableResult idtMaterialized =
            OS.Kernel.Diagnostics
                .PopulateAllN256IDTVectorsOwnedGatesNever
                .Materialize(
                    data,
                    M02Ud2VectorStatePageSize,
                    idtAddress,
                    codeAddress);
        OS.Kernel.Diagnostics.OwnedIdtGateTableResult idtReadBack =
            OS.Kernel.Diagnostics
                .PopulateAllN256IDTVectorsOwnedGatesNever
                .ValidateReadBack(
                    data,
                    M02Ud2VectorStatePageSize,
                    idtAddress,
                    codeAddress);

        byte* contextBytes = data + M02Ud2VectorStatePageSize;
        byte* stream = code + M02Ud2VectorStatePageSize;
        OS.Kernel.Diagnostics.DisabledDefaultUd2VectorStateTransitionResult
            contextPrepared =
                OS.Kernel.Diagnostics
                    .DisabledDefaultUd2VectorStateTransition
                    .PrepareContext(
                        contextBytes,
                        contextAddress,
                        M02Ud2VectorStatePageSize,
                        idtAddress,
                        ist1StackTop,
                        ist2StackTop,
                        ist3StackTop);
        OS.Kernel.Diagnostics.DisabledDefaultUd2VectorStateTransitionResult
            streamEmitted =
                OS.Kernel.Diagnostics
                    .DisabledDefaultUd2VectorStateTransition
                    .EmitStream(
                        stream,
                        commonTargetAddress,
                        M02Ud2VectorStatePageSize,
                        contextBytes,
                        contextAddress,
                        M02Ud2VectorStatePageSize,
                        idtAddress,
                        ist1StackTop,
                        ist2StackTop,
                        ist3StackTop);
        OS.Kernel.Diagnostics.DisabledDefaultUd2VectorStateTransitionResult
            streamReadBack =
                OS.Kernel.Diagnostics
                    .DisabledDefaultUd2VectorStateTransition
                    .ValidateStreamReadBack(
                        stream,
                        commonTargetAddress,
                        M02Ud2VectorStatePageSize,
                        contextBytes,
                        contextAddress,
                        M02Ud2VectorStatePageSize,
                        idtAddress,
                        ist1StackTop,
                        ist2StackTop,
                        ist3StackTop);
        OS.Kernel.Diagnostics.OwnedX64GdtVerificationResult gdtReadBack =
            OS.Kernel.Diagnostics
                .OwnedX64GdtContainingNullN64BitCode
                .Verify(
                    contextBytes + 24,
                    40,
                    contextAddress + 64,
                    103);

        bool materializationSucceeded =
            M02Ud2VectorStateNormalizedResultMatches(
                normalizedMaterialized,
                OS.Kernel.Diagnostics.NormalizeInterruptFramePageOperation
                    .Materialize,
                codeAddress,
                commonTargetAddress,
                true) &&
            M02Ud2VectorStateIdtResultMatches(
                idtMaterialized,
                OS.Kernel.Diagnostics.OwnedIdtGateTableOperation
                    .Materialize,
                idtAddress,
                codeAddress,
                true) &&
            M02Ud2VectorStateTransitionResultMatches(
                contextPrepared,
                OS.Kernel.Diagnostics
                    .DisabledDefaultUd2VectorStateTransitionOperation
                    .PrepareContext,
                contextAddress,
                0,
                idtAddress,
                true) &&
            M02Ud2VectorStateTransitionResultMatches(
                streamEmitted,
                OS.Kernel.Diagnostics
                    .DisabledDefaultUd2VectorStateTransitionOperation
                    .EmitStream,
                contextAddress,
                commonTargetAddress,
                idtAddress,
                true) &&
            contextPrepared.ContextFingerprint ==
                streamEmitted.ContextFingerprint;
        OS.Kernel.Diagnostics.DisabledDefaultUd2VectorStatePreTerminalResult
            materializationEvaluation = OS.Kernel.Diagnostics
                .DisabledDefaultUd2VectorStatePreTerminalContract
                .EvaluateMaterialization(
                    codeAddress,
                    dataAddress,
                    materializationSucceeded);
        if (!materializationEvaluation.Succeeded)
        {
            return FailM02Ud2VectorStateBeforeEntry(
                console,
                serial,
                bootServices,
                codeAddress,
                codeAllocated,
                dataAddress,
                dataAllocated);
        }

        bool readBackSucceeded =
            M02Ud2VectorStateNormalizedResultMatches(
                normalizedReadBack,
                OS.Kernel.Diagnostics.NormalizeInterruptFramePageOperation
                    .ValidateReadBack,
                codeAddress,
                commonTargetAddress,
                false) &&
            normalizedMaterialized.Fingerprint ==
                normalizedReadBack.Fingerprint &&
            M02Ud2VectorStateIdtResultMatches(
                idtReadBack,
                OS.Kernel.Diagnostics.OwnedIdtGateTableOperation
                    .ValidateReadBack,
                idtAddress,
                codeAddress,
                false) &&
            idtMaterialized.Fingerprint == idtReadBack.Fingerprint &&
            M02Ud2VectorStateTransitionResultMatches(
                streamReadBack,
                OS.Kernel.Diagnostics
                    .DisabledDefaultUd2VectorStateTransitionOperation
                    .ValidateStreamReadBack,
                contextAddress,
                commonTargetAddress,
                idtAddress,
                false) &&
            contextPrepared.ContextFingerprint ==
                streamReadBack.ContextFingerprint &&
            streamEmitted.StreamFingerprint ==
                streamReadBack.StreamFingerprint &&
            M02Ud2VectorStateGdtResultMatches(gdtReadBack) &&
            IsM02Ud2VectorStateRangeZero(
                contextBytes,
                (ulong)OS.Kernel.Diagnostics
                    .DisabledDefaultUd2VectorStateTransition
                    .ContextByteCount,
                M02Ud2VectorStatePageSize) &&
            AreM02Ud2VectorStateIstPagesZero(data);
        OS.Kernel.Diagnostics.DisabledDefaultUd2VectorStatePreTerminalResult
            readBackEvaluation = OS.Kernel.Diagnostics
                .DisabledDefaultUd2VectorStatePreTerminalContract
                .EvaluateReadBack(
                    codeAddress,
                    dataAddress,
                    readBackSucceeded);
        if (!readBackEvaluation.Succeeded)
        {
            return FailM02Ud2VectorStateBeforeEntry(
                console,
                serial,
                bootServices,
                codeAddress,
                codeAllocated,
                dataAddress,
                dataAllocated);
        }

        s_m02Ud2VectorStateCodeAddress = codeAddress;
        s_m02Ud2VectorStateDataAddress = dataAddress;
        s_m02Ud2VectorStateState = M02Ud2VectorStatePreparedState;
        return true;
    }

    private static bool ValidateM02Ud2VectorStateResources()
    {
        ulong codeAddress = s_m02Ud2VectorStateCodeAddress;
        ulong dataAddress = s_m02Ud2VectorStateDataAddress;
        if (s_m02Ud2VectorStateState !=
                M02Ud2VectorStatePreparedState ||
            !IsValidM02Ud2VectorStateRange(
                codeAddress,
                M02Ud2VectorStateCodeByteCount) ||
            !IsValidM02Ud2VectorStateRange(
                dataAddress,
                M02Ud2VectorStateDataByteCount) ||
            M02Ud2VectorStateRangesOverlap(
                codeAddress,
                M02Ud2VectorStateCodeByteCount,
                dataAddress,
                M02Ud2VectorStateDataByteCount))
        {
            return false;
        }

        byte* code = (byte*)codeAddress;
        byte* data = (byte*)dataAddress;
        byte* stream = code + M02Ud2VectorStatePageSize;
        byte* contextBytes = data + M02Ud2VectorStatePageSize;
        ulong commonTargetAddress =
            codeAddress + M02Ud2VectorStatePageSize;
        ulong idtAddress = dataAddress;
        ulong contextAddress =
            dataAddress + M02Ud2VectorStatePageSize;
        ulong ist1StackTop =
            dataAddress + (3 * M02Ud2VectorStatePageSize);
        ulong ist2StackTop =
            dataAddress + (4 * M02Ud2VectorStatePageSize);
        ulong ist3StackTop =
            dataAddress + (5 * M02Ud2VectorStatePageSize);

        OS.Kernel.Diagnostics.NormalizeInterruptFramePageResult
            normalizedReadBack =
                OS.Kernel.Diagnostics
                    .NormalizeErrorCodeNonErrorCodeInterruptFrame
                    .ValidateReadBack(
                        code,
                        M02Ud2VectorStatePageSize,
                        codeAddress,
                        commonTargetAddress);
        OS.Kernel.Diagnostics.OwnedIdtGateTableResult idtReadBack =
            OS.Kernel.Diagnostics
                .PopulateAllN256IDTVectorsOwnedGatesNever
                .ValidateReadBack(
                    data,
                    M02Ud2VectorStatePageSize,
                    idtAddress,
                    codeAddress);
        OS.Kernel.Diagnostics.DisabledDefaultUd2VectorStateTransitionResult
            streamReadBack =
                OS.Kernel.Diagnostics
                    .DisabledDefaultUd2VectorStateTransition
                    .ValidateStreamReadBack(
                        stream,
                        commonTargetAddress,
                        M02Ud2VectorStatePageSize,
                        contextBytes,
                        contextAddress,
                        M02Ud2VectorStatePageSize,
                        idtAddress,
                        ist1StackTop,
                        ist2StackTop,
                        ist3StackTop);
        OS.Kernel.Diagnostics.OwnedX64GdtVerificationResult gdtReadBack =
            OS.Kernel.Diagnostics
                .OwnedX64GdtContainingNullN64BitCode
                .Verify(
                    contextBytes + 24,
                    40,
                    contextAddress + 64,
                    103);

        return M02Ud2VectorStateNormalizedResultMatches(
                normalizedReadBack,
                OS.Kernel.Diagnostics.NormalizeInterruptFramePageOperation
                    .ValidateReadBack,
                codeAddress,
                commonTargetAddress,
                false) &&
            M02Ud2VectorStateIdtResultMatches(
                idtReadBack,
                OS.Kernel.Diagnostics.OwnedIdtGateTableOperation
                    .ValidateReadBack,
                idtAddress,
                codeAddress,
                false) &&
            M02Ud2VectorStateTransitionResultMatches(
                streamReadBack,
                OS.Kernel.Diagnostics
                    .DisabledDefaultUd2VectorStateTransitionOperation
                    .ValidateStreamReadBack,
                contextAddress,
                commonTargetAddress,
                idtAddress,
                false) &&
            M02Ud2VectorStateGdtResultMatches(gdtReadBack) &&
            IsM02Ud2VectorStateRangeZero(
                contextBytes,
                (ulong)OS.Kernel.Diagnostics
                    .DisabledDefaultUd2VectorStateTransition
                    .ContextByteCount,
                M02Ud2VectorStatePageSize) &&
            AreM02Ud2VectorStateIstPagesZero(data);
    }

    private static bool M02Ud2VectorStateNormalizedResultMatches(
        OS.Kernel.Diagnostics.NormalizeInterruptFramePageResult result,
        OS.Kernel.Diagnostics.NormalizeInterruptFramePageOperation operation,
        ulong pageAddress,
        ulong commonTargetAddress,
        bool destinationMutated)
    {
        return result.IsInitialized &&
            result.Operation == operation &&
            result.Status ==
                OS.Kernel.Diagnostics.NormalizeInterruptFramePageStatus
                    .Success &&
            result.ModeledPageAddress == pageAddress &&
            result.CommonTargetAddress == commonTargetAddress &&
            result.PageCapacity == M02Ud2VectorStatePageSize &&
            result.RequiredBytes == (int)M02Ud2VectorStatePageSize &&
            result.BytesWritten ==
                (destinationMutated
                    ? (int)M02Ud2VectorStatePageSize
                    : 0) &&
            result.BytesCompared == (int)M02Ud2VectorStatePageSize &&
            result.FirstMismatchOffset == -1 &&
            result.HardwareErrorCodeVectorCount == 9 &&
            result.SyntheticErrorCodeVectorCount == 247 &&
            result.Fingerprint != 0 &&
            result.DestinationMutated == destinationMutated &&
            result.ReadBackPerformed &&
            (destinationMutated
                ? result.Materialized
                : result.ReadBackValidated) &&
            result.Succeeded;
    }

    private static bool M02Ud2VectorStateIdtResultMatches(
        OS.Kernel.Diagnostics.OwnedIdtGateTableResult result,
        OS.Kernel.Diagnostics.OwnedIdtGateTableOperation operation,
        ulong tableAddress,
        ulong stubPageAddress,
        bool destinationMutated)
    {
        return result.IsInitialized &&
            result.Operation == operation &&
            result.Status ==
                OS.Kernel.Diagnostics.OwnedIdtGateTableStatus.Success &&
            result.ModeledTableAddress == tableAddress &&
            result.OwnedStubPageAddress == stubPageAddress &&
            result.TableCapacity == M02Ud2VectorStatePageSize &&
            result.RequiredBytes == (int)M02Ud2VectorStatePageSize &&
            result.DescriptorCount == 256 &&
            result.PresentDescriptorCount == 256 &&
            result.IstRoutedDescriptorCount == 3 &&
            result.DefaultIstDescriptorCount == 253 &&
            result.FirmwareGateCopyCount == 0 &&
            result.BytesWritten ==
                (destinationMutated
                    ? (int)M02Ud2VectorStatePageSize
                    : 0) &&
            result.BytesCompared == (int)M02Ud2VectorStatePageSize &&
            result.FirstMismatchOffset == -1 &&
            result.Fingerprint != 0 &&
            result.NormalizedPolicyFingerprint ==
                OS.Kernel.Diagnostics
                    .PopulateAllN256IDTVectorsOwnedGatesNever
                    .NormalizedPolicyFnv1A64 &&
            result.DestinationMutated == destinationMutated &&
            result.ReadBackPerformed &&
            (destinationMutated
                ? result.Materialized
                : result.ReadBackValidated) &&
            result.Succeeded;
    }

    private static bool M02Ud2VectorStateTransitionResultMatches(
        OS.Kernel.Diagnostics
            .DisabledDefaultUd2VectorStateTransitionResult result,
        OS.Kernel.Diagnostics
            .DisabledDefaultUd2VectorStateTransitionOperation operation,
        ulong contextAddress,
        ulong streamAddress,
        ulong idtAddress,
        bool destinationMutated)
    {
        int contextBytes = OS.Kernel.Diagnostics
            .DisabledDefaultUd2VectorStateTransition.ContextByteCount;
        int streamBytes = OS.Kernel.Diagnostics
            .DisabledDefaultUd2VectorStateTransition.StreamByteCount;
        bool preparing = operation == OS.Kernel.Diagnostics
            .DisabledDefaultUd2VectorStateTransitionOperation
            .PrepareContext;
        bool emitting = operation == OS.Kernel.Diagnostics
            .DisabledDefaultUd2VectorStateTransitionOperation.EmitStream;

        return result.IsInitialized &&
            result.Operation == operation &&
            result.Status == OS.Kernel.Diagnostics
                .DisabledDefaultUd2VectorStateTransitionStatus.Success &&
            result.ModeledContextAddress == contextAddress &&
            result.ModeledStreamAddress == streamAddress &&
            result.OwnedIdtAddress == idtAddress &&
            result.ContextCapacity == M02Ud2VectorStatePageSize &&
            result.StreamCapacity ==
                (preparing ? 0 : M02Ud2VectorStatePageSize) &&
            result.ContextBytesWritten ==
                (preparing ? contextBytes : 0) &&
            result.ContextBytesCompared == contextBytes &&
            result.StreamBytesWritten == (emitting ? streamBytes : 0) &&
            result.StreamBytesCompared ==
                (preparing ? 0 : streamBytes) &&
            result.FirstMismatchOffset == -1 &&
            result.ContextFingerprint != 0 &&
            result.StreamFingerprint ==
                (preparing ? 0 : result.ExpectedStreamFingerprint) &&
            result.DestinationMutated == destinationMutated &&
            result.ReadBackPerformed &&
            (preparing
                ? result.ContextPrepared
                : emitting
                    ? result.StreamEmitted
                    : result.ReadBackValidated) &&
            result.Succeeded;
    }

    private static bool M02Ud2VectorStateGdtResultMatches(
        OS.Kernel.Diagnostics.OwnedX64GdtVerificationResult result)
    {
        return result.IsInitialized &&
            result.Status == OS.Kernel.Diagnostics
                .OwnedX64GdtVerificationStatus.Success &&
            result.BytesCompared == 40 &&
            result.FirstMismatchOffset == -1 &&
            result.ObservedFingerprint != 0 &&
            result.ObservedFingerprint == result.ExpectedFingerprint &&
            result.Succeeded;
    }

    private static bool IsValidM02Ud2VectorStateRange(
        ulong address,
        ulong byteCount)
    {
        return OS.Kernel.Diagnostics
            .DisabledDefaultUd2VectorStatePreTerminalContract
            .IsValidRange(address, byteCount);
    }

    private static bool M02Ud2VectorStateRangesOverlap(
        ulong firstAddress,
        ulong firstByteCount,
        ulong secondAddress,
        ulong secondByteCount)
    {
        return OS.Kernel.Diagnostics
            .DisabledDefaultUd2VectorStatePreTerminalContract
            .RangesOverlap(
                firstAddress,
                firstByteCount,
                secondAddress,
                secondByteCount);
    }

    private static bool IsM02Ud2VectorStateRangeZero(
        byte* source,
        ulong startOffset,
        ulong endOffset)
    {
        if (source == null || startOffset > endOffset)
        {
            return false;
        }

        for (ulong offset = startOffset; offset < endOffset; offset++)
        {
            if (source[offset] != 0)
            {
                return false;
            }
        }

        return true;
    }

    private static bool AreM02Ud2VectorStateIstPagesZero(byte* data)
    {
        return IsM02Ud2VectorStateRangeZero(
                data,
                2 * M02Ud2VectorStatePageSize,
                3 * M02Ud2VectorStatePageSize) &&
            IsM02Ud2VectorStateRangeZero(
                data,
                3 * M02Ud2VectorStatePageSize,
                4 * M02Ud2VectorStatePageSize) &&
            IsM02Ud2VectorStateRangeZero(
                data,
                4 * M02Ud2VectorStatePageSize,
                5 * M02Ud2VectorStatePageSize);
    }

    private static void EnterM02Ud2VectorStateProbe(
        EfiSimpleTextOutputProtocol* console,
        EfiSerialIoProtocol* serial,
        EfiBootServices* bootServices)
    {
        OS.Kernel.Diagnostics.DisabledDefaultUd2VectorStatePreTerminalResult
            readBackEvaluation = OS.Kernel.Diagnostics
                .DisabledDefaultUd2VectorStatePreTerminalContract
                .EvaluateReadBack(
                    s_m02Ud2VectorStateCodeAddress,
                    s_m02Ud2VectorStateDataAddress,
                    ValidateM02Ud2VectorStateResources());
        if (!readBackEvaluation.Succeeded)
        {
            _ = FailM02Ud2VectorStateBeforeEntry(
                console,
                serial,
                bootServices,
                s_m02Ud2VectorStateCodeAddress,
                s_m02Ud2VectorStateCodeAddress != 0,
                s_m02Ud2VectorStateDataAddress,
                s_m02Ud2VectorStateDataAddress != 0);
            SpinM02Ud2VectorStateProbe();
        }

        OS.Kernel.Diagnostics.DisabledDefaultUd2VectorStatePreTerminalResult
            markerEvaluation = OS.Kernel.Diagnostics
                .DisabledDefaultUd2VectorStatePreTerminalContract
                .EvaluateMarkerTransport(
                    s_m02Ud2VectorStateCodeAddress,
                    s_m02Ud2VectorStateDataAddress,
                    M01StageThreeAdapter
                        .TryWriteM02Ud2VectorStateStartLine());
        if (!markerEvaluation.Succeeded)
        {
            _ = FailM02Ud2VectorStateBeforeEntry(
                console,
                serial,
                bootServices,
                s_m02Ud2VectorStateCodeAddress,
                s_m02Ud2VectorStateCodeAddress != 0,
                s_m02Ud2VectorStateDataAddress,
                s_m02Ud2VectorStateDataAddress != 0);
            SpinM02Ud2VectorStateProbe();
        }

        delegate* unmanaged<
            OS.Kernel.Diagnostics
                .DisabledDefaultUd2VectorStateTransitionContext*,
            void> terminalEntry =
                (delegate* unmanaged<
                    OS.Kernel.Diagnostics
                        .DisabledDefaultUd2VectorStateTransitionContext*,
                    void>)(s_m02Ud2VectorStateCodeAddress +
                        (ulong)OS.Kernel.Diagnostics
                            .DisabledDefaultUd2VectorStateTransition
                            .TransitionEntryCompositeOffset);
        OS.Kernel.Diagnostics.DisabledDefaultUd2VectorStateTransitionContext*
            context =
                (OS.Kernel.Diagnostics
                    .DisabledDefaultUd2VectorStateTransitionContext*)
                    (s_m02Ud2VectorStateDataAddress +
                        M02Ud2VectorStatePageSize);

        OS.Kernel.Diagnostics.DisabledDefaultUd2VectorStatePreTerminalResult
            publicationEvaluation = OS.Kernel.Diagnostics
                .DisabledDefaultUd2VectorStatePreTerminalContract
                .EvaluatePublication(
                    s_m02Ud2VectorStateCodeAddress,
                    s_m02Ud2VectorStateDataAddress,
                    X64InstructionPublication.Serialize() ==
                        X64InstructionPublication.ExpectedSuccessCookie);
        if (!publicationEvaluation.Succeeded)
        {
            _ = FailM02Ud2VectorStateBeforeEntry(
                console,
                serial,
                bootServices,
                s_m02Ud2VectorStateCodeAddress,
                s_m02Ud2VectorStateCodeAddress != 0,
                s_m02Ud2VectorStateDataAddress,
                s_m02Ud2VectorStateDataAddress != 0);
            SpinM02Ud2VectorStateProbe();
        }
        s_m02Ud2VectorStateState = M02Ud2VectorStateEnteredState;
        terminalEntry(context);

        SpinM02Ud2VectorStateProbe();
    }

    private static byte GetM02Ud2VectorStateCleanupPlan(
        bool codeAllocated,
        ulong codeAddress,
        bool dataAllocated,
        ulong dataAddress)
    {
        return OS.Kernel.Diagnostics
            .DisabledDefaultUd2VectorStatePreTerminalContract
            .PlanCleanup(
                codeAllocated,
                codeAddress,
                dataAllocated,
                dataAddress);
    }

    private static bool
        M02Ud2VectorStateAllocationRangesDefinitelyDisjoint(
            ulong codeAddress,
            ulong dataAddress)
    {
        return OS.Kernel.Diagnostics
            .DisabledDefaultUd2VectorStatePreTerminalContract
            .AllocationRangesDefinitelyDisjoint(codeAddress, dataAddress);
    }

    private static bool FailM02Ud2VectorStateBeforeEntry(
        EfiSimpleTextOutputProtocol* console,
        EfiSerialIoProtocol* serial,
        EfiBootServices* bootServices,
        ulong codeAddress,
        bool codeAllocated,
        ulong dataAddress,
        bool dataAllocated)
    {
        byte cleanupPlan = GetM02Ud2VectorStateCleanupPlan(
            codeAllocated,
            codeAddress,
            dataAllocated,
            dataAddress);
        if (bootServices != null && bootServices->FreePages != null)
        {
            if ((cleanupPlan & 1) != 0)
            {
                _ = bootServices->FreePages(
                    dataAddress,
                    M02Ud2VectorStateDataPageCount);
            }

            if ((cleanupPlan & 2) != 0)
            {
                _ = bootServices->FreePages(
                    codeAddress,
                    M02Ud2VectorStateCodePageCount);
            }
        }

        s_m02Ud2VectorStateCodeAddress = 0;
        s_m02Ud2VectorStateDataAddress = 0;
        s_m02Ud2VectorStateState = M02Ud2VectorStateFailedState;
        WriteM02Ud2VectorStateFirmwareFailureLines(console, serial);
        return false;
    }

    private static void WriteM02Ud2VectorStateFirmwareFailureLines(
        EfiSimpleTextOutputProtocol* console,
        EfiSerialIoProtocol* serial)
    {
        WriteM02Ud2VectorStateFirmwareFailureLine(
            console,
            serial,
            M02Ud2VectorStateFailureHeadingLine);
        WriteM02Ud2VectorStateFirmwareFailureLine(
            console,
            serial,
            M02Ud2VectorStateFailureDetailLine);
    }

    private static void WriteM02Ud2VectorStateFirmwareFailureLine(
        EfiSimpleTextOutputProtocol* console,
        EfiSerialIoProtocol* serial,
        int lineKind)
    {
        int length = lineKind == M02Ud2VectorStateFailureHeadingLine
            ? 34
            : lineKind == M02Ud2VectorStateFailureDetailLine
                ? 45
                : 0;
        for (int index = 0; index < length; index++)
        {
            if (!TryGetM02Ud2VectorStateFailureLineByte(
                    lineKind,
                    index,
                    out byte value))
            {
                return;
            }

            Write(console, serial, value);
        }

        WriteCrLf(console, serial);
    }

    private static bool TryGetM02Ud2VectorStateFailureLineByte(
        int lineKind,
        int index,
        out byte value)
    {
        int length = lineKind == M02Ud2VectorStateFailureHeadingLine
            ? 34
            : lineKind == M02Ud2VectorStateFailureDetailLine
                ? 45
                : 0;
        if (index < 0 || index >= length)
        {
            value = 0;
            return false;
        }

        int wordIndex = index >> 3;
        ulong word;
        if (lineKind == M02Ud2VectorStateFailureHeadingLine)
        {
            word = wordIndex switch
            {
                0 => 0x5f32_4455_5f32_304dUL,
                1 => 0x535f_524f_5443_4556UL,
                2 => 0x5245_565f_4554_4154UL,
                3 => 0x4c49_4146_5f59_4649UL,
                4 => 0x0000_0000_0000_4445UL,
                _ => 0
            };
        }
        else
        {
            word = wordIndex switch
            {
                0 => 0x4941_465f_5453_4554UL,
                1 => 0x3130_2e32_306d_204cUL,
                2 => 0x726f_7463_6576_2033UL,
                3 => 0x6967_6572_5f72_6f5fUL,
                4 => 0x7369_6d5f_7265_7473UL,
                5 => 0x0000_0068_6374_616dUL,
                _ => 0
            };
        }

        value = (byte)(word >> ((index & 7) * 8));
        return true;
    }

    internal static bool TryGetM02Ud2VectorStateStartLineByte(
        int index,
        out byte value)
    {
        value = index switch
        {
            0 => (byte)'T',
            1 => (byte)'E',
            2 => (byte)'S',
            3 => (byte)'T',
            4 => (byte)'_',
            5 => (byte)'S',
            6 => (byte)'T',
            7 => (byte)'A',
            8 => (byte)'R',
            9 => (byte)'T',
            10 => (byte)' ',
            11 => (byte)'m',
            12 => (byte)'0',
            13 => (byte)'2',
            14 => (byte)'.',
            15 => (byte)'0',
            16 => (byte)'1',
            17 => (byte)'3',
            _ => 0
        };
        return index >= 0 && index < 18;
    }

    private static void SpinM02Ud2VectorStateProbe()
    {
        while (true)
        {
        }
    }
#endif
    [System.Runtime.RuntimeExport("EfiMain")]
    private static long EfiMain(void* imageHandle, EfiSystemTable* systemTable)
    {
#if (M03_FIRST_REAL_EXIT_BOOT_SERVICES_PROBE || CLROS_NORMAL_VERIFIED_EXIT)
        if (!M03FirstExitAdapter.CaptureOriginalEntry(imageHandle, systemTable))
            return 0;
#endif
        EfiSimpleTextOutputProtocol* console = GetFirmwareConsole(systemTable);
        EfiSerialIoProtocol* serial = console == null ? GetFirmwareSerial(systemTable) : null;
#if M02_OWNED_GDT_TSS_TERMINAL_PROBE
        if (!PrepareM02OwnedGdtTssTerminalProbe(
                console,
                serial,
                systemTable))
        {
            return 0;
        }
#endif
        WriteBootMarkers(imageHandle, console, serial, systemTable);
#if M02_OWNED_GDT_TSS_TERMINAL_PROBE
        _ = FailM02OwnedGdtTssTerminalBeforeEntry(
            console,
            serial,
            systemTable == null ? null : systemTable->BootServices,
            s_m02OwnedGdtTssTerminalCodeAddress,
            s_m02OwnedGdtTssTerminalCodeAddress != 0,
            s_m02OwnedGdtTssTerminalDataAddress,
            s_m02OwnedGdtTssTerminalDataAddress != 0);
#endif
        return 0;
    }

    private static void Main()
    {
    }

    private static EfiSimpleTextOutputProtocol* GetFirmwareConsole(EfiSystemTable* systemTable)
    {
        if (systemTable == null || systemTable->ConOut == null || systemTable->ConOut->OutputString == null)
        {
            return null;
        }

        return systemTable->ConOut;
    }

    private static EfiSerialIoProtocol* GetFirmwareSerial(EfiSystemTable* systemTable)
    {
        if (systemTable == null || systemTable->BootServices == null || systemTable->BootServices->LocateProtocol == null)
        {
            return null;
        }

        EfiGuid serialIoGuid;
        serialIoGuid.Data1 = 0xbb25cf6f;
        serialIoGuid.Data2 = 0xf1d4;
        serialIoGuid.Data3 = 0x11d2;
        serialIoGuid.Data4_0 = 0x9a;
        serialIoGuid.Data4_1 = 0x0c;
        serialIoGuid.Data4_2 = 0x00;
        serialIoGuid.Data4_3 = 0x90;
        serialIoGuid.Data4_4 = 0x27;
        serialIoGuid.Data4_5 = 0x3f;
        serialIoGuid.Data4_6 = 0xc1;
        serialIoGuid.Data4_7 = 0xfd;
        void* serialInterface = null;
        ulong status = systemTable->BootServices->LocateProtocol(&serialIoGuid, null, &serialInterface);
        if (status != 0 || serialInterface == null)
        {
            return null;
        }

        EfiSerialIoProtocol* serial = (EfiSerialIoProtocol*)serialInterface;
        if (serial->Write == null)
        {
            return null;
        }

        return serial;
    }

    private static void WriteBootMarkers(void* imageHandle, EfiSimpleTextOutputProtocol* console, EfiSerialIoProtocol* serial, EfiSystemTable* systemTable)
    {
        WriteEfiMainStart(console, serial);
        WriteEfiConsoleReady(console, serial);
#if M04_INDEPENDENT_ENDIAN_CONFORMANCE_PROBE
        if (!WriteM04IndependentEndianConformanceProbe(console, serial))
        {
            return;
        }
#endif
#if M04_SPAN_OVERLAP_MEMORY_CAST_UNSAFE_WIDTH_CONFORMANCE_PROBE
        if (!WriteM04SpanMemoryConformanceProbe(console, serial))
        {
            return;
        }
#endif
#if M04_ARITHMETIC_CONFORMANCE_PROBE
        if (!WriteM04ArithmeticConformanceProbe(console, serial))
        {
            return;
        }
#endif
#if M04_LAYOUT_CONTRACT_PROBE
        if (!WriteM04LayoutContractProbe(console, serial))
        {
            return;
        }
#endif
#if M02_NORMALIZED_INTERRUPT_FRAME_PAGE_PROBE
        if (!WriteM02NormalizedInterruptFramePageProbe(
                console,
                serial,
                systemTable))
        {
            return;
        }
#endif
#if M02_OWNED_IDT_GATE_POPULATION_PROBE
        if (!WriteM02OwnedIdtGatePopulationProbe(
                console,
                serial,
                systemTable))
        {
            return;
        }
#endif
#if M02_IDTR_CONTINUED_EXECUTION_PROBE
        if (!WriteM02IdtrContinuedExecutionProbe(
                console,
                serial,
                systemTable))
        {
            return;
        }
#endif
#if M03_MEMORY_MAP_CAPTURE_PROBE
        M03MemoryMapCaptureObserver.Start(console, serial);
#endif
        if (TryCaptureUefiMemoryMap(systemTable))
        {
#if M03_MEMORY_MAP_CAPTURE_PROBE
            M03MemoryMapCaptureObserver.Report(console, serial, true);
#endif
            WriteUefiMemoryMapReady(console, serial);
            WriteUefiMemoryMapDescriptorCount(console, serial, s_memoryMapSnapshot.DescriptorCount);
            if (s_memoryMapSnapshot.IsOwned)
            {
                WriteUefiMemoryMapOwned(console, serial);
                EfiBootHandoffStatus handoffStatus = EfiBootHandoffValidator.Validate(s_bootHandoff);
                if (handoffStatus != EfiBootHandoffStatus.Success)
                {
                    WriteBootHandoffFail(console, serial, handoffStatus);
                    return;
                }

                WriteBootHandoffReady(console, serial);
                EfiMemoryMapNormalizeResult normalizeResult = EfiMemoryMapNormalizer.Normalize(s_memoryMapSnapshot.Buffer, s_memoryMapSnapshot.MapSize, s_memoryMapSnapshot.DescriptorSize, s_memoryMapSnapshot.DescriptorVersion, s_memoryMapSnapshot.DescriptorCount);
                if (!normalizeResult.Succeeded)
                {
                    WriteUefiMemoryMapNormalizeFail(console, serial, normalizeResult.Status);
                    return;
                }

                s_memoryMapSummary = normalizeResult.Summary;
                WriteUefiMemoryMapNormalized(console, serial);
                BootMemoryMapAcceptanceResult memoryMapAcceptance = BootMemoryMapAcceptance.Validate(s_memoryMapSummary);
                if (!memoryMapAcceptance.Succeeded)
                {
                    WriteBootMemoryMapRejected(console, serial, memoryMapAcceptance.Status);
                    return;
                }

                WriteBootMemoryMapAccepted(console, serial);
                if (!MaterializeAndWriteBootMemoryRegions(console, serial))
                {
                    return;
                }

                if (!ScanAndWriteAcpiRsdp(console, serial, systemTable))
                {
                    return;
                }

#if (ACPI_PHYSICAL_DISCOVERY_PROBE || CLROS_NORMAL_VERIFIED_EXIT)
                if (!WriteAcpiPhysicalDiscoveryProbe(console, serial))
                {
                    return;
                }
#endif
#if M03_FIRMWARE_ENTROPY_SEED_PROBE
                if (!FirmwareEntropyCapture.Run(systemTable, console, serial))
                {
                    // The ordinary runner still reports failure; a separate raw
                    // observer can establish terminal quiet without firmware return.
                    while (true)
                    {
                    }
                }
#endif
#if M03_TYPED_EXIT_BOOT_SERVICES_CALL_PROBE
                if (!TypedExitBootServicesCallProbe.Run(imageHandle, console, serial))
                {
                    return;
                }
                if (!FinalMapRetryProbe.Run(imageHandle, systemTable->BootServices, console, serial))
                {
                    return;
                }
#endif
#if EXIT_BOOT_SERVICES_FRESH_MAP_PROBE
                if (!WriteExitBootServicesFreshMapProbe(console, serial, systemTable))
                {
                    return;
                }
#endif
                if (M01StageOneAdapter.Run(imageHandle, console, serial, systemTable))
                {
                    return;
                }
            }
        }
        else
        {
#if M03_MEMORY_MAP_CAPTURE_PROBE
            M03MemoryMapCaptureObserver.Report(console, serial, false);
            return;
#else
            WriteUefiMemoryMapFail(console, serial);
            if (s_memoryMapCopyStatus != UefiMemoryMapCopyPlanStatus.Success)
            {
                WriteUefiMemoryMapCopyFail(console, serial, s_memoryMapCopyStatus);
            }
#endif
        }

#if UEFI_GONE_SIMULATION_PROBE
        WriteUefiGoneSimulation(console, serial);
#endif
        WriteBootSequencePlan(console, serial);
    }

    private static void WriteEfiMainStart(EfiSimpleTextOutputProtocol* console, EfiSerialIoProtocol* serial)
    {
        Write(console, serial, 84);
        Write(console, serial, 69);
        Write(console, serial, 83);
        Write(console, serial, 84);
        Write(console, serial, 95);
        Write(console, serial, 83);
        Write(console, serial, 84);
        Write(console, serial, 65);
        Write(console, serial, 82);
        Write(console, serial, 84);
        Write(console, serial, 32);
        Write(console, serial, 101);
        Write(console, serial, 102);
        Write(console, serial, 105);
        Write(console, serial, 46);
        Write(console, serial, 109);
        Write(console, serial, 97);
        Write(console, serial, 105);
        Write(console, serial, 110);
        WriteCrLf(console, serial);
    }

    private static void WriteEfiConsoleReady(EfiSimpleTextOutputProtocol* console, EfiSerialIoProtocol* serial)
    {
        Write(console, serial, 69);
        Write(console, serial, 70);
        Write(console, serial, 73);
        Write(console, serial, 95);
        Write(console, serial, 67);
        Write(console, serial, 79);
        Write(console, serial, 78);
        Write(console, serial, 83);
        Write(console, serial, 79);
        Write(console, serial, 76);
        Write(console, serial, 69);
        Write(console, serial, 95);
        Write(console, serial, 82);
        Write(console, serial, 69);
        Write(console, serial, 65);
        Write(console, serial, 68);
        Write(console, serial, 89);
        WriteCrLf(console, serial);
    }

#if M04_INDEPENDENT_ENDIAN_CONFORMANCE_PROBE
    private static bool WriteM04IndependentEndianConformanceProbe(
        EfiSimpleTextOutputProtocol* console,
        EfiSerialIoProtocol* serial)
    {
        OS.Std.Binary.IndependentEndianReadVector* readVectors =
            stackalloc OS.Std.Binary.IndependentEndianReadVector[
                OS.Std.Binary
                    .IndependentEndianBinaryPrimitivesConformanceVectorsDerivedMosa
                    .ReadVectorCount];
        OS.Std.Binary.IndependentEndianReverseVector* reverseVectors =
            stackalloc OS.Std.Binary.IndependentEndianReverseVector[
                OS.Std.Binary
                    .IndependentEndianBinaryPrimitivesConformanceVectorsDerivedMosa
                    .ReverseVectorCount];

        OS.Std.Binary.IndependentEndianConformanceResult copyResult =
            OS.Std.Binary
                .IndependentEndianBinaryPrimitivesConformanceVectorsDerivedMosa
                .CopyBuiltInCatalog(
                    readVectors,
                    OS.Std.Binary
                        .IndependentEndianBinaryPrimitivesConformanceVectorsDerivedMosa
                        .ReadVectorCount,
                    reverseVectors,
                    OS.Std.Binary
                        .IndependentEndianBinaryPrimitivesConformanceVectorsDerivedMosa
                        .ReverseVectorCount);
        OS.Std.Binary.IndependentEndianConformanceResult validationResult =
            OS.Std.Binary
                .IndependentEndianBinaryPrimitivesConformanceVectorsDerivedMosa
                .ValidateCatalog(
                    readVectors,
                    OS.Std.Binary
                        .IndependentEndianBinaryPrimitivesConformanceVectorsDerivedMosa
                        .ReadVectorCount,
                    reverseVectors,
                    OS.Std.Binary
                        .IndependentEndianBinaryPrimitivesConformanceVectorsDerivedMosa
                        .ReverseVectorCount,
                    OS.Std.Binary
                        .IndependentEndianBinaryPrimitivesConformanceVectorsDerivedMosa
                        .ExpectedCatalogFingerprint);

        if (!copyResult.Succeeded ||
            !validationResult.Succeeded ||
            copyResult.ReadVectorCount != 25 ||
            copyResult.ReverseVectorCount != 7 ||
            validationResult.ReadVectorCount != 25 ||
            validationResult.ReverseVectorCount != 7 ||
            validationResult.ActualFingerprint != 0xE81E4451E8D999F6UL ||
            OS.Std.Binary
                .IndependentEndianBinaryPrimitivesConformanceVectorsDerivedMosa
                .ByteSeriesCount != 5 ||
            OS.Std.Binary
                .IndependentEndianBinaryPrimitivesConformanceVectorsDerivedMosa
                .UInt16SeriesCount != 7 ||
            OS.Std.Binary
                .IndependentEndianBinaryPrimitivesConformanceVectorsDerivedMosa
                .ReferenceFilesRequired ||
            OS.Std.Binary
                .IndependentEndianBinaryPrimitivesConformanceVectorsDerivedMosa
                .RequiresHostConformance ||
            OS.Std.Binary
                .IndependentEndianBinaryPrimitivesConformanceVectorsDerivedMosa
                .AddsCanonicalSystemNamespace ||
            OS.Std.Binary
                .IndependentEndianBinaryPrimitivesConformanceVectorsDerivedMosa
                .SatisfiesRuntimeCompletionGate4 ||
            validationResult.SatisfiesRuntimeCompletionGate4)
        {
            WriteM04IndependentEndianConformanceFailureLine(console, serial);
            return false;
        }

        WriteM04IndependentEndianConformanceSuccessLine(console, serial);
        return true;
    }

    private static void WriteM04IndependentEndianConformanceSuccessLine(
        EfiSimpleTextOutputProtocol* console,
        EfiSerialIoProtocol* serial)
    {
        WriteM04Ascii(console, serial, (byte)'C', (byte)'L', (byte)'R', (byte)'O', (byte)'S', (byte)'_', (byte)'M', (byte)'0');
        WriteM04Ascii(console, serial, (byte)'4', (byte)'_', (byte)'0', (byte)'0', (byte)'2', (byte)'_', (byte)'R', (byte)'E');
        WriteM04Ascii(console, serial, (byte)'A', (byte)'D', (byte)'Y', (byte)' ', (byte)'r', (byte)'e', (byte)'a', (byte)'d');
        WriteM04Ascii(console, serial, (byte)'V', (byte)'e', (byte)'c', (byte)'t', (byte)'o', (byte)'r', (byte)'s', (byte)'=');
        WriteM04Ascii(console, serial, (byte)'2', (byte)'5', (byte)' ', (byte)'r', (byte)'e', (byte)'v', (byte)'e', (byte)'r');
        WriteM04Ascii(console, serial, (byte)'s', (byte)'e', (byte)'V', (byte)'e', (byte)'c', (byte)'t', (byte)'o', (byte)'r');
        WriteM04Ascii(console, serial, (byte)'s', (byte)'=', (byte)'7', (byte)' ', (byte)'b', (byte)'y', (byte)'t', (byte)'e');
        WriteM04Ascii(console, serial, (byte)'S', (byte)'e', (byte)'r', (byte)'i', (byte)'e', (byte)'s', (byte)'=', (byte)'5');
        WriteM04Ascii(console, serial, (byte)' ', (byte)'u', (byte)'i', (byte)'n', (byte)'t', (byte)'1', (byte)'6', (byte)'S');
        WriteM04Ascii(console, serial, (byte)'e', (byte)'r', (byte)'i', (byte)'e', (byte)'s', (byte)'=', (byte)'7', (byte)' ');
        WriteM04Ascii(console, serial, (byte)'f', (byte)'i', (byte)'n', (byte)'g', (byte)'e', (byte)'r', (byte)'p', (byte)'r');
        WriteM04Ascii(console, serial, (byte)'i', (byte)'n', (byte)'t', (byte)'=', (byte)'E', (byte)'8', (byte)'1', (byte)'E');
        WriteM04Ascii(console, serial, (byte)'4', (byte)'4', (byte)'5', (byte)'1', (byte)'E', (byte)'8', (byte)'D', (byte)'9');
        WriteM04Ascii(console, serial, (byte)'9', (byte)'9', (byte)'F', (byte)'6', (byte)' ', (byte)'r', (byte)'e', (byte)'f');
        WriteM04Ascii(console, serial, (byte)'e', (byte)'r', (byte)'e', (byte)'n', (byte)'c', (byte)'e', (byte)'F', (byte)'i');
        WriteM04Ascii(console, serial, (byte)'l', (byte)'e', (byte)'s', (byte)'R', (byte)'e', (byte)'q', (byte)'u', (byte)'i');
        WriteM04Ascii(console, serial, (byte)'r', (byte)'e', (byte)'d', (byte)'=', (byte)'f', (byte)'a', (byte)'l', (byte)'s');
        WriteM04Ascii(console, serial, (byte)'e', (byte)' ', (byte)'h', (byte)'o', (byte)'s', (byte)'t', (byte)'C', (byte)'o');
        WriteM04Ascii(console, serial, (byte)'n', (byte)'f', (byte)'o', (byte)'r', (byte)'m', (byte)'a', (byte)'n', (byte)'c');
        WriteM04Ascii(console, serial, (byte)'e', (byte)'=', (byte)'f', (byte)'a', (byte)'l', (byte)'s', (byte)'e', (byte)' ');
        WriteM04Ascii(console, serial, (byte)'g', (byte)'u', (byte)'e', (byte)'s', (byte)'t', (byte)'I', (byte)'n', (byte)'t');
        WriteM04Ascii(console, serial, (byte)'e', (byte)'g', (byte)'r', (byte)'a', (byte)'t', (byte)'e', (byte)'d', (byte)'=');
        WriteM04Ascii(console, serial, (byte)'t', (byte)'r', (byte)'u', (byte)'e', (byte)' ', (byte)'g', (byte)'a', (byte)'t');
        WriteM04Ascii(console, serial, (byte)'e', (byte)'4', (byte)'=', (byte)'F', (byte)'a', (byte)'i', (byte)'l', 0);
        WriteCrLf(console, serial);
    }

    private static void WriteM04IndependentEndianConformanceFailureLine(
        EfiSimpleTextOutputProtocol* console,
        EfiSerialIoProtocol* serial)
    {
        WriteM04Ascii(console, serial, (byte)'M', (byte)'0', (byte)'4', (byte)'_', (byte)'E', (byte)'N', (byte)'D', (byte)'I');
        WriteM04Ascii(console, serial, (byte)'A', (byte)'N', (byte)'_', (byte)'C', (byte)'O', (byte)'N', (byte)'F', (byte)'O');
        WriteM04Ascii(console, serial, (byte)'R', (byte)'M', (byte)'A', (byte)'N', (byte)'C', (byte)'E', (byte)'_', (byte)'F');
        WriteM04Ascii(console, serial, (byte)'A', (byte)'I', (byte)'L', (byte)'E', (byte)'D', 0, 0, 0);
        WriteCrLf(console, serial);
    }

    private static void WriteM04Ascii(
        EfiSimpleTextOutputProtocol* console,
        EfiSerialIoProtocol* serial,
        byte b0,
        byte b1,
        byte b2,
        byte b3,
        byte b4,
        byte b5,
        byte b6,
        byte b7)
    {
        if (b0 != 0)
            Write(console, serial, b0);
        if (b1 != 0)
            Write(console, serial, b1);
        if (b2 != 0)
            Write(console, serial, b2);
        if (b3 != 0)
            Write(console, serial, b3);
        if (b4 != 0)
            Write(console, serial, b4);
        if (b5 != 0)
            Write(console, serial, b5);
        if (b6 != 0)
            Write(console, serial, b6);
        if (b7 != 0)
            Write(console, serial, b7);
    }
#endif
#if M04_SPAN_OVERLAP_MEMORY_CAST_UNSAFE_WIDTH_CONFORMANCE_PROBE
    private static bool WriteM04SpanMemoryConformanceProbe(
        EfiSimpleTextOutputProtocol* console,
        EfiSerialIoProtocol* serial)
    {
        OS.Std.Memory.SpanMemoryConformanceVector* vectors =
            stackalloc OS.Std.Memory.SpanMemoryConformanceVector[16];
        byte* scratch = stackalloc byte[16];
        OS.Std.Memory.SpanMemoryVectorExecutionRecord* records =
            stackalloc OS.Std.Memory.SpanMemoryVectorExecutionRecord[16];

        OS.Std.Memory.SpanMemoryConformanceResult copyResult =
            OS.Std.Memory
                .SpanOverlapMemoryCastUnsafeWidthConformanceVectors
                .CopyBuiltInCatalog(vectors, 16);
        OS.Std.Memory.SpanMemoryExecutionResult executionResult =
            OS.Std.Memory
                .SpanOverlapMemoryCastUnsafeWidthConformanceVectors
                .ExecuteCatalog(
                    vectors,
                    16,
                    scratch,
                    16,
                    records,
                    16,
                    0xF03FF59D36F39ECDUL);

        if (!M04SpanMemoryCatalogResultMatches(copyResult) ||
            !executionResult.IsInitialized ||
            !executionResult.Succeeded ||
            executionResult.Status !=
                OS.Std.Memory.SpanMemoryExecutionStatus.Success ||
            !M04SpanMemoryCatalogResultMatches(
                executionResult.CatalogValidation) ||
            executionResult.RequiredScratchBytes != 16 ||
            executionResult.AvailableScratchBytes != 16 ||
            executionResult.RequiredOutputCount != 16 ||
            executionResult.AvailableOutputCount != 16 ||
            executionResult.ExecutedVectorCount != 16 ||
            executionResult.ProblemIndex != -1 ||
            executionResult.CatalogFingerprint !=
                0xF03FF59D36F39ECDUL ||
            OS.Std.Memory
                .SpanOverlapMemoryCastUnsafeWidthConformanceVectors
                .SliceVectorCount != 4 ||
            OS.Std.Memory
                .SpanOverlapMemoryCastUnsafeWidthConformanceVectors
                .OverlapVectorCount != 5 ||
            OS.Std.Memory
                .SpanOverlapMemoryCastUnsafeWidthConformanceVectors
                .MemoryCastVectorCount != 3 ||
            OS.Std.Memory
                .SpanOverlapMemoryCastUnsafeWidthConformanceVectors
                .UnalignedWidthVectorCount != 4 ||
            OS.Std.Memory
                .SpanOverlapMemoryCastUnsafeWidthConformanceVectors
                .TotalVectorCount != 16 ||
            OS.Std.Memory
                .SpanOverlapMemoryCastUnsafeWidthConformanceVectors
                .PointerWidth != 8 ||
            OS.Std.Memory
                .SpanOverlapMemoryCastUnsafeWidthConformanceVectors
                .RequiredScratchBytes != 16 ||
            OS.Std.Memory
                .SpanOverlapMemoryCastUnsafeWidthConformanceVectors
                .RequiredExecutionRecordCount != 16 ||
            !OS.Std.Memory
                .SpanOverlapMemoryCastUnsafeWidthConformanceVectors
                .MemoryCastAllowsUnalignedStart ||
            !OS.Std.Memory
                .SpanOverlapMemoryCastUnsafeWidthConformanceVectors
                .MemoryCastFloorTruncatesTrailingBytes ||
            !OS.Std.Memory
                .SpanOverlapMemoryCastUnsafeWidthConformanceVectors
                .UnsafeWidthUsesUnsignedReads ||
            OS.Std.Memory
                .SpanOverlapMemoryCastUnsafeWidthConformanceVectors
                .AddsCanonicalSystemNamespace ||
            OS.Std.Memory
                .SpanOverlapMemoryCastUnsafeWidthConformanceVectors
                .ReferenceFilesRequired ||
            OS.Std.Memory
                .SpanOverlapMemoryCastUnsafeWidthConformanceVectors
                .RequiresHostConformance ||
            OS.Std.Memory
                .SpanOverlapMemoryCastUnsafeWidthConformanceVectors
                .SatisfiesRuntimeCompletionGate4)
        {
            WriteM04SpanMemoryConformanceFailureLine(console, serial);
            return false;
        }

        const ulong Seed = 0x0807060504030201UL;
        if (!M04SpanMemoryVectorMatches(
                vectors[0],
                1,
                OS.Std.Memory.SpanMemoryConformanceOperation.Slice,
                0,
                0,
                8,
                1,
                1,
                1,
                8,
                8,
                Seed,
                Seed) ||
            !M04SpanMemoryVectorMatches(
                vectors[1],
                2,
                OS.Std.Memory.SpanMemoryConformanceOperation.Slice,
                2,
                0,
                4,
                1,
                1,
                1,
                4,
                4,
                Seed,
                0x0000000006050403UL) ||
            !M04SpanMemoryVectorMatches(
                vectors[2],
                3,
                OS.Std.Memory.SpanMemoryConformanceOperation.Slice,
                8,
                0,
                0,
                1,
                1,
                1,
                0,
                0,
                Seed,
                0) ||
            !M04SpanMemoryVectorMatches(
                vectors[3],
                4,
                OS.Std.Memory.SpanMemoryConformanceOperation.Slice,
                7,
                0,
                2,
                1,
                1,
                0,
                0,
                0,
                Seed,
                0) ||
            !M04SpanMemoryVectorMatches(
                vectors[4],
                5,
                OS.Std.Memory.SpanMemoryConformanceOperation.Overlap,
                2,
                0,
                4,
                1,
                1,
                1,
                8,
                8,
                Seed,
                0x0807060506050403UL) ||
            !M04SpanMemoryVectorMatches(
                vectors[5],
                6,
                OS.Std.Memory.SpanMemoryConformanceOperation.Overlap,
                0,
                2,
                4,
                1,
                1,
                1,
                8,
                8,
                Seed,
                0x0807040302010201UL) ||
            !M04SpanMemoryVectorMatches(
                vectors[6],
                7,
                OS.Std.Memory.SpanMemoryConformanceOperation.Overlap,
                1,
                1,
                4,
                1,
                1,
                1,
                8,
                8,
                Seed,
                Seed) ||
            !M04SpanMemoryVectorMatches(
                vectors[7],
                8,
                OS.Std.Memory.SpanMemoryConformanceOperation.Overlap,
                8,
                8,
                0,
                1,
                1,
                1,
                8,
                8,
                Seed,
                Seed) ||
            !M04SpanMemoryVectorMatches(
                vectors[8],
                9,
                OS.Std.Memory.SpanMemoryConformanceOperation.Overlap,
                0,
                4,
                4,
                1,
                1,
                1,
                8,
                8,
                Seed,
                0x0403020104030201UL) ||
            !M04SpanMemoryVectorMatches(
                vectors[9],
                10,
                OS.Std.Memory.SpanMemoryConformanceOperation.MemoryCast,
                0,
                0,
                8,
                1,
                2,
                1,
                8,
                4,
                Seed,
                Seed) ||
            !M04SpanMemoryVectorMatches(
                vectors[10],
                11,
                OS.Std.Memory.SpanMemoryConformanceOperation.MemoryCast,
                1,
                0,
                6,
                1,
                4,
                1,
                4,
                1,
                Seed,
                0x0000000004030201UL) ||
            !M04SpanMemoryVectorMatches(
                vectors[11],
                12,
                OS.Std.Memory.SpanMemoryConformanceOperation.MemoryCast,
                0,
                0,
                4,
                4,
                1,
                1,
                4,
                4,
                0x00000000A1B2C3D4UL,
                0x00000000A1B2C3D4UL) ||
            !M04SpanMemoryVectorMatches(
                vectors[12],
                13,
                OS.Std.Memory.SpanMemoryConformanceOperation.UnalignedWidth,
                1,
                0,
                1,
                1,
                8,
                1,
                8,
                1,
                0xFF,
                0xFF) ||
            !M04SpanMemoryVectorMatches(
                vectors[13],
                14,
                OS.Std.Memory.SpanMemoryConformanceOperation.UnalignedWidth,
                1,
                0,
                2,
                2,
                8,
                3,
                8,
                1,
                0xFF80,
                0xFFFFFFFFFFFFFF80UL) ||
            !M04SpanMemoryVectorMatches(
                vectors[14],
                15,
                OS.Std.Memory.SpanMemoryConformanceOperation.UnalignedWidth,
                1,
                0,
                4,
                4,
                8,
                1,
                8,
                1,
                0x89ABCDEF,
                0x0000000089ABCDEFUL) ||
            !M04SpanMemoryVectorMatches(
                vectors[15],
                16,
                OS.Std.Memory.SpanMemoryConformanceOperation.UnalignedWidth,
                1,
                0,
                8,
                8,
                4,
                1,
                4,
                1,
                0x1122334455667788UL,
                0x0000000055667788UL))
        {
            WriteM04SpanMemoryConformanceFailureLine(console, serial);
            return false;
        }

        if (!M04SpanMemoryRecordMatches(
                records[0], 1,
                OS.Std.Memory.SpanMemoryConformanceOperation.Slice,
                OS.Std.Memory.SpanMemoryVectorExecutionStatus.Success,
                8, 8, 0, Seed, Seed, false) ||
            !M04SpanMemoryRecordMatches(
                records[1], 2,
                OS.Std.Memory.SpanMemoryConformanceOperation.Slice,
                OS.Std.Memory.SpanMemoryVectorExecutionStatus.Success,
                4, 4, 0, 0x0000000006050403UL, Seed, false) ||
            !M04SpanMemoryRecordMatches(
                records[2], 3,
                OS.Std.Memory.SpanMemoryConformanceOperation.Slice,
                OS.Std.Memory.SpanMemoryVectorExecutionStatus.Success,
                0, 0, 0, 0, Seed, false) ||
            !M04SpanMemoryRecordMatches(
                records[3], 4,
                OS.Std.Memory.SpanMemoryConformanceOperation.Slice,
                OS.Std.Memory.SpanMemoryVectorExecutionStatus.ExpectedRejection,
                0, 0, 0, 0, Seed, true) ||
            !M04SpanMemoryRecordMatches(
                records[4], 5,
                OS.Std.Memory.SpanMemoryConformanceOperation.Overlap,
                OS.Std.Memory.SpanMemoryVectorExecutionStatus.Success,
                8, 8, 0, 0x0807060506050403UL,
                0x0807060506050403UL, false) ||
            !M04SpanMemoryRecordMatches(
                records[5], 6,
                OS.Std.Memory.SpanMemoryConformanceOperation.Overlap,
                OS.Std.Memory.SpanMemoryVectorExecutionStatus.Success,
                8, 8, 0, 0x0807040302010201UL,
                0x0807040302010201UL, false) ||
            !M04SpanMemoryRecordMatches(
                records[6], 7,
                OS.Std.Memory.SpanMemoryConformanceOperation.Overlap,
                OS.Std.Memory.SpanMemoryVectorExecutionStatus.Success,
                8, 8, 0, Seed, Seed, false) ||
            !M04SpanMemoryRecordMatches(
                records[7], 8,
                OS.Std.Memory.SpanMemoryConformanceOperation.Overlap,
                OS.Std.Memory.SpanMemoryVectorExecutionStatus.Success,
                8, 8, 0, Seed, Seed, false) ||
            !M04SpanMemoryRecordMatches(
                records[8], 9,
                OS.Std.Memory.SpanMemoryConformanceOperation.Overlap,
                OS.Std.Memory.SpanMemoryVectorExecutionStatus.Success,
                8, 8, 0, 0x0403020104030201UL,
                0x0403020104030201UL, false) ||
            !M04SpanMemoryRecordMatches(
                records[9], 10,
                OS.Std.Memory.SpanMemoryConformanceOperation.MemoryCast,
                OS.Std.Memory.SpanMemoryVectorExecutionStatus.Success,
                8, 4, 0, Seed, Seed, false) ||
            !M04SpanMemoryRecordMatches(
                records[10], 11,
                OS.Std.Memory.SpanMemoryConformanceOperation.MemoryCast,
                OS.Std.Memory.SpanMemoryVectorExecutionStatus.Success,
                4, 1, 0, 0x0000000004030201UL,
                0x0006050403020100UL, false) ||
            !M04SpanMemoryRecordMatches(
                records[11], 12,
                OS.Std.Memory.SpanMemoryConformanceOperation.MemoryCast,
                OS.Std.Memory.SpanMemoryVectorExecutionStatus.Success,
                4, 4, 0, 0x00000000A1B2C3D4UL,
                0x00000000A1B2C3D4UL, false) ||
            !M04SpanMemoryRecordMatches(
                records[12], 13,
                OS.Std.Memory.SpanMemoryConformanceOperation.UnalignedWidth,
                OS.Std.Memory.SpanMemoryVectorExecutionStatus.Success,
                8, 1, 0xFF, 0xFF, 0x000000000000FF00UL, false) ||
            !M04SpanMemoryRecordMatches(
                records[13], 14,
                OS.Std.Memory.SpanMemoryConformanceOperation.UnalignedWidth,
                OS.Std.Memory.SpanMemoryVectorExecutionStatus.Success,
                8, 1, 0xFF80, 0xFFFFFFFFFFFFFF80UL,
                0x0000000000FF8000UL, false) ||
            !M04SpanMemoryRecordMatches(
                records[14], 15,
                OS.Std.Memory.SpanMemoryConformanceOperation.UnalignedWidth,
                OS.Std.Memory.SpanMemoryVectorExecutionStatus.Success,
                8, 1, 0x89ABCDEF, 0x0000000089ABCDEFUL,
                0x00000089ABCDEF00UL, false) ||
            !M04SpanMemoryRecordMatches(
                records[15], 16,
                OS.Std.Memory.SpanMemoryConformanceOperation.UnalignedWidth,
                OS.Std.Memory.SpanMemoryVectorExecutionStatus.Success,
                4, 1, 0x1122334455667788UL, 0x0000000055667788UL,
                0x2233445566778800UL, false))
        {
            WriteM04SpanMemoryConformanceFailureLine(console, serial);
            return false;
        }

        WriteM04SpanMemoryConformanceSuccessLine(console, serial);
        return true;
    }

    private static bool M04SpanMemoryCatalogResultMatches(
        OS.Std.Memory.SpanMemoryConformanceResult result)
    {
        return result.IsInitialized &&
            result.Succeeded &&
            result.Status == OS.Std.Memory.SpanMemoryConformanceStatus.Success &&
            result.ProblemIndex == -1 &&
            result.ConflictIndex == -1 &&
            result.VectorCount == 16 &&
            result.ExpectedFingerprint == 0xF03FF59D36F39ECDUL &&
            result.ActualFingerprint == 0xF03FF59D36F39ECDUL &&
            !result.SatisfiesRuntimeCompletionGate4;
    }

    private static bool M04SpanMemoryVectorMatches(
        OS.Std.Memory.SpanMemoryConformanceVector vector,
        byte key,
        OS.Std.Memory.SpanMemoryConformanceOperation operation,
        int sourceOffset,
        int destinationOffset,
        int byteCount,
        byte sourceWidth,
        byte destinationWidth,
        byte flags,
        byte expectedByteCount,
        byte expectedElementCount,
        ulong input,
        ulong expected)
    {
        return vector.IsInitialized &&
            vector.Key == key &&
            vector.Operation == operation &&
            vector.SourceOffset == sourceOffset &&
            vector.DestinationOffset == destinationOffset &&
            vector.ByteCount == byteCount &&
            vector.SourceWidth == sourceWidth &&
            vector.DestinationWidth == destinationWidth &&
            vector.Flags == flags &&
            vector.ExpectedByteCount == expectedByteCount &&
            vector.ExpectedElementCount == expectedElementCount &&
            vector.Input == input &&
            vector.Expected == expected &&
            vector.ExpectedSuccess == ((flags & 1) != 0) &&
            vector.SignExtend == ((flags & 2) != 0);
    }

    private static bool M04SpanMemoryRecordMatches(
        OS.Std.Memory.SpanMemoryVectorExecutionRecord record,
        byte key,
        OS.Std.Memory.SpanMemoryConformanceOperation operation,
        OS.Std.Memory.SpanMemoryVectorExecutionStatus status,
        byte outputByteCount,
        byte resultElementCount,
        ulong rawUnsignedValue,
        ulong outputBytes,
        ulong scratchAfter,
        bool rejectedInputPreserved)
    {
        return record.IsInitialized &&
            record.Succeeded &&
            record.Key == key &&
            record.Operation == operation &&
            record.Status == status &&
            record.OutputByteCount == outputByteCount &&
            record.ResultElementCount == resultElementCount &&
            record.RawUnsignedValue == rawUnsignedValue &&
            record.OutputBytes == outputBytes &&
            record.ScratchAfter == scratchAfter &&
            record.RejectedInputPreserved == rejectedInputPreserved;
    }

    private static void WriteM04SpanMemoryConformanceSuccessLine(
        EfiSimpleTextOutputProtocol* console,
        EfiSerialIoProtocol* serial)
    {
        WriteM04SpanMemoryAscii(console, serial, (byte)'C', (byte)'L', (byte)'R', (byte)'O', (byte)'S', (byte)'_', (byte)'M', (byte)'0');
        WriteM04SpanMemoryAscii(console, serial, (byte)'4', (byte)'_', (byte)'0', (byte)'0', (byte)'3', (byte)'_', (byte)'R', (byte)'E');
        WriteM04SpanMemoryAscii(console, serial, (byte)'A', (byte)'D', (byte)'Y', (byte)' ', (byte)'v', (byte)'e', (byte)'c', (byte)'t');
        WriteM04SpanMemoryAscii(console, serial, (byte)'o', (byte)'r', (byte)'s', (byte)'=', (byte)'1', (byte)'6', (byte)' ', (byte)'s');
        WriteM04SpanMemoryAscii(console, serial, (byte)'p', (byte)'a', (byte)'n', (byte)'V', (byte)'e', (byte)'c', (byte)'t', (byte)'o');
        WriteM04SpanMemoryAscii(console, serial, (byte)'r', (byte)'s', (byte)'=', (byte)'4', (byte)' ', (byte)'o', (byte)'v', (byte)'e');
        WriteM04SpanMemoryAscii(console, serial, (byte)'r', (byte)'l', (byte)'a', (byte)'p', (byte)'V', (byte)'e', (byte)'c', (byte)'t');
        WriteM04SpanMemoryAscii(console, serial, (byte)'o', (byte)'r', (byte)'s', (byte)'=', (byte)'5', (byte)' ', (byte)'m', (byte)'e');
        WriteM04SpanMemoryAscii(console, serial, (byte)'m', (byte)'o', (byte)'r', (byte)'y', (byte)'C', (byte)'a', (byte)'s', (byte)'t');
        WriteM04SpanMemoryAscii(console, serial, (byte)'V', (byte)'e', (byte)'c', (byte)'t', (byte)'o', (byte)'r', (byte)'s', (byte)'=');
        WriteM04SpanMemoryAscii(console, serial, (byte)'3', (byte)' ', (byte)'u', (byte)'n', (byte)'s', (byte)'a', (byte)'f', (byte)'e');
        WriteM04SpanMemoryAscii(console, serial, (byte)'W', (byte)'i', (byte)'d', (byte)'t', (byte)'h', (byte)'V', (byte)'e', (byte)'c');
        WriteM04SpanMemoryAscii(console, serial, (byte)'t', (byte)'o', (byte)'r', (byte)'s', (byte)'=', (byte)'4', (byte)' ', (byte)'f');
        WriteM04SpanMemoryAscii(console, serial, (byte)'i', (byte)'n', (byte)'g', (byte)'e', (byte)'r', (byte)'p', (byte)'r', (byte)'i');
        WriteM04SpanMemoryAscii(console, serial, (byte)'n', (byte)'t', (byte)'=', (byte)'F', (byte)'0', (byte)'3', (byte)'F', (byte)'F');
        WriteM04SpanMemoryAscii(console, serial, (byte)'5', (byte)'9', (byte)'D', (byte)'3', (byte)'6', (byte)'F', (byte)'3', (byte)'9');
        WriteM04SpanMemoryAscii(console, serial, (byte)'E', (byte)'C', (byte)'D', (byte)' ', (byte)'p', (byte)'o', (byte)'i', (byte)'n');
        WriteM04SpanMemoryAscii(console, serial, (byte)'t', (byte)'e', (byte)'r', (byte)'W', (byte)'i', (byte)'d', (byte)'t', (byte)'h');
        WriteM04SpanMemoryAscii(console, serial, (byte)'=', (byte)'8', (byte)' ', (byte)'r', (byte)'e', (byte)'f', (byte)'e', (byte)'r');
        WriteM04SpanMemoryAscii(console, serial, (byte)'e', (byte)'n', (byte)'c', (byte)'e', (byte)'F', (byte)'i', (byte)'l', (byte)'e');
        WriteM04SpanMemoryAscii(console, serial, (byte)'s', (byte)'R', (byte)'e', (byte)'q', (byte)'u', (byte)'i', (byte)'r', (byte)'e');
        WriteM04SpanMemoryAscii(console, serial, (byte)'d', (byte)'=', (byte)'f', (byte)'a', (byte)'l', (byte)'s', (byte)'e', (byte)' ');
        WriteM04SpanMemoryAscii(console, serial, (byte)'h', (byte)'o', (byte)'s', (byte)'t', (byte)'C', (byte)'o', (byte)'n', (byte)'f');
        WriteM04SpanMemoryAscii(console, serial, (byte)'o', (byte)'r', (byte)'m', (byte)'a', (byte)'n', (byte)'c', (byte)'e', (byte)'=');
        WriteM04SpanMemoryAscii(console, serial, (byte)'f', (byte)'a', (byte)'l', (byte)'s', (byte)'e', (byte)' ', (byte)'g', (byte)'u');
        WriteM04SpanMemoryAscii(console, serial, (byte)'e', (byte)'s', (byte)'t', (byte)'I', (byte)'n', (byte)'t', (byte)'e', (byte)'g');
        WriteM04SpanMemoryAscii(console, serial, (byte)'r', (byte)'a', (byte)'t', (byte)'e', (byte)'d', (byte)'=', (byte)'t', (byte)'r');
        WriteM04SpanMemoryAscii(console, serial, (byte)'u', (byte)'e', (byte)' ', (byte)'g', (byte)'a', (byte)'t', (byte)'e', (byte)'4');
        WriteM04SpanMemoryAscii(console, serial, (byte)'=', (byte)'F', (byte)'a', (byte)'i', (byte)'l', 0, 0, 0);
        WriteCrLf(console, serial);
    }

    private static void WriteM04SpanMemoryConformanceFailureLine(
        EfiSimpleTextOutputProtocol* console,
        EfiSerialIoProtocol* serial)
    {
        WriteM04SpanMemoryAscii(console, serial, (byte)'M', (byte)'0', (byte)'4', (byte)'_', (byte)'S', (byte)'P', (byte)'A', (byte)'N');
        WriteM04SpanMemoryAscii(console, serial, (byte)'_', (byte)'M', (byte)'E', (byte)'M', (byte)'O', (byte)'R', (byte)'Y', (byte)'_');
        WriteM04SpanMemoryAscii(console, serial, (byte)'C', (byte)'O', (byte)'N', (byte)'F', (byte)'O', (byte)'R', (byte)'M', (byte)'A');
        WriteM04SpanMemoryAscii(console, serial, (byte)'N', (byte)'C', (byte)'E', (byte)'_', (byte)'F', (byte)'A', (byte)'I', (byte)'L');
        WriteM04SpanMemoryAscii(console, serial, (byte)'E', (byte)'D', 0, 0, 0, 0, 0, 0);
        WriteCrLf(console, serial);
    }

    private static void WriteM04SpanMemoryAscii(
        EfiSimpleTextOutputProtocol* console,
        EfiSerialIoProtocol* serial,
        byte b0,
        byte b1,
        byte b2,
        byte b3,
        byte b4,
        byte b5,
        byte b6,
        byte b7)
    {
        if (b0 != 0)
            Write(console, serial, b0);
        if (b1 != 0)
            Write(console, serial, b1);
        if (b2 != 0)
            Write(console, serial, b2);
        if (b3 != 0)
            Write(console, serial, b3);
        if (b4 != 0)
            Write(console, serial, b4);
        if (b5 != 0)
            Write(console, serial, b5);
        if (b6 != 0)
            Write(console, serial, b6);
        if (b7 != 0)
            Write(console, serial, b7);
    }
#endif
#if M04_ARITHMETIC_CONFORMANCE_PROBE
    private static bool WriteM04ArithmeticConformanceProbe(
        EfiSimpleTextOutputProtocol* console,
        EfiSerialIoProtocol* serial)
    {
        OS.Std.Numerics.ArithmeticConformanceResult result =
            OS.Std.Numerics.IndependentArithmeticShiftCastN64BitDivisionRemainder
                .RunCatalog();
        if (result.Status != OS.Std.Numerics.ArithmeticConformanceStatus.Passed ||
            result.CompletedCount != 57 || result.FailedIndex != -1 ||
            result.Fingerprint != 0x45FDFA24DD72EA28UL)
        {
            WriteM04ArithmeticConformanceFailureLine(console, serial);
            return false;
        }

        WriteM04ArithmeticConformanceSuccessLine(console, serial);
        return true;
    }

    private static void WriteM04ArithmeticConformanceSuccessLine(
        EfiSimpleTextOutputProtocol* console,
        EfiSerialIoProtocol* serial)
    {
        WriteM04ArithmeticAscii(console, serial, (byte)'C', (byte)'L', (byte)'R', (byte)'O', (byte)'S', (byte)'_', (byte)'M', (byte)'0');
        WriteM04ArithmeticAscii(console, serial, (byte)'4', (byte)'_', (byte)'0', (byte)'0', (byte)'4', (byte)'_', (byte)'R', (byte)'E');
        WriteM04ArithmeticAscii(console, serial, (byte)'A', (byte)'D', (byte)'Y', (byte)' ', (byte)'v', (byte)'e', (byte)'c', (byte)'t');
        WriteM04ArithmeticAscii(console, serial, (byte)'o', (byte)'r', (byte)'s', (byte)'=', (byte)'5', (byte)'7', (byte)' ', (byte)'f');
        WriteM04ArithmeticAscii(console, serial, (byte)'i', (byte)'n', (byte)'g', (byte)'e', (byte)'r', (byte)'p', (byte)'r', (byte)'i');
        WriteM04ArithmeticAscii(console, serial, (byte)'n', (byte)'t', (byte)'=', (byte)'4', (byte)'5', (byte)'F', (byte)'D', (byte)'F');
        WriteM04ArithmeticAscii(console, serial, (byte)'A', (byte)'2', (byte)'4', (byte)'D', (byte)'D', (byte)'7', (byte)'2', (byte)'E');
        WriteM04ArithmeticAscii(console, serial, (byte)'A', (byte)'2', (byte)'8', (byte)' ', (byte)'r', (byte)'e', (byte)'f', (byte)'e');
        WriteM04ArithmeticAscii(console, serial, (byte)'r', (byte)'e', (byte)'n', (byte)'c', (byte)'e', (byte)'F', (byte)'i', (byte)'l');
        WriteM04ArithmeticAscii(console, serial, (byte)'e', (byte)'s', (byte)'R', (byte)'e', (byte)'q', (byte)'u', (byte)'i', (byte)'r');
        WriteM04ArithmeticAscii(console, serial, (byte)'e', (byte)'d', (byte)'=', (byte)'f', (byte)'a', (byte)'l', (byte)'s', (byte)'e');
        WriteM04ArithmeticAscii(console, serial, (byte)' ', (byte)'h', (byte)'o', (byte)'s', (byte)'t', (byte)'C', (byte)'o', (byte)'n');
        WriteM04ArithmeticAscii(console, serial, (byte)'f', (byte)'o', (byte)'r', (byte)'m', (byte)'a', (byte)'n', (byte)'c', (byte)'e');
        WriteM04ArithmeticAscii(console, serial, (byte)'=', (byte)'f', (byte)'a', (byte)'l', (byte)'s', (byte)'e', (byte)' ', (byte)'g');
        WriteM04ArithmeticAscii(console, serial, (byte)'u', (byte)'e', (byte)'s', (byte)'t', (byte)'I', (byte)'n', (byte)'t', (byte)'e');
        WriteM04ArithmeticAscii(console, serial, (byte)'g', (byte)'r', (byte)'a', (byte)'t', (byte)'e', (byte)'d', (byte)'=', (byte)'t');
        WriteM04ArithmeticAscii(console, serial, (byte)'r', (byte)'u', (byte)'e', (byte)' ', (byte)'g', (byte)'a', (byte)'t', (byte)'e');
        WriteM04ArithmeticAscii(console, serial, (byte)'4', (byte)'=', (byte)'F', (byte)'a', (byte)'i', (byte)'l', 0, 0);
        WriteCrLf(console, serial);
    }

    private static void WriteM04ArithmeticConformanceFailureLine(
        EfiSimpleTextOutputProtocol* console,
        EfiSerialIoProtocol* serial)
    {
        WriteM04ArithmeticAscii(console, serial, (byte)'M', (byte)'0', (byte)'4', (byte)'_', (byte)'A', (byte)'R', (byte)'I', (byte)'T');
        WriteM04ArithmeticAscii(console, serial, (byte)'H', (byte)'M', (byte)'E', (byte)'T', (byte)'I', (byte)'C', (byte)'_', (byte)'C');
        WriteM04ArithmeticAscii(console, serial, (byte)'O', (byte)'N', (byte)'F', (byte)'O', (byte)'R', (byte)'M', (byte)'A', (byte)'N');
        WriteM04ArithmeticAscii(console, serial, (byte)'C', (byte)'E', (byte)'_', (byte)'F', (byte)'A', (byte)'I', (byte)'L', (byte)'E');
        WriteM04ArithmeticAscii(console, serial, (byte)'D', 0, 0, 0, 0, 0, 0, 0);
        WriteCrLf(console, serial);
    }

    private static void WriteM04ArithmeticAscii(
        EfiSimpleTextOutputProtocol* console,
        EfiSerialIoProtocol* serial,
        byte b0,
        byte b1,
        byte b2,
        byte b3,
        byte b4,
        byte b5,
        byte b6,
        byte b7)
    {
        if (b0 != 0)
            Write(console, serial, b0);
        if (b1 != 0)
            Write(console, serial, b1);
        if (b2 != 0)
            Write(console, serial, b2);
        if (b3 != 0)
            Write(console, serial, b3);
        if (b4 != 0)
            Write(console, serial, b4);
        if (b5 != 0)
            Write(console, serial, b5);
        if (b6 != 0)
            Write(console, serial, b6);
        if (b7 != 0)
            Write(console, serial, b7);
    }
#endif
#if M04_LAYOUT_CONTRACT_PROBE
    private static bool WriteM04LayoutContractProbe(
        EfiSimpleTextOutputProtocol* console,
        EfiSerialIoProtocol* serial)
    {
        NativeAotLayoutProfile profile = NativeAotLayoutProfile.Ilc11_0_0Rc3WinX64LittleEndian;
        // Independent scalar expectations; large modeled sizes allocate no memory.
        if (
            !CheckM04LayoutResult(profile, 0x50000008, 24, 0, 24UL,
                NativeAotLayoutStatus.Success, NativeAotLayoutShape.FixedObject, 0, false, -1, 8, 24UL, 24UL) ||
            !CheckM04LayoutResult(profile, 0x51000000, 32, 0, 32UL,
                NativeAotLayoutStatus.Success, NativeAotLayoutShape.FixedObject, 0, true, -1, 8, 32UL, 32UL) ||
            !CheckM04LayoutResult(profile, 0xE0020001, 24, 0, 24UL,
                NativeAotLayoutStatus.Success, NativeAotLayoutShape.SzArray, 1, false, 8, 16, 24UL, 24UL) ||
            !CheckM04LayoutResult(profile, 0xE0020001, 24, 1, 32UL,
                NativeAotLayoutStatus.Success, NativeAotLayoutShape.SzArray, 1, false, 8, 16, 25UL, 32UL) ||
            !CheckM04LayoutResult(profile, 0xE0020001, 24, 8, 32UL,
                NativeAotLayoutStatus.Success, NativeAotLayoutShape.SzArray, 1, false, 8, 16, 32UL, 32UL) ||
            !CheckM04LayoutResult(profile, 0xE0020003, 24, 3, 40UL,
                NativeAotLayoutStatus.Success, NativeAotLayoutShape.SzArray, 3, false, 8, 16, 33UL, 40UL) ||
            !CheckM04LayoutResult(profile, 0xE1020008, 24, 1, 32UL,
                NativeAotLayoutStatus.Success, NativeAotLayoutShape.SzArray, 8, true, 8, 16, 32UL, 32UL) ||
            !CheckM04LayoutResult(profile, 0xD0000002, 22, 0, 24UL,
                NativeAotLayoutStatus.Success, NativeAotLayoutShape.String, 2, false, 8, 12, 22UL, 24UL) ||
            !CheckM04LayoutResult(profile, 0xD0000002, 22, 1, 24UL,
                NativeAotLayoutStatus.Success, NativeAotLayoutShape.String, 2, false, 8, 12, 24UL, 24UL) ||
            !CheckM04LayoutResult(profile, 0xD0000002, 22, 2, 32UL,
                NativeAotLayoutStatus.Success, NativeAotLayoutShape.String, 2, false, 8, 12, 26UL, 32UL) ||
            !CheckM04LayoutResult(profile, 0xE0020001, 24, 2147483591, 2147483616UL,
                NativeAotLayoutStatus.Success, NativeAotLayoutShape.SzArray, 1, false, 8, 16, 2147483615UL, 2147483616UL) ||
            !CheckM04LayoutResult(profile, 0xE002FFFF, 24, 2147483591, 140735337136216UL,
                NativeAotLayoutStatus.Success, NativeAotLayoutShape.SzArray, 65535, false, 8, 16, 140735337136209UL, 140735337136216UL) ||
            !CheckM04LayoutResult(profile, 0xD0000002, 22, 1073741791, 2147483608UL,
                NativeAotLayoutStatus.Success, NativeAotLayoutShape.String, 2, false, 8, 12, 2147483604UL, 2147483608UL) ||
            !CheckM04LayoutResult((NativeAotLayoutProfile)255, 0x50000000, 24, 0, 24,
                NativeAotLayoutStatus.UnsupportedProfile, NativeAotLayoutShape.Uninitialized, 0, false, 0, 0, 0, 0) ||
            !CheckM04LayoutResult(profile, 0x50200000, 24, 0, 24,
                NativeAotLayoutStatus.InvalidFlags, NativeAotLayoutShape.Uninitialized, 0, false, 0, 0, 0, 0) ||
            !CheckM04LayoutResult(profile, 0x60020001, 24, 0, 24,
                NativeAotLayoutStatus.InvalidFlags, NativeAotLayoutShape.Uninitialized, 0, false, 0, 0, 0, 0) ||
            !CheckM04LayoutResult(profile, 0xE0020000, 24, 0, 24,
                NativeAotLayoutStatus.InvalidComponentSize, NativeAotLayoutShape.Uninitialized, 0, false, 0, 0, 0, 0) ||
            !CheckM04LayoutResult(profile, 0xD0000002, 22, -1, 24,
                NativeAotLayoutStatus.InvalidLength, NativeAotLayoutShape.Uninitialized, 0, false, 0, 0, 0, 0) ||
            !CheckM04LayoutResult(profile, 0x50000000, 24, 0, 23,
                NativeAotLayoutStatus.CapacityTooSmall, NativeAotLayoutShape.Uninitialized, 0, false, 0, 0, 0, 0))
        {
            WriteM04LayoutContractFailureLine(console, serial);
            return false;
        }

        WriteM04LayoutContractSuccessLine(console, serial);
        return true;
    }

    private static bool CheckM04LayoutResult(
        NativeAotLayoutProfile profile, uint flags, uint baseSize, int count, ulong capacity,
        NativeAotLayoutStatus status, NativeAotLayoutShape shape, ushort component,
        bool pointers, int lengthOffset, int dataOffset, ulong rawSize, ulong alignedSize)
    {
        NativeAotLayoutResult result = ObjectArrayStringEETypeMethodTableLayoutContracts.Evaluate(
            profile, new NativeAotMethodTablePrefix(flags, baseSize, 0, 0, 0, 0), count, capacity);
        return result.Status == status && result.Shape == shape &&
            result.ComponentSize == component && result.ContainsGcPointers == pointers &&
            result.LengthOffsetFromReference == lengthOffset && result.DataOffsetFromReference == dataOffset &&
            result.RawSizeBytes == rawSize && result.AlignedSizeBytes == alignedSize;
    }

    private static void WriteM04LayoutContractSuccessLine(
        EfiSimpleTextOutputProtocol* console,
        EfiSerialIoProtocol* serial)
    {
        WriteM04LayoutAscii(console, serial, (byte)'C', (byte)'L', (byte)'R', (byte)'O', (byte)'S', (byte)'_', (byte)'M', (byte)'0');
        WriteM04LayoutAscii(console, serial, (byte)'4', (byte)'_', (byte)'0', (byte)'0', (byte)'5', (byte)'_', (byte)'R', (byte)'E');
        WriteM04LayoutAscii(console, serial, (byte)'A', (byte)'D', (byte)'Y', (byte)' ', (byte)'p', (byte)'r', (byte)'o', (byte)'f');
        WriteM04LayoutAscii(console, serial, (byte)'i', (byte)'l', (byte)'e', (byte)'=', (byte)'i', (byte)'l', (byte)'c', (byte)'1');
        WriteM04LayoutAscii(console, serial, (byte)'1', (byte)'.', (byte)'0', (byte)'.', (byte)'0', (byte)'-', (byte)'r', (byte)'c');
        WriteM04LayoutAscii(console, serial, (byte)'.', (byte)'3', (byte)'.', (byte)'2', (byte)'6', (byte)'5', (byte)'2', (byte)'2');
        WriteM04LayoutAscii(console, serial, (byte)'.', (byte)'1', (byte)'4', (byte)'8', (byte)'-', (byte)'w', (byte)'i', (byte)'n');
        WriteM04LayoutAscii(console, serial, (byte)'-', (byte)'x', (byte)'6', (byte)'4', (byte)'-', (byte)'l', (byte)'e', (byte)' ');
        WriteM04LayoutAscii(console, serial, (byte)'c', (byte)'h', (byte)'e', (byte)'c', (byte)'k', (byte)'s', 0, 0);
        WriteM04LayoutAscii(console, serial, (byte)'=', (byte)'1', (byte)'9', (byte)' ', (byte)'s', (byte)'h', (byte)'a', (byte)'p');
        WriteM04LayoutAscii(console, serial, (byte)'e', (byte)'s', (byte)'=', (byte)'3', (byte)' ', (byte)'p', (byte)'r', (byte)'e');
        WriteM04LayoutAscii(console, serial, (byte)'f', (byte)'i', (byte)'x', (byte)'B', (byte)'y', (byte)'t', (byte)'e', (byte)'s');
        WriteM04LayoutAscii(console, serial, (byte)'=', (byte)'2', (byte)'4', (byte)' ', (byte)'r', (byte)'e', (byte)'f', (byte)'e');
        WriteM04LayoutAscii(console, serial, (byte)'r', (byte)'e', (byte)'n', (byte)'c', (byte)'e', (byte)'F', (byte)'i', (byte)'l');
        WriteM04LayoutAscii(console, serial, (byte)'e', (byte)'s', (byte)'R', (byte)'e', (byte)'q', (byte)'u', (byte)'i', (byte)'r');
        WriteM04LayoutAscii(console, serial, (byte)'e', (byte)'d', (byte)'=', (byte)'f', (byte)'a', (byte)'l', (byte)'s', (byte)'e');
        WriteM04LayoutAscii(console, serial, (byte)' ', (byte)'h', (byte)'o', (byte)'s', (byte)'t', (byte)'C', (byte)'o', (byte)'n');
        WriteM04LayoutAscii(console, serial, (byte)'f', (byte)'o', (byte)'r', (byte)'m', (byte)'a', (byte)'n', (byte)'c', (byte)'e');
        WriteM04LayoutAscii(console, serial, (byte)'=', (byte)'f', (byte)'a', (byte)'l', (byte)'s', (byte)'e', (byte)' ', (byte)'g');
        WriteM04LayoutAscii(console, serial, (byte)'u', (byte)'e', (byte)'s', (byte)'t', (byte)'I', (byte)'n', (byte)'t', (byte)'e');
        WriteM04LayoutAscii(console, serial, (byte)'g', (byte)'r', (byte)'a', (byte)'t', (byte)'e', (byte)'d', (byte)'=', (byte)'t');
        WriteM04LayoutAscii(console, serial, (byte)'r', (byte)'u', (byte)'e', (byte)' ', (byte)'g', (byte)'a', (byte)'t', (byte)'e');
        WriteM04LayoutAscii(console, serial, (byte)'4', (byte)'=', (byte)'F', (byte)'a', (byte)'i', (byte)'l', 0, 0);
        WriteCrLf(console, serial);
    }

    private static void WriteM04LayoutContractFailureLine(
        EfiSimpleTextOutputProtocol* console,
        EfiSerialIoProtocol* serial)
    {
        WriteM04LayoutAscii(console, serial, (byte)'M', (byte)'0', (byte)'4', (byte)'_', (byte)'L', (byte)'A', (byte)'Y', (byte)'O');
        WriteM04LayoutAscii(console, serial, (byte)'U', (byte)'T', (byte)'_', (byte)'C', (byte)'O', (byte)'N', (byte)'T', (byte)'R');
        WriteM04LayoutAscii(console, serial, (byte)'A', (byte)'C', (byte)'T', (byte)'_', (byte)'F', (byte)'A', (byte)'I', (byte)'L');
        WriteM04LayoutAscii(console, serial, (byte)'E', (byte)'D', 0, 0, 0, 0, 0, 0);
        WriteCrLf(console, serial);
    }

    private static void WriteM04LayoutAscii(
        EfiSimpleTextOutputProtocol* console,
        EfiSerialIoProtocol* serial,
        byte b0,
        byte b1,
        byte b2,
        byte b3,
        byte b4,
        byte b5,
        byte b6,
        byte b7)
    {
        if (b0 != 0)
            Write(console, serial, b0);
        if (b1 != 0)
            Write(console, serial, b1);
        if (b2 != 0)
            Write(console, serial, b2);
        if (b3 != 0)
            Write(console, serial, b3);
        if (b4 != 0)
            Write(console, serial, b4);
        if (b5 != 0)
            Write(console, serial, b5);
        if (b6 != 0)
            Write(console, serial, b6);
        if (b7 != 0)
            Write(console, serial, b7);
    }
#endif
#if M02_NORMALIZED_INTERRUPT_FRAME_PAGE_PROBE
    private const uint M02NormalizedInterruptFrameAllocateAnyPages = 0;
    private const uint M02NormalizedInterruptFrameEfiLoaderData = 2;
    private const ulong M02NormalizedInterruptFramePageCount = 1;
    private const ulong M02NormalizedInterruptFramePageBytes = 4096;
    private const ulong M02NormalizedInterruptFrameExpectedFnv1A64 =
        0x428D0353A96B7ACEUL;

    private static bool WriteM02NormalizedInterruptFramePageProbe(
        EfiSimpleTextOutputProtocol* console,
        EfiSerialIoProtocol* serial,
        EfiSystemTable* systemTable)
    {
        WriteM02NormalizedInterruptFramePageStartLine(console, serial);

        EfiBootServices* bootServices =
            systemTable == null ? null : systemTable->BootServices;
        if (bootServices == null ||
            bootServices->AllocatePages == null ||
            bootServices->FreePages == null)
        {
            WriteM02NormalizedInterruptFramePageFailureLines(
                console,
                serial);
            return false;
        }

        ulong pageAddress = 0;
        ulong allocationStatus = bootServices->AllocatePages(
            M02NormalizedInterruptFrameAllocateAnyPages,
            M02NormalizedInterruptFrameEfiLoaderData,
            M02NormalizedInterruptFramePageCount,
            &pageAddress);
        bool allocationSucceeded = allocationStatus == 0;
        if (!allocationSucceeded)
        {
            WriteM02NormalizedInterruptFramePageFailureLines(
                console,
                serial);
            return false;
        }

        bool addressValid =
            pageAddress != 0 &&
            (pageAddress &
                (M02NormalizedInterruptFramePageBytes - 1)) == 0 &&
            OS.Kernel.Diagnostics
                .NormalizeErrorCodeNonErrorCodeInterruptFrame
                .IsCanonicalX64Address(pageAddress) &&
            pageAddress <=
                ~0UL - M02NormalizedInterruptFramePageBytes;
        ulong commonTargetAddress = addressValid
            ? pageAddress + M02NormalizedInterruptFramePageBytes
            : 0;
        addressValid =
            addressValid &&
            OS.Kernel.Diagnostics
                .NormalizeErrorCodeNonErrorCodeInterruptFrame
                .IsCanonicalX64Address(
                    pageAddress +
                    M02NormalizedInterruptFramePageBytes - 1) &&
            OS.Kernel.Diagnostics
                .NormalizeErrorCodeNonErrorCodeInterruptFrame
                .IsCanonicalX64Address(commonTargetAddress);

        bool resultsMatch = false;
        if (addressValid)
        {
            OS.Kernel.Diagnostics.NormalizeInterruptFramePageResult
                materializeResult =
                OS.Kernel.Diagnostics
                    .NormalizeErrorCodeNonErrorCodeInterruptFrame
                    .Materialize(
                        (byte*)pageAddress,
                        M02NormalizedInterruptFramePageBytes,
                        pageAddress,
                        commonTargetAddress);
            OS.Kernel.Diagnostics.NormalizeInterruptFramePageResult
                readBackResult =
                OS.Kernel.Diagnostics
                    .NormalizeErrorCodeNonErrorCodeInterruptFrame
                    .ValidateReadBack(
                        (byte*)pageAddress,
                        M02NormalizedInterruptFramePageBytes,
                        pageAddress,
                        commonTargetAddress);

            resultsMatch =
                OS.Kernel.Diagnostics
                    .NormalizeErrorCodeNonErrorCodeInterruptFrame
                    .VectorCount == 256 &&
                OS.Kernel.Diagnostics
                    .NormalizeErrorCodeNonErrorCodeInterruptFrame
                    .SlotByteCount == 16 &&
                OS.Kernel.Diagnostics
                    .NormalizeErrorCodeNonErrorCodeInterruptFrame
                    .PageByteCount == 4096 &&
                OS.Kernel.Diagnostics
                    .NormalizeErrorCodeNonErrorCodeInterruptFrame
                    .PageAlignment == 4096 &&
                OS.Kernel.Diagnostics
                    .NormalizeErrorCodeNonErrorCodeInterruptFrame
                    .NormalizedSemanticSlotCount == 3 &&
                OS.Kernel.Diagnostics
                    .NormalizeErrorCodeNonErrorCodeInterruptFrame
                    .HardwareErrorCodeVectorCount == 9 &&
                OS.Kernel.Diagnostics
                    .NormalizeErrorCodeNonErrorCodeInterruptFrame
                    .SyntheticErrorCodeVectorCount == 247 &&
                M02NormalizedInterruptFramePageResultMatches(
                    materializeResult,
                    OS.Kernel.Diagnostics
                        .NormalizeInterruptFramePageOperation
                        .Materialize,
                    pageAddress,
                    commonTargetAddress,
                    true) &&
                M02NormalizedInterruptFramePageResultMatches(
                    readBackResult,
                    OS.Kernel.Diagnostics
                        .NormalizeInterruptFramePageOperation
                        .ValidateReadBack,
                    pageAddress,
                    commonTargetAddress,
                    false) &&
                materializeResult.Fingerprint ==
                    readBackResult.Fingerprint;
        }

        ulong releaseStatus = bootServices->FreePages(
            pageAddress,
            M02NormalizedInterruptFramePageCount);
        if (!addressValid || !resultsMatch || releaseStatus != 0)
        {
            WriteM02NormalizedInterruptFramePageFailureLines(
                console,
                serial);
            return false;
        }

        WriteM02NormalizedInterruptFramePageSuccessLine(console, serial);
        WriteM02NormalizedInterruptFramePagePassLine(console, serial);
        return true;
    }

    private static bool M02NormalizedInterruptFramePageResultMatches(
        OS.Kernel.Diagnostics.NormalizeInterruptFramePageResult result,
        OS.Kernel.Diagnostics.NormalizeInterruptFramePageOperation operation,
        ulong pageAddress,
        ulong commonTargetAddress,
        bool materialized)
    {
        return result.IsInitialized &&
            result.Operation == operation &&
            result.Status ==
                OS.Kernel.Diagnostics
                    .NormalizeInterruptFramePageStatus.Success &&
            result.ModeledPageAddress == pageAddress &&
            result.CommonTargetAddress == commonTargetAddress &&
            result.PageCapacity ==
                M02NormalizedInterruptFramePageBytes &&
            result.RequiredBytes ==
                (int)M02NormalizedInterruptFramePageBytes &&
            result.BytesWritten ==
                (materialized
                    ? (int)M02NormalizedInterruptFramePageBytes
                    : 0) &&
            result.BytesCompared ==
                (int)M02NormalizedInterruptFramePageBytes &&
            result.FirstMismatchOffset == -1 &&
            result.HardwareErrorCodeVectorCount == 9 &&
            result.SyntheticErrorCodeVectorCount == 247 &&
            result.Fingerprint ==
                M02NormalizedInterruptFrameExpectedFnv1A64 &&
            result.DestinationMutated == materialized &&
            result.ReadBackPerformed &&
            result.Materialized == materialized &&
            result.ReadBackValidated == !materialized &&
            result.Succeeded;
    }

    private static void WriteM02NormalizedInterruptFramePageStartLine(
        EfiSimpleTextOutputProtocol* console,
        EfiSerialIoProtocol* serial)
    {
        WriteM02NormalizedInterruptFramePageAscii(console, serial, (byte)'T', (byte)'E', (byte)'S', (byte)'T', (byte)'_', (byte)'S', (byte)'T', (byte)'A');
        WriteM02NormalizedInterruptFramePageAscii(console, serial, (byte)'R', (byte)'T', (byte)' ', (byte)'m', (byte)'0', (byte)'2', (byte)'.', (byte)'0');
        WriteM02NormalizedInterruptFramePageAscii(console, serial, (byte)'1', (byte)'0', 0, 0, 0, 0, 0, 0);
        WriteCrLf(console, serial);
    }

    private static void WriteM02NormalizedInterruptFramePageSuccessLine(
        EfiSimpleTextOutputProtocol* console,
        EfiSerialIoProtocol* serial)
    {
        WriteM02NormalizedInterruptFramePageAscii(console, serial, (byte)'C', (byte)'L', (byte)'R', (byte)'O', (byte)'S', (byte)'_', (byte)'M', (byte)'0');
        WriteM02NormalizedInterruptFramePageAscii(console, serial, (byte)'2', (byte)'_', (byte)'0', (byte)'1', (byte)'0', (byte)'_', (byte)'R', (byte)'E');
        WriteM02NormalizedInterruptFramePageAscii(console, serial, (byte)'A', (byte)'D', (byte)'Y', (byte)' ', (byte)'v', (byte)'e', (byte)'c', (byte)'t');
        WriteM02NormalizedInterruptFramePageAscii(console, serial, (byte)'o', (byte)'r', (byte)'s', (byte)'=', (byte)'2', (byte)'5', (byte)'6', (byte)' ');
        WriteM02NormalizedInterruptFramePageAscii(console, serial, (byte)'h', (byte)'a', (byte)'r', (byte)'d', (byte)'w', (byte)'a', (byte)'r', (byte)'e');
        WriteM02NormalizedInterruptFramePageAscii(console, serial, (byte)'E', (byte)'r', (byte)'r', (byte)'o', (byte)'r', (byte)'C', (byte)'o', (byte)'d');
        WriteM02NormalizedInterruptFramePageAscii(console, serial, (byte)'e', (byte)'V', (byte)'e', (byte)'c', (byte)'t', (byte)'o', (byte)'r', (byte)'s');
        WriteM02NormalizedInterruptFramePageAscii(console, serial, (byte)'=', (byte)'9', (byte)' ', (byte)'s', (byte)'y', (byte)'n', (byte)'t', (byte)'h');
        WriteM02NormalizedInterruptFramePageAscii(console, serial, (byte)'e', (byte)'t', (byte)'i', (byte)'c', (byte)'E', (byte)'r', (byte)'r', (byte)'o');
        WriteM02NormalizedInterruptFramePageAscii(console, serial, (byte)'r', (byte)'C', (byte)'o', (byte)'d', (byte)'e', (byte)'V', (byte)'e', (byte)'c');
        WriteM02NormalizedInterruptFramePageAscii(console, serial, (byte)'t', (byte)'o', (byte)'r', (byte)'s', (byte)'=', (byte)'2', (byte)'4', (byte)'7');
        WriteM02NormalizedInterruptFramePageAscii(console, serial, (byte)' ', (byte)'s', (byte)'l', (byte)'o', (byte)'t', (byte)'B', (byte)'y', (byte)'t');
        WriteM02NormalizedInterruptFramePageAscii(console, serial, (byte)'e', (byte)'s', (byte)'=', (byte)'1', (byte)'6', (byte)' ', (byte)'p', (byte)'a');
        WriteM02NormalizedInterruptFramePageAscii(console, serial, (byte)'g', (byte)'e', (byte)'B', (byte)'y', (byte)'t', (byte)'e', (byte)'s', (byte)'=');
        WriteM02NormalizedInterruptFramePageAscii(console, serial, (byte)'4', (byte)'0', (byte)'9', (byte)'6', (byte)' ', (byte)'n', (byte)'o', (byte)'r');
        WriteM02NormalizedInterruptFramePageAscii(console, serial, (byte)'m', (byte)'a', (byte)'l', (byte)'i', (byte)'z', (byte)'e', (byte)'d', (byte)'S');
        WriteM02NormalizedInterruptFramePageAscii(console, serial, (byte)'l', (byte)'o', (byte)'t', (byte)'s', (byte)'=', (byte)'3', (byte)' ', (byte)'c');
        WriteM02NormalizedInterruptFramePageAscii(console, serial, (byte)'o', (byte)'m', (byte)'m', (byte)'o', (byte)'n', (byte)'T', (byte)'a', (byte)'r');
        WriteM02NormalizedInterruptFramePageAscii(console, serial, (byte)'g', (byte)'e', (byte)'t', (byte)'D', (byte)'e', (byte)'l', (byte)'t', (byte)'a');
        WriteM02NormalizedInterruptFramePageAscii(console, serial, (byte)'=', (byte)'4', (byte)'0', (byte)'9', (byte)'6', (byte)' ', (byte)'f', (byte)'n');
        WriteM02NormalizedInterruptFramePageAscii(console, serial, (byte)'v', (byte)'1', (byte)'a', (byte)'6', (byte)'4', (byte)'=', (byte)'4', (byte)'2');
        WriteM02NormalizedInterruptFramePageAscii(console, serial, (byte)'8', (byte)'D', (byte)'0', (byte)'3', (byte)'5', (byte)'3', (byte)'A', (byte)'9');
        WriteM02NormalizedInterruptFramePageAscii(console, serial, (byte)'6', (byte)'B', (byte)'7', (byte)'A', (byte)'C', (byte)'E', (byte)' ', (byte)'r');
        WriteM02NormalizedInterruptFramePageAscii(console, serial, (byte)'e', (byte)'a', (byte)'d', (byte)'B', (byte)'a', (byte)'c', (byte)'k', (byte)'=');
        WriteM02NormalizedInterruptFramePageAscii(console, serial, (byte)'t', (byte)'r', (byte)'u', (byte)'e', (byte)' ', (byte)'g', (byte)'u', (byte)'e');
        WriteM02NormalizedInterruptFramePageAscii(console, serial, (byte)'s', (byte)'t', (byte)'M', (byte)'a', (byte)'t', (byte)'e', (byte)'r', (byte)'i');
        WriteM02NormalizedInterruptFramePageAscii(console, serial, (byte)'a', (byte)'l', (byte)'i', (byte)'z', (byte)'a', (byte)'t', (byte)'i', (byte)'o');
        WriteM02NormalizedInterruptFramePageAscii(console, serial, (byte)'n', (byte)'=', (byte)'t', (byte)'r', (byte)'u', (byte)'e', (byte)' ', (byte)'l');
        WriteM02NormalizedInterruptFramePageAscii(console, serial, (byte)'o', (byte)'a', (byte)'d', (byte)'e', (byte)'r', (byte)'D', (byte)'a', (byte)'t');
        WriteM02NormalizedInterruptFramePageAscii(console, serial, (byte)'a', (byte)'=', (byte)'t', (byte)'r', (byte)'u', (byte)'e', (byte)' ', (byte)'p');
        WriteM02NormalizedInterruptFramePageAscii(console, serial, (byte)'a', (byte)'g', (byte)'e', (byte)'R', (byte)'e', (byte)'l', (byte)'e', (byte)'a');
        WriteM02NormalizedInterruptFramePageAscii(console, serial, (byte)'s', (byte)'e', (byte)'d', (byte)'=', (byte)'t', (byte)'r', (byte)'u', (byte)'e');
        WriteM02NormalizedInterruptFramePageAscii(console, serial, (byte)' ', (byte)'s', (byte)'t', (byte)'u', (byte)'b', (byte)'E', (byte)'x', (byte)'e');
        WriteM02NormalizedInterruptFramePageAscii(console, serial, (byte)'c', (byte)'u', (byte)'t', (byte)'i', (byte)'o', (byte)'n', (byte)'=', (byte)'f');
        WriteM02NormalizedInterruptFramePageAscii(console, serial, (byte)'a', (byte)'l', (byte)'s', (byte)'e', (byte)' ', (byte)'i', (byte)'d', (byte)'t');
        WriteM02NormalizedInterruptFramePageAscii(console, serial, (byte)'I', (byte)'n', (byte)'s', (byte)'t', (byte)'a', (byte)'l', (byte)'l', (byte)'e');
        WriteM02NormalizedInterruptFramePageAscii(console, serial, (byte)'d', (byte)'=', (byte)'f', (byte)'a', (byte)'l', (byte)'s', (byte)'e', (byte)' ');
        WriteM02NormalizedInterruptFramePageAscii(console, serial, (byte)'f', (byte)'a', (byte)'u', (byte)'l', (byte)'t', (byte)'T', (byte)'r', (byte)'i');
        WriteM02NormalizedInterruptFramePageAscii(console, serial, (byte)'g', (byte)'g', (byte)'e', (byte)'r', (byte)'e', (byte)'d', (byte)'=', (byte)'f');
        WriteM02NormalizedInterruptFramePageAscii(console, serial, (byte)'a', (byte)'l', (byte)'s', (byte)'e', (byte)' ', (byte)'m', (byte)'a', (byte)'r');
        WriteM02NormalizedInterruptFramePageAscii(console, serial, (byte)'k', (byte)'e', (byte)'r', (byte)'T', (byte)'r', (byte)'a', (byte)'n', (byte)'s');
        WriteM02NormalizedInterruptFramePageAscii(console, serial, (byte)'p', (byte)'o', (byte)'r', (byte)'t', (byte)'=', (byte)'u', (byte)'e', (byte)'f');
        WriteM02NormalizedInterruptFramePageAscii(console, serial, (byte)'i', (byte)'S', (byte)'e', (byte)'r', (byte)'i', (byte)'a', (byte)'l', (byte)' ');
        WriteM02NormalizedInterruptFramePageAscii(console, serial, (byte)'p', (byte)'o', (byte)'s', (byte)'t', (byte)'E', (byte)'x', (byte)'i', (byte)'t');
        WriteM02NormalizedInterruptFramePageAscii(console, serial, (byte)'B', (byte)'o', (byte)'o', (byte)'t', (byte)'S', (byte)'e', (byte)'r', (byte)'v');
        WriteM02NormalizedInterruptFramePageAscii(console, serial, (byte)'i', (byte)'c', (byte)'e', (byte)'s', (byte)'=', (byte)'f', (byte)'a', (byte)'l');
        WriteM02NormalizedInterruptFramePageAscii(console, serial, (byte)'s', (byte)'e', 0, 0, 0, 0, 0, 0);
        WriteCrLf(console, serial);
    }

    private static void WriteM02NormalizedInterruptFramePagePassLine(
        EfiSimpleTextOutputProtocol* console,
        EfiSerialIoProtocol* serial)
    {
        WriteM02NormalizedInterruptFramePageAscii(console, serial, (byte)'T', (byte)'E', (byte)'S', (byte)'T', (byte)'_', (byte)'P', (byte)'A', (byte)'S');
        WriteM02NormalizedInterruptFramePageAscii(console, serial, (byte)'S', (byte)' ', (byte)'m', (byte)'0', (byte)'2', (byte)'.', (byte)'0', (byte)'1');
        WriteM02NormalizedInterruptFramePageAscii(console, serial, (byte)'0', 0, 0, 0, 0, 0, 0, 0);
        WriteCrLf(console, serial);
    }

    private static void WriteM02NormalizedInterruptFramePageFailureLines(
        EfiSimpleTextOutputProtocol* console,
        EfiSerialIoProtocol* serial)
    {
        WriteM02NormalizedInterruptFramePageAscii(console, serial, (byte)'M', (byte)'0', (byte)'2', (byte)'_', (byte)'N', (byte)'O', (byte)'R', (byte)'M');
        WriteM02NormalizedInterruptFramePageAscii(console, serial, (byte)'A', (byte)'L', (byte)'I', (byte)'Z', (byte)'E', (byte)'D', (byte)'_', (byte)'I');
        WriteM02NormalizedInterruptFramePageAscii(console, serial, (byte)'N', (byte)'T', (byte)'E', (byte)'R', (byte)'R', (byte)'U', (byte)'P', (byte)'T');
        WriteM02NormalizedInterruptFramePageAscii(console, serial, (byte)'_', (byte)'F', (byte)'R', (byte)'A', (byte)'M', (byte)'E', (byte)'_', (byte)'P');
        WriteM02NormalizedInterruptFramePageAscii(console, serial, (byte)'A', (byte)'G', (byte)'E', (byte)'_', (byte)'F', (byte)'A', (byte)'I', (byte)'L');
        WriteM02NormalizedInterruptFramePageAscii(console, serial, (byte)'E', (byte)'D', 0, 0, 0, 0, 0, 0);
        WriteCrLf(console, serial);
        WriteM02NormalizedInterruptFramePageAscii(console, serial, (byte)'T', (byte)'E', (byte)'S', (byte)'T', (byte)'_', (byte)'F', (byte)'A', (byte)'I');
        WriteM02NormalizedInterruptFramePageAscii(console, serial, (byte)'L', (byte)' ', (byte)'m', (byte)'0', (byte)'2', (byte)'.', (byte)'0', (byte)'1');
        WriteM02NormalizedInterruptFramePageAscii(console, serial, (byte)'0', (byte)' ', (byte)'g', (byte)'u', (byte)'e', (byte)'s', (byte)'t', (byte)'_');
        WriteM02NormalizedInterruptFramePageAscii(console, serial, (byte)'m', (byte)'a', (byte)'t', (byte)'e', (byte)'r', (byte)'i', (byte)'a', (byte)'l');
        WriteM02NormalizedInterruptFramePageAscii(console, serial, (byte)'i', (byte)'z', (byte)'a', (byte)'t', (byte)'i', (byte)'o', (byte)'n', (byte)'_');
        WriteM02NormalizedInterruptFramePageAscii(console, serial, (byte)'r', (byte)'e', (byte)'a', (byte)'d', (byte)'b', (byte)'a', (byte)'c', (byte)'k');
        WriteM02NormalizedInterruptFramePageAscii(console, serial, (byte)'_', (byte)'o', (byte)'r', (byte)'_', (byte)'c', (byte)'l', (byte)'e', (byte)'a');
        WriteM02NormalizedInterruptFramePageAscii(console, serial, (byte)'n', (byte)'u', (byte)'p', 0, 0, 0, 0, 0);
        WriteCrLf(console, serial);
    }

    private static void WriteM02NormalizedInterruptFramePageAscii(
        EfiSimpleTextOutputProtocol* console,
        EfiSerialIoProtocol* serial,
        byte b0,
        byte b1,
        byte b2,
        byte b3,
        byte b4,
        byte b5,
        byte b6,
        byte b7)
    {
        if (b0 != 0)
            Write(console, serial, b0);
        if (b1 != 0)
            Write(console, serial, b1);
        if (b2 != 0)
            Write(console, serial, b2);
        if (b3 != 0)
            Write(console, serial, b3);
        if (b4 != 0)
            Write(console, serial, b4);
        if (b5 != 0)
            Write(console, serial, b5);
        if (b6 != 0)
            Write(console, serial, b6);
        if (b7 != 0)
            Write(console, serial, b7);
    }
#endif
#if M02_OWNED_IDT_GATE_POPULATION_PROBE
    private const uint M02OwnedIdtGateAllocateAnyPages = 0;
    private const uint M02OwnedIdtGateEfiLoaderData = 2;
    private const ulong M02OwnedIdtGateAllocationPages = 3;
    private const ulong M02OwnedIdtGatePageBytes = 4096;
    private const ulong M02OwnedIdtGateArenaBytes =
        M02OwnedIdtGateAllocationPages * M02OwnedIdtGatePageBytes;
    private const ulong M02OwnedIdtGateStubExpectedFnv1A64 =
        0x641BA73668C75E0EUL;
    private const ulong M02OwnedIdtGateNormalizedPolicyFnv1A64 =
        0x8514F42EBDFC5D55UL;

    private static bool WriteM02OwnedIdtGatePopulationProbe(
        EfiSimpleTextOutputProtocol* console,
        EfiSerialIoProtocol* serial,
        EfiSystemTable* systemTable)
    {
        WriteM02OwnedIdtGatePopulationStartLine(console, serial);

        EfiBootServices* bootServices =
            systemTable == null ? null : systemTable->BootServices;
        if (bootServices == null ||
            bootServices->AllocatePages == null ||
            bootServices->FreePages == null)
        {
            WriteM02OwnedIdtGatePopulationFailureLines(console, serial);
            return false;
        }

        ulong arenaAddress = 0;
        ulong allocationStatus = bootServices->AllocatePages(
            M02OwnedIdtGateAllocateAnyPages,
            M02OwnedIdtGateEfiLoaderData,
            M02OwnedIdtGateAllocationPages,
            &arenaAddress);
        if (allocationStatus != 0)
        {
            WriteM02OwnedIdtGatePopulationFailureLines(console, serial);
            return false;
        }

        bool addressValid =
            arenaAddress != 0 &&
            (arenaAddress & (M02OwnedIdtGatePageBytes - 1)) == 0 &&
            OS.Kernel.Diagnostics
                .PopulateAllN256IDTVectorsOwnedGatesNever
                .IsCanonicalX64Address(arenaAddress) &&
            arenaAddress <= ~0UL - M02OwnedIdtGateArenaBytes;
        ulong idtPageAddress = addressValid
            ? arenaAddress + M02OwnedIdtGatePageBytes
            : 0;
        ulong commonTargetAddress = addressValid
            ? arenaAddress + (2 * M02OwnedIdtGatePageBytes)
            : 0;
        ulong arenaEndInclusive = addressValid
            ? arenaAddress + M02OwnedIdtGateArenaBytes - 1
            : 0;
        addressValid =
            addressValid &&
            OS.Kernel.Diagnostics
                .PopulateAllN256IDTVectorsOwnedGatesNever
                .IsCanonicalX64Address(idtPageAddress) &&
            OS.Kernel.Diagnostics
                .PopulateAllN256IDTVectorsOwnedGatesNever
                .IsCanonicalX64Address(commonTargetAddress) &&
            OS.Kernel.Diagnostics
                .PopulateAllN256IDTVectorsOwnedGatesNever
                .IsCanonicalX64Address(arenaEndInclusive) &&
            ((arenaAddress >> 47) & 1UL) ==
                ((arenaEndInclusive >> 47) & 1UL);

        bool resultsMatch = false;
        if (addressValid)
        {
            OS.Kernel.Diagnostics.NormalizeInterruptFramePageResult
                stubMaterializeResult =
                OS.Kernel.Diagnostics
                    .NormalizeErrorCodeNonErrorCodeInterruptFrame
                    .Materialize(
                        (byte*)arenaAddress,
                        M02OwnedIdtGatePageBytes,
                        arenaAddress,
                        commonTargetAddress);
            OS.Kernel.Diagnostics.NormalizeInterruptFramePageResult
                stubReadBackResult =
                OS.Kernel.Diagnostics
                    .NormalizeErrorCodeNonErrorCodeInterruptFrame
                    .ValidateReadBack(
                        (byte*)arenaAddress,
                        M02OwnedIdtGatePageBytes,
                        arenaAddress,
                        commonTargetAddress);
            OS.Kernel.Diagnostics.OwnedIdtGateTableResult
                idtMaterializeResult =
                OS.Kernel.Diagnostics
                    .PopulateAllN256IDTVectorsOwnedGatesNever
                    .Materialize(
                        (byte*)idtPageAddress,
                        M02OwnedIdtGatePageBytes,
                        idtPageAddress,
                        arenaAddress);
            OS.Kernel.Diagnostics.OwnedIdtGateTableResult
                idtReadBackResult =
                OS.Kernel.Diagnostics
                    .PopulateAllN256IDTVectorsOwnedGatesNever
                    .ValidateReadBack(
                        (byte*)idtPageAddress,
                        M02OwnedIdtGatePageBytes,
                        idtPageAddress,
                        arenaAddress);

            resultsMatch =
                M02OwnedIdtGateStubResultMatches(
                    stubMaterializeResult,
                    OS.Kernel.Diagnostics
                        .NormalizeInterruptFramePageOperation.Materialize,
                    arenaAddress,
                    commonTargetAddress,
                    true) &&
                M02OwnedIdtGateStubResultMatches(
                    stubReadBackResult,
                    OS.Kernel.Diagnostics
                        .NormalizeInterruptFramePageOperation
                        .ValidateReadBack,
                    arenaAddress,
                    commonTargetAddress,
                    false) &&
                stubMaterializeResult.Fingerprint ==
                    stubReadBackResult.Fingerprint &&
                M02OwnedIdtGateTableResultMatches(
                    idtMaterializeResult,
                    OS.Kernel.Diagnostics
                        .OwnedIdtGateTableOperation.Materialize,
                    idtPageAddress,
                    arenaAddress,
                    true) &&
                M02OwnedIdtGateTableResultMatches(
                    idtReadBackResult,
                    OS.Kernel.Diagnostics
                        .OwnedIdtGateTableOperation.ValidateReadBack,
                    idtPageAddress,
                    arenaAddress,
                    false) &&
                idtMaterializeResult.Fingerprint ==
                    idtReadBackResult.Fingerprint;
        }

        ulong releaseStatus = bootServices->FreePages(
            arenaAddress,
            M02OwnedIdtGateAllocationPages);
        if (!addressValid || !resultsMatch || releaseStatus != 0)
        {
            WriteM02OwnedIdtGatePopulationFailureLines(console, serial);
            return false;
        }

        WriteM02OwnedIdtGatePopulationSuccessLine(console, serial);
        WriteM02OwnedIdtGatePopulationPassLine(console, serial);
        return true;
    }

    private static bool M02OwnedIdtGateStubResultMatches(
        OS.Kernel.Diagnostics.NormalizeInterruptFramePageResult result,
        OS.Kernel.Diagnostics.NormalizeInterruptFramePageOperation operation,
        ulong pageAddress,
        ulong commonTargetAddress,
        bool materialized)
    {
        return result.IsInitialized &&
            result.Operation == operation &&
            result.Status ==
                OS.Kernel.Diagnostics
                    .NormalizeInterruptFramePageStatus.Success &&
            result.ModeledPageAddress == pageAddress &&
            result.CommonTargetAddress == commonTargetAddress &&
            result.PageCapacity == M02OwnedIdtGatePageBytes &&
            result.RequiredBytes == (int)M02OwnedIdtGatePageBytes &&
            result.BytesWritten ==
                (materialized ? (int)M02OwnedIdtGatePageBytes : 0) &&
            result.BytesCompared == (int)M02OwnedIdtGatePageBytes &&
            result.FirstMismatchOffset == -1 &&
            result.HardwareErrorCodeVectorCount == 9 &&
            result.SyntheticErrorCodeVectorCount == 247 &&
            result.Fingerprint == M02OwnedIdtGateStubExpectedFnv1A64 &&
            result.DestinationMutated == materialized &&
            result.ReadBackPerformed &&
            result.Materialized == materialized &&
            result.ReadBackValidated == !materialized &&
            result.Succeeded;
    }

    private static bool M02OwnedIdtGateTableResultMatches(
        OS.Kernel.Diagnostics.OwnedIdtGateTableResult result,
        OS.Kernel.Diagnostics.OwnedIdtGateTableOperation operation,
        ulong tableAddress,
        ulong stubPageAddress,
        bool materialized)
    {
        return result.IsInitialized &&
            result.Operation == operation &&
            result.Status ==
                OS.Kernel.Diagnostics.OwnedIdtGateTableStatus.Success &&
            result.ModeledTableAddress == tableAddress &&
            result.OwnedStubPageAddress == stubPageAddress &&
            result.TableCapacity == M02OwnedIdtGatePageBytes &&
            result.RequiredBytes == (int)M02OwnedIdtGatePageBytes &&
            result.DescriptorCount == 256 &&
            result.PresentDescriptorCount == 256 &&
            result.IstRoutedDescriptorCount == 3 &&
            result.DefaultIstDescriptorCount == 253 &&
            result.FirmwareGateCopyCount == 0 &&
            result.BytesWritten ==
                (materialized ? (int)M02OwnedIdtGatePageBytes : 0) &&
            result.BytesCompared == (int)M02OwnedIdtGatePageBytes &&
            result.FirstMismatchOffset == -1 &&
            result.FirstMismatchVector == -1 &&
            result.NormalizedPolicyFingerprint ==
                M02OwnedIdtGateNormalizedPolicyFnv1A64 &&
            result.DestinationMutated == materialized &&
            result.ReadBackPerformed &&
            result.Materialized == materialized &&
            result.ReadBackValidated == !materialized &&
            result.Succeeded;
    }

    private static void WriteM02OwnedIdtGatePopulationStartLine(
        EfiSimpleTextOutputProtocol* console,
        EfiSerialIoProtocol* serial)
    {
        WriteM02OwnedIdtGatePopulationAscii(console, serial, (byte)'T', (byte)'E', (byte)'S', (byte)'T', (byte)'_', (byte)'S', (byte)'T', (byte)'A');
        WriteM02OwnedIdtGatePopulationAscii(console, serial, (byte)'R', (byte)'T', (byte)' ', (byte)'m', (byte)'0', (byte)'2', (byte)'.', (byte)'0');
        WriteM02OwnedIdtGatePopulationAscii(console, serial, (byte)'1', (byte)'1', 0, 0, 0, 0, 0, 0);
        WriteCrLf(console, serial);
    }

    private static void WriteM02OwnedIdtGatePopulationSuccessLine(
        EfiSimpleTextOutputProtocol* console,
        EfiSerialIoProtocol* serial)
    {
        WriteM02OwnedIdtGatePopulationAscii(console, serial, (byte)'C', (byte)'L', (byte)'R', (byte)'O', (byte)'S', (byte)'_', (byte)'M', (byte)'0');
        WriteM02OwnedIdtGatePopulationAscii(console, serial, (byte)'2', (byte)'_', (byte)'0', (byte)'1', (byte)'1', (byte)'_', (byte)'R', (byte)'E');
        WriteM02OwnedIdtGatePopulationAscii(console, serial, (byte)'A', (byte)'D', (byte)'Y', (byte)' ', (byte)'v', (byte)'e', (byte)'c', (byte)'t');
        WriteM02OwnedIdtGatePopulationAscii(console, serial, (byte)'o', (byte)'r', (byte)'s', (byte)'=', (byte)'2', (byte)'5', (byte)'6', (byte)' ');
        WriteM02OwnedIdtGatePopulationAscii(console, serial, (byte)'g', (byte)'a', (byte)'t', (byte)'e', (byte)'s', (byte)'=', (byte)'2', (byte)'5');
        WriteM02OwnedIdtGatePopulationAscii(console, serial, (byte)'6', (byte)' ', (byte)'g', (byte)'a', (byte)'t', (byte)'e', (byte)'B', (byte)'y');
        WriteM02OwnedIdtGatePopulationAscii(console, serial, (byte)'t', (byte)'e', (byte)'s', (byte)'=', (byte)'1', (byte)'6', (byte)' ', (byte)'t');
        WriteM02OwnedIdtGatePopulationAscii(console, serial, (byte)'a', (byte)'b', (byte)'l', (byte)'e', (byte)'B', (byte)'y', (byte)'t', (byte)'e');
        WriteM02OwnedIdtGatePopulationAscii(console, serial, (byte)'s', (byte)'=', (byte)'4', (byte)'0', (byte)'9', (byte)'6', (byte)' ', (byte)'s');
        WriteM02OwnedIdtGatePopulationAscii(console, serial, (byte)'e', (byte)'l', (byte)'e', (byte)'c', (byte)'t', (byte)'o', (byte)'r', (byte)'=');
        WriteM02OwnedIdtGatePopulationAscii(console, serial, (byte)'8', (byte)' ', (byte)'p', (byte)'r', (byte)'e', (byte)'s', (byte)'e', (byte)'n');
        WriteM02OwnedIdtGatePopulationAscii(console, serial, (byte)'t', (byte)'=', (byte)'2', (byte)'5', (byte)'6', (byte)' ', (byte)'d', (byte)'p');
        WriteM02OwnedIdtGatePopulationAscii(console, serial, (byte)'l', (byte)'0', (byte)'=', (byte)'2', (byte)'5', (byte)'6', (byte)' ', (byte)'i');
        WriteM02OwnedIdtGatePopulationAscii(console, serial, (byte)'n', (byte)'t', (byte)'e', (byte)'r', (byte)'r', (byte)'u', (byte)'p', (byte)'t');
        WriteM02OwnedIdtGatePopulationAscii(console, serial, (byte)'G', (byte)'a', (byte)'t', (byte)'e', (byte)'s', (byte)'=', (byte)'2', (byte)'5');
        WriteM02OwnedIdtGatePopulationAscii(console, serial, (byte)'6', (byte)' ', (byte)'o', (byte)'w', (byte)'n', (byte)'e', (byte)'d', (byte)'H');
        WriteM02OwnedIdtGatePopulationAscii(console, serial, (byte)'a', (byte)'n', (byte)'d', (byte)'l', (byte)'e', (byte)'r', (byte)'s', (byte)'=');
        WriteM02OwnedIdtGatePopulationAscii(console, serial, (byte)'2', (byte)'5', (byte)'6', (byte)' ', (byte)'f', (byte)'i', (byte)'r', (byte)'m');
        WriteM02OwnedIdtGatePopulationAscii(console, serial, (byte)'w', (byte)'a', (byte)'r', (byte)'e', (byte)'G', (byte)'a', (byte)'t', (byte)'e');
        WriteM02OwnedIdtGatePopulationAscii(console, serial, (byte)'s', (byte)'C', (byte)'o', (byte)'p', (byte)'i', (byte)'e', (byte)'d', (byte)'=');
        WriteM02OwnedIdtGatePopulationAscii(console, serial, (byte)'0', (byte)' ', (byte)'n', (byte)'m', (byte)'i', (byte)'I', (byte)'s', (byte)'t');
        WriteM02OwnedIdtGatePopulationAscii(console, serial, (byte)'=', (byte)'3', (byte)' ', (byte)'d', (byte)'o', (byte)'u', (byte)'b', (byte)'l');
        WriteM02OwnedIdtGatePopulationAscii(console, serial, (byte)'e', (byte)'F', (byte)'a', (byte)'u', (byte)'l', (byte)'t', (byte)'I', (byte)'s');
        WriteM02OwnedIdtGatePopulationAscii(console, serial, (byte)'t', (byte)'=', (byte)'1', (byte)' ', (byte)'p', (byte)'a', (byte)'g', (byte)'e');
        WriteM02OwnedIdtGatePopulationAscii(console, serial, (byte)'F', (byte)'a', (byte)'u', (byte)'l', (byte)'t', (byte)'I', (byte)'s', (byte)'t');
        WriteM02OwnedIdtGatePopulationAscii(console, serial, (byte)'=', (byte)'2', (byte)' ', (byte)'n', (byte)'o', (byte)'r', (byte)'m', (byte)'a');
        WriteM02OwnedIdtGatePopulationAscii(console, serial, (byte)'l', (byte)'i', (byte)'z', (byte)'e', (byte)'d', (byte)'F', (byte)'n', (byte)'v');
        WriteM02OwnedIdtGatePopulationAscii(console, serial, (byte)'1', (byte)'a', (byte)'6', (byte)'4', (byte)'=', (byte)'8', (byte)'5', (byte)'1');
        WriteM02OwnedIdtGatePopulationAscii(console, serial, (byte)'4', (byte)'F', (byte)'4', (byte)'2', (byte)'E', (byte)'B', (byte)'D', (byte)'F');
        WriteM02OwnedIdtGatePopulationAscii(console, serial, (byte)'C', (byte)'5', (byte)'D', (byte)'5', (byte)'5', (byte)' ', (byte)'r', (byte)'e');
        WriteM02OwnedIdtGatePopulationAscii(console, serial, (byte)'a', (byte)'d', (byte)'B', (byte)'a', (byte)'c', (byte)'k', (byte)'=', (byte)'t');
        WriteM02OwnedIdtGatePopulationAscii(console, serial, (byte)'r', (byte)'u', (byte)'e', (byte)' ', (byte)'g', (byte)'u', (byte)'e', (byte)'s');
        WriteM02OwnedIdtGatePopulationAscii(console, serial, (byte)'t', (byte)'P', (byte)'o', (byte)'p', (byte)'u', (byte)'l', (byte)'a', (byte)'t');
        WriteM02OwnedIdtGatePopulationAscii(console, serial, (byte)'i', (byte)'o', (byte)'n', (byte)'=', (byte)'t', (byte)'r', (byte)'u', (byte)'e');
        WriteM02OwnedIdtGatePopulationAscii(console, serial, (byte)' ', (byte)'l', (byte)'o', (byte)'a', (byte)'d', (byte)'e', (byte)'r', (byte)'D');
        WriteM02OwnedIdtGatePopulationAscii(console, serial, (byte)'a', (byte)'t', (byte)'a', (byte)'=', (byte)'t', (byte)'r', (byte)'u', (byte)'e');
        WriteM02OwnedIdtGatePopulationAscii(console, serial, (byte)' ', (byte)'a', (byte)'l', (byte)'l', (byte)'o', (byte)'c', (byte)'a', (byte)'t');
        WriteM02OwnedIdtGatePopulationAscii(console, serial, (byte)'i', (byte)'o', (byte)'n', (byte)'P', (byte)'a', (byte)'g', (byte)'e', (byte)'s');
        WriteM02OwnedIdtGatePopulationAscii(console, serial, (byte)'=', (byte)'3', (byte)' ', (byte)'a', (byte)'l', (byte)'l', (byte)'o', (byte)'c');
        WriteM02OwnedIdtGatePopulationAscii(console, serial, (byte)'a', (byte)'t', (byte)'i', (byte)'o', (byte)'n', (byte)'R', (byte)'e', (byte)'l');
        WriteM02OwnedIdtGatePopulationAscii(console, serial, (byte)'e', (byte)'a', (byte)'s', (byte)'e', (byte)'d', (byte)'=', (byte)'t', (byte)'r');
        WriteM02OwnedIdtGatePopulationAscii(console, serial, (byte)'u', (byte)'e', (byte)' ', (byte)'s', (byte)'t', (byte)'u', (byte)'b', (byte)'E');
        WriteM02OwnedIdtGatePopulationAscii(console, serial, (byte)'x', (byte)'e', (byte)'c', (byte)'u', (byte)'t', (byte)'i', (byte)'o', (byte)'n');
        WriteM02OwnedIdtGatePopulationAscii(console, serial, (byte)'=', (byte)'f', (byte)'a', (byte)'l', (byte)'s', (byte)'e', (byte)' ', (byte)'i');
        WriteM02OwnedIdtGatePopulationAscii(console, serial, (byte)'d', (byte)'t', (byte)'r', (byte)'L', (byte)'o', (byte)'a', (byte)'d', (byte)'e');
        WriteM02OwnedIdtGatePopulationAscii(console, serial, (byte)'d', (byte)'=', (byte)'f', (byte)'a', (byte)'l', (byte)'s', (byte)'e', (byte)' ');
        WriteM02OwnedIdtGatePopulationAscii(console, serial, (byte)'f', (byte)'a', (byte)'u', (byte)'l', (byte)'t', (byte)'T', (byte)'r', (byte)'i');
        WriteM02OwnedIdtGatePopulationAscii(console, serial, (byte)'g', (byte)'g', (byte)'e', (byte)'r', (byte)'e', (byte)'d', (byte)'=', (byte)'f');
        WriteM02OwnedIdtGatePopulationAscii(console, serial, (byte)'a', (byte)'l', (byte)'s', (byte)'e', (byte)' ', (byte)'m', (byte)'a', (byte)'r');
        WriteM02OwnedIdtGatePopulationAscii(console, serial, (byte)'k', (byte)'e', (byte)'r', (byte)'T', (byte)'r', (byte)'a', (byte)'n', (byte)'s');
        WriteM02OwnedIdtGatePopulationAscii(console, serial, (byte)'p', (byte)'o', (byte)'r', (byte)'t', (byte)'=', (byte)'u', (byte)'e', (byte)'f');
        WriteM02OwnedIdtGatePopulationAscii(console, serial, (byte)'i', (byte)'S', (byte)'e', (byte)'r', (byte)'i', (byte)'a', (byte)'l', (byte)' ');
        WriteM02OwnedIdtGatePopulationAscii(console, serial, (byte)'p', (byte)'o', (byte)'s', (byte)'t', (byte)'E', (byte)'x', (byte)'i', (byte)'t');
        WriteM02OwnedIdtGatePopulationAscii(console, serial, (byte)'B', (byte)'o', (byte)'o', (byte)'t', (byte)'S', (byte)'e', (byte)'r', (byte)'v');
        WriteM02OwnedIdtGatePopulationAscii(console, serial, (byte)'i', (byte)'c', (byte)'e', (byte)'s', (byte)'=', (byte)'f', (byte)'a', (byte)'l');
        WriteM02OwnedIdtGatePopulationAscii(console, serial, (byte)'s', (byte)'e', 0, 0, 0, 0, 0, 0);
        WriteCrLf(console, serial);
    }

    private static void WriteM02OwnedIdtGatePopulationPassLine(
        EfiSimpleTextOutputProtocol* console,
        EfiSerialIoProtocol* serial)
    {
        WriteM02OwnedIdtGatePopulationAscii(console, serial, (byte)'T', (byte)'E', (byte)'S', (byte)'T', (byte)'_', (byte)'P', (byte)'A', (byte)'S');
        WriteM02OwnedIdtGatePopulationAscii(console, serial, (byte)'S', (byte)' ', (byte)'m', (byte)'0', (byte)'2', (byte)'.', (byte)'0', (byte)'1');
        WriteM02OwnedIdtGatePopulationAscii(console, serial, (byte)'1', 0, 0, 0, 0, 0, 0, 0);
        WriteCrLf(console, serial);
    }

    private static void WriteM02OwnedIdtGatePopulationFailureLines(
        EfiSimpleTextOutputProtocol* console,
        EfiSerialIoProtocol* serial)
    {
        WriteM02OwnedIdtGatePopulationAscii(console, serial, (byte)'M', (byte)'0', (byte)'2', (byte)'_', (byte)'O', (byte)'W', (byte)'N', (byte)'E');
        WriteM02OwnedIdtGatePopulationAscii(console, serial, (byte)'D', (byte)'_', (byte)'I', (byte)'D', (byte)'T', (byte)'_', (byte)'G', (byte)'A');
        WriteM02OwnedIdtGatePopulationAscii(console, serial, (byte)'T', (byte)'E', (byte)'_', (byte)'P', (byte)'O', (byte)'P', (byte)'U', (byte)'L');
        WriteM02OwnedIdtGatePopulationAscii(console, serial, (byte)'A', (byte)'T', (byte)'I', (byte)'O', (byte)'N', (byte)'_', (byte)'F', (byte)'A');
        WriteM02OwnedIdtGatePopulationAscii(console, serial, (byte)'I', (byte)'L', (byte)'E', (byte)'D', 0, 0, 0, 0);
        WriteCrLf(console, serial);
        WriteM02OwnedIdtGatePopulationAscii(console, serial, (byte)'T', (byte)'E', (byte)'S', (byte)'T', (byte)'_', (byte)'F', (byte)'A', (byte)'I');
        WriteM02OwnedIdtGatePopulationAscii(console, serial, (byte)'L', (byte)' ', (byte)'m', (byte)'0', (byte)'2', (byte)'.', (byte)'0', (byte)'1');
        WriteM02OwnedIdtGatePopulationAscii(console, serial, (byte)'1', (byte)' ', (byte)'g', (byte)'u', (byte)'e', (byte)'s', (byte)'t', (byte)'_');
        WriteM02OwnedIdtGatePopulationAscii(console, serial, (byte)'p', (byte)'o', (byte)'p', (byte)'u', (byte)'l', (byte)'a', (byte)'t', (byte)'i');
        WriteM02OwnedIdtGatePopulationAscii(console, serial, (byte)'o', (byte)'n', (byte)'_', (byte)'r', (byte)'e', (byte)'a', (byte)'d', (byte)'b');
        WriteM02OwnedIdtGatePopulationAscii(console, serial, (byte)'a', (byte)'c', (byte)'k', (byte)'_', (byte)'o', (byte)'r', (byte)'_', (byte)'c');
        WriteM02OwnedIdtGatePopulationAscii(console, serial, (byte)'l', (byte)'e', (byte)'a', (byte)'n', (byte)'u', (byte)'p', 0, 0);
        WriteCrLf(console, serial);
    }

    private static void WriteM02OwnedIdtGatePopulationAscii(
        EfiSimpleTextOutputProtocol* console,
        EfiSerialIoProtocol* serial,
        byte b0,
        byte b1,
        byte b2,
        byte b3,
        byte b4,
        byte b5,
        byte b6,
        byte b7)
    {
        if (b0 != 0)
            Write(console, serial, b0);
        if (b1 != 0)
            Write(console, serial, b1);
        if (b2 != 0)
            Write(console, serial, b2);
        if (b3 != 0)
            Write(console, serial, b3);
        if (b4 != 0)
            Write(console, serial, b4);
        if (b5 != 0)
            Write(console, serial, b5);
        if (b6 != 0)
            Write(console, serial, b6);
        if (b7 != 0)
            Write(console, serial, b7);
    }
#endif
#if M02_IDTR_CONTINUED_EXECUTION_PROBE
    private const uint M02IdtrContinuedExecutionAllocateAnyPages = 0;
    private const uint M02IdtrContinuedExecutionEfiLoaderCode = 1;
    private const uint M02IdtrContinuedExecutionEfiLoaderData = 2;
    private const ulong M02IdtrContinuedExecutionPageBytes = 4096;
    private const ulong M02IdtrContinuedExecutionCodePages = 2;
    private const ulong M02IdtrContinuedExecutionDataPages = 1;
    private const ulong M02IdtrContinuedExecutionCodeBytes = 8192;
    private const ulong M02IdtrContinuedExecutionDataBytes = 4096;
    private const ulong M02IdtrContinuedExecutionCommonTargetOffset = 4096;
    private const ulong M02IdtrContinuedExecutionTransitionOffset = 0x1040;
    private const ulong M02IdtrContinuedExecutionContextWorkspaceBytes = 96;
    private const ulong M02IdtrContinuedExecutionNormalizedFnv1A64 =
        0x428D0353A96B7ACEUL;
    private const ulong M02IdtrContinuedExecutionPolicyFnv1A64 =
        0x8514F42EBDFC5D55UL;

    private static bool WriteM02IdtrContinuedExecutionProbe(
        EfiSimpleTextOutputProtocol* console,
        EfiSerialIoProtocol* serial,
        EfiSystemTable* systemTable)
    {
        WriteM02IdtrContinuedExecutionStartLine(console, serial);

        EfiBootServices* bootServices = systemTable == null
            ? null
            : systemTable->BootServices;
        ulong codeAddress = 0;
        ulong dataAddress = 0;
        bool codeAllocated = false;
        bool dataAllocated = false;

        if (bootServices == null ||
            bootServices->AllocatePages == null ||
            bootServices->FreePages == null)
        {
            return FailM02IdtrContinuedExecutionBeforeInvocation(
                console,
                serial,
                bootServices,
                codeAddress,
                codeAllocated,
                dataAddress,
                dataAllocated);
        }

        ulong allocationStatus = bootServices->AllocatePages(
            M02IdtrContinuedExecutionAllocateAnyPages,
            M02IdtrContinuedExecutionEfiLoaderCode,
            M02IdtrContinuedExecutionCodePages,
            &codeAddress);
        codeAllocated = allocationStatus == 0;
        if (!codeAllocated ||
            !IsValidM02IdtrContinuedExecutionRange(
                codeAddress,
                M02IdtrContinuedExecutionCodeBytes,
                M02IdtrContinuedExecutionPageBytes))
        {
            return FailM02IdtrContinuedExecutionBeforeInvocation(
                console,
                serial,
                bootServices,
                codeAddress,
                codeAllocated,
                dataAddress,
                dataAllocated);
        }

        allocationStatus = bootServices->AllocatePages(
            M02IdtrContinuedExecutionAllocateAnyPages,
            M02IdtrContinuedExecutionEfiLoaderData,
            M02IdtrContinuedExecutionDataPages,
            &dataAddress);
        dataAllocated = allocationStatus == 0;
        if (!dataAllocated ||
            !IsValidM02IdtrContinuedExecutionRange(
                dataAddress,
                M02IdtrContinuedExecutionDataBytes,
                M02IdtrContinuedExecutionPageBytes) ||
            M02IdtrContinuedExecutionRangesOverlap(
                codeAddress,
                M02IdtrContinuedExecutionCodeBytes,
                dataAddress,
                M02IdtrContinuedExecutionDataBytes))
        {
            return FailM02IdtrContinuedExecutionBeforeInvocation(
                console,
                serial,
                bootServices,
                codeAddress,
                codeAllocated,
                dataAddress,
                dataAllocated);
        }

        M02IdtrContinuedExecutionStackWorkspace stackWorkspace = default;
        byte* workspaceStart = stackWorkspace.Bytes;
        ulong workspaceAddress = (ulong)workspaceStart;
        if (workspaceAddress >
            ~0UL -
                (OS.Kernel.Diagnostics.IDTRContinuedExecution
                    .ContextAlignment - 1))
        {
            return FailM02IdtrContinuedExecutionBeforeInvocation(
                console,
                serial,
                bootServices,
                codeAddress,
                codeAllocated,
                dataAddress,
                dataAllocated);
        }

        ulong contextAddress =
            (workspaceAddress +
                (OS.Kernel.Diagnostics.IDTRContinuedExecution
                    .ContextAlignment - 1)) &
            ~(OS.Kernel.Diagnostics.IDTRContinuedExecution
                .ContextAlignment - 1);
        if (!IsValidM02IdtrContinuedExecutionRange(
                contextAddress,
                (ulong)OS.Kernel.Diagnostics.IDTRContinuedExecution
                    .ContextByteCount,
                OS.Kernel.Diagnostics.IDTRContinuedExecution
                    .ContextAlignment) ||
            contextAddress < workspaceAddress ||
            contextAddress +
                (ulong)OS.Kernel.Diagnostics.IDTRContinuedExecution
                    .ContextByteCount >
                workspaceAddress +
                    M02IdtrContinuedExecutionContextWorkspaceBytes ||
            M02IdtrContinuedExecutionRangesOverlap(
                codeAddress,
                M02IdtrContinuedExecutionCodeBytes,
                contextAddress,
                (ulong)OS.Kernel.Diagnostics.IDTRContinuedExecution
                    .ContextByteCount) ||
            M02IdtrContinuedExecutionRangesOverlap(
                dataAddress,
                M02IdtrContinuedExecutionDataBytes,
                contextAddress,
                (ulong)OS.Kernel.Diagnostics.IDTRContinuedExecution
                    .ContextByteCount))
        {
            return FailM02IdtrContinuedExecutionBeforeInvocation(
                console,
                serial,
                bootServices,
                codeAddress,
                codeAllocated,
                dataAddress,
                dataAllocated);
        }

        ulong commonTargetAddress =
            codeAddress + M02IdtrContinuedExecutionCommonTargetOffset;
        ulong transitionAddress =
            codeAddress + M02IdtrContinuedExecutionTransitionOffset;
        if (!IsValidM02IdtrContinuedExecutionRange(
                commonTargetAddress,
                6,
                1) ||
            !IsValidM02IdtrContinuedExecutionRange(
                transitionAddress,
                (ulong)OS.Kernel.Diagnostics.IDTRContinuedExecution
                    .StubByteCount,
                OS.Kernel.Diagnostics.IDTRContinuedExecution
                    .StubAlignment))
        {
            return FailM02IdtrContinuedExecutionBeforeInvocation(
                console,
                serial,
                bootServices,
                codeAddress,
                codeAllocated,
                dataAddress,
                dataAllocated);
        }

        byte* code = (byte*)codeAddress;
        byte* data = (byte*)dataAddress;
        for (ulong offset = 0;
            offset < M02IdtrContinuedExecutionCodeBytes;
            offset++)
        {
            code[offset] = 0;
        }
        for (ulong offset = 0;
            offset < M02IdtrContinuedExecutionDataBytes;
            offset++)
        {
            data[offset] = 0;
        }

        OS.Kernel.Diagnostics.NormalizeInterruptFramePageResult
            normalizedMaterialized =
                OS.Kernel.Diagnostics
                    .NormalizeErrorCodeNonErrorCodeInterruptFrame
                    .Materialize(
                        code,
                        M02IdtrContinuedExecutionPageBytes,
                        codeAddress,
                        commonTargetAddress);

        byte* commonTarget = (byte*)commonTargetAddress;
        commonTarget[0] = 0x48;
        commonTarget[1] = 0x83;
        commonTarget[2] = 0xC4;
        commonTarget[3] = 0x18;
        commonTarget[4] = 0x48;
        commonTarget[5] = 0xCF;

        OS.Kernel.Diagnostics.NormalizeInterruptFramePageResult
            normalizedReadBack =
                OS.Kernel.Diagnostics
                    .NormalizeErrorCodeNonErrorCodeInterruptFrame
                    .ValidateReadBack(
                        code,
                        M02IdtrContinuedExecutionPageBytes,
                        codeAddress,
                        commonTargetAddress);
        OS.Kernel.Diagnostics.OwnedIdtGateTableResult idtMaterialized =
            OS.Kernel.Diagnostics
                .PopulateAllN256IDTVectorsOwnedGatesNever
                .Materialize(
                    data,
                    M02IdtrContinuedExecutionDataBytes,
                    dataAddress,
                    codeAddress);
        OS.Kernel.Diagnostics.OwnedIdtGateTableResult idtReadBack =
            OS.Kernel.Diagnostics
                .PopulateAllN256IDTVectorsOwnedGatesNever
                .ValidateReadBack(
                    data,
                    M02IdtrContinuedExecutionDataBytes,
                    dataAddress,
                    codeAddress);

        OS.Kernel.Diagnostics.IDTRContinuedExecutionContext* context =
            (OS.Kernel.Diagnostics.IDTRContinuedExecutionContext*)
                contextAddress;
        OS.Kernel.Diagnostics.IDTRContinuedExecutionPlanResult
            contextPrepared =
                OS.Kernel.Diagnostics.IDTRContinuedExecution
                    .PrepareContext(
                        context,
                        contextAddress,
                        (ulong)OS.Kernel.Diagnostics
                            .IDTRContinuedExecution.ContextByteCount,
                        dataAddress);
        OS.Kernel.Diagnostics.IDTRContinuedExecutionPlanResult stubEmitted =
            OS.Kernel.Diagnostics.IDTRContinuedExecution.EmitStub(
                (byte*)transitionAddress,
                transitionAddress,
                M02IdtrContinuedExecutionPageBytes - 0x40,
                context,
                contextAddress,
                (ulong)OS.Kernel.Diagnostics.IDTRContinuedExecution
                    .ContextByteCount,
                dataAddress);
        OS.Kernel.Diagnostics.IDTRContinuedExecutionPlanResult stubReadBack =
            OS.Kernel.Diagnostics.IDTRContinuedExecution
                .ValidateStubReadBack(
                    (byte*)transitionAddress,
                    transitionAddress,
                    M02IdtrContinuedExecutionPageBytes - 0x40,
                    context,
                    contextAddress,
                    (ulong)OS.Kernel.Diagnostics.IDTRContinuedExecution
                        .ContextByteCount,
                    dataAddress);

        if (!M02IdtrContinuedExecutionNormalizedResultMatches(
                normalizedMaterialized,
                OS.Kernel.Diagnostics.NormalizeInterruptFramePageOperation
                    .Materialize,
                codeAddress,
                commonTargetAddress,
                true) ||
            !M02IdtrContinuedExecutionNormalizedResultMatches(
                normalizedReadBack,
                OS.Kernel.Diagnostics.NormalizeInterruptFramePageOperation
                    .ValidateReadBack,
                codeAddress,
                commonTargetAddress,
                false) ||
            normalizedMaterialized.Fingerprint !=
                normalizedReadBack.Fingerprint ||
            !M02IdtrContinuedExecutionIdtResultMatches(
                idtMaterialized,
                OS.Kernel.Diagnostics.OwnedIdtGateTableOperation.Materialize,
                dataAddress,
                codeAddress,
                true) ||
            !M02IdtrContinuedExecutionIdtResultMatches(
                idtReadBack,
                OS.Kernel.Diagnostics.OwnedIdtGateTableOperation
                    .ValidateReadBack,
                dataAddress,
                codeAddress,
                false) ||
            idtMaterialized.Fingerprint != idtReadBack.Fingerprint ||
            !M02IdtrContinuedExecutionPlanMatches(
                contextPrepared,
                OS.Kernel.Diagnostics.IDTRContinuedExecutionOperation
                    .PrepareContext,
                contextAddress,
                transitionAddress,
                dataAddress) ||
            !M02IdtrContinuedExecutionPlanMatches(
                stubEmitted,
                OS.Kernel.Diagnostics.IDTRContinuedExecutionOperation
                    .EmitStub,
                contextAddress,
                transitionAddress,
                dataAddress) ||
            !M02IdtrContinuedExecutionPlanMatches(
                stubReadBack,
                OS.Kernel.Diagnostics.IDTRContinuedExecutionOperation
                    .ValidateStubReadBack,
                contextAddress,
                transitionAddress,
                dataAddress) ||
            contextPrepared.ContextFingerprint !=
                stubEmitted.ContextFingerprint ||
            contextPrepared.ContextFingerprint !=
                stubReadBack.ContextFingerprint ||
            !M02IdtrContinuedExecutionCommonTargetMatches(commonTarget) ||
            OS.Kernel.Diagnostics.IDTRContinuedExecution.StubFnv1A64 !=
                0xAFF59D6480DE7F3CUL ||
            OS.Kernel.Diagnostics.IDTRContinuedExecution
                .HasExternalRelocations)
        {
            return FailM02IdtrContinuedExecutionBeforeInvocation(
                console,
                serial,
                bootServices,
                codeAddress,
                codeAllocated,
                dataAddress,
                dataAllocated);
        }

        uint publicationCookie = X64InstructionPublication.Serialize();
        if (publicationCookie !=
            X64InstructionPublication.ExpectedSuccessCookie)
        {
            return FailM02IdtrContinuedExecutionBeforeInvocation(
                console,
                serial,
                bootServices,
                codeAddress,
                codeAllocated,
                dataAddress,
                dataAllocated);
        }

        delegate* unmanaged<
            OS.Kernel.Diagnostics.IDTRContinuedExecutionContext*,
            ulong> transition =
                (delegate* unmanaged<
                    OS.Kernel.Diagnostics.IDTRContinuedExecutionContext*,
                    ulong>)(void*)transitionAddress;
        ulong executionCookie = transition(context);

        if (!M02IdtrContinuedExecutionSafetyRestored(
                context,
                dataAddress))
        {
            SpinM02IdtrContinuedExecutionAfterUnverifiedRestore();
            return false;
        }

        OS.Kernel.Diagnostics.IDTRContinuedExecutionAuditResult audit =
            OS.Kernel.Diagnostics.IDTRContinuedExecution.AuditExecution(
                context,
                contextAddress,
                (ulong)OS.Kernel.Diagnostics.IDTRContinuedExecution
                    .ContextByteCount,
                dataAddress,
                executionCookie);
        if (!M02IdtrContinuedExecutionAuditMatches(
                audit,
                contextAddress,
                dataAddress,
                executionCookie))
        {
            _ = bootServices->FreePages(
                dataAddress,
                M02IdtrContinuedExecutionDataPages);
            _ = bootServices->FreePages(
                codeAddress,
                M02IdtrContinuedExecutionCodePages);
            WriteM02IdtrContinuedExecutionFailureLines(console, serial);
            return false;
        }

        ulong dataReleaseStatus = bootServices->FreePages(
            dataAddress,
            M02IdtrContinuedExecutionDataPages);
        ulong codeReleaseStatus = bootServices->FreePages(
            codeAddress,
            M02IdtrContinuedExecutionCodePages);
        if (dataReleaseStatus != 0 || codeReleaseStatus != 0)
        {
            WriteM02IdtrContinuedExecutionFailureLines(console, serial);
            return false;
        }

        WriteM02IdtrContinuedExecutionSuccessLine(console, serial);
        WriteM02IdtrContinuedExecutionPassLine(console, serial);
        return true;
    }

    private static bool M02IdtrContinuedExecutionNormalizedResultMatches(
        OS.Kernel.Diagnostics.NormalizeInterruptFramePageResult result,
        OS.Kernel.Diagnostics.NormalizeInterruptFramePageOperation operation,
        ulong pageAddress,
        ulong commonTargetAddress,
        bool materialized)
    {
        return result.IsInitialized &&
            result.Operation == operation &&
            result.Status == OS.Kernel.Diagnostics
                .NormalizeInterruptFramePageStatus.Success &&
            result.ModeledPageAddress == pageAddress &&
            result.CommonTargetAddress == commonTargetAddress &&
            result.PageCapacity == M02IdtrContinuedExecutionPageBytes &&
            result.RequiredBytes == 4096 &&
            result.BytesWritten == (materialized ? 4096 : 0) &&
            result.BytesCompared == 4096 &&
            result.FirstMismatchOffset == -1 &&
            result.HardwareErrorCodeVectorCount == 9 &&
            result.SyntheticErrorCodeVectorCount == 247 &&
            result.Fingerprint ==
                M02IdtrContinuedExecutionNormalizedFnv1A64 &&
            result.DestinationMutated == materialized &&
            result.ReadBackPerformed &&
            result.Materialized == materialized &&
            result.ReadBackValidated == !materialized &&
            result.Succeeded;
    }

    private static bool M02IdtrContinuedExecutionIdtResultMatches(
        OS.Kernel.Diagnostics.OwnedIdtGateTableResult result,
        OS.Kernel.Diagnostics.OwnedIdtGateTableOperation operation,
        ulong tableAddress,
        ulong stubPageAddress,
        bool materialized)
    {
        return result.IsInitialized &&
            result.Operation == operation &&
            result.Status ==
                OS.Kernel.Diagnostics.OwnedIdtGateTableStatus.Success &&
            result.ModeledTableAddress == tableAddress &&
            result.OwnedStubPageAddress == stubPageAddress &&
            result.TableCapacity == M02IdtrContinuedExecutionDataBytes &&
            result.RequiredBytes == 4096 &&
            result.DescriptorCount == 256 &&
            result.PresentDescriptorCount == 256 &&
            result.IstRoutedDescriptorCount == 3 &&
            result.DefaultIstDescriptorCount == 253 &&
            result.FirmwareGateCopyCount == 0 &&
            result.BytesWritten == (materialized ? 4096 : 0) &&
            result.BytesCompared == 4096 &&
            result.FirstMismatchOffset == -1 &&
            result.FirstMismatchVector == -1 &&
            result.NormalizedPolicyFingerprint ==
                M02IdtrContinuedExecutionPolicyFnv1A64 &&
            result.DestinationMutated == materialized &&
            result.ReadBackPerformed &&
            result.Materialized == materialized &&
            result.ReadBackValidated == !materialized &&
            result.Succeeded;
    }

    private static bool M02IdtrContinuedExecutionPlanMatches(
        OS.Kernel.Diagnostics.IDTRContinuedExecutionPlanResult result,
        OS.Kernel.Diagnostics.IDTRContinuedExecutionOperation operation,
        ulong contextAddress,
        ulong transitionAddress,
        ulong idtAddress)
    {
        bool operationMatches = operation ==
                OS.Kernel.Diagnostics.IDTRContinuedExecutionOperation
                    .PrepareContext
            ? result.ContextPrepared
            : operation ==
                OS.Kernel.Diagnostics.IDTRContinuedExecutionOperation
                    .EmitStub
                ? result.StubEmitted
                : result.ReadBackValidated;
        return operationMatches &&
            result.Operation == operation &&
            result.Status == OS.Kernel.Diagnostics
                .IDTRContinuedExecutionStatus.Success &&
            result.ModeledContextAddress == contextAddress &&
            result.ModeledStubAddress ==
                (operation ==
                    OS.Kernel.Diagnostics.IDTRContinuedExecutionOperation
                        .PrepareContext
                    ? 0
                    : transitionAddress) &&
            result.OwnedIdtAddress == idtAddress &&
            result.ContextCapacity == 80 &&
            result.StubCapacity ==
                (operation ==
                    OS.Kernel.Diagnostics.IDTRContinuedExecutionOperation
                        .PrepareContext
                    ? 0
                    : M02IdtrContinuedExecutionPageBytes - 0x40) &&
            result.FirstMismatchOffset == -1 &&
            result.ContextFingerprint != 0 &&
            (operation ==
                OS.Kernel.Diagnostics.IDTRContinuedExecutionOperation
                    .PrepareContext ||
                result.StubFingerprint == 0xAFF59D6480DE7F3CUL) &&
            result.Succeeded;
    }

    private static bool M02IdtrContinuedExecutionAuditMatches(
        OS.Kernel.Diagnostics.IDTRContinuedExecutionAuditResult audit,
        ulong contextAddress,
        ulong idtAddress,
        ulong executionCookie)
    {
        return audit.IsInitialized &&
            audit.Operation == OS.Kernel.Diagnostics
                .IDTRContinuedExecutionOperation.AuditExecution &&
            audit.Status == OS.Kernel.Diagnostics
                .IDTRContinuedExecutionStatus.Success &&
            audit.ModeledContextAddress == contextAddress &&
            audit.OwnedIdtAddress == idtAddress &&
            audit.ContextCapacity == 80 &&
            audit.ExpectedCookie == 0x4F53494454523132UL &&
            audit.ObservedCookie == executionCookie &&
            executionCookie == 0x4F53494454523132UL &&
            audit.ContextBytesCompared == 80 &&
            audit.FirstMismatchOffset == -1 &&
            audit.TransitionObserved &&
            audit.OwnedIdtrLoaded &&
            audit.FirmwareIdtrRestored &&
            audit.InterruptFlagRestored &&
            audit.ContinuedExecution &&
            audit.Succeeded &&
            (audit.PreviousLimit != audit.LoadedLimit ||
                audit.PreviousBase != audit.LoadedBase);
    }

    private static bool M02IdtrContinuedExecutionSafetyRestored(
        OS.Kernel.Diagnostics.IDTRContinuedExecutionContext* context,
        ulong ownedIdtAddress)
    {
        byte* bytes = (byte*)context;
        ushort loadedLimit = ReadM02IdtrContinuedExecutionU16(
            bytes + OS.Kernel.Diagnostics.IDTRContinuedExecution
                .LoadedIdtrOffset);
        ulong loadedBase = ReadM02IdtrContinuedExecutionU64(
            bytes + OS.Kernel.Diagnostics.IDTRContinuedExecution
                .LoadedIdtrOffset + 2);
        if (loadedLimit !=
                OS.Kernel.Diagnostics.IDTRContinuedExecution.IdtrLimit ||
            loadedBase != ownedIdtAddress)
        {
            return false;
        }

        for (int offset = 0;
            offset < OS.Kernel.Diagnostics.IDTRContinuedExecution
                .IdtrByteCount;
            offset++)
        {
            if (bytes[
                    OS.Kernel.Diagnostics.IDTRContinuedExecution
                        .PreviousIdtrOffset + offset] !=
                bytes[
                    OS.Kernel.Diagnostics.IDTRContinuedExecution
                        .RestoredIdtrOffset + offset])
            {
                return false;
            }
        }

        ushort previousLimit = ReadM02IdtrContinuedExecutionU16(
            bytes + OS.Kernel.Diagnostics.IDTRContinuedExecution
                .PreviousIdtrOffset);
        ulong previousBase = ReadM02IdtrContinuedExecutionU64(
            bytes + OS.Kernel.Diagnostics.IDTRContinuedExecution
                .PreviousIdtrOffset + 2);
        if (previousLimit ==
                OS.Kernel.Diagnostics.IDTRContinuedExecution.IdtrLimit &&
            previousBase == ownedIdtAddress)
        {
            return false;
        }

        ulong rflagsBefore = ReadM02IdtrContinuedExecutionU64(
            bytes + OS.Kernel.Diagnostics.IDTRContinuedExecution
                .RflagsBeforeOffset);
        ulong rflagsAfter = ReadM02IdtrContinuedExecutionU64(
            bytes + OS.Kernel.Diagnostics.IDTRContinuedExecution
                .RflagsAfterOffset);
        return ((rflagsBefore ^ rflagsAfter) &
            OS.Kernel.Diagnostics.IDTRContinuedExecution
                .RflagsInterruptEnableBit) == 0;
    }

    private static ushort ReadM02IdtrContinuedExecutionU16(byte* source)
    {
        return (ushort)(source[0] | (source[1] << 8));
    }

    private static ulong ReadM02IdtrContinuedExecutionU64(byte* source)
    {
        return (ulong)source[0] |
            ((ulong)source[1] << 8) |
            ((ulong)source[2] << 16) |
            ((ulong)source[3] << 24) |
            ((ulong)source[4] << 32) |
            ((ulong)source[5] << 40) |
            ((ulong)source[6] << 48) |
            ((ulong)source[7] << 56);
    }

    private static bool M02IdtrContinuedExecutionCommonTargetMatches(
        byte* commonTarget)
    {
        return commonTarget[0] == 0x48 &&
            commonTarget[1] == 0x83 &&
            commonTarget[2] == 0xC4 &&
            commonTarget[3] == 0x18 &&
            commonTarget[4] == 0x48 &&
            commonTarget[5] == 0xCF;
    }

    private static bool IsValidM02IdtrContinuedExecutionRange(
        ulong address,
        ulong length,
        ulong alignment)
    {
        if (address == 0 ||
            length == 0 ||
            alignment == 0 ||
            (address & (alignment - 1)) != 0 ||
            !OS.Kernel.Diagnostics.IDTRContinuedExecution
                .IsCanonicalX64Address(address) ||
            address > ~0UL - length)
        {
            return false;
        }

        ulong endInclusive = address + length - 1;
        return OS.Kernel.Diagnostics.IDTRContinuedExecution
                .IsCanonicalX64Address(endInclusive) &&
            ((address >> 47) & 1UL) ==
                ((endInclusive >> 47) & 1UL);
    }

    private static bool M02IdtrContinuedExecutionRangesOverlap(
        ulong firstAddress,
        ulong firstLength,
        ulong secondAddress,
        ulong secondLength)
    {
        return firstAddress <= secondAddress
            ? secondAddress - firstAddress < firstLength
            : firstAddress - secondAddress < secondLength;
    }

    private static bool FailM02IdtrContinuedExecutionBeforeInvocation(
        EfiSimpleTextOutputProtocol* console,
        EfiSerialIoProtocol* serial,
        EfiBootServices* bootServices,
        ulong codeAddress,
        bool codeAllocated,
        ulong dataAddress,
        bool dataAllocated)
    {
        if (bootServices != null && bootServices->FreePages != null)
        {
            if (dataAllocated)
            {
                _ = bootServices->FreePages(
                    dataAddress,
                    M02IdtrContinuedExecutionDataPages);
            }
            if (codeAllocated &&
                (!dataAllocated ||
                    !M02IdtrContinuedExecutionRangesOverlap(
                        codeAddress,
                        M02IdtrContinuedExecutionCodeBytes,
                        dataAddress,
                        M02IdtrContinuedExecutionDataBytes)))
            {
                _ = bootServices->FreePages(
                    codeAddress,
                    M02IdtrContinuedExecutionCodePages);
            }
        }

        WriteM02IdtrContinuedExecutionFailureLines(console, serial);
        return false;
    }

    private static void SpinM02IdtrContinuedExecutionAfterUnverifiedRestore()
    {
        while (true)
        {
        }
    }

    private static void WriteM02IdtrContinuedExecutionStartLine(
        EfiSimpleTextOutputProtocol* console,
        EfiSerialIoProtocol* serial)
    {
        WriteM02IdtrContinuedExecutionAscii(console, serial, (byte)'T', (byte)'E', (byte)'S', (byte)'T', (byte)'_', (byte)'S', (byte)'T', (byte)'A');
        WriteM02IdtrContinuedExecutionAscii(console, serial, (byte)'R', (byte)'T', (byte)' ', (byte)'m', (byte)'0', (byte)'2', (byte)'.', (byte)'0');
        WriteM02IdtrContinuedExecutionAscii(console, serial, (byte)'1', (byte)'2', 0, 0, 0, 0, 0, 0);
        WriteCrLf(console, serial);
    }

    private static void WriteM02IdtrContinuedExecutionSuccessLine(
        EfiSimpleTextOutputProtocol* console,
        EfiSerialIoProtocol* serial)
    {
        WriteM02IdtrContinuedExecutionAscii(console, serial, (byte)'C', (byte)'L', (byte)'R', (byte)'O', (byte)'S', (byte)'_', (byte)'M', (byte)'0');
        WriteM02IdtrContinuedExecutionAscii(console, serial, (byte)'2', (byte)'_', (byte)'0', (byte)'1', (byte)'2', (byte)'_', (byte)'R', (byte)'E');
        WriteM02IdtrContinuedExecutionAscii(console, serial, (byte)'A', (byte)'D', (byte)'Y', (byte)' ', (byte)'t', (byte)'a', (byte)'b', (byte)'l');
        WriteM02IdtrContinuedExecutionAscii(console, serial, (byte)'e', (byte)'B', (byte)'y', (byte)'t', (byte)'e', (byte)'s', (byte)'=', (byte)'4');
        WriteM02IdtrContinuedExecutionAscii(console, serial, (byte)'0', (byte)'9', (byte)'6', (byte)' ', (byte)'i', (byte)'d', (byte)'t', (byte)'r');
        WriteM02IdtrContinuedExecutionAscii(console, serial, (byte)'B', (byte)'y', (byte)'t', (byte)'e', (byte)'s', (byte)'=', (byte)'1', (byte)'0');
        WriteM02IdtrContinuedExecutionAscii(console, serial, (byte)' ', (byte)'i', (byte)'d', (byte)'t', (byte)'r', (byte)'L', (byte)'i', (byte)'m');
        WriteM02IdtrContinuedExecutionAscii(console, serial, (byte)'i', (byte)'t', (byte)'=', (byte)'4', (byte)'0', (byte)'9', (byte)'5', (byte)' ');
        WriteM02IdtrContinuedExecutionAscii(console, serial, (byte)'p', (byte)'o', (byte)'l', (byte)'i', (byte)'c', (byte)'y', (byte)'F', (byte)'n');
        WriteM02IdtrContinuedExecutionAscii(console, serial, (byte)'v', (byte)'=', (byte)'8', (byte)'5', (byte)'1', (byte)'4', (byte)'F', (byte)'4');
        WriteM02IdtrContinuedExecutionAscii(console, serial, (byte)'2', (byte)'E', (byte)'B', (byte)'D', (byte)'F', (byte)'C', (byte)'5', (byte)'D');
        WriteM02IdtrContinuedExecutionAscii(console, serial, (byte)'5', (byte)'5', (byte)' ', (byte)'f', (byte)'i', (byte)'r', (byte)'m', (byte)'w');
        WriteM02IdtrContinuedExecutionAscii(console, serial, (byte)'a', (byte)'r', (byte)'e', (byte)'G', (byte)'a', (byte)'t', (byte)'e', (byte)'s');
        WriteM02IdtrContinuedExecutionAscii(console, serial, (byte)'C', (byte)'o', (byte)'p', (byte)'i', (byte)'e', (byte)'d', (byte)'=', (byte)'0');
        WriteM02IdtrContinuedExecutionAscii(console, serial, (byte)' ', (byte)'f', (byte)'i', (byte)'r', (byte)'m', (byte)'w', (byte)'a', (byte)'r');
        WriteM02IdtrContinuedExecutionAscii(console, serial, (byte)'e', (byte)'I', (byte)'d', (byte)'t', (byte)'r', (byte)'C', (byte)'a', (byte)'p');
        WriteM02IdtrContinuedExecutionAscii(console, serial, (byte)'t', (byte)'u', (byte)'r', (byte)'e', (byte)'d', (byte)'=', (byte)'t', (byte)'r');
        WriteM02IdtrContinuedExecutionAscii(console, serial, (byte)'u', (byte)'e', (byte)' ', (byte)'o', (byte)'w', (byte)'n', (byte)'e', (byte)'d');
        WriteM02IdtrContinuedExecutionAscii(console, serial, (byte)'I', (byte)'d', (byte)'t', (byte)'r', (byte)'L', (byte)'o', (byte)'a', (byte)'d');
        WriteM02IdtrContinuedExecutionAscii(console, serial, (byte)'e', (byte)'d', (byte)'=', (byte)'t', (byte)'r', (byte)'u', (byte)'e', (byte)' ');
        WriteM02IdtrContinuedExecutionAscii(console, serial, (byte)'o', (byte)'w', (byte)'n', (byte)'e', (byte)'d', (byte)'I', (byte)'d', (byte)'t');
        WriteM02IdtrContinuedExecutionAscii(console, serial, (byte)'r', (byte)'R', (byte)'e', (byte)'a', (byte)'d', (byte)'B', (byte)'a', (byte)'c');
        WriteM02IdtrContinuedExecutionAscii(console, serial, (byte)'k', (byte)'=', (byte)'t', (byte)'r', (byte)'u', (byte)'e', (byte)' ', (byte)'f');
        WriteM02IdtrContinuedExecutionAscii(console, serial, (byte)'i', (byte)'r', (byte)'m', (byte)'w', (byte)'a', (byte)'r', (byte)'e', (byte)'I');
        WriteM02IdtrContinuedExecutionAscii(console, serial, (byte)'d', (byte)'t', (byte)'r', (byte)'R', (byte)'e', (byte)'s', (byte)'t', (byte)'o');
        WriteM02IdtrContinuedExecutionAscii(console, serial, (byte)'r', (byte)'e', (byte)'d', (byte)'=', (byte)'t', (byte)'r', (byte)'u', (byte)'e');
        WriteM02IdtrContinuedExecutionAscii(console, serial, (byte)' ', (byte)'r', (byte)'e', (byte)'s', (byte)'t', (byte)'o', (byte)'r', (byte)'e');
        WriteM02IdtrContinuedExecutionAscii(console, serial, (byte)'R', (byte)'e', (byte)'a', (byte)'d', (byte)'B', (byte)'a', (byte)'c', (byte)'k');
        WriteM02IdtrContinuedExecutionAscii(console, serial, (byte)'=', (byte)'t', (byte)'r', (byte)'u', (byte)'e', (byte)' ', (byte)'o', (byte)'w');
        WriteM02IdtrContinuedExecutionAscii(console, serial, (byte)'n', (byte)'e', (byte)'d', (byte)'I', (byte)'d', (byte)'t', (byte)'r', (byte)'A');
        WriteM02IdtrContinuedExecutionAscii(console, serial, (byte)'c', (byte)'t', (byte)'i', (byte)'v', (byte)'e', (byte)'=', (byte)'f', (byte)'a');
        WriteM02IdtrContinuedExecutionAscii(console, serial, (byte)'l', (byte)'s', (byte)'e', (byte)' ', (byte)'c', (byte)'o', (byte)'n', (byte)'t');
        WriteM02IdtrContinuedExecutionAscii(console, serial, (byte)'i', (byte)'n', (byte)'u', (byte)'e', (byte)'d', (byte)'E', (byte)'x', (byte)'e');
        WriteM02IdtrContinuedExecutionAscii(console, serial, (byte)'c', (byte)'u', (byte)'t', (byte)'i', (byte)'o', (byte)'n', (byte)'=', (byte)'t');
        WriteM02IdtrContinuedExecutionAscii(console, serial, (byte)'r', (byte)'u', (byte)'e', (byte)' ', (byte)'c', (byte)'l', (byte)'i', (byte)'W');
        WriteM02IdtrContinuedExecutionAscii(console, serial, (byte)'i', (byte)'n', (byte)'d', (byte)'o', (byte)'w', (byte)'=', (byte)'t', (byte)'r');
        WriteM02IdtrContinuedExecutionAscii(console, serial, (byte)'u', (byte)'e', (byte)' ', (byte)'i', (byte)'f', (byte)'R', (byte)'e', (byte)'s');
        WriteM02IdtrContinuedExecutionAscii(console, serial, (byte)'t', (byte)'o', (byte)'r', (byte)'e', (byte)'d', (byte)'=', (byte)'t', (byte)'r');
        WriteM02IdtrContinuedExecutionAscii(console, serial, (byte)'u', (byte)'e', (byte)' ', (byte)'i', (byte)'n', (byte)'s', (byte)'t', (byte)'r');
        WriteM02IdtrContinuedExecutionAscii(console, serial, (byte)'u', (byte)'c', (byte)'t', (byte)'i', (byte)'o', (byte)'n', (byte)'P', (byte)'u');
        WriteM02IdtrContinuedExecutionAscii(console, serial, (byte)'b', (byte)'l', (byte)'i', (byte)'s', (byte)'h', (byte)'e', (byte)'d', (byte)'=');
        WriteM02IdtrContinuedExecutionAscii(console, serial, (byte)'t', (byte)'r', (byte)'u', (byte)'e', (byte)' ', (byte)'a', (byte)'l', (byte)'l');
        WriteM02IdtrContinuedExecutionAscii(console, serial, (byte)'o', (byte)'c', (byte)'a', (byte)'t', (byte)'i', (byte)'o', (byte)'n', (byte)'s');
        WriteM02IdtrContinuedExecutionAscii(console, serial, (byte)'R', (byte)'e', (byte)'l', (byte)'e', (byte)'a', (byte)'s', (byte)'e', (byte)'d');
        WriteM02IdtrContinuedExecutionAscii(console, serial, (byte)'=', (byte)'t', (byte)'r', (byte)'u', (byte)'e', (byte)' ', (byte)'w', (byte)'x');
        WriteM02IdtrContinuedExecutionAscii(console, serial, (byte)'E', (byte)'n', (byte)'f', (byte)'o', (byte)'r', (byte)'c', (byte)'e', (byte)'d');
        WriteM02IdtrContinuedExecutionAscii(console, serial, (byte)'=', (byte)'f', (byte)'a', (byte)'l', (byte)'s', (byte)'e', (byte)' ', (byte)'i');
        WriteM02IdtrContinuedExecutionAscii(console, serial, (byte)'n', (byte)'t', (byte)'e', (byte)'r', (byte)'r', (byte)'u', (byte)'p', (byte)'t');
        WriteM02IdtrContinuedExecutionAscii(console, serial, (byte)'T', (byte)'r', (byte)'i', (byte)'g', (byte)'g', (byte)'e', (byte)'r', (byte)'e');
        WriteM02IdtrContinuedExecutionAscii(console, serial, (byte)'d', (byte)'=', (byte)'f', (byte)'a', (byte)'l', (byte)'s', (byte)'e', (byte)' ');
        WriteM02IdtrContinuedExecutionAscii(console, serial, (byte)'f', (byte)'a', (byte)'u', (byte)'l', (byte)'t', (byte)'T', (byte)'r', (byte)'i');
        WriteM02IdtrContinuedExecutionAscii(console, serial, (byte)'g', (byte)'g', (byte)'e', (byte)'r', (byte)'e', (byte)'d', (byte)'=', (byte)'f');
        WriteM02IdtrContinuedExecutionAscii(console, serial, (byte)'a', (byte)'l', (byte)'s', (byte)'e', (byte)' ', (byte)'m', (byte)'a', (byte)'r');
        WriteM02IdtrContinuedExecutionAscii(console, serial, (byte)'k', (byte)'e', (byte)'r', (byte)'T', (byte)'r', (byte)'a', (byte)'n', (byte)'s');
        WriteM02IdtrContinuedExecutionAscii(console, serial, (byte)'p', (byte)'o', (byte)'r', (byte)'t', (byte)'=', (byte)'u', (byte)'e', (byte)'f');
        WriteM02IdtrContinuedExecutionAscii(console, serial, (byte)'i', (byte)'S', (byte)'e', (byte)'r', (byte)'i', (byte)'a', (byte)'l', (byte)' ');
        WriteM02IdtrContinuedExecutionAscii(console, serial, (byte)'p', (byte)'o', (byte)'s', (byte)'t', (byte)'E', (byte)'x', (byte)'i', (byte)'t');
        WriteM02IdtrContinuedExecutionAscii(console, serial, (byte)'B', (byte)'o', (byte)'o', (byte)'t', (byte)'S', (byte)'e', (byte)'r', (byte)'v');
        WriteM02IdtrContinuedExecutionAscii(console, serial, (byte)'i', (byte)'c', (byte)'e', (byte)'s', (byte)'=', (byte)'f', (byte)'a', (byte)'l');
        WriteM02IdtrContinuedExecutionAscii(console, serial, (byte)'s', (byte)'e', 0, 0, 0, 0, 0, 0);
        WriteCrLf(console, serial);
    }

    private static void WriteM02IdtrContinuedExecutionPassLine(
        EfiSimpleTextOutputProtocol* console,
        EfiSerialIoProtocol* serial)
    {
        WriteM02IdtrContinuedExecutionAscii(console, serial, (byte)'T', (byte)'E', (byte)'S', (byte)'T', (byte)'_', (byte)'P', (byte)'A', (byte)'S');
        WriteM02IdtrContinuedExecutionAscii(console, serial, (byte)'S', (byte)' ', (byte)'m', (byte)'0', (byte)'2', (byte)'.', (byte)'0', (byte)'1');
        WriteM02IdtrContinuedExecutionAscii(console, serial, (byte)'2', 0, 0, 0, 0, 0, 0, 0);
        WriteCrLf(console, serial);
    }

    private static void WriteM02IdtrContinuedExecutionFailureLines(
        EfiSimpleTextOutputProtocol* console,
        EfiSerialIoProtocol* serial)
    {
        WriteM02IdtrContinuedExecutionAscii(console, serial, (byte)'M', (byte)'0', (byte)'2', (byte)'_', (byte)'I', (byte)'D', (byte)'T', (byte)'R');
        WriteM02IdtrContinuedExecutionAscii(console, serial, (byte)'_', (byte)'C', (byte)'O', (byte)'N', (byte)'T', (byte)'I', (byte)'N', (byte)'U');
        WriteM02IdtrContinuedExecutionAscii(console, serial, (byte)'E', (byte)'D', (byte)'_', (byte)'E', (byte)'X', (byte)'E', (byte)'C', (byte)'U');
        WriteM02IdtrContinuedExecutionAscii(console, serial, (byte)'T', (byte)'I', (byte)'O', (byte)'N', (byte)'_', (byte)'F', (byte)'A', (byte)'I');
        WriteM02IdtrContinuedExecutionAscii(console, serial, (byte)'L', (byte)'E', (byte)'D', 0, 0, 0, 0, 0);
        WriteCrLf(console, serial);
        WriteM02IdtrContinuedExecutionAscii(console, serial, (byte)'T', (byte)'E', (byte)'S', (byte)'T', (byte)'_', (byte)'F', (byte)'A', (byte)'I');
        WriteM02IdtrContinuedExecutionAscii(console, serial, (byte)'L', (byte)' ', (byte)'m', (byte)'0', (byte)'2', (byte)'.', (byte)'0', (byte)'1');
        WriteM02IdtrContinuedExecutionAscii(console, serial, (byte)'2', (byte)' ', (byte)'i', (byte)'d', (byte)'t', (byte)'r', (byte)'_', (byte)'l');
        WriteM02IdtrContinuedExecutionAscii(console, serial, (byte)'o', (byte)'a', (byte)'d', (byte)'_', (byte)'r', (byte)'e', (byte)'a', (byte)'d');
        WriteM02IdtrContinuedExecutionAscii(console, serial, (byte)'b', (byte)'a', (byte)'c', (byte)'k', (byte)'_', (byte)'r', (byte)'e', (byte)'s');
        WriteM02IdtrContinuedExecutionAscii(console, serial, (byte)'t', (byte)'o', (byte)'r', (byte)'e', (byte)'_', (byte)'r', (byte)'e', (byte)'t');
        WriteM02IdtrContinuedExecutionAscii(console, serial, (byte)'u', (byte)'r', (byte)'n', (byte)'_', (byte)'o', (byte)'r', (byte)'_', (byte)'c');
        WriteM02IdtrContinuedExecutionAscii(console, serial, (byte)'l', (byte)'e', (byte)'a', (byte)'n', (byte)'u', (byte)'p', 0, 0);
        WriteCrLf(console, serial);
    }

    private static void WriteM02IdtrContinuedExecutionAscii(
        EfiSimpleTextOutputProtocol* console,
        EfiSerialIoProtocol* serial,
        byte b0,
        byte b1,
        byte b2,
        byte b3,
        byte b4,
        byte b5,
        byte b6,
        byte b7)
    {
        if (b0 != 0)
            Write(console, serial, b0);
        if (b1 != 0)
            Write(console, serial, b1);
        if (b2 != 0)
            Write(console, serial, b2);
        if (b3 != 0)
            Write(console, serial, b3);
        if (b4 != 0)
            Write(console, serial, b4);
        if (b5 != 0)
            Write(console, serial, b5);
        if (b6 != 0)
            Write(console, serial, b6);
        if (b7 != 0)
            Write(console, serial, b7);
    }

#endif
    private static void WriteUefiMemoryMapReady(EfiSimpleTextOutputProtocol* console, EfiSerialIoProtocol* serial)
    {
        Write(console, serial, (byte)'U');
        Write(console, serial, (byte)'E');
        Write(console, serial, (byte)'F');
        Write(console, serial, (byte)'I');
        Write(console, serial, (byte)'_');
        Write(console, serial, (byte)'M');
        Write(console, serial, (byte)'E');
        Write(console, serial, (byte)'M');
        Write(console, serial, (byte)'O');
        Write(console, serial, (byte)'R');
        Write(console, serial, (byte)'Y');
        Write(console, serial, (byte)'_');
        Write(console, serial, (byte)'M');
        Write(console, serial, (byte)'A');
        Write(console, serial, (byte)'P');
        Write(console, serial, (byte)'_');
        Write(console, serial, (byte)'R');
        Write(console, serial, (byte)'E');
        Write(console, serial, (byte)'A');
        Write(console, serial, (byte)'D');
        Write(console, serial, (byte)'Y');
        WriteCrLf(console, serial);
    }

    private static void WriteUefiMemoryMapFail(EfiSimpleTextOutputProtocol* console, EfiSerialIoProtocol* serial)
    {
        Write(console, serial, (byte)'U');
        Write(console, serial, (byte)'E');
        Write(console, serial, (byte)'F');
        Write(console, serial, (byte)'I');
        Write(console, serial, (byte)'_');
        Write(console, serial, (byte)'M');
        Write(console, serial, (byte)'E');
        Write(console, serial, (byte)'M');
        Write(console, serial, (byte)'O');
        Write(console, serial, (byte)'R');
        Write(console, serial, (byte)'Y');
        Write(console, serial, (byte)'_');
        Write(console, serial, (byte)'M');
        Write(console, serial, (byte)'A');
        Write(console, serial, (byte)'P');
        Write(console, serial, (byte)'_');
        Write(console, serial, (byte)'F');
        Write(console, serial, (byte)'A');
        Write(console, serial, (byte)'I');
        Write(console, serial, (byte)'L');
        WriteCrLf(console, serial);
    }

    private static void WriteUefiMemoryMapDescriptorCount(EfiSimpleTextOutputProtocol* console, EfiSerialIoProtocol* serial, ulong descriptorCount)
    {
        Write(console, serial, (byte)'U');
        Write(console, serial, (byte)'E');
        Write(console, serial, (byte)'F');
        Write(console, serial, (byte)'I');
        Write(console, serial, (byte)'_');
        Write(console, serial, (byte)'M');
        Write(console, serial, (byte)'E');
        Write(console, serial, (byte)'M');
        Write(console, serial, (byte)'O');
        Write(console, serial, (byte)'R');
        Write(console, serial, (byte)'Y');
        Write(console, serial, (byte)'_');
        Write(console, serial, (byte)'M');
        Write(console, serial, (byte)'A');
        Write(console, serial, (byte)'P');
        Write(console, serial, (byte)'_');
        Write(console, serial, (byte)'D');
        Write(console, serial, (byte)'E');
        Write(console, serial, (byte)'S');
        Write(console, serial, (byte)'C');
        Write(console, serial, (byte)'R');
        Write(console, serial, (byte)'I');
        Write(console, serial, (byte)'P');
        Write(console, serial, (byte)'T');
        Write(console, serial, (byte)'O');
        Write(console, serial, (byte)'R');
        Write(console, serial, (byte)'_');
        Write(console, serial, (byte)'C');
        Write(console, serial, (byte)'O');
        Write(console, serial, (byte)'U');
        Write(console, serial, (byte)'N');
        Write(console, serial, (byte)'T');
        Write(console, serial, (byte)' ');
        WriteUnsignedDecimal(console, serial, descriptorCount);
        WriteCrLf(console, serial);
    }

    private static void WriteUefiMemoryMapOwned(EfiSimpleTextOutputProtocol* console, EfiSerialIoProtocol* serial)
    {
        Write(console, serial, (byte)'U');
        Write(console, serial, (byte)'E');
        Write(console, serial, (byte)'F');
        Write(console, serial, (byte)'I');
        Write(console, serial, (byte)'_');
        Write(console, serial, (byte)'M');
        Write(console, serial, (byte)'E');
        Write(console, serial, (byte)'M');
        Write(console, serial, (byte)'O');
        Write(console, serial, (byte)'R');
        Write(console, serial, (byte)'Y');
        Write(console, serial, (byte)'_');
        Write(console, serial, (byte)'M');
        Write(console, serial, (byte)'A');
        Write(console, serial, (byte)'P');
        Write(console, serial, (byte)'_');
        Write(console, serial, (byte)'O');
        Write(console, serial, (byte)'W');
        Write(console, serial, (byte)'N');
        Write(console, serial, (byte)'E');
        Write(console, serial, (byte)'D');
        WriteCrLf(console, serial);
    }

    private static void WriteUefiMemoryMapNormalized(EfiSimpleTextOutputProtocol* console, EfiSerialIoProtocol* serial)
    {
        Write(console, serial, (byte)'U');
        Write(console, serial, (byte)'E');
        Write(console, serial, (byte)'F');
        Write(console, serial, (byte)'I');
        Write(console, serial, (byte)'_');
        Write(console, serial, (byte)'M');
        Write(console, serial, (byte)'E');
        Write(console, serial, (byte)'M');
        Write(console, serial, (byte)'O');
        Write(console, serial, (byte)'R');
        Write(console, serial, (byte)'Y');
        Write(console, serial, (byte)'_');
        Write(console, serial, (byte)'M');
        Write(console, serial, (byte)'A');
        Write(console, serial, (byte)'P');
        Write(console, serial, (byte)'_');
        Write(console, serial, (byte)'N');
        Write(console, serial, (byte)'O');
        Write(console, serial, (byte)'R');
        Write(console, serial, (byte)'M');
        Write(console, serial, (byte)'A');
        Write(console, serial, (byte)'L');
        Write(console, serial, (byte)'I');
        Write(console, serial, (byte)'Z');
        Write(console, serial, (byte)'E');
        Write(console, serial, (byte)'D');
        WriteCrLf(console, serial);
    }

    private static void WriteBootMemoryMapAccepted(EfiSimpleTextOutputProtocol* console, EfiSerialIoProtocol* serial)
    {
        Write(console, serial, (byte)'B');
        Write(console, serial, (byte)'O');
        Write(console, serial, (byte)'O');
        Write(console, serial, (byte)'T');
        Write(console, serial, (byte)'_');
        Write(console, serial, (byte)'M');
        Write(console, serial, (byte)'E');
        Write(console, serial, (byte)'M');
        Write(console, serial, (byte)'O');
        Write(console, serial, (byte)'R');
        Write(console, serial, (byte)'Y');
        Write(console, serial, (byte)'_');
        Write(console, serial, (byte)'M');
        Write(console, serial, (byte)'A');
        Write(console, serial, (byte)'P');
        Write(console, serial, (byte)'_');
        Write(console, serial, (byte)'A');
        Write(console, serial, (byte)'C');
        Write(console, serial, (byte)'C');
        Write(console, serial, (byte)'E');
        Write(console, serial, (byte)'P');
        Write(console, serial, (byte)'T');
        Write(console, serial, (byte)'E');
        Write(console, serial, (byte)'D');
        WriteCrLf(console, serial);
    }

    private static void WriteBootMemoryMapRejected(EfiSimpleTextOutputProtocol* console, EfiSerialIoProtocol* serial, BootMemoryMapAcceptanceStatus status)
    {
        Write(console, serial, (byte)'B');
        Write(console, serial, (byte)'O');
        Write(console, serial, (byte)'O');
        Write(console, serial, (byte)'T');
        Write(console, serial, (byte)'_');
        Write(console, serial, (byte)'M');
        Write(console, serial, (byte)'E');
        Write(console, serial, (byte)'M');
        Write(console, serial, (byte)'O');
        Write(console, serial, (byte)'R');
        Write(console, serial, (byte)'Y');
        Write(console, serial, (byte)'_');
        Write(console, serial, (byte)'M');
        Write(console, serial, (byte)'A');
        Write(console, serial, (byte)'P');
        Write(console, serial, (byte)'_');
        Write(console, serial, (byte)'R');
        Write(console, serial, (byte)'E');
        Write(console, serial, (byte)'J');
        Write(console, serial, (byte)'E');
        Write(console, serial, (byte)'C');
        Write(console, serial, (byte)'T');
        Write(console, serial, (byte)'E');
        Write(console, serial, (byte)'D');
        Write(console, serial, (byte)' ');
        WriteUnsignedDecimal(console, serial, (ulong)status);
        WriteCrLf(console, serial);
    }

    private static bool MaterializeAndWriteBootMemoryRegions(EfiSimpleTextOutputProtocol* console, EfiSerialIoProtocol* serial)
    {
        fixed (ulong* regionSlots = &s_ownedMemoryRegions.Slots[0])
        {
            s_memoryRegionMaterialization = BootMemoryRegionMaterializer.Materialize(s_memoryMapSnapshot.Buffer, s_memoryMapSnapshot.MapSize, s_memoryMapSnapshot.DescriptorSize, s_memoryMapSnapshot.DescriptorVersion, s_memoryMapSnapshot.DescriptorCount, (BootMemoryRegionRecord*)regionSlots, OwnedMemoryRegionCapacity);
        }

        if (!s_memoryRegionMaterialization.Succeeded)
        {
            WriteBootMemoryRegionsFail(console, serial, s_memoryRegionMaterialization.Status);
            return false;
        }

        WriteBootMemoryRegionsReady(console, serial);
        WriteBootMemoryRegionCount(console, serial, s_memoryRegionMaterialization.RegionCount);
        WriteBootPageAllocatorReadiness(console, serial);
        return true;
    }

    private static void WriteBootMemoryRegionsReady(EfiSimpleTextOutputProtocol* console, EfiSerialIoProtocol* serial)
    {
        Write(console, serial, (byte)'B');
        Write(console, serial, (byte)'O');
        Write(console, serial, (byte)'O');
        Write(console, serial, (byte)'T');
        Write(console, serial, (byte)'_');
        Write(console, serial, (byte)'M');
        Write(console, serial, (byte)'E');
        Write(console, serial, (byte)'M');
        Write(console, serial, (byte)'O');
        Write(console, serial, (byte)'R');
        Write(console, serial, (byte)'Y');
        Write(console, serial, (byte)'_');
        Write(console, serial, (byte)'R');
        Write(console, serial, (byte)'E');
        Write(console, serial, (byte)'G');
        Write(console, serial, (byte)'I');
        Write(console, serial, (byte)'O');
        Write(console, serial, (byte)'N');
        Write(console, serial, (byte)'S');
        Write(console, serial, (byte)'_');
        Write(console, serial, (byte)'R');
        Write(console, serial, (byte)'E');
        Write(console, serial, (byte)'A');
        Write(console, serial, (byte)'D');
        Write(console, serial, (byte)'Y');
        WriteCrLf(console, serial);
    }

    private static void WriteBootMemoryRegionCount(EfiSimpleTextOutputProtocol* console, EfiSerialIoProtocol* serial, ulong regionCount)
    {
        Write(console, serial, (byte)'B');
        Write(console, serial, (byte)'O');
        Write(console, serial, (byte)'O');
        Write(console, serial, (byte)'T');
        Write(console, serial, (byte)'_');
        Write(console, serial, (byte)'M');
        Write(console, serial, (byte)'E');
        Write(console, serial, (byte)'M');
        Write(console, serial, (byte)'O');
        Write(console, serial, (byte)'R');
        Write(console, serial, (byte)'Y');
        Write(console, serial, (byte)'_');
        Write(console, serial, (byte)'R');
        Write(console, serial, (byte)'E');
        Write(console, serial, (byte)'G');
        Write(console, serial, (byte)'I');
        Write(console, serial, (byte)'O');
        Write(console, serial, (byte)'N');
        Write(console, serial, (byte)'_');
        Write(console, serial, (byte)'C');
        Write(console, serial, (byte)'O');
        Write(console, serial, (byte)'U');
        Write(console, serial, (byte)'N');
        Write(console, serial, (byte)'T');
        Write(console, serial, (byte)' ');
        WriteUnsignedDecimal(console, serial, regionCount);
        WriteCrLf(console, serial);
    }

    private static void WriteBootMemoryRegionsFail(EfiSimpleTextOutputProtocol* console, EfiSerialIoProtocol* serial, BootMemoryRegionMaterializationStatus status)
    {
        Write(console, serial, (byte)'B');
        Write(console, serial, (byte)'O');
        Write(console, serial, (byte)'O');
        Write(console, serial, (byte)'T');
        Write(console, serial, (byte)'_');
        Write(console, serial, (byte)'M');
        Write(console, serial, (byte)'E');
        Write(console, serial, (byte)'M');
        Write(console, serial, (byte)'O');
        Write(console, serial, (byte)'R');
        Write(console, serial, (byte)'Y');
        Write(console, serial, (byte)'_');
        Write(console, serial, (byte)'R');
        Write(console, serial, (byte)'E');
        Write(console, serial, (byte)'G');
        Write(console, serial, (byte)'I');
        Write(console, serial, (byte)'O');
        Write(console, serial, (byte)'N');
        Write(console, serial, (byte)'S');
        Write(console, serial, (byte)'_');
        Write(console, serial, (byte)'F');
        Write(console, serial, (byte)'A');
        Write(console, serial, (byte)'I');
        Write(console, serial, (byte)'L');
        Write(console, serial, (byte)' ');
        WriteUnsignedDecimal(console, serial, (ulong)status);
        WriteCrLf(console, serial);
    }

    private static void WriteBootPageAllocatorReadiness(EfiSimpleTextOutputProtocol* console, EfiSerialIoProtocol* serial)
    {
        s_bootPageAllocatorReadiness = PlanBootPageAllocatorReadiness();
        if (s_bootPageAllocatorReadiness.IsReady)
        {
            WriteBootPageAllocatorReadyPrefix(console, serial);
            WriteReadyStatus(console, serial);
        }
        else
        {
            WriteBootPageAllocatorNotReadyPrefix(console, serial);
            if (s_bootPageAllocatorReadiness.Status == EfiBootPageAllocatorReadinessStatus.NoConventionalMemory)
            {
                WriteNoConventionalMemoryStatus(console, serial);
            }
            else
            {
                WriteLiveAllocatorNotInstalledStatus(console, serial);
            }
        }

        WriteBootPageAllocatorReadinessMetadata(console, serial, s_bootPageAllocatorReadiness);
    }

    private static EfiBootPageAllocatorReadinessResult PlanBootPageAllocatorReadiness()
    {
        EfiBootPageAllocatorReadinessResult result = default;
        result.Status = EfiBootPageAllocatorReadinessStatus.MaterializationMissing;
        result.RegionCount = s_memoryRegionMaterialization.RegionCount;
        result.ConventionalPages = s_memoryRegionMaterialization.ConventionalMemoryPages;
        result.ConventionalBytes = s_memoryRegionMaterialization.ConventionalMemoryBytes;
        result.PageSize = BootMemoryRegionRecord.PageSize;
        if (!s_memoryRegionMaterialization.Succeeded)
        {
            return result;
        }

        if (s_memoryRegionMaterialization.RegionCount == 0 || s_memoryRegionMaterialization.RegionCount > OwnedMemoryRegionCapacity)
        {
            result.Status = EfiBootPageAllocatorReadinessStatus.InvalidRegionCount;
            return result;
        }

        fixed (ulong* regionSlots = &s_ownedMemoryRegions.Slots[0])
        fixed (ulong* nodeSlots = &s_bootPageAllocatorNodes.Slots[0])
        {
            BootMemoryRegionRecord* regions = (BootMemoryRegionRecord*)regionSlots;
            ulong previousStart = 0;
            ulong previousEndExclusive = 0;
            int nodeCount = 0;
            ulong freePages = 0;
            ulong freeBytes = 0;
            for (ulong regionIndex = 0; regionIndex < s_memoryRegionMaterialization.RegionCount; regionIndex++)
            {
                BootMemoryRegionRecord region = regions[(int)regionIndex];
                EfiBootPageAllocatorReadinessStatus regionStatus = ValidateBootAllocatorRegion(region, regionIndex, previousStart, previousEndExclusive, out ulong byteLength, out ulong physicalEndExclusive);
                if (regionStatus != EfiBootPageAllocatorReadinessStatus.Ready)
                {
                    result.Status = regionStatus;
                    result.FreeNodeCount = nodeCount;
                    result.FreePages = freePages;
                    result.FreeBytes = freeBytes;
                    return result;
                }

                if (region.UefiType == UefiConventionalMemoryType)
                {
                    if (nodeCount >= BootPageAllocatorNodeCapacity)
                    {
                        result.Status = EfiBootPageAllocatorReadinessStatus.NodeCapacityTooSmall;
                        result.FreeNodeCount = nodeCount;
                        result.FreePages = freePages;
                        result.FreeBytes = freeBytes;
                        return result;
                    }

                    if (nodeCount != 0)
                    {
                        SetBootPageAllocatorNodeNext(nodeSlots, nodeCount - 1, (ulong)nodeCount);
                    }

                    WriteBootPageAllocatorNodeUnchecked(nodeSlots, nodeCount, regionIndex, region.PhysicalStart, region.PhysicalEndExclusive, region.PageCount, byteLength, BootPageAllocatorInvalidNodeIndex);
                    nodeCount++;
                    if (!TryAdd(freePages, region.PageCount, out ulong nextFreePages) || !TryAdd(freeBytes, byteLength, out ulong nextFreeBytes))
                    {
                        result.Status = EfiBootPageAllocatorReadinessStatus.TotalMismatch;
                        result.FreeNodeCount = nodeCount;
                        result.FreePages = freePages;
                        result.FreeBytes = freeBytes;
                        return result;
                    }

                    freePages = nextFreePages;
                    freeBytes = nextFreeBytes;
                }

                previousStart = region.PhysicalStart;
                previousEndExclusive = physicalEndExclusive;
            }

            result.FreeNodeCount = nodeCount;
            result.FreePages = freePages;
            result.FreeBytes = freeBytes;
            if (nodeCount == 0)
            {
                result.Status = EfiBootPageAllocatorReadinessStatus.NoConventionalMemory;
                return result;
            }

            if (freePages == 0)
            {
                result.Status = EfiBootPageAllocatorReadinessStatus.NoFreePages;
                return result;
            }

            result.Status = ValidateBootPageAllocatorNodeChain(nodeSlots, nodeCount, freePages, freeBytes);
            result.IsReady = result.Status == EfiBootPageAllocatorReadinessStatus.Ready;
            return result;
        }
    }

    private static EfiBootPageAllocatorReadinessStatus ValidateBootAllocatorRegion(BootMemoryRegionRecord region, ulong regionIndex, ulong previousStart, ulong previousEndExclusive, out ulong byteLength, out ulong physicalEndExclusive)
    {
        byteLength = 0;
        physicalEndExclusive = 0;
        if (region.PageCount == 0 || region.PageCount > 0xffffffffffffffff / BootMemoryRegionRecord.PageSize)
        {
            return EfiBootPageAllocatorReadinessStatus.InvalidRegionShape;
        }

        byteLength = region.PageCount * BootMemoryRegionRecord.PageSize;
        if (region.ByteLength != byteLength)
        {
            return EfiBootPageAllocatorReadinessStatus.InvalidRegionShape;
        }

        if (!TryAdd(region.PhysicalStart, byteLength, out physicalEndExclusive) || region.PhysicalEndExclusive != physicalEndExclusive)
        {
            return EfiBootPageAllocatorReadinessStatus.InvalidRegionShape;
        }

        if (regionIndex != 0)
        {
            if (region.PhysicalStart < previousStart)
            {
                return EfiBootPageAllocatorReadinessStatus.UnsortedRegion;
            }

            if (region.PhysicalStart < previousEndExclusive)
            {
                return EfiBootPageAllocatorReadinessStatus.OverlappingRegion;
            }
        }

        if (region.UefiType == UefiConventionalMemoryType && (region.PhysicalStart & (BootMemoryRegionRecord.PageSize - 1)) != 0)
        {
            return EfiBootPageAllocatorReadinessStatus.InvalidRegionShape;
        }

        return EfiBootPageAllocatorReadinessStatus.Ready;
    }

    private static EfiBootPageAllocatorReadinessStatus ValidateBootPageAllocatorNodeChain(ulong* nodeSlots, int nodeCount, ulong expectedFreePages, ulong expectedFreeBytes)
    {
        int currentNodeIndex = 0;
        int visitedNodeCount = 0;
        ulong totalPages = 0;
        ulong totalBytes = 0;
        while (currentNodeIndex != -1)
        {
            if (currentNodeIndex < 0 || currentNodeIndex >= nodeCount || visitedNodeCount >= nodeCount)
            {
                return EfiBootPageAllocatorReadinessStatus.NodeValidationFailed;
            }

            ulong physicalStart = GetBootPageAllocatorNodeSlot(nodeSlots, currentNodeIndex, BootPageAllocatorNodePhysicalStartSlot);
            ulong physicalEndExclusive = GetBootPageAllocatorNodeSlot(nodeSlots, currentNodeIndex, BootPageAllocatorNodePhysicalEndExclusiveSlot);
            ulong pageCount = GetBootPageAllocatorNodeSlot(nodeSlots, currentNodeIndex, BootPageAllocatorNodePageCountSlot);
            ulong byteLength = GetBootPageAllocatorNodeSlot(nodeSlots, currentNodeIndex, BootPageAllocatorNodeByteLengthSlot);
            ulong nextNodeIndex = GetBootPageAllocatorNodeSlot(nodeSlots, currentNodeIndex, BootPageAllocatorNodeNextIndexSlot);
            if (pageCount == 0 || (physicalStart & (BootMemoryRegionRecord.PageSize - 1)) != 0 || pageCount > 0xffffffffffffffff / BootMemoryRegionRecord.PageSize)
            {
                return EfiBootPageAllocatorReadinessStatus.NodeValidationFailed;
            }

            ulong calculatedByteLength = pageCount * BootMemoryRegionRecord.PageSize;
            if (byteLength != calculatedByteLength)
            {
                return EfiBootPageAllocatorReadinessStatus.NodeValidationFailed;
            }

            if (!TryAdd(physicalStart, byteLength, out ulong calculatedEnd) || physicalEndExclusive != calculatedEnd)
            {
                return EfiBootPageAllocatorReadinessStatus.NodeValidationFailed;
            }

            if (!TryAdd(totalPages, pageCount, out ulong nextTotalPages) || !TryAdd(totalBytes, byteLength, out ulong nextTotalBytes))
            {
                return EfiBootPageAllocatorReadinessStatus.TotalMismatch;
            }

            totalPages = nextTotalPages;
            totalBytes = nextTotalBytes;
            visitedNodeCount++;
            if (nextNodeIndex == BootPageAllocatorInvalidNodeIndex)
            {
                currentNodeIndex = -1;
            }
            else if (nextNodeIndex >= (ulong)nodeCount || nextNodeIndex == (ulong)currentNodeIndex)
            {
                return EfiBootPageAllocatorReadinessStatus.NodeValidationFailed;
            }
            else
            {
                currentNodeIndex = (int)nextNodeIndex;
            }
        }

        if (visitedNodeCount != nodeCount)
        {
            return EfiBootPageAllocatorReadinessStatus.NodeCountMismatch;
        }

        return totalPages == expectedFreePages && totalBytes == expectedFreeBytes ? EfiBootPageAllocatorReadinessStatus.Ready : EfiBootPageAllocatorReadinessStatus.TotalMismatch;
    }

    private static void WriteBootPageAllocatorNodeUnchecked(ulong* nodeSlots, int nodeIndex, ulong sourceRegionIndex, ulong physicalStart, ulong physicalEndExclusive, ulong pageCount, ulong byteLength, ulong nextNodeIndex)
    {
        SetBootPageAllocatorNodeSlot(nodeSlots, nodeIndex, BootPageAllocatorNodeSourceRegionIndexSlot, sourceRegionIndex);
        SetBootPageAllocatorNodeSlot(nodeSlots, nodeIndex, BootPageAllocatorNodePhysicalStartSlot, physicalStart);
        SetBootPageAllocatorNodeSlot(nodeSlots, nodeIndex, BootPageAllocatorNodePhysicalEndExclusiveSlot, physicalEndExclusive);
        SetBootPageAllocatorNodeSlot(nodeSlots, nodeIndex, BootPageAllocatorNodePageCountSlot, pageCount);
        SetBootPageAllocatorNodeSlot(nodeSlots, nodeIndex, BootPageAllocatorNodeByteLengthSlot, byteLength);
        SetBootPageAllocatorNodeSlot(nodeSlots, nodeIndex, BootPageAllocatorNodeNextIndexSlot, nextNodeIndex);
    }

    private static void SetBootPageAllocatorNodeNext(ulong* nodeSlots, int nodeIndex, ulong nextNodeIndex)
    {
        SetBootPageAllocatorNodeSlot(nodeSlots, nodeIndex, BootPageAllocatorNodeNextIndexSlot, nextNodeIndex);
    }

    private static void SetBootPageAllocatorNodeSlot(ulong* nodeSlots, int nodeIndex, int slotIndex, ulong value)
    {
        nodeSlots[(nodeIndex * BootPageAllocatorNodeSlotCount) + slotIndex] = value;
    }

    private static ulong GetBootPageAllocatorNodeSlot(ulong* nodeSlots, int nodeIndex, int slotIndex)
    {
        return nodeSlots[(nodeIndex * BootPageAllocatorNodeSlotCount) + slotIndex];
    }

    private static bool TryAdd(ulong left, ulong right, out ulong value)
    {
        if (left > 0xffffffffffffffff - right)
        {
            value = 0;
            return false;
        }

        value = left + right;
        return true;
    }

    private static void WriteBootPageAllocatorReadinessMetadata(EfiSimpleTextOutputProtocol* console, EfiSerialIoProtocol* serial, EfiBootPageAllocatorReadinessResult readiness)
    {
        Write(console, serial, (byte)' ');
        WriteRegionsField(console, serial);
        WriteUnsignedDecimal(console, serial, readiness.RegionCount);
        Write(console, serial, (byte)' ');
        WriteConventionalPagesField(console, serial);
        WriteUnsignedDecimal(console, serial, readiness.ConventionalPages);
        Write(console, serial, (byte)' ');
        WriteConventionalBytesField(console, serial);
        WriteUnsignedDecimal(console, serial, readiness.ConventionalBytes);
        Write(console, serial, (byte)' ');
        WriteFreeNodesField(console, serial);
        WriteUnsignedDecimal(console, serial, (ulong)readiness.FreeNodeCount);
        Write(console, serial, (byte)' ');
        WriteFreePagesField(console, serial);
        WriteUnsignedDecimal(console, serial, readiness.FreePages);
        Write(console, serial, (byte)' ');
        WriteFreeBytesField(console, serial);
        WriteUnsignedDecimal(console, serial, readiness.FreeBytes);
        Write(console, serial, (byte)' ');
        WritePageSizeField(console, serial);
        WriteUnsignedDecimal(console, serial, readiness.PageSize);
        Write(console, serial, (byte)' ');
        WriteStatusCodeField(console, serial);
        WriteUnsignedDecimal(console, serial, (ulong)readiness.Status);
        WriteCrLf(console, serial);
    }

    private static void WriteBootPageAllocatorReadyPrefix(EfiSimpleTextOutputProtocol* console, EfiSerialIoProtocol* serial)
    {
        Write(console, serial, (byte)'B');
        Write(console, serial, (byte)'O');
        Write(console, serial, (byte)'O');
        Write(console, serial, (byte)'T');
        Write(console, serial, (byte)'_');
        Write(console, serial, (byte)'P');
        Write(console, serial, (byte)'A');
        Write(console, serial, (byte)'G');
        Write(console, serial, (byte)'E');
        Write(console, serial, (byte)'_');
        Write(console, serial, (byte)'A');
        Write(console, serial, (byte)'L');
        Write(console, serial, (byte)'L');
        Write(console, serial, (byte)'O');
        Write(console, serial, (byte)'C');
        Write(console, serial, (byte)'A');
        Write(console, serial, (byte)'T');
        Write(console, serial, (byte)'O');
        Write(console, serial, (byte)'R');
        Write(console, serial, (byte)'_');
        Write(console, serial, (byte)'R');
        Write(console, serial, (byte)'E');
        Write(console, serial, (byte)'A');
        Write(console, serial, (byte)'D');
        Write(console, serial, (byte)'Y');
        Write(console, serial, (byte)' ');
        WriteStatusField(console, serial);
    }

    private static void WriteReadyStatus(EfiSimpleTextOutputProtocol* console, EfiSerialIoProtocol* serial)
    {
        Write(console, serial, (byte)'R');
        Write(console, serial, (byte)'e');
        Write(console, serial, (byte)'a');
        Write(console, serial, (byte)'d');
        Write(console, serial, (byte)'y');
    }

    private static void WriteBootPageAllocatorNotReadyPrefix(EfiSimpleTextOutputProtocol* console, EfiSerialIoProtocol* serial)
    {
        Write(console, serial, (byte)'B');
        Write(console, serial, (byte)'O');
        Write(console, serial, (byte)'O');
        Write(console, serial, (byte)'T');
        Write(console, serial, (byte)'_');
        Write(console, serial, (byte)'P');
        Write(console, serial, (byte)'A');
        Write(console, serial, (byte)'G');
        Write(console, serial, (byte)'E');
        Write(console, serial, (byte)'_');
        Write(console, serial, (byte)'A');
        Write(console, serial, (byte)'L');
        Write(console, serial, (byte)'L');
        Write(console, serial, (byte)'O');
        Write(console, serial, (byte)'C');
        Write(console, serial, (byte)'A');
        Write(console, serial, (byte)'T');
        Write(console, serial, (byte)'O');
        Write(console, serial, (byte)'R');
        Write(console, serial, (byte)'_');
        Write(console, serial, (byte)'N');
        Write(console, serial, (byte)'O');
        Write(console, serial, (byte)'T');
        Write(console, serial, (byte)'_');
        Write(console, serial, (byte)'R');
        Write(console, serial, (byte)'E');
        Write(console, serial, (byte)'A');
        Write(console, serial, (byte)'D');
        Write(console, serial, (byte)'Y');
        Write(console, serial, (byte)' ');
        WriteStatusField(console, serial);
    }

    private static void WriteStatusField(EfiSimpleTextOutputProtocol* console, EfiSerialIoProtocol* serial)
    {
        Write(console, serial, (byte)'s');
        Write(console, serial, (byte)'t');
        Write(console, serial, (byte)'a');
        Write(console, serial, (byte)'t');
        Write(console, serial, (byte)'u');
        Write(console, serial, (byte)'s');
        Write(console, serial, (byte)'=');
    }

    private static void WriteStatusCodeField(EfiSimpleTextOutputProtocol* console, EfiSerialIoProtocol* serial)
    {
        Write(console, serial, (byte)'s');
        Write(console, serial, (byte)'t');
        Write(console, serial, (byte)'a');
        Write(console, serial, (byte)'t');
        Write(console, serial, (byte)'u');
        Write(console, serial, (byte)'s');
        Write(console, serial, (byte)'C');
        Write(console, serial, (byte)'o');
        Write(console, serial, (byte)'d');
        Write(console, serial, (byte)'e');
        Write(console, serial, (byte)'=');
    }

    private static void WriteLiveAllocatorNotInstalledStatus(EfiSimpleTextOutputProtocol* console, EfiSerialIoProtocol* serial)
    {
        Write(console, serial, (byte)'L');
        Write(console, serial, (byte)'i');
        Write(console, serial, (byte)'v');
        Write(console, serial, (byte)'e');
        Write(console, serial, (byte)'A');
        Write(console, serial, (byte)'l');
        Write(console, serial, (byte)'l');
        Write(console, serial, (byte)'o');
        Write(console, serial, (byte)'c');
        Write(console, serial, (byte)'a');
        Write(console, serial, (byte)'t');
        Write(console, serial, (byte)'o');
        Write(console, serial, (byte)'r');
        Write(console, serial, (byte)'N');
        Write(console, serial, (byte)'o');
        Write(console, serial, (byte)'t');
        Write(console, serial, (byte)'I');
        Write(console, serial, (byte)'n');
        Write(console, serial, (byte)'s');
        Write(console, serial, (byte)'t');
        Write(console, serial, (byte)'a');
        Write(console, serial, (byte)'l');
        Write(console, serial, (byte)'l');
        Write(console, serial, (byte)'e');
        Write(console, serial, (byte)'d');
    }

    private static void WriteNoConventionalMemoryStatus(EfiSimpleTextOutputProtocol* console, EfiSerialIoProtocol* serial)
    {
        Write(console, serial, (byte)'N');
        Write(console, serial, (byte)'o');
        Write(console, serial, (byte)'C');
        Write(console, serial, (byte)'o');
        Write(console, serial, (byte)'n');
        Write(console, serial, (byte)'v');
        Write(console, serial, (byte)'e');
        Write(console, serial, (byte)'n');
        Write(console, serial, (byte)'t');
        Write(console, serial, (byte)'i');
        Write(console, serial, (byte)'o');
        Write(console, serial, (byte)'n');
        Write(console, serial, (byte)'a');
        Write(console, serial, (byte)'l');
        Write(console, serial, (byte)'M');
        Write(console, serial, (byte)'e');
        Write(console, serial, (byte)'m');
        Write(console, serial, (byte)'o');
        Write(console, serial, (byte)'r');
        Write(console, serial, (byte)'y');
    }

    private static void WriteRegionsField(EfiSimpleTextOutputProtocol* console, EfiSerialIoProtocol* serial)
    {
        Write(console, serial, (byte)'r');
        Write(console, serial, (byte)'e');
        Write(console, serial, (byte)'g');
        Write(console, serial, (byte)'i');
        Write(console, serial, (byte)'o');
        Write(console, serial, (byte)'n');
        Write(console, serial, (byte)'s');
        Write(console, serial, (byte)'=');
    }

    private static void WriteConventionalPagesField(EfiSimpleTextOutputProtocol* console, EfiSerialIoProtocol* serial)
    {
        Write(console, serial, (byte)'c');
        Write(console, serial, (byte)'o');
        Write(console, serial, (byte)'n');
        Write(console, serial, (byte)'v');
        Write(console, serial, (byte)'e');
        Write(console, serial, (byte)'n');
        Write(console, serial, (byte)'t');
        Write(console, serial, (byte)'i');
        Write(console, serial, (byte)'o');
        Write(console, serial, (byte)'n');
        Write(console, serial, (byte)'a');
        Write(console, serial, (byte)'l');
        Write(console, serial, (byte)'P');
        Write(console, serial, (byte)'a');
        Write(console, serial, (byte)'g');
        Write(console, serial, (byte)'e');
        Write(console, serial, (byte)'s');
        Write(console, serial, (byte)'=');
    }

    private static void WriteConventionalBytesField(EfiSimpleTextOutputProtocol* console, EfiSerialIoProtocol* serial)
    {
        Write(console, serial, (byte)'c');
        Write(console, serial, (byte)'o');
        Write(console, serial, (byte)'n');
        Write(console, serial, (byte)'v');
        Write(console, serial, (byte)'e');
        Write(console, serial, (byte)'n');
        Write(console, serial, (byte)'t');
        Write(console, serial, (byte)'i');
        Write(console, serial, (byte)'o');
        Write(console, serial, (byte)'n');
        Write(console, serial, (byte)'a');
        Write(console, serial, (byte)'l');
        Write(console, serial, (byte)'B');
        Write(console, serial, (byte)'y');
        Write(console, serial, (byte)'t');
        Write(console, serial, (byte)'e');
        Write(console, serial, (byte)'s');
        Write(console, serial, (byte)'=');
    }

    private static void WriteFreeNodesField(EfiSimpleTextOutputProtocol* console, EfiSerialIoProtocol* serial)
    {
        Write(console, serial, (byte)'f');
        Write(console, serial, (byte)'r');
        Write(console, serial, (byte)'e');
        Write(console, serial, (byte)'e');
        Write(console, serial, (byte)'N');
        Write(console, serial, (byte)'o');
        Write(console, serial, (byte)'d');
        Write(console, serial, (byte)'e');
        Write(console, serial, (byte)'s');
        Write(console, serial, (byte)'=');
    }

    private static void WriteFreePagesField(EfiSimpleTextOutputProtocol* console, EfiSerialIoProtocol* serial)
    {
        Write(console, serial, (byte)'f');
        Write(console, serial, (byte)'r');
        Write(console, serial, (byte)'e');
        Write(console, serial, (byte)'e');
        Write(console, serial, (byte)'P');
        Write(console, serial, (byte)'a');
        Write(console, serial, (byte)'g');
        Write(console, serial, (byte)'e');
        Write(console, serial, (byte)'s');
        Write(console, serial, (byte)'=');
    }

    private static void WriteFreeBytesField(EfiSimpleTextOutputProtocol* console, EfiSerialIoProtocol* serial)
    {
        Write(console, serial, (byte)'f');
        Write(console, serial, (byte)'r');
        Write(console, serial, (byte)'e');
        Write(console, serial, (byte)'e');
        Write(console, serial, (byte)'B');
        Write(console, serial, (byte)'y');
        Write(console, serial, (byte)'t');
        Write(console, serial, (byte)'e');
        Write(console, serial, (byte)'s');
        Write(console, serial, (byte)'=');
    }

    private static void WritePageSizeField(EfiSimpleTextOutputProtocol* console, EfiSerialIoProtocol* serial)
    {
        Write(console, serial, (byte)'p');
        Write(console, serial, (byte)'a');
        Write(console, serial, (byte)'g');
        Write(console, serial, (byte)'e');
        Write(console, serial, (byte)'S');
        Write(console, serial, (byte)'i');
        Write(console, serial, (byte)'z');
        Write(console, serial, (byte)'e');
        Write(console, serial, (byte)'=');
    }

    private static bool ScanAndWriteAcpiRsdp(EfiSimpleTextOutputProtocol* console, EfiSerialIoProtocol* serial, EfiSystemTable* systemTable)
    {
        if (systemTable == null)
        {
            s_acpiRsdpScanResult = EfiConfigurationTableScanResult.Fail(EfiConfigurationTableScanStatus.InvalidConfigurationTableAddress);
            s_bootHandoff.ClearAcpiRsdp((uint)s_acpiRsdpScanResult.Status);
            WriteUefiAcpiRsdpInvalid(console, serial, s_acpiRsdpScanResult.Status);
            return true;
        }

        s_acpiRsdpScanResult = EfiConfigurationTableScanner.Scan(systemTable->ConfigurationTable, systemTable->NumberOfTableEntries);
        if (s_acpiRsdpScanResult.Succeeded)
        {
            s_bootHandoff.SetAcpiRsdp(s_acpiRsdpScanResult.RsdpAddress, (uint)s_acpiRsdpScanResult.TableKind, (uint)s_acpiRsdpScanResult.Status);
            EfiBootHandoffStatus handoffStatus = EfiBootHandoffValidator.Validate(s_bootHandoff);
            if (handoffStatus != EfiBootHandoffStatus.Success)
            {
                WriteBootHandoffFail(console, serial, handoffStatus);
                return false;
            }

            WriteBootAcpiRsdpHandoffReady(console, serial);
            WriteUefiAcpiRsdpReady(console, serial);
            return true;
        }

        s_bootHandoff.ClearAcpiRsdp((uint)s_acpiRsdpScanResult.Status);
        if (s_acpiRsdpScanResult.Status == EfiConfigurationTableScanStatus.NotFound)
        {
            WriteUefiAcpiRsdpMissing(console, serial);
            return true;
        }

        WriteUefiAcpiRsdpInvalid(console, serial, s_acpiRsdpScanResult.Status);
        return true;
    }

    private static void WriteBootAcpiRsdpHandoffReady(EfiSimpleTextOutputProtocol* console, EfiSerialIoProtocol* serial)
    {
        Write(console, serial, (byte)'B');
        Write(console, serial, (byte)'O');
        Write(console, serial, (byte)'O');
        Write(console, serial, (byte)'T');
        Write(console, serial, (byte)'_');
        Write(console, serial, (byte)'A');
        Write(console, serial, (byte)'C');
        Write(console, serial, (byte)'P');
        Write(console, serial, (byte)'I');
        Write(console, serial, (byte)'_');
        Write(console, serial, (byte)'R');
        Write(console, serial, (byte)'S');
        Write(console, serial, (byte)'D');
        Write(console, serial, (byte)'P');
        Write(console, serial, (byte)'_');
        Write(console, serial, (byte)'H');
        Write(console, serial, (byte)'A');
        Write(console, serial, (byte)'N');
        Write(console, serial, (byte)'D');
        Write(console, serial, (byte)'O');
        Write(console, serial, (byte)'F');
        Write(console, serial, (byte)'F');
        Write(console, serial, (byte)'_');
        Write(console, serial, (byte)'R');
        Write(console, serial, (byte)'E');
        Write(console, serial, (byte)'A');
        Write(console, serial, (byte)'D');
        Write(console, serial, (byte)'Y');
        WriteCrLf(console, serial);
    }

    private static void WriteUefiAcpiRsdpReady(EfiSimpleTextOutputProtocol* console, EfiSerialIoProtocol* serial)
    {
        Write(console, serial, (byte)'U');
        Write(console, serial, (byte)'E');
        Write(console, serial, (byte)'F');
        Write(console, serial, (byte)'I');
        Write(console, serial, (byte)'_');
        Write(console, serial, (byte)'A');
        Write(console, serial, (byte)'C');
        Write(console, serial, (byte)'P');
        Write(console, serial, (byte)'I');
        Write(console, serial, (byte)'_');
        Write(console, serial, (byte)'R');
        Write(console, serial, (byte)'S');
        Write(console, serial, (byte)'D');
        Write(console, serial, (byte)'P');
        Write(console, serial, (byte)'_');
        Write(console, serial, (byte)'R');
        Write(console, serial, (byte)'E');
        Write(console, serial, (byte)'A');
        Write(console, serial, (byte)'D');
        Write(console, serial, (byte)'Y');
        WriteCrLf(console, serial);
    }

    private static void WriteUefiAcpiRsdpMissing(EfiSimpleTextOutputProtocol* console, EfiSerialIoProtocol* serial)
    {
        Write(console, serial, (byte)'U');
        Write(console, serial, (byte)'E');
        Write(console, serial, (byte)'F');
        Write(console, serial, (byte)'I');
        Write(console, serial, (byte)'_');
        Write(console, serial, (byte)'A');
        Write(console, serial, (byte)'C');
        Write(console, serial, (byte)'P');
        Write(console, serial, (byte)'I');
        Write(console, serial, (byte)'_');
        Write(console, serial, (byte)'R');
        Write(console, serial, (byte)'S');
        Write(console, serial, (byte)'D');
        Write(console, serial, (byte)'P');
        Write(console, serial, (byte)'_');
        Write(console, serial, (byte)'M');
        Write(console, serial, (byte)'I');
        Write(console, serial, (byte)'S');
        Write(console, serial, (byte)'S');
        Write(console, serial, (byte)'I');
        Write(console, serial, (byte)'N');
        Write(console, serial, (byte)'G');
        WriteCrLf(console, serial);
    }

    private static void WriteUefiAcpiRsdpInvalid(EfiSimpleTextOutputProtocol* console, EfiSerialIoProtocol* serial, EfiConfigurationTableScanStatus status)
    {
        Write(console, serial, (byte)'U');
        Write(console, serial, (byte)'E');
        Write(console, serial, (byte)'F');
        Write(console, serial, (byte)'I');
        Write(console, serial, (byte)'_');
        Write(console, serial, (byte)'A');
        Write(console, serial, (byte)'C');
        Write(console, serial, (byte)'P');
        Write(console, serial, (byte)'I');
        Write(console, serial, (byte)'_');
        Write(console, serial, (byte)'R');
        Write(console, serial, (byte)'S');
        Write(console, serial, (byte)'D');
        Write(console, serial, (byte)'P');
        Write(console, serial, (byte)'_');
        Write(console, serial, (byte)'I');
        Write(console, serial, (byte)'N');
        Write(console, serial, (byte)'V');
        Write(console, serial, (byte)'A');
        Write(console, serial, (byte)'L');
        Write(console, serial, (byte)'I');
        Write(console, serial, (byte)'D');
        Write(console, serial, (byte)' ');
        WriteUnsignedDecimal(console, serial, (ulong)status);
        WriteCrLf(console, serial);
    }

#if (ACPI_PHYSICAL_DISCOVERY_PROBE || CLROS_NORMAL_VERIFIED_EXIT)
    // Only this conditional probe owns these buffers. Phase 1 is one-shot
    // acquisition, 2 is admitted snapshots, and 3 publishes all five copies.
    private static bool WriteAcpiPhysicalDiscoveryProbe(
        EfiSimpleTextOutputProtocol* console, EfiSerialIoProtocol* serial)
    {
        if (!s_acpiRsdpScanResult.Succeeded)
        {
            WriteAcpiDiscoveryFail(console, serial, 1, 10 + (ulong)s_acpiRsdpScanResult.Status);
            return false;
        }
        fixed (AcpiBootstrapWorkspace* w = &s_acpiBootstrap)
        fixed (AcpiBootstrapOwnedBuffer* owned = &s_acpiBootstrapOwned)
        {
            if (!TryCaptureAcpiBootstrap(w, owned->Bytes, AcpiOwnedPayloadCapacity,
                    s_acpiRsdpScanResult.RsdpAddress, out ulong status))
            {
                WriteAcpiDiscoveryFail(console, serial, 4, status);
                return false;
            }
            WriteAcpiRsdpSnapshotReady(console, serial);
            WriteAcpiRsdpValidated(console, serial);
            WriteAcpiRootSdtSnapshotReady(console, serial);
            WriteAcpiRootSdtValidated(console, serial);
            WriteAcpiMcfgDiscoveryReady(console, serial);
            WriteAcpiBootstrapTranscript(console, serial, w, owned->Bytes);
            return true;
        }
    }

    private static bool TryCaptureAcpiBootstrap(AcpiBootstrapWorkspace* w,
        byte* owned, int capacity, ulong rsdpAddress, out ulong status)
    {
        status = 60;
        if (!AcpiSourceAdmission(w, owned, capacity, rsdpAddress, 20) ||
            !TryBeginAcpiBootstrap(w, owned, capacity))
            return false;
        w->SourceRsdp = rsdpAddress;
        if (!TryReadAndValidateAcpiRsdp(w, owned, capacity, rsdpAddress, out status) ||
            !TryReadAndValidateRootSdt(w, owned, capacity, out status) ||
            !TryDiscoverSelectedAcpiTables(w, owned, capacity, out status))
            return false;
        w->Phase = 2;
        return TryPublishAcpiBootstrap(w, owned, capacity, out status);
    }

    private static bool AcpiRange(ulong start, ulong length) =>
        start != 0 && length != 0 && length <= 0xffffffffffffffff - start;

    // Callers validate both complete ranges before this comparison.
    private static bool AcpiOverlap(ulong a, ulong n, ulong b, ulong m) =>
        a < b + m && b < a + n;

    private static bool AcpiBootstrapStorage(ulong workspace, ulong owned, int capacity)
    {
        if (capacity <= 0 || capacity > AcpiOwnedPayloadCapacity ||
            (workspace & 7) != 0 || !AcpiRange(workspace, (ulong)sizeof(AcpiBootstrapWorkspace)) ||
            !AcpiRange(owned, (ulong)capacity + 32) ||
            AcpiOverlap(workspace, (ulong)sizeof(AcpiBootstrapWorkspace), owned, (ulong)capacity + 32))
            return false;
        fixed (OwnedMemoryRegionBuffer* regions = &s_ownedMemoryRegions)
        fixed (BootMemoryRegionMaterializationResult* map = &s_memoryRegionMaterialization)
        fixed (EfiConfigurationTableScanResult* scan = &s_acpiRsdpScanResult)
        {
            return AcpiStorageExcludes(workspace, owned, capacity, (ulong)regions, (ulong)sizeof(OwnedMemoryRegionBuffer)) &&
                AcpiStorageExcludes(workspace, owned, capacity, (ulong)map, (ulong)sizeof(BootMemoryRegionMaterializationResult)) &&
                AcpiStorageExcludes(workspace, owned, capacity, (ulong)scan, (ulong)sizeof(EfiConfigurationTableScanResult));
        }
    }

    private static bool AcpiStorageExcludes(ulong workspace, ulong owned, int capacity, ulong address, ulong length) =>
        AcpiRange(address, length) &&
        !AcpiOverlap(workspace, (ulong)sizeof(AcpiBootstrapWorkspace), address, length) &&
        !AcpiOverlap(owned, (ulong)capacity + 32, address, length);

    private static bool TryBeginAcpiBootstrap(AcpiBootstrapWorkspace* w, byte* owned, int capacity)
    {
        // Numeric full-capacity admission precedes the first state read/reset.
        if (!AcpiBootstrapStorage((ulong)w, (ulong)owned, capacity) || w->Phase != 0)
            return false;
        byte* bytes = (byte*)w;
        for (int i = 0; i < sizeof(AcpiBootstrapWorkspace); i++)
            bytes[i] = 0;
        w->Phase = 1;
        w->Owned = (ulong)owned;
        w->Capacity = (ulong)capacity;
        return true;
    }

    private static bool AcpiSourceAdmission(AcpiBootstrapWorkspace* w, byte* owned,
        int capacity, ulong address, int length)
    {
        if (length <= 0 || !AcpiRange(address, (ulong)length) ||
            !AcpiBootstrapStorage((ulong)w, (ulong)owned, capacity) ||
            AcpiOverlap(address, (ulong)length, (ulong)w, (ulong)sizeof(AcpiBootstrapWorkspace)) ||
            AcpiOverlap(address, (ulong)length, (ulong)owned, (ulong)capacity + 32))
            return false;
        fixed (OwnedMemoryRegionBuffer* regions = &s_ownedMemoryRegions)
        fixed (BootMemoryRegionMaterializationResult* map = &s_memoryRegionMaterialization)
        fixed (EfiConfigurationTableScanResult* scan = &s_acpiRsdpScanResult)
        {
            return !AcpiOverlap(address, (ulong)length, (ulong)regions, (ulong)sizeof(OwnedMemoryRegionBuffer)) &&
                !AcpiOverlap(address, (ulong)length, (ulong)map, (ulong)sizeof(BootMemoryRegionMaterializationResult)) &&
                !AcpiOverlap(address, (ulong)length, (ulong)scan, (ulong)sizeof(EfiConfigurationTableScanResult)) &&
                IsAcpiPhysicalRangeAllowed(address, (ulong)length);
        }
    }

    private static bool TryReadAcpiPhysicalBytes(AcpiBootstrapWorkspace* w, byte* owned,
        int capacity, ulong address, int length, byte* destination, int destinationCapacity)
    {
        // All writable targets are exact finite workspace members, never a caller pointer.
        if (!AcpiReadDestination(w, destination, destinationCapacity) || length > destinationCapacity ||
            !AcpiSourceAdmission(w, owned, capacity, address, length))
            return false;
        byte* source = (byte*)address;
        for (int i = 0; i < length; i++)
            destination[i] = source[i];
        return true;
    }

    private static bool AcpiReadDestination(AcpiBootstrapWorkspace* w, byte* p, int n) =>
        (p == w->Rsdp && n == AcpiRsdpScratchSize) ||
        (p == w->Root && n == AcpiRootSdtBufferSize) ||
        (p == w->Header && n == AcpiChildHeaderScratchSize) ||
        (p == w->Child && n == AcpiChildTableBufferSize);

    private static bool AcpiRsdpEnvelope(byte* p, int length, byte revision) =>
        length >= 20 && length <= AcpiRsdpScratchSize && HasRsdpSignature(p) &&
        !HasZeroOemId(p) && p[15] == revision && revision != 1 && IsChecksumValid(p, 20) &&
        (revision == 0 ? length == 20 : length >= 36 && ReadUInt32LittleEndian(p + 20) == (uint)length && IsChecksumValid(p, length));

    private static bool AcpiSdtEnvelope(byte* p, int length, uint signature) =>
        length >= AcpiSdtHeaderLength && length <= AcpiChildTableBufferSize &&
        ReadUInt32LittleEndian(p) == signature && ReadUInt32LittleEndian(p + 4) == (uint)length &&
        IsChecksumValid(p, length);

    private static bool TryReadAndValidateAcpiRsdp(AcpiBootstrapWorkspace* w,
        byte* owned, int capacity, ulong address, out ulong status)
    {
        status = 20;
        if (!TryReadAcpiPhysicalBytes(w, owned, capacity, address, 20, w->Rsdp, AcpiRsdpScratchSize))
            return false;
        byte revision = w->Rsdp[15];
        if (!HasRsdpSignature(w->Rsdp) || HasZeroOemId(w->Rsdp) ||
            !IsChecksumValid(w->Rsdp, 20) || revision == 1)
        {
            status = 21;
            return false;
        }
        int length = 20;
        if (revision != 0)
        {
            status = 25;
            if (!TryReadAcpiPhysicalBytes(w, owned, capacity, address, 36, w->Rsdp, AcpiRsdpScratchSize))
                return false;
            uint declared = ReadUInt32LittleEndian(w->Rsdp + 20);
            if (declared < 36 || declared > AcpiRsdpScratchSize)
            {
                status = 26;
                return false;
            }
            length = (int)declared;
            if (!TryReadAcpiPhysicalBytes(w, owned, capacity, address, length, w->Rsdp, AcpiRsdpScratchSize))
                return false;
        }
        if (!AcpiRsdpEnvelope(w->Rsdp, length, revision))
        {
            status = 28;
            return false;
        }
        AcpiDescriptor(w, 0, address, 0x2052545020445352, length, 0xffffffffffffffff);
        status = 0;
        return true;
    }

    private static bool AcpiSelectedSourceDisjoint(AcpiBootstrapWorkspace* w, ulong address, int length, int beforeRole)
    {
        if (!AcpiRange(address, (ulong)length))
            return false;
        for (int role = 0; role < beforeRole; role++)
        {
            ulong* row = w->Descriptors + role * 6;
            if (row[2] != 0 && AcpiOverlap(address, (ulong)length, row[0], row[2]))
                return false;
        }
        return true;
    }

    private static void AcpiDescriptor(AcpiBootstrapWorkspace* w, int role, ulong physical,
        ulong signature, int length, ulong rootIndex)
    {
        ulong* row = w->Descriptors + role * 6;
        row[0] = physical;
        row[1] = signature;
        row[2] = (ulong)length;
        row[3] = rootIndex;
        row[4] = 0;
        row[5] = 0;
    }

    private static bool TryReadAndValidateRootSdt(AcpiBootstrapWorkspace* w, byte* owned,
        int capacity, out ulong status)
    {
        ulong xsdt = w->Rsdp[15] >= 2 ? ReadUInt64LittleEndian(w->Rsdp + 24) : 0;
        ulong address = xsdt != 0 ? xsdt : ReadUInt32LittleEndian(w->Rsdp + 16);
        uint signature = xsdt != 0 ? AcpiXsdtSignature : AcpiRsdtSignature;
        status = 31;
        if (!AcpiSelectedSourceDisjoint(w, address, AcpiSdtHeaderLength, 1) ||
            !TryReadAcpiPhysicalBytes(w, owned, capacity, address, AcpiSdtHeaderLength, w->Root, AcpiRootSdtBufferSize))
            return false;
        uint declared = ReadUInt32LittleEndian(w->Root + 4);
        if (ReadUInt32LittleEndian(w->Root) != signature || declared < 36 || declared > AcpiRootSdtBufferSize)
        {
            status = 33;
            return false;
        }
        int length = (int)declared;
        if (!AcpiSelectedSourceDisjoint(w, address, length, 1) ||
            !TryReadAcpiPhysicalBytes(w, owned, capacity, address, length, w->Root, AcpiRootSdtBufferSize))
            return false;
        if (!AcpiSdtEnvelope(w->Root, length, signature))
        {
            status = 35;
            return false;
        }
        int width = xsdt != 0 ? 8 : 4, count = (length - 36) / width;
        if ((length - 36) % width != 0 || count == 0 || count > AcpiPointerScratchCapacity)
        {
            status = 37;
            return false;
        }
        AcpiDescriptor(w, 1, address, signature, length, 0xffffffffffffffff);
        for (int i = 0; i < count; i++)
        {
            byte* p = w->Root + 36 + i * width;
            ulong value = width == 8 ? ReadUInt64LittleEndian(p) : ReadUInt32LittleEndian(p);
            if (!AcpiRange(value, 36) || !AcpiSelectedSourceDisjoint(w, value, 36, 2))
            {
                status = 38;
                return false;
            }
            for (int j = 0; j < i; j++)
                if (w->Pointers[j] == value)
                {
                    status = 39;
                    return false;
                }
            w->Pointers[i] = value;
        }
        w->PointerCount = (ulong)count;
        status = 0;
        return true;
    }

    private static bool AcpiChildSourceDisjoint(AcpiBootstrapWorkspace* w, int index, int length)
    {
        ulong address = w->Pointers[index];
        if (!AcpiSelectedSourceDisjoint(w, address, length, 2))
            return false;
        for (int i = 0; i < (int)w->PointerCount; i++)
        {
            if (i == index)
                continue;
            // Known headers for future entries; complete envelopes for visited entries.
            ulong n = w->VisitedLengths[i] == 0 ? 36UL : w->VisitedLengths[i];
            if (AcpiOverlap(address, (ulong)length, w->Pointers[i], n))
                return false;
        }
        return true;
    }

    private static bool TryDiscoverSelectedAcpiTables(AcpiBootstrapWorkspace* w, byte* owned,
        int capacity, out ulong status)
    {
        status = 40;
        if (w->PointerCount == 0 || w->PointerCount > AcpiPointerScratchCapacity)
            return false;
        for (int i = 0; i < (int)w->PointerCount; i++)
        {
            ulong address = w->Pointers[i];
            if (!AcpiChildSourceDisjoint(w, i, 36) ||
                !TryReadAcpiPhysicalBytes(w, owned, capacity, address, 36, w->Header, AcpiChildHeaderScratchSize))
            {
                status = 41;
                return false;
            }
            uint signature = ReadUInt32LittleEndian(w->Header), declared = ReadUInt32LittleEndian(w->Header + 4);
            if (declared < 36 || declared > AcpiChildTableBufferSize)
            {
                status = 42;
                return false;
            }
            int length = (int)declared;
            if (!AcpiChildSourceDisjoint(w, i, length) ||
                !TryReadAcpiPhysicalBytes(w, owned, capacity, address, length, w->Child, AcpiChildTableBufferSize))
            {
                status = 43;
                return false;
            }
            if (!AcpiSdtEnvelope(w->Child, length, signature))
            {
                status = 44;
                return false;
            }
            w->VisitedLengths[i] = (uint)length;
            int role = signature == AcpiMcfgSignature ? 2 : signature == AcpiMadtSignature ? 3 : signature == AcpiHpetSignature ? 4 : -1;
            if (role >= 0 && w->Descriptors[role * 6 + 2] == 0)
            {
                if (role == 2 && !TryParseMcfgSegments(w->Child, length, w->Segments, out status))
                    return false;
                byte* target = AcpiSnapshot(w, role);
                for (int j = 0; j < length; j++)
                    target[j] = w->Child[j];
                AcpiDescriptor(w, role, address, signature, length, (ulong)i);
            }
            if (w->Descriptors[14] != 0 && w->Descriptors[20] != 0 && w->Descriptors[26] != 0)
            {
                status = 0;
                return true;
            }
        }
        status = 45;
        return false;
    }

    private static byte* AcpiSnapshot(AcpiBootstrapWorkspace* w, int role) =>
        role == 0 ? w->Rsdp : role == 1 ? w->Root : role == 2 ? w->Mcfg : role == 3 ? w->Madt : w->Hpet;

    private static bool AcpiFinalAdmission(AcpiBootstrapWorkspace* w, byte* owned, int capacity, out ulong total)
    {
        total = 0;
        if (!AcpiBootstrapStorage((ulong)w, (ulong)owned, capacity) || w->Phase != 2)
            return false;
        for (int role = 0; role < 5; role++)
            if (w->Descriptors[role * 6 + 4] != 0 || w->Descriptors[role * 6 + 5] != 0)
                return false;
        return AcpiSnapshotAdmission(w, owned, capacity, out total);
    }

    private static bool AcpiSnapshotAdmission(AcpiBootstrapWorkspace* w, byte* owned, int capacity, out ulong total)
    {
        total = 0;
        if (!AcpiBootstrapStorage((ulong)w, (ulong)owned, capacity) || (w->Phase != 2 && w->Phase != 3) ||
            w->Owned != (ulong)owned || w->Capacity != (ulong)capacity ||
            w->PointerCount == 0 || w->PointerCount > AcpiPointerScratchCapacity)
            return false;
        for (int role = 0; role < 5; role++)
        {
            ulong* row = w->Descriptors + role * 6;
            int limit = role == 0 ? AcpiRsdpScratchSize : AcpiChildTableBufferSize;
            if (row[2] == 0 || row[2] > (ulong)limit ||
                !AcpiSourceAdmission(w, owned, capacity, row[0], (int)row[2]) ||
                !AcpiSelectedSourceDisjoint(w, row[0], (int)row[2], role))
                return false;
            byte* snapshot = AcpiSnapshot(w, role);
            if (role == 0)
            {
                if (row[0] != w->SourceRsdp || row[1] != 0x2052545020445352 || row[3] != 0xffffffffffffffff ||
                    !AcpiRsdpEnvelope(snapshot, (int)row[2], snapshot[15]))
                    return false;
            }
            else
            {
                uint signature = role == 1 ? (w->Rsdp[15] >= 2 && ReadUInt64LittleEndian(w->Rsdp + 24) != 0 ? AcpiXsdtSignature : AcpiRsdtSignature)
                    : role == 2 ? AcpiMcfgSignature : role == 3 ? AcpiMadtSignature : AcpiHpetSignature;
                if (row[1] != signature || !AcpiSdtEnvelope(snapshot, (int)row[2], signature))
                    return false;
                if (role == 1)
                {
                    ulong selected = signature == AcpiXsdtSignature ? ReadUInt64LittleEndian(w->Rsdp + 24) : ReadUInt32LittleEndian(w->Rsdp + 16);
                    if (row[0] != selected || row[3] != 0xffffffffffffffff ||
                        row[2] != 36 + w->PointerCount * (signature == AcpiXsdtSignature ? 8UL : 4UL))
                        return false;
                }
                else
                {
                    if (row[3] >= w->PointerCount || row[0] != w->Pointers[(int)row[3]])
                        return false;
                    int width = w->Descriptors[7] == AcpiXsdtSignature ? 8 : 4;
                    byte* rootPointer = w->Root + 36 + (int)row[3] * width;
                    if (row[0] != (width == 8 ? ReadUInt64LittleEndian(rootPointer) : ReadUInt32LittleEndian(rootPointer)))
                        return false;
                }
            }
            total += row[2]; // Five individually bounded lengths, maximum 266240.
        }
        return total <= (ulong)capacity;
    }

    private static bool TryPublishAcpiBootstrap(AcpiBootstrapWorkspace* w, byte* owned,
        int capacity, out ulong status)
    {
        status = 61;
        if (!AcpiFinalAdmission(w, owned, capacity, out ulong total))
            return false;
        // This is the first final-destination write, after all five spans pass.
        for (int i = 0; i < capacity + 32; i++)
            owned[i] = i < 16 || i >= capacity + 16 ? (byte)0xA7 : (byte)0xCD;
        int offset = 0;
        for (int role = 0; role < 5; role++)
        {
            ulong* row = w->Descriptors + role * 6;
            byte* source = AcpiSnapshot(w, role);
            row[4] = (ulong)offset;
            for (int i = 0; i < (int)row[2]; i++)
                owned[16 + offset + i] = source[i];
            offset += (int)row[2];
        }
        w->TotalBytes = total;
        if (!ReadBackAcpiBootstrap(w, owned, capacity))
        {
            status = 62;
            return false;
        }
        w->Phase = 3;
        status = 0;
        return true;
    }

    private static bool ReadBackAcpiBootstrap(AcpiBootstrapWorkspace* w, byte* owned, int capacity)
    {
        if (!AcpiSnapshotAdmission(w, owned, capacity, out ulong total) || w->TotalBytes != total)
            return false;
        ulong offset = 0, mismatch = 0;
        for (int role = 0; role < 5; role++)
        {
            ulong* row = w->Descriptors + role * 6;
            ulong limit = role == 0 ? (ulong)AcpiRsdpScratchSize : (ulong)AcpiChildTableBufferSize;
            if (row[2] == 0 || row[2] > limit || row[4] != offset || row[2] > (ulong)capacity - offset)
                return false;
            byte* source = AcpiSnapshot(w, role);
            for (int i = 0; i < (int)row[2]; i++)
                if (owned[16 + (int)offset + i] != source[i])
                    mismatch++;
            row[5] = row[2];
            offset += row[2];
        }
        if (offset != w->TotalBytes)
            return false;
        for (int i = 0; i < 16; i++)
        {
            if (owned[i] != 0xA7)
                mismatch++;
            if (owned[16 + capacity + i] != 0xA7)
                mismatch++;
        }
        for (int i = (int)offset; i < capacity; i++)
            if (owned[16 + i] != 0xCD)
                mismatch++;
        w->MismatchBytes = mismatch;
        return mismatch == 0;
    }

    private static void WriteAcpiBootstrapAscii(EfiSimpleTextOutputProtocol* console,
        EfiSerialIoProtocol* serial, ulong bytes, int count)
    {
        for (int i = 0; i < count; i++)
        {
            Write(console, serial, (byte)bytes);
            bytes >>= 8;
        }
    }

    private static void WriteAcpiBootstrapTranscript(EfiSimpleTextOutputProtocol* console,
        EfiSerialIoProtocol* serial, AcpiBootstrapWorkspace* w, byte* owned)
    {
        WriteAcpiBootstrapAscii(console, serial, 0x4154535F54534554, 8);
        WriteAcpiBootstrapAscii(console, serial, 0x302E33306D205452, 8);
        WriteAcpiBootstrapAscii(console, serial, 0x000000000A0D3230, 4);
        for (int role = 0; role < 5; role++)
        {
            ulong* row = w->Descriptors + role * 6;
            WriteAcpiBootstrapAscii(console, serial, 0x304D5F534F524C43, 8);
            WriteAcpiBootstrapAscii(console, serial, 0x41545F3230305F33, 8);
            WriteAcpiBootstrapAscii(console, serial, 0x656C6F7220454C42, 8);
            WriteAcpiBootstrapAscii(console, serial, 0x000000000000003D, 1);
            if (role == 0)
            {
                WriteAcpiBootstrapAscii(console, serial, 0x0000000070647372, 4);
            }
            else if (role == 1)
            {
                WriteAcpiBootstrapAscii(console, serial, 0x00000000746F6F72, 4);
            }
            else if (role == 2)
            {
                WriteAcpiBootstrapAscii(console, serial, 0x000000006766636D, 4);
            }
            else if (role == 3)
            {
                WriteAcpiBootstrapAscii(console, serial, 0x0000000063697061, 4);
            }
            else if (role == 4)
            {
                WriteAcpiBootstrapAscii(console, serial, 0x0000000074657068, 4);
            }
            WriteAcpiBootstrapAscii(console, serial, 0x6163697379687020, 8);
            WriteAcpiBootstrapAscii(console, serial, 0x0000000000003D6C, 2);
            WriteUnsignedDecimal(console, serial, row[0]);
            WriteAcpiBootstrapAscii(console, serial, 0x003D64656E776F20, 7);
            WriteUnsignedDecimal(console, serial, (ulong)(owned + 16) + row[4]);
            WriteAcpiBootstrapAscii(console, serial, 0x3D74657366666F20, 8);
            WriteUnsignedDecimal(console, serial, row[4]);
            WriteAcpiBootstrapAscii(console, serial, 0x3D6874676E656C20, 8);
            WriteUnsignedDecimal(console, serial, row[2]);
            WriteAcpiBootstrapAscii(console, serial, 0x7574616E67697320, 8);
            WriteAcpiBootstrapAscii(console, serial, 0x00000000003D6572, 3);
            WriteUnsignedDecimal(console, serial, row[1]);
            WriteAcpiBootstrapAscii(console, serial, 0x646E49746F6F7220, 8);
            WriteAcpiBootstrapAscii(console, serial, 0x00000000003D7865, 3);
            WriteUnsignedDecimal(console, serial, row[3]);
            WriteAcpiBootstrapAscii(console, serial, 0x657261706D6F6320, 8);
            WriteAcpiBootstrapAscii(console, serial, 0x0000000000003D64, 2);
            WriteUnsignedDecimal(console, serial, row[5]);
            WriteAcpiBootstrapAscii(console, serial, 0x6374616D73696D20, 8);
            WriteAcpiBootstrapAscii(console, serial, 0x000000003D736568, 4);
            WriteUnsignedDecimal(console, serial, w->MismatchBytes);
            WriteCrLf(console, serial);
        }
        WriteAcpiBootstrapAscii(console, serial, 0x304D5F534F524C43, 8);
        WriteAcpiBootstrapAscii(console, serial, 0x45525F3230305F33, 8);
        WriteAcpiBootstrapAscii(console, serial, 0x746F74204E494154, 8);
        WriteAcpiBootstrapAscii(console, serial, 0x00000000003D6C61, 3);
        WriteUnsignedDecimal(console, serial, w->TotalBytes);
        WriteAcpiBootstrapAscii(console, serial, 0x7069726373656420, 8);
        WriteAcpiBootstrapAscii(console, serial, 0x6720353D73726F74, 8);
        WriteAcpiBootstrapAscii(console, serial, 0x32333D7364726175, 8);
        WriteAcpiBootstrapAscii(console, serial, 0x00003D6C69617420, 6);
        WriteUnsignedDecimal(console, serial, w->Capacity - w->TotalBytes);
        WriteAcpiBootstrapAscii(console, serial, 0x6374616D73696D20, 8);
        WriteAcpiBootstrapAscii(console, serial, 0x430A0D303D736568, 8);
        WriteAcpiBootstrapAscii(console, serial, 0x33304D5F534F524C, 8);
        WriteAcpiBootstrapAscii(console, serial, 0x4145525F3230305F, 8);
        WriteAcpiBootstrapAscii(console, serial, 0x776D726966205944, 8);
        WriteAcpiBootstrapAscii(console, serial, 0x6C69617641657261, 8);
        WriteAcpiBootstrapAscii(console, serial, 0x7572743D656C6261, 8);
        WriteAcpiBootstrapAscii(console, serial, 0x6F42746978652065, 8);
        WriteAcpiBootstrapAscii(console, serial, 0x636976726553746F, 8);
        WriteAcpiBootstrapAscii(console, serial, 0x65736C61663D7365, 8);
        WriteAcpiBootstrapAscii(console, serial, 0x505F545345540A0D, 8);
        WriteAcpiBootstrapAscii(console, serial, 0x2E33306D20535341, 8);
        WriteAcpiBootstrapAscii(console, serial, 0x0000000A0D323030, 5);
    }

    private static bool TryParseMcfgSegments(
        byte* table,
        int tableLength,
        ulong* segmentScratch,
        out ulong status)
    {
        status = 0;
        if (tableLength < AcpiMcfgAllocationOffset + AcpiMcfgAllocationLength)
        {
            status = 50;
            return false;
        }

        for (int index = 0; index < 8; index++)
        {
            if (table[AcpiSdtHeaderLength + index] != 0)
            {
                status = 51;
                return false;
            }
        }

        int allocationBytes = tableLength - AcpiMcfgAllocationOffset;
        if ((allocationBytes % AcpiMcfgAllocationLength) != 0)
        {
            status = 52;
            return false;
        }

        int segmentCount = allocationBytes / AcpiMcfgAllocationLength;
        if (segmentCount == 0 || segmentCount > AcpiEcamSegmentScratchCapacity)
        {
            status = 53;
            return false;
        }

        for (int index = 0; index < segmentCount; index++)
        {
            byte* entry = table + AcpiMcfgAllocationOffset + (index * AcpiMcfgAllocationLength);
            byte startBus = entry[10];
            byte endBus = entry[11];
            if (startBus > endBus)
            {
                status = 54;
                return false;
            }

            int slot = index * 4;
            segmentScratch[slot] = ReadUInt64LittleEndian(entry);
            segmentScratch[slot + 1] = ReadUInt16LittleEndian(entry + 8);
            segmentScratch[slot + 2] = startBus;
            segmentScratch[slot + 3] = endBus;
        }

        return true;
    }

    private static bool IsAcpiPhysicalRangeAllowed(ulong physicalAddress, ulong byteLength)
    {
        if (!s_memoryRegionMaterialization.Succeeded ||
            s_memoryRegionMaterialization.RegionCount == 0 ||
            s_memoryRegionMaterialization.RegionCount > OwnedMemoryRegionCapacity ||
            !AcpiRange(physicalAddress, byteLength))
        {
            return false;
        }

        ulong endExclusive = physicalAddress + byteLength;
        if (endExclusive <= physicalAddress)
        {
            return false;
        }

        fixed (ulong* regionSlots = &s_ownedMemoryRegions.Slots[0])
        {
            BootMemoryRegionRecord* regions = (BootMemoryRegionRecord*)regionSlots;
            for (ulong regionIndex = 0; regionIndex < s_memoryRegionMaterialization.RegionCount; regionIndex++)
            {
                BootMemoryRegionRecord region = regions[(int)regionIndex];
                if ((region.UefiType == UefiAcpiReclaimMemoryType || region.UefiType == UefiAcpiMemoryNvsType)
                    && region.PhysicalStart != 0 && region.PhysicalEndExclusive > region.PhysicalStart
                    && physicalAddress >= region.PhysicalStart
                    && endExclusive <= region.PhysicalEndExclusive)
                {
                    return true;
                }
            }
        }

        return false;
    }

    private static bool HasRsdpSignature(byte* source)
    {
        return source[0] == (byte)'R'
            && source[1] == (byte)'S'
            && source[2] == (byte)'D'
            && source[3] == (byte)' '
            && source[4] == (byte)'P'
            && source[5] == (byte)'T'
            && source[6] == (byte)'R'
            && source[7] == (byte)' ';
    }

    private static bool HasZeroOemId(byte* source)
    {
        for (int index = 0; index < 6; index++)
        {
            if (source[9 + index] != 0)
            {
                return false;
            }
        }

        return true;
    }

    private static bool IsChecksumValid(byte* source, int byteCount)
    {
        uint sum = 0;
        for (int index = 0; index < byteCount; index++)
        {
            sum += source[index];
        }

        return (sum & 0xff) == 0;
    }

    private static ushort ReadUInt16LittleEndian(byte* source)
    {
        return (ushort)(source[0] | (source[1] << 8));
    }

    private static uint ReadUInt32LittleEndian(byte* source)
    {
        return source[0]
            | ((uint)source[1] << 8)
            | ((uint)source[2] << 16)
            | ((uint)source[3] << 24);
    }

    private static ulong ReadUInt64LittleEndian(byte* source)
    {
        return (ulong)source[0]
            | ((ulong)source[1] << 8)
            | ((ulong)source[2] << 16)
            | ((ulong)source[3] << 24)
            | ((ulong)source[4] << 32)
            | ((ulong)source[5] << 40)
            | ((ulong)source[6] << 48)
            | ((ulong)source[7] << 56);
    }

    private static void WriteAcpiRsdpSnapshotReady(EfiSimpleTextOutputProtocol* console, EfiSerialIoProtocol* serial)
    {
        WriteAcpiPrefix(console, serial);
        WriteRsdp(console, serial);
        WriteUnderscore(console, serial);
        WriteSnapshotReady(console, serial);
        WriteCrLf(console, serial);
    }

    private static void WriteAcpiRsdpValidated(EfiSimpleTextOutputProtocol* console, EfiSerialIoProtocol* serial)
    {
        WriteAcpiPrefix(console, serial);
        WriteRsdp(console, serial);
        WriteUnderscore(console, serial);
        WriteValidated(console, serial);
        WriteCrLf(console, serial);
    }

    private static void WriteAcpiRootSdtSnapshotReady(EfiSimpleTextOutputProtocol* console, EfiSerialIoProtocol* serial)
    {
        WriteAcpiPrefix(console, serial);
        WriteRootSdt(console, serial);
        WriteUnderscore(console, serial);
        WriteSnapshotReady(console, serial);
        WriteCrLf(console, serial);
    }

    private static void WriteAcpiRootSdtValidated(EfiSimpleTextOutputProtocol* console, EfiSerialIoProtocol* serial)
    {
        WriteAcpiPrefix(console, serial);
        WriteRootSdt(console, serial);
        WriteUnderscore(console, serial);
        WriteValidated(console, serial);
        WriteCrLf(console, serial);
    }

    private static void WriteAcpiMcfgDiscoveryReady(EfiSimpleTextOutputProtocol* console, EfiSerialIoProtocol* serial)
    {
        WriteAcpiPrefix(console, serial);
        Write(console, serial, (byte)'M');
        Write(console, serial, (byte)'C');
        Write(console, serial, (byte)'F');
        Write(console, serial, (byte)'G');
        WriteUnderscore(console, serial);
        Write(console, serial, (byte)'D');
        Write(console, serial, (byte)'I');
        Write(console, serial, (byte)'S');
        Write(console, serial, (byte)'C');
        Write(console, serial, (byte)'O');
        Write(console, serial, (byte)'V');
        Write(console, serial, (byte)'E');
        Write(console, serial, (byte)'R');
        Write(console, serial, (byte)'Y');
        WriteUnderscore(console, serial);
        WriteReady(console, serial);
        WriteCrLf(console, serial);
    }

    private static void WriteAcpiDiscoveryFail(
        EfiSimpleTextOutputProtocol* console,
        EfiSerialIoProtocol* serial,
        ulong stage,
        ulong status)
    {
        WriteAcpiPrefix(console, serial);
        Write(console, serial, (byte)'D');
        Write(console, serial, (byte)'I');
        Write(console, serial, (byte)'S');
        Write(console, serial, (byte)'C');
        Write(console, serial, (byte)'O');
        Write(console, serial, (byte)'V');
        Write(console, serial, (byte)'E');
        Write(console, serial, (byte)'R');
        Write(console, serial, (byte)'Y');
        WriteUnderscore(console, serial);
        Write(console, serial, (byte)'F');
        Write(console, serial, (byte)'A');
        Write(console, serial, (byte)'I');
        Write(console, serial, (byte)'L');
        Write(console, serial, (byte)' ');
        WriteUnsignedDecimal(console, serial, stage);
        Write(console, serial, (byte)' ');
        WriteUnsignedDecimal(console, serial, status);
        WriteCrLf(console, serial);
    }

    private static void WriteAcpiPrefix(EfiSimpleTextOutputProtocol* console, EfiSerialIoProtocol* serial)
    {
        Write(console, serial, (byte)'A');
        Write(console, serial, (byte)'C');
        Write(console, serial, (byte)'P');
        Write(console, serial, (byte)'I');
        WriteUnderscore(console, serial);
    }

    private static void WriteRsdp(EfiSimpleTextOutputProtocol* console, EfiSerialIoProtocol* serial)
    {
        Write(console, serial, (byte)'R');
        Write(console, serial, (byte)'S');
        Write(console, serial, (byte)'D');
        Write(console, serial, (byte)'P');
    }

    private static void WriteRootSdt(EfiSimpleTextOutputProtocol* console, EfiSerialIoProtocol* serial)
    {
        Write(console, serial, (byte)'R');
        Write(console, serial, (byte)'O');
        Write(console, serial, (byte)'O');
        Write(console, serial, (byte)'T');
        WriteUnderscore(console, serial);
        Write(console, serial, (byte)'S');
        Write(console, serial, (byte)'D');
        Write(console, serial, (byte)'T');
    }

    private static void WriteSnapshotReady(EfiSimpleTextOutputProtocol* console, EfiSerialIoProtocol* serial)
    {
        Write(console, serial, (byte)'S');
        Write(console, serial, (byte)'N');
        Write(console, serial, (byte)'A');
        Write(console, serial, (byte)'P');
        Write(console, serial, (byte)'S');
        Write(console, serial, (byte)'H');
        Write(console, serial, (byte)'O');
        Write(console, serial, (byte)'T');
        WriteUnderscore(console, serial);
        WriteReady(console, serial);
    }

    private static void WriteValidated(EfiSimpleTextOutputProtocol* console, EfiSerialIoProtocol* serial)
    {
        Write(console, serial, (byte)'V');
        Write(console, serial, (byte)'A');
        Write(console, serial, (byte)'L');
        Write(console, serial, (byte)'I');
        Write(console, serial, (byte)'D');
        Write(console, serial, (byte)'A');
        Write(console, serial, (byte)'T');
        Write(console, serial, (byte)'E');
        Write(console, serial, (byte)'D');
    }

    private static void WriteReady(EfiSimpleTextOutputProtocol* console, EfiSerialIoProtocol* serial)
    {
        Write(console, serial, (byte)'R');
        Write(console, serial, (byte)'E');
        Write(console, serial, (byte)'A');
        Write(console, serial, (byte)'D');
        Write(console, serial, (byte)'Y');
    }

    private static void WriteUnderscore(EfiSimpleTextOutputProtocol* console, EfiSerialIoProtocol* serial)
    {
        Write(console, serial, (byte)'_');
    }
#endif
    private static bool WriteExitBootServicesFreshMapProbe(EfiSimpleTextOutputProtocol* console, EfiSerialIoProtocol* serial, EfiSystemTable* systemTable)
    {
        WriteUefiEbsFreshMapProbeStart(console, serial);
        if (!TryCaptureUefiMemoryMap(systemTable))
        {
            WriteUefiEbsFreshMapFail(console, serial, (ulong)s_memoryMapCopyStatus);
            return false;
        }

        EfiMemoryMapNormalizeResult normalizeResult = EfiMemoryMapNormalizer.Normalize(s_memoryMapSnapshot.Buffer, s_memoryMapSnapshot.MapSize, s_memoryMapSnapshot.DescriptorSize, s_memoryMapSnapshot.DescriptorVersion, s_memoryMapSnapshot.DescriptorCount);
        if (!normalizeResult.Succeeded)
        {
            WriteUefiEbsFreshMapFail(console, serial, (ulong)normalizeResult.Status);
            return false;
        }

        UefiExitBootServicesPlanResult planResult = UefiExitBootServicesPlan.Evaluate(s_bootHandoff, freshMemoryMapCaptured: true, UefiExitBootServicesAttemptOutcome.ExitCallSkipped, attemptIndex: 0, maximumRetryCount: UefiExitBootServicesPlan.DefaultMaximumRetryCount);
        if (planResult.Status != UefiExitBootServicesPlanStatus.ExitCallSkipped)
        {
            WriteUefiEbsFreshMapFail(console, serial, (ulong)planResult.Status);
            return false;
        }

        WriteUefiEbsFreshMapReady(console, serial);
        WriteUefiEbsCallSkipped(console, serial);
        return true;
    }

    private static void WriteUefiEbsFreshMapProbeStart(EfiSimpleTextOutputProtocol* console, EfiSerialIoProtocol* serial)
    {
        Write(console, serial, (byte)'U');
        Write(console, serial, (byte)'E');
        Write(console, serial, (byte)'F');
        Write(console, serial, (byte)'I');
        Write(console, serial, (byte)'_');
        Write(console, serial, (byte)'E');
        Write(console, serial, (byte)'B');
        Write(console, serial, (byte)'S');
        Write(console, serial, (byte)'_');
        Write(console, serial, (byte)'F');
        Write(console, serial, (byte)'R');
        Write(console, serial, (byte)'E');
        Write(console, serial, (byte)'S');
        Write(console, serial, (byte)'H');
        Write(console, serial, (byte)'_');
        Write(console, serial, (byte)'M');
        Write(console, serial, (byte)'A');
        Write(console, serial, (byte)'P');
        Write(console, serial, (byte)'_');
        Write(console, serial, (byte)'P');
        Write(console, serial, (byte)'R');
        Write(console, serial, (byte)'O');
        Write(console, serial, (byte)'B');
        Write(console, serial, (byte)'E');
        Write(console, serial, (byte)'_');
        Write(console, serial, (byte)'S');
        Write(console, serial, (byte)'T');
        Write(console, serial, (byte)'A');
        Write(console, serial, (byte)'R');
        Write(console, serial, (byte)'T');
        WriteCrLf(console, serial);
    }

    private static void WriteUefiEbsFreshMapReady(EfiSimpleTextOutputProtocol* console, EfiSerialIoProtocol* serial)
    {
        Write(console, serial, (byte)'U');
        Write(console, serial, (byte)'E');
        Write(console, serial, (byte)'F');
        Write(console, serial, (byte)'I');
        Write(console, serial, (byte)'_');
        Write(console, serial, (byte)'E');
        Write(console, serial, (byte)'B');
        Write(console, serial, (byte)'S');
        Write(console, serial, (byte)'_');
        Write(console, serial, (byte)'F');
        Write(console, serial, (byte)'R');
        Write(console, serial, (byte)'E');
        Write(console, serial, (byte)'S');
        Write(console, serial, (byte)'H');
        Write(console, serial, (byte)'_');
        Write(console, serial, (byte)'M');
        Write(console, serial, (byte)'A');
        Write(console, serial, (byte)'P');
        Write(console, serial, (byte)'_');
        Write(console, serial, (byte)'R');
        Write(console, serial, (byte)'E');
        Write(console, serial, (byte)'A');
        Write(console, serial, (byte)'D');
        Write(console, serial, (byte)'Y');
        WriteCrLf(console, serial);
    }

    private static void WriteUefiEbsFreshMapFail(EfiSimpleTextOutputProtocol* console, EfiSerialIoProtocol* serial, ulong status)
    {
        Write(console, serial, (byte)'U');
        Write(console, serial, (byte)'E');
        Write(console, serial, (byte)'F');
        Write(console, serial, (byte)'I');
        Write(console, serial, (byte)'_');
        Write(console, serial, (byte)'E');
        Write(console, serial, (byte)'B');
        Write(console, serial, (byte)'S');
        Write(console, serial, (byte)'_');
        Write(console, serial, (byte)'F');
        Write(console, serial, (byte)'R');
        Write(console, serial, (byte)'E');
        Write(console, serial, (byte)'S');
        Write(console, serial, (byte)'H');
        Write(console, serial, (byte)'_');
        Write(console, serial, (byte)'M');
        Write(console, serial, (byte)'A');
        Write(console, serial, (byte)'P');
        Write(console, serial, (byte)'_');
        Write(console, serial, (byte)'F');
        Write(console, serial, (byte)'A');
        Write(console, serial, (byte)'I');
        Write(console, serial, (byte)'L');
        Write(console, serial, (byte)' ');
        WriteUnsignedDecimal(console, serial, status);
        WriteCrLf(console, serial);
    }

    private static void WriteUefiEbsCallSkipped(EfiSimpleTextOutputProtocol* console, EfiSerialIoProtocol* serial)
    {
        Write(console, serial, (byte)'U');
        Write(console, serial, (byte)'E');
        Write(console, serial, (byte)'F');
        Write(console, serial, (byte)'I');
        Write(console, serial, (byte)'_');
        Write(console, serial, (byte)'E');
        Write(console, serial, (byte)'B');
        Write(console, serial, (byte)'S');
        Write(console, serial, (byte)'_');
        Write(console, serial, (byte)'C');
        Write(console, serial, (byte)'A');
        Write(console, serial, (byte)'L');
        Write(console, serial, (byte)'L');
        Write(console, serial, (byte)'_');
        Write(console, serial, (byte)'S');
        Write(console, serial, (byte)'K');
        Write(console, serial, (byte)'I');
        Write(console, serial, (byte)'P');
        Write(console, serial, (byte)'P');
        Write(console, serial, (byte)'E');
        Write(console, serial, (byte)'D');
        WriteCrLf(console, serial);
    }

    private static void WriteUefiMemoryMapNormalizeFail(EfiSimpleTextOutputProtocol* console, EfiSerialIoProtocol* serial, EfiMemoryMapNormalizeStatus status)
    {
        Write(console, serial, (byte)'U');
        Write(console, serial, (byte)'E');
        Write(console, serial, (byte)'F');
        Write(console, serial, (byte)'I');
        Write(console, serial, (byte)'_');
        Write(console, serial, (byte)'M');
        Write(console, serial, (byte)'E');
        Write(console, serial, (byte)'M');
        Write(console, serial, (byte)'O');
        Write(console, serial, (byte)'R');
        Write(console, serial, (byte)'Y');
        Write(console, serial, (byte)'_');
        Write(console, serial, (byte)'M');
        Write(console, serial, (byte)'A');
        Write(console, serial, (byte)'P');
        Write(console, serial, (byte)'_');
        Write(console, serial, (byte)'N');
        Write(console, serial, (byte)'O');
        Write(console, serial, (byte)'R');
        Write(console, serial, (byte)'M');
        Write(console, serial, (byte)'A');
        Write(console, serial, (byte)'L');
        Write(console, serial, (byte)'I');
        Write(console, serial, (byte)'Z');
        Write(console, serial, (byte)'E');
        Write(console, serial, (byte)'_');
        Write(console, serial, (byte)'F');
        Write(console, serial, (byte)'A');
        Write(console, serial, (byte)'I');
        Write(console, serial, (byte)'L');
        Write(console, serial, (byte)' ');
        WriteUnsignedDecimal(console, serial, (ulong)status);
        WriteCrLf(console, serial);
    }

    private static void WriteBootHandoffReady(EfiSimpleTextOutputProtocol* console, EfiSerialIoProtocol* serial)
    {
        Write(console, serial, (byte)'B');
        Write(console, serial, (byte)'O');
        Write(console, serial, (byte)'O');
        Write(console, serial, (byte)'T');
        Write(console, serial, (byte)'_');
        Write(console, serial, (byte)'H');
        Write(console, serial, (byte)'A');
        Write(console, serial, (byte)'N');
        Write(console, serial, (byte)'D');
        Write(console, serial, (byte)'O');
        Write(console, serial, (byte)'F');
        Write(console, serial, (byte)'F');
        Write(console, serial, (byte)'_');
        Write(console, serial, (byte)'R');
        Write(console, serial, (byte)'E');
        Write(console, serial, (byte)'A');
        Write(console, serial, (byte)'D');
        Write(console, serial, (byte)'Y');
        WriteCrLf(console, serial);
    }

    private static void WriteBootHandoffFail(EfiSimpleTextOutputProtocol* console, EfiSerialIoProtocol* serial, EfiBootHandoffStatus status)
    {
        Write(console, serial, (byte)'B');
        Write(console, serial, (byte)'O');
        Write(console, serial, (byte)'O');
        Write(console, serial, (byte)'T');
        Write(console, serial, (byte)'_');
        Write(console, serial, (byte)'H');
        Write(console, serial, (byte)'A');
        Write(console, serial, (byte)'N');
        Write(console, serial, (byte)'D');
        Write(console, serial, (byte)'O');
        Write(console, serial, (byte)'F');
        Write(console, serial, (byte)'F');
        Write(console, serial, (byte)'_');
        Write(console, serial, (byte)'F');
        Write(console, serial, (byte)'A');
        Write(console, serial, (byte)'I');
        Write(console, serial, (byte)'L');
        Write(console, serial, (byte)' ');
        WriteUnsignedDecimal(console, serial, (ulong)status);
        WriteCrLf(console, serial);
    }

    private static void WriteUefiMemoryMapCopyFail(EfiSimpleTextOutputProtocol* console, EfiSerialIoProtocol* serial, UefiMemoryMapCopyPlanStatus status)
    {
        Write(console, serial, (byte)'U');
        Write(console, serial, (byte)'E');
        Write(console, serial, (byte)'F');
        Write(console, serial, (byte)'I');
        Write(console, serial, (byte)'_');
        Write(console, serial, (byte)'M');
        Write(console, serial, (byte)'E');
        Write(console, serial, (byte)'M');
        Write(console, serial, (byte)'O');
        Write(console, serial, (byte)'R');
        Write(console, serial, (byte)'Y');
        Write(console, serial, (byte)'_');
        Write(console, serial, (byte)'M');
        Write(console, serial, (byte)'A');
        Write(console, serial, (byte)'P');
        Write(console, serial, (byte)'_');
        Write(console, serial, (byte)'C');
        Write(console, serial, (byte)'O');
        Write(console, serial, (byte)'P');
        Write(console, serial, (byte)'Y');
        Write(console, serial, (byte)'_');
        Write(console, serial, (byte)'F');
        Write(console, serial, (byte)'A');
        Write(console, serial, (byte)'I');
        Write(console, serial, (byte)'L');
        Write(console, serial, (byte)' ');
        WriteUnsignedDecimal(console, serial, (ulong)status);
        WriteCrLf(console, serial);
    }

    private static void WriteM01InstructionPublicationCookieFailure(EfiSimpleTextOutputProtocol* console, EfiSerialIoProtocol* serial)
    {
        WriteM01InstructionPublicationAscii(console, serial, (byte)'M', (byte)'0', (byte)'1', (byte)'_', (byte)'I', (byte)'N', (byte)'S', (byte)'T');
        WriteM01InstructionPublicationAscii(console, serial, (byte)'R', (byte)'U', (byte)'C', (byte)'T', (byte)'I', (byte)'O', (byte)'N', (byte)'_');
        WriteM01InstructionPublicationAscii(console, serial, (byte)'P', (byte)'U', (byte)'B', (byte)'L', (byte)'I', (byte)'C', (byte)'A', (byte)'T');
        WriteM01InstructionPublicationAscii(console, serial, (byte)'I', (byte)'O', (byte)'N', (byte)'_', (byte)'C', (byte)'O', (byte)'O', (byte)'K');
        WriteM01InstructionPublicationAscii(console, serial, (byte)'I', (byte)'E', (byte)'_', (byte)'F', (byte)'A', (byte)'I', (byte)'L', (byte)'E');
        WriteM01InstructionPublicationAscii(console, serial, (byte)'D', 0, 0, 0, 0, 0, 0, 0);
        WriteCrLf(console, serial);
    }

    private static void WriteM01InstructionPublicationEpochsVerified(EfiSimpleTextOutputProtocol* console, EfiSerialIoProtocol* serial)
    {
        WriteM01InstructionPublicationAscii(console, serial, (byte)'M', (byte)'0', (byte)'1', (byte)'_', (byte)'I', (byte)'N', (byte)'S', (byte)'T');
        WriteM01InstructionPublicationAscii(console, serial, (byte)'R', (byte)'U', (byte)'C', (byte)'T', (byte)'I', (byte)'O', (byte)'N', (byte)'_');
        WriteM01InstructionPublicationAscii(console, serial, (byte)'P', (byte)'U', (byte)'B', (byte)'L', (byte)'I', (byte)'C', (byte)'A', (byte)'T');
        WriteM01InstructionPublicationAscii(console, serial, (byte)'I', (byte)'O', (byte)'N', (byte)'_', (byte)'E', (byte)'P', (byte)'O', (byte)'C');
        WriteM01InstructionPublicationAscii(console, serial, (byte)'H', (byte)'S', (byte)'_', (byte)'V', (byte)'E', (byte)'R', (byte)'I', (byte)'F');
        WriteM01InstructionPublicationAscii(console, serial, (byte)'I', (byte)'E', (byte)'D', 0, 0, 0, 0, 0);
        WriteCrLf(console, serial);
    }

    private static void WriteM01InstructionPublicationAscii(EfiSimpleTextOutputProtocol* console, EfiSerialIoProtocol* serial, byte b0, byte b1, byte b2, byte b3, byte b4, byte b5, byte b6, byte b7)
    {
        if (b0 != 0)
            Write(console, serial, b0);
        if (b1 != 0)
            Write(console, serial, b1);
        if (b2 != 0)
            Write(console, serial, b2);
        if (b3 != 0)
            Write(console, serial, b3);
        if (b4 != 0)
            Write(console, serial, b4);
        if (b5 != 0)
            Write(console, serial, b5);
        if (b6 != 0)
            Write(console, serial, b6);
        if (b7 != 0)
            Write(console, serial, b7);
    }

    private static void WriteBootSequencePlan(EfiSimpleTextOutputProtocol* console, EfiSerialIoProtocol* serial)
    {
        int count = VerifiedExitPathOptDefaultRepeatedCleanQEMU.Enabled ? BootSequencePlan.NonSmokeLineCount : BootSequencePlan.LineCount;
        for (int lineIndex = 0; lineIndex < count; lineIndex++)
        {
            int length = BootSequencePlan.GetLineLength(lineIndex);
            for (int offset = 0; offset < length; offset++)
            {
                Write(console, serial, BootSequencePlan.GetLineByte(lineIndex, offset));
            }

            WriteCrLf(console, serial);
        }
    }

#if UEFI_GONE_SIMULATION_PROBE
    private static void WriteUefiGoneSimulation(EfiSimpleTextOutputProtocol* console, EfiSerialIoProtocol* serial)
    {
        WriteUefiGoneSimulationStart(console, serial);
        WriteUefiGoneSimulationPlatformDenied(console, serial);
        WritePostFirmwareDiagnosticsReady(console, serial);
        WriteUefiGoneSimulationPass(console, serial);
    }

    private static void WriteUefiGoneSimulationStart(EfiSimpleTextOutputProtocol* console, EfiSerialIoProtocol* serial)
    {
        Write(console, serial, (byte)'U');
        Write(console, serial, (byte)'E');
        Write(console, serial, (byte)'F');
        Write(console, serial, (byte)'I');
        Write(console, serial, (byte)'_');
        Write(console, serial, (byte)'G');
        Write(console, serial, (byte)'O');
        Write(console, serial, (byte)'N');
        Write(console, serial, (byte)'E');
        Write(console, serial, (byte)'_');
        Write(console, serial, (byte)'S');
        Write(console, serial, (byte)'I');
        Write(console, serial, (byte)'M');
        Write(console, serial, (byte)'_');
        Write(console, serial, (byte)'S');
        Write(console, serial, (byte)'T');
        Write(console, serial, (byte)'A');
        Write(console, serial, (byte)'R');
        Write(console, serial, (byte)'T');
        WriteCrLf(console, serial);
    }

    private static void WriteUefiGoneSimulationPlatformDenied(EfiSimpleTextOutputProtocol* console, EfiSerialIoProtocol* serial)
    {
        Write(console, serial, (byte)'U');
        Write(console, serial, (byte)'E');
        Write(console, serial, (byte)'F');
        Write(console, serial, (byte)'I');
        Write(console, serial, (byte)'_');
        Write(console, serial, (byte)'G');
        Write(console, serial, (byte)'O');
        Write(console, serial, (byte)'N');
        Write(console, serial, (byte)'E');
        Write(console, serial, (byte)'_');
        Write(console, serial, (byte)'S');
        Write(console, serial, (byte)'I');
        Write(console, serial, (byte)'M');
        Write(console, serial, (byte)'_');
        Write(console, serial, (byte)'P');
        Write(console, serial, (byte)'L');
        Write(console, serial, (byte)'A');
        Write(console, serial, (byte)'T');
        Write(console, serial, (byte)'F');
        Write(console, serial, (byte)'O');
        Write(console, serial, (byte)'R');
        Write(console, serial, (byte)'M');
        Write(console, serial, (byte)'_');
        Write(console, serial, (byte)'D');
        Write(console, serial, (byte)'E');
        Write(console, serial, (byte)'N');
        Write(console, serial, (byte)'I');
        Write(console, serial, (byte)'E');
        Write(console, serial, (byte)'D');
        WriteCrLf(console, serial);
    }

    private static void WritePostFirmwareDiagnosticsReady(EfiSimpleTextOutputProtocol* console, EfiSerialIoProtocol* serial)
    {
        Write(console, serial, (byte)'P');
        Write(console, serial, (byte)'O');
        Write(console, serial, (byte)'S');
        Write(console, serial, (byte)'T');
        Write(console, serial, (byte)'_');
        Write(console, serial, (byte)'F');
        Write(console, serial, (byte)'I');
        Write(console, serial, (byte)'R');
        Write(console, serial, (byte)'M');
        Write(console, serial, (byte)'W');
        Write(console, serial, (byte)'A');
        Write(console, serial, (byte)'R');
        Write(console, serial, (byte)'E');
        Write(console, serial, (byte)'_');
        Write(console, serial, (byte)'D');
        Write(console, serial, (byte)'I');
        Write(console, serial, (byte)'A');
        Write(console, serial, (byte)'G');
        Write(console, serial, (byte)'N');
        Write(console, serial, (byte)'O');
        Write(console, serial, (byte)'S');
        Write(console, serial, (byte)'T');
        Write(console, serial, (byte)'I');
        Write(console, serial, (byte)'C');
        Write(console, serial, (byte)'S');
        Write(console, serial, (byte)'_');
        Write(console, serial, (byte)'R');
        Write(console, serial, (byte)'E');
        Write(console, serial, (byte)'A');
        Write(console, serial, (byte)'D');
        Write(console, serial, (byte)'Y');
        WriteCrLf(console, serial);
    }

    private static void WriteUefiGoneSimulationPass(EfiSimpleTextOutputProtocol* console, EfiSerialIoProtocol* serial)
    {
        Write(console, serial, (byte)'U');
        Write(console, serial, (byte)'E');
        Write(console, serial, (byte)'F');
        Write(console, serial, (byte)'I');
        Write(console, serial, (byte)'_');
        Write(console, serial, (byte)'G');
        Write(console, serial, (byte)'O');
        Write(console, serial, (byte)'N');
        Write(console, serial, (byte)'E');
        Write(console, serial, (byte)'_');
        Write(console, serial, (byte)'S');
        Write(console, serial, (byte)'I');
        Write(console, serial, (byte)'M');
        Write(console, serial, (byte)'_');
        Write(console, serial, (byte)'P');
        Write(console, serial, (byte)'A');
        Write(console, serial, (byte)'S');
        Write(console, serial, (byte)'S');
        WriteCrLf(console, serial);
    }
#endif
#if M03_MEMORY_MAP_CAPTURE_PROBE

#endif
    private static bool TryCaptureUefiMemoryMap(EfiSystemTable* systemTable)
    {
        s_memoryMapCopyStatus = UefiMemoryMapCopyPlanStatus.Success;
        if (systemTable == null || systemTable->BootServices == null)
        {
            return false;
        }

        EfiBootServices* bootServices = systemTable->BootServices;
        if (bootServices->GetMemoryMap == null || bootServices->AllocatePool == null)
        {
            return false;
        }

        UefiMemoryMapCaptureResult captureResult;
        fixed (byte* ownedMemoryMapBytes = &s_ownedMemoryMap.Bytes[0])
        {
#if M03_MEMORY_MAP_CAPTURE_PROBE
            fixed (OwnedMemoryMapBuffer* guarded = &s_ownedMemoryMap)
            {
                if (!M03MemoryMapCaptureObserver.Prepare(bootServices, guarded, ownedMemoryMapBytes))
                    return false;
            }
#endif
            captureResult = UefiMemoryMapCapture.Capture(
#if M03_MEMORY_MAP_CAPTURE_PROBE
                &M03MemoryMapCaptureObserver.ForwardGetMemoryMap,
                &M03MemoryMapCaptureObserver.ForwardAllocatePool,
#else
            bootServices->GetMemoryMap, bootServices->AllocatePool,
#endif
            ownedMemoryMapBytes, OwnedMemoryMapBufferSize);
#if M03_MEMORY_MAP_CAPTURE_PROBE
            if (!M03MemoryMapCaptureObserver.ReadBack(captureResult))
            {
                s_memoryMapCopyStatus = captureResult.CopyPlanStatus;
                return false;
            }
#endif
        }

        if (!captureResult.Succeeded)
        {
            s_memoryMapCopyStatus = captureResult.CopyPlanStatus;
            return false;
        }

        FirmwareMemoryMapRecord sourceRecord = captureResult.SourceRecord;
        FirmwareMemoryMapRecord ownedRecord = captureResult.OwnedRecord;
        s_memoryMapSnapshot.Buffer = (void*)ownedRecord.BufferAddress;
        s_memoryMapSnapshot.BufferSize = ownedRecord.BufferSize;
        s_memoryMapSnapshot.SourceBuffer = (void*)sourceRecord.BufferAddress;
        s_memoryMapSnapshot.SourceBufferSize = sourceRecord.BufferSize;
        s_memoryMapSnapshot.MapSize = ownedRecord.MapSize;
        s_memoryMapSnapshot.MapKey = ownedRecord.MapKey;
        s_memoryMapSnapshot.DescriptorSize = ownedRecord.DescriptorSize;
        s_memoryMapSnapshot.DescriptorVersion = ownedRecord.DescriptorVersion;
        s_memoryMapSnapshot.DescriptorCount = ownedRecord.DescriptorCount;
        s_memoryMapSnapshot.IsOwned = true;
        s_bootHandoff = BootHandoffRecord.CreateUefiWithOwnedMemoryMap(sourceRecord.BufferAddress, sourceRecord.BufferSize, ownedRecord.BufferAddress, ownedRecord.BufferSize, ownedRecord.MapSize, ownedRecord.MapKey, ownedRecord.DescriptorSize, ownedRecord.DescriptorVersion, s_memoryMapSnapshot.DescriptorCount);
#if M03_MEMORY_MAP_CAPTURE_PROBE
        if (!M03MemoryMapCaptureObserver.Handoff(s_memoryMapSnapshot, s_bootHandoff))
            return false;
#endif
        return s_memoryMapSnapshot.DescriptorCount != 0;
    }

#if (M01_LOADED_IMAGE_STACK_PROBE || CLROS_NORMAL_VERIFIED_EXIT)
    private static byte s_m01InstructionPublicationInitialized;
    private static byte s_m01InstructionPublicationEpochMask;

#if M04_RHP_NEW_FAST_PROBE || M02_UNEXPECTED_VECTOR_PANIC_PROBE || M04_TYPE_MANAGER_MODULE_REGISTRATION_PROBE || (M03_FIRST_REAL_EXIT_BOOT_SERVICES_PROBE || CLROS_NORMAL_VERIFIED_EXIT)
#if (M03_FIRST_REAL_EXIT_BOOT_SERVICES_PROBE || CLROS_NORMAL_VERIFIED_EXIT)

#endif

#if M04_TYPE_MANAGER_MODULE_REGISTRATION_PROBE || M04_GC_STATIC_ROOT_MATERIALIZATION_PROBE || (M03_FIRST_REAL_EXIT_BOOT_SERVICES_PROBE || CLROS_NORMAL_VERIFIED_EXIT)

#endif

#if M04_RHP_NEW_FAST_PROBE

#endif
#endif

#if M04_CHECKED_REFERENCE_BARRIER_PROBE || M04_GC_STATIC_ROOT_MATERIALIZATION_PROBE || M04_X64_UNWIND_METADATA_PROBE

#endif

#if (M01_LIVE_ALLOCATOR_PROBE || CLROS_NORMAL_VERIFIED_EXIT)

#endif

#if M04_MANAGED_HEAP_PAGE_RANGE_PROBE

#endif
#if M04_RHP_NEW_FAST_PROBE

#endif
#if M04_TYPE_MANAGER_MODULE_REGISTRATION_PROBE

#endif

#if (M03_FIRST_REAL_EXIT_BOOT_SERVICES_PROBE || CLROS_NORMAL_VERIFIED_EXIT)

#endif
#if M02_PAGE_FAULT_CR2_ERROR_CODE_PROBE

#endif
#if M02_DOUBLE_FAULT_IST_PROBE

#endif
#if M02_UNEXPECTED_VECTOR_PANIC_PROBE

#endif
#if M02_UD2_VECTOR_STATE_PROBE

#endif
#if M02_OWNED_GDT_TSS_TERMINAL_PROBE

#endif

#else
#endif
#if (M01_LIVE_ALLOCATOR_PROBE || CLROS_NORMAL_VERIFIED_EXIT)

#if (M03_FIRST_REAL_EXIT_BOOT_SERVICES_PROBE || CLROS_NORMAL_VERIFIED_EXIT)

#if M16_002_PROBE

#endif
#endif
#if M04_MANAGED_HEAP_PAGE_RANGE_PROBE

#if M04_RHP_NEW_FAST_PROBE

#if M04_GC_STATIC_ROOT_MATERIALIZATION_PROBE

#else

#endif

#endif
#endif

#if M01_OWNED_PAGING_PROBE

#endif

#if M04_MANAGED_HEAP_PAGE_RANGE_PROBE

#endif

#else
#endif
#if M04_RHP_NEW_FAST_PROBE
    internal static void WriteRhpByte(byte value) => M01StageOneAdapter.WriteRhpByte(value);
#endif
#if M04_CHECKED_REFERENCE_BARRIER_PROBE || M04_GC_STATIC_ROOT_MATERIALIZATION_PROBE || M04_X64_UNWIND_METADATA_PROBE
    internal static bool TryGetOriginalReferenceStack(out ulong authority,
        out ulong start, out ulong end, out ulong initialRsp) =>
        M01StageOneAdapter.TryGetOriginalReferenceStack(out authority, out start, out end, out initialRsp);
#endif
    private static void WriteCrLf(EfiSimpleTextOutputProtocol* console, EfiSerialIoProtocol* serial)
    {
        EfiConsoleAdapter adapter = new EfiConsoleAdapter(console, serial);
        adapter.WriteCrLf();
    }

    private static void Write(EfiSimpleTextOutputProtocol* console, EfiSerialIoProtocol* serial, byte value)
    {
        EfiConsoleAdapter adapter = new EfiConsoleAdapter(console, serial);
        adapter.WriteByte(value);
    }

#if M04_MANAGED_HEAP_PAGE_RANGE_PROBE

#endif
    private static void WriteUnsignedDecimal(EfiSimpleTextOutputProtocol* console, EfiSerialIoProtocol* serial, ulong value)
    {
        if (value >= 10)
        {
            WriteUnsignedDecimal(console, serial, value / 10);
        }

        Write(console, serial, (byte)((value % 10) + (byte)'0'));
    }
}
#if M03_MEMORY_MAP_CAPTURE_PROBE

#endif
#if M03_MEMORY_MAP_CAPTURE_PROBE

#endif
#if (ACPI_PHYSICAL_DISCOVERY_PROBE || CLROS_NORMAL_VERIFIED_EXIT)

#endif
#pragma warning restore 649
