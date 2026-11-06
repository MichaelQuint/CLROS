using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using OS.Storage;
using C = OS.Storage.ContinuingFat32Mutation;
using B = OS.Storage.WritableBlockSession;

internal static unsafe partial class ContinuingFat32MutationTests
{
    internal sealed class Fixture : IDisposable
    {
        internal readonly WritableBlockSessionTests.Fixture Device;
        internal readonly C.State* State = (C.State*)NativeMemory.AllocZeroed((nuint)sizeof(C.State));
        private readonly byte* arena = (byte*)NativeMemory.AlignedAlloc(208896, 4096);
        internal readonly byte* Output = (byte*)NativeMemory.AllocZeroed(128);
        internal C.Admission Input;
        internal ulong BackingBytes;
        internal readonly List<(uint Type, ulong Sector)> Trace = [];
        internal bool Record = true;
        internal uint FailAt;
        internal int FailPublication;
        private int publication;
        internal Span<byte> Volatile => new(Device.Hardware->Medium, Sectors * 512);
        internal Span<byte> Persistent => new(Device.Hardware->Persistent, Sectors * 512);
        internal Fixture(byte sectorsPerCluster = 1, bool observe = false, uint spareSectors = 0)
        {
            ulong capacity = (ulong)Data + Clusters * (ulong)sectorsPerCluster;
            Device = new(capacity + spareSectors, observe);
            new Span<byte>(arena, 208896).Fill(0xa7);
            byte[] image = new byte[Sectors * 512];
            Span<byte> boot = image.AsSpan(First * 512, 512);
            Put16(boot, 11, 512);
            boot[13] = sectorsPerCluster;
            Put16(boot, 14, Reserved);
            boot[16] = 2;
            Put32(boot, 32, (uint)(capacity - First));
            Put32(boot, 36, FatSectors);
            Put32(boot, 44, 2);
            boot[510] = 0x55;
            boot[511] = 0xaa;
            foreach (int lba in new[] { First + Reserved, First + Reserved + FatSectors })
            {
                Put32(image, lba * 512, 0xffffff8);
                Put32(image, lba * 512 + 4, 0xfffffff);
                Put32(image, lba * 512 + 8, 0xfffffff);
                Put32(image, lba * 512 + 12, 0xfffffff);
                Put32(image, lba * 512 + 16, 6);
                Put32(image, lba * 512 + 24, 0xfffffff);
            }
            var keep = image.AsSpan(Data * 512, 32);
            "KEEP    TXT"u8.CopyTo(keep);
            keep[11] = 0x20;
            Put16(keep, 26, 3);
            Put32(keep, 28, 3);
            var file = image.AsSpan(Data * 512 + 32, 32);
            "DATA    BIN"u8.CopyTo(file);
            file[11] = 0x20;
            Put16(file, 26, 4);
            Put32(file, 28, 900);
            for (int i = 0; i < 900; i++)
                image[(i < 512 ? Data + 2 : Data + 4) * 512 + i % 512] = Value(i);
            Device.UseMedium(image, sectorsPerCluster != 1 || spareSectors != 0);
            HostCheck.Equal(B.Status.Success, Device.Start());
            ulong cursor = (ulong)arena + 4096;
            void* Take(ulong bytes)
            {
                cursor = (cursor + 15) & ~15UL;
                ulong value = cursor;
                cursor += bytes;
                return (void*)value;
            }
            Input = new()
            {
                Device = Device.State,
                Owner = 111,
                Epoch = 321,
                PartitionFirst = First,
                PartitionLast = capacity - 1,
                ChangeCapacity = C.JournalChanges,
                ChainCapacity = C.MaximumClusters,
                InputCapacity = C.WriteBytes,
                WorkBudget = 100000,
                Changes = (Fat32Mutation.Change*)Take(C.JournalChanges * (ulong)sizeof(Fat32Mutation.Change)),
                Before = (byte*)Take(C.JournalChanges * 512UL),
                After = (byte*)Take(C.JournalChanges * 512UL),
                DirectoryMap = (uint*)Take(C.MaximumClusters * 4UL),
                FileMap = (uint*)Take(C.MaximumClusters * 4UL),
                WalkMap = (uint*)Take(C.MaximumClusters * 4UL),
                Sector = (byte*)Take(512),
                Fat0 = (byte*)Take(512),
                Fat1 = (byte*)Take(512),
                Input = (byte*)Take(C.WriteBytes),
                Name = (ushort*)Take(C.NameWords * 2UL),
                LongName = (ushort*)Take(C.VfatWords * 2UL),
                EncodedEntries = (byte*)Take(C.EncodedEntryBytes)
            };
            BackingBytes = cursor - ((ulong)arena + 4096);
            new Span<byte>(arena + 4096, (int)BackingBytes).Clear();
        }
        internal void Mount()
        {
            HostCheck.Equal(C.Status.Pending, C.Start(State, Input));
            HostCheck.Equal(C.Status.Ready, Run());
            HostCheck.True(State->IsMounted);
        }
        internal C.Status Turn()
        {
            var status = C.Pump(State);
            if (Device.Hardware->Pending)
            {
                uint type = Device.Hardware->Type;
                if (Record)
                    Trace.Add((type, Device.Hardware->Sector));
                if (type != 0)
                    publication++;
                bool fail = Device.Hardware->Notifies == FailAt || type != 0 && publication == FailPublication;
                Device.Complete(length: fail && type == 0 ? 1U : uint.MaxValue, status: fail ? (byte)1 : (byte)0);
            }
            return status;
        }
        internal C.Status Run()
        {
            for (int i = 0; i < 100000; i++)
            {
                var status = Turn();
                if (status != C.Status.Pending)
                    return status;
            }
            throw new InvalidOperationException("continuing mutation did not terminate");
        }
        internal void Fat(uint cluster, uint value)
        {
            foreach (int lba in new[] { First + Reserved, First + Reserved + FatSectors })
                Put32(Volatile, lba * 512 + (int)cluster * 4, value);
        }
        public void Dispose()
        {
            HostCheck.Equal(B.Status.Success, B.Stop(Device.State));
            if (State->LastFailure != C.Status.Uninitialized && !State->Dirty)
                C.Stop(State);
            HostCheck.True(new ReadOnlySpan<byte>(arena, 4096).IndexOfAnyExcept((byte)0xa7) < 0 && new ReadOnlySpan<byte>(arena + 204800, 4096).IndexOfAnyExcept((byte)0xa7) < 0);
            Device.CheckGuards();
            Device.Dispose();
            NativeMemory.AlignedFree(arena);
            NativeMemory.Free(State);
            NativeMemory.Free(Output);
        }
    }
}
