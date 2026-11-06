using OS.Storage;
using System.Runtime.InteropServices;

internal static unsafe class LiveDeviceExistingIReadOnlyBlockDeviceContractTests
{
    [HostTest("block.contract constrained Info and ReadBlocks return actual checked device data")]
    private static void Success()
    {
        using var f = new BlockReadFixture();
        f.State->ExpectedSector = 0;
        var device = f.CreateLiveDevice();
        var info = LBAN0Guest.Info(ref device);
        HostCheck.True(info.BlockSize == 512 && info.BlockCount == BlockReadFixture.Capacity && info.CapacityBytes == info.BlockCount * 512);
        var read = LBAN0Guest.Read(ref device, new Span<byte>(f.Output, 512));
        HostCheck.True(read.Succeeded && read.BlocksRead == 1 && read.BytesRead == 512 && device.LastRead.Succeeded && device.Attempted);
        for (int i = 0; i < 512; i++)
            HostCheck.Equal(BlockReadFixture.Payload(i), f.Output[i]);
        uint calls = f.State->Calls;
        HostCheck.Equal(BlockDeviceStatus.IoError, LBAN0Guest.Read(ref device, new Span<byte>(f.Output, 512)).Status);
        HostCheck.Equal(calls, f.State->Calls);
        f.Check();
    }

    [HostTest("block.contract preserves canonical zero range and destination bounds before callbacks")]
    private static void Admission()
    {
        using var f = new BlockReadFixture();
        var device = f.CreateLiveDevice();
        var zero = device.ReadBlocks(BlockReadFixture.Capacity, 0, default);
        HostCheck.True(zero.Succeeded && zero.BytesRead == 0 && !device.Attempted);
        HostCheck.Equal(BlockDeviceStatus.InvalidBlockRange, device.ReadBlocks(BlockReadFixture.Capacity + 1, 0, default).Status);
        HostCheck.Equal(BlockDeviceStatus.InvalidBlockRange, device.ReadBlocks(BlockReadFixture.Capacity, 1, new Span<byte>(f.Output, 512)).Status);
        HostCheck.Equal(BlockDeviceStatus.DestinationTooSmall, device.ReadBlocks(0, 1, new Span<byte>(f.Output, 511)).Status);
        Span<byte> two = stackalloc byte[1024];
        HostCheck.Equal(BlockDeviceStatus.InvalidBlockCount, device.ReadBlocks(0, 2, two).Status);
        HostCheck.True(!device.Attempted && f.State->Calls == 0);
        var absent = default(LiveDeviceExistingIReadOnlyBlockDeviceContract);
        HostCheck.True(!absent.ReadBlocks(0, 0, default).Succeeded);
        var wrongSize = f.CreateLiveDevice(blockSize: 1024);
        HostCheck.Equal(BlockDeviceStatus.InvalidBlockSize, wrongSize.ReadBlocks(0, 0, default).Status);
        f.Unchanged();
        f.Check();
    }

    [HostTest("block.contract rejects stale capacity before publication and retains failure ownership")]
    private static void Failures()
    {
        using (var f = new BlockReadFixture())
        {
            var device = f.CreateLiveDevice();
            f.State->Capacity--;
            HostCheck.True(!LBAN0Guest.Read(ref device, new Span<byte>(f.Output, 512)).Succeeded);
            HostCheck.True(device.LastRead.Status == VirtioBlkReadStatus.ConfigurationChanged && !device.LastRead.Published &&
                device.LastRead.Queue.ResetComplete && f.Pool->AllocatedMask == 0);
            f.Unchanged();
            f.Check();
        }
        for (int mode = 0; mode < 3; mode++)
        {
            using var f = new BlockReadFixture();
            f.State->ExpectedSector = 0;
            if (mode == 0)
                f.State->CompletionStatus = 1;
            if (mode == 1)
                f.State->NeverComplete = true;
            if (mode == 2)
                f.State->ResetStuck = true;
            var device = f.CreateLiveDevice();
            var result = LBAN0Guest.Read(ref device, new Span<byte>(f.Output, 512));
            HostCheck.True(!result.Succeeded && result.BytesRead == 0 && device.Attempted);
            HostCheck.Equal(mode == 2 ? 1UL : 0UL, f.Pool->AllocatedMask);
            uint calls = f.State->Calls;
            HostCheck.True(!LBAN0Guest.Read(ref device, new Span<byte>(f.Output, 512)).Succeeded);
            HostCheck.Equal(calls, f.State->Calls);
            f.Unchanged();
            f.Check();
        }
    }

    [HostTest("block.contract rejects receiver overlap before changing adapter or destination")]
    private static void ReceiverAlias()
    {
        byte* memory = (byte*)NativeMemory.AlignedAlloc(4096, 4096);
        try
        {
            foreach (var offsets in new (int Receiver, int Output, int Length)[] { (0, 0, 512), (496, 0, 512), (0, 16, 512), (600, 0, 1024) })
            {
                using var f = new BlockReadFixture();
                new Span<byte>(memory, 4096).Fill(0xa5);
                var receiver = (LiveDeviceExistingIReadOnlyBlockDeviceContract*)(memory + offsets.Receiver);
                *receiver = f.CreateLiveDevice();
                var before = new ReadOnlySpan<byte>(memory, 4096).ToArray();
                var output = new Span<byte>(memory + offsets.Output, offsets.Length);
                HostCheck.True(!LBAN0Guest.Read(ref *receiver, output).Succeeded);
                HostCheck.True(!receiver->Attempted && f.State->Calls == 0 &&
                    new ReadOnlySpan<byte>(memory, 4096).SequenceEqual(before));
                f.Unchanged();
                f.Check();
            }
        }
        finally
        {
            NativeMemory.AlignedFree(memory);
        }
    }
}
