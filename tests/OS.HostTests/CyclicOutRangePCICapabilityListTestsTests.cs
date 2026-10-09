using OS.Drivers;

internal static unsafe class CyclicOutRangePCICapabilityListTestsTests
{
    [HostTest("pci.capability_walk accepts endpoint bridge and unknown IDs", HostLane.Fast, 0)]
    private static bool AcceptsConventionalHeaders()
    {
        foreach (byte header in new byte[] { 0, 1, 0x80, 0x81 })
        {
            byte[] config = Snapshot();
            config[0x0e] = header;
            config[0x34] = 0x80;
            config[0x80] = 0xfe; config[0x81] = 0x40;
            config[0x40] = 0x09; config[0x41] = 0xfc;
            config[0xfc] = 0xff;
            Check(config, PciCapabilityWalkStatus.CompletePresent, 3, 0);
        }
        return true;
    }

    [HostTest("pci.capability_walk distinguishes empty absent and unsupported headers", HostLane.Fast, 1)]
    private static bool EmptyAbsentAndUnsupported()
    {
        byte[] config = Snapshot();
        config[6] = 0; config[0x34] = 0x41; // Ignore the stale pointer when no list is advertised.
        Check(config, PciCapabilityWalkStatus.CompleteEmpty, 0, 0);
        config[6] = 0x10; config[0x34] = 0;
        Check(config, PciCapabilityWalkStatus.CompleteEmpty, 0, 0);
        foreach (byte header in new byte[] { 2, 0x82, 0x7f, 0xff })
        {
            config[0x0e] = header;
            Check(config, PciCapabilityWalkStatus.UnsupportedHeader, 0, 0x0e);
        }
        config[0] = 0xff; config[1] = 0xff;
        Check(config, PciCapabilityWalkStatus.AbsentDevice, 0, 0);
        return true;
    }

    [HostTest("pci.capability_walk rejects invalid storage and truncated snapshots", HostLane.Fast, 2)]
    private static bool InvalidStorageAndTruncation()
    {
        PciCapabilityWalkResult empty = default;
        HostCheck.True(!empty.IsInitialized && !empty.Succeeded && empty.Count == 0 && empty.FailingOffset == 0);
        CheckResult(PciCapabilityWalk.Validate(null, 256), PciCapabilityWalkStatus.InvalidStorage, 0, 0);
        CheckResult(PciCapabilityWalk.Validate((byte*)(ulong.MaxValue - 127), 256), PciCapabilityWalkStatus.InvalidStorage, 0, 0);
        byte[] config = Snapshot();
        fixed (byte* bytes = config)
        {
            foreach (uint length in new uint[] { 0, 1, 16, 64, 253, 255 })
                CheckResult(PciCapabilityWalk.Validate(bytes, length), PciCapabilityWalkStatus.Truncated, 0, length);
        }
        return true;
    }

    [HostTest("pci.capability_walk rejects bad links self loops and longer cycles", HostLane.Fast, 3)]
    private static bool InvalidLinksAndCycles()
    {
        foreach ((byte offset, PciCapabilityWalkStatus status) in new[]
        {
            ((byte)1, PciCapabilityWalkStatus.OffsetOutOfRange),
            ((byte)0x3c, PciCapabilityWalkStatus.OffsetOutOfRange),
            ((byte)0xff, PciCapabilityWalkStatus.OffsetOutOfRange),
            ((byte)0x41, PciCapabilityWalkStatus.OffsetUnaligned),
            ((byte)0xfb, PciCapabilityWalkStatus.OffsetUnaligned)
        })
        {
            byte[] config = Snapshot();
            config[0x34] = offset;
            Check(config, status, 0, offset);
            config[0x34] = 0x40; config[0x41] = offset;
            Check(config, status, 1, offset);
        }
        byte[] cyclic = Snapshot();
        cyclic[0x34] = 0x40; cyclic[0x41] = 0x40;
        Check(cyclic, PciCapabilityWalkStatus.LoopDetected, 1, 0x40);
        cyclic[0x41] = 0x44; cyclic[0x45] = 0x80; cyclic[0x81] = 0x44;
        Check(cyclic, PciCapabilityWalkStatus.LoopDetected, 3, 0x44);
        return true;
    }

    [HostTest("pci.capability_walk bounds maximum chains and preserves canaries", HostLane.Fast, 4)]
    private static bool MaximumChainAndCanaries()
    {
        byte[] storage = new byte[256 + 32];
        storage.AsSpan().Fill(0xa7);
        byte[] config = Snapshot();
        config[0x34] = 0x40;
        for (int offset = 0x40; offset <= 0xfc; offset += 4)
        {
            config[offset] = 0xf0;
            config[offset + 1] = offset == 0xfc ? (byte)0 : (byte)(offset + 4);
        }
        config.CopyTo(storage, 16);
        fixed (byte* bytes = storage)
        {
            foreach (bool loop in new[] { false, true })
            {
                storage[16 + 0xfd] = loop ? (byte)0x40 : (byte)0;
                byte[] original = (byte[])storage.Clone();
                for (int repeat = 0; repeat < 3; repeat++)
                    CheckResult(PciCapabilityWalk.Validate(bytes + 16, 256),
                        loop ? PciCapabilityWalkStatus.LoopDetected : PciCapabilityWalkStatus.CompletePresent,
                        48, loop ? 0x40U : 0);
                HostCheck.True(storage.AsSpan().SequenceEqual(original), "snapshot or canaries changed");
            }
        }
        return true;
    }

    private static byte[] Snapshot()
    {
        byte[] config = new byte[256];
        config[0] = 0x34; config[1] = 0x12;
        config[6] = 0x10;
        return config;
    }

    private static void Check(byte[] config, PciCapabilityWalkStatus status, uint count, uint failingOffset)
    {
        byte[] original = (byte[])config.Clone();
        fixed (byte* bytes = config)
            for (int repeat = 0; repeat < 2; repeat++)
                CheckResult(PciCapabilityWalk.Validate(bytes, (uint)config.Length), status, count, failingOffset);
        HostCheck.True(config.AsSpan().SequenceEqual(original), "configuration snapshot changed");
    }

    private static void CheckResult(PciCapabilityWalkResult result, PciCapabilityWalkStatus status, uint count, uint failingOffset)
    {
        HostCheck.True(result.IsInitialized);
        HostCheck.Equal(status, result.Status);
        HostCheck.Equal(count, result.Count);
        HostCheck.Equal(failingOffset, result.FailingOffset);
        HostCheck.Equal(status is PciCapabilityWalkStatus.CompleteEmpty or PciCapabilityWalkStatus.CompletePresent, result.Succeeded);
    }
}
