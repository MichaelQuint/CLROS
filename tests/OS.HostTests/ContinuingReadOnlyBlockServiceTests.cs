using System.Runtime.InteropServices;
using OS.Drivers;
using OS.Kernel.Platform;
using OS.Storage;
using Block = OS.Storage.ContinuingReadOnlyBlockService;

internal static unsafe class ContinuingReadOnlyBlockServiceTests
{
    [HostTest("block.service retained independent reads cross actual ushort ring wrap", HostLane.Fast, 0)]
    private static bool RetainedAndWrap()
    {
        using var f = new Fixture(); HostCheck.Equal(Block.Status.Success, f.Start());
        Span<byte> output = stackalloc byte[520]; bool good = true;
        for (uint i = 0; i < 65538; i++)
        {
            output.Fill(0xa5);
            good &= Submit(f.State, i, out var token) == Block.Status.Success;
            f.Complete();
            good &= Poll(f.State, out var completion) == Block.Status.Ready && completion.Sector == i && completion.Bytes == 512;
            good &= Block.Copy(f.State, token, output) == Block.Status.Success;
            for (int j = 0; j < 512; j++) good &= output[j] == Payload(i, j);
            good &= output[512..].IndexOfAnyExcept((byte)0xa5) < 0 && f.Pool->AllocatedMask == 1 && f.Owner == DmaBufferOwnership.Device;
        }
        HostCheck.True(good && !f.Hardware->BadOrder);
        HostCheck.Equal(65538UL, f.State->Submitted); HostCheck.Equal(65538UL, f.State->Completed); HostCheck.Equal(65538UL, f.State->Copied);
        HostCheck.Equal((ushort)2, f.State->PublishedIndex); HostCheck.Equal((ushort)2, f.State->ConsumedIndex);
        HostCheck.Equal(1U, f.Hardware->DriverOkWrites); HostCheck.Equal(0U, f.Hardware->Resets);
        HostCheck.Equal(Block.Status.Success, Block.Stop(f.State));
        HostCheck.True(f.State->Retired && f.Hardware->HeldAtResetFence && !f.Hardware->BusEnabled);
        HostCheck.Equal(0UL, f.Pool->AllocatedMask); HostCheck.True(new ReadOnlySpan<byte>(f.Data, 4096).IndexOfAnyExcept((byte)0) < 0);
        f.CheckGuards(); return true;
    }

    [HostTest("block.service busy token copy retry and copied owner guards", HostLane.Fast, 1)]
    private static bool TokensAndCopy()
    {
        using var f = new Fixture(); HostCheck.Equal(Block.Status.Success, f.Start());
        Span<byte> output = stackalloc byte[512]; output.Fill(0xa5);
        HostCheck.Equal(Block.Status.Success, Submit(f.State, 7, out var first));
        HostCheck.Equal(Block.Status.Busy, Submit(f.State, 8, out var absent)); HostCheck.True(!absent.IsInitialized);
        HostCheck.Equal(Block.Status.Pending, Block.Copy(f.State, first, output));
        HostCheck.Equal(Block.Status.Pending, Poll(f.State, out _));
        Block.State copy = *f.State; uint before = f.Hardware->Calls;
        HostCheck.Equal(Block.Status.InvalidState, Poll(&copy, out _));
        HostCheck.Equal(Block.Status.InvalidState, Block.Stop(&copy)); HostCheck.Equal(before, f.Hardware->Calls);
        f.Complete(); HostCheck.Equal(Block.Status.Ready, Poll(f.State, out _));
        HostCheck.Equal(Block.Status.Ready, Poll(f.State, out _));
        HostCheck.Equal(Block.Status.ShortBuffer, Block.Copy(f.State, first, output[..511]));
        before = f.Hardware->Calls;
        HostCheck.Equal(Block.Status.InvalidStorage, Block.Copy(f.State, first, new Span<byte>(f.State, 512)));
        HostCheck.Equal(Block.Status.InvalidStorage, Block.Copy(f.State, first, new Span<byte>(f.Pool, 512)));
        HostCheck.Equal(Block.Status.InvalidStorage, Block.Copy(f.State, first, new Span<byte>(f.Slot, 512)));
        HostCheck.Equal(Block.Status.InvalidStorage, Block.Copy(f.State, first, new Span<byte>(f.Data + 512, 512)));
        HostCheck.Equal(Block.Status.InvalidStorage, Block.Copy(f.State, first, new Span<byte>((void*)Common, 512)));
        HostCheck.Equal(Block.Status.StaleToken, Block.Copy(f.State, CorruptToken(first, "Epoch"), output));
        HostCheck.Equal(Block.Status.StaleToken, Block.Copy(f.State, CorruptToken(first, "Owner"), output));
        HostCheck.Equal(Block.Status.StaleToken, Block.Copy(f.State, CorruptToken(first, "Generation"), output));
        HostCheck.Equal(before, f.Hardware->Calls); HostCheck.True(output.IndexOfAnyExcept((byte)0xa5) < 0 && f.State->ReadReady);
        HostCheck.Equal(Block.Status.Success, Block.Copy(f.State, first, output)); CheckPayload(output, 7);
        HostCheck.Equal(Block.Status.StaleToken, Block.Copy(f.State, first, output));
        HostCheck.Equal(Block.Status.Success, Submit(f.State, 8, out var second));
        output.Fill(0xa5); HostCheck.Equal(Block.Status.StaleToken, Block.Copy(f.State, first, output));
        f.Complete(); HostCheck.Equal(Block.Status.Ready, Poll(f.State, out _));
        HostCheck.Equal(Block.Status.Success, Block.Copy(f.State, second, output)); CheckPayload(output, 8);
        HostCheck.Equal(Block.Status.Success, Block.Stop(f.State)); return true;
    }

