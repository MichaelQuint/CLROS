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
#if (ACPI_PHYSICAL_DISCOVERY_PROBE || CLROS_NORMAL_VERIFIED_EXIT)

#endif
#if M02_OWNED_GDT_TSS_TERMINAL_PROBE

#endif
#if M02_UD2_VECTOR_STATE_PROBE

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
#if (ACPI_PHYSICAL_DISCOVERY_PROBE || CLROS_NORMAL_VERIFIED_EXIT)

#endif
#if M02_OWNED_GDT_TSS_TERMINAL_PROBE

#endif
#if M02_UNEXPECTED_VECTOR_PANIC_PROBE

#endif
#if M02_PAGE_FAULT_CR2_ERROR_CODE_PROBE

#endif
#if M02_DOUBLE_FAULT_IST_PROBE

#endif
#if M02_UD2_VECTOR_STATE_PROBE

#endif
#if (M03_FIRST_REAL_EXIT_BOOT_SERVICES_PROBE || CLROS_NORMAL_VERIFIED_EXIT)

#endif
#if M02_OWNED_GDT_TSS_TERMINAL_PROBE

#endif
#if M02_OWNED_GDT_TSS_TERMINAL_PROBE

#endif
#if M04_INDEPENDENT_ENDIAN_CONFORMANCE_PROBE

#endif
#if M04_SPAN_OVERLAP_MEMORY_CAST_UNSAFE_WIDTH_CONFORMANCE_PROBE

#endif
#if M04_ARITHMETIC_CONFORMANCE_PROBE

#endif
#if M04_LAYOUT_CONTRACT_PROBE

#endif
#if M02_NORMALIZED_INTERRUPT_FRAME_PAGE_PROBE

#endif
#if M02_OWNED_IDT_GATE_POPULATION_PROBE

#endif
#if M02_IDTR_CONTINUED_EXECUTION_PROBE

#endif
#if M03_MEMORY_MAP_CAPTURE_PROBE

#endif
#if M03_MEMORY_MAP_CAPTURE_PROBE

#endif
#if (ACPI_PHYSICAL_DISCOVERY_PROBE || CLROS_NORMAL_VERIFIED_EXIT)

#endif
#if M03_FIRMWARE_ENTROPY_SEED_PROBE

#endif
#if M03_TYPED_EXIT_BOOT_SERVICES_CALL_PROBE

#endif
#if EXIT_BOOT_SERVICES_FRESH_MAP_PROBE

#endif
#if M03_MEMORY_MAP_CAPTURE_PROBE

#else
#endif
#if UEFI_GONE_SIMULATION_PROBE

#endif
#if M04_INDEPENDENT_ENDIAN_CONFORMANCE_PROBE

#endif
#if M04_SPAN_OVERLAP_MEMORY_CAST_UNSAFE_WIDTH_CONFORMANCE_PROBE

#endif
#if M04_ARITHMETIC_CONFORMANCE_PROBE

#endif
#if M04_LAYOUT_CONTRACT_PROBE

#endif
#if M02_NORMALIZED_INTERRUPT_FRAME_PAGE_PROBE

#endif
#if M02_OWNED_IDT_GATE_POPULATION_PROBE

#endif
#if M02_IDTR_CONTINUED_EXECUTION_PROBE

#endif
#if (ACPI_PHYSICAL_DISCOVERY_PROBE || CLROS_NORMAL_VERIFIED_EXIT)

#endif
#if UEFI_GONE_SIMULATION_PROBE

#endif
#if M03_MEMORY_MAP_CAPTURE_PROBE

#endif
#if M03_MEMORY_MAP_CAPTURE_PROBE

#endif
#if M03_MEMORY_MAP_CAPTURE_PROBE

#else
#endif
#if M03_MEMORY_MAP_CAPTURE_PROBE

#endif
#if M03_MEMORY_MAP_CAPTURE_PROBE

