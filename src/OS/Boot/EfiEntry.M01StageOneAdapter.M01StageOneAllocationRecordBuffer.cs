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
#if (M01_LOADED_IMAGE_STACK_PROBE || CLROS_NORMAL_VERIFIED_EXIT)

    private static partial class M01StageOneAdapter
    {






        private struct M01StageOneAllocationRecordBuffer
        {
            public BootMemoryRegionRecord Code;
            public BootMemoryRegionRecord Data;
        }

    }
#else
    private static partial class M01StageOneAdapter
    {
    }
#endif
}
#pragma warning restore 649
