using System.Buffers.Binary;
using System.Runtime.InteropServices;
using OS.Drivers;
using OS.Kernel.Platform;
using OS.Storage;

internal static unsafe class SubmitPollSectorRequestTests
{
    [HostTest("virtio.blk.read submits an independently checked three-descriptor sector chain")]
    private static void Success()
    {
        HostCheck.True(!default(VirtioBlkReadResult).Succeeded);
        using var f = new BlockReadFixture();
        var r = f.Run();
        HostCheck.True(r.Succeeded && r.Published && r.Notified && r.BytesRead == 512);
        HostCheck.Equal((ushort)1, *(ushort*)(f.State->Data + 128));
        HostCheck.True(r.Sector == BlockReadFixture.Sector && r.Capacity == BlockReadFixture.Capacity && r.CapacityReads == 1);
        HostCheck.True(r.NotifyOffset == 3 && r.NotifyAddress == BlockReadFixture.Notify + 12 && r.Generation == 7);
        HostCheck.True(r.UsedIndex == 1 && r.UsedId == 0 && r.UsedLength == 513 && r.DeviceStatus == 0 && r.Polls == 1);
        HostCheck.True(r.Queue.Succeeded && r.Queue.Fences == 2 && r.Fences == 4 && r.Queue.ResetReads == 1);
        HostCheck.True(f.State->ChainChecked && f.State->Acquired && f.State->ResetObserved && f.State->CleanupBusOff && f.State->CleanupFence && !f.State->BusEnabled && f.Pool->AllocatedMask == 0);
        for (int i = 0; i < 512; i++)
            HostCheck.Equal(BlockReadFixture.Payload(i), f.Output[i]);
        f.Check();
    }

    [HostTest("virtio.blk.read rejects capacity range generation and notify errors before publication")]
    private static void Admission()
    {
        foreach (ulong capacity in new ulong[]
        {
            0,
            ulong.MaxValue / 512 + 1,
            BlockReadFixture.Sector
        }

        )
        {
            using var f = new BlockReadFixture();
            f.State->Capacity = capacity;
            var r = f.Run();
            HostCheck.Equal(capacity == BlockReadFixture.Sector ? VirtioBlkReadStatus.SectorOutOfRange : VirtioBlkReadStatus.InvalidCapacity, r.Status);
            HostCheck.True(!r.Published && f.Pool->AllocatedMask == 0 && r.Queue.ResetComplete);
            f.Unchanged();
            f.Check();
        }

        using (var f = new BlockReadFixture())
        {
            f.State->GenerationChurn = true;
            var r = f.Run();
            HostCheck.True(r.Status == VirtioBlkReadStatus.ConfigurationChanged && r.CapacityReads == 16 && !r.Published);
            f.Unchanged();
            f.Check();
        }

        using (var f = new BlockReadFixture())
        {
            f.State->ChurnOnce = true;
            var r = f.Run();
            HostCheck.True(r.Succeeded && r.CapacityReads == 2);
            f.Check();
        }

        using (var f = new BlockReadFixture())
        {
            f.State->ChangeBeforePublish = true;
            var r = f.Run();
            HostCheck.True(r.Status == VirtioBlkReadStatus.ConfigurationChanged && !r.Published && r.Queue.DmaReleased);
            f.Unchanged();
            f.Check();
        }

        using (var f = new BlockReadFixture())
        {
            f.State->NotifyOffset = ushort.MaxValue;
            var r = f.Run();
            HostCheck.True(r.Status == VirtioBlkReadStatus.InvalidNotify && !r.Published && r.Queue.DmaReleased);
            f.Unchanged();
            f.Check();
        }

        using (var f = new BlockReadFixture())
        {
            f.Multiplier = 0;
            var r = f.Run();
            HostCheck.True(r.Succeeded && r.NotifyAddress == BlockReadFixture.Notify);
            f.Check();
        }
    }

