using System.Runtime.InteropServices;
using OS.Drivers;

internal static unsafe class DescriptorAllocationExhaustionInvalidIndexDoubleFreeTestsTests
{
    [HostTest("virtqueue.descriptors exhausts capacity and reuses lowest free index with fresh generation", HostLane.Fast, 0)]
    private static void CapacityAndReuse()
    {
        using var f = new Fixture(8, 1);
        VirtqueueDescriptorToken[] tokens = new VirtqueueDescriptorToken[8];
        for (ushort i = 0; i < 8; i++) { tokens[i] = Allocate(f.Pool); HostCheck.Equal(i, tokens[i].Index); }
        HostCheck.Equal((ushort)8, f.Pool->Capacity);
        HostCheck.Equal((ushort)8, f.Pool->AllocatedCount);
        byte[] before = Snapshot(f);
        HostCheck.Equal(VirtqueueDescriptorStatus.Exhausted, VirtqueueDescriptors.TryAllocate(f.Pool, out var rejected));
        Empty(rejected); Unchanged(f, before);
        HostCheck.Equal(VirtqueueDescriptorStatus.Success, VirtqueueDescriptors.TryRelease(f.Pool, tokens[1]));
        HostCheck.Equal(VirtqueueDescriptorStatus.Success, VirtqueueDescriptors.TryRelease(f.Pool, tokens[5]));
        var one = Allocate(f.Pool); var five = Allocate(f.Pool);
        HostCheck.Equal((ushort)1, one.Index); HostCheck.Equal((ushort)5, five.Index);
        before = Snapshot(f);
        HostCheck.Equal(VirtqueueDescriptorStatus.StaleToken, VirtqueueDescriptors.TryRelease(f.Pool, tokens[1]));
        HostCheck.Equal(VirtqueueDescriptorStatus.StaleToken, VirtqueueDescriptors.TryPublish(f.Pool, tokens[5]));
        Unchanged(f, before);
        HostCheck.Equal(VirtqueueDescriptorStatus.Success, VirtqueueDescriptors.TryRelease(f.Pool, one));
        before = Snapshot(f);
        HostCheck.Equal(VirtqueueDescriptorStatus.StaleToken, VirtqueueDescriptors.TryRelease(f.Pool, one));
        Unchanged(f, before);
    }

    [HostTest("virtqueue.descriptors checks metadata ownership transitions and in-flight release", HostLane.Fast, 1)]
    private static void Ownership()
    {
        using var f = new Fixture(1, 2);
        var token = Allocate(f.Pool);
        byte[] before = Snapshot(f);
        HostCheck.Equal(VirtqueueDescriptorStatus.InvalidOwnership, VirtqueueDescriptors.TryReclaim(f.Pool, token));
        Unchanged(f, before);
        HostCheck.Equal(VirtqueueDescriptorStatus.Success, VirtqueueDescriptors.TryPublish(f.Pool, token));
        before = Snapshot(f);
        HostCheck.Equal(VirtqueueDescriptorStatus.InvalidOwnership, VirtqueueDescriptors.TryPublish(f.Pool, token));
        HostCheck.Equal(VirtqueueDescriptorStatus.InvalidOwnership, VirtqueueDescriptors.TryRelease(f.Pool, token));
        HostCheck.Equal(VirtqueueDescriptorStatus.Exhausted, VirtqueueDescriptors.TryAllocate(f.Pool, out var rejected));
        Empty(rejected); Unchanged(f, before);
        HostCheck.Equal(VirtqueueDescriptorStatus.Success, VirtqueueDescriptors.TryReclaim(f.Pool, token));
        HostCheck.Equal(VirtqueueDescriptorStatus.Success, VirtqueueDescriptors.TryRelease(f.Pool, token));
        HostCheck.Equal((ushort)0, f.Pool->AllocatedCount);
        before = Snapshot(f);
        HostCheck.Equal(VirtqueueDescriptorStatus.StaleToken, VirtqueueDescriptors.TryReclaim(f.Pool, token));
        Unchanged(f, before);
    }