    [HostTest("block.service cancellation drains outstanding DMA and revokes ready output", HostLane.Fast, 2)]
    private static bool Cancellation()
    {
        using var f = new Fixture(); HostCheck.Equal(Block.Status.Success, f.Start()); Span<byte> output = stackalloc byte[512]; output.Fill(0xa5);
        HostCheck.Equal(Block.Status.Success, Submit(f.State, 9, out var token));
        HostCheck.Equal(Block.Status.Success, Block.Cancel(f.State, token)); HostCheck.Equal(Block.Status.Success, Block.Cancel(f.State, token));
        HostCheck.Equal(Block.Status.Cancelled, Block.Copy(f.State, token, output));
        HostCheck.Equal(Block.Status.Busy, Submit(f.State, 10, out _)); HostCheck.Equal(Block.Status.Pending, Poll(f.State, out _));
        HostCheck.Equal(1UL, f.Pool->AllocatedMask); HostCheck.Equal(DmaBufferOwnership.Device, f.Owner);
        f.Complete(); HostCheck.Equal(Block.Status.Cancelled, Poll(f.State, out var done)); HostCheck.Equal(0U, done.Bytes);
        HostCheck.Equal(Block.Status.StaleToken, Block.Copy(f.State, token, output)); HostCheck.Equal(Block.Status.StaleToken, Block.Cancel(f.State, token));
        HostCheck.Equal(Block.Status.Success, Submit(f.State, 10, out token)); f.Complete(); HostCheck.Equal(Block.Status.Ready, Poll(f.State, out _));
        HostCheck.Equal(Block.Status.Success, Block.Cancel(f.State, token)); HostCheck.Equal(Block.Status.StaleToken, Block.Copy(f.State, token, output));
        HostCheck.Equal(2UL, f.State->Cancelled); HostCheck.Equal(0UL, f.State->Copied); HostCheck.True(output.IndexOfAnyExcept((byte)0xa5) < 0);
        HostCheck.Equal(Block.Status.Idle, Poll(f.State, out _)); HostCheck.Equal(Block.Status.Success, Block.Stop(f.State)); return true;
    }

    [HostTest("block.service short error stale and duplicate used entries never copy", HostLane.Fast, 3)]
    private static bool InvalidCompletions()
    {
        (uint Id, uint Length, byte DeviceStatus, ushort Delta, Block.Status Expected)[] cases =
        [ (1, 513, 0, 1, Block.Status.InvalidCompletion), (0, 0, 0, 1, Block.Status.InvalidCompletion),
          (0, 512, 0, 1, Block.Status.InvalidCompletion), (0, 514, 0, 1, Block.Status.InvalidCompletion),
          (0, 1, 1, 1, Block.Status.DeviceIoError), (0, 1, 2, 1, Block.Status.UnsupportedRequest),
          (0, 513, 255, 1, Block.Status.InvalidCompletion), (0, 513, 0, 2, Block.Status.InvalidCompletion),
          (0, 513, 0, ushort.MaxValue, Block.Status.InvalidCompletion) ];
        Span<byte> output = stackalloc byte[512];
        foreach (var item in cases)
        {
            using var f = new Fixture(); HostCheck.Equal(Block.Status.Success, f.Start()); output.Fill(0xa5);
            HostCheck.Equal(Block.Status.Success, Submit(f.State, 1, out var token)); f.Complete(item.Id, item.Length, item.DeviceStatus, item.Delta);
            HostCheck.Equal(item.Expected, Poll(f.State, out _)); HostCheck.Equal(item.Expected, f.State->LastFailure);
            HostCheck.Equal(Block.Status.InvalidState, Block.Copy(f.State, token, output));
            HostCheck.True(output.IndexOfAnyExcept((byte)0xa5) < 0 && f.State->RequiresCleanup); HostCheck.Equal(1UL, f.Pool->AllocatedMask);
            HostCheck.Equal(Block.Status.Success, Block.Stop(f.State));
        }
        for (int phase = 0; phase < 2; phase++)
        {
            using var f = new Fixture(); HostCheck.Equal(Block.Status.Success, f.Start());
            HostCheck.Equal(Block.Status.Success, Submit(f.State, 2, out var token)); f.Complete(); HostCheck.Equal(Block.Status.Ready, Poll(f.State, out _));
            if (phase == 1) HostCheck.Equal(Block.Status.Success, Block.Copy(f.State, token, output));
            output.Fill(0xa5); *(ushort*)(f.Data + 150) = 2;
            HostCheck.Equal(Block.Status.InvalidCompletion, phase == 0 ? Block.Copy(f.State, token, output) : Poll(f.State, out _));
            HostCheck.True(output.IndexOfAnyExcept((byte)0xa5) < 0); HostCheck.Equal(Block.Status.Success, Block.Stop(f.State));
        }
        return true;
    }

