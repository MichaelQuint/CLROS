using System.Runtime.InteropServices;
using OS.Drivers;
using OS.Kernel.Platform;

internal static unsafe partial class SplitVirtqueuePollingNotificationCompletionTests
{
    private const ulong CommonAddress = 0x400000, NotifyAddress = 0x500000;
    private const ulong PhysicalAddress = 0x220000, DeviceAddress = 0x123400000000;
    // Independent VERSION_1-only size-eight layout: 128 descriptor bytes,
    // 4+16 available bytes, then the aligned used header and eight entries.
    private const uint Available = 128, Used = 148;
    [HostTest("virtqueue.polling transmits one readonly descriptor with distinct address domains and ordered cleanup", HostLane.Fast, 0)]
    private static void Success()
    {
        foreach (uint multiplier in new uint[]
        {
            0,
            2,
            4
        }

        )
        {
            using var f = new Fixture();
            f.State->Maximum = 16;
            f.State->NotifyOffset = multiplier == 0 ? ushort.MaxValue : (ushort)3;
            var r = f.Run(multiplier);
            HostCheck.True(r.Succeeded && r.Published && r.ResetComplete && r.BusMasterDisabled && r.DmaReleased);
            HostCheck.Equal((ushort)16, r.MaximumSize);
            HostCheck.Equal((ushort)8, r.Size);
            HostCheck.Equal((ulong)f.Data, r.CpuAddress);
            HostCheck.Equal(DeviceAddress, r.DeviceAddress);
            HostCheck.True(r.CpuAddress != PhysicalAddress && r.CpuAddress != DeviceAddress && PhysicalAddress != DeviceAddress);
            HostCheck.Equal(NotifyAddress + (ulong)f.State->NotifyOffset * multiplier, r.NotifyAddress);
            HostCheck.Equal((ushort)1, r.AvailableIndex);
            HostCheck.Equal((ushort)1, r.UsedIndex);
            HostCheck.Equal(0U, r.UsedId);
            HostCheck.Equal(0U, r.UsedLength);
            HostCheck.Equal(1U, r.Polls);
            HostCheck.Equal(5U, r.Fences);
            HostCheck.Equal(1U, f.State->Notifications);
            HostCheck.True(f.State->PayloadObserved && f.State->HeldDuringReset && f.State->ResetObserved && f.State->CleanupFenceObserved);
            HostCheck.Equal(0UL, f.Pool->AllocatedMask);
            Clean(f);
        }

        HostCheck.True(!default(VirtqueuePollingResult).Succeeded);
    }

    [HostTest("virtqueue.polling rejects invalid queue maxima enabled queues and notification extents before DMA", HostLane.Fast, 1)]
    private static void QueueAdmission()
    {
        foreach (ushort maximum in new ushort[]
        {
            0,
            3,
            4,
            32769
        }

        )
        {
            using var f = new Fixture();
            f.State->Maximum = maximum;
            HostCheck.Equal(VirtqueuePollingStatus.InvalidQueue, f.Run().Status);
            NoDma(f);
        }

        using (var f = new Fixture())
        {
            f.State->Enabled = 1;
            HostCheck.Equal(VirtqueuePollingStatus.InvalidQueue, f.Run().Status);
            NoDma(f);
        }

        using (var f = new Fixture())
        {
            f.State->Queues = 1;
            HostCheck.Equal(VirtqueuePollingStatus.InvalidQueue, f.Run().Status);
            NoDma(f);
        }

        foreach ((ushort offset, uint multiplier, uint bytes) in new[]
        {
            ((ushort)1, 4U, 2U),
            ((ushort)1024, 4U, 4096U),
            (ushort.MaxValue, 0x80000000U, 4096U)
        }

        )
        {
            using var f = new Fixture(bytes);
            f.State->NotifyOffset = offset;
            HostCheck.Equal(VirtqueuePollingStatus.InvalidNotify, f.Run(multiplier).Status);
            NoDma(f);
        }
    }