    [HostTest("virtio.blk.read rejects invalid callbacks limits and destinations without effects")]
    private static void InvalidInputs()
    {
        for (int missing = 0; missing < 9; missing++)
        {
            using var f = new BlockReadFixture();
            var a = f.Access;
            switch (missing)
            {
                case 0:
                    a.Queue.Read8 = null;
                    break;
                case 1:
                    a.Queue.Write8 = null;
                    break;
                case 2:
                    a.Queue.Read16 = null;
                    break;
                case 3:
                    a.Queue.Write16 = null;
                    break;
                case 4:
                    a.Queue.Read32 = null;
                    break;
                case 5:
                    a.Queue.Write32 = null;
                    break;
                case 6:
                    a.Queue.Fence = null;
                    break;
                case 7:
                    a.Queue.BusMasterOff = null;
                    break;
                case 8:
                    a.BusMasterOn = null;
                    break;
            }

            var r = f.Run(a);
            HostCheck.True(r.Status == VirtioBlkReadStatus.InvalidAccess && f.State->Calls == 0);
            f.Unchanged();
            f.Check();
        }

        for (int input = 0; input < 10; input++)
        {
            using var f = new BlockReadFixture();
            if (input == 0)
                f.PollLimit = 0;
            if (input == 1)
                f.PollLimit = 1000001;
            if (input == 2)
                f.ResetLimit = 0;
            if (input == 3)
                f.ResetLimit = 1025;
            if (input == 4)
                f.Multiplier = 3;
            byte* output = input == 5 ? null : input == 6 ? (byte*)1 : input == 7 ? (byte*)BlockReadFixture.Common : input == 8 ? (byte*)0x0000800000000000UL : f.Output;
            var r = f.Run(f.Access, output, input == 9 ? 511U : 512U);
            HostCheck.True(!r.Succeeded && f.State->Calls == 0);
            f.Unchanged();
            f.Check();
        }
    }

    [HostTest("virtio.blk.read interrupt and masked recovery preserve completion validation and cleanup")]
    private static void InterruptRoutes()
    {
        foreach (var route in new[]
        {
            BlockCompletionRoute.Interrupt,
            BlockCompletionRoute.PollingRecovery
        }

        )
        {
            using var f = new BlockReadFixture();
            f.State->InterruptRoute = (byte)route;
            f.State->Delay = route == BlockCompletionRoute.Interrupt ? 0U : 2U;
            var r = f.Run(f.InterruptAccess);
            HostCheck.True(r.Succeeded && r.InterruptAttempted && r.InterruptPrepared && r.InterruptStopped && r.CompletionRoute == route && f.State->InterruptWaited && f.State->InterruptStopped && r.Polls == f.State->Delay + 1 && f.Pool->AllocatedMask == 0);
            HostCheck.Equal((ushort)0, *(ushort*)(f.State->Data + 128));
            f.Check();
        }

        using (var f = new BlockReadFixture())
        {
            f.State->InterruptRoute = (byte)BlockCompletionRoute.Interrupt;
            f.State->Delay = 1;
            var r = f.Run(f.InterruptAccess);
            HostCheck.True(r.Status == VirtioBlkReadStatus.Timeout && r.Polls == 1 && r.InterruptStopped && r.Queue.DmaReleased);
            f.Unchanged();
            f.Check();
        }
    }

    [HostTest("virtio.blk.read partial interrupt callback sets reject before hardware effects")]
    private static void InterruptAdmission()
    {
        for (int present = 1; present < 7; present++)
        {
            using var f = new BlockReadFixture();
            var a = f.InterruptAccess;
            if ((present & 1) == 0)
                a.PrepareInterrupt = null;
            if ((present & 2) == 0)
                a.WaitInterrupt = null;
            if ((present & 4) == 0)
                a.StopInterrupt = null;
            var r = f.Run(a);
            HostCheck.True(r.Status == VirtioBlkReadStatus.InvalidAccess && f.State->Calls == 0 && !r.InterruptAttempted);
            f.Unchanged();
            f.Check();
        }
    }

    [HostTest("virtio.blk.read failed prepare or invalid wait shuts down before cleanup and never copies")]
    private static void InterruptFailures()
    {
        for (int fault = 0; fault < 4; fault++)
        {
            using var f = new BlockReadFixture();
            f.State->FailInterruptPrepare = fault == 0;
            f.State->InterruptRoute = fault == 1 ? (byte)0 : fault == 2 ? (byte)BlockCompletionRoute.InvalidWait : (byte)255;
            var r = f.Run(f.InterruptAccess);
            HostCheck.Equal(fault == 0 ? VirtioBlkReadStatus.InterruptPrepareFailed : VirtioBlkReadStatus.InterruptWaitFailed, r.Status);
            HostCheck.True(r.InterruptAttempted && r.InterruptStopped && f.State->InterruptStopped && r.Queue.ResetComplete && r.Queue.BusMasterDisabled && f.Pool->AllocatedMask == 0 && (fault != 0 || !r.Published));
            f.Unchanged();
            f.Check();
        }
    }