    [HostTest("block.service capacity and configuration generations fail closed", HostLane.Fast, 4)]
    private static bool Configuration()
    {
        using (var f = new Fixture())
        { f.Hardware->Capacity = 0; HostCheck.Equal(Block.Status.InvalidCapacity, f.Start()); HostCheck.True(f.State->Retired); }
        using (var f = new Fixture())
        { f.Hardware->Capacity++; HostCheck.Equal(Block.Status.ConfigurationChanged, f.Start()); HostCheck.Equal(0UL, f.Pool->AllocatedMask); }
        using (var f = new Fixture())
        {
            f.Hardware->GenerationChurn = true; HostCheck.Equal(Block.Status.ConfigurationChanged, f.Start());
            HostCheck.Equal(32U, f.Hardware->GenerationReads); HostCheck.True(f.State->Retired);
        }
        Span<byte> output = stackalloc byte[512];
        for (int phase = 0; phase < 3; phase++)
        {
            using var f = new Fixture(); HostCheck.Equal(Block.Status.Success, f.Start()); output.Fill(0xa5); Block.ReadToken token = default;
            if (phase != 0) { HostCheck.Equal(Block.Status.Success, Submit(f.State, 3, out token)); f.Complete(); }
            if (phase == 2) HostCheck.Equal(Block.Status.Ready, Poll(f.State, out _));
            f.Hardware->Generation++; f.Hardware->Capacity++;
            Block.Status result = phase == 0 ? Submit(f.State, 3, out _) : phase == 1 ? Poll(f.State, out _) : Block.Copy(f.State, token, output);
            HostCheck.Equal(Block.Status.ConfigurationChanged, result); HostCheck.True(output.IndexOfAnyExcept((byte)0xa5) < 0);
            HostCheck.Equal(Block.Status.Success, Block.Stop(f.State));
        }
        return true;
    }

    [HostTest("block.service original deadline and completion priority remain bounded", HostLane.Fast, 5)]
    private static bool Deadlines()
    {
        using (var f = new Fixture())
        {
            HostCheck.Equal(Block.Status.Success, f.Start()); HostCheck.Equal(Block.Status.Success, Submit(f.State, 1, out _));
            for (ulong i = 1; i < 10; i++) { f.Hardware->Now = i * 100; HostCheck.Equal(Block.Status.Pending, Poll(f.State, out _)); }
            f.Hardware->Now = 1000; HostCheck.Equal(Block.Status.Timeout, Poll(f.State, out _));
            HostCheck.Equal(1UL, f.Pool->AllocatedMask); HostCheck.Equal(Block.Status.Success, Block.Stop(f.State));
        }
        using (var f = new Fixture())
        {
            HostCheck.Equal(Block.Status.Success, f.Start()); HostCheck.Equal(Block.Status.Success, Submit(f.State, 1, out var token));
            f.Hardware->Now = 10000; f.Complete(); HostCheck.Equal(Block.Status.Ready, Poll(f.State, out _));
            Span<byte> output = stackalloc byte[512]; HostCheck.Equal(Block.Status.Success, Block.Copy(f.State, token, output));
            f.Hardware->Now = 100000000; HostCheck.Equal(Block.Status.Idle, Poll(f.State, out _));
            HostCheck.Equal(Block.Status.Success, Block.Stop(f.State));
        }
        using (var f = new Fixture())
        {
            HostCheck.Equal(Block.Status.Success, f.Start()); HostCheck.Equal(Block.Status.Success, Submit(f.State, 1, out var token));
            HostCheck.Equal(Block.Status.Success, Block.Cancel(f.State, token)); f.Hardware->Now = 1000;
            HostCheck.Equal(Block.Status.Timeout, Poll(f.State, out _)); HostCheck.Equal(Block.Status.Success, Block.Stop(f.State));
        }
        return true;
    }

    [HostTest("block.service clock failure regression overflow and idle failure retain DMA", HostLane.Fast, 6)]
    private static bool Clocks()
    {
        for (int mode = 0; mode < 4; mode++)
        {
            using var f = new Fixture(); f.Hardware->Now = mode == 2 ? ulong.MaxValue - 1 : 10;
            HostCheck.Equal(Block.Status.Success, f.Start());
            if (mode == 0 || mode == 3) f.Hardware->ClockFailure = true;
            if (mode == 1) f.Hardware->Now = 9;
            HostCheck.Equal(mode == 1 ? Block.Status.ClockRegressed : Block.Status.ClockFailed,
                mode == 3 ? Poll(f.State, out _) : Submit(f.State, 1, out _));
            HostCheck.True(f.State->RequiresCleanup && !f.State->Running); HostCheck.Equal(1UL, f.Pool->AllocatedMask);
            HostCheck.Equal(Block.Status.Success, Block.Stop(f.State));
        }
        return true;
    }

    [HostTest("block.service outstanding shutdown quarantines until fresh reset disable and fence", HostLane.Fast, 7)]
    private static bool QuarantinedReset()
    {
        using var f = new Fixture(); HostCheck.Equal(Block.Status.Success, f.Start()); HostCheck.Equal(Block.Status.Success, Submit(f.State, 55, out var token));
        Span<byte> output = stackalloc byte[512]; output.Fill(0xa5);
        f.Hardware->ResetStuck = true; HostCheck.Equal(Block.Status.ResetFailed, Block.Stop(f.State));
        HostCheck.Equal(Block.Status.ResetFailed, f.State->LastFailure);
        HostCheck.Equal(3U, f.State->Receipt.ResetReads); HostCheck.True(!f.State->Receipt.ResetComplete);
        f.Hardware->ResetStuck = false; f.Hardware->DisableFailure = true;
        HostCheck.Equal(Block.Status.ResetFailed, Block.Stop(f.State)); HostCheck.True(f.State->Receipt.ResetComplete && !f.State->Receipt.BusMasterDisabled);
        f.Hardware->DisableFailure = false; f.Hardware->ResetWriteFailure = true;
        HostCheck.Equal(Block.Status.ResetFailed, Block.Stop(f.State)); HostCheck.True(!f.State->Receipt.ResetComplete && f.State->Receipt.BusMasterDisabled);
        f.Hardware->ResetWriteFailure = false; f.Hardware->ResetFenceFailure = true;
        HostCheck.Equal(Block.Status.ResetFailed, Block.Stop(f.State)); HostCheck.Equal(VirtioBlkQueueStatus.OrderingFailed, f.State->Receipt.CleanupStatus);
        HostCheck.True(f.State->RequiresCleanup && !f.State->Running && *(ulong*)f.Data != 0);
        HostCheck.Equal(1UL, f.Pool->AllocatedMask); HostCheck.Equal(DmaBufferOwnership.Device, f.Owner);
        HostCheck.Equal(Block.Status.InvalidState, Block.Copy(f.State, token, output)); HostCheck.True(output.IndexOfAnyExcept((byte)0xa5) < 0);
        f.Hardware->ResetFenceFailure = false; HostCheck.Equal(Block.Status.Success, Block.Stop(f.State));
        HostCheck.True(f.State->Retired && f.Hardware->HeldAtResetFence); HostCheck.Equal(0UL, f.Pool->AllocatedMask);
        HostCheck.True(new ReadOnlySpan<byte>(f.Data, 4096).IndexOfAnyExcept((byte)0) < 0);
        uint before = f.Hardware->Calls; HostCheck.Equal(Block.Status.Success, Block.Stop(f.State));
        HostCheck.Equal(Block.Status.InvalidState, Submit(f.State, 1, out _)); HostCheck.Equal(before, f.Hardware->Calls);
        f.CheckGuards(); return true;
    }

