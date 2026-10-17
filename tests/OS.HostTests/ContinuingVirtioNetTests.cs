using System.Runtime.InteropServices;
using OS.Drivers;
using OS.Kernel.Platform;
using Net = OS.Drivers.ContinuingVirtioNet;

internal static unsafe class ContinuingVirtioNetTests
{
    // Independent size-eight wire layout without EVENT_IDX:128 descriptor
    // bytes,20 available bytes, then68 used bytes aligned to four.
    private const int RxAvailable = 128, RxUsed = 148, TxAvailable = 2176, TxUsed = 2196;

    [HostTest("net.continuing prepares eight RX buffers in the device address domain", HostLane.Fast, 0)]
    private static bool Layout()
    {
        using var f = new Fixture();
        HostCheck.Equal(Net.Status.Success, f.Start());
        HostCheck.Equal((ushort)8, *(ushort*)(f.Data + RxAvailable + 2));
        HostCheck.Equal((ushort)0, *(ushort*)(f.Data + TxAvailable + 2));
        for (int i = 0; i < 8; i++)
        {
            HostCheck.Equal(0x400000UL + 4096UL + (ulong)i * 1536, *(ulong*)(f.Data + i * 16));
            HostCheck.Equal(1536U, *(uint*)(f.Data + i * 16 + 8));
            HostCheck.Equal((ushort)2, *(ushort*)(f.Data + i * 16 + 12));
            HostCheck.Equal((ushort)i, *(ushort*)(f.Data + RxAvailable + 4 + i * 2));
        }
        HostCheck.Equal(Net.Status.Success, Net.ReleaseAfterReset(f.State, Quiescent()));
        HostCheck.True(f.State->Retired && new ReadOnlySpan<byte>(f.Data, 32768).IndexOfAnyExcept((byte)0) < 0);
        return true;
    }

    [HostTest("net.continuing TX capacity and out-of-order partial reclaim preserve pending packets", HostLane.Fast, 1)]
    private static bool Capacity()
    {
        using var f = new Fixture(); HostCheck.Equal(Net.Status.Success, f.Start());
        Span<byte> packet = stackalloc byte[14]; Frame(packet);
        for (int i = 0; i < 8; i++) HostCheck.Equal(Net.Status.Success, Net.Enqueue(f.State, packet));
        var before = new ReadOnlySpan<byte>(f.Data, 32768).ToArray();
        HostCheck.Equal(Net.Status.Full, Net.Enqueue(f.State, packet));
        HostCheck.True(before.AsSpan().SequenceEqual(new ReadOnlySpan<byte>(f.Data, 32768)));
        f.Completion(true, 0, 1, 0); f.Completion(true, 1, 0, 0);
        *(ushort*)(f.Data + TxUsed + 2) = 2;
        HostCheck.Equal(Net.Status.Success, Net.Reclaim(f.State, 1, out uint reclaimed));
        HostCheck.Equal(1U, reclaimed);
        HostCheck.Equal(Net.Status.Success, Net.Enqueue(f.State, packet));
        HostCheck.Equal((ushort)1, *(ushort*)(f.Data + TxAvailable + 4)); // Lowest released descriptor.
        HostCheck.Equal(Net.Status.Success, Net.Reclaim(f.State, 8, out reclaimed));
        HostCheck.Equal(1U, reclaimed);
        HostCheck.Equal(2UL, f.State->ReclaimedTransmits);
        HostCheck.Equal(Net.Status.Success, Net.ReleaseAfterReset(f.State, Quiescent()));
        return true;
    }

    [HostTest("net.continuing RX short output retries before copy and replenishment", HostLane.Fast, 2)]
    private static bool ReceiveRetry()
    {
        using var f = new Fixture(); HostCheck.Equal(Net.Status.Success, f.Start());
        f.Packet(3, 71); f.Completion(false, 0, 3, 26); *(ushort*)(f.Data + RxUsed + 2) = 1;
        Span<byte> small = stackalloc byte[13]; small.Fill(0xcc);
        HostCheck.Equal(Net.Status.ShortBuffer, Net.Receive(f.State, small, out uint bytes));
        HostCheck.Equal(14U, bytes); HostCheck.True(small.IndexOfAnyExcept((byte)0xcc) < 0);
        HostCheck.Equal((ushort)0, f.State->ReceiveUsedIndex);
        Span<byte> full = stackalloc byte[14];
        HostCheck.Equal(Net.Status.Success, Net.Receive(f.State, full, out bytes));
        HostCheck.Equal((byte)71, full[0]); HostCheck.Equal(14U, bytes);
        HostCheck.Equal((ushort)9, f.State->ReceiveAvailableIndex);
        HostCheck.Equal((ushort)3, *(ushort*)(f.Data + RxAvailable + 4));
        HostCheck.Equal(Net.Status.Empty, Net.Receive(f.State, full, out bytes));
        HostCheck.Equal(Net.Status.Success, Net.ReleaseAfterReset(f.State, Quiescent()));
        return true;
    }

