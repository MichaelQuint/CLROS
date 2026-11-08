using System.Runtime.InteropServices;
using OS.Drivers;
using OS.Storage;
using Block = OS.Storage.ContinuingReadOnlyBlockService;

internal static unsafe partial class ContinuingReadOnlyBlockServiceTests
{
    [HostTest("block.service explicit three page layout retains all sixteen sector lengths and exact cleanup")]
    private static void LargeReadLayouts()
    {
        for (uint sectors = 1; sectors <= 16; sectors++)
        {
            using var f = new Fixture(largeReads: true);
            uint bytes = sectors * 512;
            f.Hardware->ExpectedBytes = bytes;
            using var capture = new Capture(f, bytes);
            HostCheck.Equal(Block.Status.Success, f.Start());
            var layout = f.State->Receipt.Layout;
            HostCheck.Equal(12288UL, layout.Range.ByteLength);
            HostCheck.Equal(4096U, layout.DataByteOffset);
            HostCheck.Equal(16U, f.State->MaximumTransferSectors);
            HostCheck.Equal(7UL, f.Pool->AllocatedMask);
            HostCheck.Equal(VirtioBlkDmaStatus.InvalidRange, DMASafeRequestStatusDescriptorAvailableUsedRing.TryLayout(layout.Range, out _));
            HostCheck.Equal(VirtioBlkDmaStatus.Success, DMASafeRequestStatusDescriptorAvailableUsedRing.TryContinuingReadLayout(layout.Range, out _));
            HostCheck.Equal(Block.Status.Success, Submit(f.State, Capacity - sectors, sectors, out var token));
            HostCheck.Equal(DeviceBase + 4096, *(ulong*)(f.Data + 16));
            HostCheck.Equal(bytes, *(uint*)(f.Data + 24));
            f.Complete(length: bytes + 1);
            HostCheck.Equal(Block.Status.Ready, Poll(f.State, out var completed));
            HostCheck.Equal(bytes, completed.Bytes);
            Span<byte> output = new(capture.Sink->Output, (int)bytes + 2);
            output.Fill(0xa5);
            HostCheck.Equal(Block.Status.Success, Block.Copy(f.State, token, output.Slice(1, (int)bytes)));
            HostCheck.Equal((byte)0xa5, output[0]);
            HostCheck.Equal((byte)0xa5, output[^1]);
            for (int i = 0; i < bytes; i++)
                HostCheck.Equal(Payload(Capacity - sectors, i), output[i + 1]);
            HostCheck.True(!capture.Sink->Bad && capture.Sink->Count == 3);
            HostCheck.Equal(1UL, f.State->Submitted);
            HostCheck.Equal(1UL, f.State->Completed);
            HostCheck.Equal(1UL, f.State->Copied);
            HostCheck.Equal(Block.Status.Success, Block.Stop(f.State));
            HostCheck.Equal(0UL, f.Pool->AllocatedMask);
            HostCheck.True(new ReadOnlySpan<byte>(f.Data, 12288).IndexOfAnyExcept((byte)0) < 0);
            HostCheck.Equal(Block.Status.Success, Block.Stop(f.State));
            f.CheckGuards();
        }
    }

    [HostTest("block.service large read bounds and malformed completions cannot publish or copy guessed bytes")]
    private static void LargeReadFailures()
    {
        foreach (uint length in new uint[] { 0, 8192, 8194 })
        {
            using var f = new Fixture(largeReads: true);
            f.Hardware->ExpectedBytes = 8192;
            HostCheck.Equal(Block.Status.Success, f.Start());
            uint calls = f.Hardware->Calls;
            foreach ((ulong sector, uint count) in new[] { (Capacity - 15, 16U), (0UL, 17U), (0UL, 0U) })
                HostCheck.Equal(Block.Status.SectorOutOfRange, Submit(f.State, sector, count, out _));
            HostCheck.Equal(calls, f.Hardware->Calls);
            HostCheck.Equal(0UL, f.State->Submitted);
            HostCheck.Equal(Block.Status.Success, Submit(f.State, 31, 16, out var token));
            f.Complete(length: length);
            HostCheck.Equal(Block.Status.InvalidCompletion, Poll(f.State, out _));
            byte[] output = Enumerable.Repeat((byte)0xa5, 8192).ToArray();
            HostCheck.Equal(Block.Status.InvalidState, Block.Copy(f.State, token, output));
            HostCheck.True(output.All(value => value == 0xa5));
            HostCheck.Equal(Block.Status.Success, Block.Stop(f.State));
            HostCheck.Equal(0UL, f.Pool->AllocatedMask);
            f.CheckGuards();
        }
        using var legacy = new Fixture();
        var input = legacy.Input;
        input.UseLargeReadLayout = true;
        HostCheck.Equal(Block.Status.SetupFailed, Block.Start(legacy.State, input));
        HostCheck.Equal(0UL, legacy.Pool->AllocatedMask);
        HostCheck.True(!legacy.State->RequiresCleanup);
    }