    [HostTest("block.service invalid admission and metadata aliases have no effects", HostLane.Fast, 8)]
    private static bool Admission()
    {
        using var f = new Fixture(); var a = f.Input;
        a.NegotiatedFeatures = 0; HostCheck.Equal(Block.Status.UnsupportedFeatures, Block.Start(f.State, a));
        a = f.Input; a.NegotiatedFeatures |= 1UL << 28; HostCheck.Equal(Block.Status.UnsupportedFeatures, Block.Start(f.State, a));
        for (int mode = 0; mode < 12; mode++)
        {
            a = f.Input;
            switch (mode)
            {
                case 0: a.Epoch = 0; break; case 1: a.ExpectedCapacity = 0; break;
                case 2: a.ResetLimit = 0; break; case 3: a.ResetLimit = 1025; break;
                case 4: a.CompletionTimeoutNanos = 0; break; case 5: a.CompletionTimeoutNanos = 60000000001; break;
                case 6: a.Clock.Read = null; break; case 7: a.NotifyMultiplier = 3; break;
                case 8: a.Notify = a.Common; break; case 9: a.Access.BusMasterOn = null; break;
                case 10: a.Access.PrepareInterrupt = &Interrupt; break;
                case 11: a.Common = ChangedRegion(a.Common, "VirtualStart", (ulong)f.Pool); break;
            }
            HostCheck.Equal(Block.Status.InvalidAccess, Block.Start(f.State, a));
        }
        HostCheck.Equal(Block.Status.InvalidAccess, Block.Start((Block.State*)f.Data, f.Input));
        a = f.Input; a.Device = ChangedRegion(a.Device, "PhysicalStart", Physical); HostCheck.Equal(Block.Status.InvalidAccess, Block.Start(f.State, a));
        HostCheck.Equal(0U, f.Hardware->Calls); HostCheck.Equal(0UL, f.Pool->AllocatedMask);
        HostCheck.True(new ReadOnlySpan<byte>(f.Data, 4096).IndexOfAnyExcept((byte)0xa5) < 0);
        HostCheck.Equal(Block.Status.Success, f.Start()); HostCheck.Equal(Block.Status.InvalidAccess, f.Start());
        HostCheck.Equal(Block.Status.Success, Block.Stop(f.State)); return true;
    }

    [HostTest("block.service nonwrapping request generation and range reject before publication", HostLane.Fast, 9)]
    private static bool GenerationAndRange()
    {
        using var f = new Fixture(); HostCheck.Equal(Block.Status.Success, f.Start()); uint before = f.Hardware->Calls;
        HostCheck.Equal(Block.Status.SectorOutOfRange, Submit(f.State, Capacity, out var absent)); HostCheck.True(!absent.IsInitialized);
        // Host-only boundary setup avoids an impractical 2^64 request loop.
        ulong* generation = (ulong*)((byte*)f.State + (int)Marshal.OffsetOf<Block.State>("RequestGeneration"));
        *generation = ulong.MaxValue;
        HostCheck.Equal(Block.Status.GenerationExhausted, Submit(f.State, 0, out absent)); HostCheck.True(!absent.IsInitialized);
        HostCheck.Equal(before, f.Hardware->Calls); HostCheck.Equal(0UL, f.State->Submitted);
        (*generation)--; HostCheck.Equal(Block.Status.Success, Submit(f.State, Capacity - 1, out var last));
        f.Complete(); HostCheck.Equal(Block.Status.Ready, Poll(f.State, out _)); Span<byte> output = stackalloc byte[512];
        HostCheck.Equal(Block.Status.Success, Block.Copy(f.State, last, output)); CheckPayload(output, Capacity - 1);
        HostCheck.Equal(Block.Status.GenerationExhausted, Submit(f.State, 0, out _));
        HostCheck.Equal(Block.Status.Success, Block.Stop(f.State)); return true;
    }