    [HostTest("m07-028 an in-buffer VLAN frame is recycled before the next valid frame", HostLane.Fast, 9)]
    private static bool ContentDropPreservesReceive()
    {
        using var f = new Fixture(); HostCheck.Equal(Net.Status.Success, f.Start());
        Span<byte> rejected = new(f.Data + 4096, 1530); rejected.Clear();
        *(ushort*)(f.Data + 4096 + 10) = 1; Frame(rejected.Slice(12, 14));
        rejected[24] = 0x81; rejected[25] = 0; // Independent 1518-byte VLAN Ethernet frame.
        f.Packet(1, 71); f.Completion(false, 0, 0, 1530); f.Completion(false, 1, 1, 26);
        *(ushort*)(f.Data + RxUsed + 2) = 2;
        Span<byte> output = stackalloc byte[1514]; output.Fill(0xcc);
        HostCheck.Equal(Net.Status.Success, Net.Receive(f.State, output, out uint bytes));
        HostCheck.Equal(14U, bytes); HostCheck.Equal((byte)71, output[0]);
        HostCheck.True(output[14..].IndexOfAnyExcept((byte)0xcc) < 0 && !f.State->Faulted);
        HostCheck.Equal((ushort)2, f.State->ReceiveUsedIndex); HostCheck.Equal((ushort)10, f.State->ReceiveAvailableIndex);
        HostCheck.Equal(1UL, f.State->DroppedFrames); HostCheck.Equal(1UL, f.State->ReceivedFrames);
        HostCheck.Equal(Net.Status.Empty, Net.Receive(f.State, output, out _));
        HostCheck.Equal(Net.Status.Success, Net.ReleaseAfterReset(f.State, Quiescent())); return true;
    }

    [HostTest("net.continuing rejects malformed used batches before any partial reclaim", HostLane.Fast, 3)]
    private static bool MalformedCompletions()
    {
        Span<byte> frame = stackalloc byte[14]; Frame(frame);
        for (int variant = 0; variant < 6; variant++)
        {
            using var f = new Fixture(); HostCheck.Equal(Net.Status.Success, f.Start());
            HostCheck.Equal(Net.Status.Success, Net.Enqueue(f.State, frame));
            f.Completion(true, 0, 0, 0);
            f.Completion(true, 1, variant == 0 ? 8U : variant == 1 ? 0U : 7U, variant == 2 ? 1U : 0U);
            if (variant == 5) f.Completion(true, 0, 0, 1); // Valid owned ID: isolate illegal device-written TX length.
            *(ushort*)(f.Data + TxUsed + 2) = (ushort)(variant == 3 ? 9 : variant == 4 ? 0xffff : variant == 5 ? 1 : 2);
            HostCheck.Equal(Net.Status.InvalidCompletion, Net.Reclaim(f.State, 8, out uint count));
            HostCheck.Equal(0U, count); HostCheck.Equal((ushort)0, f.State->TransmitUsedIndex);
            HostCheck.True(f.State->Faulted && !f.State->Retired);
            HostCheck.Equal(Net.Status.Success, Net.ReleaseAfterReset(f.State, Quiescent()));
        }
        return true;
    }

