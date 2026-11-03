using System.Runtime.InteropServices;
using System.Runtime.CompilerServices;
using OS.Kernel.Compatibility.LinuxX64;
using OS.Kernel.Memory;
using OS.Std.Hash;

internal static unsafe partial class LinuxStartupServicesTests
{
    private sealed class Snapshot
    {
        private readonly byte[] tables, memory, startup, pool, staticRows;
        public Snapshot(Owner owner)
        {
            tables = owner.Memory.Tables();
            memory = new ReadOnlySpan<byte>(owner.Memory.State, sizeof(LinuxMemoryServices.State)).ToArray();
            startup = new ReadOnlySpan<byte>(owner.State, sizeof(LinuxStartupServices.State)).ToArray();
            pool = new ReadOnlySpan<byte>(owner.Memory.Memory.Pool, checked((int)owner.Memory.Memory.PoolPages * 4096)).ToArray();
            staticRows = new ReadOnlySpan<byte>(owner.Memory.Memory.StaticRanges, checked((int)owner.Memory.Memory.StaticCount * sizeof(X64UserMappingRange))).ToArray();
        }

        public void AssertUnchanged(Owner owner) => HostCheck.True(tables.AsSpan().SequenceEqual(owner.Memory.Tables()) && memory.AsSpan().SequenceEqual(new ReadOnlySpan<byte>(owner.Memory.State, sizeof(LinuxMemoryServices.State))) && startup.AsSpan().SequenceEqual(new ReadOnlySpan<byte>(owner.State, sizeof(LinuxStartupServices.State))) && pool.AsSpan().SequenceEqual(new ReadOnlySpan<byte>(owner.Memory.Memory.Pool, pool.Length)) && staticRows.AsSpan().SequenceEqual(new ReadOnlySpan<byte>(owner.Memory.Memory.StaticRanges, staticRows.Length)));
    }
}
