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
    // One observer around the original Capture algorithm. Firmware statuses are
    // recorded unchanged; Fault is independent and never represents firmware.
    private static partial class M03MemoryMapCaptureObserver
    {
        private const ulong Abort = 0x8000000000000015;
        private const ulong None = 0xFFFFFFFFFFFFFFFF;
        private const byte Guard = 0xA7;
        private const byte Prefill = 0xCD;
        private static Observation s_observation;

        private static bool Span(ulong address, ulong bytes)
        {
            return address != 0 && bytes != 0 && bytes <= None - address;
        }

        private static bool Separate(ulong a, ulong aBytes, ulong b, ulong bBytes)
        {
            return Span(a, aBytes) && Span(b, bBytes)
                && (a + aBytes <= b || b + bBytes <= a);
        }

        // This is arithmetic admission only. The actual static objects and the
        // original successful firmware allocation supply the storage authority.
        private static bool Storage(ulong observation, ulong guard, ulong payload)
        {
            return Span(observation, (ulong)sizeof(Observation))
                && Span(guard, (ulong)sizeof(OwnedMemoryMapBuffer))
                && (ulong)sizeof(OwnedMemoryMapBuffer) == OwnedMemoryMapBufferSize + 32UL
                && payload == guard + 16
                && Separate(observation, (ulong)sizeof(Observation),
                    guard, (ulong)sizeof(OwnedMemoryMapBuffer));
        }

        private static ulong Reject(Observation* o, ulong fault)
        {
            if (o->Fault == 0)
                o->Fault = fault;
            return Abort;
        }

        public static bool Prepare(EfiBootServices* services, OwnedMemoryMapBuffer* guard, byte* payload)
        {
            fixed (Observation* o = &s_observation)
            {
                if (!Storage((ulong)o, (ulong)guard, (ulong)payload))
                    return false;
                if (o->Phase != 0 || o->Fault != 0)
                {
                    Reject(o, 1);
                    return false;
                }
                if (services == null || services->GetMemoryMap == null || services->AllocatePool == null
                    || !Separate((ulong)services, (ulong)sizeof(EfiBootServices), (ulong)o, (ulong)sizeof(Observation))
                    || !Separate((ulong)services, (ulong)sizeof(EfiBootServices), (ulong)guard, (ulong)sizeof(OwnedMemoryMapBuffer)))
                {
                    Reject(o, 2);
                    return false;
                }
                o->ObservationAddress = (ulong)o;
                o->ObservationBytes = (ulong)sizeof(Observation);
                o->GuardAddress = (ulong)guard;
                o->GuardSpanBytes = (ulong)sizeof(OwnedMemoryMapBuffer);
                o->Destination = (ulong)payload;
                o->Capacity = OwnedMemoryMapBufferSize;
                o->BootServices = (ulong)services;
                o->GetMemoryMap = (ulong)services->GetMemoryMap;
                o->AllocatePool = (ulong)services->AllocatePool;
                o->FirstMismatch = None;
                o->Phase = 1;
                for (int i = 0; i < 16; i++)
                {
                    ((byte*)guard)[i] = Guard;
                    payload[OwnedMemoryMapBufferSize + i] = Guard;
                }
                for (int i = 0; i < OwnedMemoryMapBufferSize; i++)
                    payload[i] = Prefill;
                return true;
            }
        }

        private static bool OutsideStorage(Observation* o, ulong address, ulong bytes)
        {
            return Separate(address, bytes, o->ObservationAddress, o->ObservationBytes)
                && Separate(address, bytes, o->GuardAddress, o->GuardSpanBytes)
                && Separate(address, bytes, o->BootServices, (ulong)sizeof(EfiBootServices));
        }

        private static bool MapArguments(Observation* o, ulong size, ulong key, ulong stride, ulong version)
        {
            return OutsideStorage(o, size, 8) && OutsideStorage(o, key, 8)
                && OutsideStorage(o, stride, 8) && OutsideStorage(o, version, 4)
                && Separate(size, 8, key, 8) && Separate(size, 8, stride, 8)
                && Separate(size, 8, version, 4) && Separate(key, 8, stride, 8)
                && Separate(key, 8, version, 4) && Separate(stride, 8, version, 4);
        }

        private static bool OutsideArguments(Observation* o, ulong address, ulong bytes)
        {
            return Separate(address, bytes, o->SizeSlot, 8)
                && Separate(address, bytes, o->KeySlot, 8)
                && Separate(address, bytes, o->StrideSlot, 8)
                && Separate(address, bytes, o->VersionSlot, 4);
        }

        [System.Runtime.InteropServices.UnmanagedCallersOnly]
        public static ulong ForwardGetMemoryMap(ulong* size, void* map, ulong* key, ulong* stride, uint* version)
        {
            fixed (Observation* o = &s_observation)
            {
                if (o->Fault != 0 || (o->Phase != 1 && o->Phase != 5))
                    return Reject(o, 3);
                if (!MapArguments(o, (ulong)size, (ulong)key, (ulong)stride, (ulong)version))
                    return Reject(o, 4);
                bool query = o->Phase == 1;
                if (query)
                {
                    if (*size != 0 || map != null)
                        return Reject(o, 4);
                    o->SizeSlot = (ulong)size;
                    o->KeySlot = (ulong)key;
                    o->StrideSlot = (ulong)stride;
                    o->VersionSlot = (ulong)version;
                    o->QueryCapacity = *size;
                    o->QueryBuffer = (ulong)map;
                    o->QueryCalls++;
                    o->QueryEvent = ++o->EventCount;
                    o->Phase = 2;
                }
                else
                {
                    // Capture uses a separate capturedSize local. Admit its full
                    // extent before reading it; native slot reuse is not required.
                    if (o->PoolAdmitted != 1 || (ulong)map != o->Pool
                        || !Separate((ulong)size, 8, o->Pool, o->AllocationBytes)
                        || !Separate((ulong)size, 8, o->PoolSlot, 8)
                        || ((ulong)size != o->SizeSlot && !Separate((ulong)size, 8, o->SizeSlot, 8))
                        || (ulong)key != o->KeySlot || (ulong)stride != o->StrideSlot
                        || (ulong)version != o->VersionSlot || *size != o->AllocationBytes)
                        return Reject(o, 4);
                    o->FinalSizeSlot = (ulong)size;
                    o->FinalCapacity = *size;
                    o->FinalBuffer = (ulong)map;
                    o->FinalCalls++;
                    o->FinalEvent = ++o->EventCount;
                    o->Phase = 6;
                }
                ulong status = ((delegate* unmanaged<ulong*, void*, ulong*, ulong*, uint*, ulong>)o->GetMemoryMap)
                    (size, map, key, stride, version);
                if (query)
                {
                    o->QueryStatus = status;
                    o->RequiredBytes = *size;
                    o->QueryKey = *key;
                    o->QueryStride = *stride;
                    o->QueryVersion = *version;
                    o->Phase = 3;
                }
                else
                {
                    o->FinalStatus = status;
                    o->FinalBytes = *size;
                    o->FinalKey = *key;
                    o->FinalStride = *stride;
                    o->FinalVersion = *version;
                    o->Phase = 7;
                }
                // Preserve the original return even if a nested call set Fault.
                return status;
            }
        }

        [System.Runtime.InteropServices.UnmanagedCallersOnly]
        public static ulong ForwardAllocatePool(uint type, ulong bytes, void** buffer)
        {
            fixed (Observation* o = &s_observation)
            {
                if (o->Fault != 0 || o->Phase != 3 || o->QueryCalls != 1
                    || o->QueryStatus != UefiMemoryMapCapture.EfiBufferTooSmall)
                    return Reject(o, 3);
                if (!OutsideStorage(o, (ulong)buffer, 8) || !OutsideArguments(o, (ulong)buffer, 8)
                    || type != UefiMemoryMapCapture.EfiLoaderData || o->RequiredBytes == 0
                    || o->QueryStride == 0 || o->QueryStride > (None - o->RequiredBytes) / 8
                    || bytes != o->RequiredBytes + 8 * o->QueryStride)
                    return Reject(o, 4);
                o->PoolSlot = (ulong)buffer;
                o->AllocationType = type;
                o->AllocationBytes = bytes;
                o->AllocationCalls++;
                o->AllocationEvent = ++o->EventCount;
                o->Phase = 4;
                ulong status = ((delegate* unmanaged<uint, ulong, void**, ulong>)o->AllocatePool)(type, bytes, buffer);
                o->AllocationStatus = status;
                o->Pool = (ulong)*buffer;
                o->Phase = 5;
                // Failure/null remains the original Capture failure. A successful
                // but aliased extent is locally rejected before recapture/copy.
                if (status != 0 || *buffer == null)
                    return status;
                if (!OutsideStorage(o, o->Pool, bytes) || !OutsideArguments(o, o->Pool, bytes)
                    || !Separate(o->Pool, bytes, o->PoolSlot, 8))
                    return Reject(o, 5);
                o->PoolAdmitted = 1;
                return status;
            }
        }

        public static bool ReadBack(UefiMemoryMapCaptureResult result)
        {
            fixed (Observation* o = &s_observation)
            {
                o->CaptureStatus = (ulong)result.Status;
                o->CopyPlanStatus = (ulong)result.CopyPlanStatus;
                o->FirmwareStatus = result.FirmwareStatus;
                if (!result.Succeeded || o->Fault != 0)
                    return false;
                FirmwareMemoryMapRecord source = result.SourceRecord;
                FirmwareMemoryMapRecord owned = result.OwnedRecord;
                if (o->Phase != 7 || o->QueryCalls != 1 || o->AllocationCalls != 1 || o->FinalCalls != 1
                    || o->EventCount != 3 || o->QueryEvent != 1 || o->AllocationEvent != 2 || o->FinalEvent != 3
                    || o->QueryStatus != UefiMemoryMapCapture.EfiBufferTooSmall
                    || o->AllocationStatus != 0 || o->FinalStatus != 0 || o->PoolAdmitted != 1
                    || source.BufferAddress != o->Pool || source.BufferSize != o->AllocationBytes
                    || source.SourceBufferAddress != o->Pool || source.SourceBufferSize != o->AllocationBytes
                    || source.MapSize != o->FinalBytes || source.MapKey != o->FinalKey
                    || source.DescriptorSize != o->FinalStride || source.DescriptorVersion != o->FinalVersion
                    || source.DescriptorSize == 0 || source.MapSize == 0 || source.MapSize > o->Capacity
                    || source.MapSize > source.BufferSize || source.MapSize % source.DescriptorSize != 0
                    || source.DescriptorCount != source.MapSize / source.DescriptorSize || source.Flags != 0
                    || owned.BufferAddress != o->Destination || owned.BufferSize != o->Capacity
                    || owned.SourceBufferAddress != source.BufferAddress || owned.SourceBufferSize != source.BufferSize
                    || owned.MapSize != source.MapSize || owned.MapKey != source.MapKey
                    || owned.DescriptorSize != source.DescriptorSize || owned.DescriptorVersion != source.DescriptorVersion
                    || owned.DescriptorCount != source.DescriptorCount || owned.Flags != FirmwareMemoryMapRecord.FlagOwnedCopy
                    || result.AllocationSize != o->AllocationBytes || result.CopyPlan.RequiredDestinationBytes != source.MapSize
                    || result.CopyPlan.SourceAddress != source.BufferAddress || result.CopyPlan.SourceBufferSize != source.BufferSize
                    || result.CopyPlan.SourceMapSize != source.MapSize || result.CopyPlan.DestinationCapacity != o->Capacity
                    || result.CopyPlan.MapKey != source.MapKey || result.CopyPlan.DescriptorSize != source.DescriptorSize
                    || result.CopyPlan.DescriptorVersion != source.DescriptorVersion || result.CopyPlan.DescriptorCount != source.DescriptorCount)
                {
                    Reject(o, 6);
                    return false;
                }
                o->OwnedAddress = owned.BufferAddress;
                o->OwnedCapacity = owned.BufferSize;
                o->OwnedFlag = owned.Flags;
                o->DescriptorCount = owned.DescriptorCount;
                o->CopyBytes = result.CopyPlan.RequiredDestinationBytes;
                // No firmware call or output occurs between Capture's copy and
                // these full, bounded comparisons of the actual same buffers.
                for (ulong i = 0; i < source.MapSize; i++)
                {
                    o->ComparedBytes++;
                    if (((byte*)source.BufferAddress)[i] != ((byte*)owned.BufferAddress)[i])
                    {
                        if (o->MismatchBytes == 0)
                            o->FirstMismatch = i;
                        o->MismatchBytes++;
                    }
                }
                for (ulong i = 0; i < 16; i++)
                {
                    o->GuardBytes += 2;
                    if (((byte*)o->GuardAddress)[i] != Guard)
                        o->GuardMismatches++;
                    if (((byte*)o->Destination)[o->Capacity + i] != Guard)
                        o->GuardMismatches++;
                }
                for (ulong i = source.MapSize; i < o->Capacity; i++)
                {
                    o->TailBytes++;
                    if (((byte*)o->Destination)[i] != Prefill)
                        o->TailMismatches++;
                }
                if (o->MismatchBytes != 0 || o->GuardMismatches != 0 || o->TailMismatches != 0)
                {
                    Reject(o, 7);
                    return false;
                }
                o->Phase = 8;
                return true;
            }
        }

        public static bool Handoff(EfiMemoryMapSnapshot snapshot, BootHandoffRecord handoff)
        {
            fixed (Observation* o = &s_observation)
            {
                o->SnapshotKey = snapshot.MapKey;
                o->HandoffKey = handoff.MemoryMap.MapKey;
                FirmwareMemoryMapRecord map = handoff.MemoryMap;
                if (o->Phase != 8 || o->Fault != 0 || !snapshot.IsOwned
                    || (ulong)snapshot.Buffer != o->Destination || snapshot.BufferSize != o->Capacity
                    || (ulong)snapshot.SourceBuffer != o->Pool || snapshot.SourceBufferSize != o->AllocationBytes
                    || snapshot.MapSize != o->FinalBytes || snapshot.MapKey != o->FinalKey
                    || snapshot.DescriptorSize != o->FinalStride || snapshot.DescriptorVersion != o->FinalVersion
                    || snapshot.DescriptorCount != o->DescriptorCount
                    || map.BufferAddress != o->Destination || map.BufferSize != o->Capacity
                    || map.SourceBufferAddress != o->Pool || map.SourceBufferSize != o->AllocationBytes
                    || map.MapSize != o->FinalBytes || map.MapKey != o->FinalKey
                    || map.DescriptorSize != o->FinalStride || map.DescriptorVersion != o->FinalVersion
                    || map.DescriptorCount != o->DescriptorCount || map.Flags != o->OwnedFlag
                    || handoff.BootMode != BootHandoffRecord.BootModeUefi
                    || handoff.FirmwareServicesMode != BootHandoffRecord.FirmwareServicesAvailable)
                {
                    Reject(o, 8);
                    return false;
                }
                o->HandoffMatched = 1;
                return true;
            }
        }

        public static void Start(EfiSimpleTextOutputProtocol* console, EfiSerialIoProtocol* serial)
        {
            Literal(console, serial, 0);
            WriteCrLf(console, serial);
        }

        public static void Report(EfiSimpleTextOutputProtocol* console, EfiSerialIoProtocol* serial, bool succeeded)
        {
            fixed (Observation* o = &s_observation)
            {
                if (!succeeded)
                {
                    Literal(console, serial, 45);
                    Literal(console, serial, 46);
                    WriteUnsignedDecimal(console, serial, o->CaptureStatus);
                    Literal(console, serial, 47);
                    WriteUnsignedDecimal(console, serial, o->CopyPlanStatus);
                    Literal(console, serial, 2);
                    WriteUnsignedDecimal(console, serial, o->Fault);
                    Literal(console, serial, 48);
                    WriteUnsignedDecimal(console, serial, o->Phase);
                    Literal(console, serial, 49);
                    WriteUnsignedDecimal(console, serial, o->QueryCalls);
                    Literal(console, serial, 50);
                    Hex(console, serial, o->QueryStatus);
                    Literal(console, serial, 51);
                    WriteUnsignedDecimal(console, serial, o->AllocationCalls);
                    Literal(console, serial, 52);
                    Hex(console, serial, o->AllocationStatus);
                    Literal(console, serial, 53);
                    WriteUnsignedDecimal(console, serial, o->FinalCalls);
                    Literal(console, serial, 54);
                    Hex(console, serial, o->FinalStatus);
                    Literal(console, serial, 55);
                    Hex(console, serial, o->FirmwareStatus);
                    WriteCrLf(console, serial);
                    Literal(console, serial, 56);
                    WriteCrLf(console, serial);
                    return;
                }
                Literal(console, serial, 1);
                Literal(console, serial, 2);
                Hex(console, serial, o->ObservationAddress);
                Literal(console, serial, 3);
                WriteUnsignedDecimal(console, serial, o->ObservationBytes);
                Literal(console, serial, 4);
                Hex(console, serial, o->GuardAddress);
                Literal(console, serial, 5);
                WriteUnsignedDecimal(console, serial, o->GuardSpanBytes);
                Literal(console, serial, 6);
                Hex(console, serial, o->Destination);
                Literal(console, serial, 7);
                WriteUnsignedDecimal(console, serial, o->Capacity);
                Literal(console, serial, 8);
                Hex(console, serial, o->BootServices);
                Literal(console, serial, 9);
                Hex(console, serial, o->GetMemoryMap);
                Literal(console, serial, 10);
                Hex(console, serial, o->AllocatePool);
                WriteCrLf(console, serial);
                Literal(console, serial, 11);
                Literal(console, serial, 12);
                WriteUnsignedDecimal(console, serial, o->QueryCalls);
                Literal(console, serial, 13);
                WriteUnsignedDecimal(console, serial, o->QueryEvent);
                Literal(console, serial, 14);
                WriteUnsignedDecimal(console, serial, o->QueryCapacity);
                Literal(console, serial, 15);
                Hex(console, serial, o->QueryBuffer);
                Literal(console, serial, 16);
                Hex(console, serial, o->QueryStatus);
                Literal(console, serial, 17);
                WriteUnsignedDecimal(console, serial, o->RequiredBytes);
                Literal(console, serial, 18);
                Hex(console, serial, o->QueryKey);
                Literal(console, serial, 19);
                WriteUnsignedDecimal(console, serial, o->QueryStride);
                Literal(console, serial, 20);
                WriteUnsignedDecimal(console, serial, o->QueryVersion);
                WriteCrLf(console, serial);
                Literal(console, serial, 21);
                Literal(console, serial, 12);
                WriteUnsignedDecimal(console, serial, o->AllocationCalls);
                Literal(console, serial, 13);
                WriteUnsignedDecimal(console, serial, o->AllocationEvent);
                Literal(console, serial, 22);
                WriteUnsignedDecimal(console, serial, o->AllocationType);
                Literal(console, serial, 23);
                WriteUnsignedDecimal(console, serial, o->AllocationBytes);
                Literal(console, serial, 16);
                Hex(console, serial, o->AllocationStatus);
                Literal(console, serial, 24);
                Hex(console, serial, o->Pool);
                Literal(console, serial, 25);
                WriteUnsignedDecimal(console, serial, o->PoolAdmitted);
                WriteCrLf(console, serial);
                Literal(console, serial, 26);
                Literal(console, serial, 12);
                WriteUnsignedDecimal(console, serial, o->FinalCalls);
                Literal(console, serial, 13);
                WriteUnsignedDecimal(console, serial, o->FinalEvent);
                Literal(console, serial, 14);
                WriteUnsignedDecimal(console, serial, o->FinalCapacity);
                Literal(console, serial, 15);
                Hex(console, serial, o->FinalBuffer);
                Literal(console, serial, 16);
                Hex(console, serial, o->FinalStatus);
                Literal(console, serial, 23);
                WriteUnsignedDecimal(console, serial, o->FinalBytes);
                Literal(console, serial, 18);
                Hex(console, serial, o->FinalKey);
                Literal(console, serial, 19);
                WriteUnsignedDecimal(console, serial, o->FinalStride);
                Literal(console, serial, 20);
                WriteUnsignedDecimal(console, serial, o->FinalVersion);
                WriteCrLf(console, serial);
                Literal(console, serial, 27);
                Literal(console, serial, 28);
                Hex(console, serial, o->Pool);
                Literal(console, serial, 29);
                WriteUnsignedDecimal(console, serial, o->AllocationBytes);
                Literal(console, serial, 30);
                Hex(console, serial, o->OwnedAddress);
                Literal(console, serial, 7);
                WriteUnsignedDecimal(console, serial, o->OwnedCapacity);
                Literal(console, serial, 23);
                WriteUnsignedDecimal(console, serial, o->CopyBytes);
                Literal(console, serial, 31);
                WriteUnsignedDecimal(console, serial, o->DescriptorCount);
                Literal(console, serial, 32);
                WriteUnsignedDecimal(console, serial, o->OwnedFlag);
                Literal(console, serial, 33);
                WriteUnsignedDecimal(console, serial, o->ComparedBytes);
                Literal(console, serial, 34);
                WriteUnsignedDecimal(console, serial, o->MismatchBytes);
                Literal(console, serial, 35);
                Hex(console, serial, o->FirstMismatch);
                WriteCrLf(console, serial);
                Literal(console, serial, 36);
                Literal(console, serial, 23);
                WriteUnsignedDecimal(console, serial, o->GuardBytes);
                Literal(console, serial, 34);
                WriteUnsignedDecimal(console, serial, o->GuardMismatches);
                Literal(console, serial, 37);
                WriteUnsignedDecimal(console, serial, o->TailBytes);
                Literal(console, serial, 38);
                WriteUnsignedDecimal(console, serial, o->TailMismatches);
                WriteCrLf(console, serial);
                Literal(console, serial, 39);
                Literal(console, serial, 40);
                Hex(console, serial, o->SnapshotKey);
                Literal(console, serial, 41);
                Hex(console, serial, o->HandoffKey);
                Literal(console, serial, 32);
                WriteUnsignedDecimal(console, serial, o->OwnedFlag);
                Literal(console, serial, 42);
                WriteUnsignedDecimal(console, serial, o->HandoffMatched);
                WriteCrLf(console, serial);
                Literal(console, serial, 43);
                WriteCrLf(console, serial);
                Literal(console, serial, 44);
                WriteCrLf(console, serial);
            }
        }

        private static void Hex(EfiSimpleTextOutputProtocol* console, EfiSerialIoProtocol* serial, ulong value)
        {
            for (int shift = 60; shift >= 0; shift -= 4)
            {
                uint digit = (uint)((value >> shift) & 15);
                Write(console, serial, (byte)(digit < 10 ? digit + 48 : digit + 55));
            }
        }

        private static void Ascii(EfiSimpleTextOutputProtocol* console, EfiSerialIoProtocol* serial, ulong word, int bytes)
        {
            for (int i = 0; i < bytes; i++)
            {
                Write(console, serial, (byte)word);
                word >>= 8;
            }
        }

        private static void Literal(EfiSimpleTextOutputProtocol* console, EfiSerialIoProtocol* serial, int id)
        {
            switch (id)
            {
                case 0: // TEST_START m03.001.capture
                    Ascii(console, serial, 0x4154535F54534554, 8);
                    Ascii(console, serial, 0x302E33306D205452, 8);
                    Ascii(console, serial, 0x75747061632E3130, 8);
                    Ascii(console, serial, 0x0000000000006572, 2);
                    return;
                case 1: // M03_CAPTURE_AUTH
                    Ascii(console, serial, 0x545041435F33304D, 8);
                    Ascii(console, serial, 0x485455415F455255, 8);
                    return;
                case 2: //  observer=
                    Ascii(console, serial, 0x6576726573626F20, 8);
                    Ascii(console, serial, 0x0000000000003D72, 2);
                    return;
                case 3: //  observerBytes=
                    Ascii(console, serial, 0x6576726573626F20, 8);
                    Ascii(console, serial, 0x003D736574794272, 7);
                    return;
                case 4: //  guard=
                    Ascii(console, serial, 0x003D647261756720, 7);
                    return;
                case 5: //  guardSpan=
                    Ascii(console, serial, 0x7053647261756720, 8);
                    Ascii(console, serial, 0x00000000003D6E61, 3);
                    return;
                case 6: //  payload=
                    Ascii(console, serial, 0x64616F6C79617020, 8);
                    Ascii(console, serial, 0x000000000000003D, 1);
                    return;
                case 7: //  capacity=
                    Ascii(console, serial, 0x7469636170616320, 8);
                    Ascii(console, serial, 0x0000000000003D79, 2);
                    return;
                case 8: //  services=
                    Ascii(console, serial, 0x6563697672657320, 8);
                    Ascii(console, serial, 0x0000000000003D73, 2);
                    return;
                case 9: //  get=
                    Ascii(console, serial, 0x0000003D74656720, 5);
                    return;
                case 10: //  allocate=
                    Ascii(console, serial, 0x7461636F6C6C6120, 8);
                    Ascii(console, serial, 0x0000000000003D65, 2);
                    return;
                case 11: // M03_CAPTURE_QUERY
                    Ascii(console, serial, 0x545041435F33304D, 8);
                    Ascii(console, serial, 0x524555515F455255, 8);
                    Ascii(console, serial, 0x0000000000000059, 1);
                    return;
                case 12: //  calls=
                    Ascii(console, serial, 0x003D736C6C616320, 7);
                    return;
                case 13: //  order=
                    Ascii(console, serial, 0x003D726564726F20, 7);
                    return;
                case 14: //  inputBytes=
                    Ascii(console, serial, 0x79427475706E6920, 8);
                    Ascii(console, serial, 0x000000003D736574, 4);
                    return;
                case 15: //  buffer=
                    Ascii(console, serial, 0x3D72656666756220, 8);
                    return;
                case 16: //  status=
                    Ascii(console, serial, 0x3D73757461747320, 8);
                    return;
                case 17: //  required=
                    Ascii(console, serial, 0x6572697571657220, 8);
                    Ascii(console, serial, 0x0000000000003D64, 2);
                    return;
                case 18: //  key=
                    Ascii(console, serial, 0x0000003D79656B20, 5);
                    return;
                case 19: //  stride=
                    Ascii(console, serial, 0x3D65646972747320, 8);
                    return;
                case 20: //  version=
                    Ascii(console, serial, 0x6E6F697372657620, 8);
                    Ascii(console, serial, 0x000000000000003D, 1);
                    return;
                case 21: // M03_CAPTURE_ALLOC
                    Ascii(console, serial, 0x545041435F33304D, 8);
                    Ascii(console, serial, 0x4F4C4C415F455255, 8);
                    Ascii(console, serial, 0x0000000000000043, 1);
                    return;
                case 22: //  type=
                    Ascii(console, serial, 0x00003D6570797420, 6);
                    return;
                case 23: //  bytes=
                    Ascii(console, serial, 0x003D736574796220, 7);
                    return;
                case 24: //  pool=
                    Ascii(console, serial, 0x00003D6C6F6F7020, 6);
                    return;
                case 25: //  admitted=
                    Ascii(console, serial, 0x657474696D646120, 8);
                    Ascii(console, serial, 0x0000000000003D64, 2);
                    return;
                case 26: // M03_CAPTURE_FINAL
                    Ascii(console, serial, 0x545041435F33304D, 8);
                    Ascii(console, serial, 0x414E49465F455255, 8);
                    Ascii(console, serial, 0x000000000000004C, 1);
                    return;
                case 27: // M03_CAPTURE_COPY
                    Ascii(console, serial, 0x545041435F33304D, 8);
                    Ascii(console, serial, 0x59504F435F455255, 8);
                    return;
                case 28: //  source=
                    Ascii(console, serial, 0x3D656372756F7320, 8);
                    return;
                case 29: //  sourceBytes=
                    Ascii(console, serial, 0x42656372756F7320, 8);
                    Ascii(console, serial, 0x0000003D73657479, 5);
                    return;
                case 30: //  owned=
                    Ascii(console, serial, 0x003D64656E776F20, 7);
                    return;
                case 31: //  count=
                    Ascii(console, serial, 0x003D746E756F6320, 7);
                    return;
                case 32: //  flag=
                    Ascii(console, serial, 0x00003D67616C6620, 6);
                    return;
                case 33: //  compared=
                    Ascii(console, serial, 0x657261706D6F6320, 8);
                    Ascii(console, serial, 0x0000000000003D64, 2);
                    return;
                case 34: //  mismatches=
                    Ascii(console, serial, 0x6374616D73696D20, 8);
                    Ascii(console, serial, 0x000000003D736568, 4);
                    return;
                case 35: //  firstMismatch=
                    Ascii(console, serial, 0x694D747372696620, 8);
                    Ascii(console, serial, 0x003D686374616D73, 7);
                    return;
                case 36: // M03_CAPTURE_GUARDS
                    Ascii(console, serial, 0x545041435F33304D, 8);
                    Ascii(console, serial, 0x524155475F455255, 8);
                    Ascii(console, serial, 0x0000000000005344, 2);
                    return;
                case 37: //  tailBytes=
                    Ascii(console, serial, 0x7479426C69617420, 8);
                    Ascii(console, serial, 0x00000000003D7365, 3);
                    return;
                case 38: //  tailMismatches=
                    Ascii(console, serial, 0x73694D6C69617420, 8);
                    Ascii(console, serial, 0x3D7365686374616D, 8);
                    return;
                case 39: // M03_CAPTURE_HANDOFF
                    Ascii(console, serial, 0x545041435F33304D, 8);
                    Ascii(console, serial, 0x444E41485F455255, 8);
                    Ascii(console, serial, 0x000000000046464F, 3);
                    return;
                case 40: //  snapshotKey=
                    Ascii(console, serial, 0x6F687370616E7320, 8);
                    Ascii(console, serial, 0x0000003D79654B74, 5);
                    return;
                case 41: //  handoffKey=
                    Ascii(console, serial, 0x66666F646E616820, 8);
                    Ascii(console, serial, 0x000000003D79654B, 4);
                    return;
                case 42: //  matched=
                    Ascii(console, serial, 0x6465686374616D20, 8);
                    Ascii(console, serial, 0x000000000000003D, 1);
                    return;
                case 43: // CLROS_M03_001_READY firmwareAvailable=true exitBootServices=false
                    Ascii(console, serial, 0x304D5F534F524C43, 8);
                    Ascii(console, serial, 0x45525F3130305F33, 8);
                    Ascii(console, serial, 0x6D72696620594441, 8);
                    Ascii(console, serial, 0x6961764165726177, 8);
                    Ascii(console, serial, 0x72743D656C62616C, 8);
                    Ascii(console, serial, 0x4274697865206575, 8);
                    Ascii(console, serial, 0x6976726553746F6F, 8);
                    Ascii(console, serial, 0x736C61663D736563, 8);
                    Ascii(console, serial, 0x0000000000000065, 1);
                    return;
                case 44: // TEST_PASS m03.001.capture
                    Ascii(console, serial, 0x5341505F54534554, 8);
                    Ascii(console, serial, 0x30302E33306D2053, 8);
                    Ascii(console, serial, 0x7275747061632E31, 8);
                    Ascii(console, serial, 0x0000000000000065, 1);
                    return;
                case 45: // M03_CAPTURE_FAILURE
                    Ascii(console, serial, 0x545041435F33304D, 8);
                    Ascii(console, serial, 0x4C4941465F455255, 8);
                    Ascii(console, serial, 0x0000000000455255, 3);
                    return;
                case 46: //  capture=
                    Ascii(console, serial, 0x6572757470616320, 8);
                    Ascii(console, serial, 0x000000000000003D, 1);
                    return;
                case 47: //  copy=
                    Ascii(console, serial, 0x00003D79706F6320, 6);
                    return;
                case 48: //  phase=
                    Ascii(console, serial, 0x003D657361687020, 7);
                    return;
                case 49: //  queryCalls=
                    Ascii(console, serial, 0x6143797265757120, 8);
                    Ascii(console, serial, 0x000000003D736C6C, 4);
                    return;
                case 50: //  queryStatus=
                    Ascii(console, serial, 0x7453797265757120, 8);
                    Ascii(console, serial, 0x0000003D73757461, 5);
                    return;
                case 51: //  allocationCalls=
                    Ascii(console, serial, 0x7461636F6C6C6120, 8);
                    Ascii(console, serial, 0x736C6C61436E6F69, 8);
                    Ascii(console, serial, 0x000000000000003D, 1);
                    return;
                case 52: //  allocationStatus=
                    Ascii(console, serial, 0x7461636F6C6C6120, 8);
                    Ascii(console, serial, 0x75746174536E6F69, 8);
                    Ascii(console, serial, 0x0000000000003D73, 2);
                    return;
                case 53: //  finalCalls=
                    Ascii(console, serial, 0x61436C616E696620, 8);
                    Ascii(console, serial, 0x000000003D736C6C, 4);
                    return;
                case 54: //  finalStatus=
                    Ascii(console, serial, 0x74536C616E696620, 8);
                    Ascii(console, serial, 0x0000003D73757461, 5);
                    return;
                case 55: //  captureFirmwareStatus=
                    Ascii(console, serial, 0x6572757470616320, 8);
                    Ascii(console, serial, 0x657261776D726946, 8);
                    Ascii(console, serial, 0x003D737574617453, 7);
                    return;
                case 56: // TEST_FAIL m03.001.capture capture_failed
                    Ascii(console, serial, 0x4941465F54534554, 8);
                    Ascii(console, serial, 0x30302E33306D204C, 8);
                    Ascii(console, serial, 0x7275747061632E31, 8);
                    Ascii(console, serial, 0x7275747061632065, 8);
                    Ascii(console, serial, 0x64656C6961665F65, 8);
                    return;
            }
        }
    }
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
