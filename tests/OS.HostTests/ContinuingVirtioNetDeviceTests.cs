using System.Runtime.InteropServices;
using OS.Drivers;
using OS.Kernel.Platform;
using Nic = OS.Drivers.ContinuingVirtioNetDevice;

internal static unsafe class ContinuingVirtioNetDeviceTests
{
    [HostTest("net.device actual queue readback notification and repeated retained packets", HostLane.Fast, 0)]
    private static bool Continuing()
    {
        using var f = new Fixture(); HostCheck.Equal(Nic.Status.Success, f.Start());
        HostCheck.True(f.State->Running && f.Hardware->BusEnabled && f.Hardware->Primed);
        HostCheck.Equal(255UL, f.Pool->AllocatedMask);
        HostCheck.Equal(QueueTests.DeviceAddress + 2048, f.State->Receipt.Transmit.Descriptor);
        Span<byte> frame = stackalloc byte[14]; Frame(frame); Span<byte> output = stackalloc byte[14];
        for (uint i = 0; i < 32; i++)
        {
            HostCheck.Equal(Nic.Status.Success, Nic.Send(f.State, frame));
            f.Tx(i, 0); HostCheck.Equal(Nic.Status.Success, Nic.Poll(f.State, 8, out uint count)); HostCheck.Equal(1U, count);
            f.Rx(i); HostCheck.Equal(Nic.Status.Success, Nic.Receive(f.State, output, out uint bytes)); HostCheck.Equal(14U, bytes);
            HostCheck.Equal(255UL, f.Pool->AllocatedMask); HostCheck.Equal(0U, f.Hardware->Resets);
        }
        HostCheck.Equal(32UL, f.State->ReceivedFrames); HostCheck.Equal(32UL, f.State->ReclaimedTransmits);
        HostCheck.True(f.Hardware->RxNotifies == 33 && f.Hardware->TxNotifies == 32 && f.Hardware->DriverOkWrites == 1);
        HostCheck.Equal(Nic.Status.Success, Nic.Stop(f.State));
        HostCheck.True(f.State->Retired && f.Hardware->HeldAtResetFence && !f.Hardware->BusEnabled);
        HostCheck.Equal(0UL, f.Pool->AllocatedMask); HostCheck.True(new ReadOnlySpan<byte>(f.Data, 32768).IndexOfAnyExcept((byte)0) < 0);
        uint calls = f.Hardware->Calls;
        HostCheck.Equal(Nic.Status.InvalidState, Nic.Send(f.State, frame));
        HostCheck.Equal(Nic.Status.InvalidState, Nic.Stop(f.State)); HostCheck.Equal(calls, f.Hardware->Calls);
        return true;
    }

    [HostTest("net.device used completions win timeout even with partial pump budget", HostLane.Fast, 1)]
    private static bool CompletedBeforeBudget()
    {
        using var f = new Fixture(); HostCheck.Equal(Nic.Status.Success, f.Start()); Span<byte> frame = stackalloc byte[14]; Frame(frame);
        HostCheck.Equal(Nic.Status.Success, Nic.Send(f.State, frame)); HostCheck.Equal(Nic.Status.Success, Nic.Send(f.State, frame));
        f.Tx(0, 1); f.Tx(1, 0); f.Hardware->Now = 2000000000;
        HostCheck.Equal(Nic.Status.Success, Nic.Poll(f.State, 1, out uint count)); HostCheck.Equal(1U, count);
        HostCheck.Equal(Nic.Status.Success, Nic.Poll(f.State, 1, out count)); HostCheck.Equal(1U, count);
        f.Hardware->Now = 100000000000;
        HostCheck.Equal(Nic.Status.Success, Nic.Poll(f.State, 8, out count)); HostCheck.Equal(0U, count); // Idle lifetime is not a TX timeout.
        HostCheck.Equal(Nic.Status.Success, Nic.Stop(f.State)); return true;
    }

    [HostTest("net.device newer traffic cannot renew an older outstanding TX deadline", HostLane.Fast, 2)]
    private static bool OldDeadline()
    {
        using var f = new Fixture(); HostCheck.Equal(Nic.Status.Success, f.Start()); Span<byte> frame = stackalloc byte[14]; Frame(frame);
        HostCheck.Equal(Nic.Status.Success, Nic.Send(f.State, frame)); HostCheck.Equal(Nic.Status.Success, Nic.Send(f.State, frame));
        f.Hardware->Now = 999999999; f.Tx(0, 0);
        HostCheck.Equal(Nic.Status.Success, Nic.Poll(f.State, 1, out _));
        HostCheck.Equal(Nic.Status.Success, Nic.Send(f.State, frame)); f.Tx(1, 0); f.Hardware->Now = 1000000000;
        HostCheck.Equal(Nic.Status.Timeout, Nic.Poll(f.State, 1, out uint count)); HostCheck.Equal(1U, count);
        HostCheck.True(f.State->RequiresCleanup && !f.State->Running); HostCheck.Equal(255UL, f.Pool->AllocatedMask);
        HostCheck.Equal(Nic.Status.Success, Nic.Stop(f.State)); HostCheck.Equal(0UL, f.Pool->AllocatedMask); return true;
    }

