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
    private static partial class M02UnexpectedVectorPanicAdapter
    {
        private const ulong Page = 4096, CodeBytes = 8192, DataBytes = 28672;
        private const ulong LowEnd = 0x0000800000000000;
        private const ulong SentinelOffset = 20480, SentinelBytes = 16;
        private const ulong Sentinel0 = 0x4d30325f50414e30, Sentinel1 = 0x4d30325f50414e31;
        // The original one-shot authority is in the loaded image, never in C/D.
        // Only State changes after preparation. No callback can bind or reset it.
        private static Authority s_authority;

        internal static void Run(EfiSimpleTextOutputProtocol* console,
            EfiSerialIoProtocol* serial, EfiBootServices* services)
        {
            if (s_authority.State != 0)
            {
                Failure(console, serial);
                Spin();
            }
            s_authority.State = 1;
            ulong c = 0, d = 0;
            bool allocatedC = false, allocatedD = false;
            s_authority.AllocateCodeStatus = s_authority.AllocateDataStatus = ~0UL;
            s_authority.FreeCodeStatus = s_authority.FreeDataStatus = ~0UL;
            byte* image = null;
            ulong imageBytes = 0;
            M02CheckedPortIoFunctionPointerIntegrationResult binding = default;
            if (OS.Kernel.Diagnostics.Probes.EnableUnexpectedVectorPanicProbe ||
                services == null || services->AllocatePages == null || services->FreePages == null ||
                !M01StageOneAdapter.TryGetOriginalLoadedImage(out image, out imageBytes) ||
                !M01StageThreeAdapter.TryGetUnexpectedPanicBinding(out binding) ||
                !Extent((ulong)image, imageBytes, 1))
            {
                Failure(console, serial);
                Spin();
            }
            ulong callback = (ulong)(delegate* unmanaged<ulong, ulong, uint>)&Callback;
            fixed (Authority* authority = &s_authority)
            {
                if (!Contained((ulong)authority, (ulong)sizeof(Authority), (ulong)image, imageBytes) ||
                    !Contained(callback, 1, (ulong)image, imageBytes) || !BindingBytes(binding))
                {
                    Failure(console, serial);
                    Spin();
                }
            }
            var observation = default(OS.Kernel.Memory.X64PagingModePreflightObservation);
            var scratch = default(OS.Kernel.Memory.X64PagingModePreflightResult);
            ulong* selectors = stackalloc ulong[3];
            ulong observationAddress = (ulong)&observation, scratchAddress = (ulong)&scratch, selectorsAddress = (ulong)selectors;
            // These exact temporary extents are exclusions before the first allocation
            // and remain part of every admission and pre-entry cleanup decision.
            if (!TemporaryStorage(observationAddress, scratchAddress, selectorsAddress, (ulong)image, imageBytes, binding.PageAddress))
            {
                Failure(console, serial);
                Spin();
            }
            s_authority.AllocationAttemptMask |= 1;
            s_authority.AllocateCodeStatus = services->AllocatePages(0, 1, 2, &c);
            allocatedC = s_authority.AllocateCodeStatus == 0;
            if (!allocatedC || !Lease(c, CodeBytes, (ulong)image, imageBytes, binding.PageAddress, observationAddress, scratchAddress, selectorsAddress))
            {
                FailBeforeEntry(console, serial, services, c, allocatedC, d, allocatedD, (ulong)image, imageBytes, binding.PageAddress, observationAddress, scratchAddress, selectorsAddress);
                Spin();
            }
            s_authority.AllocationAttemptMask |= 2;
            s_authority.AllocateDataStatus = services->AllocatePages(0, 2, 7, &d);
            allocatedD = s_authority.AllocateDataStatus == 0;
            if (!allocatedD || !Lease(d, DataBytes, (ulong)image, imageBytes, binding.PageAddress, observationAddress, scratchAddress, selectorsAddress) ||
                !Disjoint(c, CodeBytes, d, DataBytes))
            {
                FailBeforeEntry(console, serial, services, c, allocatedC, d, allocatedD, (ulong)image, imageBytes, binding.PageAddress, observationAddress, scratchAddress, selectorsAddress);
                Spin();
            }

            // All actual temporary spans are checked before the first query-code write.
            if (!ScratchBuffers(observationAddress, scratchAddress, selectorsAddress,
                    c, d, (ulong)image, imageBytes, binding.PageAddress))
            {
                FailBeforeEntry(console, serial, services, c, allocatedC, d, allocatedD, (ulong)image, imageBytes, binding.PageAddress, observationAddress, scratchAddress, selectorsAddress);
                Spin();
            }
            if (!Query(c, &observation, &scratch, selectors) || !Mode(observation, selectors[0], selectors[1], selectors[2]))
            {
                FailBeforeEntry(console, serial, services, c, allocatedC, d, allocatedD, (ulong)image, imageBytes, binding.PageAddress, observationAddress, scratchAddress, selectorsAddress);
                Spin();
            }
            var request = new OS.Kernel.Diagnostics.UnexpectedVectorPanicRequest(
                c, d, (ulong)image, imageBytes, callback, selectors[0], observation.RawCr3, true);
            if (!Prepare(c, CodeBytes, d, DataBytes, request) || !BindingBytes(binding))
            {
                FailBeforeEntry(console, serial, services, c, allocatedC, d, allocatedD, (ulong)image, imageBytes, binding.PageAddress, observationAddress, scratchAddress, selectorsAddress);
                Spin();
            }

            s_authority.Code = c;
            s_authority.CodeCapacity = CodeBytes;
            s_authority.Data = d;
            s_authority.DataCapacity = DataBytes;
            s_authority.Image = (ulong)image;
            s_authority.ImageBytes = imageBytes;
            s_authority.Binding = binding;
            s_authority.Backend = new OS.Kernel.Diagnostics.Com1OwnedPortIoBackend(binding.Binding);
            s_authority.Request = request;
            // Pristine readback requires zero stack bytes. The only later C/D writes
            // before entry are this declared sentinel, outside the proven native depth.
            PlaceSentinel(d);
            if (!CheckSentinel(d) ||
                X64InstructionPublication.Serialize() != X64InstructionPublication.ExpectedSuccessCookie)
            {
                FailBeforeEntry(console, serial, services, c, allocatedC, d, allocatedD, (ulong)image, imageBytes, binding.PageAddress, observationAddress, scratchAddress, selectorsAddress);
                Spin();
            }
            s_authority.State = 2;
            ((delegate* unmanaged<byte*, void>)(c + Page + 512))((byte*)(d + Page));
            // The stream and callback are terminal; never recover firmware ownership.
            Spin();
        }

        [System.Runtime.InteropServices.UnmanagedCallersOnly]
        [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.NoInlining)]
        private static uint Callback(ulong capture, ulong context)
        {
            // Scalar equality precedes every read through the supplied arguments.
            // All subsequent reads use the retained original pointers, not those arguments.
            if (!CallbackArguments(s_authority.State, s_authority.Data, s_authority.DataCapacity, capture, context))
                Spin();
            s_authority.State = 3;
            if (!CheckSentinel(s_authority.Data) || !BindingBytes(s_authority.Binding))
                Spin();
            _ = OS.Kernel.Diagnostics.UnexpectedVectorPanicEvidenceDeterministicQEMUFailureClassification.ReportCaptured(
                s_authority.Backend, (byte*)s_authority.Code, s_authority.CodeCapacity,
                (byte*)s_authority.Data, s_authority.DataCapacity, s_authority.Request);
            // Report success and every rejection halt locally; no extra serial record.
            while (true)
            {
            }
        }

        internal static bool CallbackArguments(byte state, ulong d, ulong capacity, ulong capture, ulong context)
            => state == 2 && capacity == DataBytes && Extent(d, capacity, Page) &&
                capture == d + 28488 && context == d + Page;

        internal static bool Prepare(ulong c, ulong codeCapacity, ulong d, ulong dataCapacity,
            OS.Kernel.Diagnostics.UnexpectedVectorPanicRequest request)
        {
            if (OS.Kernel.Diagnostics.Probes.EnableUnexpectedVectorPanicProbe ||
                c != request.CodeAddress || d != request.DataAddress ||
                codeCapacity != CodeBytes || dataCapacity != DataBytes)
                return false;
            var prepared = OS.Kernel.Diagnostics.UnexpectedVectorPanicEvidenceDeterministicQEMUFailureClassification.PrepareContext(
                (byte*)c, codeCapacity, (byte*)d, dataCapacity, request);
            if (!prepared.Succeeded || prepared.BytesWritten != 36864)
                return false;
            var emitted = OS.Kernel.Diagnostics.UnexpectedVectorPanicEvidenceDeterministicQEMUFailureClassification.EmitStream(
                (byte*)c, codeCapacity, (byte*)d, dataCapacity, request);
            if (!emitted.Succeeded || emitted.BytesWritten != 4096)
                return false;
            var read = OS.Kernel.Diagnostics.UnexpectedVectorPanicEvidenceDeterministicQEMUFailureClassification.ValidateStreamReadBack(
                (byte*)c, codeCapacity, (byte*)d, dataCapacity, request);
            return read.Succeeded && read.BytesWritten == 0 && read.FirstMismatchOffset == -1;
        }

        internal static bool Extent(ulong address, ulong bytes, ulong alignment)
            => address != 0 && bytes != 0 && (address & (alignment - 1)) == 0 &&
                address < LowEnd && bytes <= LowEnd - address;

        internal static bool Disjoint(ulong a, ulong an, ulong b, ulong bn)
            => a <= ulong.MaxValue - an && b <= ulong.MaxValue - bn && (a + an <= b || b + bn <= a);

        private static bool Contained(ulong address, ulong bytes, ulong owner, ulong capacity)
            => Extent(address, bytes, 1) && Extent(owner, capacity, 1) &&
                address >= owner && address - owner <= capacity && bytes <= capacity - (address - owner);

        internal static bool Lease(ulong address, ulong bytes, ulong image, ulong imageBytes, ulong io,
            ulong observation, ulong scratch, ulong selectors)
            => Extent(address, bytes, Page) && TemporaryStorage(observation, scratch, selectors, image, imageBytes, io) &&
                Disjoint(address, bytes, image, imageBytes) && Disjoint(address, bytes, io, Page) &&
                Disjoint(address, bytes, observation, 56) && Disjoint(address, bytes, scratch, 96) && Disjoint(address, bytes, selectors, 24);

        internal static bool TemporaryStorage(ulong observation, ulong scratch, ulong selectors,
            ulong image, ulong imageBytes, ulong io)
            => Extent(image, imageBytes, 1) && Extent(io, Page, Page) &&
                Temporary(observation, 56, image, imageBytes, io) && Temporary(scratch, 96, image, imageBytes, io) &&
                Temporary(selectors, 24, image, imageBytes, io) && Disjoint(observation, 56, scratch, 96) &&
                Disjoint(observation, 56, selectors, 24) && Disjoint(scratch, 96, selectors, 24);

        private static bool Temporary(ulong address, ulong bytes, ulong image, ulong imageBytes, ulong io)
            => Extent(address, bytes, 8) && Disjoint(address, bytes, image, imageBytes) && Disjoint(address, bytes, io, Page);

        internal static bool ScratchBuffers(ulong observation, ulong scratch, ulong selectors,
            ulong c, ulong d, ulong image, ulong imageBytes, ulong io)
            => TemporaryStorage(observation, scratch, selectors, image, imageBytes, io) &&
                Disjoint(observation, 56, c, CodeBytes) && Disjoint(observation, 56, d, DataBytes) &&
                Disjoint(scratch, 96, c, CodeBytes) && Disjoint(scratch, 96, d, DataBytes) &&
                Disjoint(selectors, 24, c, CodeBytes) && Disjoint(selectors, 24, d, DataBytes);

        internal static bool Mode(OS.Kernel.Memory.X64PagingModePreflightObservation o, ulong cs, ulong lar, ulong features)
        {
            if (!o.IsInitialized || o.CpuidMaximumExtendedLeaf < 0x80000008 || o.PhysicalAddressWidth < 36 || o.PhysicalAddressWidth > 52 ||
                cs == 0 || cs > 65535 || (cs & 7) != 0 || (lar >> 32) != 0 || (lar & 0x00f0fe00) != 0x00a09a00 ||
                (features & 0x07000000) != 0x07000000 || (features >> 32) != 0 ||
                (o.Cr0 & 0x8001000d) != 0x80010001 || (o.RawCr4 & 0x620) != 0x620 ||
                (o.RawCr4 & 0x01821000) != 0 || (o.Ia32Efer & 0xd00) != 0xd00 ||
                (o.Rflags & 0x24502) != 2)
                return false;
            ulong physicalMask = (1UL << o.PhysicalAddressWidth) - 1;
            return (o.RawCr3 & ~((physicalMask & ~4095UL) | 0x18UL)) == 0 && (o.RawCr3 & ~4095UL) != 0;
        }

        private static bool Query(ulong c, OS.Kernel.Memory.X64PagingModePreflightObservation* observation,
            OS.Kernel.Memory.X64PagingModePreflightResult* scratch, ulong* selectors)
        {
            // CS/LAR/CPUID are unprivileged. Reject a non-ring-zero caller before MOV CRx/RDMSR.
            for (int i = 0; i < 3; i++)
                selectors[i] = 0;
            for (int i = 0; i < SelectorQueryBytes; i++)
                ((byte*)c)[128 + i] = SelectorQueryByte(i);
            for (int i = 0; i < SelectorQueryBytes; i++)
                if (((byte*)c)[128 + i] != SelectorQueryByte(i))
                    return false;
            var materialized = OS.Kernel.Memory.X64PagingModePreflightStubPlan.TryMaterialize((byte*)c, Page, 0, 111, observation, 56, scratch, 96);
            var readback = OS.Kernel.Memory.X64PagingModePreflightStubPlan.TryValidateMaterialized((byte*)c, Page, 0, 111, observation, 56, scratch, 96);
            if (!materialized.Succeeded || !readback.Succeeded ||
                X64InstructionPublication.Serialize() != X64InstructionPublication.ExpectedSuccessCookie)
                return false;
            if (((delegate* unmanaged<ulong*, uint>)(c + 128))(selectors) != 1 ||
                selectors[0] == 0 || selectors[0] > 65535 || (selectors[0] & 7) != 0 ||
                (selectors[1] & 0x00f0fe00) != 0x00a09a00 || (selectors[2] & 0x07000000) != 0x07000000)
                return false;
            ((delegate* unmanaged<OS.Kernel.Memory.X64PagingModePreflightObservation*, void>)c)(observation);
            return observation->IsInitialized;
        }

        // ENDBR64; CS; LAR with ZF rejection; CPUID.1:EDX; preserve RBX; scalar return.
        // The exact byte oracle and actual linked caller are checked before guest execution.
        private const int SelectorQueryBytes = 46;
        internal static byte SelectorQueryByte(int index)
        {
            return index switch
            {
                0 => 0xf3,
                1 => 0x0f,
                2 => 0x1e,
                3 => 0xfa,
                4 => 0x31,
                5 => 0xc0,
                6 => 0x66,
                7 => 0x8c,
                8 => 0xc8,
                9 => 0x48,
                10 => 0x89,
                11 => 0x01,
                12 => 0x0f,
                13 => 0x02,
                14 => 0xd0,
                15 => 0x75,
                16 => 0x1a,
                17 => 0x48,
                18 => 0x89,
                19 => 0x51,
                20 => 0x08,
                21 => 0x53,
                22 => 0x49,
                23 => 0x89,
                24 => 0xc8,
                25 => 0xb8,
                26 => 0x01,
                27 => 0x00,
                28 => 0x00,
                29 => 0x00,
                30 => 0x0f,
                31 => 0xa2,
                32 => 0x49,
                33 => 0x89,
                34 => 0x50,
                35 => 0x10,
                36 => 0x5b,
                37 => 0xb8,
                38 => 0x01,
                39 => 0x00,
                40 => 0x00,
                41 => 0x00,
                42 => 0xc3,
                43 => 0x31,
                44 => 0xc0,
                45 => 0xc3,
                _ => 0
            };
        }

        internal static bool BindingBytes(M02CheckedPortIoFunctionPointerIntegrationResult binding)
        {
            if (!binding.Succeeded || !Extent(binding.PageAddress, Page, Page) ||
                binding.Binding.PayloadBaseAddress != (nuint)binding.PageAddress ||
                binding.Binding.PayloadEndAddress != (nuint)(binding.PageAddress + Page))
                return false;
            byte* input = (byte*)binding.In8Address;
            byte* output = (byte*)binding.Out8Address;
            return input[0] == 0x66 && input[1] == 0x89 && input[2] == 0xca && input[3] == 0xec && input[4] == 0xc3 &&
                output[0] == 0x88 && output[1] == 0xd0 && output[2] == 0x66 && output[3] == 0x89 &&
                output[4] == 0xca && output[5] == 0xee && output[6] == 0xc3;
        }

        private static void PlaceSentinel(ulong d)
        {
            *(ulong*)(d + SentinelOffset) = Sentinel0;
            *(ulong*)(d + SentinelOffset + 8) = Sentinel1;
        }

        internal static bool CheckSentinel(ulong d)
            => *(ulong*)(d + SentinelOffset) == Sentinel0 && *(ulong*)(d + SentinelOffset + 8) == Sentinel1;

        internal static byte CleanupPlan(ulong c, bool allocatedC, ulong d, bool allocatedD,
            ulong image, ulong imageBytes, ulong io, ulong observation, ulong scratch, ulong selectors)
        {
            // A malformed successful response may alias foreign or earlier storage.
            // Retain all claims rather than invent ownership or issue overlapping frees.
            if ((allocatedC && !Lease(c, CodeBytes, image, imageBytes, io, observation, scratch, selectors)) ||
                (allocatedD && (!allocatedC || !Lease(d, DataBytes, image, imageBytes, io, observation, scratch, selectors))) ||
                (allocatedC && allocatedD && !Disjoint(c, CodeBytes, d, DataBytes)))
                return 0;
            return (byte)((allocatedC ? 1 : 0) | (allocatedD ? 2 : 0));
        }

        private static void FailBeforeEntry(EfiSimpleTextOutputProtocol* console, EfiSerialIoProtocol* serial,
            EfiBootServices* services, ulong c, bool allocatedC, ulong d, bool allocatedD,
            ulong image, ulong imageBytes, ulong io, ulong observation, ulong scratch, ulong selectors)
        {
            if (s_authority.State != 1)
            {
                Failure(console, serial);
                return;
            }
            byte plan = CleanupPlan(c, allocatedC, d, allocatedD, image, imageBytes, io, observation, scratch, selectors);
            s_authority.State = 4;
            s_authority.RetainedMask = (byte)(((allocatedC ? 1 : 0) | (allocatedD ? 2 : 0)) & ~plan);
            if ((plan & 2) != 0)
            {
                s_authority.FreeAttemptMask |= 2;
                s_authority.FreeDataStatus = services->FreePages(d, 7);
                if (s_authority.FreeDataStatus != 0)
                    s_authority.RetainedMask |= 2;
            }
            if ((plan & 1) != 0)
            {
                s_authority.FreeAttemptMask |= 1;
                s_authority.FreeCodeStatus = services->FreePages(c, 2);
                if (s_authority.FreeCodeStatus != 0)
                    s_authority.RetainedMask |= 1;
            }
            Failure(console, serial);
        }

        private static void Failure(EfiSimpleTextOutputProtocol* console, EfiSerialIoProtocol* serial)
        {
            // A preparation failure is never the expected terminal panic outcome.
            for (int i = 0; i < 38; i++)
            {
                ulong word = (i / 8) switch
                {
                    0 => 0x4941465f54534554UL,
                    1 => 0x31302e32306d204cUL,
                    2 => 0x7261706572702036UL,
                    3 => 0x61665f6e6f697461UL,
                    4 => 0x0a0d64656c69UL,
                    _ => 0
                };
                EfiEntry.Write(console, serial, (byte)(word >> ((i % 8) * 8)));
            }
        }
        private static void Spin()
        {
            while (true)
            {
            }
        }
    }
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