    [HostTest("virtio.blk.read uncertain interrupt shutdown retains device DMA and suppresses payload")]
    private static void InterruptShutdown()
    {
        using var f = new BlockReadFixture();
        f.State->InterruptRoute = (byte)BlockCompletionRoute.Interrupt;
        f.State->FailInterruptStop = true;
        var r = f.Run(f.InterruptAccess);
        HostCheck.True(r.Status == VirtioBlkReadStatus.InterruptShutdownFailed && r.BytesRead == 0 && !r.InterruptStopped && !r.Queue.DmaReleased && r.Queue.ResetComplete && r.Queue.BusMasterDisabled && f.Pool->AllocatedMask == 1);
        HostCheck.Equal(DmaBufferStatus.Exhausted, PageAlignedDmaBuffers.TryAllocate(f.Pool, 1, out _));
        f.Unchanged();
        f.Check();
    }

    [HostTest("virtio.blk.read protects DMA and output from MMIO and metadata overlap")]
    private static void Aliases()
    {
        using (var f = new BlockReadFixture())
        {
            HostCheck.Equal(DmaBufferStatus.Success, PageAlignedDmaBuffers.TryAllocate(f.Pool, 1, out var held));
            var r = f.Run();
            HostCheck.True(r.Status == VirtioBlkReadStatus.QueueRejected && !r.Queue.DmaReleased && f.Pool->AllocatedMask == 1);
            HostCheck.Equal(DmaBufferStatus.Success, PageAlignedDmaBuffers.TryRelease(f.Pool, held.Token));
            HostCheck.Equal(DmaBufferStatus.StaleToken, PageAlignedDmaBuffers.TryRelease(f.Pool, held.Token));
            f.Unchanged();
            f.Check();
        }

        foreach (ulong physical in new ulong[]
        {
            BlockReadFixture.Bar,
            BlockReadFixture.Bar + 0x2000,
            BlockReadFixture.Bar + 0x3000
        }

        )
        {
            using var f = new BlockReadFixture(physical);
            var r = f.Run();
            HostCheck.True(r.Status == VirtioBlkReadStatus.QueueRejected && r.Queue.Status == VirtioBlkQueueStatus.Overlap);
            HostCheck.True(r.Queue.DmaReleased && new ReadOnlySpan<byte>(f.Data, 4096).IndexOfAnyExcept((byte)0xa5) < 0);
            f.Unchanged();
            f.Check();
        }

        using (var f = new BlockReadFixture())
        {
            var r = f.Run(f.Access, f.Data, 512);
            HostCheck.True(r.Status == VirtioBlkReadStatus.QueueRejected && r.Queue.Status == VirtioBlkQueueStatus.Overlap && r.Queue.DmaReleased);
            HostCheck.True(new ReadOnlySpan<byte>(f.Data, 4096).IndexOfAnyExcept((byte)0xa5) < 0);
            f.Check();
        }

        using (var f = new BlockReadFixture())
        {
            byte[] before = new ReadOnlySpan<byte>(f.Pool, sizeof(DmaBufferPool)).ToArray();
            var r = f.Run(f.Access, (byte*)f.Pool, 512);
            HostCheck.True(r.Status == VirtioBlkReadStatus.InvalidDestination && f.State->Calls == 0 && before.AsSpan().SequenceEqual(new ReadOnlySpan<byte>(f.Pool, sizeof(DmaBufferPool))));
            f.Check();
        }

        using (var f = new BlockReadFixture())
        {
            byte[] before = new ReadOnlySpan<byte>(f.Slot, sizeof(DmaBufferSlot)).ToArray();
            var r = f.Run(f.Access, (byte*)f.Slot, 512);
            HostCheck.True(r.Status == VirtioBlkReadStatus.InvalidDestination && f.State->Calls == 0 && before.AsSpan().SequenceEqual(new ReadOnlySpan<byte>(f.Slot, sizeof(DmaBufferSlot))));
            f.Check();
        }
    }
}