    [HostTest("net.device reset retries never reuse stale quiescence or release early", HostLane.Fast, 3)]
    private static bool ResetRetry()
    {
        using var f = new Fixture(); HostCheck.Equal(Nic.Status.Success, f.Start());
        f.Hardware->DisableFailure = true;
        HostCheck.Equal(Nic.Status.ResetFailed, Nic.Stop(f.State));
        HostCheck.True(f.State->Receipt.ResetComplete && !f.State->Receipt.BusMasterDisabled);
        HostCheck.Equal(255UL, f.Pool->AllocatedMask);
        f.Hardware->DisableFailure = false; f.Hardware->ResetWriteFailure = true;
        HostCheck.Equal(Nic.Status.ResetFailed, Nic.Stop(f.State));
        HostCheck.True(!f.State->Receipt.ResetComplete && f.State->Receipt.BusMasterDisabled);
        HostCheck.Equal(255UL, f.Pool->AllocatedMask); HostCheck.True(*(ulong*)f.Data != 0);
        f.Hardware->ResetWriteFailure = false;
        HostCheck.Equal(Nic.Status.Success, Nic.Stop(f.State)); HostCheck.Equal(0UL, f.Pool->AllocatedMask); return true;
    }

    [HostTest("net.device setup and notification failures preserve original reason and cleanup", HostLane.Fast, 4)]
    private static bool FailedSetupAndNotify()
    {
        using (var f = new Fixture())
        {
            f.Hardware->WrongReadback = true;
            HostCheck.Equal(Nic.Status.SetupFailed, f.Start()); HostCheck.True(f.State->Retired);
            HostCheck.Equal(VirtioNetQueueStatus.WriteFailed, f.State->Receipt.Status); HostCheck.Equal(0UL, f.Pool->AllocatedMask);
        }
        using (var f = new Fixture())
        {
            HostCheck.Equal(Nic.Status.Success, f.Start()); f.Hardware->NotifyFailure = true;
            Span<byte> frame = stackalloc byte[14]; Frame(frame);
            HostCheck.Equal(Nic.Status.WriteFailed, Nic.Send(f.State, frame));
            HostCheck.True(f.State->RequiresCleanup && !f.State->Running); HostCheck.Equal(255UL, f.Pool->AllocatedMask);
            HostCheck.Equal(Nic.Status.Success, Nic.Stop(f.State));
        }
        return true;
    }

    [HostTest("net.device monotonic failure regression and overflow fail before unsafe reuse", HostLane.Fast, 5)]
    private static bool Clocks()
    {
        Span<byte> packet = stackalloc byte[14]; Frame(packet);
        for (int mode = 0; mode < 3; mode++)
        {
            using var f = new Fixture(); f.Hardware->Now = mode == 2 ? ulong.MaxValue - 1 : 10;
            HostCheck.Equal(Nic.Status.Success, f.Start());
            if (mode == 0) f.Hardware->ClockFailure = true;
            if (mode == 1) f.Hardware->Now = 9;
            Nic.Status result;
            if (mode == 2) result = Nic.Send(f.State, packet);
            else result = Nic.Poll(f.State, 8, out _);
            HostCheck.Equal(mode == 1 ? Nic.Status.ClockRegressed : Nic.Status.ClockFailed, result);
            HostCheck.True(f.State->RequiresCleanup && !f.State->Running);
            HostCheck.Equal(Nic.Status.Success, Nic.Stop(f.State));
        }
        return true;
    }