    [HostTest("block.service callback failures preserve original setup and retained request ownership", HostLane.Fast, 10)]
    private static bool CallbackFailures()
    {
        Span<byte> output = stackalloc byte[512];
        uint setupCalls;
        using (var f = new Fixture()) { HostCheck.Equal(Block.Status.Success, f.Start()); setupCalls = f.Hardware->Calls; HostCheck.Equal(Block.Status.Success, Block.Stop(f.State)); }
        for (uint call = 1; call <= setupCalls; call++)
        {
            using var f = new Fixture(); f.Hardware->FailCall = call; f.Hardware->FailAfterWrite = true;
            var result = f.Start(); HostCheck.True(result != Block.Status.Success && f.Hardware->FailureFired);
            HostCheck.Equal(result, f.State->LastFailure); HostCheck.True(f.State->Retired); HostCheck.Equal(0UL, f.Pool->AllocatedMask);
        }
        for (int operation = 0; operation < 3; operation++)
        {
            uint calls;
            using (var f = new Fixture())
            {
                HostCheck.Equal(Block.Status.Success, f.Start()); Block.ReadToken token = default;
                if (operation > 0) { HostCheck.Equal(Block.Status.Success, Submit(f.State, 1, out token)); f.Complete(); }
                if (operation == 2) HostCheck.Equal(Block.Status.Ready, Poll(f.State, out _));
                uint start = f.Hardware->Calls;
                _ = operation == 0 ? Submit(f.State, 1, out _) : operation == 1 ? Poll(f.State, out _) : Block.Copy(f.State, token, output);
                calls = f.Hardware->Calls - start; HostCheck.Equal(Block.Status.Success, Block.Stop(f.State));
            }
            for (uint call = 1; call <= calls; call++)
            {
                using var f = new Fixture(); HostCheck.Equal(Block.Status.Success, f.Start()); Block.ReadToken token = default;
                output.Fill(0xa5);
                if (operation > 0) { HostCheck.Equal(Block.Status.Success, Submit(f.State, 1, out token)); f.Complete(); }
                if (operation == 2) HostCheck.Equal(Block.Status.Ready, Poll(f.State, out _));
                f.Hardware->FailCall = f.Hardware->Calls + call; f.Hardware->FailAfterWrite = true;
                var result = operation == 0 ? Submit(f.State, 1, out _) : operation == 1 ? Poll(f.State, out _) : Block.Copy(f.State, token, output);
                HostCheck.True(f.Hardware->FailureFired && f.State->RequiresCleanup && !f.State->Running);
                HostCheck.Equal(result, f.State->LastFailure); HostCheck.Equal(1UL, f.Pool->AllocatedMask);
                HostCheck.True(output.IndexOfAnyExcept((byte)0xa5) < 0); HostCheck.Equal(Block.Status.Success, Block.Stop(f.State));
            }
        }
        return true;
    }

    [HostTest("block.service exhausted pool preserves existing allocation and bytes", HostLane.Fast, 11)]
    private static bool ExhaustedPool()
    {
        using var f = new Fixture(); HostCheck.Equal(DmaBufferStatus.Success, PageAlignedDmaBuffers.TryAllocate(f.Pool, 1, out var held));
        HostCheck.Equal(Block.Status.SetupFailed, f.Start()); HostCheck.Equal(VirtioBlkQueueStatus.DmaRejected, f.State->Receipt.Status);
        HostCheck.True(f.State->Retired); HostCheck.Equal(1UL, f.Pool->AllocatedMask);
        HostCheck.True(new ReadOnlySpan<byte>(f.Data, 4096).IndexOfAnyExcept((byte)0xa5) < 0);
        HostCheck.Equal(DmaBufferStatus.Success, PageAlignedDmaBuffers.TryRelease(f.Pool, held.Token)); return true;
    }

    [HostTest("block.service warmed submit poll copy has no managed allocation", HostLane.Fast, 12)]
    private static bool NoAllocation()
    {
        using var f = new Fixture(); HostCheck.Equal(Block.Status.Success, f.Start()); Span<byte> output = stackalloc byte[512]; bool good = true; long before = 0;
        for (uint i = 0; i < 1001; i++)
        {
            if (i == 1) before = GC.GetAllocatedBytesForCurrentThread();
            good &= Submit(f.State, i, out var token) == Block.Status.Success; f.Complete();
            good &= Poll(f.State, out _) == Block.Status.Ready; good &= Block.Copy(f.State, token, output) == Block.Status.Success;
        }
        long after = GC.GetAllocatedBytesForCurrentThread(); HostCheck.True(good && !f.Hardware->BadOrder); HostCheck.Equal(before, after);
        HostCheck.Equal(Block.Status.Success, Block.Stop(f.State)); return true;
    }

