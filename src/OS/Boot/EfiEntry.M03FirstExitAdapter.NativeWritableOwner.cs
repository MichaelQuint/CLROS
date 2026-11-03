#if M16_027_PROBE
#if !M16_025_PROBE || !M16_024_PROBE || !M16_023_PROBE
#error Writable disk admission requires the selected disk compile infrastructure.
#endif
using OS.Drivers;
using OS.Kernel.Compatibility.LinuxX64;
using OS.Kernel.Diagnostics;
using OS.Kernel.Memory;
using OS.Kernel.Platform;
using OS.Storage;
using B = OS.Storage.WritableBlockSession;
using C = OS.Storage.ContinuingFat32Mutation;
using K = OS.Storage.ContinuingFat32Bootstrap;
using P = OS.Storage.WritableBlockReadPort;
using W = OS.Kernel.Compatibility.LinuxX64.LinuxContinuingWritableDescriptorServices;
using F = OS.Kernel.Compatibility.LinuxX64.LinuxDescriptorServices;
using Fs = OS.Storage.ReadOnlyWebRootFileSystem;

namespace OS.Boot;

// Default-off owner preparation only. Shared boot/profile/worker/runner hooks
// and original native/guest qualification are separate integration obligations.
public static unsafe partial class EfiEntry
{
    private static partial class M03FirstExitAdapter
    {







        private struct NativeWritableOwner
        {
            internal Storage* Boot;
            internal ulong Arena, Pages, Bar, NicBar, Capacity, Deadline;
            internal uint Command, NotifyMultiplier, PrimaryPid;
            internal byte Phase; //1 retained,2 device,3 GPT,4 C mount,5 FS mount,6 pair ready,7 retired,8 quarantined.
            internal bool BootstrapStarted, EngineStarted, PortStarted, FsStarted, ProviderStarted, ReadAttached, WriteAttached, Attached;
            internal bool UserCleanup;
            internal uint Reaped, Transfers;
            internal ulong CleanupClock, Epochs;
            internal VirtioMappedRegion Common, Notify, Configuration;
            internal MsixCapability Msix;
            internal B.State* Device;
            internal DmaBufferPool* Pool;
            internal DmaBufferSlot* Slot;
            internal B.Observation* Observation;
            internal NativeWritableEvidence* Evidence;
            internal NativeWritableCarrier* Carrier;
            internal C.State* Engine;
            internal C.Admission EngineInput;
            internal K.State* Bootstrap;
            internal K.Result* Selected;
            internal byte* BootstrapSector;
            internal byte* Entries;
            internal byte* Header;
            internal P.State* Port;
            internal Fs.Mount* Mount;
            internal Fs.Operation* Operations;
            internal Fs.FileHandle* Files;
            internal Fs.CacheSlot* Cache;
            internal F.DiskState* ReadDescriptors;
            internal F.DiskBinding* ReadBindings;
            internal F.DiskRequest* ReadRequests;
            internal F.WritableDiskState* WriteDescriptors;
            internal F.WritableDiskBinding* WriteBindings;
            internal W.State* Provider;
            internal W.Descriptor* Fds;
            internal W.Description* Descriptions;
            internal byte* ProviderInput;
            internal W.ProcessSlot* Processes;
            internal W.RangeLock* Locks;
            internal W.RangeLock* LockScratch;
            internal LiveEfiPartitionResult Esp;
        }





















































































































































































































































































































































































































































































































































































    }
}
#endif