    [HostTest("block.service large read rejects stale DMA generation extent and ownership without touching data or copy output")]
    private static void LargeReadStaleDma()
    {
        foreach (string field in new[] { "Generation", "Pages", "Owner" })
        {
            using var f = new Fixture(largeReads: true);
            f.Hardware->ExpectedBytes = 8192;
            HostCheck.Equal(Block.Status.Success, f.Start());
            HostCheck.Equal(Block.Status.Success, Submit(f.State, 41, 16, out var token));
            f.Complete(length: 8193);
            HostCheck.Equal(Block.Status.Ready, Poll(f.State, out _));
            byte[] data = new ReadOnlySpan<byte>(f.Data, 12288).ToArray();
            byte[] output = Enumerable.Repeat((byte)0xa5, 8192).ToArray();
            byte* word = (byte*)f.Slot + (int)Marshal.OffsetOf<DmaBufferSlot>(field);
            byte original = *word;
            try
            {
                *word ^= 1;
                uint calls = f.Hardware->Calls;
                HostCheck.Equal(Block.Status.InvalidState, Block.Copy(f.State, token, output));
                HostCheck.Equal(Block.Status.InvalidState, Poll(f.State, out _));
                HostCheck.Equal(calls, f.Hardware->Calls);
                HostCheck.True(data.AsSpan().SequenceEqual(new ReadOnlySpan<byte>(f.Data, 12288)));
                HostCheck.True(output.All(value => value == 0xa5));
                HostCheck.Equal(Block.Status.DmaRejected, Block.Stop(f.State));
                HostCheck.Equal(7UL, f.Pool->AllocatedMask);
                HostCheck.True(data.AsSpan().SequenceEqual(new ReadOnlySpan<byte>(f.Data, 12288)));
            }
            finally
            {
                *word = original;
            }
            HostCheck.Equal(Block.Status.Success, Block.Stop(f.State));
            HostCheck.Equal(0UL, f.Pool->AllocatedMask);
            f.CheckGuards();
        }
    }

    [HostTest("block.service sixteen sector cancellation drains the original request and preserves exact reuse and cleanup")]
    private static void LargeReadCancellation()
    {
        foreach (bool ready in new[] { false, true })
        {
            using var f = new Fixture(largeReads: true);
            f.Hardware->ExpectedBytes = 8192;
            HostCheck.Equal(Block.Status.Success, f.Start());
            HostCheck.Equal(Block.Status.Success, Submit(f.State, 51, 16, out var token));
            if (ready)
            {
                f.Complete(length: 8193);
                HostCheck.Equal(Block.Status.Ready, Poll(f.State, out _));
            }
            HostCheck.Equal(Block.Status.Success, Block.Cancel(f.State, token));
            if (!ready)
            {
                HostCheck.Equal(Block.Status.Busy, Submit(f.State, 52, 16, out _));
                f.Complete(length: 8193);
                HostCheck.Equal(Block.Status.Cancelled, Poll(f.State, out var completed));
                HostCheck.Equal(0U, completed.Bytes);
            }
            HostCheck.Equal(Block.Status.StaleToken, Block.Copy(f.State, token, new byte[8192]));
            HostCheck.Equal(0UL, f.State->Copied);
            HostCheck.Equal(1UL, f.State->Cancelled);
            HostCheck.Equal(Block.Status.Success, Submit(f.State, 52, 16, out var next));
            HostCheck.Equal(Block.Status.StaleToken, Block.Copy(f.State, token, new byte[8192]));
            f.Complete(length: 8193);
            HostCheck.Equal(Block.Status.Ready, Poll(f.State, out _));
            HostCheck.Equal(Block.Status.Success, Block.Copy(f.State, next, new byte[8192]));
            HostCheck.Equal(Block.Status.Success, Block.Stop(f.State));
            HostCheck.Equal(0UL, f.Pool->AllocatedMask);
            f.CheckGuards();
        }
    }
}