    [HostTest("block.service metadata output aliases cannot damage an active owner or DMA", HostLane.Fast, 13)]
    private static bool MetadataAliases()
    {
        using var f = new Fixture(); HostCheck.Equal(Block.Status.Success, f.Start());
        HostCheck.Equal(Block.Status.Success, Submit(f.State, 27, out var token));
        byte[] state = new ReadOnlySpan<byte>(f.State, sizeof(Block.State)).ToArray();
        byte[] pool = new ReadOnlySpan<byte>(f.Pool, sizeof(DmaBufferPool)).ToArray();
        byte[] slot = new ReadOnlySpan<byte>(f.Slot, sizeof(DmaBufferSlot)).ToArray();
        byte[] dma = new ReadOnlySpan<byte>(f.Data, 4096).ToArray();
        byte* ordinary = stackalloc byte[128]; new Span<byte>(ordinary, 128).Fill(0xa5);
        ulong[] rejected = [0, 1, 0x0000800000000000UL, 0x00007ffffffffff8UL, ulong.MaxValue - 7, (ulong)ordinary + 1,
            (ulong)f.State, (ulong)f.State + 8, (ulong)f.Pool, (ulong)f.Slot,
            (ulong)f.Data + 128, (ulong)f.Data + 512, Common, Notify, Config];
        uint calls = f.Hardware->Calls;
        foreach (ulong address in rejected)
        {
            HostCheck.Equal(Block.Status.InvalidStorage, Block.Submit(f.State, 28, (Block.ReadToken*)address));
            HostCheck.Equal(Block.Status.InvalidStorage, Block.Poll(f.State, (Block.Completion*)address));
            HostCheck.True(new ReadOnlySpan<byte>(f.State, sizeof(Block.State)).SequenceEqual(state));
            HostCheck.True(new ReadOnlySpan<byte>(f.Pool, sizeof(DmaBufferPool)).SequenceEqual(pool));
            HostCheck.True(new ReadOnlySpan<byte>(f.Slot, sizeof(DmaBufferSlot)).SequenceEqual(slot));
            HostCheck.True(new ReadOnlySpan<byte>(f.Data, 4096).SequenceEqual(dma));
        }
        HostCheck.Equal(calls, f.Hardware->Calls); HostCheck.Equal(1UL, f.Pool->AllocatedMask);
        HostCheck.Equal(DmaBufferOwnership.Device, f.Owner); HostCheck.True(new ReadOnlySpan<byte>(ordinary, 128).IndexOfAnyExcept((byte)0xa5) < 0);
        f.Complete(); HostCheck.Equal(Block.Status.Ready, Poll(f.State, out _)); Span<byte> output = stackalloc byte[512];
        HostCheck.Equal(Block.Status.Success, Block.Copy(f.State, token, output)); CheckPayload(output, 27);
        HostCheck.Equal(Block.Status.Success, Block.Stop(f.State)); HostCheck.Equal(0UL, f.Pool->AllocatedMask); f.CheckGuards();
        // Invalid/copy/retired owners must not clear even an ordinary output.
        HostCheck.Equal(Block.Status.InvalidState, Block.Submit(f.State, 1, (Block.ReadToken*)ordinary));
        HostCheck.Equal(Block.Status.InvalidState, Block.Poll(f.State, (Block.Completion*)ordinary));
        HostCheck.True(new ReadOnlySpan<byte>(ordinary, 128).IndexOfAnyExcept((byte)0xa5) < 0); return true;
    }

    // Test-only value conveniences use distinct stack storage. Production APIs
    // validate explicit metadata pointers before their first output write.
    private static Block.Status Submit(Block.State* state, ulong sector, out Block.ReadToken token)
    {
        Block.ReadToken value = default; var status = Block.Submit(state, sector, &value); token = value; return status;
    }
    private static Block.Status Poll(Block.State* state, out Block.Completion completion)
    {
        Block.Completion value = default; var status = Block.Poll(state, &value); completion = value; return status;
    }

    private const ulong Common = 0x400000, Notify = 0x403000, Config = 0x402000,
        Physical = 0x220000, DeviceBase = 0x123456780000, Capacity = 0x100000010;
    private static byte Payload(ulong sector, int index) => unchecked((byte)((sector * 13) ^ ((ulong)index * 17) ^ (sector >> 8)));
    private static void CheckPayload(ReadOnlySpan<byte> bytes, ulong sector)
    { bool good = true; for (int i = 0; i < 512; i++) good &= bytes[i] == Payload(sector, i); HostCheck.True(good); }
    private static Block.ReadToken CorruptToken(Block.ReadToken token, string field)
    { *(ulong*)((byte*)&token + (int)Marshal.OffsetOf<Block.ReadToken>(field)) ^= 1; return token; }
    private static VirtioMappedRegion ChangedRegion(VirtioMappedRegion region, string field, ulong value)
    { *(ulong*)((byte*)&region + (int)Marshal.OffsetOf<VirtioMappedRegion>(field)) = value; return region; }
    private struct Hardware
    {
        internal byte* Data;
        internal DmaBufferPool* Pool;
        internal ulong Capacity, Now, Descriptor, Available, Used, Sector;
        internal uint Calls, Fences, FailCall, Resets, DriverOkWrites, Notifies, GenerationReads, Produced;
        internal ushort Selected, Size, Enabled, ConfigVector, QueueVector, AvailableSeen, FencedIndex;
        internal byte Status, Generation;
        internal bool BusEnabled, BadOrder, Pending, IndexObserved, Acquired, ResetRequested, ResetObserved, HeldAtResetFence;
        internal bool FailureFired, FailAfterWrite, ResetStuck, ResetWriteFailure, DisableFailure, ResetFenceFailure, ClockFailure, GenerationChurn;
    }

