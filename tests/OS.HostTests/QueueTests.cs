using System.Runtime.InteropServices;
using OS.Drivers;
using OS.Kernel.Platform;

internal static unsafe class QueueTests
{
    internal const ulong CommonAddress = 0x400000, NotifyAddress = 0x500000;
    internal const ulong PhysicalAddress = 0x220000, DeviceAddress = 0x123400000000;

    internal struct QueueRegisters
    {
        public ulong Descriptor, Available, Used;
        public ushort Maximum, Size, Vector, Enabled, NotifyOffset;
    }
    internal struct Device
    {
        public byte* Data;
        public DmaBufferPool* Pool;
        public QueueRegisters Receive, Transmit;
        public uint Calls, FailCall, Fences, FailFence, ExecutionFences, Multiplier, Notifications, ResetReads;
        public uint CompletionId, CompletionLength, UsedReads, DriverOkWrites, EnableCalls;
        public ushort Queues, Selected, ConfigVector, CompletionIndex;
        public byte Status;
        public bool InvalidAccess, InvalidOrder, FailedCallback, BusEnabled, Sending, RxPrimed, WireObserved;
        public bool ReceiveNotified, TransmitNotified, NoCompletion, ResetStuck, DisableFailure, EnableFailure;
        public bool ResetRequested, ResetObserved, HeldDuringReset, CleanupFenceObserved;
        public bool WrongSelection, WrongAddress, BadOnNotify, BadAfterUsed;
    }

    [HostTest("virtio.net.queue configures disjoint RX TX rings and primes RX without DRIVER_OK", HostLane.Fast, 0)]
    private static void Pair()
    {
        using var f = new Fixture(); var r = f.Run();
        HostCheck.True(r.Succeeded && r.Receive.Published && !r.Transmit.Published && !r.TransmitCompleted);
        HostCheck.Equal((ushort)0, r.Receive.Index); HostCheck.Equal((ushort)1, r.Transmit.Index);
        HostCheck.Equal((ushort)8, r.Receive.Size); HostCheck.Equal((ushort)8, r.Transmit.Size);
        HostCheck.Equal((ushort)8, r.Receive.MaximumSize); HostCheck.Equal((ushort)16, r.Transmit.MaximumSize);
        HostCheck.Equal(DeviceAddress, r.Receive.Descriptor); HostCheck.Equal(DeviceAddress + 128, r.Receive.Available);
        HostCheck.Equal(DeviceAddress + 148, r.Receive.Used);
        HostCheck.Equal(DeviceAddress + 2048, r.Transmit.Descriptor); HostCheck.Equal(DeviceAddress + 2176, r.Transmit.Available);
        HostCheck.Equal(DeviceAddress + 2196, r.Transmit.Used);
        HostCheck.Equal(NotifyAddress + 4, r.Receive.Notify); HostCheck.Equal(NotifyAddress + 12, r.Transmit.Notify);
        HostCheck.True(Layout(f.State) && f.State->RxPrimed && f.State->HeldDuringReset && f.State->CleanupFenceObserved);
        HostCheck.Equal((ushort)1, *(ushort*)(f.Data + 130)); HostCheck.Equal((ushort)0, *(ushort*)(f.Data + 2178));
        HostCheck.Equal(0U, f.State->DriverOkWrites); HostCheck.Equal(0U, f.State->EnableCalls);
        HostCheck.Equal(0U, f.State->Notifications); HostCheck.Equal(4U, r.Fences);
        HostCheck.Equal(0UL, f.Pool->AllocatedMask); Clean(f);
        HostCheck.True(!default(VirtioNetQueueResult).Succeeded);
    }

    [HostTest("virtio.net.queue rejects queue selection size readback and notification bounds", HostLane.Fast, 1)]
    private static void Admission()
    {
        foreach (int invalid in new[] { 0, 1, 2, 3, 4, 5, 6 })
        {
            using var f = new Fixture();
            switch (invalid)
            {
                case 0: f.State->Queues = 1; break;
                case 1: f.State->Receive.Maximum = 4; break;
                case 2: f.State->Transmit.Maximum = 4; break;
                case 3: f.State->Transmit.Enabled = 1; break;
                case 4: f.State->WrongSelection = true; break;
                case 5: f.State->Transmit.NotifyOffset = 1024; break;
                case 6: f.State->WrongAddress = true; break;
            }
            var r = f.Run();
            HostCheck.Equal(invalid == 5 ? VirtioNetQueueStatus.InvalidNotify : invalid == 6
                ? VirtioNetQueueStatus.WriteFailed : VirtioNetQueueStatus.InvalidQueue, r.Status);
            HostCheck.True(!r.Succeeded && r.ResetComplete && r.BusMasterDisabled);
            HostCheck.Equal(0UL, f.Pool->AllocatedMask); HostCheck.Equal(0U, f.State->Notifications); Clean(f);
        }
    }

