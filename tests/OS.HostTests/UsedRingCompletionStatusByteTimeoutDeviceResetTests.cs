using OS.Drivers;
using OS.Storage;

internal static unsafe class UsedRingCompletionStatusByteTimeoutDeviceResetTests
{
    [HostTest("virtio.blk.read explicit sector count requires exact data plus status length and preserves legacy validation")]
    private static void BoundedReadCompletion()
    {
        for (uint sectors = 1; sectors <= 4; sectors++)
        {
            uint bytes = sectors * 512;
            HostCheck.Equal(VirtioBlkReadStatus.Success, UsedRingCompletionStatusByteTimeoutDeviceReset.Validate(1, 0, bytes + 1, 0, sectors));
            foreach (uint length in new uint[] { 0, 1, bytes, bytes + 2, uint.MaxValue })
                HostCheck.Equal(VirtioBlkReadStatus.InvalidCompletion, UsedRingCompletionStatusByteTimeoutDeviceReset.Validate(1, 0, length, 0, sectors));
            HostCheck.Equal(VirtioBlkReadStatus.DeviceIoError, UsedRingCompletionStatusByteTimeoutDeviceReset.Validate(1, 0, 1, 1, sectors));
            HostCheck.Equal(VirtioBlkReadStatus.UnsupportedRequest, UsedRingCompletionStatusByteTimeoutDeviceReset.Validate(1, 0, 1, 2, sectors));
            HostCheck.Equal(VirtioBlkReadStatus.InvalidCompletion, UsedRingCompletionStatusByteTimeoutDeviceReset.Validate(2, 0, bytes + 1, 0, sectors));
            HostCheck.Equal(VirtioBlkReadStatus.InvalidCompletion, UsedRingCompletionStatusByteTimeoutDeviceReset.Validate(1, 1, bytes + 1, 0, sectors));
            HostCheck.Equal(VirtioBlkReadStatus.InvalidCompletion, UsedRingCompletionStatusByteTimeoutDeviceReset.Validate(1, 0, bytes + 2, 1, sectors));
        }
        foreach (uint sectors in new uint[] { 0, 5, uint.MaxValue })
            HostCheck.Equal(VirtioBlkReadStatus.InvalidCompletion, UsedRingCompletionStatusByteTimeoutDeviceReset.Validate(1, 0, 513, 0, sectors));
        HostCheck.Equal(VirtioBlkReadStatus.InvalidCompletion, UsedRingCompletionStatusByteTimeoutDeviceReset.Validate(1, 0, 2049, 0));
        HostCheck.Equal(VirtioBlkReadStatus.Success, UsedRingCompletionStatusByteTimeoutDeviceReset.Validate(1, 0, 513, 0));
    }

    [HostTest("virtio.blk.read validates exact used head length and status without accepting stale completions")]
    private static void Completion()
    {
        HostCheck.Equal(VirtioBlkReadStatus.Success, UsedRingCompletionStatusByteTimeoutDeviceReset.Validate(1, 0, 513, 0));
        foreach (ushort index in new ushort[] { 0, 2, 8, 65535 })
            HostCheck.Equal(VirtioBlkReadStatus.InvalidCompletion, UsedRingCompletionStatusByteTimeoutDeviceReset.Validate(index, 0, 513, 0));
        foreach (uint id in new uint[] { 1, 2, 8, uint.MaxValue })
            HostCheck.Equal(VirtioBlkReadStatus.InvalidCompletion, UsedRingCompletionStatusByteTimeoutDeviceReset.Validate(1, id, 513, 0));
        foreach (uint length in new uint[] { 0, 1, 512, 514, uint.MaxValue })
            HostCheck.Equal(VirtioBlkReadStatus.InvalidCompletion, UsedRingCompletionStatusByteTimeoutDeviceReset.Validate(1, 0, length, 0));
        for (int scenario = 0; scenario < 8; scenario++)
        {
            using var f = new BlockReadFixture();
            if (scenario == 0)
                f.State->UsedIndex = 2;
            if (scenario == 1)
                f.State->UsedId = 1;
            if (scenario == 2)
                f.State->UsedLength = 512;
            if (scenario == 3)
                f.State->UsedLength = 514;
            if (scenario == 4)
            {
                f.State->UsedLength = 1;
                f.State->CompletionStatus = 1;
            }
            if (scenario == 5)
                f.State->CompletionStatus = 2;
            if (scenario == 6)
                f.State->CompletionStatus = 255;
            if (scenario == 7)
                f.State->ChangeAfterCompletion = true;
            var r = f.Run();
            HostCheck.Equal(scenario == 4 ? VirtioBlkReadStatus.DeviceIoError : scenario == 5 ? VirtioBlkReadStatus.UnsupportedRequest :
                scenario == 7 ? VirtioBlkReadStatus.ConfigurationChanged : VirtioBlkReadStatus.InvalidCompletion, r.Status);
            HostCheck.True(!r.Succeeded && r.Queue.ResetComplete && r.Queue.DmaReleased && f.Pool->AllocatedMask == 0);
            f.Unchanged();
            f.Check();
        }
    }

    [HostTest("virtio.blk.read bounds polling and releases only after observed reset disable and fence")]
    private static void TimeoutAndCleanup()
    {
        using (var f = new BlockReadFixture())
        {
            f.State->Delay = 3;
            var r = f.Run();
            HostCheck.True(r.Succeeded && r.Polls == 4);
            f.Check();
        }
        using (var f = new BlockReadFixture())
        {
            f.State->NeverComplete = true;
            var r = f.Run();
            HostCheck.True(r.Status == VirtioBlkReadStatus.Timeout && r.Polls == 4 && r.Queue.DmaReleased);
            f.Unchanged();
            f.Check();
        }
        for (int failed = 0; failed < 3; failed++)
        {
            using var f = new BlockReadFixture();
            if (failed == 0)
                f.State->ResetStuck = true;
            if (failed == 1)
                f.State->FailBusOff = 3;
            if (failed == 2)
                f.State->FailFence = 6;
            var r = f.Run();
            HostCheck.True(!r.Succeeded && !r.Queue.DmaReleased && f.Pool->AllocatedMask == 1);
            HostCheck.Equal(failed == 0 ? VirtioBlkQueueStatus.ResetFailed : failed == 1 ? VirtioBlkQueueStatus.BusMasterFailed :
                VirtioBlkQueueStatus.OrderingFailed, r.Queue.CleanupStatus);
            HostCheck.Equal(DmaBufferStatus.Exhausted, PageAlignedDmaBuffers.TryAllocate(f.Pool, 1, out _));
            if (failed == 0)
                HostCheck.Equal(3U, r.Queue.ResetReads);
            f.Unchanged();
            f.Check();
        }
    }

    [HostTest("virtio.blk.read handles every callback failure including writes performed before failure")]
    private static void CallbackFailures()
    {
        uint count;
        using (var f = new BlockReadFixture())
        {
            HostCheck.True(f.Run().Succeeded);
            count = f.State->Calls;
        }
        foreach (bool after in new[] { false, true })
            for (uint call = 1; call <= count; call++)
            {
                using var f = new BlockReadFixture();
                f.State->FailCall = call;
                f.State->FailAfterWrite = after;
                var r = f.Run();
                HostCheck.True(!r.Succeeded && f.State->FailedCallback, $"call={call} after={after}");
                if (r.Queue.DmaReleased)
                    HostCheck.True(r.Queue.ResetComplete && r.Queue.BusMasterDisabled && f.State->CleanupFence);
                f.Unchanged();
                f.Check();
            }
    }
}