    private static bool Enter(Hardware* h, bool write = false)
    { h->Calls++; if (h->Calls != h->FailCall) return true; h->FailureFired = true; return write && h->FailAfterWrite; }
    private static byte Exit(Hardware* h) => h->Calls == h->FailCall ? (byte)0 : (byte)1;
    [UnmanagedCallersOnly] private static byte Interrupt(void* c, VirtioMappedRegion common) => 0;
    [UnmanagedCallersOnly] private static byte Read8(void* c, ulong address, byte* value)
    {
        var h = (Hardware*)c; if (!Enter(h)) return 0;
        if (address == Common + 20) { *value = h->Status; if (h->ResetRequested && *value == 0) h->ResetObserved = true; }
        else if (address == Common + 21) { h->GenerationReads++; *value = h->GenerationChurn ? (byte)h->GenerationReads : h->Generation; }
        else if (address == (ulong)h->Data + 272) { if (!h->Acquired) h->BadOrder = true; *value = h->Data[272]; }
        else return 0;
        return 1;
    }
    [UnmanagedCallersOnly] private static byte Write8(void* c, ulong address, byte value)
    {
        var h = (Hardware*)c; if (!Enter(h, true) || address != Common + 20) return 0;
        if (value == 15)
        {
            if (!h->BusEnabled || h->Enabled != 1 || h->ConfigVector != ushort.MaxValue || h->QueueVector != ushort.MaxValue) h->BadOrder = true;
            h->Status = 15; h->DriverOkWrites++;
        }
        else if (value == 0)
        {
            h->ResetRequested = true; h->ResetObserved = false;
            if (h->ResetWriteFailure) return 0;
            h->Resets++; if (!h->ResetStuck) { h->Status = 0; h->Enabled = 0; }
        }
        else return 0;
        return Exit(h);
    }
    [UnmanagedCallersOnly] private static byte Read16(void* c, ulong address, ushort* value)
    {
        var h = (Hardware*)c; if (!Enter(h)) return 0;
        if (address == (ulong)h->Data + 150) { *value = *(ushort*)(h->Data + 150); h->IndexObserved = true; h->Acquired = false; return 1; }
        switch (address - Common)
        {
            case 16: *value = h->ConfigVector; break; case 18: *value = 1; break;
            case 22: *value = h->Selected; break; case 24: *value = h->Size == 0 ? (ushort)16 : h->Size; break;
            case 26: *value = h->QueueVector; break; case 28: *value = h->Enabled; break; case 30: *value = 3; break;
            default: return 0;
        }
        return 1;
    }
    [UnmanagedCallersOnly] private static byte Write16(void* c, ulong address, uint value)
    {
        var h = (Hardware*)c; if (!Enter(h, true)) return 0;
        if (address == Notify + 12 && value == 0)
        {
            ushort index = *(ushort*)(h->Data + 130);
            if (!h->BusEnabled || h->Status != 15 || h->Enabled != 1 || h->Pending ||
                h->Pool->AllocatedMask != 1 || index != unchecked((ushort)(h->AvailableSeen + 1)) || h->FencedIndex != index ||
                *(ushort*)(h->Data + 132 + (h->AvailableSeen & 7) * 2) != 0 || *(ushort*)(h->Data + 128) != 1) h->BadOrder = true;
            for (int i = 0; i < 3; i++)
            {
                byte* p = h->Data + i * 16;
                if (*(ulong*)p != DeviceBase + (i == 0 ? 256UL : i == 1 ? 512UL : 272UL) ||
                    *(uint*)(p + 8) != (i == 0 ? 16U : i == 1 ? 512U : 1U) ||
                    *(ushort*)(p + 12) != (i == 0 ? 1 : i == 1 ? 3 : 2) || *(ushort*)(p + 14) != (i == 0 ? 1 : i == 1 ? 2 : 0)) h->BadOrder = true;
            }
            if (*(ulong*)(h->Data + 256) != 0 || h->Data[272] != 255 || new ReadOnlySpan<byte>(h->Data + 512, 512).IndexOfAnyExcept((byte)0) >= 0) h->BadOrder = true;
            h->Sector = *(ulong*)(h->Data + 264); h->AvailableSeen = index; h->Notifies++; h->Pending = true;
            return Exit(h);
        }
        switch (address - Common)
        {
            case 16: h->ConfigVector = (ushort)value; break; case 22: h->Selected = (ushort)value; break;
            case 24: h->Size = (ushort)value; break; case 26: h->QueueVector = (ushort)value; break;
            case 28:
                if (h->BusEnabled || h->Descriptor != DeviceBase || h->Available != DeviceBase + 128 || h->Used != DeviceBase + 148) h->BadOrder = true;
                h->Enabled = (ushort)value; break;
            default: return 0;
        }
        return Exit(h);
    }
    [UnmanagedCallersOnly] private static byte Read32(void* c, ulong address, uint* value)
    {
        var h = (Hardware*)c; if (!Enter(h)) return 0;
        if (address == Config) { *value = (uint)h->Capacity; return 1; }
        if (address == Config + 4) { *value = (uint)(h->Capacity >> 32); return 1; }
        if (address >= (ulong)h->Data + 152 && address < (ulong)h->Data + 216 && (address & 3) == 0)
        { if (!h->Acquired) h->BadOrder = true; *value = *(uint*)address; return 1; }
        ulong offset = address - Common;
        if (offset < 32 || offset > 52 || (offset & 3) != 0) return 0;
        ulong bits = offset < 40 ? h->Descriptor : offset < 48 ? h->Available : h->Used;
        *value = (uint)(bits >> ((offset & 4) == 0 ? 0 : 32)); return 1;
    }
    [UnmanagedCallersOnly] private static byte Write32(void* c, ulong address, uint value)
    {
        var h = (Hardware*)c; if (!Enter(h, true)) return 0; ulong offset = address - Common;
        if (offset < 32 || offset > 52 || (offset & 3) != 0) return 0;
        if (h->BusEnabled || h->Enabled != 0) h->BadOrder = true;
        ulong* target = offset < 40 ? &h->Descriptor : offset < 48 ? &h->Available : &h->Used;
        int shift = (offset & 4) == 0 ? 0 : 32; *target = (*target & ~(0xffffffffUL << shift)) | ((ulong)value << shift); return Exit(h);
    }
    [UnmanagedCallersOnly] private static byte Fence(void* c)
    {
        var h = (Hardware*)c; if (!Enter(h)) return 0; h->Fences++;
        if (h->ResetRequested)
        {
            if (h->ResetFenceFailure) return 0;
            if (!h->ResetObserved || h->BusEnabled) h->BadOrder = true;
            if (h->Pool->AllocatedMask == 1) h->HeldAtResetFence = true;
        }
        if (h->IndexObserved) h->Acquired = true;
        h->FencedIndex = *(ushort*)(h->Data + 130); return 1;
    }
    [UnmanagedCallersOnly] private static byte BusOn(void* c)
    { var h = (Hardware*)c; if (!Enter(h, true)) return 0; h->BusEnabled = true; return Exit(h); }
    [UnmanagedCallersOnly] private static byte BusOff(void* c)
    { var h = (Hardware*)c; if (!Enter(h, true) || h->DisableFailure) return 0; h->BusEnabled = false; return Exit(h); }
    [UnmanagedCallersOnly] private static byte Clock(void* c, ulong* value)
    { var h = (Hardware*)c; if (!Enter(h) || h->ClockFailure) return 0; *value = h->Now; return 1; }

