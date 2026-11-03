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
    // One terminal invocation. Host preparation results never attest installed CPU state.
    private static class M02PageFaultAdapter
    {
        private static byte s_state;
        private const ulong Page = 4096;
        private const ulong LowEnd = 0x0000800000000000;
        private const ulong CodeBytes = 3 * Page, DataBytes = 10 * Page, TableBytes = 43 * Page;

        internal static void Run(EfiSimpleTextOutputProtocol* console,
            EfiSerialIoProtocol* serial, EfiBootServices* services)
        {
            if (s_state != 0)
            {
                Failure(console, serial);
                Spin();
            }
            s_state = 1;
            ulong c = 0, d = 0, t = 0;
            bool ownedC = false, ownedD = false, ownedT = false;
            if (services == null || services->AllocatePages == null || services->FreePages == null)
            {
                Failure(console, serial);
                Spin();
            }
            ownedC = services->AllocatePages(0, 1, 3, &c) == 0;
            if (!ownedC || !Range(c, CodeBytes, 52))
            {
                FailBeforeEntry(console, serial, services, c, ownedC, d, ownedD, t, ownedT);
                Spin();
            }
            ownedD = services->AllocatePages(0, 2, 10, &d) == 0;
            if (!ownedD || !Range(d, DataBytes, 52) || !Disjoint(c, CodeBytes, d, DataBytes))
            {
                FailBeforeEntry(console, serial, services, c, ownedC, d, ownedD, t, ownedT);
                Spin();
            }
            ownedT = services->AllocatePages(0, 2, 43, &t) == 0;
            if (!ownedT || !Range(t, TableBytes, 52) || !Disjoint(c, CodeBytes, t, TableBytes) || !Disjoint(d, DataBytes, t, TableBytes))
            {
                FailBeforeEntry(console, serial, services, c, ownedC, d, ownedD, t, ownedT);
                Spin();
            }

            var observation = default(OS.Kernel.Memory.X64PagingModePreflightObservation);
            var scratch = default(OS.Kernel.Memory.X64PagingModePreflightResult);
            ulong pat = 0;
            OS.Kernel.Diagnostics.SeparatePageFaultMappingRange* ranges = stackalloc OS.Kernel.Diagnostics.SeparatePageFaultMappingRange[6];
            OS.Kernel.Memory.X64PageTableImagePage* pages = stackalloc OS.Kernel.Memory.X64PageTableImagePage[43];
            // Validate every caller-owned temporary before the first query-code or bulk write.
            if (!ScratchBuffers((ulong)&observation, (ulong)&scratch, (ulong)&pat,
                (ulong)ranges, (ulong)pages, c, d, t))
            {
                FailBeforeEntry(console, serial, services, c, ownedC, d, ownedD, t, ownedT);
                Spin();
            }
            if (!Query(c, d, t, &observation, &scratch, &pat))
            {
                FailBeforeEntry(console, serial, services, c, ownedC, d, ownedD, t, ownedT);
                Spin();
            }
            if (!Range(c, CodeBytes, observation.PhysicalAddressWidth) ||
                !Range(d, DataBytes, observation.PhysicalAddressWidth) || !Range(t, TableBytes, observation.PhysicalAddressWidth))
            {
                FailBeforeEntry(console, serial, services, c, ownedC, d, ownedD, t, ownedT);
                Spin();
            }
            Zero((byte*)c, CodeBytes);
            Zero((byte*)d, DataBytes);
            Zero((byte*)t, TableBytes);
            if (!Prepare(c, d, t, observation, pat, ranges, pages, out var request))
            {
                FailBeforeEntry(console, serial, services, c, ownedC, d, ownedD, t, ownedT);
                Spin();
            }

            // Fresh full Audit binds the actual backing and exact derived ranges immediately before entry.
            // The frozen summary alone has no C/D identity or content fingerprint.
            FillRanges(ranges, c, d);
            var current = OS.Kernel.Diagnostics.SeparateDisabledDefaultPageFaultPageTables.Audit(
                request.Image.Request, ranges, 6, 6, pages, 43, (ulong*)t, 43 * 512);
            var fresh = new OS.Kernel.Diagnostics.SeparatePageFaultTransitionRequest(c, d, t, observation, pat, current);
            if (!ImageMatches(current, t) || !Intent(current, c) ||
                !ReadBack(c, d, fresh) ||
                X64InstructionPublication.Serialize() != X64InstructionPublication.ExpectedSuccessCookie)
            {
                FailBeforeEntry(console, serial, services, c, ownedC, d, ownedD, t, ownedT);
                Spin();
            }
            s_state = 2;
            // No pre-entry TEST_START: the closed stream emits it only after the hardware capture.
            delegate* unmanaged<byte*, void> enter = (delegate* unmanaged<byte*, void>)(c + Page + OS.Kernel.Diagnostics.SeparateDisabledDefaultPageFaultTransition.EntryOffset);
            enter((byte*)(d + Page));
            // An impossible return cannot authorize firmware cleanup or further managed callbacks.
            while (true)
            {
            }
        }

        internal static bool Range(ulong address, ulong size, byte width)
            => width >= 36 && width <= 52 && address != 0 && (address & 4095) == 0 &&
                address < LowEnd && size < LowEnd - address &&
                address < (1UL << width) && size < (1UL << width) - address;

        internal static bool Disjoint(ulong a, ulong an, ulong b, ulong bn)
            => a <= ulong.MaxValue - an && b <= ulong.MaxValue - bn && (a + an <= b || b + bn <= a);

        internal static byte CleanupPlan(ulong c, bool ownedC, ulong d, bool ownedD, ulong t, bool ownedT)
        {
            byte result = ownedT ? (byte)4 : (byte)0;
            if (ownedD && (!ownedT || Disjoint(d, DataBytes, t, TableBytes)))
                result |= 2;
            if (ownedC && (!ownedT || Disjoint(c, CodeBytes, t, TableBytes)) &&
                (!ownedD || Disjoint(c, CodeBytes, d, DataBytes)))
                result |= 1;
            return result;
        }

        private static void FailBeforeEntry(EfiSimpleTextOutputProtocol* console,
            EfiSerialIoProtocol* serial, EfiBootServices* services,
            ulong c, bool ownedC, ulong d, bool ownedD, ulong t, bool ownedT)
        {
            byte plan = CleanupPlan(c, ownedC, d, ownedD, t, ownedT);
            if ((plan & 4) != 0)
                _ = services->FreePages(t, 43);
            if ((plan & 2) != 0)
                _ = services->FreePages(d, 10);
            if ((plan & 1) != 0)
                _ = services->FreePages(c, 3);
            s_state = 3;
            Failure(console, serial);
        }

        internal static bool Mode(OS.Kernel.Memory.X64PagingModePreflightObservation observation, ulong pat,
            out OS.Kernel.Memory.X64PageTableEncodingPreconditions conditions)
        {
            conditions = default;
            if (!observation.IsInitialized || observation.CpuidMaximumExtendedLeaf < 0x80000008 ||
                observation.PhysicalAddressWidth < 36 || observation.PhysicalAddressWidth > 52 ||
                (observation.Cr0 & 0x80010001) != 0x80010001 || (observation.RawCr4 & 0x20) == 0 ||
                (observation.RawCr4 & 0x01821000) != 0 || (observation.Ia32Efer & 0xd00) != 0xd00 ||
                (observation.Rflags & 0x502) != 2 ||
                OS.Kernel.Memory.X64PatMappingToken.TryCreate(pat, 0, 255, 255, out var mapping) != OS.Kernel.Memory.X64PageTableEntryStatus.Success)
                return false;
            conditions = OS.Kernel.Memory.X64PageTableEncodingPreconditions.CreateObserved(observation.PhysicalAddressWidth,
                true, true, false, false, mapping);
            return conditions.Validate() == OS.Kernel.Memory.X64PageTableEntryStatus.Success;
        }

        private static bool Query(ulong c, ulong d, ulong t, OS.Kernel.Memory.X64PagingModePreflightObservation* observation,
            OS.Kernel.Memory.X64PagingModePreflightResult* scratch, ulong* pat)
        {
            if (!ScratchStorage((ulong)observation, 56, c, d, t) || !ScratchStorage((ulong)scratch, 96, c, d, t) ||
                !ScratchStorage((ulong)pat, 8, c, d, t) || !Disjoint((ulong)observation, 56, (ulong)scratch, 96) ||
                !Disjoint((ulong)observation, 56, (ulong)pat, 8) || !Disjoint((ulong)scratch, 96, (ulong)pat, 8))
                return false;
            var materialized = OS.Kernel.Memory.X64PagingModePreflightStubPlan.TryMaterialize((byte*)c, Page, 0, 111, observation, 56, scratch, 96);
            var readback = OS.Kernel.Memory.X64PagingModePreflightStubPlan.TryValidateMaterialized((byte*)c, Page, 0, 111, observation, 56, scratch, 96);
            if (!materialized.Succeeded || !readback.Succeeded)
                return false;
            for (ulong i = 0; i < 49; i++)
                ((byte*)c)[128 + i] = GetPatReaderByte(i);
            for (ulong i = 0; i < 49; i++)
                if (((byte*)c)[128 + i] != GetPatReaderByte(i))
                    return false;
            if (X64InstructionPublication.Serialize() != X64InstructionPublication.ExpectedSuccessCookie)
                return false;
            ((delegate* unmanaged<OS.Kernel.Memory.X64PagingModePreflightObservation*, void>)c)(observation);
            if (!observation->IsInitialized || ((delegate* unmanaged<ulong*, uint>)(c + 128))(pat) != 1)
                return false;
            if (PhysicalAddress.TryCreateFromUefiBoundary(t, out var root) != AddressValueStatus.Success)
                return false;
            var request = OS.Kernel.Memory.X64PagingModePreflightRequest.CreateEnabled(root, root, TableBytes);
            var evaluated = OS.Kernel.Memory.X64PagingModePreflightStubPlan.Evaluate(&request, 1, 1, observation, 1, 1, scratch, 1, 1);
            return evaluated.Succeeded && scratch->Succeeded && Mode(*observation, *pat, out _);
        }

        internal static void FillRanges(OS.Kernel.Diagnostics.SeparatePageFaultMappingRange* ranges, ulong c, ulong d)
        {
            ranges[0] = new(c, 3, OS.Kernel.Memory.PageProtection.Read | OS.Kernel.Memory.PageProtection.Execute);
            ranges[1] = new(d, 2, OS.Kernel.Memory.PageProtection.Read | OS.Kernel.Memory.PageProtection.Write);
            ranges[2] = new(d + 0x3000, 1, OS.Kernel.Memory.PageProtection.Read | OS.Kernel.Memory.PageProtection.Write);
            ranges[3] = new(d + 0x5000, 1, OS.Kernel.Memory.PageProtection.Read | OS.Kernel.Memory.PageProtection.Write);
            ranges[4] = new(d + 0x7000, 1, OS.Kernel.Memory.PageProtection.Read | OS.Kernel.Memory.PageProtection.Write);
            ranges[5] = new(d + 0x9000, 1, OS.Kernel.Memory.PageProtection.Read | OS.Kernel.Memory.PageProtection.Write);
        }

        internal static bool Prepare(ulong c, ulong d, ulong t, OS.Kernel.Memory.X64PagingModePreflightObservation observation,
            ulong pat, OS.Kernel.Diagnostics.SeparatePageFaultMappingRange* ranges, OS.Kernel.Memory.X64PageTableImagePage* pages,
            out OS.Kernel.Diagnostics.SeparatePageFaultTransitionRequest request)
        {
            request = default;
            if (!Mode(observation, pat, out var conditions) || !Range(c, CodeBytes, observation.PhysicalAddressWidth) ||
                !Range(d, DataBytes, observation.PhysicalAddressWidth) || !Range(t, TableBytes, observation.PhysicalAddressWidth) ||
                !Disjoint(c, CodeBytes, d, DataBytes) || !Disjoint(c, CodeBytes, t, TableBytes) || !Disjoint(d, DataBytes, t, TableBytes))
                return false;
            if (!ScratchStorage((ulong)ranges, 6UL * (ulong)sizeof(OS.Kernel.Diagnostics.SeparatePageFaultMappingRange), c, d, t) ||
                !ScratchStorage((ulong)pages, 43UL * (ulong)sizeof(OS.Kernel.Memory.X64PageTableImagePage), c, d, t) ||
                !Disjoint((ulong)ranges, 6UL * (ulong)sizeof(OS.Kernel.Diagnostics.SeparatePageFaultMappingRange),
                    (ulong)pages, 43UL * (ulong)sizeof(OS.Kernel.Memory.X64PageTableImagePage)))
                return false;
            if (PhysicalAddress.TryCreateFromUefiBoundary(t, out var root) != AddressValueStatus.Success)
                return false;
            var arena = new OS.Kernel.Diagnostics.GuardedIstStackArenaRequest(d + 0x2000, 0x6000, Page, Page, d, true, false);
            var initialized = OS.Kernel.Diagnostics.GuardedISTStacksDoubleFaultPageFaultNMI.Initialize(arena, out var state);
            if (!initialized.Succeeded)
                return false;
            var stacks = OS.Kernel.Diagnostics.GuardedISTStacksDoubleFaultPageFaultNMI.Allocate(ref state);
            if (!stacks.Succeeded || !StackMatches(stacks.DoubleFault, OS.Kernel.Diagnostics.GuardedIstStackKind.DoubleFault, 1, d + 0x2000) ||
                !StackMatches(stacks.PageFault, OS.Kernel.Diagnostics.GuardedIstStackKind.PageFault, 2, d + 0x4000) ||
                !StackMatches(stacks.NonMaskableInterrupt, OS.Kernel.Diagnostics.GuardedIstStackKind.NonMaskableInterrupt, 3, d + 0x6000))
                return false;
            if (!ZeroReadBack((byte*)d + 0x2000, 0x8000) || !ZeroReadBack((byte*)t, TableBytes))
                return false;
            FillRanges(ranges, c, d);
            var tableRequest = new OS.Kernel.Diagnostics.SeparatePageFaultPageTableRequest(root, 43, conditions);
            var image = OS.Kernel.Diagnostics.SeparateDisabledDefaultPageFaultPageTables.Build(tableRequest, ranges, 6, 6, pages, 43, (ulong*)t, 43 * 512);
            if (!ImageMatches(image, t))
                return false;
            var vector = OS.Kernel.Diagnostics.NormalizeErrorCodeNonErrorCodeInterruptFrame.Materialize((byte*)c, Page, c, c + Page);
            var idt = OS.Kernel.Diagnostics.PopulateAllN256IDTVectorsOwnedGatesNever.Materialize((byte*)d, Page, d, c);
            request = new(c, d, t, observation, pat, image);
            var context = OS.Kernel.Diagnostics.SeparateDisabledDefaultPageFaultTransition.PrepareContext((byte*)(d + Page), 4096, request);
            var stream = OS.Kernel.Diagnostics.SeparateDisabledDefaultPageFaultTransition.EmitStream((byte*)(c + Page), 8192, (byte*)(d + Page), 4096, request);
            var read = OS.Kernel.Diagnostics.SeparateDisabledDefaultPageFaultTransition.ValidateStreamReadBack((byte*)(c + Page), 8192, (byte*)(d + Page), 4096, request);
            return VectorMatches(vector, OS.Kernel.Diagnostics.NormalizeInterruptFramePageOperation.Materialize, c, true) &&
                IdtMatches(idt, OS.Kernel.Diagnostics.OwnedIdtGateTableOperation.Materialize, d, c, true) &&
                TransitionMatches(context, OS.Kernel.Diagnostics.SeparatePageFaultTransitionOperation.PrepareContext, 4096, 0) &&
                TransitionMatches(stream, OS.Kernel.Diagnostics.SeparatePageFaultTransitionOperation.EmitStream, 8192, 0) &&
                TransitionMatches(read, OS.Kernel.Diagnostics.SeparatePageFaultTransitionOperation.ValidateStreamReadBack, 0, 8192) &&
                context.ContextFingerprint == stream.ContextFingerprint && stream.ContextFingerprint == read.ContextFingerprint &&
                stream.StreamFingerprint == read.StreamFingerprint && ReadBack(c, d, request) && Intent(image, c);
        }

        internal static bool ScratchBuffers(ulong observation, ulong scratch, ulong pat,
            ulong ranges, ulong pages, ulong c, ulong d, ulong t)
        {
            const ulong observationBytes = 56, scratchBytes = 96, patBytes = 8;
            ulong rangeBytes = 6UL * (ulong)sizeof(OS.Kernel.Diagnostics.SeparatePageFaultMappingRange);
            ulong pageBytes = 43UL * (ulong)sizeof(OS.Kernel.Memory.X64PageTableImagePage);
            return ScratchStorage(observation, observationBytes, c, d, t) &&
                ScratchStorage(scratch, scratchBytes, c, d, t) && ScratchStorage(pat, patBytes, c, d, t) &&
                ScratchStorage(ranges, rangeBytes, c, d, t) && ScratchStorage(pages, pageBytes, c, d, t) &&
                Disjoint(observation, observationBytes, scratch, scratchBytes) &&
                Disjoint(observation, observationBytes, pat, patBytes) &&
                Disjoint(observation, observationBytes, ranges, rangeBytes) &&
                Disjoint(observation, observationBytes, pages, pageBytes) &&
                Disjoint(scratch, scratchBytes, pat, patBytes) && Disjoint(scratch, scratchBytes, ranges, rangeBytes) &&
                Disjoint(scratch, scratchBytes, pages, pageBytes) && Disjoint(pat, patBytes, ranges, rangeBytes) &&
                Disjoint(pat, patBytes, pages, pageBytes) && Disjoint(ranges, rangeBytes, pages, pageBytes);
        }

        private static bool ScratchStorage(ulong address, ulong size, ulong c, ulong d, ulong t)
            => address != 0 && (address & 7) == 0 && address < LowEnd && size < LowEnd - address &&
                Disjoint(address, size, c, CodeBytes) && Disjoint(address, size, d, DataBytes) && Disjoint(address, size, t, TableBytes);

        private static bool Intent(OS.Kernel.Diagnostics.SeparatePageFaultPageTableResult image, ulong c)
        {
            var nullRead = OS.Kernel.Diagnostics.NullPageFaultProbePlan.Plan(OS.Kernel.Diagnostics.NullPageFaultProbeRequest.CreateDisabled(image.PageZero, true, true));
            var fault = OS.Kernel.Diagnostics.SeparateDisabledDefaultPageFaultCR2ErrorCode.Plan(OS.Kernel.Diagnostics.SeparatePageFaultRequest.CreateDisabled(
                nullRead, c + Page + OS.Kernel.Diagnostics.SeparateDisabledDefaultPageFaultTransition.FaultOffset, true, true, true, true));
            return nullRead.Status == OS.Kernel.Diagnostics.NullPageFaultProbePlanStatus.ReadyDisabled && fault.Status == OS.Kernel.Diagnostics.SeparatePageFaultStatus.ReadyDisabled;
        }

        internal static bool ImageMatches(OS.Kernel.Diagnostics.SeparatePageFaultPageTableResult image, ulong t)
            => image.Succeeded && image.RangeCount == 6 && image.MappedPages == 52 && image.UsedTablePages >= 4 && image.UsedTablePages <= 43 &&
                image.Request.ArenaPageCount == 43 && image.Request.ArenaStart.TryGetRawValueForHardwareRegisterBoundary(out ulong root) == AddressValueStatus.Success && root == t;

        internal static bool ReadBack(ulong c, ulong d, OS.Kernel.Diagnostics.SeparatePageFaultTransitionRequest request)
            => VectorMatches(OS.Kernel.Diagnostics.NormalizeErrorCodeNonErrorCodeInterruptFrame.ValidateReadBack((byte*)c, Page, c, c + Page),
                    OS.Kernel.Diagnostics.NormalizeInterruptFramePageOperation.ValidateReadBack, c, false) &&
                IdtMatches(OS.Kernel.Diagnostics.PopulateAllN256IDTVectorsOwnedGatesNever.ValidateReadBack((byte*)d, Page, d, c),
                    OS.Kernel.Diagnostics.OwnedIdtGateTableOperation.ValidateReadBack, d, c, false) &&
                TransitionMatches(OS.Kernel.Diagnostics.SeparateDisabledDefaultPageFaultTransition.ValidateStreamReadBack((byte*)(c + Page), 8192, (byte*)(d + Page), 4096, request),
                    OS.Kernel.Diagnostics.SeparatePageFaultTransitionOperation.ValidateStreamReadBack, 0, 8192) && ZeroReadBack((byte*)d + 0x2000, 0x8000);

        private static bool TransitionMatches(OS.Kernel.Diagnostics.SeparatePageFaultTransitionResult result,
            OS.Kernel.Diagnostics.SeparatePageFaultTransitionOperation operation, int written, int compared)
            => result.Succeeded && result.Operation == operation && result.BytesWritten == written && result.BytesCompared == compared &&
                result.FirstMismatchOffset == -1 && result.ContextFingerprint != 0 &&
                !result.GuestExecution && !result.HardwareStateCaptured && !result.FaultTriggered && !result.PostExitBootServices;

        private static bool StackMatches(OS.Kernel.Diagnostics.GuardedIstStackDescriptor stack, OS.Kernel.Diagnostics.GuardedIstStackKind kind, byte index, ulong guard)
            => stack.IsUsable && stack.Kind == kind && stack.IstIndex == index && stack.GuardBaseAddress == guard &&
                stack.GuardSizeBytes == Page && stack.StackBaseAddress == guard + Page && stack.StackSizeBytes == Page &&
                stack.StackTopAddress == guard + 2 * Page && !stack.GuardPagePresent && stack.StackWritable && !stack.StackExecutable;

        private static void Zero(byte* address, ulong count)
        {
            for (ulong i = 0; i < count; i++)
                address[i] = 0;
        }
        private static bool ZeroReadBack(byte* address, ulong count)
        {
            for (ulong i = 0; i < count; i++)
                if (address[i] != 0)
                    return false;
            return true;
        }
        private static void Spin()
        {
            while (true)
            {
            }
        }
        private static byte GetPatReaderByte(ulong index)
        {
            return index switch
            {
                0 => 0xf3,
                1 => 0x0f,
                2 => 0x1e,
                3 => 0xfa,
                4 => 0x53,
                5 => 0x49,
                6 => 0x89,
                7 => 0xc8,
                8 => 0xb8,
                9 => 0x01,
                10 => 0x00,
                11 => 0x00,
                12 => 0x00,
                13 => 0x0f,
                14 => 0xa2,
                15 => 0x0f,
                16 => 0xba,
                17 => 0xe2,
                18 => 0x10,
                19 => 0x73,
                20 => 0x18,
                21 => 0xb9,
                22 => 0x77,
                23 => 0x02,
                24 => 0x00,
                25 => 0x00,
                26 => 0x0f,
                27 => 0x32,
                28 => 0x48,
                29 => 0xc1,
                30 => 0xe2,
                31 => 0x20,
                32 => 0x48,
                33 => 0x09,
                34 => 0xd0,
                35 => 0x49,
                36 => 0x89,
                37 => 0x00,
                38 => 0xb8,
                39 => 0x01,
                40 => 0x00,
                41 => 0x00,
                42 => 0x00,
                43 => 0x5b,
                44 => 0xc3,
                45 => 0x31,
                46 => 0xc0,
                47 => 0x5b,
                48 => 0xc3,
                _ => 0
            };
        }

        private static bool VectorMatches(
            OS.Kernel.Diagnostics.NormalizeInterruptFramePageResult result,
            OS.Kernel.Diagnostics.NormalizeInterruptFramePageOperation operation,
            ulong pageAddress,
            bool destinationMutated)
        {
            return result.IsInitialized &&
                result.Operation == operation &&
                result.Status ==
                    OS.Kernel.Diagnostics.NormalizeInterruptFramePageStatus
                        .Success &&
                result.ModeledPageAddress == pageAddress &&
                result.CommonTargetAddress == pageAddress + Page &&
                result.PageCapacity == Page &&
                result.RequiredBytes == (int)Page &&
                result.BytesWritten ==
                    (destinationMutated
                        ? (int)Page
                        : 0) &&
                result.BytesCompared == (int)Page &&
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

        private static bool IdtMatches(
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
                result.TableCapacity == Page &&
                result.RequiredBytes == (int)Page &&
                result.DescriptorCount == 256 &&
                result.PresentDescriptorCount == 256 &&
                result.IstRoutedDescriptorCount == 3 &&
                result.DefaultIstDescriptorCount == 253 &&
                result.FirmwareGateCopyCount == 0 &&
                result.BytesWritten ==
                    (destinationMutated
                        ? (int)Page
                        : 0) &&
                result.BytesCompared == (int)Page &&
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

        private static void Failure(EfiSimpleTextOutputProtocol* console, EfiSerialIoProtocol* serial)
        {
            for (int i = 0; i < 76; i++)
            {
                ulong word = (i / 8) switch
                {
                    0 => 0x454741505F32304DUL,
                    1 => 0x435F544C5541465FUL,
                    2 => 0x524F5252455F3252UL,
                    3 => 0x41465F45444F435FUL,
                    4 => 0x45540A0D44454C49UL,
                    5 => 0x204C4941465F5453UL,
                    6 => 0x203431302E32306DUL,
                    7 => 0x7461726170657270UL,
                    8 => 0x6C6961665F6E6F69UL,
                    9 => 0x000000000A0D6465UL,
                    _ => 0
                };
                EfiEntry.Write(console, serial, (byte)(word >> ((i % 8) * 8)));
            }
        }

    }
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