#endif
#if (M01_LOADED_IMAGE_STACK_PROBE || CLROS_NORMAL_VERIFIED_EXIT)

    private static partial class M01StageOneAdapter
    {
        private const ulong EfiSuccess = 0;
        private const uint AllocateAnyPages = 0;
        private const uint EfiLoaderCode = 1;
        private const uint EfiLoaderData = 2;
        private const ulong PageSize = 4096;
        private const ulong CodePageCount = 1;
        private const ulong GuardPageCount = 1;
        private const ulong UsableStackPageCount = 33;
        private const ulong DataArenaPageCount =
            GuardPageCount + UsableStackPageCount;
        private const ulong StubEntryOffset = 16;
        private const ulong StackProbeEntryOffset = 64;
        private const ulong StackProbeByteLength = 27;
        private const ulong CodeAllocationOrdinal = 1;
        private const ulong DataAllocationOrdinal = 2;
        private const ulong MapCaptureOrdinal = 3;
        private const ulong LedgerBuildOrdinal = 4;
        private const ulong InitialMapGeneration = 1;
        private const ulong RefreshedMapGeneration = 2;
        private const ulong ReservationCount = 4;
        private const ulong ReservationCapacity = 4;
        private const ulong NotAttemptedStatus = 0xffffffffffffffff;

        private static EfiSimpleTextOutputProtocol* s_console;
        private static EfiSerialIoProtocol* s_serial;
        private static EfiBootServices* s_bootServices;
        private static M01LoadedImageStackStaticState s_staticState;
        private static M01LoadedImageStackEvidence s_evidenceScratch;
        private static M01StageOneMemoryMapBuffer s_memoryMapSource;
        private static M01StageOneAllocationRecordBuffer
            s_allocationRecords;
        private static M01StageOneReservationLedgerBuffer
            s_reservationLedger;

        public static bool Run(
            void* imageHandle,
            EfiSimpleTextOutputProtocol* console,
            EfiSerialIoProtocol* serial,
            EfiSystemTable* systemTable)
        {
            WriteSuccessLine(console, serial, 0);

            if (imageHandle == null
                || systemTable == null
                || systemTable->BootServices == null
                || systemTable->BootServices->HandleProtocol == null
                || systemTable->BootServices->AllocatePages == null
                || systemTable->BootServices->FreePages == null
                || systemTable->BootServices->GetMemoryMap == null
                || !EfiEntry.s_bootHandoff.MemoryMap.HasOwnedCopy())
            {
                return FailBeforeTransfer(
                    console,
                    serial,
                    systemTable == null
                        ? null
                        : systemTable->BootServices,
                    M01StageOneAdapterStatus.MissingFirmwareBoundary,
                    (ulong)M01LoadedImageStackStatus.Uninitialized,
                    (ulong)M01LoadedImageStackCheckpoint.Uninitialized,
                    0,
                    false,
                    0,
                    false);
            }

            EfiBootServices* bootServices = systemTable->BootServices;
            if (EfiEntry
                    .s_m01InstructionPublicationInitialized != 0
                || EfiEntry
                    .s_m01InstructionPublicationEpochMask != 0)
            {
                EfiEntry
                    .WriteM01InstructionPublicationCookieFailure(
                        console,
                        serial);
                return FailBeforeTransfer(
                    console,
                    serial,
                    bootServices,
                    M01StageOneAdapterStatus
                        .InstructionPublicationRejected,
                    (ulong)M01LoadedImageStackStatus
                        .Uninitialized,
                    (ulong)M01LoadedImageStackCheckpoint
                        .Uninitialized,
                    0,
                    false,
                    0,
                    false);
            }
            EfiEntry.s_m01InstructionPublicationEpochMask = 0;
            EfiEntry.s_m01InstructionPublicationInitialized = 1;

            M01StageOneAdapterStatus captureStatus =
                TryCaptureLoadedImage(
                    imageHandle,
                    systemTable,
                    bootServices,
                    out EfiLoadedImageRangeCapturePlan loadedImage,
                    out M01LoadedImageVirtualMappingEvidence
                        loadedImageMapping,
                    out ulong loadedImageFirmwareStatus);
            if (captureStatus != M01StageOneAdapterStatus.Success)
            {
                return FailBeforeTransfer(
                    console,
                    serial,
                    bootServices,
                    captureStatus,
                    captureStatus
                        == M01StageOneAdapterStatus
                            .LoadedImageProtocolFailed
                        ? loadedImageFirmwareStatus
                        : (ulong)M01LoadedImageStackStatus
                            .LoadedImageRejected,
                    (ulong)M01LoadedImageStackCheckpoint.Uninitialized,
                    0,
                    false,
                    0,
                    false);
            }

            ulong codeAddress = 0;
            ulong codeFirmwareStatus =
                bootServices->AllocatePages(
                    AllocateAnyPages,
                    EfiLoaderCode,
                    CodePageCount,
                    &codeAddress);
            bool codeAllocated =
                codeFirmwareStatus == EfiSuccess;
            if (codeFirmwareStatus != EfiSuccess
                || !TryValidateAllocation(
                    codeAddress,
                    CodePageCount,
                    out ulong codeByteLength,
                    out ulong codeEndExclusive))
            {
                return FailBeforeTransfer(
                    console,
                    serial,
                    bootServices,
                    codeFirmwareStatus == EfiSuccess
                        ? M01StageOneAdapterStatus
                            .InvalidCodeAllocation
                        : M01StageOneAdapterStatus
                            .CodeAllocationFailed,
                    codeFirmwareStatus,
                    (ulong)M01LoadedImageStackCheckpoint
                        .LoadedImageValidated,
                    codeAddress,
                    codeAllocated,
                    0,
                    false);
            }

            ulong dataAddress = 0;
            ulong dataFirmwareStatus =
                bootServices->AllocatePages(
                    AllocateAnyPages,
                    EfiLoaderData,
                    DataArenaPageCount,
                    &dataAddress);
            bool dataAllocated =
                dataFirmwareStatus == EfiSuccess;
            if (dataFirmwareStatus != EfiSuccess
                || !TryValidateAllocation(
                    dataAddress,
                    DataArenaPageCount,
                    out ulong dataByteLength,
                    out ulong dataEndExclusive))
            {
                return FailBeforeTransfer(
                    console,
                    serial,
                    bootServices,
                    dataFirmwareStatus == EfiSuccess
                        ? M01StageOneAdapterStatus
                            .InvalidDataAllocation
                        : M01StageOneAdapterStatus
                            .DataAllocationFailed,
                    dataFirmwareStatus,
                    (ulong)M01LoadedImageStackCheckpoint
                        .LoadedImageValidated,
                    codeAddress,
                    codeAllocated,
                    dataAddress,
                    dataAllocated);
            }

            if (RangesOverlap(
                    codeAddress,
                    codeEndExclusive,
                    dataAddress,
                    dataEndExclusive)
                || !TryGetReservationBounds(
                    loadedImage.Range.Reservation,
                    out ulong imageReservationStart,
                    out ulong imageReservationEnd)
                || RangesOverlap(
                    codeAddress,
                    codeEndExclusive,
                    imageReservationStart,
                    imageReservationEnd)
                || RangesOverlap(
                    dataAddress,
                    dataEndExclusive,
                    imageReservationStart,
                    imageReservationEnd))
            {
                return FailBeforeTransfer(
                    console,
                    serial,
                    bootServices,
                    M01StageOneAdapterStatus.AllocationOverlap,
                    (ulong)M01LoadedImageStackStatus
                        .AllocationTableRejected,
                    (ulong)M01LoadedImageStackCheckpoint
                        .LoadedImageValidated,
                    codeAddress,
                    codeAllocated,
                    dataAddress,
                    dataAllocated);
            }

            ulong previousMapKey =
                EfiEntry.s_bootHandoff.MemoryMap.MapKey;
            M01StageOneAdapterStatus mapStatus =
                TryRecaptureMemoryMap(
                    bootServices,
                    out FirmwareMemoryMapRecord refreshedMap,
                    out ulong memoryMapFirmwareStatus);
            if (mapStatus != M01StageOneAdapterStatus.Success
                || refreshedMap.MapKey == previousMapKey)
            {
                return FailBeforeTransfer(
                    console,
                    serial,
                    bootServices,
                    mapStatus != M01StageOneAdapterStatus.Success
                        ? mapStatus
                        : M01StageOneAdapterStatus
                            .MemoryMapKeyUnchanged,
                    mapStatus
                        == M01StageOneAdapterStatus
                            .MemoryMapRecaptureFailed
                        ? memoryMapFirmwareStatus
                        : (ulong)M01LoadedImageStackStatus
                            .MapLedgerEvidenceRejected,
                    (ulong)M01LoadedImageStackCheckpoint
                        .AllocationEvidenceValidated,
                    codeAddress,
                    codeAllocated,
                    dataAddress,
                    dataAllocated);
            }

            if (!MapContainsAllocation(
                    refreshedMap,
                    codeAddress,
                    codeEndExclusive,
                    EfiLoaderCode)
                || !MapContainsAllocation(
                    refreshedMap,
                    dataAddress,
                    dataEndExclusive,
                    EfiLoaderData))
            {
                return FailBeforeTransfer(
                    console,
                    serial,
                    bootServices,
                    M01StageOneAdapterStatus
                        .MemoryMapAllocationTypeMismatch,
                    (ulong)M01LoadedImageStackStatus
                        .MapLedgerEvidenceRejected,
                    (ulong)M01LoadedImageStackCheckpoint
                        .AllocationEvidenceValidated,
                    codeAddress,
                    codeAllocated,
                    dataAddress,
                    dataAllocated);
            }

            M01StageOneAdapterStatus stackProbeStatus =
                MaterializeAndInstallStackProbe(
                    codeAddress,
                    codeByteLength);
            if (stackProbeStatus
                != M01StageOneAdapterStatus.Success)
            {
                return FailBeforeTransfer(
                    console,
                    serial,
                    bootServices,
                    stackProbeStatus,
                    (ulong)M01LoadedImageStackStatus
                        .SwitchPlanRejected,
                    (ulong)M01LoadedImageStackCheckpoint
                        .MapLedgerValidated,
                    codeAddress,
                    codeAllocated,
                    dataAddress,
                    dataAllocated);
            }

            if (EfiEntry.s_m01InstructionPublicationEpochMask
                != 0)
            {
                EfiEntry
                    .WriteM01InstructionPublicationCookieFailure(
                        console,
                        serial);
                return FailBeforeTransfer(
                    console,
                    serial,
                    bootServices,
                    M01StageOneAdapterStatus
                        .InstructionPublicationRejected,
                    (ulong)M01LoadedImageStackStatus
                        .SwitchPlanRejected,
                    (ulong)M01LoadedImageStackCheckpoint
                        .MapLedgerValidated,
                    codeAddress,
                    codeAllocated,
                    dataAddress,
                    dataAllocated);
            }
            uint epochOnePublicationCookie =
                global::OS.Boot.X64InstructionPublication.Serialize();
            if (epochOnePublicationCookie
                != global::OS.Boot.X64InstructionPublication
                    .ExpectedSuccessCookie)
            {
                EfiEntry
                    .WriteM01InstructionPublicationCookieFailure(
                        console,
                        serial);
                return FailBeforeTransfer(
                    console,
                    serial,
                    bootServices,
                    M01StageOneAdapterStatus
                        .InstructionPublicationRejected,
                    (ulong)M01LoadedImageStackStatus
                        .SwitchPlanRejected,
                    (ulong)M01LoadedImageStackCheckpoint
                        .MapLedgerValidated,
                    codeAddress,
                    codeAllocated,
                    dataAddress,
                    dataAllocated);
            }
            EfiEntry.s_m01InstructionPublicationEpochMask = 1;

            M01StageOneAdapterStatus prepareStatus =
                PrepareAndTransfer(
                    console,
                    serial,
                    bootServices,
                    loadedImage,
                    loadedImageMapping,
                    refreshedMap,
                    previousMapKey,
                    codeAddress,
                    codeByteLength,
                    codeEndExclusive,
                    codeFirmwareStatus,
                    dataAddress,
                    dataByteLength,
                    dataEndExclusive,
                    dataFirmwareStatus,
                    out ulong coordinatorStatus,
                    out ulong coordinatorCheckpoint);
            if (prepareStatus
                == M01StageOneAdapterStatus.UnexpectedSwitchReturn)
            {
                WriteFailure(
                    console,
                    serial,
                    prepareStatus,
                    coordinatorStatus,
                    coordinatorCheckpoint,
                    NotAttemptedStatus,
                    NotAttemptedStatus);
                Spin();
            }

            if (prepareStatus != M01StageOneAdapterStatus.Success)
            {
                return FailBeforeTransfer(
                    console,
                    serial,
                    bootServices,
                    prepareStatus,
                    coordinatorStatus,
                    coordinatorCheckpoint,
                    codeAddress,
                    codeAllocated,
                    dataAddress,
                    dataAllocated);
            }

            WriteFailure(
                console,
                serial,
                M01StageOneAdapterStatus.UnexpectedSwitchReturn,
                (ulong)M01LoadedImageStackStatus.UnexpectedReturn,
                (ulong)M01LoadedImageStackCheckpoint
                    .TransferRecorded,
                NotAttemptedStatus,
                NotAttemptedStatus);
            Spin();
            return true;
        }

        private static M01StageOneAdapterStatus
            TryCaptureLoadedImage(
                void* imageHandle,
                EfiSystemTable* systemTable,
                EfiBootServices* bootServices,
                out EfiLoadedImageRangeCapturePlan loadedImage,
                out M01LoadedImageVirtualMappingEvidence mapping,
                out ulong firmwareStatus)
        {
            loadedImage = default;
            mapping = default;
            firmwareStatus = NotAttemptedStatus;

            if (sizeof(EfiLoadedImageProtocol) != 96)
            {
                return M01StageOneAdapterStatus
                    .LoadedImageProtocolLayoutRejected;
            }

            EfiGuid loadedImageGuid;
            loadedImageGuid.Data1 = 0x5b1b31a1;
            loadedImageGuid.Data2 = 0x9562;
            loadedImageGuid.Data3 = 0x11d2;
            loadedImageGuid.Data4_0 = 0x8e;
            loadedImageGuid.Data4_1 = 0x3f;
            loadedImageGuid.Data4_2 = 0x00;
            loadedImageGuid.Data4_3 = 0xa0;
            loadedImageGuid.Data4_4 = 0xc9;
            loadedImageGuid.Data4_5 = 0x69;
            loadedImageGuid.Data4_6 = 0x72;
            loadedImageGuid.Data4_7 = 0x3b;

            void* loadedImageInterface = null;
            firmwareStatus = bootServices->HandleProtocol(
                imageHandle,
                &loadedImageGuid,
                &loadedImageInterface);
            ulong protocolAddress = (ulong)loadedImageInterface;
            if (firmwareStatus != EfiSuccess
                || loadedImageInterface == null
                || (protocolAddress
                    & (M01LoadedImageStackStaticState
                        .RequiredStorageAlignment - 1)) != 0
                || protocolAddress > ~0UL - 96)
            {
                return M01StageOneAdapterStatus
                    .LoadedImageProtocolFailed;
            }

            EfiLoadedImageProtocol* protocol =
                (EfiLoadedImageProtocol*)loadedImageInterface;
            if (protocol->SystemTable != systemTable
                || protocol->ImageBase == null
                || protocol->ImageSize == 0)
            {
                return M01StageOneAdapterStatus
                    .LoadedImageProtocolRejected;
            }

            EfiLoadedImageProtocolObservation observation =
                EfiLoadedImageProtocolObservation
                    .CaptureFromUefiBoundary(
                        firmwareStatus,
                        (ulong)imageHandle,
                        protocolAddress,
                        (ulong)protocol->ImageBase,
                        protocol->ImageSize);
            loadedImage =
                EfiLoadedImageRangeCapturePlan.Create(
                    &observation,
                    EfiLoadedImageRangeCapturePlan
                        .RequiredObservationCount,
                    EfiLoadedImageRangeCapturePlan
                        .RequiredObservationCount,
                    EfiLoadedImageRange.PageSize);
            if (!loadedImage.Succeeded)
            {
                return M01StageOneAdapterStatus
                    .LoadedImageRangeRejected;
            }

            mapping = M01LoadedImageVirtualMappingEvidence
                .CaptureCallerAttestedIdentity(loadedImage);
            return mapping.IsValidFor(loadedImage)
                ? M01StageOneAdapterStatus.Success
                : M01StageOneAdapterStatus
                    .LoadedImageMappingRejected;
        }

        private static bool TryValidateAllocation(
            ulong address,
            ulong pageCount,
            out ulong byteLength,
            out ulong endExclusive)
        {
            byteLength = 0;
            endExclusive = 0;

            if (address == 0
                || (address & (PageSize - 1)) != 0
                || pageCount == 0
                || pageCount > ~0UL / PageSize)
            {
                return false;
            }

            byteLength = pageCount * PageSize;
            if (address > ~0UL - byteLength)
            {
                byteLength = 0;
                return false;
            }

            endExclusive = address + byteLength;
            return endExclusive > address
                && (endExclusive & (PageSize - 1)) == 0;
        }

        private static bool TryGetReservationBounds(
            OS.Kernel.Memory.BootReservedRange reservation,
            out ulong start,
            out ulong endExclusive)
        {
            start = 0;
            endExclusive = 0;
            return reservation.IsValid
                && reservation.PhysicalStart
                        .TryGetRawValueForUefiBoundary(out start)
                    == AddressValueStatus.Success
                && reservation.PhysicalEndExclusive
                        .TryGetRawValueForUefiBoundary(
                            out endExclusive)
                    == AddressValueStatus.Success
                && start < endExclusive;
        }

        private static M01StageOneAdapterStatus
            TryRecaptureMemoryMap(
                EfiBootServices* bootServices,
                out FirmwareMemoryMapRecord refreshedMap,
                out ulong firmwareStatus)
        {
            refreshedMap = default;
            firmwareStatus = NotAttemptedStatus;
            if (bootServices == null
                || bootServices->GetMemoryMap == null)
            {
                return M01StageOneAdapterStatus
                    .MissingMemoryMapBoundary;
            }

            fixed (byte* source =
                &s_memoryMapSource.Bytes[0])
            fixed (byte* destination =
                &EfiEntry.s_ownedMemoryMap.Bytes[0])
            {
                ulong sourceAddress = (ulong)source;
                ulong destinationAddress = (ulong)destination;
                ulong capacity = OwnedMemoryMapBufferSize;
                if (sourceAddress == 0
                    || destinationAddress == 0
                    || sourceAddress > ~0UL - capacity
                    || destinationAddress > ~0UL - capacity
                    || RangesOverlap(
                        sourceAddress,
                        sourceAddress + capacity,
                        destinationAddress,
                        destinationAddress + capacity))
                {
                    return M01StageOneAdapterStatus
                        .MemoryMapStorageRejected;
                }

                ulong mapSize = capacity;
                ulong mapKey = 0;
                ulong descriptorSize = 0;
                uint descriptorVersion = 0;
                firmwareStatus = bootServices->GetMemoryMap(
                    &mapSize,
                    source,
                    &mapKey,
                    &descriptorSize,
                    &descriptorVersion);
                if (firmwareStatus != EfiSuccess)
                {
                    return M01StageOneAdapterStatus
                        .MemoryMapRecaptureFailed;
                }

                FirmwareMemoryMapRecord sourceRecord;
                sourceRecord.BufferAddress = sourceAddress;
                sourceRecord.BufferSize = capacity;
                sourceRecord.SourceBufferAddress = sourceAddress;
                sourceRecord.SourceBufferSize = capacity;
                sourceRecord.MapSize = mapSize;
                sourceRecord.MapKey = mapKey;
                sourceRecord.DescriptorSize = descriptorSize;
                sourceRecord.DescriptorVersion =
                    descriptorVersion;
                sourceRecord.DescriptorCount =
                    descriptorSize == 0
                        ? 0
                        : mapSize / descriptorSize;
                sourceRecord.Flags = 0;

                UefiMemoryMapCopyPlanResult copyPlan =
                    UefiMemoryMapCopyPlanResult.Create(
                        sourceRecord,
                        capacity);
                if (!copyPlan.Succeeded)
                {
                    return M01StageOneAdapterStatus
                        .MemoryMapMetadataRejected;
                }

                for (ulong index = 0;
                    index
                        < copyPlan.Plan.RequiredDestinationBytes;
                    index++)
                {
                    destination[index] = source[index];
                }

                refreshedMap.BufferAddress =
                    destinationAddress;
                refreshedMap.BufferSize = capacity;
                refreshedMap.SourceBufferAddress =
                    sourceAddress;
                refreshedMap.SourceBufferSize = capacity;
                refreshedMap.MapSize = mapSize;
                refreshedMap.MapKey = mapKey;
                refreshedMap.DescriptorSize =
                    descriptorSize;
                refreshedMap.DescriptorVersion =
                    descriptorVersion;
                refreshedMap.DescriptorCount =
                    sourceRecord.DescriptorCount;
                refreshedMap.Flags =
                    FirmwareMemoryMapRecord.FlagOwnedCopy;

                EfiMemoryMapNormalizeResult normalized =
                    EfiMemoryMapNormalizer.Normalize(
                        (void*)refreshedMap.BufferAddress,
                        refreshedMap.MapSize,
                        refreshedMap.DescriptorSize,
                        refreshedMap.DescriptorVersion,
                        refreshedMap.DescriptorCount);
                if (!normalized.Succeeded)
                {
                    refreshedMap = default;
                    return M01StageOneAdapterStatus
                        .MemoryMapMetadataRejected;
                }

                EfiEntry.s_memoryMapSummary =
                    normalized.Summary;
                EfiEntry.s_memoryMapSnapshot.Buffer =
                    (void*)refreshedMap.BufferAddress;
                EfiEntry.s_memoryMapSnapshot.BufferSize =
                    refreshedMap.BufferSize;
                EfiEntry.s_memoryMapSnapshot.SourceBuffer =
                    (void*)refreshedMap
                        .SourceBufferAddress;
                EfiEntry.s_memoryMapSnapshot
                    .SourceBufferSize =
                    refreshedMap.SourceBufferSize;
                EfiEntry.s_memoryMapSnapshot.MapSize =
                    refreshedMap.MapSize;
                EfiEntry.s_memoryMapSnapshot.MapKey =
                    refreshedMap.MapKey;
                EfiEntry.s_memoryMapSnapshot
                    .DescriptorSize =
                    refreshedMap.DescriptorSize;
                EfiEntry.s_memoryMapSnapshot
                    .DescriptorVersion =
                    refreshedMap.DescriptorVersion;
                EfiEntry.s_memoryMapSnapshot
                    .DescriptorCount =
                    refreshedMap.DescriptorCount;
                EfiEntry.s_memoryMapSnapshot.IsOwned = true;
                EfiEntry.s_bootHandoff.MemoryMap =
                    refreshedMap;
            }

            return M01StageOneAdapterStatus.Success;
        }

        private static bool MapContainsAllocation(
            FirmwareMemoryMapRecord map,
            ulong targetStart,
            ulong targetEndExclusive,
            uint expectedType)
        {
            if (!map.HasOwnedCopy()
                || targetStart == 0
                || targetStart >= targetEndExclusive
                || map.DescriptorSize
                    < UefiMemoryMapCopyPlan
                        .MinimumDescriptorSize
                || map.DescriptorCount == 0
                || map.DescriptorCount
                    > UefiMemoryMapCopyPlan
                        .DefaultMaximumDescriptorCount)
            {
                return false;
            }

            byte* mapBytes = (byte*)map.BufferAddress;
            ulong cursor = targetStart;
            for (ulong segment = 0;
                segment < map.DescriptorCount
                    && cursor < targetEndExclusive;
                segment++)
            {
                ulong bestEnd = cursor;
                for (ulong index = 0;
                    index < map.DescriptorCount;
                    index++)
                {
                    byte* descriptor =
                        mapBytes
                        + (index * map.DescriptorSize);
                    uint type =
                        ReadUInt32(descriptor);
                    ulong physicalStart =
                        ReadUInt64(
                            descriptor + 8);
                    ulong pageCount =
                        ReadUInt64(
                            descriptor + 24);
                    if (type != expectedType
                        || pageCount == 0
                        || pageCount > ~0UL / PageSize)
                    {
                        continue;
                    }

                    ulong byteLength =
                        pageCount * PageSize;
                    if (physicalStart
                        > ~0UL - byteLength)
                    {
                        return false;
                    }

                    ulong descriptorEnd =
                        physicalStart + byteLength;
                    if (physicalStart <= cursor
                        && cursor < descriptorEnd
                        && descriptorEnd > bestEnd)
                    {
                        bestEnd = descriptorEnd;
                    }
                }

                if (bestEnd == cursor)
                {
                    return false;
                }

                cursor = bestEnd < targetEndExclusive
                    ? bestEnd
                    : targetEndExclusive;
            }

            return cursor == targetEndExclusive;
        }

        private static M01StageOneAdapterStatus
            MaterializeAndInstallStackProbe(
                ulong codeAddress,
                ulong codeByteLength)
        {
            if (codeAddress == 0
                || codeByteLength != PageSize
                || StackProbeEntryOffset
                    < StubEntryOffset
                        + OwnedBootStackSwitchPlan
                            .RequiredStubByteLength
                || StackProbeEntryOffset
                    > codeByteLength - StackProbeByteLength
                || codeAddress
                    > ~0UL - StackProbeEntryOffset
                || ((codeAddress + StackProbeEntryOffset)
                    & (OwnedBootStackSwitchPlan
                        .StubEntryAlignmentBytes - 1)) != 0)
            {
                return M01StageOneAdapterStatus
                    .StackProbeLayoutRejected;
            }

            byte* codePage = (byte*)codeAddress;
            for (ulong index = 0;
                index < codeByteLength;
                index++)
            {
                codePage[index] = 0xcc;
            }

            for (ulong index = 0;
                index < codeByteLength;
                index++)
            {
                if (codePage[index] != 0xcc)
                {
                    return M01StageOneAdapterStatus
                        .StackProbeReadbackRejected;
                }
            }

            byte* probe =
                codePage + StackProbeEntryOffset;
            for (ulong index = 0;
                index < StackProbeByteLength;
                index++)
            {
                probe[index] = GetStackProbeByte(index);
            }

            for (ulong index = 0;
                index < StackProbeByteLength;
                index++)
            {
                if (probe[index]
                    != GetStackProbeByte(index))
                {
                    return M01StageOneAdapterStatus
                        .StackProbeReadbackRejected;
                }
            }

            return System.Runtime.X64StackProbeRuntime
                .TryInstall((ulong)probe)
                ? M01StageOneAdapterStatus.Success
                : M01StageOneAdapterStatus
                    .StackProbeInstallRejected;
        }

        private static byte GetStackProbeByte(ulong index)
        {
            return index switch
            {
                0 => 0xf3,
                1 => 0x0f,
                2 => 0x1e,
                3 => 0xfa,
                4 => 0x48,
                5 => 0x89,
                6 => 0xe0,
                7 => 0x48,
                8 => 0x25,
                9 => 0x00,
                10 => 0xf0,
                11 => 0xff,
                12 => 0xff,
                13 => 0x48,
                14 => 0x2d,
                15 => 0x00,
                16 => 0x10,
                17 => 0x00,
                18 => 0x00,
                19 => 0x85,
                20 => 0x00,
                21 => 0x4c,
                22 => 0x39,
                23 => 0xd8,
                24 => 0x7f,
                25 => 0xf3,
                26 => 0xc3,
                _ => 0
            };
        }

        private static M01StageOneAdapterStatus
            PrepareAndTransfer(
                EfiSimpleTextOutputProtocol* console,
                EfiSerialIoProtocol* serial,
                EfiBootServices* bootServices,
                EfiLoadedImageRangeCapturePlan loadedImage,
                M01LoadedImageVirtualMappingEvidence
                    loadedImageMapping,
                FirmwareMemoryMapRecord refreshedMap,
                ulong previousMapKey,
                ulong codeAddress,
                ulong codeByteLength,
                ulong codeEndExclusive,
                ulong codeFirmwareStatus,
                ulong dataAddress,
                ulong dataByteLength,
                ulong dataEndExclusive,
                ulong dataFirmwareStatus,
                out ulong coordinatorStatus,
                out ulong coordinatorCheckpoint)
        {
            coordinatorStatus =
                (ulong)M01LoadedImageStackStatus
                    .Uninitialized;
            coordinatorCheckpoint =
                (ulong)M01LoadedImageStackCheckpoint
                    .Uninitialized;

            fixed (M01StageOneAllocationRecordBuffer*
                allocationStorage = &s_allocationRecords)
            fixed (M01StageOneReservationLedgerBuffer*
                ledgerStorage = &s_reservationLedger)
            fixed (M01LoadedImageStackStaticState*
                staticState = &s_staticState)
            fixed (M01LoadedImageStackEvidence*
                evidenceScratch = &s_evidenceScratch)
            {
                if ((ulong)sizeof(
                        M01StageOneAllocationRecordBuffer)
                        != M01LoadedImageStackIntegration
                            .RequiredAllocationRecordCount
                            * (ulong)sizeof(
                                BootMemoryRegionRecord)
                    || (ulong)sizeof(
                        M01StageOneReservationLedgerBuffer)
                        != ReservationCapacity
                            * (ulong)sizeof(
                                OS.Kernel.Memory
                                    .BootReservedRange))
                {
                    return M01StageOneAdapterStatus
                        .StaticStorageLayoutRejected;
                }

                BootMemoryRegionRecord* allocationRecords =
                    &allocationStorage->Code;
                allocationStorage->Code =
                    new BootMemoryRegionRecord(
                        EfiLoaderCode,
                        codeAddress,
                        codeAddress,
                        CodePageCount,
                        0,
                        codeByteLength,
                        codeEndExclusive);
                allocationStorage->Data =
                    new BootMemoryRegionRecord(
                        EfiLoaderData,
                        dataAddress,
                        dataAddress,
                        DataArenaPageCount,
                        0,
                        dataByteLength,
                        dataEndExclusive);

                M01StageOneAllocationEvidence
                    allocationEvidence =
                    M01StageOneAllocationEvidence
                        .CaptureCallerAttested(
                            allocationRecords,
                            M01LoadedImageStackIntegration
                                .RequiredAllocationRecordCount,
                            M01LoadedImageStackIntegration
                                .RequiredAllocationRecordCount,
                            CodeAllocationOrdinal,
                            DataAllocationOrdinal,
                            codeFirmwareStatus,
                            dataFirmwareStatus);
                M01StageOneAddressMappingEvidence
                    addressMapping =
                    M01StageOneAddressMappingEvidence
                        .CaptureCallerAttestedIdentity(
                            allocationRecords,
                            M01LoadedImageStackIntegration
                                .RequiredAllocationRecordCount,
                            M01LoadedImageStackIntegration
                                .RequiredAllocationRecordCount,
                            refreshedMap);
                if (!allocationEvidence.IsWellFormed
                    || !addressMapping.IsValidFor(
                        allocationStorage->Code,
                        allocationStorage->Data,
                        refreshedMap))
                {
                    return M01StageOneAdapterStatus
                        .AllocationEvidenceRejected;
                }

                OS.Kernel.Memory.BootReservedRange*
                    reservationLedger =
                    &ledgerStorage->Range0;
                M01StageOneAdapterStatus ledgerStatus =
                    BuildReservationLedger(
                        loadedImage,
                        codeAddress,
                        dataAddress,
                        reservationLedger);
                if (ledgerStatus
                    != M01StageOneAdapterStatus.Success)
                {
                    return ledgerStatus;
                }

                OS.Kernel.Memory
                    .BootReservationTableValidationResult
                    ledgerValidation =
                    OS.Kernel.Memory
                        .BootReservationTableValidator
                        .Validate(
                            reservationLedger,
                            ReservationCount,
                            ReservationCapacity);
                if (!ledgerValidation.Succeeded
                    || ledgerValidation.ReservationCount
                        != ReservationCount
                    || ledgerValidation
                            .AvailableReservationCapacity
                        != ReservationCapacity)
                {
                    return M01StageOneAdapterStatus
                        .ReservationLedgerRejected;
                }

                M01StageOneMapLedgerEvidence
                    mapLedgerEvidence =
                    M01StageOneMapLedgerEvidence
                        .CaptureCallerAttested(
                            previousMapKey,
                            InitialMapGeneration,
                            refreshedMap,
                            RefreshedMapGeneration,
                            allocationEvidence,
                            MapCaptureOrdinal,
                            LedgerBuildOrdinal,
                            reservationLedger,
                            ReservationCount,
                            ReservationCapacity);
                if (!mapLedgerEvidence.IsValidFor(
                    allocationEvidence,
                    reservationLedger,
                    ReservationCount,
                    ReservationCapacity))
                {
                    return M01StageOneAdapterStatus
                        .MapLedgerEvidenceRejected;
                }

                delegate* unmanaged<
                    ulong,
                    OwnedBootStackSwitchHandoff*,
                    void> continuation =
                    &M01StageOneContinuation;
                ulong continuationRaw =
                    (ulong)(void*)continuation;
                if (VirtualAddress
                        .TryCreateFromUefiBoundary(
                            continuationRaw,
                            out VirtualAddress
                                continuationAddress)
                    != AddressValueStatus.Success)
                {
                    return M01StageOneAdapterStatus
                        .ContinuationAddressRejected;
                }

                M01LoadedImageStackStaticPreparationResult
                    prepared =
                    M01LoadedImageStackIntegration
                        .PrepareFromStaticScratch(
                        loadedImage,
                        loadedImageMapping,
                        addressMapping,
                        allocationRecords,
                        M01LoadedImageStackIntegration
                            .RequiredAllocationRecordCount,
                        M01LoadedImageStackIntegration
                            .RequiredAllocationRecordCount,
                        allocationEvidence,
                        mapLedgerEvidence,
                        reservationLedger,
                        ReservationCount,
                        ReservationCapacity,
                        (byte*)codeAddress,
                        codeByteLength,
                        PageSize,
                        StubEntryOffset,
                        OwnedBootStackSwitchPlan
                            .RequiredStubByteLength,
                        continuationAddress,
                        staticState,
                        M01LoadedImageStackStaticState
                            .RequiredStorageBytes,
                        evidenceScratch,
                        M01LoadedImageStackIntegration
                            .RequiredEvidenceScratchBytes);
                coordinatorStatus =
                    (ulong)prepared.Status;
                coordinatorCheckpoint =
                    (ulong)prepared.Checkpoint;
                if (!prepared.IsPrepared)
                {
                    return M01StageOneAdapterStatus
                        .CoordinatorPrepareRejected;
                }

                prepared = default;
                M01LoadedImageStackStaticTransitionResult
                    transfer =
                    M01LoadedImageStackIntegration
                        .RecordTransferFromStaticState(
                            staticState,
                            M01LoadedImageStackStaticState
                                .RequiredStorageBytes);
                coordinatorStatus =
                    (ulong)transfer.Status;
                coordinatorCheckpoint =
                    (ulong)transfer.Checkpoint;
                if (!transfer.TransferRecorded)
                {
                    return M01StageOneAdapterStatus
                        .TransferRecordRejected;
                }

                M01LoadedImageStackTransferTarget target =
                    transfer.TransferTarget;
                OwnedBootStackSwitchPlan plan =
                    staticState->SwitchStateStorage.Plan;
                if (!target.IsValidFor(plan)
                    || target.Entry
                            .TryGetRawValueForUefiBoundary(
                                out ulong entryRaw)
                        != AddressValueStatus.Success
                    || target.NewRspTop
                            .TryGetRawValueForUefiBoundary(
                                out ulong newRspRaw)
                        != AddressValueStatus.Success
                    || target.HandoffAddress
                            .TryGetRawValueForUefiBoundary(
                                out ulong handoffRaw)
                        != AddressValueStatus.Success
                    || target.ContinuationAddress
                            .TryGetRawValueForUefiBoundary(
                                out ulong targetContinuationRaw)
                        != AddressValueStatus.Success
                    || plan.ExpectedContinuationStackPointer
                            .TryGetRawValueForUefiBoundary(
                                out ulong expectedRspRaw)
                        != AddressValueStatus.Success
                    || plan.UsableStackVirtualStart
                            .TryGetRawValueForUefiBoundary(
                                out ulong usableStartRaw)
                        != AddressValueStatus.Success
                    || plan.UsableStackVirtualEndExclusive
                            .TryGetRawValueForUefiBoundary(
                                out ulong usableEndRaw)
                        != AddressValueStatus.Success
                    || entryRaw != codeAddress
                        + StubEntryOffset
                    || (entryRaw
                        & (OwnedBootStackSwitchPlan
                            .StubEntryAlignmentBytes - 1)) != 0
                    || entryRaw >= codeEndExclusive
                    || newRspRaw
                        != usableEndRaw
                            - OwnedBootStackSwitchPlan
                                .X64ShadowSpaceBytes
                    || (newRspRaw
                        & (OwnedBootStackSwitchPlan
                            .X64StackAlignmentBytes - 1)) != 0
                    || expectedRspRaw != newRspRaw
                        - OwnedBootStackSwitchPlan
                            .FakeReturnAddressBytes
                    || expectedRspRaw < usableStartRaw
                    || expectedRspRaw >= usableEndRaw
                    || handoffRaw
                        != (ulong)&staticState
                            ->HandoffStorage
                    || (handoffRaw
                        & (M01LoadedImageStackStaticState
                            .RequiredStorageAlignment - 1)) != 0
                    || targetContinuationRaw
                        != continuationRaw)
                {
                    return M01StageOneAdapterStatus
                        .TransferTargetRejected;
                }

                s_console = console;
                s_serial = serial;
                s_bootServices = bootServices;
                transfer = default;

                if (EfiEntry
                        .s_m01InstructionPublicationEpochMask
                    != 1)
                {
                    EfiEntry
                        .WriteM01InstructionPublicationCookieFailure(
                            console,
                            serial);
                    return M01StageOneAdapterStatus
                        .InstructionPublicationRejected;
                }
                uint epochTwoPublicationCookie =
                    global::OS.Boot.X64InstructionPublication.Serialize();
                if (epochTwoPublicationCookie
                    != global::OS.Boot.X64InstructionPublication
                        .ExpectedSuccessCookie)
                {
                    EfiEntry
                        .WriteM01InstructionPublicationCookieFailure(
                            console,
                            serial);
                    return M01StageOneAdapterStatus
                        .InstructionPublicationRejected;
                }
                EfiEntry.s_m01InstructionPublicationEpochMask = 3;

                delegate* unmanaged<
                    ulong,
                    OwnedBootStackSwitchHandoff*,
                    delegate* unmanaged<
                        ulong,
                        OwnedBootStackSwitchHandoff*,
                        void>,
                    void> switchEntry =
                    (delegate* unmanaged<
                        ulong,
                        OwnedBootStackSwitchHandoff*,
                        delegate* unmanaged<
                            ulong,
                            OwnedBootStackSwitchHandoff*,
                            void>,
                        void>)(void*)entryRaw;
                switchEntry(
                    newRspRaw,
                    (OwnedBootStackSwitchHandoff*)
                        handoffRaw,
                    continuation);

                M01LoadedImageStackStaticTransitionResult
                    unexpected =
                    M01LoadedImageStackIntegration
                        .ReportUnexpectedReturnFromStaticState(
                            staticState,
                            M01LoadedImageStackStaticState
                                .RequiredStorageBytes);
                coordinatorStatus =
                    (ulong)unexpected.Status;
                coordinatorCheckpoint =
                    (ulong)unexpected.Checkpoint;
                return M01StageOneAdapterStatus
                    .UnexpectedSwitchReturn;
            }
        }

        private static uint ReadUInt32(byte* source)
        {
            return (uint)(
                source[0]
                | (source[1] << 8)
                | (source[2] << 16)
                | (source[3] << 24));
        }

        private static ulong ReadUInt64(byte* source)
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

        private static M01StageOneAdapterStatus
            BuildReservationLedger(
                EfiLoadedImageRangeCapturePlan loadedImage,
                ulong codeAddress,
                ulong dataAddress,
                OS.Kernel.Memory.BootReservedRange*
                    reservationLedger)
        {
            if (reservationLedger == null
                || PhysicalAddress.TryCreateFromUefiBoundary(
                        codeAddress,
                        out PhysicalAddress codeStart)
                    != AddressValueStatus.Success
                || PhysicalAddress.TryCreateFromUefiBoundary(
                        dataAddress,
                        out PhysicalAddress dataStart)
                    != AddressValueStatus.Success
                || dataAddress > ~0UL - PageSize
                || PhysicalAddress.TryCreateFromUefiBoundary(
                        dataAddress + PageSize,
                        out PhysicalAddress stackStart)
                    != AddressValueStatus.Success)
            {
                return M01StageOneAdapterStatus
                    .ReservationAddressRejected;
            }

            reservationLedger[0] =
                loadedImage.Range.Reservation;
            if (OS.Kernel.Memory.BootReservedRange
                    .TryCreate(
                        OS.Kernel.Memory.BootReservationKind
                            .BootStackSwitchStub,
                        codeStart,
                        CodePageCount,
                        out reservationLedger[1])
                    != OS.Kernel.Memory
                        .BootReservedRangeStatus.Success
                || OS.Kernel.Memory.BootReservedRange
                    .TryCreate(
                        OS.Kernel.Memory.BootReservationKind
                            .BootStackGuard,
                        dataStart,
                        GuardPageCount,
                        out reservationLedger[2])
                    != OS.Kernel.Memory
                        .BootReservedRangeStatus.Success
                || OS.Kernel.Memory.BootReservedRange
                    .TryCreate(
                        OS.Kernel.Memory.BootReservationKind
                            .BootStack,
                        stackStart,
                        UsableStackPageCount,
                        out reservationLedger[3])
                    != OS.Kernel.Memory
                        .BootReservedRangeStatus.Success)
            {
                return M01StageOneAdapterStatus
                    .ReservationConstructionRejected;
            }

            for (ulong index = 1;
                index < ReservationCount;
                index++)
            {
                OS.Kernel.Memory.BootReservedRange current =
                    reservationLedger[index];
                ulong destination = index;
                while (destination != 0)
                {
                    AddressValueStatus compareStatus =
                        reservationLedger[destination - 1]
                            .PhysicalStart.TryCompareTo(
                                current.PhysicalStart,
                                out int comparison);
                    if (compareStatus
                        != AddressValueStatus.Success)
                    {
                        return M01StageOneAdapterStatus
                            .ReservationSortRejected;
                    }

                    if (comparison <= 0)
                    {
                        break;
                    }

                    reservationLedger[destination] =
                        reservationLedger[
                            destination - 1];
                    destination--;
                }

                reservationLedger[destination] = current;
            }

            return M01StageOneAdapterStatus.Success;
        }

        internal static bool IsFinalStageOneSuccess(
            void* stageOneState,
            ulong stageOneStateCapacity)
        {
            ulong stateAddress = (ulong)stageOneState;
            ulong requiredCapacity =
                M01LoadedImageStackStaticState.RequiredStorageBytes;
            if ((ulong)sizeof(M01LoadedImageStackStaticState)
                    != requiredCapacity
                || stageOneState == null
                || (stateAddress
                    & (M01LoadedImageStackStaticState
                        .RequiredStorageAlignment - 1)) != 0
                || stageOneStateCapacity != requiredCapacity
                || stateAddress > ~0UL - stageOneStateCapacity)
            {
                return false;
            }

            M01LoadedImageStackStaticState* state =
                (M01LoadedImageStackStaticState*)stageOneState;
            fixed (M01LoadedImageStackStaticState*
                expectedState = &s_staticState)
            {
                return state == expectedState
                    && state->IsInitialized
                    && state->Status
                        == M01LoadedImageStackStatus.Success
                    && state->Checkpoint
                        == M01LoadedImageStackCheckpoint
                            .ContinuationRspContained
                    && state->SwitchState.State
                        == OwnedBootStackSwitchState
                            .ContinuationObserved
                    && state->StackLayout.Lifetime
                        == OwnedBootStackLifetime.Active;
            }
        }

#if M04_RHP_NEW_FAST_PROBE || M02_UNEXPECTED_VECTOR_PANIC_PROBE || M04_TYPE_MANAGER_MODULE_REGISTRATION_PROBE || (M03_FIRST_REAL_EXIT_BOOT_SERVICES_PROBE || CLROS_NORMAL_VERIFIED_EXIT)
#if (M03_FIRST_REAL_EXIT_BOOT_SERVICES_PROBE || CLROS_NORMAL_VERIFIED_EXIT)
        internal static bool TryGetOriginalExitStackProbe(out ulong page)
        {
            page = 0;
            fixed (M01LoadedImageStackStaticState* state = &s_staticState)
            {
                if (!TryGetFinalSwitchPlan(state, M01LoadedImageStackStaticState.RequiredStorageBytes,
                        out _, out BootMemoryRegionRecord allocation) || allocation.UefiType != EfiLoaderCode ||
                    allocation.VirtualStart != allocation.PhysicalStart || allocation.PhysicalStart == 0 ||
                    (allocation.PhysicalStart & (PageSize - 1)) != 0)
                    return false;
                byte* probe = (byte*)(allocation.VirtualStart + StackProbeEntryOffset);
                for (ulong i = 0; i < StackProbeByteLength; i++)
                    if (probe[i] != GetStackProbeByte(i))
                        return false;
                // Stage-one success follows its sole successful TryInstall;
                // this exclusive path never calls its failure-only TryClear.
                page = allocation.VirtualStart;
                return true;
            }
        }
#endif
        internal static bool TryGetOriginalLoadedImage(out byte* image, out ulong bytes)
        {
            image = null;
            bytes = 0;
            fixed (M01LoadedImageStackStaticState* state = &s_staticState)
                if (!IsFinalStageOneSuccess(state, M01LoadedImageStackStaticState.RequiredStorageBytes))
                    return false;
            EfiLoadedImageRangeCapturePlan capture = s_evidenceScratch.LoadedImageCapture;
            M01LoadedImageVirtualMappingEvidence mapping = s_evidenceScratch.LoadedImageVirtualMapping;
            if (!capture.Succeeded || !mapping.IsValidFor(capture) ||
                mapping.Provenance != M01LoadedImageVirtualMappingProvenance.CallerAttestedIdentityMapping ||
                capture.Range.ImageBase.TryGetRawValueForUefiBoundary(out ulong physical) != AddressValueStatus.Success ||
                mapping.VirtualImageBase.TryGetRawValueForUefiBoundary(out ulong address) != AddressValueStatus.Success ||
                address == 0 || address != physical || mapping.ImageByteLength != capture.Range.ImageSize ||
                capture.Range.ImageSize == 0 || address > ~0UL - capture.Range.ImageSize)
                return false;
            image = (byte*)address;
            bytes = capture.Range.ImageSize;
            return true;
        }

#if M04_TYPE_MANAGER_MODULE_REGISTRATION_PROBE || M04_GC_STATIC_ROOT_MATERIALIZATION_PROBE || (M03_FIRST_REAL_EXIT_BOOT_SERVICES_PROBE || CLROS_NORMAL_VERIFIED_EXIT)
        internal static bool TryGetOriginalTypeManagerImage(out EfiLoadedImageRangeCapturePlan capture,
            out M01LoadedImageVirtualMappingEvidence mapping)
        {
            capture = default;
            mapping = default;
            if (!TryGetOriginalLoadedImage(out byte* image, out ulong bytes))
                return false;
            EfiLoadedImageRangeCapturePlan originalCapture = s_evidenceScratch.LoadedImageCapture;
            M01LoadedImageVirtualMappingEvidence originalMapping = s_evidenceScratch.LoadedImageVirtualMapping;
            if (!originalCapture.Succeeded || !originalMapping.IsValidFor(originalCapture) ||
                originalMapping.VirtualImageBase.TryGetRawValueForUefiBoundary(out ulong address) != AddressValueStatus.Success ||
                address != (ulong)image || originalCapture.Range.ImageSize != bytes)
                return false;
            capture = originalCapture;
            mapping = originalMapping;
            return true;
        }
#endif

#if M04_RHP_NEW_FAST_PROBE
        internal static void WriteRhpByte(byte value) => EfiEntry.Write(s_console, s_serial, value);
#endif
#endif

#if M04_CHECKED_REFERENCE_BARRIER_PROBE || M04_GC_STATIC_ROOT_MATERIALIZATION_PROBE || M04_X64_UNWIND_METADATA_PROBE
        internal static bool TryGetOriginalReferenceStack(out ulong authority,
            out ulong start, out ulong end, out ulong initialRsp)
        {
            authority = start = end = initialRsp = 0;
            fixed (M01LoadedImageStackStaticState* state = &s_staticState)
            {
                if (!IsFinalStageOneSuccess(state, M01LoadedImageStackStaticState.RequiredStorageBytes))
                    return false;
                OwnedBootStackSwitchStateRecord record = state->SwitchStateStorage;
                OwnedBootStackSwitchPlan plan = record.Plan;
                if (!record.IsValid || !plan.IsValid ||
                    record.StorageAddress != (ulong)&state->SwitchStateStorage ||
                    !plan.StackLayout.BackingStart.Equals(state->StackLayoutStorage.BackingStart) ||
                    !plan.StackLayout.BackingEndExclusive.Equals(state->StackLayoutStorage.BackingEndExclusive) ||
                    plan.StackLayout.BackingPageCount != state->StackLayoutStorage.BackingPageCount ||
                    !plan.StackLayout.PhysicalStackTop.Equals(state->StackLayoutStorage.PhysicalStackTop) ||
                    plan.UsableStackVirtualStart.TryGetRawValueForUefiBoundary(out ulong low) != AddressValueStatus.Success ||
                    plan.UsableStackVirtualEndExclusive.TryGetRawValueForUefiBoundary(out ulong high) != AddressValueStatus.Success ||
                    record.ObservedRsp.TryGetRawValueForUefiBoundary(out ulong observed) != AddressValueStatus.Success ||
                    !record.ObservedRsp.Equals(plan.ExpectedContinuationStackPointer) ||
                    low == 0 || (low & (PageSize - 1)) != 0 || high <= low ||
                    high - low != UsableStackPageCount * PageSize || observed != high - 40)
                    return false;
                authority = (ulong)state;
                start = low;
                end = high;
                initialRsp = observed;
                return true;
            }
        }
#endif

        internal static bool TryGetFinalSwitchPlan(
            void* stageOneState,
            ulong stageOneStateCapacity,
            out OwnedBootStackSwitchPlanResult switchPlan,
            out BootMemoryRegionRecord stageOneCodeAllocation)
        {
            switchPlan = default;
            stageOneCodeAllocation = default;
            if (!IsFinalStageOneSuccess(
                    stageOneState,
                    stageOneStateCapacity))
            {
                return false;
            }

            switchPlan = s_evidenceScratch.SwitchPlanResult;
            stageOneCodeAllocation =
                s_evidenceScratch.CodeAllocation;
            return switchPlan.Succeeded
                && stageOneCodeAllocation.PhysicalStart
                    == switchPlan.Plan.CodeAllocation
                        .PhysicalStart
                && stageOneCodeAllocation.PhysicalEndExclusive
                    == switchPlan.Plan.CodeAllocation
                        .PhysicalEndExclusive
                && stageOneCodeAllocation.PageCount == 1
                && stageOneCodeAllocation.ByteLength == PageSize;
        }

        internal static bool TryValidateStageTwoAllocation(
            ulong address,
            ulong pageCount,
            out ulong byteLength,
            out ulong endExclusive)
        {
            return TryValidateAllocation(
                address,
                pageCount,
                out byteLength,
                out endExclusive);
        }

        internal static bool TryRecaptureStageTwoMemoryMap(
            EfiBootServices* bootServices,
            out FirmwareMemoryMapRecord refreshedMap,
            out ulong firmwareStatus)
        {
            M01StageOneAdapterStatus status =
                TryRecaptureMemoryMap(
                    bootServices,
                    out refreshedMap,
                    out firmwareStatus);
            return status == M01StageOneAdapterStatus.Success;
        }

        internal static bool StageTwoMapContainsAllocation(
            FirmwareMemoryMapRecord map,
            ulong targetStart,
            ulong targetEndExclusive,
            uint expectedType)
        {
            return MapContainsAllocation(
                map,
                targetStart,
                targetEndExclusive,
                expectedType);
        }

        internal static M01LiveAllocatorIntegrationResult
            ExecuteStageTwoCoordinator(
                void* stageOneState,
                ulong stageOneStateCapacity,
                BootMemoryRegionRecord* allocationRecords,
                ulong allocationRecordCount,
                ulong allocationRecordCapacity,
                M01LiveAllocatorAllocationEvidence
                    allocationEvidence,
                M01LiveAllocatorMapLedgerEvidence
                    mapLedgerEvidence,
                OS.Kernel.Memory.BootReservedRange*
                    reservationLedger,
                ulong reservationCount,
                ulong reservationCapacity,
                M01LiveAllocatorStaticState* staticState,
                ulong staticStateCapacity)
        {
            return M01LiveAllocatorIntegration
                .Execute(
                    (M01LoadedImageStackStaticState*)
                        stageOneState,
                    stageOneStateCapacity,
                    allocationRecords,
                    allocationRecordCount,
                    allocationRecordCapacity,
                    allocationEvidence,
                    mapLedgerEvidence,
                    reservationLedger,
                    reservationCount,
                    reservationCapacity,
                    staticState,
                    staticStateCapacity);
        }

        [System.Runtime.InteropServices.UnmanagedCallersOnly]
        private static void M01StageOneContinuation(
            ulong observedRsp,
            OwnedBootStackSwitchHandoff* handoff)
        {
            fixed (M01LoadedImageStackStaticState*
                staticState = &s_staticState)
            {
                if (EfiEntry
                        .s_m01InstructionPublicationEpochMask
                    != 3)
                {
                    EfiEntry
                        .WriteM01InstructionPublicationCookieFailure(
                            s_console,
                            s_serial);
                    WriteFailure(
                        s_console,
                        s_serial,
                        M01StageOneAdapterStatus
                            .InstructionPublicationRejected,
                        (ulong)M01LoadedImageStackStatus
                            .ContinuationRspRejected,
                        (ulong)M01LoadedImageStackCheckpoint
                            .TransferRecorded,
                        NotAttemptedStatus,
                        NotAttemptedStatus);
                    Spin();
                }

                VirtualAddress observedRspAddress = default;
                if (handoff == null
                    || handoff
                        != &staticState->HandoffStorage
                    || VirtualAddress
                            .TryCreateFromUefiBoundary(
                                observedRsp,
                                out observedRspAddress)
                        != AddressValueStatus.Success)
                {
                    WriteFailure(
                        s_console,
                        s_serial,
                        M01StageOneAdapterStatus
                            .ContinuationHandoffRejected,
                        (ulong)M01LoadedImageStackStatus
                            .ContinuationRspRejected,
                        (ulong)M01LoadedImageStackCheckpoint
                            .TransferRecorded,
                        NotAttemptedStatus,
                        NotAttemptedStatus);
                    Spin();
                }

                M01LoadedImageStackStaticTransitionResult
                    observation =
                    M01LoadedImageStackIntegration
                        .ObserveContinuationRspFromStaticState(
                            staticState,
                            M01LoadedImageStackStaticState
                                .RequiredStorageBytes,
                            observedRspAddress);
                if (!observation.ContinuationRspContained)
                {
                    WriteFailure(
                        s_console,
                        s_serial,
                        M01StageOneAdapterStatus
                            .ContinuationRspRejected,
                        (ulong)observation.Status,
                        (ulong)observation.Checkpoint,
                        NotAttemptedStatus,
                        NotAttemptedStatus);
                    Spin();
                }

                for (int lineIndex = 1;
                    lineIndex
                        < BootSequencePlan
                            .M01StageOneSuccessLineCount;
                    lineIndex++)
                {
                    WriteSuccessLine(
                        s_console,
                        s_serial,
                        lineIndex);
                }

#if (M01_LIVE_ALLOCATOR_PROBE || CLROS_NORMAL_VERIFIED_EXIT)
                fixed (M01StageOneReservationLedgerBuffer*
                    stageOneLedgerStorage = &s_reservationLedger)
                {
                    if (!M01StageTwoAdapter.RunAfterStageOne(
                            (void*)staticState,
                            M01LoadedImageStackStaticState
                                .RequiredStorageBytes,
                            s_bootServices,
                            s_console,
                            s_serial,
                            &stageOneLedgerStorage->Range0,
                            ReservationCount,
                            ReservationCapacity))
                    {
                        Spin();
                    }
                }
#endif

#if M04_MANAGED_HEAP_PAGE_RANGE_PROBE
                if (!M01StageTwoAdapter.ValidateManagedHeapBeforeBootPlan(s_console, s_serial))
                    Spin();
#endif
#if M04_RHP_NEW_FAST_PROBE
                M01StageTwoAdapter.RunRhpNewFastAfterZeroCheckpoint();
#endif
#if M04_TYPE_MANAGER_MODULE_REGISTRATION_PROBE
                // Default capture on authority failure is rejected by the same
                // adapter; no reconstructed image authority is fabricated.
                TryGetOriginalTypeManagerImage(out EfiLoadedImageRangeCapturePlan typeManagerCapture,
                    out M01LoadedImageVirtualMappingEvidence typeManagerMapping);
                if (!OS.Runtime.NativeAotModuleRegistrationProbe.Run(typeManagerCapture, typeManagerMapping,
                        new EfiConsoleAdapter(s_console, s_serial)))
                    Spin();
#endif
                EfiEntry.WriteBootSequencePlan(
                    s_console,
                    s_serial);
#if (M03_FIRST_REAL_EXIT_BOOT_SERVICES_PROBE || CLROS_NORMAL_VERIFIED_EXIT)
                M03FirstExitAdapter.Run(s_console, s_serial, s_bootServices);
                Spin();
#endif
#if M02_PAGE_FAULT_CR2_ERROR_CODE_PROBE
                M02PageFaultAdapter.Run(s_console, s_serial, s_bootServices);
#endif
#if M02_DOUBLE_FAULT_IST_PROBE
                M02DoubleFaultIstAdapter.Run(s_console, s_serial, s_bootServices);
#endif
#if M02_UNEXPECTED_VECTOR_PANIC_PROBE
                M02UnexpectedVectorPanicAdapter.Run(s_console, s_serial, s_bootServices);
#endif
#if M02_UD2_VECTOR_STATE_PROBE
                if (!EfiEntry.PrepareM02Ud2VectorStateProbe(
                        s_console,
                        s_serial,
                        s_bootServices))
                {
                    Spin();
                }
                EfiEntry.EnterM02Ud2VectorStateProbe(
                    s_console,
                    s_serial,
                    s_bootServices);
#endif
#if M02_OWNED_GDT_TSS_TERMINAL_PROBE
                EfiEntry.EnterM02OwnedGdtTssTerminalProbe(
                    s_console,
                    s_serial,
                    s_bootServices);
#endif
                Spin();
            }
        }

        private static bool FailBeforeTransfer(
            EfiSimpleTextOutputProtocol* console,
            EfiSerialIoProtocol* serial,
            EfiBootServices* bootServices,
            M01StageOneAdapterStatus adapterStatus,
            ulong status,
            ulong checkpoint,
            ulong codeAddress,
            bool codeAllocated,
            ulong dataAddress,
            bool dataAllocated)
        {
            ulong dataCleanupStatus = NotAttemptedStatus;
            ulong codeCleanupStatus = NotAttemptedStatus;

            if (bootServices != null
                && bootServices->FreePages != null)
            {
                if (dataAllocated)
                {
                    dataCleanupStatus =
                        bootServices->FreePages(
                            dataAddress,
                            DataArenaPageCount);
                }

                if (codeAllocated)
                {
                    bool stackProbeCleared =
                        TryClearStackProbeBytes(
                            codeAddress);
                    if (stackProbeCleared)
                    {
                        codeCleanupStatus =
                            bootServices->FreePages(
                                codeAddress,
                                CodePageCount);
                    }
                    else
                    {
                        codeCleanupStatus =
                            NotAttemptedStatus - 1;
                    }
                }
            }

            WriteFailure(
                console,
                serial,
                adapterStatus,
                status,
                checkpoint,
                dataCleanupStatus,
                codeCleanupStatus);
            return true;
        }

        private static bool TryClearStackProbeBytes(
            ulong codeAddress)
        {
            if (!TryValidateAllocation(
                    codeAddress,
                    CodePageCount,
                    out ulong codeByteLength,
                    out _))
            {
                return true;
            }

            if (StackProbeEntryOffset
                    > codeByteLength - StackProbeByteLength
                || codeAddress
                    > ~0UL - StackProbeEntryOffset)
            {
                return false;
            }

            ulong stackProbeAddress =
                codeAddress + StackProbeEntryOffset;
            if (!System.Runtime.X64StackProbeRuntime
                    .TryClear(stackProbeAddress))
            {
                return false;
            }

            byte* probe = (byte*)stackProbeAddress;
            for (ulong index = 0;
                index < StackProbeByteLength;
                index++)
            {
                probe[index] = 0;
            }

            for (ulong index = 0;
                index < StackProbeByteLength;
                index++)
            {
                if (probe[index] != 0)
                {
                    return false;
                }
            }

            return true;
        }

        private static void WriteSuccessLine(
            EfiSimpleTextOutputProtocol* console,
            EfiSerialIoProtocol* serial,
            int lineIndex)
        {
            int length =
                BootSequencePlan
                    .GetM01StageOneSuccessLineLength(
                        lineIndex);
            for (int offset = 0;
                offset < length;
                offset++)
            {
                EfiEntry.Write(
                    console,
                    serial,
                    BootSequencePlan
                        .GetM01StageOneSuccessLineByte(
                            lineIndex,
                            offset));
            }

            EfiEntry.WriteCrLf(console, serial);
        }

        private static void WriteFailure(
            EfiSimpleTextOutputProtocol* console,
            EfiSerialIoProtocol* serial,
            M01StageOneAdapterStatus adapterStatus,
            ulong status,
            ulong checkpoint,
            ulong dataCleanupStatus,
            ulong codeCleanupStatus)
        {
            WriteFailurePrefix(console, serial, false);
            WriteFailureFields(
                console,
                serial,
                adapterStatus,
                status,
                checkpoint,
                dataCleanupStatus,
                codeCleanupStatus);
            WriteFailurePrefix(console, serial, true);
            WriteFailureFields(
                console,
                serial,
                adapterStatus,
                status,
                checkpoint,
                dataCleanupStatus,
                codeCleanupStatus);
        }

        private static void WriteFailurePrefix(
            EfiSimpleTextOutputProtocol* console,
            EfiSerialIoProtocol* serial,
            bool stagePrefix)
        {
            if (stagePrefix)
            {
                WriteAscii(
                    console,
                    serial,
                    (byte)'M',
                    (byte)'0',
                    (byte)'1',
                    (byte)'_',
                    (byte)'S',
                    (byte)'T',
                    (byte)'A',
                    (byte)'G');
                WriteAscii(
                    console,
                    serial,
                    (byte)'E',
                    (byte)'1',
                    (byte)'_',
                    (byte)'F',
                    (byte)'A',
                    (byte)'I',
                    (byte)'L',
                    (byte)' ');
                return;
            }

            WriteAscii(
                console,
                serial,
                (byte)'T',
                (byte)'E',
                (byte)'S',
                (byte)'T',
                (byte)'_',
                (byte)'F',
                (byte)'A',
                (byte)'I');
            WriteAscii(
                console,
                serial,
                (byte)'L',
                (byte)' ',
                (byte)'m',
                (byte)'0',
                (byte)'1',
                (byte)'.',
                (byte)'s',
                (byte)'t');
            WriteAscii(
                console,
                serial,
                (byte)'a',
                (byte)'g',
                (byte)'e',
                (byte)'1',
                (byte)' ',
                0,
                0,
                0);
        }

        private static void WriteFailureFields(
            EfiSimpleTextOutputProtocol* console,
            EfiSerialIoProtocol* serial,
            M01StageOneAdapterStatus adapterStatus,
            ulong status,
            ulong checkpoint,
            ulong dataCleanupStatus,
            ulong codeCleanupStatus)
        {
            WriteAscii(
                console,
                serial,
                (byte)'s',
                (byte)'t',
                (byte)'a',
                (byte)'t',
                (byte)'u',
                (byte)'s',
                (byte)'=',
                0);
            EfiEntry.WriteUnsignedDecimal(
                console,
                serial,
                status);
            WriteAscii(
                console,
                serial,
                (byte)' ',
                (byte)'a',
                (byte)'d',
                (byte)'a',
                (byte)'p',
                (byte)'t',
                (byte)'e',
                (byte)'r');
            WriteAscii(
                console,
                serial,
                (byte)'=',
                0,
                0,
                0,
                0,
                0,
                0,
                0);
            EfiEntry.WriteUnsignedDecimal(
                console,
                serial,
                (ulong)adapterStatus);
            WriteAscii(
                console,
                serial,
                (byte)' ',
                (byte)'c',
                (byte)'h',
                (byte)'e',
                (byte)'c',
                (byte)'k',
                (byte)'p',
                (byte)'o');
            WriteAscii(
                console,
                serial,
                (byte)'i',
                (byte)'n',
                (byte)'t',
                (byte)'=',
                0,
                0,
                0,
                0);
            EfiEntry.WriteUnsignedDecimal(
                console,
                serial,
                checkpoint);
            WriteAscii(
                console,
                serial,
                (byte)' ',
                (byte)'f',
                (byte)'r',
                (byte)'e',
                (byte)'e',
                (byte)'_',
                (byte)'d',
                (byte)'a');
            WriteAscii(
                console,
                serial,
                (byte)'t',
                (byte)'a',
                (byte)'=',
                0,
                0,
                0,
                0,
                0);
            EfiEntry.WriteUnsignedDecimal(
                console,
                serial,
                dataCleanupStatus);
            WriteAscii(
                console,
                serial,
                (byte)' ',
                (byte)'f',
                (byte)'r',
                (byte)'e',
                (byte)'e',
                (byte)'_',
                (byte)'c',
                (byte)'o');
            WriteAscii(
                console,
                serial,
                (byte)'d',
                (byte)'e',
                (byte)'=',
                0,
                0,
                0,
                0,
                0);
            EfiEntry.WriteUnsignedDecimal(
                console,
                serial,
                codeCleanupStatus);
            EfiEntry.WriteCrLf(console, serial);
        }

        private static void WriteAscii(
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
                EfiEntry.Write(console, serial, b0);
            if (b1 != 0)
                EfiEntry.Write(console, serial, b1);
            if (b2 != 0)
                EfiEntry.Write(console, serial, b2);
            if (b3 != 0)
                EfiEntry.Write(console, serial, b3);
            if (b4 != 0)
                EfiEntry.Write(console, serial, b4);
            if (b5 != 0)
                EfiEntry.Write(console, serial, b5);
            if (b6 != 0)
                EfiEntry.Write(console, serial, b6);
            if (b7 != 0)
                EfiEntry.Write(console, serial, b7);
        }

        private static bool RangesOverlap(
            ulong firstStart,
            ulong firstEndExclusive,
            ulong secondStart,
            ulong secondEndExclusive)
        {
            return firstStart < secondEndExclusive
                && secondStart < firstEndExclusive;
        }

        private static void Spin()
        {
            while (true)
            {
            }
        }

    }
#else
    private static partial class M01StageOneAdapter
    {
        public static bool Run(void* imageHandle, EfiSimpleTextOutputProtocol* console, EfiSerialIoProtocol* serial, EfiSystemTable* systemTable)
        {
            return false;
        }
    }
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

#endif
#if M04_CHECKED_REFERENCE_BARRIER_PROBE || M04_GC_STATIC_ROOT_MATERIALIZATION_PROBE || M04_X64_UNWIND_METADATA_PROBE

#endif
#if M04_MANAGED_HEAP_PAGE_RANGE_PROBE

#endif
}
#if M03_MEMORY_MAP_CAPTURE_PROBE

#endif
#if M03_MEMORY_MAP_CAPTURE_PROBE

#endif
#if (ACPI_PHYSICAL_DISCOVERY_PROBE || CLROS_NORMAL_VERIFIED_EXIT)

#endif
#pragma warning restore 649