    [HostTest("net.continuing unsafe RX metadata faults while unsupported content is recycled", HostLane.Fast, 4)]
    private static bool MalformedReceive()
    {
        Span<byte> output = stackalloc byte[1514];
        for (int variant = 0; variant < 5; variant++)
        {
            using var f = new Fixture(); HostCheck.Equal(Net.Status.Success, f.Start()); f.Packet(0, 51);
            f.Completion(false, 0, 0, variant == 0 ? uint.MaxValue : variant == 1 ? 25U : 26U);
            *(ushort*)(f.Data + RxUsed + 2) = (ushort)(variant == 2 ? 2 : 1);
            if (variant == 2) f.Completion(false, 1, 0, 26);
            if (variant == 3) f.Data[4096] = 1; // Actual no-offload header rejects checksum work.
            if (variant == 4) *(ushort*)(f.Data + 4096 + 10) = 2; // Bounded merged discard, never delivered.
            output.Fill(0xcc);
            HostCheck.Equal(variant is 0 or 2 ? Net.Status.InvalidCompletion : Net.Status.Empty,
                Net.Receive(f.State, output, out _));
            HostCheck.True(f.State->Faulted == (variant is 0 or 2) && output.IndexOfAnyExcept((byte)0xcc) < 0);
            if (variant == 4)
            {
                HostCheck.Equal((ushort)0, f.State->ReceiveUsedIndex); HostCheck.Equal(0UL, f.State->DroppedFrames);
                f.Packet(1, 99); f.Completion(false, 1, 1, 26); *(ushort*)(f.Data + RxUsed + 2) = 2;
                HostCheck.Equal(Net.Status.Empty, Net.Receive(f.State, output, out _));
                HostCheck.Equal(1UL, f.State->DroppedFrames); HostCheck.Equal((ushort)2, f.State->ReceiveUsedIndex);
                f.Packet(2, 71); f.Completion(false, 2, 2, 26); *(ushort*)(f.Data + RxUsed + 2) = 3;
                HostCheck.Equal(Net.Status.Success, Net.Receive(f.State, output, out uint bytes));
                HostCheck.Equal(14U, bytes); HostCheck.Equal((byte)71, output[0]);
            }
            else if (variant is 1 or 3) HostCheck.Equal(1UL, f.State->DroppedFrames);
            HostCheck.Equal(Net.Status.Success, Net.ReleaseAfterReset(f.State, Quiescent()));
        }
        return true;
    }

    [HostTest("m07-028 a full rejected RX batch is bounded and its recycled IDs deliver later traffic", HostLane.Fast, 11)]
    private static bool RejectedBatchIsBounded()
    {
        using var f = new Fixture(); HostCheck.Equal(Net.Status.Success, f.Start());
        for (uint i = 0; i < 8; i++)
        { f.Packet(i, 51); f.Data[4096 + i * 1536] = 1; f.Completion(false, i, i, 26); }
        *(ushort*)(f.Data + RxUsed + 2) = 8; Span<byte> output = stackalloc byte[1514]; output.Fill(0xcc);
        HostCheck.Equal(Net.Status.Empty, Net.Receive(f.State, output, out uint bytes));
        HostCheck.Equal(0U, bytes); HostCheck.Equal(8UL, f.State->DroppedFrames);
        HostCheck.Equal((ushort)8, f.State->ReceiveUsedIndex); HostCheck.Equal((ushort)16, f.State->ReceiveAvailableIndex);
        HostCheck.True(!f.State->Faulted && output.IndexOfAnyExcept((byte)0xcc) < 0);
        f.Packet(0, 71); f.Completion(false, 8, 0, 26); *(ushort*)(f.Data + RxUsed + 2) = 9;
        HostCheck.Equal(Net.Status.Success, Net.Receive(f.State, output, out bytes));
        HostCheck.Equal(14U, bytes); HostCheck.Equal((byte)71, output[0]);
        HostCheck.Equal(Net.Status.Success, Net.ReleaseAfterReset(f.State, Quiescent())); return true;
    }

    [HostTest("m07-028 a later unsafe completion prevents even an earlier content drop", HostLane.Fast, 10)]
    private static bool UnsafeBatchCannotPartiallyDrop()
    {
        using var f = new Fixture(); HostCheck.Equal(Net.Status.Success, f.Start()); f.Packet(0, 51);
        f.Data[4096] = 1; f.Completion(false, 0, 0, 26); f.Completion(false, 1, 1, Net.BufferBytes + 1);
        *(ushort*)(f.Data + RxUsed + 2) = 2; Span<byte> output = stackalloc byte[1514]; output.Fill(0xcc);
        HostCheck.Equal(Net.Status.InvalidCompletion, Net.Receive(f.State, output, out _));
        HostCheck.True(f.State->Faulted && output.IndexOfAnyExcept((byte)0xcc) < 0);
        HostCheck.Equal(0UL, f.State->DroppedFrames); HostCheck.Equal((ushort)0, f.State->ReceiveUsedIndex);
        HostCheck.Equal((ushort)8, f.State->ReceiveAvailableIndex);
        HostCheck.Equal(Net.Status.Success, Net.ReleaseAfterReset(f.State, Quiescent())); return true;
    }