    [HostTest("virtio.net.queue rejects malformed access without callbacks or DMA writes", HostLane.Fast, 2)]
    private static void Access()
    {
        for (int missing = 0; missing < 8; missing++)
        {
            using var f = new Fixture(); var a = f.Access;
            switch (missing)
            {
                case 0: a.Read8 = null; break; case 1: a.Write8 = null; break;
                case 2: a.Read16 = null; break; case 3: a.Write16 = null; break;
                case 4: a.Read32 = null; break; case 5: a.Write32 = null; break;
                case 6: a.Fence = null; break; case 7: a.BusMaster = null; break;
            }
            HostCheck.Equal(VirtioNetQueueStatus.InvalidAccess, VirtioNetQueue.Run(f.Common, f.Notify, 4, f.Pool, a, 3).Status);
            NoEffects(f);
        }
        for (int invalid = 0; invalid < 5; invalid++)
        {
            using var f = new Fixture();
            HostCheck.Equal(VirtioNetQueueStatus.InvalidAccess, VirtioNetQueue.Run(invalid == 0 ? default : f.Common,
                invalid == 1 ? default : f.Notify, invalid == 2 ? 3U : 4U, f.Pool, f.Access,
                invalid == 3 ? 0U : invalid == 4 ? 1025U : 3U).Status);
            NoEffects(f);
        }
    }

    [HostTest("virtio.net.queue fences publication and retains DMA on uncertain cleanup ordering", HostLane.Fast, 3)]
    private static void Fences()
    {
        for (uint fail = 1; fail <= 4; fail++)
        {
            using var f = new Fixture(); f.State->FailFence = fail; var r = f.Run();
            HostCheck.True(!r.Succeeded && f.State->FailedCallback && r.ResetComplete && r.BusMasterDisabled);
            HostCheck.Equal(VirtioNetQueueStatus.OrderingFailed, fail == 4 ? r.CleanupStatus : r.Status);
            if (fail == 4) Retained(f, r); else HostCheck.True(r.DmaReleased);
            Clean(f);
        }
    }

    internal static void Clean(Fixture f) => HostCheck.True(!f.State->InvalidAccess && !f.State->InvalidOrder);
    private static void NoEffects(Fixture f)
    {
        HostCheck.Equal(0U, f.State->Calls); HostCheck.Equal(0UL, f.Pool->AllocatedMask);
        HostCheck.True(new ReadOnlySpan<byte>(f.Data, 4096).IndexOfAnyExcept((byte)0xa5) < 0); Clean(f);
    }
    internal static void Retained(Fixture f, VirtioNetQueueResult r)
    {
        HostCheck.True(!r.Succeeded && !r.DmaReleased); HostCheck.Equal(1UL, f.Pool->AllocatedMask);
        HostCheck.Equal(DmaBufferStatus.Exhausted, PageAlignedDmaBuffers.TryAllocate(f.Pool, 1, out _));
    }
    private static bool Begin(Device* d)
    { d->Calls++; if (d->Calls != d->FailCall) return true; d->FailedCallback = true; return false; }
    private static byte Bad(Device* d, bool order = false)
    { if (order) d->InvalidOrder = true; else d->InvalidAccess = true; return 0; }
    private static QueueRegisters* Selected(Device* d) => d->Selected == 0 ? &d->Receive : &d->Transmit;