    [HostTest("virtqueue.polling rejects wrong stale completion identities indices lengths and missing completion", HostLane.Fast, 2)]
    private static void Completions()
    {
        foreach ((ushort index, uint id, uint length) in new[]
        {
            ((ushort)1, 1U, 0U),
            ((ushort)1, 8U, 0U),
            ((ushort)1, uint.MaxValue, 0U),
            ((ushort)2, 0U, 0U),
            ((ushort)9, 0U, 0U),
            (ushort.MaxValue, 0U, 0U),
            ((ushort)1, 0U, 1U),
            ((ushort)1, 0U, 72U)
        }

        )
        {
            using var f = new Fixture();
            f.State->CompletionIndex = index;
            f.State->CompletionId = id;
            f.State->CompletionLength = length;
            var r = f.Run();
            HostCheck.Equal(VirtqueuePollingStatus.InvalidCompletion, r.Status);
            HostCheck.True(!r.Succeeded && r.ResetComplete && r.DmaReleased && f.State->HeldDuringReset);
            Clean(f);
        }

        using var timeout = new Fixture();
        timeout.State->NoCompletion = true;
        var failed = timeout.Run();
        HostCheck.Equal(VirtqueuePollingStatus.Timeout, failed.Status);
        HostCheck.Equal(3U, failed.Polls);
        HostCheck.Equal((ushort)0, failed.UsedIndex);
        HostCheck.True(failed.ResetComplete && failed.DmaReleased);
        Clean(timeout);
    }

    [HostTest("virtqueue.polling checks fresh status and every fence failure", HostLane.Fast, 3)]
    private static void StatusAndOrdering()
    {
        for (int stage = 0; stage < 4; stage++)
        {
            using var f = new Fixture();
            if (stage == 0)
                f.State->Status = 0x8b;
            if (stage == 1)
                f.State->BadAfterEnable = true;
            if (stage == 2)
                f.State->BadOnNotify = true;
            if (stage == 3)
                f.State->BadAfterUsed = true;
            var r = f.Run();
            HostCheck.Equal(VirtqueuePollingStatus.StatusRejected, r.Status);
            HostCheck.True(r.ResetComplete && r.BusMasterDisabled && !r.Succeeded);
            Clean(f);
            HostCheck.Equal(0UL, f.Pool->AllocatedMask);
        }

        for (uint fence = 1; fence <= 5; fence++)
        {
            using var f = new Fixture();
            f.State->FailFence = fence;
            var r = f.Run();
            HostCheck.True(!r.Succeeded && f.State->FailedCallback);
            if (fence < 5)
            {
                HostCheck.Equal(VirtqueuePollingStatus.OrderingFailed, r.Status);
                HostCheck.True(r.DmaReleased);
            }
            else
            {
                HostCheck.Equal(VirtqueuePollingStatus.OrderingFailed, r.CleanupStatus);
                Retained(f, r);
            }

            Clean(f);
        }
    }

    [HostTest("virtqueue.polling retains DMA when reset or bus-master disable cannot be proven", HostLane.Fast, 4)]
    private static void CleanupFailures()
    {
        using (var f = new Fixture())
        {
            f.State->ResetStuck = true;
            var r = f.Run();
            HostCheck.Equal(VirtqueuePollingStatus.ResetFailed, r.CleanupStatus);
            HostCheck.Equal(3U, f.State->ResetReads);
            HostCheck.True(!r.ResetComplete);
            Retained(f, r);
            Clean(f);
        }

        using (var f = new Fixture())
        {
            f.State->DisableFailure = true;
            var r = f.Run();
            HostCheck.Equal(VirtqueuePollingStatus.BusMasterFailed, r.CleanupStatus);
            HostCheck.True(r.ResetComplete && !r.BusMasterDisabled);
            Retained(f, r);
            Clean(f);
        }

        using (var f = new Fixture())
        {
            f.State->EnableFailure = true; // Callback performs its effect before reporting failure.
            var r = f.Run();
            HostCheck.Equal(VirtqueuePollingStatus.BusMasterFailed, r.Status);
            HostCheck.True(r.ResetComplete && r.BusMasterDisabled && r.DmaReleased);
            Clean(f);
        }
    }

