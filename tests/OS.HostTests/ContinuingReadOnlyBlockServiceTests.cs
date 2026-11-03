using System.Runtime.InteropServices;
using OS.Drivers;
using OS.Kernel.Platform;
using OS.Storage;
using Block = OS.Storage.ContinuingReadOnlyBlockService;

internal static unsafe partial class ContinuingReadOnlyBlockServiceTests
{
    [HostTest("block.service copy widths preserve every supported length alignment and destination boundary")]
    private static void CopyWidthsAndBounds()
    {
        for (uint sectors = 1; sectors <= Block.MaximumReadSectors; sectors++)
            for (int offset = 0; offset < 8; offset++)
            {
                uint bytes = sectors * 512;
                using var f = new Fixture();
                f.Hardware->ExpectedBytes = bytes;
                using var capture = new Capture(f, bytes);
                HostCheck.Equal(Block.Status.Success, f.Start());
                byte* allocation = capture.Sink->Output;
                HostCheck.True(((ulong)allocation & 7) == 0 && ((ulong)(f.Data + 512) & 7) == 0);
                Span<byte> guarded = new(allocation, (int)bytes + 8);
                guarded.Fill(0xa5);
                capture.Sink->Output = allocation + offset;
                try
                {
                    HostCheck.Equal(Block.Status.Success, Submit(f.State, 61, sectors, out var token));
                    f.Complete(length: bytes + 1);
                    HostCheck.Equal(Block.Status.Ready, Poll(f.State, out _));
                    new Span<byte>(f.Data + 512 + bytes, 8).Fill(0x77);
                    byte[] source = new ReadOnlySpan<byte>(f.Data + 512, (int)bytes + 8).ToArray();
                    // Exact and oversized spans alternate; payload length comes
                    // from the retained request, never from span capacity.
                    Span<byte> output = new(capture.Sink->Output, (int)bytes + (offset % 2 == 0 ? 0 : 7 - offset));
                    HostCheck.Equal(Block.Status.Success, Block.Copy(f.State, token, output));
                    HostCheck.True(output[..(int)bytes].SequenceEqual(source.AsSpan(0, (int)bytes)));
                    HostCheck.True(guarded[..offset].IndexOfAnyExcept((byte)0xa5) < 0 &&
                        guarded[(offset + (int)bytes)..].IndexOfAnyExcept((byte)0xa5) < 0);
                    HostCheck.True(new ReadOnlySpan<byte>(f.Data + 512, (int)bytes + 8).SequenceEqual(source));
                    HostCheck.True(!capture.Sink->Bad && capture.Sink->Count == 3 && f.State->Copied == 1);
                    HostCheck.True(!f.State->ReadReady && f.State->Submitted == 1 && f.State->Completed == 1);
                    HostCheck.Equal(Block.Status.StaleToken, Block.Copy(f.State, token, output));
                    HostCheck.Equal(Block.Status.Success, Block.Stop(f.State));
                    f.CheckGuards();
                }
                finally
                {
                    capture.Sink->Output = allocation;
                }
            }
    }

    [HostTest("block.service copy widths preserve observer rejection cancellation stale owner fence and short output without writes")]
    private static void CopyWidthsRejectBeforeWrite()
    {
        foreach (int offset in new[] { 0, 1 })
            for (int mode = 0; mode < 5; mode++)
            {
                const uint bytes = 2048;
                using var f = new Fixture();
                f.Hardware->ExpectedBytes = bytes;
                using var capture = new Capture(f, bytes);
                HostCheck.Equal(Block.Status.Success, f.Start());
                byte* allocation = capture.Sink->Output;
                Span<byte> guarded = new(allocation, (int)bytes + 8);
                guarded.Fill(0xa5);
                capture.Sink->Output = allocation + offset;
                try
                {
                    Span<byte> output = new(capture.Sink->Output, (int)bytes);
                    HostCheck.Equal(Block.Status.Success, Submit(f.State, 62, 4, out var token));
                    if (mode != 3)
                    {
                        f.Complete(length: bytes + 1);
                        HostCheck.Equal(Block.Status.Ready, Poll(f.State, out _));
                        HostCheck.Equal(Block.Status.ShortBuffer, Block.Copy(f.State, token, output[..2047]));
                        HostCheck.Equal(2U, capture.Sink->Count);
                    }
                    Block.Status expected;
                    if (mode == 0)
                    {
                        capture.Sink->Reject = true;
                        capture.Sink->RejectKind = Block.ObservationKind.CopyReady;
                        expected = Block.Status.ObservationRejected;
                    }
                    else if (mode == 2)
                    {
                        token = CorruptToken(token, "Owner");
                        expected = Block.Status.StaleToken;
                    }
                    else if (mode == 4)
                    {
                        // Two device/config reads and the used-index read
                        // succeed, then the existing pre-copy fence rejects.
                        f.Hardware->FailCall = f.Hardware->Calls + 4;
                        expected = Block.Status.OrderingFailed;
                    }
                    else
                    {
                        HostCheck.Equal(Block.Status.Success, Block.Cancel(f.State, token));
                        expected = mode == 3 ? Block.Status.Cancelled : Block.Status.StaleToken;
                    }
                    HostCheck.Equal(expected, Block.Copy(f.State, token, output));
                    if (mode == 3)
                    {
                        f.Complete(length: bytes + 1);
                        HostCheck.Equal(Block.Status.Cancelled, Poll(f.State, out var completion));
                        HostCheck.Equal(0U, completion.Bytes);
                    }
                    HostCheck.True(guarded.IndexOfAnyExcept((byte)0xa5) < 0 && f.State->Copied == 0 && !capture.Sink->Bad);
                    if (mode == 4)
                        HostCheck.True(f.Hardware->FailureFired);
                    HostCheck.Equal(mode == 0 ? 3U : 2U, capture.Sink->Count);
                    HostCheck.Equal(Block.Status.Success, Block.Stop(f.State));
                    f.CheckGuards();
                }
                finally
                {
                    capture.Sink->Output = allocation;
                }
            }
    }

    [HostTest("block.service explicit one through four sectors retain exact descriptor observation and copy lengths")]
    private static void BoundedReads()
    {
        HostCheck.Equal(4U, Block.MaximumReadSectors);
        for (uint sectors = 1; sectors <= 4; sectors++)
        {
            uint bytes = sectors * 512;
            using var f = new Fixture();
            f.Hardware->ExpectedBytes = bytes;
            using var capture = new Capture(f, bytes);
            HostCheck.Equal(Block.Status.Success, f.Start());
            new Span<byte>(f.Data + 512 + bytes, (int)(4096 - 512 - bytes)).Fill(0x77);
            Span<byte> output = new(capture.Sink->Output, (int)bytes + 8);
            output.Fill(0xa5);
            HostCheck.Equal(Block.Status.Success, Submit(f.State, 20, sectors, out var token));
            HostCheck.Equal(bytes, f.State->RequestBytes);
            HostCheck.Equal(bytes, *(uint*)(f.Data + 24));
            HostCheck.True(new ReadOnlySpan<byte>(f.Data + 512 + bytes, (int)(4096 - 512 - bytes)).IndexOfAnyExcept((byte)0x77) < 0);
            f.Complete(length: bytes + 1);
            HostCheck.Equal(Block.Status.Ready, Poll(f.State, out var completion));
            HostCheck.Equal(bytes, completion.Bytes);
            HostCheck.Equal(Block.Status.Ready, Poll(f.State, out completion));
            HostCheck.Equal(bytes, completion.Bytes);
            HostCheck.Equal(Block.Status.ShortBuffer, Block.Copy(f.State, token, output[..((int)bytes - 1)]));
            HostCheck.Equal(Block.Status.StaleToken, Block.Copy(f.State, CorruptToken(token, "Generation"), output));
            HostCheck.True(output.IndexOfAnyExcept((byte)0xa5) < 0);
            HostCheck.Equal(Block.Status.Success, Block.Copy(f.State, token, output));
            for (int i = 0; i < bytes; i++)
                HostCheck.Equal(Payload(20, i), output[i]);
            HostCheck.True(output[(int)bytes..].IndexOfAnyExcept((byte)0xa5) < 0);
            HostCheck.True(!capture.Sink->Bad && capture.Sink->Count == 3 && f.State->Copied == 1);
            HostCheck.Equal(Block.Status.StaleToken, Block.Copy(f.State, token, output));
            HostCheck.Equal(Block.Status.Success, Block.Stop(f.State));
            f.CheckGuards();
        }
    }