    [UnmanagedCallersOnly]
    private static byte Read8(void* context, ulong address, byte* output)
    {
        Device* d = (Device*)context; if (!Begin(d)) return 0;
        if (address != CommonAddress + 20) return Bad(d);
        if (d->ResetRequested)
        { d->ResetReads++; if (!d->ResetStuck) { d->Status = 0; d->ResetObserved = true; } }
        *output = d->Status; return 1;
    }
    [UnmanagedCallersOnly]
    private static byte Write8(void* context, ulong address, byte value)
    {
        Device* d = (Device*)context; if (!Begin(d)) return 0;
        if (address != CommonAddress + 20 || value is not (0 or 15)) return Bad(d);
        if (value == 0)
        { d->ResetRequested = true; d->HeldDuringReset = d->Pool->AllocatedMask != 0; return 1; }
        if (!d->Sending || d->Status != 11 || !d->BusEnabled || !d->RxPrimed ||
            d->Receive.Enabled != 1 || d->Transmit.Enabled != 1 || *(ushort*)(d->Data + 130) != 1 ||
            *(ushort*)(d->Data + 2178) != 0) return Bad(d, true);
        d->DriverOkWrites++; d->Status = value; return 1;
    }
    [UnmanagedCallersOnly]
    private static byte Read16(void* context, ulong address, ushort* output)
    {
        Device* d = (Device*)context; if (!Begin(d)) return 0;
        if (address == CommonAddress + 18) { *output = d->Queues; return 1; }
        if (address == CommonAddress + 22) { *output = (ushort)(d->WrongSelection ? d->Selected ^ 1 : d->Selected); return 1; }
        if (address == CommonAddress + 16) { *output = d->ConfigVector; return 1; }
        if (address == (ulong)d->Data + 2198)
        {
            if (!d->TransmitNotified) return Bad(d, true);
            d->UsedReads++; *output = *(ushort*)address; return 1;
        }
        QueueRegisters* q = Selected(d);
        if (address == CommonAddress + 24) *output = q->Size == 0 ? q->Maximum : q->Size;
        else if (address == CommonAddress + 26) *output = q->Vector;
        else if (address == CommonAddress + 28) *output = q->Enabled;
        else if (address == CommonAddress + 30) *output = q->NotifyOffset;
        else return Bad(d);
        return 1;
    }
    [UnmanagedCallersOnly]
    private static byte Write16(void* context, ulong address, uint value)
    {
        Device* d = (Device*)context; if (!Begin(d)) return 0;
        if (value <= 1 && address == NotifyAddress + (ulong)(value == 0 ? d->Receive.NotifyOffset : d->Transmit.NotifyOffset) * d->Multiplier)
        {
            if (d->ExecutionFences != 5 || d->Status != 15 || !d->BusEnabled || !d->RxPrimed ||
                *(ushort*)(d->Data + 2178) != 1 || !Wire(d)) return Bad(d, true);
            if (value == 0)
            { if (d->ReceiveNotified) return Bad(d, true); d->ReceiveNotified = true; }
            else
            {
                if (!d->ReceiveNotified || d->TransmitNotified) return Bad(d, true);
                d->TransmitNotified = true; d->WireObserved = true;
                if (!d->NoCompletion)
                {
                    *(uint*)(d->Data + 2200) = d->CompletionId;
                    *(uint*)(d->Data + 2204) = d->CompletionLength;
                    *(ushort*)(d->Data + 2198) = d->CompletionIndex;
                }
                if (d->BadOnNotify) d->Status = 0x4f;
            }
            d->Notifications++; return 1;
        }
        if (address == CommonAddress + 22 && value <= 1) { d->Selected = (ushort)value; return 1; }
        if (address == CommonAddress + 16 && value == 0xffff) { d->ConfigVector = (ushort)value; return 1; }
        QueueRegisters* q = Selected(d);
        if (address == CommonAddress + 24 && value == 8 && d->ExecutionFences == 1 && q->Enabled == 0) q->Size = 8;
        else if (address == CommonAddress + 26 && value == 0xffff) q->Vector = (ushort)value;
        else if (address == CommonAddress + 28 && value == 1)
        {
            if (q->Size != 8 || q->Vector != 0xffff || d->ConfigVector != 0xffff || !Addresses(d, q)) return Bad(d, true);
            q->Enabled = 1;
        }
        else return Bad(d);
        return 1;
    }
    [UnmanagedCallersOnly]
    private static byte Read32(void* context, ulong address, uint* output)
    {
        Device* d = (Device*)context; if (!Begin(d)) return 0;
        if (address == (ulong)d->Data + 2200 || address == (ulong)d->Data + 2204)
        {
            if (d->ExecutionFences != 6) return Bad(d, true);
            *output = *(uint*)address;
            if (d->BadAfterUsed && address == (ulong)d->Data + 2204) d->Status = 0x4f;
            return 1;
        }
        if (address < CommonAddress + 32 || address > CommonAddress + 52 || (address & 3) != 0) return Bad(d);
        QueueRegisters* q = Selected(d); uint offset = (uint)(address - CommonAddress);
        ulong value = offset < 40 ? q->Descriptor : offset < 48 ? q->Available : q->Used;
        *output = (uint)(value >> ((offset & 4) == 0 ? 0 : 32));
        if (d->WrongAddress) *output ^= 1; return 1;
    }
    [UnmanagedCallersOnly]
    private static byte Write32(void* context, ulong address, uint value)
    {
        Device* d = (Device*)context; if (!Begin(d)) return 0;
        if (address < CommonAddress + 32 || address > CommonAddress + 52 || (address & 3) != 0) return Bad(d);
        QueueRegisters* q = Selected(d);
        if (d->ExecutionFences != 1 || q->Enabled != 0) return Bad(d, true);
        uint offset = (uint)(address - CommonAddress);
        ulong* target = offset < 40 ? &q->Descriptor : offset < 48 ? &q->Available : &q->Used;
        *target = (offset & 4) == 0 ? (*target & 0xffffffff00000000UL) | value : (*target & 0xffffffffUL) | ((ulong)value << 32);
        return 1;
    }
    [UnmanagedCallersOnly]
    private static byte Fence(void* context)
    {
        Device* d = (Device*)context; d->Fences++;
        if (!Begin(d)) return 0;
        if (d->Fences == d->FailFence) { d->FailedCallback = true; return 0; }
        if (d->ResetRequested)
        {
            if (!d->ResetObserved || d->BusEnabled) return Bad(d, true);
            d->CleanupFenceObserved = true; return 1;
        }
        d->ExecutionFences++;
        ushort rx = *(ushort*)(d->Data + 130), tx = *(ushort*)(d->Data + 2178);
        if (!Layout(d) || d->Pool->AllocatedMask != 1) return Bad(d, true);
        switch (d->ExecutionFences)
        {
            case 1: if (rx != 0 || tx != 0) return Bad(d, true); break;
            case 2: if (rx != 0 || tx != 0 || d->Receive.Enabled != 1 || d->Transmit.Enabled != 1) return Bad(d, true); break;
            case 3: if (rx != 1 || tx != 0 || d->Status != 11) return Bad(d, true); d->RxPrimed = true; break;
            case 4: if (rx != 1 || tx != 0 || d->Status != 15 || !d->BusEnabled || !Wire(d)) return Bad(d, true); break;
            case 5: if (rx != 1 || tx != 1) return Bad(d, true); break;
            case 6: if (!d->TransmitNotified || *(ushort*)(d->Data + 2198) == 0) return Bad(d, true); break;
            default: return Bad(d, true);
        }
        return 1;
    }
    [UnmanagedCallersOnly]
    private static byte BusMaster(void* context, byte enabled)
    {
        Device* d = (Device*)context; if (!Begin(d)) return 0;
        if (enabled > 1) return Bad(d);
        if (enabled == 0)
        {
            if (d->ResetRequested && d->DisableFailure) return 0;
            d->BusEnabled = false; return 1;
        }
        if (!d->Sending || !d->RxPrimed || d->Receive.Enabled != 1 || d->Transmit.Enabled != 1 || !Wire(d)) return Bad(d, true);
        d->EnableCalls++; d->BusEnabled = true;
        return d->EnableFailure ? (byte)0 : (byte)1;
    }
    private static bool Addresses(Device* d, QueueRegisters* q)
    {
        ulong start = DeviceAddress + (d->Selected == 0 ? 0UL : 2048UL);
        return q->Descriptor == start && q->Available == start + 128 && q->Used == start + 148;
    }
    internal static bool Layout(Device* d) =>
        *(ulong*)d->Data == DeviceAddress + 256 && *(uint*)(d->Data + 8) == 1526 && *(uint*)(d->Data + 12) == 2 &&
        *(ulong*)(d->Data + 2048) == DeviceAddress + 2304 && *(uint*)(d->Data + 2056) == 72 && *(uint*)(d->Data + 2060) == 0 &&
        *(ushort*)(d->Data + 128) == 1 && *(ushort*)(d->Data + 2176) == 1 &&
        *(ushort*)(d->Data + 132) == 0 && *(ushort*)(d->Data + 2180) == 0;
    internal static bool Wire(Device* d)
    {
        // Independent wire vector: twelve zero header bytes followed by the
        // 60-byte Ethernet frame and padding, without implementation constants.
        ReadOnlySpan<byte> header = [2, 0, 0, 0, 0, 2, 2, 0, 0, 0, 0, 1, 0x88, 0xb5,
            (byte)'C', (byte)'L', (byte)'R', (byte)'O', (byte)'S', (byte)'-', (byte)'M', (byte)'0', (byte)'7'];
        for (int i = 0; i < 72; i++)
            if (d->Data[2304 + i] != (i >= 12 && i < 12 + header.Length ? header[i - 12] : (byte)0)) return false;
        return true;
    }

