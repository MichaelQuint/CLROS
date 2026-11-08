using System.Buffers.Binary;
using System.Runtime.InteropServices;
using System.Text;
using OS.Drivers;
using OS.Kernel.Platform;
using Block = OS.Storage.ContinuingReadOnlyBlockService;
using Fs = OS.Storage.ReadOnlyWebRootFileSystem;
using OS.Storage;

internal static unsafe partial class ReadOnlyWebRootFileSystemTests
{
    internal sealed class Fixture : IDisposable
    {
        internal const int ArenaBytes = 262144;
        private static long epoch;
        internal readonly byte* Arena;
        private readonly uint arenaCapacity;
        private readonly byte* blockBacking = (byte*)NativeMemory.AlignedAlloc(12288, 4096);
        internal byte* Data => blockBacking;
        internal readonly Block.State* Device = (Block.State*)NativeMemory.AllocZeroed((nuint)sizeof(Block.State));
        internal readonly DmaBufferPool* Pool = (DmaBufferPool*)NativeMemory.AllocZeroed((nuint)sizeof(DmaBufferPool));
        private readonly DmaBufferSlot* dmaSlot = (DmaBufferSlot*)NativeMemory.AllocZeroed(3, (nuint)sizeof(DmaBufferSlot));
        private readonly Hardware* hardware = (Hardware*)NativeMemory.AllocZeroed((nuint)sizeof(Hardware));
        private readonly GCHandle diskHandle;
        internal bool CompleteOnPoll
        {
            set => hardware->CompleteOnPoll = value;
        }
        internal readonly Fs.Mount* Mount;
        internal readonly Fs.Operation* Operations;
        internal readonly Fs.FileHandle* Files;
        internal readonly Fs.CacheSlot* Cache;
        internal readonly byte* DirectoryCacheBacking;
        internal readonly uint DirectoryCacheSlots;
        internal readonly byte* ChainCacheBacking;
        internal readonly uint ChainCacheSlots;
        internal readonly Disk Disk;
        internal ulong DrivePolls; // Host Drive/Poll calls only; excludes Begin/Copy and PumpMount.
        private readonly Fs.Admission input;
        internal Fs.Admission Input => input;
        private uint arenaAt;
        private readonly uint dataReadSectors;
        internal uint UsedArenaBytes => arenaAt;
        internal Fixture(uint cacheSlots = 2, uint workBudget = 65536, uint history = 4096, uint map = 4096, uint clusters = 65525, bool collision = false, uint slots = 2,
            Block.ObservationAccess observation = default, uint dataReadSectors = 1, uint directoryCacheSlots = 0, uint chainCacheSlots = 0, bool largeReads = false)
        {
            this.dataReadSectors = dataReadSectors;
            DirectoryCacheSlots = directoryCacheSlots;
            ChainCacheSlots = chainCacheSlots;
            HostCheck.True(slots is >= 2 and <= 8);
            arenaCapacity = slots == 2 ? (uint)ArenaBytes : 1048576U;
            Arena = (byte*)NativeMemory.AlignedAlloc(arenaCapacity, 4096);
            new Span<byte>(Arena, (int)arenaCapacity).Clear();
            new Span<byte>(Data, 12288).Clear();
            Disk = new(clusters, collision);
            ulong lifetime = (ulong)Interlocked.Increment(ref epoch);
            uint dmaPages = largeReads ? 3U : 1U;
            HostCheck.Equal(DmaAddressStatus.Success, DmaAddressWindow.TryCreate((ulong)Data, Physical, DeviceBase, dmaPages * 4096UL, 64, out var window));
            HostCheck.Equal(DmaBufferStatus.Success, PageAlignedDmaBuffers.TryInitialize(Pool, window, dmaSlot, dmaPages, lifetime));
            diskHandle = GCHandle.Alloc(Disk);
            hardware->DiskHandle = GCHandle.ToIntPtr(diskHandle);
            hardware->Data = Data;
            hardware->DataOffset = largeReads ? 4096U : 512U;
            hardware->Capacity = Disk.Capacity;
            hardware->Status = 11;
            PciSizedBar* bars = stackalloc PciSizedBar[6];
            new Span<PciSizedBar>(bars, 6).Clear();
            HostCheck.Equal(PciBarProbeStatus.Success, BARPrefetchTypeRegressionTestsDerivedMOSAFailures.Decode(0xc, 0xc0, 0xffffc00c, 0xffffffff, 4, out bars[4]));
            var caps = VirtioPciCapabilitySet.Create(new(VirtioPciCapabilityType.CommonConfiguration, 4, 0, 56, 0),
                new(VirtioPciCapabilityType.NotifyConfiguration, 4, 0x3000, 64, 4), new(VirtioPciCapabilityType.IsrStatus, 4, 0x1000, 1, 0),
                new(VirtioPciCapabilityType.DeviceConfiguration, 4, 0x2000, 8, 0));
            VirtioConfigurationRegion* regions = stackalloc VirtioConfigurationRegion[4];
            HostCheck.Equal(VirtioRegionStatus.Success, CommonNotifyISRDeviceConfigurationRegions.PlanBlock(caps, bars, 6, regions, 4));
            HostCheck.Equal(VirtioRegionStatus.Success, CommonNotifyISRDeviceConfigurationRegions.Bind(regions[0], Common, out var common));
            HostCheck.Equal(VirtioRegionStatus.Success, CommonNotifyISRDeviceConfigurationRegions.BindNotificationPage(regions[1], 64, Notify, out var notify));
            HostCheck.Equal(VirtioRegionStatus.Success, CommonNotifyISRDeviceConfigurationRegions.Bind(regions[3], Config, out var device));
            Block.Admission block = new()
            {
                Common = common,
                Notify = notify,
                Device = device,
                NotifyMultiplier = 4,
                ResetLimit = 3,
                Pool = Pool,
                UseLargeReadLayout = largeReads,
                Epoch = lifetime,
                NegotiatedFeatures = BoundedVirtioFeatures.Version1,
                ExpectedCapacity = Disk.Capacity,
                CompletionTimeoutNanos = 1000,
                Access = new()
                {
                    Queue = new()
                    {
                        Context = hardware,
                        Read8 = &Read8,
                        Write8 = &Write8,
                        Read16 = &Read16,
                        Write16 = &Write16,
                        Read32 = &Read32,
                        Write32 = &Write32,
                        Fence = &Fence,
                        BusMasterOff = &BusOff
                    },
                    BusMasterOn = &BusOn
                },
                Clock = new()
                {
                    Context = hardware,
                    Read = &Clock
                },
                Observation = observation
            };
            HostCheck.Equal(Block.Status.Success, Block.Start(Device, block));
            Mount = (Fs.Mount*)Take((uint)sizeof(Fs.Mount));
            Operations = (Fs.Operation*)Take(slots * (uint)sizeof(Fs.Operation));
            Files = (Fs.FileHandle*)Take(slots * (uint)sizeof(Fs.FileHandle));
            Cache = (Fs.CacheSlot*)Take(cacheSlots * (uint)sizeof(Fs.CacheSlot));
            for (uint i = 0; i < slots; i++)
            {
                Operations[i].Storage = new()
                {
                    Path = Take(256),
                    Sector = Take(512),
                    Name = (ushort*)Take(520),
                    History = (uint*)Take(history * 4),
                    HistoryCapacity = history,
                    Range = Take(65536),
                    RangeCapacity = 65536
                };
                Files[i].Clusters = (uint*)Take(map * 4);
                Files[i].ClusterCapacity = map;
            }
            for (uint i = 0; i < cacheSlots; i++)
                Cache[i].Data = Take(512);
            if (directoryCacheSlots != 0)
            {
                uint bytes = Fs.ImmutableDirectoryCacheBytes(directoryCacheSlots);
                HostCheck.True(bytes != 0);
                byte* guarded = Take(bytes + 32);
                new Span<byte>(guarded, (int)bytes + 32).Fill(0xc7);
                DirectoryCacheBacking = guarded + 16;
            }
            if (chainCacheSlots != 0)
            {
                uint bytes = Fs.ImmutableFileChainCacheBytes(chainCacheSlots);
                HostCheck.True(bytes != 0);
                byte* guarded = Take(bytes + 32);
                new Span<byte>(guarded, (int)bytes + 32).Fill(0xc7);
                ChainCacheBacking = guarded + 16;
            }
            input = new()
            {
                Device = Device,
                Epoch = lifetime,
                PartitionFirst = ReadOnlyWebRootFileSystemTests.Disk.Partition,
                PartitionLast = Disk.Last,
                Operations = Operations,
                OperationCount = slots,
                Files = Files,
                FileCount = slots,
                Cache = Cache,
                CacheCount = cacheSlots,
                DirectoryEntryBudget = workBudget
            };
        }
        private byte* Take(uint bytes)
        {
            byte* p = Arena + arenaAt;
            arenaAt = (arenaAt + bytes + 7) & ~7U;
            if (arenaAt > arenaCapacity)
                throw new InvalidOperationException("fixture_arena");
            return p;
        }
        internal void Tick()
        {
            Complete(hardware);
        }
        internal Fs.Status StartMount()
        {
            Fs.Status status = Fs.Start(Mount, input, dataReadSectors);
            if (status == Fs.Status.Pending && DirectoryCacheSlots != 0)
                HostCheck.Equal(Fs.Status.Success, Fs.EnableImmutableDirectoryCache(Mount, DirectoryCacheBacking,
                    Fs.ImmutableDirectoryCacheBytes(DirectoryCacheSlots), DirectoryCacheSlots));
            if (status == Fs.Status.Pending && ChainCacheSlots != 0)
                HostCheck.Equal(Fs.Status.Success, Fs.EnableImmutableFileChainCache(Mount, ChainCacheBacking,
                    Fs.ImmutableFileChainCacheBytes(ChainCacheSlots), ChainCacheSlots));
            for (int i = 0; i < 10000 && status == Fs.Status.Pending; i++)
            {
                Tick();
                status = Fs.PumpMount(Mount);
            }
            return status;
        }
        internal void MountReady() => HostCheck.Equal(Fs.Status.Ready, StartMount());
        internal Fs.Status Drive(Fs.OperationToken token)
        {
            Fs.Status status = Fs.Status.Pending;
            for (int i = 0; i < 100000 && status is Fs.Status.Pending or Fs.Status.Busy; i++)
            {
                Tick();
                DrivePolls++;
                status = Fs.Poll(Mount, token);
            }
            return status;
        }
        internal Fs.OpenResult Open(string path)
        {
            Fs.OperationToken token = default;
            HostCheck.Equal(Fs.Status.Pending, Fs.BeginOpen(Mount, Encoding.ASCII.GetBytes(path), &token));
            HostCheck.Equal(Fs.Status.Ready, Drive(token));
            Fs.OpenResult result = default;
            HostCheck.Equal(Fs.Status.Success, Fs.CopyOpen(Mount, token, &result));
            return result;
        }
        internal void Read(Fs.FileToken file, ulong offset, uint count, Span<byte> output, uint expected)
        {
            Fs.OperationToken token = default;
            HostCheck.Equal(Fs.Status.Pending, Fs.BeginRead(Mount, file, offset, count, &token));
            HostCheck.Equal(Fs.Status.Ready, Drive(token));
            uint bytes = 0;
            HostCheck.Equal(Fs.Status.Success, Fs.CopyRead(Mount, token, output, &bytes));
            HostCheck.Equal(expected, bytes);
        }
        internal Fs.Status ScanToEnd(Fs.FileToken file)
        {
            Fs.OperationToken token = default;
            HostCheck.Equal(Fs.Status.Pending, Fs.BeginDirectory(Mount, file, &token));
            Span<byte> name = stackalloc byte[256];
            Fs.Metadata info = default;
            for (int i = 0; i < 10000; i++)
            {
                var status = Drive(token);
                if (status == Fs.Status.EndOfDirectory)
                    return status;
                if (status != Fs.Status.Entry)
                {
                    HostCheck.Equal(Fs.Status.Cancelled, Fs.Cancel(Mount, token));
                    return status;
                }
                HostCheck.Equal(Fs.Status.OutputFull, Fs.CopyEntry(Mount, token, &info, name[..0]));
                HostCheck.Equal(Fs.Status.Success, Fs.CopyEntry(Mount, token, &info, name));
            }
            return Fs.Status.InvalidState;
        }
        internal bool OneRead()
        {
            Fs.OperationToken token = default;
            Fs.OpenResult file = default;
            if (Fs.BeginOpen(Mount, "/app/wwwroot/index.html"u8, &token) != Fs.Status.Pending || Drive(token) != Fs.Status.Ready ||
                Fs.CopyOpen(Mount, token, &file) != Fs.Status.Success || Fs.BeginRead(Mount, file.File, 511, 2, &token) != Fs.Status.Pending || Drive(token) != Fs.Status.Ready)
                return false;
            Span<byte> bytes = stackalloc byte[2];
            uint count = 0;
            return Fs.CopyRead(Mount, token, bytes, &count) == Fs.Status.Success && count == 2 && bytes[0] == Payload(511) && bytes[1] == Payload(512) && Fs.Close(Mount, file.File) == Fs.Status.Success;
        }
        public void Dispose()
        {
            if (Mount->RequiresCleanup)
            {
                _ = Fs.Stop(Mount);
            }
            if (!Device->Retired)
                _ = Block.Stop(Device);
            if (Mount->RequiresCleanup)
                _ = Fs.Stop(Mount);
            diskHandle.Free();
            NativeMemory.AlignedFree(Arena);
            NativeMemory.AlignedFree(blockBacking);
            NativeMemory.Free(Device);
            NativeMemory.Free(Pool);
            NativeMemory.Free(dmaSlot);
            NativeMemory.Free(hardware);
        }
    }
}
