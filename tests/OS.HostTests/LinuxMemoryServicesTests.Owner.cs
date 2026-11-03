using System.Runtime.InteropServices;
using OS.Kernel.Compatibility.LinuxX64;
using OS.Kernel.Memory;

internal static unsafe partial class LinuxMemoryServicesTests
{
    internal sealed class Owner : IDisposable
    {
        public readonly LinuxMemoryServices.State* State;
        public readonly LinuxMemoryServices.Admission Memory;
        public Owner(uint tables = TablePages, ulong quota = 0x400000000, uint backingPages = PoolPages, uint staticRows = 2, bool runtimeImage = false)
        {
            HostCheck.True(staticRows >= 2 && staticRows <= X64UserAddressSpace.MaximumRanges);
            HostCheck.True(!runtimeImage || staticRows >= 4);
            State = (LinuxMemoryServices.State*)NativeMemory.AlignedAlloc((nuint)sizeof(LinuxMemoryServices.State), 16);
            *State = default;
            byte* pool = (byte*)NativeMemory.AlignedAlloc(backingPages * 4096, 4096);
            uint allocatedTables = Math.Max(tables, TablePages);
            ulong* live = (ulong*)NativeMemory.AlignedAlloc(allocatedTables * 4096, 4096);
            ulong* candidate = (ulong*)NativeMemory.AlignedAlloc(allocatedTables * 4096, 4096);
            var ranges = (X64UserMappingRange*)NativeMemory.AlignedAlloc((nuint)(staticRows * sizeof(X64UserMappingRange)), 16);
            ranges[0] = new()
            {
                VirtualStart = 0x400000,
                PhysicalStart = 0x300000,
                Pages = 1,
                Protection = PageProtection.Read | PageProtection.Execute,
                Cache = PageCachePolicy.WriteBack,
                User = true
            };
            ranges[1] = new()
            {
                VirtualStart = 0x100000,
                PhysicalStart = 0x100000,
                Pages = 1,
                Protection = PageProtection.Read | PageProtection.Write,
                Cache = PageCachePolicy.WriteBack
            };
            for (uint i = 2; i < staticRows; i++)
                ranges[i] = new()
                {
                    VirtualStart = 0x800000 + i * 8192UL,
                    PhysicalStart = 0x800000 + i * 8192UL,
                    Pages = 1,
                    Protection = PageProtection.Read | PageProtection.Write,
                    Cache = PageCachePolicy.WriteBack
                };
            if (runtimeImage)
            {
                ranges[2] = new()
                {
                    VirtualStart = 0x533000,
                    PhysicalStart = 0x433000,
                    Pages = 1,
                    Protection = PageProtection.Read,
                    Cache = PageCachePolicy.WriteBack,
                    User = true
                };
                ranges[3] = new()
                {
                    VirtualStart = 0x6fe80000,
                    PhysicalStart = 0x600000,
                    Pages = 384,
                    Protection = PageProtection.Read | PageProtection.Write,
                    Cache = PageCachePolicy.WriteBack,
                    User = true
                };
            }

            new Span<byte>(pool, (int)backingPages * 4096).Fill(0xa5);
            new Span<ulong>(live, (int)allocatedTables * 512).Clear();
            new Span<ulong>(candidate, (int)allocatedTables * 512).Clear();
            HostCheck.Equal(X64PageTableEntryStatus.Success, X64PatMappingToken.TryCreate(0x0007040600070406, 0, 3, 255, out var pat));
            Memory = new()
            {
                Pool = pool,
                PoolPhysical = Pool,
                PoolPages = backingPages,
                RootPhysical = Root,
                TablePages = tables,
                Tables = live,
                CandidateTables = candidate,
                StaticRanges = ranges,
                StaticCount = staticRows,
                ReservationBytes = quota,
                ImageVirtualStart = runtimeImage ? 0x400000UL : 0,
                ImagePhysicalStart = runtimeImage ? 0x300000UL : 0,
                ImageBytes = runtimeImage ? 0x134000UL : 0,
                Conditions = X64PageTableEncodingPreconditions.CreateObserved(48, true, true, false, false, pat)
            };
        }

        public void Begin() => HostCheck.True(LinuxMemoryServices.Begin(State, Memory));
        public ulong Leaf(ulong va)
        {
            HostCheck.True(X64UserAddressSpace.TryReadLeaf(Memory.Tables, Root, Memory.TablePages, va, out ulong leaf));
            return leaf;
        }

        public byte* At(ulong va)
        {
            ulong leaf = Leaf(va), physical = leaf & 0x000ffffffffff000;
            HostCheck.True((leaf & 5) == 5 && physical >= Memory.PoolPhysical && physical < Memory.PoolPhysical + Memory.PoolPages * 4096UL);
            return Memory.Pool + physical - Memory.PoolPhysical + (va & 4095);
        }

        public byte[] Tables() => new ReadOnlySpan<byte>(Memory.Tables, (int)Memory.TablePages * 4096).ToArray();
        public void Dispose()
        {
            NativeMemory.AlignedFree(State);
            NativeMemory.AlignedFree(Memory.Pool);
            NativeMemory.AlignedFree(Memory.Tables);
            NativeMemory.AlignedFree(Memory.CandidateTables);
            NativeMemory.AlignedFree(Memory.StaticRanges);
        }
    }
}