    internal sealed class Fixture : IDisposable
    {
        private static long nextEpoch;
        internal readonly byte* Data = (byte*)NativeMemory.AlignedAlloc(4096, 4096);
        internal readonly DmaBufferPool* Pool = (DmaBufferPool*)NativeMemory.AllocZeroed((nuint)sizeof(DmaBufferPool));
        private readonly DmaBufferSlot* slot = (DmaBufferSlot*)NativeMemory.AllocZeroed((nuint)sizeof(DmaBufferSlot));
        internal readonly Device* State = (Device*)NativeMemory.AllocZeroed((nuint)sizeof(Device));
        internal readonly VirtioMappedRegion Common, Notify;
        internal VirtqueuePollingAccess Access => new() { Context = State, Read8 = &Read8, Write8 = &Write8,
            Read16 = &Read16, Write16 = &Write16, Read32 = &Read32, Write32 = &Write32, Fence = &Fence, BusMaster = &BusMaster };
        internal Fixture()
        {
            new Span<byte>(Data, 4096).Fill(0xa5);
            HostCheck.Equal(DmaAddressStatus.Success, DmaAddressWindow.TryCreate((ulong)Data, PhysicalAddress, DeviceAddress, 4096, 64, out var window));
            HostCheck.Equal(DmaBufferStatus.Success, PageAlignedDmaBuffers.TryInitialize(Pool, window, slot, 1,
                (ulong)System.Threading.Interlocked.Increment(ref nextEpoch)));
            State->Data = Data; State->Pool = Pool; State->Status = 11; State->Queues = 2; State->Multiplier = 4;
            State->Receive.Maximum = 8; State->Transmit.Maximum = 16;
            State->Receive.NotifyOffset = 1; State->Transmit.NotifyOffset = 3; State->CompletionIndex = 1;
            PciSizedBar* bars = stackalloc PciSizedBar[6]; for (int i = 0; i < 6; i++) bars[i] = default;
            HostCheck.Equal(PciBarProbeStatus.Success, BARPrefetchTypeRegressionTestsDerivedMOSAFailures.Decode(0x0000000c, 0xc0, 0xffffc00c, 0xffffffff, 4, out bars[4]));
            var caps = VirtioPciCapabilitySet.Create(new(VirtioPciCapabilityType.CommonConfiguration, 4, 0, 4096, 0),
                new(VirtioPciCapabilityType.NotifyConfiguration, 4, 0x3000, 4096, 4),
                new(VirtioPciCapabilityType.IsrStatus, 4, 0x1000, 4096, 0), new(VirtioPciCapabilityType.DeviceConfiguration, 4, 0x2000, 4096, 0));
            VirtioConfigurationRegion* regions = stackalloc VirtioConfigurationRegion[4];
            HostCheck.Equal(VirtioRegionStatus.Success, CommonNotifyISRDeviceConfigurationRegions.Plan(caps, bars, 6, regions, 4));
            HostCheck.Equal(VirtioRegionStatus.Success, CommonNotifyISRDeviceConfigurationRegions.Bind(regions[0], CommonAddress, out Common));
            HostCheck.Equal(VirtioRegionStatus.Success, CommonNotifyISRDeviceConfigurationRegions.BindNotificationPage(regions[1], 4096, NotifyAddress, out Notify));
        }
        internal VirtioNetQueueResult Run(bool transmit = false, uint multiplier = 4)
        {
            State->Sending = transmit; State->Multiplier = multiplier;
            return transmit ? FixedEthernetFrameReclaimDescriptors.Run(Common, Notify, multiplier, Pool, Access, 3, 3) :
                VirtioNetQueue.Run(Common, Notify, multiplier, Pool, Access, 3);
        }
        public void Dispose() { NativeMemory.Free(State); NativeMemory.Free(slot); NativeMemory.Free(Pool); NativeMemory.AlignedFree(Data); }
    }
}
