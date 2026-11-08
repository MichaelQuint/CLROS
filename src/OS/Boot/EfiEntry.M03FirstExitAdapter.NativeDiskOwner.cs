#if M16_025_PROBE
#if !M16_023_PROBE || !M16_024_PROBE
#error Live disk descriptors require their explicit continuing block and filesystem parents.
#endif
using OS.Drivers;
using OS.Kernel.Compatibility.LinuxX64;
using OS.Kernel.Diagnostics;
using OS.Kernel.Memory;
using OS.Kernel.Platform;
using OS.Storage;
using Block = OS.Storage.ContinuingReadOnlyBlockService;
using Fs = OS.Storage.ReadOnlyWebRootFileSystem;

namespace OS.Boot;

// One retained BSP-owned diagnostic disk lifetime. Real device, mapping,
// native stack and guest admission are independently required by the runner.
public static unsafe partial class EfiEntry
{
    private static partial class M03FirstExitAdapter
    {

        private struct NativeDiskOwner
        {
            internal Storage* Boot;
            internal Block.State Device;
            internal DmaBufferPool Pool;
            internal DmaBufferSlot* DmaSlots;
            internal Fs.Mount Mount;
            internal LinuxDescriptorServices.DiskState Descriptors;
            internal NativeDiskEvidence Evidence;
            internal NativeDiskExitObservation ExitObservation;
#if M10_018_TIMING_PROBE
            internal LinuxThreadServices.DiskPumpMeasurements TimingPump;
            internal LinuxThreadServices.DiskDispatchMeasurements TimingDispatch;
            internal LinuxThreadServices.DiskDispatchMeasurements TimingDriver;
            internal LinuxThreadServices.DiskVerificationMeasurements TimingVerification;
            internal ulong TimingPreNetwork, TimingPostNetwork, TimingPostReadiness, TimingPostWaitRecord, TimingNetworkCalls, TimingReadBytes;
            internal ulong TimingReadinessRefresh, TimingReadinessWaitScan;
            internal ulong TimingNetworkTotal, TimingDiskRound;
            internal fixed ulong TimingCauseCalls[4], TimingCauseTotal[4], TimingCausePre[4], TimingCauseDisk[4], TimingCausePost[4];
            internal byte TimingPhase; //0 unarmed,1 counting after listen,2 frozen before the single dump.
#endif
            internal Fs.Operation* Operations;
            internal Fs.FileHandle* Files;
            internal Fs.CacheSlot* Cache;
#if M10_018_PROBE
            internal byte* DirectoryCache;
#endif
#if M10_018_FULL_PROBE || M10_018_TIMING_PROBE
            internal byte* FileChainCache;
#endif
            internal LinuxDescriptorServices.DiskBinding* Bindings;
            internal LinuxDescriptorServices.DiskRequest* Requests;
            internal byte* Sector;
            internal byte* Entries;
            internal VirtioMappedRegion Common, Notify, Configuration;
            internal MsixCapability Msix;
            internal ulong Arena, ArenaPages, Bar, Capacity, BootstrapDeadline, BootstrapCompleted, BootstrapCopied;
            internal uint Command, NotifyMultiplier, ArraySector;
            internal byte Phase; //1 firmware backing,2 starting,3 MBR,4 header,5 array,6 mounting,7 ready,8 fault,9 retired.
            internal bool IoActive;
            internal Block.ReadToken Io;
            internal LiveGptHeaderResult Header;
            internal LiveEfiPartitionResult Partition;
        }

    }
}
#endif
