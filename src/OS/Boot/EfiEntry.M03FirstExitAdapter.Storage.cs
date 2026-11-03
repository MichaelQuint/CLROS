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
    // Explicit default-off profile. BootSequence consumes the prepared firmware
    // binding only after M01, copied ACPI and the ordinary boot marker plan.
    private static partial class M03FirstExitAdapter
    {


        private struct Storage
        {
            internal ulong Phase;
            internal void* ImageHandle;
            internal EfiSystemTable* SystemTable;
            internal FirstExitOwnedArenaState Arena;
            internal FirstExitDiagnosticState Diagnostic;
            internal FirstExitDiagnosticMemory Memory;
            internal ulong PreparationCheckpoint;
            internal uint TableMemoryType;
            internal fixed ulong PageMetadata[(int)TablePages * 3]; // 24 bytes per page, naturally aligned
#if M09_004_PROBE || M09_005_PROBE || M09_006_PROBE || M09_009_PROBE || M09_010_PROBE || M16_008_PROBE || M07_026_PROBE
            internal ulong CooperativePool;
            internal uint CooperativePhase;
#endif
#if M16_002_PROBE
            internal ulong LinuxPackage;
            internal uint LinuxBytes, LinuxPages, LinuxClosedHandles, LinuxReadCalls, LinuxLedgerCount, LinuxOwnedPhase;
            internal fixed byte LinuxPackageHash[32];
            internal OS.Manifest.LinuxElfPackageView LinuxView;
            internal fixed ulong LinuxLedger[(int)LinuxLedgerCapacity * 8];
#endif
#if M16_004_PROBE
            internal ulong LinuxImageBacking, LinuxImagePages, LinuxUserTables;
            internal uint LinuxStartupPhase, LinuxUserRangeCount, LinuxHwcap;
            internal LinuxElfLoadPlan LinuxImagePlan;
            internal LinuxElfStartupPlan LinuxInitialStack;
            internal X64UserAddressSpaceResult LinuxUserRoot;
            internal fixed ulong LinuxUserRanges[(int)X64UserAddressSpace.MaximumRanges * 4];
            internal FirmwareEntropyCapture.State LinuxEntropy;
            internal FirmwareEntropyCapture.Buffer LinuxEntropyScratch, LinuxEntropyRetained;
#endif
#if M16_005_PROBE
            internal ulong LinuxSyscallCode, LinuxSyscallData;
            internal uint LinuxSyscallPhase, LinuxSyscallExitCode, LinuxSyscallFaults, LinuxSyscallWrites, LinuxSyscallFailed;
            internal X64LinuxUserTransitionPlan LinuxSyscallPlan;
            internal LinuxSyscallGateState LinuxSyscallState;
            internal fixed ulong LinuxSyscallOriginal[(int)X64LinuxUserTransition.ObservationWords];
#endif
#if M16_006_PROBE
            internal ulong LinuxMemoryPool, LinuxMemoryCandidate, LinuxDispatchRoot;
            internal uint LinuxMemoryPhase, LinuxMemoryCheckpoint;
            internal LinuxMemoryServices.State LinuxMemory;
            internal LinuxStartupServices.State LinuxServices;
            internal FirmwareEntropyCapture.State LinuxRandomCapture;
            internal FirmwareEntropyCapture.Buffer LinuxRandomScratch, LinuxRandomRetained;
            internal FirmwareEntropyReservoir.State LinuxRuntimeRandom;
#if M16_014_PROBE
            internal OS.Std.Hash.HmacSha256Drbg.State LinuxServiceRandom;
            internal FirmwareEntropyCapture.State LinuxServiceNonceCapture;
            internal FirmwareEntropyCapture.Buffer LinuxServiceNonceScratch, LinuxServiceNonceRetained;
            internal TcpInitialSequence.Owner LinuxServiceSequence;
            internal FirmwareEntropyCapture.State LinuxServiceSequenceCapture;
            internal FirmwareEntropyCapture.Buffer LinuxServiceSequenceScratch, LinuxServiceSequenceRetained;
            internal uint LinuxServiceRandomPhase;
            internal LinuxQuotaDisposition LinuxQuota;
#endif
#endif
#if M16_007_PROBE
            internal ulong LinuxDescriptorArena;
            internal uint LinuxDescriptorPhase;
            internal LinuxDescriptorServices.State LinuxDescriptors;
#endif
#if M10_004_PROBE
            internal LinuxAppLogBudget LinuxLogBudget;
            internal uint LinuxLogRecordLimit;
#endif
#if M05_PHYSICAL_MAPPING_PROBE
            internal ulong MappingPhase, MappingCr0;
#endif
#if M05_OWNED_ACPI_SELECTION_PROBE
            internal ulong AcpiSelectionPhase;
#endif
#if M05_PCI_ENUMERATION_PROBE
            internal ulong PciPhase;
            internal BoundedPciWindow PciWindow;
#if M05_BAR_DMA_PROBE
            internal uint BarDmaPhase;
#endif
#if M05_VIRTIO_NEGOTIATION_PROBE
            internal uint VirtioPhase;
#endif
#if M06_VIRTIO_BLOCK_QUEUE_SETUP_PROBE
            internal uint BlockPhase, BlockCommand;
            internal MsixCapability BlockMsix;
#if M06_VIRTIO_BLOCK_READ_PROBE
            internal uint BlockReadPhase, BlockNotifyMultiplier;
            internal VirtioMappedRegion BlockCommon, BlockNotify, BlockDevice;
#if M06_LIVE_BLOCK_CONTRACT_PROBE
            internal uint BlockContractPhase;
#if M06_GPT_PARTITION_PROBE
            internal uint GptPhase;
#if M06_FAT_READ_PROBE
            internal uint FatPhase;
            internal ulong FatPartitionFirst, FatPartitionLast;
#if M06_PACKAGE_READ_PROBE
            internal uint PackagePhase;
            // Exclusive, bounded profile scratch lives in retained image storage.
            // It is never DMA backing and does not enlarge the diagnostic stack.
            internal fixed byte ManifestBytes[4096], PackageBytes[4096];
            internal fixed byte PackageSector[512], PackageMirror[512];
            internal fixed ushort PackageName[260];
            internal fixed uint PackageHistory[8];
#endif
#endif
#endif
            internal ulong BlockReadCapacity;
#endif
#endif
#endif
#if M05_VIRTQUEUE_POLLING_PROBE || M05_VIRTQUEUE_INTERRUPT_PROBE
            internal uint QueuePhase, QueueCommand;
#endif
#if M07_VIRTIO_NET_PROBE
            internal uint NetPhase;
#if M07_ARP_RECEIVE_PROBE
            internal uint ArpPhase;
#endif
#endif
#if M05_APIC_SETUP_PROBE
            internal uint ApicPhase;
            internal ulong MadtAddress, MadtBytes;
            internal MadtPollingTopology ApicTopology;
            internal XApicCpuObservation ApicCpu;
#endif
#if M05_VIRTQUEUE_INTERRUPT_PROBE || M06_BLOCK_MSIX_PROBE
            internal uint InterruptPhase;
            internal MsixCapability MsixCapability;
            internal MsixRegions MsixRegions;
            internal MsixInterruptContext InterruptContext;
            internal MsixProgrammingResult InterruptProgramming;
            internal VirtqueuePollingResult InterruptQueue;
            internal fixed ulong InterruptIdtStorage[514];
#if M06_BLOCK_MSIX_PROBE
            internal VirtioBlkReadResult BlockMsixRead;
            internal uint BlockMsixCrc;
#endif
#endif
#endif
#if M05_MONOTONIC_CLOCK_PROBE || M16_003_PROBE
            internal ulong ClockPhase;
            internal HpetMmioDescription HpetDescription;
            internal OwnedMonotonicClockState Clock;
#if M10_004_PROBE
            internal OwnedRealtimeClock.State Realtime;
            internal OwnedRealtimeClock.FirmwareTime RealtimeSnapshot;
            internal ulong RealtimeCaptureTicks;
#if M16_019_UNAVAILABLE_PROBE || M16_019_INVALID_PROBE
            internal uint RealtimeAdmissionControl;
            internal ulong RealtimeRejectedStatus;
            internal byte RealtimeRejectedMonth;
#endif
#endif
#endif
#if M16_003_PROBE
            internal uint ServicePhase;
            internal WorkloadServiceLoopState ServiceLoop;
#endif
#if M07_026_PROBE
            internal uint NetworkPhase;
            internal ulong NetworkArena;
#endif
#if (M03_OWNED_SUBSYSTEM_SURVIVAL_PROBE || CLROS_NORMAL_VERIFIED_EXIT)
            internal OwnedAllocatorRoundTripState OwnedAllocator;
            internal ulong OwnedPhase; // 0 fresh, 1 consumed, 2 positive complete, 3 armed, 4 fault entered, 5 reported
            internal ulong AcpiWorkspace, AcpiWorkspaceBytes;
#if M03_OWNED_SUBSYSTEM_PANIC_PROBE
            internal ulong PanicGuard, UnexpectedGuardValue;
#endif
#endif
#if M03_FIRMWARE_ADAPTER_REVOCATION_PROBE || M16_003_PROBE
            internal FirmwareAdapterAuthority AdapterAuthority;
            internal AdapterHandles OriginalHandles, CopiedHandles;
            internal EfiSimpleTextOutputProtocol* AdapterConsole;
            internal EfiBootServices* AdapterServices;
            internal fixed ulong AdapterEntries[8];
#endif
        }































    }
#endif
}
#pragma warning restore 649
