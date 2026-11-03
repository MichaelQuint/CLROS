using System.Reflection;
using System.Runtime.InteropServices;
using OS.Storage;
using OS.Std.Hash;
using K = OS.Storage.ContinuingFat32Bootstrap;
using B = OS.Storage.WritableBlockSession;
using C = OS.Storage.ContinuingFat32Mutation;

internal static unsafe partial class ContinuingFat32BootstrapTests
{
    private sealed class EngineMemory : IDisposable
    {
        internal readonly C.State* State = (C.State*)NativeMemory.AllocZeroed((nuint)sizeof(C.State));
        private readonly byte* bytes = (byte*)NativeMemory.AlignedAlloc(48 * 4096, 4096);
        internal C.Admission Input;
        internal EngineMemory(B.State* device, ulong first, ulong last)
        {
            ulong cursor = (ulong)bytes;
            ulong Take(ulong count)
            {
                ulong at = (cursor + 15) & ~15UL;
                cursor = at + count;
                return at;
            }
            Input = new()
            {
                Device = device,
                Owner = 17,
                Epoch = 18,
                PartitionFirst = first,
                PartitionLast = last,
                ChangeCapacity = C.JournalChanges,
                ChainCapacity = C.MaximumClusters,
                InputCapacity = C.WriteBytes,
                WorkBudget = 10000,
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
            HostCheck.True(cursor - (ulong)bytes <= 48 * 4096UL);
            new Span<byte>(bytes, 48 * 4096).Clear();
        }
        public void Dispose()
        {
            NativeMemory.AlignedFree(bytes);
            NativeMemory.Free(State);
        }
    }
}