    private sealed class Fixture : IDisposable
    {
        private static long epoch;
        private readonly byte* backing = (byte*)NativeMemory.AlignedAlloc(12288, 4096);
        internal byte* Data => backing + 4096;
        internal readonly Block.State* State = (Block.State*)NativeMemory.AllocZeroed((nuint)sizeof(Block.State));
        internal readonly DmaBufferPool* Pool = (DmaBufferPool*)NativeMemory.AllocZeroed((nuint)sizeof(DmaBufferPool));
        internal readonly DmaBufferSlot* Slot = (DmaBufferSlot*)NativeMemory.AllocZeroed((nuint)sizeof(DmaBufferSlot));
        internal readonly Hardware* Hardware = (Hardware*)NativeMemory.AllocZeroed((nuint)sizeof(Hardware));
        private static readonly int ownerOffset = (int)Marshal.OffsetOf<DmaBufferSlot>("Owner");
        internal DmaBufferOwnership Owner => *(DmaBufferOwnership*)((byte*)Slot + ownerOffset);
        private readonly ulong lifetime;
        private readonly VirtioMappedRegion common, notify, device;
        internal Fixture()
        {
            new Span<byte>(backing, 12288).Fill(0xa5);
            HostCheck.Equal(DmaAddressStatus.Success, DmaAddressWindow.TryCreate((ulong)Data, Physical, DeviceBase, 4096, 64, out var window));
            lifetime = (ulong)Interlocked.Increment(ref epoch);
            HostCheck.Equal(DmaBufferStatus.Success, PageAlignedDmaBuffers.TryInitialize(Pool, window, Slot, 1, lifetime));
            Hardware->Data = Data; Hardware->Pool = Pool; Hardware->Capacity = Capacity; Hardware->Status = 11; Hardware->Generation = 7;
            PciSizedBar* bars = stackalloc PciSizedBar[6]; new Span<PciSizedBar>(bars, 6).Clear();
            HostCheck.Equal(PciBarProbeStatus.Success, BARPrefetchTypeRegressionTestsDerivedMOSAFailures.Decode(0xc, 0xc0, 0xffffc00c, 0xffffffff, 4, out bars[4]));
            var caps = VirtioPciCapabilitySet.Create(new(VirtioPciCapabilityType.CommonConfiguration, 4, 0, 56, 0),
                new(VirtioPciCapabilityType.NotifyConfiguration, 4, 0x3000, 64, 4),
                new(VirtioPciCapabilityType.IsrStatus, 4, 0x1000, 1, 0), new(VirtioPciCapabilityType.DeviceConfiguration, 4, 0x2000, 8, 0));
            VirtioConfigurationRegion* regions = stackalloc VirtioConfigurationRegion[4];
            HostCheck.Equal(VirtioRegionStatus.Success, CommonNotifyISRDeviceConfigurationRegions.PlanBlock(caps, bars, 6, regions, 4));
            HostCheck.Equal(VirtioRegionStatus.Success, CommonNotifyISRDeviceConfigurationRegions.Bind(regions[0], Common, out common));
            HostCheck.Equal(VirtioRegionStatus.Success, CommonNotifyISRDeviceConfigurationRegions.BindNotificationPage(regions[1], 64, Notify, out notify));
            HostCheck.Equal(VirtioRegionStatus.Success, CommonNotifyISRDeviceConfigurationRegions.Bind(regions[3], Config, out device));
        }
        internal Block.Admission Input => new()
        {
            Common = common, Notify = notify, Device = device,
            NotifyMultiplier = 4, ResetLimit = 3, Pool = Pool, Epoch = lifetime, NegotiatedFeatures = BoundedVirtioFeatures.Version1 | BoundedVirtioFeatures.BlockReadOnly,
            ExpectedCapacity = Capacity, CompletionTimeoutNanos = 1000,
            Access = new() { Queue = new() { Context = Hardware, Read8 = &Read8, Write8 = &Write8, Read16 = &Read16, Write16 = &Write16,
                Read32 = &Read32, Write32 = &Write32, Fence = &Fence, BusMasterOff = &BusOff }, BusMasterOn = &BusOn },
            Clock = new() { Context = Hardware, Read = &Clock }
        };
        internal Block.Status Start() => Block.Start(State, Input);
        internal void Complete(uint id = 0, uint length = 513, byte status = 0, ushort delta = 1)
        {
            if (!Hardware->Pending) Hardware->BadOrder = true;
            for (int i = 0; i < 512; i++) Data[512 + i] = Payload(Hardware->Sector, i);
            Data[272] = status; byte* element = Data + 152 + (Hardware->Produced & 7) * 8;
            *(uint*)element = id; *(uint*)(element + 4) = length;
            Hardware->Produced += delta; *(ushort*)(Data + 150) = unchecked((ushort)Hardware->Produced); Hardware->Pending = false;
        }
        internal void CheckGuards() => HostCheck.True(!Hardware->BadOrder &&
            new ReadOnlySpan<byte>(backing, 4096).IndexOfAnyExcept((byte)0xa5) < 0 && new ReadOnlySpan<byte>(Data + 4096, 4096).IndexOfAnyExcept((byte)0xa5) < 0);
        public void Dispose()
        { NativeMemory.AlignedFree(backing); NativeMemory.Free(State); NativeMemory.Free(Pool); NativeMemory.Free(Slot); NativeMemory.Free(Hardware); }
    }
}