    [HostTest("net.device invalid admission copied state and outer metadata aliases reject without publication", HostLane.Fast, 6)]
    private static bool Admission()
    {
        using var f = new Fixture(); var a = f.Input;
        a.NegotiatedFeatures = 0; HostCheck.Equal(Nic.Status.UnsupportedFeatures, Nic.Start(f.State, a));
        a = f.Input; a.SlotCount = 15; HostCheck.Equal(Nic.Status.InvalidAccess, Nic.Start(f.State, a));
        a = f.Input; a.Slots = (VirtqueueDescriptorSlot*)f.Data; HostCheck.Equal(Nic.Status.InvalidAccess, Nic.Start(f.State, a));
        a = f.Input; a.Notify = a.Common; HostCheck.Equal(Nic.Status.InvalidAccess, Nic.Start(f.State, a));
        a = f.Input; a.Clock.Read = null; HostCheck.Equal(Nic.Status.InvalidAccess, Nic.Start(f.State, a));
        HostCheck.Equal(0U, f.Hardware->Calls); HostCheck.Equal(0UL, f.Pool->AllocatedMask);
        HostCheck.Equal(Nic.Status.Success, f.Start()); Nic.State copy = *f.State;
        HostCheck.Equal(Nic.Status.InvalidState, Nic.Poll(&copy, 8, out _));
        HostCheck.Equal(Nic.Status.InvalidStorage, Nic.Send(f.State, new ReadOnlySpan<byte>(f.State, 14)));
        HostCheck.Equal(Nic.Status.InvalidStorage, Nic.Receive(f.State, new Span<byte>(f.Pool, 14), out _));
        HostCheck.Equal(Nic.Status.Success, Nic.Stop(f.State)); return true;
    }

    [HostTest("net.device warmed retained send receive pump allocate no managed memory", HostLane.Fast, 7)]
    private static bool NoAllocation()
    {
        using var f = new Fixture(); HostCheck.Equal(Nic.Status.Success, f.Start()); Span<byte> frame = stackalloc byte[14]; Frame(frame); Span<byte> output = stackalloc byte[14];
        bool good = true;
        for (uint i = 0; i < 1001; i++)
        {
            if (i == 1) f.AllocationBefore = GC.GetAllocatedBytesForCurrentThread();
            good &= Nic.Send(f.State, frame) == Nic.Status.Success; f.Tx(i, 0);
            good &= Nic.Poll(f.State, 8, out _) == Nic.Status.Success; f.Rx(i);
            good &= Nic.Receive(f.State, output, out _) == Nic.Status.Success;
        }
        long after = GC.GetAllocatedBytesForCurrentThread(); HostCheck.True(good); HostCheck.Equal(f.AllocationBefore, after);
        HostCheck.Equal(Nic.Status.Success, Nic.Stop(f.State)); return true;
    }

    [HostTest("net.device failed cleanup fence retains backing until a fresh successful reset", HostLane.Fast, 8)]
    private static bool CleanupFence()
    {
        using var f = new Fixture(); HostCheck.Equal(Nic.Status.Success, f.Start());
        f.Hardware->ResetFenceFailure = true;
        HostCheck.Equal(Nic.Status.ResetFailed, Nic.Stop(f.State));
        HostCheck.Equal(VirtioNetQueueStatus.OrderingFailed, f.State->Receipt.CleanupStatus);
        HostCheck.True(f.State->RequiresCleanup && *(ulong*)f.Data != 0);
        HostCheck.Equal(255UL, f.Pool->AllocatedMask);
        f.Hardware->ResetFenceFailure = false;
        HostCheck.Equal(Nic.Status.Success, Nic.Stop(f.State)); HostCheck.Equal(0UL, f.Pool->AllocatedMask);
        return true;
    }

    [HostTest("net.device exhausted DMA leaves an existing allocation and its bytes unchanged", HostLane.Fast, 9)]
    private static bool ExhaustedPool()
    {
        using var f = new Fixture();
        HostCheck.Equal(DmaBufferStatus.Success, PageAlignedDmaBuffers.TryAllocate(f.Pool, 8, out var held));
        HostCheck.Equal(Nic.Status.DmaRejected, f.Start()); HostCheck.True(f.State->Retired);
        HostCheck.Equal(255UL, f.Pool->AllocatedMask);
        HostCheck.True(new ReadOnlySpan<byte>(f.Data, 32768).IndexOfAnyExcept((byte)0xa5) < 0);
        HostCheck.Equal(DmaBufferStatus.Success, PageAlignedDmaBuffers.TryRelease(f.Pool, held.Token));
        return true;
    }