    [HostTest("virtqueue.polling rejects malformed access without effects and stops on every callback failure", HostLane.Fast, 5)]
    private static void AccessFailures()
    {
        for (int missing = 0; missing < 8; missing++)
        {
            using var f = new Fixture();
            var a = f.Access;
            switch (missing)
            {
                case 0:
                    a.Read8 = null;
                    break;
                case 1:
                    a.Write8 = null;
                    break;
                case 2:
                    a.Read16 = null;
                    break;
                case 3:
                    a.Write16 = null;
                    break;
                case 4:
                    a.Read32 = null;
                    break;
                case 5:
                    a.Write32 = null;
                    break;
                case 6:
                    a.Fence = null;
                    break;
                case 7:
                    a.BusMaster = null;
                    break;
            }

            HostCheck.Equal(VirtqueuePollingStatus.InvalidAccess, SplitVirtqueuePollingNotificationCompletion.Run(f.Common, f.Notify, 4, f.Pool, a, 3, 3).Status);
            HostCheck.Equal(0U, f.State->Calls);
            NoDma(f);
        }

        for (int invalid = 0; invalid < 7; invalid++)
        {
            using var f = new Fixture();
            var r = SplitVirtqueuePollingNotificationCompletion.Run(invalid == 0 ? default : f.Common, invalid == 1 ? default : f.Notify, invalid == 2 ? 3U : 4U, f.Pool, f.Access, invalid == 3 ? 0U : invalid == 4 ? 1000001U : 3U, invalid == 5 ? 0U : invalid == 6 ? 1025U : 3U);
            HostCheck.Equal(VirtqueuePollingStatus.InvalidAccess, r.Status);
            HostCheck.Equal(0U, f.State->Calls);
            NoDma(f);
        }

        uint calls;
        using (var baseline = new Fixture())
        {
            HostCheck.True(baseline.Run().Succeeded);
            calls = baseline.State->Calls;
        }

        for (uint fail = 1; fail <= calls; fail++)
        {
            using var f = new Fixture();
            f.State->FailCall = fail;
            var r = f.Run();
            HostCheck.True(f.State->FailedCallback && !r.Succeeded);
            Clean(f);
            if (r.DmaReleased)
                HostCheck.True(r.ResetComplete && r.BusMasterDisabled && f.State->CleanupFenceObserved);
            if (f.Pool->AllocatedMask != 0)
                Retained(f, r);
        }
    }

    private static bool Begin(Device* d)
    {
        d->Calls++;
        if (d->Calls != d->FailCall)
            return true;
        d->FailedCallback = true;
        return false;
    }

    [HostTest("virtqueue.interrupt requests entry zero waits before acquisition and masks before reset", HostLane.Fast, 6)]
    private static void InterruptSuccess()
    {
        using var f = new Fixture();
        var r = f.RunInterrupt();
        HostCheck.True(r.Succeeded && r.InterruptObserved && r.InterruptsMasked && r.Polls == 1 && r.Fences == 5);
        HostCheck.True(f.State->Vector == 0 && f.State->WaitCalls == 1 && f.State->UsedReads == 1 && f.State->MaskAttempted);
        HostCheck.Equal((ushort)0, *(ushort*)(f.Data + Available));
        HostCheck.True(r.ResetComplete && r.BusMasterDisabled && r.DmaReleased && f.State->HeldDuringReset);
        Clean(f);
        using var polling = new Fixture();
        var p = polling.Run();
        HostCheck.True(p.Succeeded && !p.InterruptObserved && !p.InterruptsMasked && polling.State->WaitCalls == 0 && !polling.State->MaskAttempted && polling.State->Vector == 0xffff && *(ushort*)(polling.Data + Available) == 1);
        Clean(polling);
    }