    [HostTest("block.service bounded count capacity and overflow refusals publish nothing")]
    private static void BoundedRangeRejections()
    {
        using var f = new Fixture();
        HostCheck.Equal(Block.Status.Success, f.Start());
        byte[] data = new ReadOnlySpan<byte>(f.Data, 4096).ToArray();
        uint calls = f.Hardware->Calls;
        foreach (var request in new (ulong Sector, uint Sectors)[]
            { (0, 0), (0, 5), (0, uint.MaxValue), (Capacity - 1, 2), (Capacity - 3, 4), (ulong.MaxValue, 4) })
        {
            HostCheck.Equal(Block.Status.SectorOutOfRange, Submit(f.State, request.Sector, request.Sectors, out var token));
            HostCheck.True(!token.IsInitialized && f.State->Submitted == 0 && f.Hardware->Calls == calls);
            HostCheck.True(data.AsSpan().SequenceEqual(new ReadOnlySpan<byte>(f.Data, 4096)));
        }
        f.Hardware->ExpectedBytes = 2048;
        HostCheck.Equal(Block.Status.Success, Submit(f.State, Capacity - 4, 4, out var last));
        f.Complete(length: 2049);
        HostCheck.Equal(Block.Status.Ready, Poll(f.State, out var done));
        HostCheck.Equal(Capacity - 4, done.Sector);
        HostCheck.Equal(2048U, done.Bytes);
        Span<byte> output = stackalloc byte[2048];
        HostCheck.Equal(Block.Status.Success, Block.Copy(f.State, last, output));
        f.Hardware->ExpectedBytes = 512;
        HostCheck.Equal(Block.Status.Success, Submit(f.State, Capacity - 1, out var legacy));
        f.Complete();
        HostCheck.Equal(Block.Status.Ready, Poll(f.State, out done));
        HostCheck.Equal(512U, done.Bytes);
        HostCheck.Equal(Block.Status.Success, Block.Copy(f.State, legacy, output));
        HostCheck.Equal(Block.Status.Success, Block.Stop(f.State));
        f.CheckGuards();
    }

    [HostTest("block.service bounded cancellation drains with zero bytes and revokes ready copies")]
    private static void BoundedCancellation()
    {
        foreach (uint sectors in new uint[] { 2, 4 })
            foreach (bool ready in new[] { false, true })
            {
                uint bytes = sectors * 512;
                using var f = new Fixture();
                f.Hardware->ExpectedBytes = bytes;
                using var capture = new Capture(f, bytes);
                HostCheck.Equal(Block.Status.Success, f.Start());
                Span<byte> output = new(capture.Sink->Output, (int)bytes);
                output.Fill(0xa5);
                HostCheck.Equal(Block.Status.Success, Submit(f.State, 40, sectors, out var token));
                if (ready)
                {
                    f.Complete(length: bytes + 1);
                    HostCheck.Equal(Block.Status.Ready, Poll(f.State, out _));
                }
                HostCheck.Equal(Block.Status.Success, Block.Cancel(f.State, token));
                HostCheck.Equal(ready ? Block.Status.StaleToken : Block.Status.Cancelled, Block.Copy(f.State, token, output));
                if (!ready)
                {
                    HostCheck.Equal(Block.Status.Busy, Submit(f.State, 41, sectors, out _));
                    f.Complete(length: bytes + 1);
                    HostCheck.Equal(Block.Status.Cancelled, Poll(f.State, out var done));
                    HostCheck.Equal(0U, done.Bytes);
                    HostCheck.Equal(0U, capture.Sink->Records[1].Bytes);
                }
                HostCheck.True(!capture.Sink->Bad && capture.Sink->Count == 2 && f.State->Copied == 0 && f.State->Cancelled == 1);
                HostCheck.True(output.IndexOfAnyExcept((byte)0xa5) < 0);
                HostCheck.Equal(Block.Status.StaleToken, Block.Copy(f.State, token, output));
                HostCheck.Equal(Block.Status.Success, Block.Stop(f.State));
                f.CheckGuards();
            }
    }

    [HostTest("block.service bounded reads preserve completion config deadline observation and reentry failure isolation")]
    private static void BoundedFailures()
    {
        for (int mode = 0; mode < 5; mode++)
        {
            using var f = new Fixture();
            f.Hardware->ExpectedBytes = 2048;
            using var capture = new Capture(f, 2048);
            HostCheck.Equal(Block.Status.Success, f.Start());
            Span<byte> output = new(capture.Sink->Output, 2048);
            output.Fill(0xa5);
            HostCheck.Equal(Block.Status.Success, Submit(f.State, 50, 4, out var token));
            if (mode == 2)
                f.Hardware->Now = 1000;
            else
                f.Complete(length: mode == 0 ? 2048U : 2049U);
            if (mode == 1)
                f.Hardware->Generation++;
            if (mode < 3)
                HostCheck.Equal(mode == 0 ? Block.Status.InvalidCompletion : mode == 1 ? Block.Status.ConfigurationChanged : Block.Status.Timeout,
                    Poll(f.State, out _));
            else
            {
                HostCheck.Equal(Block.Status.Ready, Poll(f.State, out _));
                if (mode == 3)
                {
                    capture.Sink->Reject = true;
                    capture.Sink->RejectKind = Block.ObservationKind.CopyReady;
                }
                else
                {
                    capture.Sink->ReenterOperation = 6;
                    capture.Sink->ReenterKind = Block.ObservationKind.CopyReady;
                }
                HostCheck.Equal(Block.Status.ObservationRejected, Block.Copy(f.State, token, output));
                if (mode == 4)
                    HostCheck.True(capture.Sink->NestedStatus == Block.Status.ObservationRejected && capture.Sink->NestedUnchanged);
            }
            HostCheck.True(output.IndexOfAnyExcept((byte)0xa5) < 0 && f.State->Copied == 0 && f.Pool->AllocatedMask == 1);
            HostCheck.Equal(Block.Status.Success, Block.Stop(f.State));
            f.CheckGuards();
        }
    }

    [HostTest("block.service bounded request byte occupies legacy tail padding without changing state size or counter offsets")]
    private static void BoundedStateLayout()
    {
        HostCheck.Equal(sizeof(LegacyState), sizeof(Block.State));
        LegacyState legacy = default;
        Block.State state = default;
        int oldSubmitted = (int)((byte*)&legacy.SubmittedCount - (byte*)&legacy);
        int oldFailure = (int)((byte*)&legacy.Failure - (byte*)&legacy);
        int request = -1, submitted = -1;
        // Observe actual managed offsets through existing copied-value accessors;
        // Marshal's unmanaged BOOL representation need not match unsafe sizeof.
        for (int i = 0; i < sizeof(Block.State); i++)
        {
            ((byte*)&state)[i] = 1;
            if (state.RequestBytes == 512)
            {
                HostCheck.Equal(-1, request);
                request = i;
            }
            if (state.Submitted == 1)
            {
                HostCheck.Equal(-1, submitted);
                submitted = i;
            }
            ((byte*)&state)[i] = 0;
        }
        HostCheck.Equal(oldFailure + 1, request);
        HostCheck.Equal(oldSubmitted, submitted);
        HostCheck.True(request < submitted && submitted - oldFailure == 4);
    }

    [HostTest("block.service retained independent reads cross actual ushort ring wrap", HostLane.Fast, 0)]
    private static bool RetainedAndWrap()
    {
        using var f = new Fixture();
        HostCheck.Equal(Block.Status.Success, f.Start());
        Span<byte> output = stackalloc byte[520];
        bool good = true;
        for (uint i = 0; i < 65538; i++)
        {
            output.Fill(0xa5);
            good &= Submit(f.State, i, out var token) == Block.Status.Success;
            f.Complete();
            good &= Poll(f.State, out var completion) == Block.Status.Ready && completion.Sector == i && completion.Bytes == 512;
            good &= Block.Copy(f.State, token, output) == Block.Status.Success;
            for (int j = 0; j < 512; j++)
                good &= output[j] == Payload(i, j);
            good &= output[512..].IndexOfAnyExcept((byte)0xa5) < 0 && f.Pool->AllocatedMask == 1 && f.Owner == DmaBufferOwnership.Device;
        }
        HostCheck.True(good && !f.Hardware->BadOrder);
        HostCheck.Equal(65538UL, f.State->Submitted);
        HostCheck.Equal(65538UL, f.State->Completed);
        HostCheck.Equal(65538UL, f.State->Copied);
        HostCheck.Equal((ushort)2, f.State->PublishedIndex);
        HostCheck.Equal((ushort)2, f.State->ConsumedIndex);
        HostCheck.Equal(1U, f.Hardware->DriverOkWrites);
        HostCheck.Equal(0U, f.Hardware->Resets);
        HostCheck.Equal(Block.Status.Success, Block.Stop(f.State));
        HostCheck.True(f.State->Retired && f.Hardware->HeldAtResetFence && !f.Hardware->BusEnabled);
        HostCheck.Equal(0UL, f.Pool->AllocatedMask);
        HostCheck.True(new ReadOnlySpan<byte>(f.Data, 4096).IndexOfAnyExcept((byte)0) < 0);
        f.CheckGuards();
        return true;
    }