    [HostTest("virtqueue.descriptors rejects invalid indices epochs wrong pools and copied pool state", HostLane.Fast, 2)]
    private static void TokenIdentity()
    {
        using var first = new Fixture(8, 3);
        using var second = new Fixture(8, 4);
        var token = Allocate(first.Pool);
        byte[] before = Snapshot(first), other = Snapshot(second);
        HostCheck.Equal(VirtqueueDescriptorStatus.InvalidToken, VirtqueueDescriptors.TryRelease(second.Pool, token));
        HostCheck.Equal(VirtqueueDescriptorStatus.InvalidToken, VirtqueueDescriptors.TryPublish(first.Pool, default));
        // Host-only corrupt-token vectors independently exercise the opaque boundary.
        foreach (ushort index in new ushort[] { 8, ushort.MaxValue })
        {
            var invalid = token;
            *(ushort*)((byte*)&invalid + (int)Marshal.OffsetOf<VirtqueueDescriptorToken>("Index")) = index;
            HostCheck.Equal(VirtqueueDescriptorStatus.InvalidToken, VirtqueueDescriptors.TryRelease(first.Pool, invalid));
        }
        var wrongEpoch = token;
        *(ulong*)((byte*)&wrongEpoch + (int)Marshal.OffsetOf<VirtqueueDescriptorToken>("Epoch")) = 4;
        HostCheck.Equal(VirtqueueDescriptorStatus.InvalidToken, VirtqueueDescriptors.TryRelease(first.Pool, wrongEpoch));
        VirtqueueDescriptorPoolState copy = *first.Pool, empty = default;
        HostCheck.Equal(VirtqueueDescriptorStatus.InvalidPool, VirtqueueDescriptors.TryRelease(&copy, token));
        HostCheck.Equal(VirtqueueDescriptorStatus.InvalidPool, VirtqueueDescriptors.TryAllocate(&copy, out var rejected));
        Empty(rejected);
        HostCheck.Equal(VirtqueueDescriptorStatus.InvalidPool, VirtqueueDescriptors.TryAllocate(&empty, out rejected));
        Empty(rejected);
        HostCheck.Equal(VirtqueueDescriptorStatus.InvalidPool, VirtqueueDescriptors.TryAllocate(null, out rejected));
        Empty(rejected); Unchanged(first, before); Unchanged(second, other);
        HostCheck.True(default(VirtqueueDescriptorStatus) != VirtqueueDescriptorStatus.Success);
    }

    [HostTest("virtqueue.descriptors refuses generation wrap and live metadata rebinding", HostLane.Fast, 3)]
    private static void LifetimeBoundaries()
    {
        using var f = new Fixture(2, 5);
        // Avoid an impractical 2^64 allocation loop; retain the original slot binding.
        *(ulong*)((byte*)f.Slots + (int)Marshal.OffsetOf<VirtqueueDescriptorSlot>("Generation")) = ulong.MaxValue - 1;
        var terminal = Allocate(f.Pool);
        HostCheck.Equal(VirtqueueDescriptorStatus.Success, VirtqueueDescriptors.TryRelease(f.Pool, terminal));
        byte[] before = Snapshot(f);
        HostCheck.Equal(VirtqueueDescriptorStatus.GenerationExhausted, VirtqueueDescriptors.TryAllocate(f.Pool, out var rejected));
        Empty(rejected);
        HostCheck.Equal(VirtqueueDescriptorStatus.StaleToken, VirtqueueDescriptors.TryPublish(f.Pool, terminal));
        HostCheck.Equal(VirtqueueDescriptorStatus.AlreadyInitialized, VirtqueueDescriptors.TryInitialize(f.Pool, f.Slots, 2, 6));
        VirtqueueDescriptorPoolState second = default;
        HostCheck.Equal(VirtqueueDescriptorStatus.MetadataNotEmpty, VirtqueueDescriptors.TryInitialize(&second, f.Slots, 2, 6));
        HostCheck.True(!second.IsInitialized); Unchanged(f, before);
        HostCheck.Equal((ushort)0, f.Pool->AllocatedCount);
    }