    [HostTest("virtqueue.interrupt rejects absent IRQ without polling fallback and retains uncertain masking", HostLane.Fast, 7)]
    private static void InterruptFailures()
    {
        using (var f = new Fixture())
        {
            f.State->WaitFailure = true;
            var r = f.RunInterrupt();
            HostCheck.True(r.Status == VirtqueuePollingStatus.InterruptFailed && !r.InterruptObserved && r.Polls == 0 && f.State->UsedReads == 0 && r.InterruptsMasked && r.DmaReleased);
            Clean(f);
        }

        using (var f = new Fixture())
        {
            f.State->NoCompletion = true;
            var r = f.RunInterrupt();
            HostCheck.True(r.Status == VirtqueuePollingStatus.Timeout && r.InterruptObserved && r.Polls == 1 && r.DmaReleased);
            Clean(f);
        }

        using (var f = new Fixture())
        {
            f.State->MaskFailure = true;
            var r = f.RunInterrupt();
            HostCheck.True(r.Status == VirtqueuePollingStatus.Success && r.CleanupStatus == VirtqueuePollingStatus.InterruptFailed && !r.InterruptsMasked && r.ResetComplete && r.BusMasterDisabled);
            Retained(f, r);
            Clean(f);
        }

        using (var f = new Fixture())
        {
            f.State->RejectVector = true;
            var r = f.RunInterrupt();
            HostCheck.True(r.Status == VirtqueuePollingStatus.InvalidQueue && !r.Published && !r.InterruptObserved && r.InterruptsMasked && r.DmaReleased && f.State->WaitCalls == 0);
            Clean(f);
        }

        for (int missing = 0; missing < 2; missing++)
        {
            using var f = new Fixture();
            var irq = f.InterruptAccess;
            if (missing == 0)
                irq.Wait = null;
            else
                irq.Mask = null;
            var r = SplitVirtqueuePollingNotificationCompletion.RunInterrupt(f.Common, f.Notify, 4, f.Pool, f.Access, 3, 3, irq);
            HostCheck.True(r.Status == VirtqueuePollingStatus.InvalidAccess && f.State->Calls == 0 && !f.State->MaskAttempted);
            NoDma(f);
        }
    }

    private static byte Bad(Device* d, bool order = false)
    {
        if (order)
            d->InvalidOrder = true;
        else
            d->InvalidAccess = true;
        return 0;
    }

    [UnmanagedCallersOnly]
    private static byte Read8(void* context, ulong address, byte* output)
    {
        Device* d = (Device*)context;
        if (!Begin(d))
            return 0;
        if (address != CommonAddress + 20)
            return Bad(d);
        if (d->ResetRequested)
        {
            d->ResetReads++;
            if (!d->ResetStuck)
            {
                d->Status = 0;
                d->ResetObserved = true;
            }
        }

        *output = d->Status;
        return 1;
    }

    [UnmanagedCallersOnly]
    private static byte Write8(void* context, ulong address, byte value)
    {
        Device* d = (Device*)context;
        if (!Begin(d))
            return 0;
        if (address != CommonAddress + 20 || (value != 0 && value != 15))
            return Bad(d);
        if (value == 0)
        {
            if (d->InterruptMode && !d->MaskAttempted)
                return Bad(d, true);
            d->ResetRequested = true;
            d->HeldDuringReset = d->Pool->AllocatedMask != 0;
            return 1;
        }

        if (d->Status != 11 || d->Enabled != 1 || !d->BusEnabled)
            return Bad(d, true);
        d->Status = value;
        return 1;
    }

    [UnmanagedCallersOnly]
    private static byte Read16(void* context, ulong address, ushort* output)
    {
        Device* d = (Device*)context;
        if (!Begin(d))
            return 0;
        if (address == CommonAddress + 18)
        {
            *output = d->Queues;
            return 1;
        }

        if (address == (ulong)d->Data + Used + 2)
        {
            if (!d->Notified || (d->InterruptMode && !d->WaitObserved))
                return Bad(d, true);
            d->UsedReads++;
            *output = *(ushort*)(d->Data + Used + 2);
            return 1;
        }

        if (d->SelectedQueue != 1)
            return Bad(d, true);
        if (address == CommonAddress + 24)
            *output = d->Size == 0 ? d->Maximum : d->Size;
        else if (address == CommonAddress + 26)
            *output = d->Vector;
        else if (address == CommonAddress + 28)
            *output = d->Enabled;
        else if (address == CommonAddress + 30)
            *output = d->NotifyOffset;
        else
            return Bad(d);
        return 1;
    }