    [HostTest("block.service busy token copy retry and copied owner guards", HostLane.Fast, 1)]
    private static bool TokensAndCopy()
    {
        using var f = new Fixture();
        HostCheck.Equal(Block.Status.Success, f.Start());
        Span<byte> output = stackalloc byte[512];
        output.Fill(0xa5);
        HostCheck.Equal(Block.Status.Success, Submit(f.State, 7, out var first));
        HostCheck.Equal(Block.Status.Busy, Submit(f.State, 8, out var absent));
        HostCheck.True(!absent.IsInitialized);
        HostCheck.Equal(Block.Status.Pending, Block.Copy(f.State, first, output));
        HostCheck.Equal(Block.Status.Pending, Poll(f.State, out _));
        Block.State copy = *f.State;
        uint before = f.Hardware->Calls;
        HostCheck.Equal(Block.Status.InvalidState, Poll(&copy, out _));
        HostCheck.Equal(Block.Status.InvalidState, Block.Stop(&copy));
        HostCheck.Equal(before, f.Hardware->Calls);
        f.Complete();
        HostCheck.Equal(Block.Status.Ready, Poll(f.State, out _));
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
        HostCheck.Equal(before, f.Hardware->Calls);
        HostCheck.True(output.IndexOfAnyExcept((byte)0xa5) < 0 && f.State->ReadReady);
        HostCheck.Equal(Block.Status.Success, Block.Copy(f.State, first, output));
        CheckPayload(output, 7);
        HostCheck.Equal(Block.Status.StaleToken, Block.Copy(f.State, first, output));
        HostCheck.Equal(Block.Status.Success, Submit(f.State, 8, out var second));
        output.Fill(0xa5);
        HostCheck.Equal(Block.Status.StaleToken, Block.Copy(f.State, first, output));
        f.Complete();
        HostCheck.Equal(Block.Status.Ready, Poll(f.State, out _));
        HostCheck.Equal(Block.Status.Success, Block.Copy(f.State, second, output));
        CheckPayload(output, 8);
        HostCheck.Equal(Block.Status.Success, Block.Stop(f.State));
        return true;
    }

