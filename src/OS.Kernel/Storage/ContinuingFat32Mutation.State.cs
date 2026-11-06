using OS.Drivers;
using OS.Std.Binary;
using Block = OS.Storage.WritableBlockSession;
using Chain = OS.Storage.FATClusterChainsLoopBadClusterTruncationDetection;
using Vfat = OS.Storage.MultiClusterDirectoryVFATLongNameSupportSeparate;

namespace OS.Storage;

public static unsafe partial class ContinuingFat32Mutation
{
    /// <summary>Zeroed retained unmanaged owner; it contains only POD values
    /// and pointers into Admission. Copying/moving an active owner is forbidden.
    /// Inspect copied properties after failure; never free dirty journals merely
    /// because the outer block Stop/reset succeeded.</summary>
    public struct State
    {
        internal ulong Self, DeviceEpoch, PoolEpoch, Revision, RequestGeneration;
        internal Admission Input;
        internal LiveFat32Volume Volume;
        internal Life Phase;
        internal Kind Kind;
        internal Step Step;
        internal Purpose Purpose;
        internal byte ScanReason, FatPart;
        internal bool Mounted, IoActive, Cancelled, DirtyValue, DeviceFault, CreateHasRun, CreateEnd;
        internal ulong IoLba, FatLba, LoadedDirectoryLba, Offset;
        internal byte* IoTarget;
        internal Block.RequestToken Io;
        internal FileToken File;
        internal uint RootCount, TargetCount, WalkCount, WalkNext, OtherLength;
        internal uint DirectorySector, DirectorySlot, NameLength, Wanted, PlannedBytes, Changes, Issued, Work;
        internal uint CreateSlots, CreateStart, CreateFree;
        internal fixed uint Growth[MaximumGrowthClusters];
        internal uint GrowthCount, NewLength, GrowthCursor, FatCursor, FsCursor, FsInfoSector, BackupBootSector, FlushedThrough;
        internal uint EditIndex, EditOffset, EditBytes, EditValue;
        internal ulong ZeroPosition, ZeroEnd;
        internal Fat32Mutation.Stage EditStage;
        internal Transform EditKind;
        internal uint ScanRunStart, RunStart, RunSlots, KeepCount, ReleaseCount;
        internal LiveVfatState Vfat;
        internal Status Failure;
        internal Block.Status DriverFailure;
        public readonly bool IsMounted => Mounted;
        public readonly bool RequestActive => Phase is Life.Mounting or Life.Working or Life.Ready;
        public readonly bool IsReady => Phase == Life.Ready;
        public readonly bool Dirty => DirtyValue;
        public readonly bool Retired => Phase == Life.Retired;
        public readonly Status LastFailure => Failure;
        public readonly Block.Status LastBlockFailure => DriverFailure;
        public readonly uint ChangeCount => Changes;
        public readonly uint IssuedChanges => Issued;
        public readonly ulong VolumeRevision => Revision;
    }
}