    [UnmanagedCallersOnly]
    private static byte Write16(void* context, ulong address, uint value)
    {
        Device* d = (Device*)context;
        if (!Begin(d))
            return 0;
        if (address == NotifyAddress + (ulong)d->NotifyOffset * d->Multiplier)
        {
            if (value != 1 || d->Status != 15 || !d->BusEnabled || d->FenceMask != 7 || !Addresses(d) || !Payload(d))
                return Bad(d, true);
            d->Notifications++;
            d->Notified = true;
            d->PayloadObserved = true;
            if (!d->NoCompletion && !d->InterruptMode)
            {
                *(uint*)(d->Data + Used + 4) = d->CompletionId;
                *(uint*)(d->Data + Used + 8) = d->CompletionLength;
                *(ushort*)(d->Data + Used + 2) = d->CompletionIndex;
            }

            if (d->BadOnNotify)
                d->Status = 0x4f;
            return 1;
        }

        if (address == CommonAddress + 22 && value == 1)
            d->SelectedQueue = 1;
        else if (address == CommonAddress + 24 && value == 8 && (d->FenceMask & 1) != 0)
            d->Size = 8;
        else if (address == CommonAddress + 26 && value == (d->InterruptMode ? 0U : 0xffffU))
            d->Vector = d->RejectVector ? (ushort)0xffff : (ushort)value;
        else if (address == CommonAddress + 28 && value == 1 && Addresses(d))
            d->Enabled = 1;
        else
            return Bad(d);
        return 1;
    }

    [UnmanagedCallersOnly]
    private static byte Read32(void* context, ulong address, uint* output)
    {
        Device* d = (Device*)context;
        if (!Begin(d))
            return 0;
        if (address == (ulong)d->Data + Used + 4 || address == (ulong)d->Data + Used + 8)
        {
            if ((d->FenceMask & 8) == 0)
                return Bad(d, true);
            *output = *(uint*)address;
            if (d->BadAfterUsed && address == (ulong)d->Data + Used + 8)
                d->Status = 0x4f;
            return 1;
        }

        if (address < CommonAddress + 32 || address > CommonAddress + 52 || (address & 3) != 0)
            return Bad(d);
        uint offset = (uint)(address - CommonAddress);
        ulong value = offset < 40 ? d->Descriptor : offset < 48 ? d->AvailableAddress : d->UsedAddress;
        *output = (uint)(value >> ((offset & 4) == 0 ? 0 : 32));
        return 1;
    }

    [UnmanagedCallersOnly]
    private static byte Write32(void* context, ulong address, uint value)
    {
        Device* d = (Device*)context;
        if (!Begin(d))
            return 0;
        if (address < CommonAddress + 32 || address > CommonAddress + 52 || (address & 3) != 0)
            return Bad(d);
        if (d->FenceMask != 1 || d->Enabled != 0)
            return Bad(d, true);
        uint offset = (uint)(address - CommonAddress);
        ulong* target = offset < 40 ? &d->Descriptor : offset < 48 ? &d->AvailableAddress : &d->UsedAddress;
        *target = (offset & 4) == 0 ? (*target & 0xffffffff00000000UL) | value : (*target & 0xffffffffUL) | ((ulong)value << 32);
        return 1;
    }

    [UnmanagedCallersOnly]
    private static byte Fence(void* context)
    {
        Device* d = (Device*)context;
        d->Fences++;
        if (!Begin(d))
            return 0;
        if (d->Fences == d->FailFence)
        {
            d->FailedCallback = true;
            return 0;
        }

        if (d->ResetRequested)
        {
            if (!d->ResetObserved || d->BusEnabled || (d->Notified && d->Pool->AllocatedMask == 0))
                return Bad(d, true);
            d->CleanupFenceObserved = true;
            return 1;
        }

        d->ExecutionFences++;
        ushort available = *(ushort*)(d->Data + Available + 2);
        if (d->ExecutionFences <= 2 && available != 0)
            return Bad(d, true);
        if (d->ExecutionFences == 2 && (d->Status != 15 || !d->BusEnabled))
            return Bad(d, true);
        if (d->ExecutionFences == 3 && available != 1)
            return Bad(d, true);
        if (d->ExecutionFences == 4 && (!d->Notified || *(ushort*)(d->Data + Used + 2) != 1))
            return Bad(d, true);
        if (d->ExecutionFences > 4)
            return Bad(d, true);
        d->FenceMask |= 1U << (int)(d->ExecutionFences - 1);
        return 1;
    }

