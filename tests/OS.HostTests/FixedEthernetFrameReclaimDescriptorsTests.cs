using OS.Drivers;

internal static unsafe class FixedEthernetFrameReclaimDescriptorsTests
{
    [HostTest("virtio.net.fixed frame publishes exact wire bytes after RX priming and reclaims both queues", HostLane.Fast, 0)]
    private static void Frame()
    {
        foreach (uint multiplier in new uint[] { 0, 2, 4 })
        {
            using var f = new QueueTests.Fixture();
            var r = f.Run(true, multiplier);
            HostCheck.True(r.Succeeded && r.Receive.Published && r.Transmit.Published && r.TransmitCompleted);
            HostCheck.True(QueueTests.Layout(f.State) && QueueTests.Wire(f.State) && f.State->WireObserved);
            HostCheck.Equal((ushort)1, r.Receive.AvailableIndex);
            HostCheck.Equal((ushort)1, r.Transmit.AvailableIndex);
            HostCheck.Equal((ushort)1, r.UsedIndex);
            HostCheck.Equal(0U, r.UsedId);
            HostCheck.Equal(0U, r.UsedLength);
            HostCheck.Equal(2U, f.State->Notifications);
            HostCheck.Equal(1U, f.State->DriverOkWrites);
            HostCheck.Equal(1U, f.State->EnableCalls);
            HostCheck.Equal(1U, r.Polls);
            HostCheck.Equal(7U, r.Fences);
            HostCheck.True(f.State->RxPrimed && f.State->ReceiveNotified && f.State->TransmitNotified &&
                f.State->HeldDuringReset && f.State->CleanupFenceObserved && r.Receive.DescriptorReleased && r.Transmit.DescriptorReleased);
            HostCheck.Equal(0UL, f.Pool->AllocatedMask);
            QueueTests.Clean(f);
        }
    }

    [HostTest("virtio.net.fixed frame rejects malformed used entries timeout and changed device status", HostLane.Fast, 1)]
    private static void Completions()
    {
        for (int invalid = 0; invalid < 7; invalid++)
        {
            using var f = new QueueTests.Fixture();
            switch (invalid)
            {
                case 0:
                    f.State->CompletionId = 1;
                    break;
                case 1:
                    f.State->CompletionLength = 72;
                    break;
                case 2:
                    f.State->CompletionIndex = 2;
                    break;
                case 3:
                    f.State->CompletionIndex = ushort.MaxValue;
                    break;
                case 4:
                    f.State->NoCompletion = true;
                    break;
                case 5:
                    f.State->BadOnNotify = true;
                    break;
                case 6:
                    f.State->BadAfterUsed = true;
                    break;
            }
            var r = f.Run(true);
            HostCheck.Equal(invalid == 4 ? VirtioNetQueueStatus.Timeout : invalid >= 5
                ? VirtioNetQueueStatus.StatusRejected : VirtioNetQueueStatus.InvalidCompletion, r.Status);
            HostCheck.True(!r.Succeeded && !r.TransmitCompleted && r.ResetComplete && r.BusMasterDisabled && r.DmaReleased);
            HostCheck.Equal(invalid == 4 ? 3U : 1U, r.Polls);
            QueueTests.Clean(f);
        }
    }

    [HostTest("virtio.net.fixed frame stops on each callback failure and guards completion fences", HostLane.Fast, 2)]
    private static void Callbacks()
    {
        foreach (uint limit in new uint[] { 0, 1000001 })
        {
            using var f = new QueueTests.Fixture();
            HostCheck.Equal(VirtioNetQueueStatus.InvalidAccess,
                FixedEthernetFrameReclaimDescriptors.Run(f.Common, f.Notify, 4, f.Pool, f.Access, limit, 3).Status);
            HostCheck.Equal(0U, f.State->Calls);
            HostCheck.Equal(0UL, f.Pool->AllocatedMask);
        }
        uint calls;
        using (var baseline = new QueueTests.Fixture())
        {
            HostCheck.True(baseline.Run(true).Succeeded);
            calls = baseline.State->Calls;
        }
        for (uint fail = 1; fail <= calls; fail++)
        {
            using var f = new QueueTests.Fixture();
            f.State->FailCall = fail;
            var r = f.Run(true);
            HostCheck.True(f.State->FailedCallback && !r.Succeeded);
            if (r.DmaReleased)
                HostCheck.True(r.ResetComplete && r.BusMasterDisabled && f.State->CleanupFenceObserved);
            if (f.Pool->AllocatedMask != 0)
                QueueTests.Retained(f, r);
            QueueTests.Clean(f);
        }
        foreach (uint fail in new uint[] { 4, 5, 6, 7 })
        {
            using var f = new QueueTests.Fixture();
            f.State->FailFence = fail;
            var r = f.Run(true);
            HostCheck.Equal(VirtioNetQueueStatus.OrderingFailed, fail == 7 ? r.CleanupStatus : r.Status);
            HostCheck.True(!r.Succeeded && f.State->FailedCallback);
            if (fail == 7)
                QueueTests.Retained(f, r);
            else
                HostCheck.True(r.DmaReleased);
            QueueTests.Clean(f);
        }
    }

    [HostTest("virtio.net.fixed frame retains device DMA after failed reset or bus master disable", HostLane.Fast, 3)]
    private static void Cleanup()
    {
        using (var f = new QueueTests.Fixture())
        {
            f.State->ResetStuck = true;
            var r = f.Run(true);
            HostCheck.Equal(VirtioNetQueueStatus.ResetFailed, r.CleanupStatus);
            HostCheck.Equal(3U, r.ResetReads);
            HostCheck.True(!r.ResetComplete && r.BusMasterDisabled);
            HostCheck.True(f.State->HeldDuringReset);
            QueueTests.Retained(f, r);
            QueueTests.Clean(f);
        }
        using (var f = new QueueTests.Fixture())
        {
            f.State->DisableFailure = true;
            var r = f.Run(true);
            HostCheck.Equal(VirtioNetQueueStatus.BusMasterFailed, r.CleanupStatus);
            HostCheck.True(r.ResetComplete && !r.BusMasterDisabled && f.State->HeldDuringReset);
            QueueTests.Retained(f, r);
            QueueTests.Clean(f);
        }
        using (var f = new QueueTests.Fixture())
        {
            f.State->EnableFailure = true;
            var r = f.Run(true);
            HostCheck.Equal(VirtioNetQueueStatus.BusMasterFailed, r.Status);
            HostCheck.True(r.ResetComplete && r.BusMasterDisabled && r.DmaReleased && !r.Transmit.Published);
            QueueTests.Clean(f);
        }
    }
}