    [HostTest("net.continuing real 16-bit RX and TX index wrap remains bounded", HostLane.Fast, 5)]
    private static bool Wrap()
    {
        using var f = new Fixture(); HostCheck.Equal(Net.Status.Success, f.Start());
        Span<byte> frame = stackalloc byte[14]; Frame(frame); Span<byte> output = stackalloc byte[14];
        bool good = true;
        for (uint i = 0; i < 65540; i++)
        {
            good &= Net.Enqueue(f.State, frame) == Net.Status.Success;
            f.Completion(true, i, 0, 0); *(ushort*)(f.Data + TxUsed + 2) = unchecked((ushort)(i + 1));
            good &= Net.Reclaim(f.State, 1, out uint count) == Net.Status.Success && count == 1;
            uint id = i & 7; f.Packet(id, (byte)i); f.Completion(false, i, id, 26);
            *(ushort*)(f.Data + RxUsed + 2) = unchecked((ushort)(i + 1));
            good &= Net.Receive(f.State, output, out uint bytes) == Net.Status.Success && bytes == 14 && output[0] == (byte)i;
            if (!good) break;
        }
        HostCheck.True(good);
        HostCheck.Equal((ushort)4, f.State->TransmitAvailableIndex); HostCheck.Equal((ushort)4, f.State->TransmitUsedIndex);
        HostCheck.Equal((ushort)12, f.State->ReceiveAvailableIndex); HostCheck.Equal((ushort)4, f.State->ReceiveUsedIndex);
        HostCheck.Equal(65540UL, f.State->ReceivedFrames);
        HostCheck.Equal(Net.Status.Success, Net.ReleaseAfterReset(f.State, Quiescent()));
        return true;
    }

    [HostTest("net.continuing admission aliases stale copies and reset proof retain ownership", HostLane.Fast, 6)]
    private static bool AdmissionAndReset()
    {
        using var f = new Fixture();
        HostCheck.Equal(Net.Status.InvalidStorage, Net.Initialize(f.State, f.Range, f.Slots, 15, f.Tokens, 16, 1, f.Hook, &Fence));
        HostCheck.Equal(Net.Status.Success, f.Start());
        Net.State copy = *f.State;
        HostCheck.Equal(Net.Status.InvalidState, Net.Enqueue(&copy, new ReadOnlySpan<byte>(f.Data + 512, 14)));
        HostCheck.Equal(Net.Status.InvalidStorage, Net.Enqueue(f.State, new ReadOnlySpan<byte>(f.Data + 512, 14)));
        HostCheck.Equal(Net.Status.InvalidStorage, Net.Receive(f.State, new Span<byte>(f.Tokens, 14), out _));
        HostCheck.Equal(Net.Status.InvalidState, Net.Reclaim(f.State, 0, out _));
        HostCheck.Equal(Net.Status.NotQuiescent, Net.ReleaseAfterReset(f.State, default));
        HostCheck.True(!f.State->Retired && *(ushort*)(f.Data + RxAvailable + 2) == 8);
        HostCheck.Equal(Net.Status.Success, Net.ReleaseAfterReset(f.State, Quiescent()));
        HostCheck.Equal(Net.Status.InvalidState, Net.ReleaseAfterReset(f.State, Quiescent()));
        HostCheck.Equal(Net.Status.InvalidState, f.Start());
        return true;
    }

    [HostTest("net.continuing fence failures retain all DMA until reset and a working fence", HostLane.Fast, 7)]
    private static bool FailedOrdering()
    {
        using var f = new Fixture(); HostCheck.Equal(Net.Status.Success, f.Start());
        f.Hook->Fail = f.Hook->Calls + 2;
        Span<byte> frame = stackalloc byte[14]; Frame(frame);
        HostCheck.Equal(Net.Status.OrderingFailed, Net.Enqueue(f.State, frame));
        HostCheck.True(f.State->Faulted && !f.State->Retired);
        f.Hook->Fail = f.Hook->Calls + 1;
        HostCheck.Equal(Net.Status.OrderingFailed, Net.ReleaseAfterReset(f.State, Quiescent()));
        HostCheck.True(!f.State->Retired);
        f.Hook->Fail = 0;
        HostCheck.Equal(Net.Status.Success, Net.ReleaseAfterReset(f.State, Quiescent()));
        return true;
    }