    [UnmanagedCallersOnly]
    private static byte BusMaster(void* context, byte enabled)
    {
        Device* d = (Device*)context;
        if (!Begin(d))
            return 0;
        if (enabled > 1)
            return Bad(d);
        if (enabled == 0)
        {
            if (d->Notified && d->Pool->AllocatedMask == 0)
                return Bad(d, true);
            if (d->DisableFailure)
                return 0;
            d->BusEnabled = false;
            return 1;
        }

        if (d->Enabled != 1 || !Addresses(d))
            return Bad(d, true);
        d->BusEnabled = true;
        if (d->BadAfterEnable)
            d->Status = 0x4b;
        return d->EnableFailure ? (byte)0 : (byte)1;
    }

    private static bool Addresses(Device* d) => d->Descriptor == DeviceAddress && d->AvailableAddress == DeviceAddress + Available && d->UsedAddress == DeviceAddress + Used;
    private static bool Payload(Device* d)
    {
        if (*(ulong*)d->Data != DeviceAddress + 256 || *(uint*)(d->Data + 8) != 72 || *(uint*)(d->Data + 12) != 0 || *(ushort*)(d->Data + Available) != (d->InterruptMode ? 0 : 1) || *(ushort*)(d->Data + Available + 2) != 1 || *(ushort*)(d->Data + Available + 4) != 0)
            return false;
        for (uint i = 0; i < 72; i++)
        {
            byte expected = i == 12 || i == 17 || i == 18 ? (byte)2 : i == 23 ? (byte)1 : i == 24 ? (byte)0x88 : i == 25 ? (byte)0xb5 : (byte)0;
            if (d->Data[256 + i] != expected)
                return false;
        }

        return true;
    }

    [UnmanagedCallersOnly]
    private static byte WaitInterrupt(void* context, uint limit)
    {
        Device* d = (Device*)context;
        d->WaitCalls++;
        if (!d->InterruptMode || !d->Notified || d->Vector != 0 || limit != 3 || d->UsedReads != 0)
            return Bad(d, true);
        if (d->WaitFailure)
            return 0;
        d->WaitObserved = true;
        if (!d->NoCompletion)
        {
            *(uint*)(d->Data + Used + 4) = d->CompletionId;
            *(uint*)(d->Data + Used + 8) = d->CompletionLength;
            *(ushort*)(d->Data + Used + 2) = d->CompletionIndex;
        }

        return 1;
    }

    [UnmanagedCallersOnly]
    private static byte MaskInterrupt(void* context)
    {
        Device* d = (Device*)context;
        if (!d->InterruptMode || d->ResetRequested)
            return Bad(d, true);
        d->MaskAttempted = true;
        return d->MaskFailure ? (byte)0 : (byte)1;
    }

    private static void Clean(Fixture f) => HostCheck.True(!f.State->InvalidAccess && !f.State->InvalidOrder);
    private static void NoDma(Fixture f)
    {
        HostCheck.Equal(0UL, f.Pool->AllocatedMask);
        HostCheck.Equal(0U, f.State->Notifications);
        HostCheck.True(new ReadOnlySpan<byte>(f.Data, 4096).IndexOfAnyExcept((byte)0xa5) < 0);
        Clean(f);
    }

    private static void Retained(Fixture f, VirtqueuePollingResult r)
    {
        HostCheck.True(!r.Succeeded && !r.DmaReleased);
        HostCheck.Equal(1UL, f.Pool->AllocatedMask);
        HostCheck.Equal(DmaBufferStatus.Exhausted, PageAlignedDmaBuffers.TryAllocate(f.Pool, 1, out _));
    }
}