    private static void Frame(Span<byte> frame) { frame.Clear(); frame[6] = 2; frame[12] = 8; frame[13] = 6; }
    private struct Registers { internal ulong Descriptor, Available, Used; internal ushort Size, Maximum, Vector, Enabled, Offset; }
    private struct Hardware
    {
        internal byte* Data; internal DmaBufferPool* Pool; internal Registers Rx, Tx;
        internal ulong Now; internal uint Calls, RxNotifies, TxNotifies, DriverOkWrites, Resets;
        internal ushort Selected, ConfigVector; internal byte Status;
        internal bool BusEnabled, Primed, HeldAtResetFence, DisableFailure, ResetWriteFailure, WrongReadback, NotifyFailure, ClockFailure, ResetFenceFailure;
    }
    [UnmanagedCallersOnly] private static byte Read8(void* c, ulong address, byte* value)
    { var h = (Hardware*)c; h->Calls++; if (address != QueueTests.CommonAddress + 20) return 0; *value = h->Status; return 1; }
    [UnmanagedCallersOnly] private static byte Write8(void* c, ulong address, byte value)
    {
        var h = (Hardware*)c; h->Calls++; if (address != QueueTests.CommonAddress + 20) return 0;
        if (value == 0) { if (h->ResetWriteFailure) return 0; h->Resets++; h->Status = 0; h->Rx.Enabled = h->Tx.Enabled = 0; return 1; }
        if (value != 15 || !h->BusEnabled || h->Rx.Enabled != 1 || h->Tx.Enabled != 1 || *(ushort*)(h->Data + 130) != 8) return 0;
        h->Status = 15; h->DriverOkWrites++; return 1;
    }
    [UnmanagedCallersOnly] private static byte Read16(void* c, ulong address, ushort* value)
    {
        var h = (Hardware*)c; h->Calls++; Registers* q = h->Selected == 0 ? &h->Rx : &h->Tx;
        *value = (address - QueueTests.CommonAddress) switch { 16 => h->ConfigVector, 18 => (ushort)2, 22 => h->Selected, 24 => q->Size == 0 ? q->Maximum : q->Size, 26 => q->Vector, 28 => q->Enabled, 30 => q->Offset, _ => ushort.MaxValue };
        return *value == ushort.MaxValue && address != QueueTests.CommonAddress + 16 && address != QueueTests.CommonAddress + 26 ? (byte)0 : (byte)1;
    }
    [UnmanagedCallersOnly] private static byte Write16(void* c, ulong address, uint value)
    {
        var h = (Hardware*)c; h->Calls++;
        if (address == QueueTests.NotifyAddress + 4 || address == QueueTests.NotifyAddress + 12)
        {
            if (h->Status != 15 || !h->BusEnabled || h->NotifyFailure) return 0;
            if (address == QueueTests.NotifyAddress + 4 && value == 0)
            {
                for (int i = 0; i < 8; i++) if (*(ulong*)(h->Data + i * 16) != QueueTests.DeviceAddress + 4096UL + (ulong)i * 1536 || *(ushort*)(h->Data + i * 16 + 12) != 2) return 0;
                h->Primed = true; h->RxNotifies++; return 1;
            }
            if (address == QueueTests.NotifyAddress + 12 && value == 1) { h->TxNotifies++; return 1; }
            return 0;
        }
        Registers* q = h->Selected == 0 ? &h->Rx : &h->Tx;
        switch (address - QueueTests.CommonAddress) { case 16: h->ConfigVector = (ushort)value; break; case 22: if (value > 1) return 0; h->Selected = (ushort)value; break; case 24: q->Size = (ushort)value; break; case 26: q->Vector = (ushort)value; break; case 28: q->Enabled = (ushort)value; break; default: return 0; } return 1;
    }
    [UnmanagedCallersOnly] private static byte Read32(void* c, ulong address, uint* value)
    {
        var h = (Hardware*)c; h->Calls++; ulong offset = address - QueueTests.CommonAddress;
        if (offset < 32 || offset > 52 || (offset & 3) != 0) return 0;
        Registers* q = h->Selected == 0 ? &h->Rx : &h->Tx;
        ulong bits = offset < 40 ? q->Descriptor : offset < 48 ? q->Available : q->Used;
        *value = (uint)(bits >> ((offset & 4) == 0 ? 0 : 32)); if (h->WrongReadback && offset == 32) *value ^= 1; return 1;
    }
    [UnmanagedCallersOnly] private static byte Write32(void* c, ulong address, uint value)
    {
        var h = (Hardware*)c; h->Calls++; ulong offset = address - QueueTests.CommonAddress;
        if (offset < 32 || offset > 52 || (offset & 3) != 0) return 0;
        Registers* q = h->Selected == 0 ? &h->Rx : &h->Tx; ulong* target = offset < 40 ? &q->Descriptor : offset < 48 ? &q->Available : &q->Used;
        int shift = (offset & 4) == 0 ? 0 : 32; *target = (*target & ~(0xffffffffUL << shift)) | ((ulong)value << shift); return 1;
    }
    [UnmanagedCallersOnly] private static byte Fence(void* c)
    { var h = (Hardware*)c; h->Calls++; if (h->Status == 0 && !h->BusEnabled) { if (h->ResetFenceFailure) return 0; if (h->Pool->AllocatedMask == 255) h->HeldAtResetFence = true; } return 1; }
    [UnmanagedCallersOnly] private static byte BusMaster(void* c, byte enabled)
    { var h = (Hardware*)c; h->Calls++; if (enabled == 0 && h->DisableFailure) return 0; h->BusEnabled = enabled != 0; return 1; }
    [UnmanagedCallersOnly] private static byte Clock(void* c, ulong* value)
    { var h = (Hardware*)c; h->Calls++; if (h->ClockFailure) return 0; *value = h->Now; return 1; }