    [HostTest("net.continuing warmed queue operations allocate no managed storage", HostLane.Fast, 8)]
    private static bool NoAllocation()
    {
        using var f = new Fixture(); HostCheck.Equal(Net.Status.Success, f.Start());
        Span<byte> frame = stackalloc byte[14]; Frame(frame); Span<byte> output = stackalloc byte[14];
        ulong before = (ulong)GC.GetAllocatedBytesForCurrentThread(); bool good = true;
        for (uint i = 0; i < 1000; i++)
        {
            good &= Net.Enqueue(f.State, frame) == Net.Status.Success;
            f.Completion(true, i, 0, 0); *(ushort*)(f.Data + TxUsed + 2) = (ushort)(i + 1);
            good &= Net.Reclaim(f.State, 8, out _) == Net.Status.Success;
            f.Packet(i & 7, 37); f.Completion(false, i, i & 7, 26); *(ushort*)(f.Data + RxUsed + 2) = (ushort)(i + 1);
            good &= Net.Receive(f.State, output, out _) == Net.Status.Success;
        }
        ulong after = (ulong)GC.GetAllocatedBytesForCurrentThread();
        HostCheck.True(good); HostCheck.Equal(before, after);
        HostCheck.Equal(Net.Status.Success, Net.ReleaseAfterReset(f.State, Quiescent()));
        return true;
    }

    private static VirtioNetQueueResult Quiescent() => new()
    { ResetComplete = true, BusMasterDisabled = true, CleanupStatus = VirtioNetQueueStatus.Success };
    private static void Frame(Span<byte> bytes) { bytes.Clear(); bytes[6] = 2; bytes[12] = 8; bytes[13] = 6; }
    private struct FenceState { internal uint Calls, Fail; }
    [UnmanagedCallersOnly]
    private static byte Fence(void* context) { var s = (FenceState*)context; return ++s->Calls == s->Fail ? (byte)0 : (byte)1; }

    private sealed class Fixture : IDisposable
    {
        internal readonly Net.State* State = (Net.State*)NativeMemory.AllocZeroed((nuint)sizeof(Net.State));
        internal readonly VirtqueueDescriptorSlot* Slots = (VirtqueueDescriptorSlot*)NativeMemory.AllocZeroed(16, (nuint)sizeof(VirtqueueDescriptorSlot));
        internal readonly VirtqueueDescriptorToken* Tokens = (VirtqueueDescriptorToken*)NativeMemory.AllocZeroed(16, (nuint)sizeof(VirtqueueDescriptorToken));
        internal readonly FenceState* Hook = (FenceState*)NativeMemory.AllocZeroed((nuint)sizeof(FenceState));
        internal readonly byte* Data = (byte*)NativeMemory.AlignedAlloc(32768, 4096);
        internal readonly DmaAddressRange Range;
        internal Fixture()
        {
            new Span<byte>(Data, 32768).Clear();
            HostCheck.Equal(DmaAddressStatus.Success, DmaAddressWindow.TryCreate((ulong)Data, 0x200000, 0x400000, 32768, 64, out var window));
            HostCheck.Equal(DmaAddressStatus.Success, window.TrySlice(0, 32768, out Range));
        }
        internal Net.Status Start() => Net.Initialize(State, Range, Slots, 16, Tokens, 16, 1, Hook, &Fence);
        internal void Completion(bool tx, uint ordinal, uint id, uint length)
        { byte* e = Data + (tx ? TxUsed : RxUsed) + 4 + (ordinal & 7) * 8; *(uint*)e = id; *(uint*)(e + 4) = length; }
        internal void Packet(uint id, byte value)
        { byte* b = Data + 4096 + id * 1536; new Span<byte>(b, 26).Clear(); *(ushort*)(b + 10) = 1; Frame(new Span<byte>(b + 12, 14)); b[12] = value; }
        public void Dispose()
        { NativeMemory.Free(State); NativeMemory.Free(Slots); NativeMemory.Free(Tokens); NativeMemory.Free(Hook); NativeMemory.AlignedFree(Data); }
    }
}