    [HostTest("block.service cancellation drains outstanding DMA and revokes ready output", HostLane.Fast, 2)]
    private static bool Cancellation()
    {
        using var f = new Fixture();
        HostCheck.Equal(Block.Status.Success, f.Start());
        Span<byte> output = stackalloc byte[512];
        output.Fill(0xa5);
        HostCheck.Equal(Block.Status.Success, Submit(f.State, 9, out var token));
        HostCheck.Equal(Block.Status.Success, Block.Cancel(f.State, token));
        HostCheck.Equal(Block.Status.Success, Block.Cancel(f.State, token));
        HostCheck.Equal(Block.Status.Cancelled, Block.Copy(f.State, token, output));
        HostCheck.Equal(Block.Status.Busy, Submit(f.State, 10, out _));
        HostCheck.Equal(Block.Status.Pending, Poll(f.State, out _));
        HostCheck.Equal(1UL, f.Pool->AllocatedMask);
        HostCheck.Equal(DmaBufferOwnership.Device, f.Owner);
        f.Complete();
        HostCheck.Equal(Block.Status.Cancelled, Poll(f.State, out var done));
        HostCheck.Equal(0U, done.Bytes);
        HostCheck.Equal(Block.Status.StaleToken, Block.Copy(f.State, token, output));
        HostCheck.Equal(Block.Status.StaleToken, Block.Cancel(f.State, token));
        HostCheck.Equal(Block.Status.Success, Submit(f.State, 10, out token));
        f.Complete();
        HostCheck.Equal(Block.Status.Ready, Poll(f.State, out _));
        HostCheck.Equal(Block.Status.Success, Block.Cancel(f.State, token));
        HostCheck.Equal(Block.Status.StaleToken, Block.Copy(f.State, token, output));
        HostCheck.Equal(2UL, f.State->Cancelled);
        HostCheck.Equal(0UL, f.State->Copied);
        HostCheck.True(output.IndexOfAnyExcept((byte)0xa5) < 0);
        HostCheck.Equal(Block.Status.Idle, Poll(f.State, out _));
        HostCheck.Equal(Block.Status.Success, Block.Stop(f.State));
        return true;
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
            using var f = new Fixture();
            HostCheck.Equal(Block.Status.Success, f.Start());
            output.Fill(0xa5);
            HostCheck.Equal(Block.Status.Success, Submit(f.State, 1, out var token));
            f.Complete(item.Id, item.Length, item.DeviceStatus, item.Delta);
            HostCheck.Equal(item.Expected, Poll(f.State, out _));
            HostCheck.Equal(item.Expected, f.State->LastFailure);
            HostCheck.Equal(Block.Status.InvalidState, Block.Copy(f.State, token, output));
            HostCheck.True(output.IndexOfAnyExcept((byte)0xa5) < 0 && f.State->RequiresCleanup);
            HostCheck.Equal(1UL, f.Pool->AllocatedMask);
            HostCheck.Equal(Block.Status.Success, Block.Stop(f.State));
        }
        for (int phase = 0; phase < 2; phase++)
        {
            using var f = new Fixture();
            HostCheck.Equal(Block.Status.Success, f.Start());
            HostCheck.Equal(Block.Status.Success, Submit(f.State, 2, out var token));
            f.Complete();
            HostCheck.Equal(Block.Status.Ready, Poll(f.State, out _));
            if (phase == 1)
                HostCheck.Equal(Block.Status.Success, Block.Copy(f.State, token, output));
            output.Fill(0xa5);
            *(ushort*)(f.Data + 150) = 2;
            HostCheck.Equal(Block.Status.InvalidCompletion, phase == 0 ? Block.Copy(f.State, token, output) : Poll(f.State, out _));
            HostCheck.True(output.IndexOfAnyExcept((byte)0xa5) < 0);
            HostCheck.Equal(Block.Status.Success, Block.Stop(f.State));
        }
        return true;
    }

    [HostTest("block.service capacity and configuration generations fail closed", HostLane.Fast, 4)]
    private static bool Configuration()
    {
        using (var f = new Fixture())
        {
            f.Hardware->Capacity = 0;
            HostCheck.Equal(Block.Status.InvalidCapacity, f.Start());
            HostCheck.True(f.State->Retired);
        }
        using (var f = new Fixture())
        {
            f.Hardware->Capacity++;
            HostCheck.Equal(Block.Status.ConfigurationChanged, f.Start());
            HostCheck.Equal(0UL, f.Pool->AllocatedMask);
        }
        using (var f = new Fixture())
        {
            f.Hardware->GenerationChurn = true;
            HostCheck.Equal(Block.Status.ConfigurationChanged, f.Start());
            HostCheck.Equal(32U, f.Hardware->GenerationReads);
            HostCheck.True(f.State->Retired);
        }
        Span<byte> output = stackalloc byte[512];
        for (int phase = 0; phase < 3; phase++)
        {
            using var f = new Fixture();
            HostCheck.Equal(Block.Status.Success, f.Start());
            output.Fill(0xa5);
            Block.ReadToken token = default;
            if (phase != 0)
            {
                HostCheck.Equal(Block.Status.Success, Submit(f.State, 3, out token));
                f.Complete();
            }
            if (phase == 2)
                HostCheck.Equal(Block.Status.Ready, Poll(f.State, out _));
            f.Hardware->Generation++;
            f.Hardware->Capacity++;
            Block.Status result = phase == 0 ? Submit(f.State, 3, out _) : phase == 1 ? Poll(f.State, out _) : Block.Copy(f.State, token, output);
            HostCheck.Equal(Block.Status.ConfigurationChanged, result);
            HostCheck.True(output.IndexOfAnyExcept((byte)0xa5) < 0);
            HostCheck.Equal(Block.Status.Success, Block.Stop(f.State));
        }
        return true;
    }

    [HostTest("block.service original deadline and completion priority remain bounded", HostLane.Fast, 5)]
    private static bool Deadlines()
    {
        using (var f = new Fixture())
        {
            HostCheck.Equal(Block.Status.Success, f.Start());
            HostCheck.Equal(Block.Status.Success, Submit(f.State, 1, out _));
            for (ulong i = 1; i < 10; i++)
            {
                f.Hardware->Now = i * 100;
                HostCheck.Equal(Block.Status.Pending, Poll(f.State, out _));
            }
            f.Hardware->Now = 1000;
            HostCheck.Equal(Block.Status.Timeout, Poll(f.State, out _));
            HostCheck.Equal(1UL, f.Pool->AllocatedMask);
            HostCheck.Equal(Block.Status.Success, Block.Stop(f.State));
        }
        using (var f = new Fixture())
        {
            HostCheck.Equal(Block.Status.Success, f.Start());
            HostCheck.Equal(Block.Status.Success, Submit(f.State, 1, out var token));
            f.Hardware->Now = 10000;
            f.Complete();
            HostCheck.Equal(Block.Status.Ready, Poll(f.State, out _));
            Span<byte> output = stackalloc byte[512];
            HostCheck.Equal(Block.Status.Success, Block.Copy(f.State, token, output));
            f.Hardware->Now = 100000000;
            HostCheck.Equal(Block.Status.Idle, Poll(f.State, out _));
            HostCheck.Equal(Block.Status.Success, Block.Stop(f.State));
        }
        using (var f = new Fixture())
        {
            HostCheck.Equal(Block.Status.Success, f.Start());
            HostCheck.Equal(Block.Status.Success, Submit(f.State, 1, out var token));
            HostCheck.Equal(Block.Status.Success, Block.Cancel(f.State, token));
            f.Hardware->Now = 1000;
            HostCheck.Equal(Block.Status.Timeout, Poll(f.State, out _));
            HostCheck.Equal(Block.Status.Success, Block.Stop(f.State));
        }
        return true;
    }

    [HostTest("block.service clock failure regression overflow and idle failure retain DMA", HostLane.Fast, 6)]
    private static bool Clocks()
    {
        for (int mode = 0; mode < 4; mode++)
        {
            using var f = new Fixture();
            f.Hardware->Now = mode == 2 ? ulong.MaxValue - 1 : 10;
            HostCheck.Equal(Block.Status.Success, f.Start());
            if (mode == 0 || mode == 3)
                f.Hardware->ClockFailure = true;
            if (mode == 1)
                f.Hardware->Now = 9;
            HostCheck.Equal(mode == 1 ? Block.Status.ClockRegressed : Block.Status.ClockFailed,
                mode == 3 ? Poll(f.State, out _) : Submit(f.State, 1, out _));
            HostCheck.True(f.State->RequiresCleanup && !f.State->Running);
            HostCheck.Equal(1UL, f.Pool->AllocatedMask);
            HostCheck.Equal(Block.Status.Success, Block.Stop(f.State));
        }
        return true;
    }

    [HostTest("block.service outstanding shutdown quarantines until fresh reset disable and fence", HostLane.Fast, 7)]
    private static bool QuarantinedReset()
    {
        using var f = new Fixture();
        HostCheck.Equal(Block.Status.Success, f.Start());
        HostCheck.Equal(Block.Status.Success, Submit(f.State, 55, out var token));
        Span<byte> output = stackalloc byte[512];
        output.Fill(0xa5);
        f.Hardware->ResetStuck = true;
        HostCheck.Equal(Block.Status.ResetFailed, Block.Stop(f.State));
        HostCheck.Equal(Block.Status.ResetFailed, f.State->LastFailure);
        HostCheck.Equal(3U, f.State->Receipt.ResetReads);
        HostCheck.True(!f.State->Receipt.ResetComplete);
        f.Hardware->ResetStuck = false;
        f.Hardware->DisableFailure = true;
        HostCheck.Equal(Block.Status.ResetFailed, Block.Stop(f.State));
        HostCheck.True(f.State->Receipt.ResetComplete && !f.State->Receipt.BusMasterDisabled);
        f.Hardware->DisableFailure = false;
        f.Hardware->ResetWriteFailure = true;
        HostCheck.Equal(Block.Status.ResetFailed, Block.Stop(f.State));
        HostCheck.True(!f.State->Receipt.ResetComplete && f.State->Receipt.BusMasterDisabled);
        f.Hardware->ResetWriteFailure = false;
        f.Hardware->ResetFenceFailure = true;
        HostCheck.Equal(Block.Status.ResetFailed, Block.Stop(f.State));
        HostCheck.Equal(VirtioBlkQueueStatus.OrderingFailed, f.State->Receipt.CleanupStatus);
        HostCheck.True(f.State->RequiresCleanup && !f.State->Running && *(ulong*)f.Data != 0);
        HostCheck.Equal(1UL, f.Pool->AllocatedMask);
        HostCheck.Equal(DmaBufferOwnership.Device, f.Owner);
        HostCheck.Equal(Block.Status.InvalidState, Block.Copy(f.State, token, output));
        HostCheck.True(output.IndexOfAnyExcept((byte)0xa5) < 0);
        f.Hardware->ResetFenceFailure = false;
        HostCheck.Equal(Block.Status.Success, Block.Stop(f.State));
        HostCheck.True(f.State->Retired && f.Hardware->HeldAtResetFence);
        HostCheck.Equal(0UL, f.Pool->AllocatedMask);
        HostCheck.True(new ReadOnlySpan<byte>(f.Data, 4096).IndexOfAnyExcept((byte)0) < 0);
        uint before = f.Hardware->Calls;
        HostCheck.Equal(Block.Status.Success, Block.Stop(f.State));
        HostCheck.Equal(Block.Status.InvalidState, Submit(f.State, 1, out _));
        HostCheck.Equal(before, f.Hardware->Calls);
        f.CheckGuards();
        return true;
    }

    [HostTest("block.service invalid admission and metadata aliases have no effects", HostLane.Fast, 8)]
    private static bool Admission()
    {
        using var f = new Fixture();
        var a = f.Input;
        a.NegotiatedFeatures = 0;
        HostCheck.Equal(Block.Status.UnsupportedFeatures, Block.Start(f.State, a));
        a = f.Input;
        a.NegotiatedFeatures |= 1UL << 28;
        HostCheck.Equal(Block.Status.UnsupportedFeatures, Block.Start(f.State, a));
        for (int mode = 0; mode < 12; mode++)
        {
            a = f.Input;
            switch (mode)
            {
                case 0:
                    a.Epoch = 0;
                    break;
                case 1:
                    a.ExpectedCapacity = 0;
                    break;
                case 2:
                    a.ResetLimit = 0;
                    break;
                case 3:
                    a.ResetLimit = 1025;
                    break;
                case 4:
                    a.CompletionTimeoutNanos = 0;
                    break;
                case 5:
                    a.CompletionTimeoutNanos = 60000000001;
                    break;
                case 6:
                    a.Clock.Read = null;
                    break;
                case 7:
                    a.NotifyMultiplier = 3;
                    break;
                case 8:
                    a.Notify = a.Common;
                    break;
                case 9:
                    a.Access.BusMasterOn = null;
                    break;
                case 10:
                    a.Access.PrepareInterrupt = &Interrupt;
                    break;
                case 11:
                    a.Common = ChangedRegion(a.Common, "VirtualStart", (ulong)f.Pool);
                    break;
            }
            HostCheck.Equal(Block.Status.InvalidAccess, Block.Start(f.State, a));
        }
        HostCheck.Equal(Block.Status.InvalidAccess, Block.Start((Block.State*)f.Data, f.Input));
        a = f.Input;
        a.Device = ChangedRegion(a.Device, "PhysicalStart", Physical);
        HostCheck.Equal(Block.Status.InvalidAccess, Block.Start(f.State, a));
        HostCheck.Equal(0U, f.Hardware->Calls);
        HostCheck.Equal(0UL, f.Pool->AllocatedMask);
        HostCheck.True(new ReadOnlySpan<byte>(f.Data, 4096).IndexOfAnyExcept((byte)0xa5) < 0);
        HostCheck.Equal(Block.Status.Success, f.Start());
        HostCheck.Equal(Block.Status.InvalidAccess, f.Start());
        HostCheck.Equal(Block.Status.Success, Block.Stop(f.State));
        return true;
    }

    [HostTest("block.service nonwrapping request generation and range reject before publication", HostLane.Fast, 9)]
    private static bool GenerationAndRange()
    {
        using var f = new Fixture();
        HostCheck.Equal(Block.Status.Success, f.Start());
        uint before = f.Hardware->Calls;
        HostCheck.Equal(Block.Status.SectorOutOfRange, Submit(f.State, Capacity, out var absent));
        HostCheck.True(!absent.IsInitialized);
        // Host-only boundary setup avoids an impractical 2^64 request loop.
        ulong* generation = (ulong*)((byte*)f.State + (int)Marshal.OffsetOf<Block.State>("RequestGeneration"));
        *generation = ulong.MaxValue;
        HostCheck.Equal(Block.Status.GenerationExhausted, Submit(f.State, 0, out absent));
        HostCheck.True(!absent.IsInitialized);
        HostCheck.Equal(before, f.Hardware->Calls);
        HostCheck.Equal(0UL, f.State->Submitted);
        (*generation)--;
        HostCheck.Equal(Block.Status.Success, Submit(f.State, Capacity - 1, out var last));
        f.Complete();
        HostCheck.Equal(Block.Status.Ready, Poll(f.State, out _));
        Span<byte> output = stackalloc byte[512];
        HostCheck.Equal(Block.Status.Success, Block.Copy(f.State, last, output));
        CheckPayload(output, Capacity - 1);
        HostCheck.Equal(Block.Status.GenerationExhausted, Submit(f.State, 0, out _));
        HostCheck.Equal(Block.Status.Success, Block.Stop(f.State));
        return true;
    }

    [HostTest("block.service callback failures preserve original setup and retained request ownership", HostLane.Fast, 10)]
    private static bool CallbackFailures()
    {
        Span<byte> output = stackalloc byte[512];
        uint setupCalls;
        using (var f = new Fixture())
        {
            HostCheck.Equal(Block.Status.Success, f.Start());
            setupCalls = f.Hardware->Calls;
            HostCheck.Equal(Block.Status.Success, Block.Stop(f.State));
        }
        for (uint call = 1; call <= setupCalls; call++)
        {
            using var f = new Fixture();
            f.Hardware->FailCall = call;
            f.Hardware->FailAfterWrite = true;
            var result = f.Start();
            HostCheck.True(result != Block.Status.Success && f.Hardware->FailureFired);
            HostCheck.Equal(result, f.State->LastFailure);
            HostCheck.True(f.State->Retired);
            HostCheck.Equal(0UL, f.Pool->AllocatedMask);
        }
        for (int operation = 0; operation < 3; operation++)
        {
            uint calls;
            using (var f = new Fixture())
            {
                HostCheck.Equal(Block.Status.Success, f.Start());
                Block.ReadToken token = default;
                if (operation > 0)
                {
                    HostCheck.Equal(Block.Status.Success, Submit(f.State, 1, out token));
                    f.Complete();
                }
                if (operation == 2)
                    HostCheck.Equal(Block.Status.Ready, Poll(f.State, out _));
                uint start = f.Hardware->Calls;
                _ = operation == 0 ? Submit(f.State, 1, out _) : operation == 1 ? Poll(f.State, out _) : Block.Copy(f.State, token, output);
                calls = f.Hardware->Calls - start;
                HostCheck.Equal(Block.Status.Success, Block.Stop(f.State));
            }
            for (uint call = 1; call <= calls; call++)
            {
                using var f = new Fixture();
                HostCheck.Equal(Block.Status.Success, f.Start());
                Block.ReadToken token = default;
                output.Fill(0xa5);
                if (operation > 0)
                {
                    HostCheck.Equal(Block.Status.Success, Submit(f.State, 1, out token));
                    f.Complete();
                }
                if (operation == 2)
                    HostCheck.Equal(Block.Status.Ready, Poll(f.State, out _));
                f.Hardware->FailCall = f.Hardware->Calls + call;
                f.Hardware->FailAfterWrite = true;
                var result = operation == 0 ? Submit(f.State, 1, out _) : operation == 1 ? Poll(f.State, out _) : Block.Copy(f.State, token, output);
                HostCheck.True(f.Hardware->FailureFired && f.State->RequiresCleanup && !f.State->Running);
                HostCheck.Equal(result, f.State->LastFailure);
                HostCheck.Equal(1UL, f.Pool->AllocatedMask);
                HostCheck.True(output.IndexOfAnyExcept((byte)0xa5) < 0);
                HostCheck.Equal(Block.Status.Success, Block.Stop(f.State));
            }
        }
        return true;
    }

    [HostTest("block.service exhausted pool preserves existing allocation and bytes", HostLane.Fast, 11)]
    private static bool ExhaustedPool()
    {
        using var f = new Fixture();
        HostCheck.Equal(DmaBufferStatus.Success, PageAlignedDmaBuffers.TryAllocate(f.Pool, 1, out var held));
        HostCheck.Equal(Block.Status.SetupFailed, f.Start());
        HostCheck.Equal(VirtioBlkQueueStatus.DmaRejected, f.State->Receipt.Status);
        HostCheck.True(f.State->Retired);
        HostCheck.Equal(1UL, f.Pool->AllocatedMask);
        HostCheck.True(new ReadOnlySpan<byte>(f.Data, 4096).IndexOfAnyExcept((byte)0xa5) < 0);
        HostCheck.Equal(DmaBufferStatus.Success, PageAlignedDmaBuffers.TryRelease(f.Pool, held.Token));
        return true;
    }

    [HostTest("block.service warmed submit poll copy has no managed allocation", HostLane.Fast, 12)]
    private static bool NoAllocation()
    {
        using var f = new Fixture();
        HostCheck.Equal(Block.Status.Success, f.Start());
        Span<byte> output = stackalloc byte[512];
        bool good = true;
        long before = 0;
        for (uint i = 0; i < 1001; i++)
        {
            if (i == 1)
                before = GC.GetAllocatedBytesForCurrentThread();
            good &= Submit(f.State, i, out var token) == Block.Status.Success;
            f.Complete();
            good &= Poll(f.State, out _) == Block.Status.Ready;
            good &= Block.Copy(f.State, token, output) == Block.Status.Success;
        }
        long after = GC.GetAllocatedBytesForCurrentThread();
        HostCheck.True(good && !f.Hardware->BadOrder);
        HostCheck.Equal(before, after);
        HostCheck.Equal(Block.Status.Success, Block.Stop(f.State));
        return true;
    }

    [HostTest("block.service metadata output aliases cannot damage an active owner or DMA", HostLane.Fast, 13)]
    private static bool MetadataAliases()
    {
        using var f = new Fixture();
        HostCheck.Equal(Block.Status.Success, f.Start());
        HostCheck.Equal(Block.Status.Success, Submit(f.State, 27, out var token));
        byte[] state = new ReadOnlySpan<byte>(f.State, sizeof(Block.State)).ToArray();
        byte[] pool = new ReadOnlySpan<byte>(f.Pool, sizeof(DmaBufferPool)).ToArray();
        byte[] slot = new ReadOnlySpan<byte>(f.Slot, sizeof(DmaBufferSlot)).ToArray();
        byte[] dma = new ReadOnlySpan<byte>(f.Data, 4096).ToArray();
        byte* ordinary = stackalloc byte[128];
        new Span<byte>(ordinary, 128).Fill(0xa5);
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
        HostCheck.Equal(calls, f.Hardware->Calls);
        HostCheck.Equal(1UL, f.Pool->AllocatedMask);
        HostCheck.Equal(DmaBufferOwnership.Device, f.Owner);
        HostCheck.True(new ReadOnlySpan<byte>(ordinary, 128).IndexOfAnyExcept((byte)0xa5) < 0);
        f.Complete();
        HostCheck.Equal(Block.Status.Ready, Poll(f.State, out _));
        Span<byte> output = stackalloc byte[512];
        HostCheck.Equal(Block.Status.Success, Block.Copy(f.State, token, output));
        CheckPayload(output, 27);
        HostCheck.Equal(Block.Status.Success, Block.Stop(f.State));
        HostCheck.Equal(0UL, f.Pool->AllocatedMask);
        f.CheckGuards();
        // Invalid/copy/retired owners must not clear even an ordinary output.
        HostCheck.Equal(Block.Status.InvalidState, Block.Submit(f.State, 1, (Block.ReadToken*)ordinary));
        HostCheck.Equal(Block.Status.InvalidState, Block.Poll(f.State, (Block.Completion*)ordinary));
        HostCheck.True(new ReadOnlySpan<byte>(ordinary, 128).IndexOfAnyExcept((byte)0xa5) < 0);
        return true;
    }

    [HostTest("block.service observer captures exact request completion and copy candidates", HostLane.Fast, 14)]
    private static bool Observations()
    {
        using var f = new Fixture();
        using var capture = new Capture(f);
        HostCheck.Equal(Block.Status.Success, f.Start());
        HostCheck.Equal(0U, capture.Sink->Count);
        Span<byte> output = new(capture.Sink->Output, 512);
        foreach (ulong sector in new ulong[] { 7, 0x100000001 })
        {
            output.Fill(0xa5);
            uint first = capture.Sink->Count;
            HostCheck.Equal(Block.Status.Success, Submit(f.State, sector, out var token));
            HostCheck.Equal(Block.Status.Pending, Poll(f.State, out _));
            HostCheck.Equal(first + 1, capture.Sink->Count);
            f.Complete();
            HostCheck.Equal(Block.Status.Ready, Poll(f.State, out _));
            HostCheck.Equal(Block.Status.Ready, Poll(f.State, out _));
            HostCheck.Equal(first + 2, capture.Sink->Count);
            HostCheck.Equal(Block.Status.ShortBuffer, Block.Copy(f.State, token, output[..511]));
            HostCheck.Equal(Block.Status.InvalidStorage, Block.Copy(f.State, token, new Span<byte>(f.Data, 512)));
            HostCheck.Equal(Block.Status.StaleToken, Block.Copy(f.State, CorruptToken(token, "Generation"), output));
            HostCheck.Equal(first + 2, capture.Sink->Count);
            HostCheck.Equal(Block.Status.Success, Block.Copy(f.State, token, output));
            var submitted = capture.Sink->Records[first % Capture.RecordCount];
            var completed = capture.Sink->Records[(first + 1) % Capture.RecordCount];
            var candidate = capture.Sink->Records[(first + 2) % Capture.RecordCount];
            HostCheck.Equal(Block.ObservationKind.Submitted, submitted.Kind);
            HostCheck.Equal(Block.ObservationKind.CompletionValidated, completed.Kind);
            HostCheck.Equal(Block.ObservationKind.CopyReady, candidate.Kind);
            HostCheck.True(submitted.Owner == (ulong)f.State && submitted.Epoch == f.Input.Epoch && submitted.Generation != 0 &&
                submitted.Generation == completed.Generation && submitted.Generation == candidate.Generation &&
                submitted.Sector == sector && completed.Sector == sector && candidate.Sector == sector);
            HostCheck.True(submitted.Sequence == f.State->Submitted && completed.Sequence == f.State->Completed &&
                candidate.Sequence == f.State->Copied && candidate.AvailableIndex == f.State->PublishedIndex &&
                candidate.ConsumedIndex == f.State->ConsumedIndex);
            CheckPayload(output, sector);
            CheckPayload(new ReadOnlySpan<byte>(capture.Sink->Payload, 512), sector);
            HostCheck.True(!capture.Sink->Bad && !f.State->ReadReady);
        }
        uint events = capture.Sink->Count;
        HostCheck.Equal(Block.Status.Success, Block.Stop(f.State));
        HostCheck.Equal(events, capture.Sink->Count);
        f.CheckGuards();
        return true;
    }

    [HostTest("block.service observer rejection preserves outputs and quarantines every stage", HostLane.Fast, 15)]
    private static bool ObservationRejections()
    {
        foreach (var kind in new[] { Block.ObservationKind.Submitted, Block.ObservationKind.CompletionValidated, Block.ObservationKind.CopyReady })
            foreach (byte rejected in new byte[] { 0, 2, 255 })
            {
                using var f = new Fixture();
                using var capture = new Capture(f);
                capture.Sink->Reject = true;
                capture.Sink->RejectKind = kind;
                capture.Sink->RejectValue = rejected;
                HostCheck.Equal(Block.Status.Success, f.Start());
                Span<byte> output = new(capture.Sink->Output, 512);
                output.Fill(0xa5);
                Block.ReadToken token = default;
                if (kind == Block.ObservationKind.Submitted)
                {
                    HostCheck.Equal(Block.Status.ObservationRejected, Submit(f.State, 31, out token));
                    HostCheck.True(!token.IsInitialized);
                }
                else
                {
                    HostCheck.Equal(Block.Status.Success, Submit(f.State, 31, out token));
                    f.Complete();
                    if (kind == Block.ObservationKind.CompletionValidated)
                    {
                        HostCheck.Equal(Block.Status.ObservationRejected, Poll(f.State, out var result));
                        HostCheck.True(!result.Token.IsInitialized && result.Bytes == 0);
                    }
                    else
                    {
                        HostCheck.Equal(Block.Status.Ready, Poll(f.State, out _));
                        HostCheck.Equal(Block.Status.ObservationRejected, Block.Copy(f.State, token, output));
                    }
                }
                HostCheck.True(!f.State->Running && f.State->RequiresCleanup && !capture.Sink->Bad);
                HostCheck.Equal(Block.Status.ObservationRejected, f.State->LastFailure);
                HostCheck.Equal(0UL, f.State->Copied);
                HostCheck.True(output.IndexOfAnyExcept((byte)0xa5) < 0 && f.Pool->AllocatedMask == 1 && f.Owner == DmaBufferOwnership.Device);
                HostCheck.Equal(1U, f.Hardware->Notifies);
                uint events = capture.Sink->Count;
                if (kind == Block.ObservationKind.CopyReady)
                {
                    f.Hardware->ResetStuck = true;
                    HostCheck.Equal(Block.Status.ResetFailed, Block.Stop(f.State));
                    HostCheck.Equal(1UL, f.Pool->AllocatedMask);
                    f.Hardware->ResetStuck = false;
                }
                HostCheck.Equal(Block.Status.Success, Block.Stop(f.State));
                HostCheck.Equal(events, capture.Sink->Count);
                HostCheck.Equal(Block.Status.ObservationRejected, f.State->LastFailure);
                HostCheck.Equal(0UL, f.Pool->AllocatedMask);
                f.CheckGuards();
            }
        return true;
    }

    [HostTest("block.service observer reentry rejects all mutators before publication or cleanup", HostLane.Fast, 16)]
    private static bool ObservationReentry()
    {
        foreach (var kind in new[] { Block.ObservationKind.Submitted, Block.ObservationKind.CompletionValidated, Block.ObservationKind.CopyReady })
            for (int operation = 0; operation < 6; operation++)
            {
                using var f = new Fixture();
                using var capture = new Capture(f);
                capture.Sink->ReenterKind = kind;
                capture.Sink->ReenterOperation = operation;
                HostCheck.Equal(Block.Status.Success, f.Start());
                Span<byte> output = new(capture.Sink->Output, 512);
                output.Fill(0xa5);
                Block.Status result = Submit(f.State, 41, out var token);
                if (kind != Block.ObservationKind.Submitted)
                {
                    HostCheck.Equal(Block.Status.Success, result);
                    f.Complete();
                    result = Poll(f.State, out _);
                    if (kind == Block.ObservationKind.CopyReady)
                    {
                        HostCheck.Equal(Block.Status.Ready, result);
                        result = Block.Copy(f.State, token, output);
                    }
                }
                HostCheck.Equal(Block.Status.ObservationRejected, result);
                HostCheck.Equal(Block.Status.ObservationRejected, capture.Sink->NestedStatus);
                HostCheck.True(capture.Sink->NestedUnchanged && !capture.Sink->Bad && !f.State->Running && f.State->RequiresCleanup);
                HostCheck.True(output.IndexOfAnyExcept((byte)0xa5) < 0 && f.Pool->AllocatedMask == 1);
                HostCheck.Equal(1U, f.Hardware->Notifies);
                HostCheck.Equal(0U, f.Hardware->Resets);
                uint events = capture.Sink->Count;
                HostCheck.Equal(Block.Status.Success, Block.Stop(f.State));
                HostCheck.Equal(events, capture.Sink->Count);
                HostCheck.Equal(0UL, f.Pool->AllocatedMask);
                f.CheckGuards();
            }
        return true;
    }

    [HostTest("block.service observer excludes invalid and cancelled copies and duplicate readiness", HostLane.Fast, 17)]
    private static bool ObservationCancellationAndFailures()
    {
        using (var f = new Fixture())
        using (var capture = new Capture(f))
        {
            HostCheck.Equal(Block.Status.Success, f.Start());
            HostCheck.Equal(Block.Status.Success, Submit(f.State, 8, out var token));
            HostCheck.Equal(Block.Status.Success, Block.Cancel(f.State, token));
            f.Complete();
            HostCheck.Equal(Block.Status.Cancelled, Poll(f.State, out _));
            HostCheck.Equal(2U, capture.Sink->Count);
            HostCheck.Equal(0U, capture.Sink->Records[1].Bytes);
            HostCheck.Equal(0UL, capture.Sink->Candidate);
            HostCheck.Equal(Block.Status.Success, Submit(f.State, 9, out token));
            f.Complete();
            HostCheck.Equal(Block.Status.Ready, Poll(f.State, out _));
            HostCheck.Equal(Block.Status.Success, Block.Cancel(f.State, token));
            HostCheck.Equal(Block.Status.StaleToken, Block.Copy(f.State, token, new Span<byte>(capture.Sink->Output, 512)));
            HostCheck.Equal(4U, capture.Sink->Count);
            HostCheck.Equal(0UL, capture.Sink->Candidate);
            HostCheck.True(!capture.Sink->Bad);
            HostCheck.Equal(Block.Status.Success, Block.Stop(f.State));
        }
        for (int mode = 0; mode < 4; mode++)
        {
            using var f = new Fixture();
            using var capture = new Capture(f);
            HostCheck.Equal(Block.Status.Success, f.Start());
            if (mode == 0)
            {
                // Seven hardware calls precede/perform notification on this fixture.
                f.Hardware->FailCall = f.Hardware->Calls + 7;
                f.Hardware->FailAfterWrite = true;
                HostCheck.Equal(Block.Status.WriteFailed, Submit(f.State, 10, out _));
                HostCheck.Equal(0U, capture.Sink->Count);
            }
            else
            {
                HostCheck.Equal(Block.Status.Success, Submit(f.State, 10, out var token));
                f.Complete(length: mode == 1 ? 512U : 513U);
                if (mode == 1)
                    HostCheck.Equal(Block.Status.InvalidCompletion, Poll(f.State, out _));
                else if (mode == 2)
                {
                    f.Hardware->ClockFailure = true;
                    HostCheck.Equal(Block.Status.ClockFailed, Poll(f.State, out _));
                }
                else
                {
                    HostCheck.Equal(Block.Status.Ready, Poll(f.State, out _));
                    f.Hardware->Generation++;
                    HostCheck.Equal(Block.Status.ConfigurationChanged, Block.Copy(f.State, token, new Span<byte>(capture.Sink->Output, 512)));
                }
                HostCheck.Equal(mode == 3 ? 2U : 1U, capture.Sink->Count);
                HostCheck.Equal(0UL, capture.Sink->Candidate);
            }
            HostCheck.True(!capture.Sink->Bad);
            HostCheck.Equal(Block.Status.Success, Block.Stop(f.State));
        }
        return true;
    }

    [HostTest("block.service observed requests retain generation across ushort ring wrap without allocations", HostLane.Fast, 18)]
    private static bool ObservationWrapAndAllocation()
    {
        using var f = new Fixture();
        using var capture = new Capture(f);
        HostCheck.Equal(Block.Status.Success, f.Start());
        Span<byte> output = new(capture.Sink->Output, 512);
        long before = 0;
        bool good = true;
        for (uint i = 0; i < 65538; i++)
        {
            if (i == 1)
                before = GC.GetAllocatedBytesForCurrentThread();
            output.Fill(0xa5);
            good &= Submit(f.State, i, out var token) == Block.Status.Success;
            f.Complete();
            good &= Poll(f.State, out _) == Block.Status.Ready && Block.Copy(f.State, token, output) == Block.Status.Success;
            good &= capture.Sink->Candidate == f.State->Copied && capture.Sink->LastGeneration == (ulong)i + 1;
        }
        long after = GC.GetAllocatedBytesForCurrentThread();
        HostCheck.Equal(before, after);
        HostCheck.True(good && !capture.Sink->Bad);
        HostCheck.Equal(196614U, capture.Sink->Count);
        HostCheck.Equal((ushort)2, f.State->PublishedIndex);
        HostCheck.Equal((ushort)2, f.State->ConsumedIndex);
        HostCheck.Equal(Block.Status.Success, Block.Stop(f.State));
        f.CheckGuards();
        return true;
    }

    [UnmanagedCallersOnly]
    private static byte CaptureObservation(void* context, Block.Observation* value, byte* payload)
    {
        var sink = (ObservationSink*)context;
        Block.State* state = sink->State;
        Hardware* hardware = sink->Hardware;
        sink->Records[sink->Count++ % Capture.RecordCount] = *value;
        sink->LastGeneration = value->Generation;
        sink->Bad |= value->Owner != (ulong)state || value->Epoch != sink->Input.Epoch || value->Generation == 0 ||
            value->AvailableIndex != state->PublishedIndex || value->ConsumedIndex != state->ConsumedIndex;
        if (value->Kind == Block.ObservationKind.Submitted)
            sink->Bad |= payload != null || value->Bytes != sink->ExpectedBytes || value->Sequence != state->Submitted ||
                !state->ReadPending || hardware->Notifies != state->Submitted;
        else if (value->Kind == Block.ObservationKind.CompletionValidated)
            sink->Bad |= payload != null || (value->Bytes != 0 && value->Bytes != sink->ExpectedBytes) || value->Sequence != state->Completed || !state->ReadPending;
        else if (value->Kind == Block.ObservationKind.CopyReady)
        {
            sink->Bad |= payload != hardware->Data + 512 || value->Bytes != sink->ExpectedBytes || value->Sequence != state->Copied + 1 || !state->ReadReady;
            sink->Candidate = value->Sequence;
            if (payload != null)
                for (int i = 0; i < sink->ExpectedBytes; i++)
                {
                    sink->Payload[i] = payload[i];
                    sink->Bad |= payload[i] != Payload(value->Sector, i) || sink->Output[i] != 0xa5;
                }
        }
        else
            sink->Bad = true;
        if (sink->ReenterOperation >= 0 && value->Kind == sink->ReenterKind)
        {
            uint calls = hardware->Calls;
            ulong submitted = state->Submitted, completed = state->Completed, copied = state->Copied;
            new Span<byte>(sink->Metadata, 64).Fill(0xa5);
            sink->NestedStatus = sink->ReenterOperation switch
            {
                0 => Block.Start(state, sink->Input),
                1 => Block.Submit(state, 22, (Block.ReadToken*)sink->Metadata),
                2 => Block.Poll(state, (Block.Completion*)sink->Metadata),
                3 => Block.Copy(state, default, new Span<byte>(sink->Output, 512)),
                4 => Block.Cancel(state, default),
                6 => Block.Submit(state, 22, 4, (Block.ReadToken*)sink->Metadata),
                _ => Block.Stop(state)
            };
            sink->NestedUnchanged = calls == hardware->Calls && submitted == state->Submitted && completed == state->Completed && copied == state->Copied &&
                new ReadOnlySpan<byte>(sink->Metadata, 64).IndexOfAnyExcept((byte)0xa5) < 0 &&
                new ReadOnlySpan<byte>(sink->Output, 512).IndexOfAnyExcept((byte)0xa5) < 0 && hardware->Pool->AllocatedMask == 1;
        }
        return sink->Reject && value->Kind == sink->RejectKind ? sink->RejectValue : (byte)1;
    }

    // Test-only value conveniences use distinct stack storage. Production APIs
    // validate explicit metadata pointers before their first output write.
    private static Block.Status Submit(Block.State* state, ulong sector, out Block.ReadToken token)
    {
        Block.ReadToken value = default;
        var status = Block.Submit(state, sector, &value);
        token = value;
        return status;
    }
    private static Block.Status Submit(Block.State* state, ulong sector, uint sectors, out Block.ReadToken token)
    {
        Block.ReadToken value = default;
        var status = Block.Submit(state, sector, sectors, &value);
        token = value;
        return status;
    }
    private static Block.Status Poll(Block.State* state, out Block.Completion completion)
    {
        Block.Completion value = default;
        var status = Block.Poll(state, &value);
        completion = value;
        return status;
    }

    private const ulong Common = 0x400000, Notify = 0x403000, Config = 0x402000,
        Physical = 0x220000, DeviceBase = 0x123456780000, Capacity = 0x100000010;
    private static byte Payload(ulong sector, int index) => unchecked((byte)((sector * 13) ^ ((ulong)index * 17) ^ (sector >> 8)));
    private static void CheckPayload(ReadOnlySpan<byte> bytes, ulong sector)
    {
        bool good = true;
        for (int i = 0; i < 512; i++)
            good &= bytes[i] == Payload(sector, i);
        HostCheck.True(good);
    }
    private static Block.ReadToken CorruptToken(Block.ReadToken token, string field)
    {
        *(ulong*)((byte*)&token + (int)Marshal.OffsetOf<Block.ReadToken>(field)) ^= 1;
        return token;
    }
    private static VirtioMappedRegion ChangedRegion(VirtioMappedRegion region, string field, ulong value)
    {
        *(ulong*)((byte*)&region + (int)Marshal.OffsetOf<VirtioMappedRegion>(field)) = value;
        return region;
    }

    private static bool Enter(Hardware* h, bool write = false)
    {
        h->Calls++;
        if (h->Calls != h->FailCall)
            return true;
        h->FailureFired = true;
        return write && h->FailAfterWrite;
    }
    private static byte Exit(Hardware* h) => h->Calls == h->FailCall ? (byte)0 : (byte)1;
    [UnmanagedCallersOnly] private static byte Interrupt(void* c, VirtioMappedRegion common) => 0;
    [UnmanagedCallersOnly]
    private static byte Read8(void* c, ulong address, byte* value)
    {
        var h = (Hardware*)c;
        if (!Enter(h))
            return 0;
        if (address == Common + 20)
        {
            *value = h->Status;
            if (h->ResetRequested && *value == 0)
                h->ResetObserved = true;
        }
        else if (address == Common + 21)
        {
            h->GenerationReads++;
            *value = h->GenerationChurn ? (byte)h->GenerationReads : h->Generation;
        }
        else if (address == (ulong)h->Data + 272)
        {
            if (!h->Acquired)
                h->BadOrder = true;
            *value = h->Data[272];
        }
        else
            return 0;
        return 1;
    }
    [UnmanagedCallersOnly]
    private static byte Write8(void* c, ulong address, byte value)
    {
        var h = (Hardware*)c;
        if (!Enter(h, true) || address != Common + 20)
            return 0;
        if (value == 15)
        {
            if (!h->BusEnabled || h->Enabled != 1 || h->ConfigVector != ushort.MaxValue || h->QueueVector != ushort.MaxValue)
                h->BadOrder = true;
            h->Status = 15;
            h->DriverOkWrites++;
        }
        else if (value == 0)
        {
            h->ResetRequested = true;
            h->ResetObserved = false;
            if (h->ResetWriteFailure)
                return 0;
            h->Resets++;
            if (!h->ResetStuck)
            {
                h->Status = 0;
                h->Enabled = 0;
            }
        }
        else
            return 0;
        return Exit(h);
    }
    [UnmanagedCallersOnly]
    private static byte Read16(void* c, ulong address, ushort* value)
    {
        var h = (Hardware*)c;
        if (!Enter(h))
            return 0;
        if (address == (ulong)h->Data + 150)
        {
            *value = *(ushort*)(h->Data + 150);
            h->IndexObserved = true;
            h->Acquired = false;
            return 1;
        }
        switch (address - Common)
        {
            case 16:
                *value = h->ConfigVector;
                break;
            case 18:
                *value = 1;
                break;
            case 22:
                *value = h->Selected;
                break;
            case 24:
                *value = h->Size == 0 ? (ushort)16 : h->Size;
                break;
            case 26:
                *value = h->QueueVector;
                break;
            case 28:
                *value = h->Enabled;
                break;
            case 30:
                *value = 3;
                break;
            default:
                return 0;
        }
        return 1;
    }
    [UnmanagedCallersOnly]
    private static byte Write16(void* c, ulong address, uint value)
    {
        var h = (Hardware*)c;
        if (!Enter(h, true))
            return 0;
        if (address == Notify + 12 && value == 0)
        {
            ushort index = *(ushort*)(h->Data + 130);
            if (!h->BusEnabled || h->Status != 15 || h->Enabled != 1 || h->Pending ||
                h->Pool->AllocatedMask != 1 || index != unchecked((ushort)(h->AvailableSeen + 1)) || h->FencedIndex != index ||
                *(ushort*)(h->Data + 132 + (h->AvailableSeen & 7) * 2) != 0 || *(ushort*)(h->Data + 128) != 1)
                h->BadOrder = true;
            for (int i = 0; i < 3; i++)
            {
                byte* p = h->Data + i * 16;
                if (*(ulong*)p != DeviceBase + (i == 0 ? 256UL : i == 1 ? 512UL : 272UL) ||
                    *(uint*)(p + 8) != (i == 0 ? 16U : i == 1 ? h->ExpectedBytes : 1U) ||
                    *(ushort*)(p + 12) != (i == 0 ? 1 : i == 1 ? 3 : 2) || *(ushort*)(p + 14) != (i == 0 ? 1 : i == 1 ? 2 : 0))
                    h->BadOrder = true;
            }
            if (*(ulong*)(h->Data + 256) != 0 || h->Data[272] != 255 || new ReadOnlySpan<byte>(h->Data + 512, (int)h->ExpectedBytes).IndexOfAnyExcept((byte)0) >= 0)
                h->BadOrder = true;
            h->Sector = *(ulong*)(h->Data + 264);
            h->AvailableSeen = index;
            h->Notifies++;
            h->Pending = true;
            return Exit(h);
        }
        switch (address - Common)
        {
            case 16:
                h->ConfigVector = (ushort)value;
                break;
            case 22:
                h->Selected = (ushort)value;
                break;
            case 24:
                h->Size = (ushort)value;
                break;
            case 26:
                h->QueueVector = (ushort)value;
                break;
            case 28:
                if (h->BusEnabled || h->Descriptor != DeviceBase || h->Available != DeviceBase + 128 || h->Used != DeviceBase + 148)
                    h->BadOrder = true;
                h->Enabled = (ushort)value;
                break;
            default:
                return 0;
        }
        return Exit(h);
    }
    [UnmanagedCallersOnly]
    private static byte Read32(void* c, ulong address, uint* value)
    {
        var h = (Hardware*)c;
        if (!Enter(h))
            return 0;
        if (address == Config)
        {
            *value = (uint)h->Capacity;
            return 1;
        }
        if (address == Config + 4)
        {
            *value = (uint)(h->Capacity >> 32);
            return 1;
        }
        if (address >= (ulong)h->Data + 152 && address < (ulong)h->Data + 216 && (address & 3) == 0)
        {
            if (!h->Acquired)
                h->BadOrder = true;
            *value = *(uint*)address;
            return 1;
        }
        ulong offset = address - Common;
        if (offset < 32 || offset > 52 || (offset & 3) != 0)
            return 0;
        ulong bits = offset < 40 ? h->Descriptor : offset < 48 ? h->Available : h->Used;
        *value = (uint)(bits >> ((offset & 4) == 0 ? 0 : 32));
        return 1;
    }
    [UnmanagedCallersOnly]
    private static byte Write32(void* c, ulong address, uint value)
    {
        var h = (Hardware*)c;
        if (!Enter(h, true))
            return 0;
        ulong offset = address - Common;
        if (offset < 32 || offset > 52 || (offset & 3) != 0)
            return 0;
        if (h->BusEnabled || h->Enabled != 0)
            h->BadOrder = true;
        ulong* target = offset < 40 ? &h->Descriptor : offset < 48 ? &h->Available : &h->Used;
        int shift = (offset & 4) == 0 ? 0 : 32;
        *target = (*target & ~(0xffffffffUL << shift)) | ((ulong)value << shift);
        return Exit(h);
    }
    [UnmanagedCallersOnly]
    private static byte Fence(void* c)
    {
        var h = (Hardware*)c;
        if (!Enter(h))
            return 0;
        h->Fences++;
        if (h->ResetRequested)
        {
            if (h->ResetFenceFailure)
                return 0;
            if (!h->ResetObserved || h->BusEnabled)
                h->BadOrder = true;
            if (h->Pool->AllocatedMask == 1)
                h->HeldAtResetFence = true;
        }
        if (h->IndexObserved)
            h->Acquired = true;
        h->FencedIndex = *(ushort*)(h->Data + 130);
        return 1;
    }
    [UnmanagedCallersOnly]
    private static byte BusOn(void* c)
    {
        var h = (Hardware*)c;
        if (!Enter(h, true))
            return 0;
        h->BusEnabled = true;
        return Exit(h);
    }
    [UnmanagedCallersOnly]
    private static byte BusOff(void* c)
    {
        var h = (Hardware*)c;
        if (!Enter(h, true) || h->DisableFailure)
            return 0;
        h->BusEnabled = false;
        return Exit(h);
    }
    [UnmanagedCallersOnly]
    private static byte Clock(void* c, ulong* value)
    {
        var h = (Hardware*)c;
        if (!Enter(h) || h->ClockFailure)
            return 0;
        *value = h->Now;
        return 1;
    }

}