    [HostTest("virtqueue.descriptors initialization validates sizes storage overlap and empty metadata atomically", HostLane.Fast, 4)]
    private static void Initialization()
    {
        using var f = new Fixture(8, 7, false);
        byte[] before = Snapshot(f);
        foreach (uint capacity in new uint[] { 0, 3, 32769, 65536, uint.MaxValue })
            HostCheck.Equal(VirtqueueDescriptorStatus.InvalidSize, VirtqueueDescriptors.TryInitialize(f.Pool, f.Slots, capacity, 7));
        HostCheck.Equal(VirtqueueDescriptorStatus.InvalidEpoch, VirtqueueDescriptors.TryInitialize(f.Pool, f.Slots, 8, 0));
        foreach (nint address in new nint[] { 0, (nint)((byte*)f.Slots + 1), (nint)f.Pool, unchecked((nint)(ulong.MaxValue - 7)) })
            HostCheck.Equal(VirtqueueDescriptorStatus.InvalidStorage,
                VirtqueueDescriptors.TryInitialize(f.Pool, (VirtqueueDescriptorSlot*)address, 8, 7));
        foreach (nint address in new nint[] { 0, (nint)((byte*)f.Pool + 1), unchecked((nint)(ulong.MaxValue - 7)) })
            HostCheck.Equal(VirtqueueDescriptorStatus.InvalidStorage,
                VirtqueueDescriptors.TryInitialize((VirtqueueDescriptorPoolState*)address, f.Slots, 8, 7));
        Unchanged(f, before);
        *(byte*)f.Slots = 1;
        before = Snapshot(f);
        HostCheck.Equal(VirtqueueDescriptorStatus.MetadataNotEmpty, VirtqueueDescriptors.TryInitialize(f.Pool, f.Slots, 8, 7));
        Unchanged(f, before);
        *(byte*)f.Slots = 0;
        HostCheck.Equal(VirtqueueDescriptorStatus.Success, VirtqueueDescriptors.TryInitialize(f.Pool, f.Slots, 8, 7));
        // A valid pool may not operate on uninitialized replacement slots.
        new Span<byte>(f.Slots, sizeof(VirtqueueDescriptorSlot)).Clear();
        before = Snapshot(f);
        HostCheck.Equal(VirtqueueDescriptorStatus.InvalidPool, VirtqueueDescriptors.TryAllocate(f.Pool, out var rejected));
        Empty(rejected); Unchanged(f, before);
        using var maximum = new Fixture(32768, 8);
        HostCheck.Equal((ushort)32768, maximum.Pool->Capacity);
        HostCheck.Equal((ushort)0, Allocate(maximum.Pool).Index);
    }

    private static VirtqueueDescriptorToken Allocate(VirtqueueDescriptorPoolState* pool)
    {
        HostCheck.Equal(VirtqueueDescriptorStatus.Success, VirtqueueDescriptors.TryAllocate(pool, out var token));
        HostCheck.True(token.IsInitialized);
        return token;
    }
    private static void Empty(VirtqueueDescriptorToken token) => HostCheck.True(!token.IsInitialized && token.Index == 0);
    private static byte[] Snapshot(Fixture fixture)
    {
        int poolBytes = sizeof(VirtqueueDescriptorPoolState), slotBytes = checked((int)fixture.Count * sizeof(VirtqueueDescriptorSlot));
        byte[] result = new byte[poolBytes + slotBytes];
        new ReadOnlySpan<byte>(fixture.Pool, poolBytes).CopyTo(result);
        new ReadOnlySpan<byte>(fixture.Slots, slotBytes).CopyTo(result.AsSpan(poolBytes));
        return result;
    }
    private static void Unchanged(Fixture fixture, byte[] before) => HostCheck.True(Snapshot(fixture).AsSpan().SequenceEqual(before));
    private sealed class Fixture : IDisposable
    {
        internal readonly VirtqueueDescriptorPoolState* Pool = (VirtqueueDescriptorPoolState*)NativeMemory.AllocZeroed((nuint)sizeof(VirtqueueDescriptorPoolState));
        internal readonly VirtqueueDescriptorSlot* Slots;
        internal readonly uint Count;
        internal Fixture(uint count, ulong epoch, bool initialize = true)
        {
            Count = count;
            Slots = (VirtqueueDescriptorSlot*)NativeMemory.AllocZeroed(count, (nuint)sizeof(VirtqueueDescriptorSlot));
            if (initialize) HostCheck.Equal(VirtqueueDescriptorStatus.Success, VirtqueueDescriptors.TryInitialize(Pool, Slots, count, epoch));
        }
        public void Dispose() { NativeMemory.Free(Slots); NativeMemory.Free(Pool); }
    }
}