    private sealed class Fixture : IDisposable
    {
        private static long epoch;
        internal readonly byte* Data = (byte*)NativeMemory.AlignedAlloc(32768, 4096);
        internal readonly Nic.State* State = (Nic.State*)NativeMemory.AllocZeroed((nuint)sizeof(Nic.State));
        internal readonly DmaBufferPool* Pool = (DmaBufferPool*)NativeMemory.AllocZeroed((nuint)sizeof(DmaBufferPool));
        private readonly DmaBufferSlot* poolSlots = (DmaBufferSlot*)NativeMemory.AllocZeroed(8, (nuint)sizeof(DmaBufferSlot));
        private readonly VirtqueueDescriptorSlot* slots = (VirtqueueDescriptorSlot*)NativeMemory.AllocZeroed(16, (nuint)sizeof(VirtqueueDescriptorSlot));
        private readonly VirtqueueDescriptorToken* tokens = (VirtqueueDescriptorToken*)NativeMemory.AllocZeroed(16, (nuint)sizeof(VirtqueueDescriptorToken));
        internal readonly Hardware* Hardware = (Hardware*)NativeMemory.AllocZeroed((nuint)sizeof(Hardware));
        private readonly VirtioMappedRegion common, notify;
        internal long AllocationBefore;
        internal Fixture()
        {
            using var regions = new QueueTests.Fixture(); common = regions.Common; notify = regions.Notify;
            new Span<byte>(Data, 32768).Fill(0xa5);
            HostCheck.Equal(DmaAddressStatus.Success, DmaAddressWindow.TryCreate((ulong)Data, QueueTests.PhysicalAddress, QueueTests.DeviceAddress, 32768, 64, out var window));
            HostCheck.Equal(DmaBufferStatus.Success, PageAlignedDmaBuffers.TryInitialize(Pool, window, poolSlots, 8, (ulong)Interlocked.Increment(ref epoch)));
            Hardware->Data = Data; Hardware->Pool = Pool; Hardware->Status = 11;
            Hardware->Rx.Maximum = 8; Hardware->Tx.Maximum = 16; Hardware->Rx.Offset = 1; Hardware->Tx.Offset = 3;
        }
        internal Nic.Admission Input => new() { Common = common, Notify = notify, NotifyMultiplier = 4, Pool = Pool,
            Slots = slots, SlotCount = 16, Tokens = tokens, TokenCount = 16, Epoch = 1,
            NegotiatedFeatures = BoundedVirtioFeatures.NetSingleBufferReceive, ResetLimit = 3, CompletionTimeoutNanos = 1000000000,
            Mmio = new() { Context = Hardware, Read8 = &Read8, Write8 = &Write8, Read16 = &Read16, Write16 = &Write16,
                Read32 = &Read32, Write32 = &Write32, Fence = &Fence, BusMaster = &BusMaster }, Clock = new() { Context = Hardware, Read = &Clock } };
        internal Nic.Status Start() => Nic.Start(State, Input);
        internal void Tx(uint ordinal, uint id) { byte* e = Data + 2200 + (ordinal & 7) * 8; *(uint*)e = id; *(uint*)(e + 4) = 0; *(ushort*)(Data + 2198) = unchecked((ushort)(ordinal + 1)); }
        internal void Rx(uint ordinal)
        {
            uint id = ordinal & 7; byte* buffer = Data + 4096 + id * 1536; new Span<byte>(buffer, 26).Clear(); *(ushort*)(buffer + 10) = 1; Frame(new Span<byte>(buffer + 12, 14));
            byte* e = Data + 152 + (ordinal & 7) * 8; *(uint*)e = id; *(uint*)(e + 4) = 26; *(ushort*)(Data + 150) = unchecked((ushort)(ordinal + 1));
        }
        public void Dispose() { NativeMemory.AlignedFree(Data); NativeMemory.Free(State); NativeMemory.Free(Pool); NativeMemory.Free(poolSlots); NativeMemory.Free(slots); NativeMemory.Free(tokens); NativeMemory.Free(Hardware); }
    }
}
