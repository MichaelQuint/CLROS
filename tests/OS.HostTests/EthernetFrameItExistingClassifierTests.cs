using OS.Drivers;

internal static unsafe class EthernetFrameItExistingClassifierTests
{
    // Independent wire vectors, including modern num_buffers=1.
    internal static byte[] Reply()
    {
        byte[] packet = new byte[1526];
        ReadOnlySpan<byte> frame = [0,0,0,0,0,0,0,0,0,0,1,0,
            2,0,0,0,0,1,0x52,0x55,10,0,2,2,8,6,
            0,1,8,0,6,4,0,2,0x52,0x55,10,0,2,2,10,0,2,2,2,0,0,0,0,1,10,0,2,15];
        frame.CopyTo(packet); return packet;
    }
    internal static ReadOnlySpan<byte> Request => [255,255,255,255,255,255,2,0,0,0,0,1,8,6,
        0,1,8,0,6,4,0,1,2,0,0,0,0,1,10,0,2,15,0,0,0,0,0,0,10,0,2,2,
        0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0];

    internal static EthernetFrameReceiveResult Run(QueueTests.Fixture f, byte[] reply, byte[] output)
    {
        f.State->Sending = true; f.State->Receiving = true;
        fixed (byte* source = reply) fixed (byte* destination = output)
        {
            f.State->ReceiveSource = source; f.State->OutputSentinel = destination;
            return EthernetFrameItExistingClassifier.Run(f.Common, f.Notify, 4, f.Pool, f.Access, 3, 3, Request, output);
        }
    }
    internal static void Configure(QueueTests.Fixture f)
    { f.State->ReceiveCompletionIndex = 1; f.State->ReceiveCompletionLength = 54; }
    internal static byte[] Output() { byte[] output = new byte[1526]; Array.Fill(output, (byte)0xcc); return output; }

    [HostTest("virtio.net.RX validates used entry quiesces then copies and classifies modern ARP", HostLane.Fast, 0)]
    private static void Receive()
    {
        using var f = new QueueTests.Fixture(); Configure(f); byte[] reply = Reply(), output = Output();
        var r = Run(f, reply, output);
        HostCheck.True(r.Succeeded && r.Queue.TransmitCompleted && r.Queue.ResetComplete &&
            r.Queue.BusMasterDisabled && r.Queue.DmaReleased && !f.State->OutputChangedBeforeQuiescence);
        HostCheck.Equal(54U, r.CapturedBytes); HostCheck.Equal((ushort)1, r.ReceiveUsedIndex);
        HostCheck.Equal(0U, r.ReceiveUsedId); HostCheck.Equal(OS.Net.EthernetPayloadKind.Arp, r.Classification.PayloadKind);
        HostCheck.True(output.AsSpan(0, 54).SequenceEqual(reply.AsSpan(0, 54)) &&
            output.AsSpan(54).IndexOfAnyExcept((byte)0xcc) < 0);
        HostCheck.Equal(0UL, f.Pool->AllocatedMask); QueueTests.Clean(f);
        HostCheck.True(!default(EthernetFrameReceiveResult).Succeeded);
    }

    [HostTest("virtio.net.RX rejects malformed completion loss status and uncertain quiescence", HostLane.Fast, 1)]
    private static void Failures()
    {
        for (int bad = 0; bad < 11; bad++)
        {
            using var f = new QueueTests.Fixture(); Configure(f); byte[] output = Output();
            switch (bad)
            {
                case 0: f.State->ReceiveCompletionId = 1; break;
                case 1: f.State->ReceiveCompletionIndex = 2; break;
                case 2: f.State->ReceiveCompletionIndex = ushort.MaxValue; break;
                case 3: f.State->ReceiveCompletionLength = 25; break;
                case 4: f.State->ReceiveCompletionLength = 1527; break;
                case 5: f.State->NoReceiveCompletion = true; break;
                case 6: f.State->NoCompletion = true; break;
                case 7: f.State->BadOnNotify = true; break;
                case 8: f.State->BadAfterUsed = true; break;
                case 9: f.State->ResetStuck = true; break;
                case 10: f.State->DisableFailure = true; break;
            }
            var r = Run(f, Reply(), output);
            HostCheck.True(!r.Succeeded && r.CapturedBytes == 0 && !r.Classification.Succeeded &&
                output.AsSpan().IndexOfAnyExcept((byte)0xcc) < 0 && !f.State->OutputChangedBeforeQuiescence);
            if (bad < 5) HostCheck.Equal(VirtioNetQueueStatus.InvalidCompletion, r.Queue.Status);
            if (bad is 5 or 6) HostCheck.Equal(VirtioNetQueueStatus.Timeout, r.Queue.Status);
            if (bad is 7 or 8) HostCheck.Equal(VirtioNetQueueStatus.StatusRejected, r.Queue.Status);
            if (bad >= 9) QueueTests.Retained(f, r.Queue); else HostCheck.True(r.Queue.DmaReleased);
            QueueTests.Clean(f);
        }
    }

