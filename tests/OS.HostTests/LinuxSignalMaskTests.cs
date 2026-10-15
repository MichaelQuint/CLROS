using OS.Kernel.Compatibility.LinuxX64;

internal static class LinuxSignalMaskTests
{
    [HostTest("linux.signal-mask block unblock and set preserve exact kernel bits")]
    private static void Operations()
    {
        LinuxSignalMask mask = new(1UL << 1);
        var block = mask.Evaluate(LinuxSignalMask.Block, true, 1UL << 63, 8);
        HostCheck.True(block.Succeeded); HostCheck.Equal(2UL, block.Before.Blocked); HostCheck.Equal((1UL << 63) | 2, block.After.Blocked);
        var unblock = block.After.Evaluate(LinuxSignalMask.Unblock, true, 2, 8);
        HostCheck.True(unblock.Succeeded); HostCheck.Equal(1UL << 63, unblock.After.Blocked);
        var set = unblock.After.Evaluate(LinuxSignalMask.Set, true, 0x380000000, 8);
        HostCheck.True(set.Succeeded); HostCheck.Equal(0x380000000UL, set.After.Blocked);
        HostCheck.True(set.After.Contains(32) && set.After.Contains(33) && set.After.Contains(34));
        HostCheck.Equal(2UL, mask.Blocked); HostCheck.True(!default(LinuxSignalMask.Result).Succeeded);
    }

    [HostTest("linux.signal-mask strips only SIGKILL SIGSTOP and checks signal boundaries")]
    private static void UnblockableSignals()
    {
        LinuxSignalMask mask = new(ulong.MaxValue);
        HostCheck.Equal(ulong.MaxValue & ~((1UL << 8) | (1UL << 18)), mask.Blocked);
        for (uint number = 1; number <= 64; number++) HostCheck.Equal(number != 9 && number != 19, mask.Contains(number));
        HostCheck.True(!mask.Contains(0) && !mask.Contains(65) && !mask.Contains(uint.MaxValue));
        foreach (ulong how in new[] { LinuxSignalMask.Block, LinuxSignalMask.Set })
            HostCheck.Equal(mask.Blocked, default(LinuxSignalMask).Evaluate(how, true, ulong.MaxValue, 8).After.Blocked);
    }

    [HostTest("linux.signal-mask query ignores how but always checks kernel set size")]
    private static void QueryAndFailure()
    {
        LinuxSignalMask mask = new(0x180000002);
        var query = mask.Evaluate(ulong.MaxValue, false, ulong.MaxValue, 8);
        HostCheck.True(query.Succeeded); HostCheck.Equal(mask.Blocked, query.After.Blocked);
        foreach (ulong size in new ulong[] { 0, 1, 4, 7, 9, 128, ulong.MaxValue })
        {
            var rejected = mask.Evaluate(0, false, 0, size); HostCheck.True(!rejected.Succeeded); HostCheck.Equal(-22L, rejected.Error);
            HostCheck.Equal(mask.Blocked, rejected.Before.Blocked); HostCheck.Equal(mask.Blocked, rejected.After.Blocked);
        }
        foreach (ulong how in new ulong[] { 3, 0x100000000, ulong.MaxValue })
        {
            var rejected = mask.Evaluate(how, true, 0, 8); HostCheck.Equal(-22L, rejected.Error); HostCheck.Equal(mask.Blocked, rejected.After.Blocked);
        }
    }

    [HostTest("linux.signal-mask clone snapshots restore independently without sharing mutable state")]
    private static void CloneSnapshots()
    {
        LinuxSignalMask parent = new(0x380000002), child = new(parent.Blocked);
        var temporary = parent.Evaluate(LinuxSignalMask.Block, true, ulong.MaxValue, 8);
        var restored = temporary.After.Evaluate(LinuxSignalMask.Set, true, parent.Blocked, 8);
        var childRestore = child.Evaluate(LinuxSignalMask.Unblock, true, 1UL << 32, 8);
        HostCheck.True(temporary.Succeeded && restored.Succeeded && childRestore.Succeeded);
        HostCheck.Equal(parent.Blocked, restored.After.Blocked); HostCheck.Equal(parent.Blocked, child.Blocked);
        HostCheck.True(restored.After.Contains(33) && !childRestore.After.Contains(33));
        HostCheck.True(childRestore.After.Contains(32) && childRestore.After.Contains(34));
    }
}