    [HostTest("virtio.net.RX callback failures never copy before proven quiescence", HostLane.Fast, 2)]
    private static void Callbacks()
    {
        uint calls;
        using (var f = new QueueTests.Fixture())
        { Configure(f); HostCheck.True(Run(f, Reply(), Output()).Succeeded); calls = f.State->Calls; }
        for (uint fail = 1; fail <= calls; fail++)
        {
            using var f = new QueueTests.Fixture(); Configure(f); f.State->FailCall = fail;
            var r = Run(f, Reply(), Output());
            HostCheck.True(!r.Succeeded && f.State->FailedCallback && !f.State->OutputChangedBeforeQuiescence);
            if (r.CapturedBytes != 0) HostCheck.True(r.Queue.ResetComplete && r.Queue.BusMasterDisabled && f.State->CleanupFenceObserved);
            if (f.Pool->AllocatedMask != 0) QueueTests.Retained(f, r.Queue);
            QueueTests.Clean(f);
        }
    }

    [HostTest("virtio.net.RX admits disjoint mapped caller ranges before any DMA side effects", HostLane.Fast, 3)]
    private static void CallerRanges()
    {
        for (int bad = 0; bad < 9; bad++)
        {
            using var f = new QueueTests.Fixture(); byte[] output = Output();
            ReadOnlySpan<byte> input = Request; Span<byte> destination = output;
            switch (bad)
            {
                case 0: input = new ReadOnlySpan<byte>(f.Data + 2304,60); break;
                case 1: destination = new Span<byte>(f.Data,1526); break;
                case 2: input = new ReadOnlySpan<byte>(null,60); break;
                case 3: destination = new Span<byte>(null,1526); break;
                case 4: input = new ReadOnlySpan<byte>((void*)(~0UL - 8),60); break;
                case 5: destination = new Span<byte>((void*)(~0UL - 8),1526); break;
                case 6: input = new ReadOnlySpan<byte>((void*)QueueTests.CommonAddress,60); break;
                case 7: destination = new Span<byte>((void*)QueueTests.NotifyAddress,1526); break;
                case 8: input = output.AsSpan(0,60); break;
            }
            var r = EthernetFrameItExistingClassifier.Run(f.Common,f.Notify,4,f.Pool,f.Access,3,3,input,destination);
            HostCheck.Equal(bad is >= 2 and <= 5 ? VirtioNetQueueStatus.InvalidAccess : VirtioNetQueueStatus.Overlap,r.Queue.Status);
            HostCheck.True(!r.Succeeded && r.CapturedBytes == 0 && f.State->Calls == 0 && f.Pool->AllocatedMask == 0 &&
                output.AsSpan().IndexOfAnyExcept((byte)0xcc) < 0 && new ReadOnlySpan<byte>(f.Data,4096).IndexOfAnyExcept((byte)0xa5) < 0);
        }
    }

    [HostTest("virtio.net.RX keeps caller output unpublished when final DMA reclaim is rejected", HostLane.Fast, 4)]
    private static void FinalReclaim()
    {
        using var f = new QueueTests.Fixture(); Configure(f); f.State->InvalidateDmaOnQuiescence = true;
        byte[] output = Output(); var r = Run(f,Reply(),output);
        HostCheck.True(!r.Succeeded && r.Queue.ResetComplete && r.Queue.BusMasterDisabled &&
            !r.Queue.DmaReleased && r.CapturedBytes == 0 && !r.Classification.Succeeded &&
            output.AsSpan().IndexOfAnyExcept((byte)0xcc) < 0);
        HostCheck.Equal(VirtioNetQueueStatus.DmaRejected,r.Queue.CleanupStatus); QueueTests.Clean(f);
    }
}
