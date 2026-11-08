using Block = OS.Storage.ContinuingReadOnlyBlockService;

internal static unsafe partial class ContinuingReadOnlyBlockServiceTests
{
    [HostTest("block idle polls retain device checks without timer samples and next submit freshly rejects regression")]
    private static void IdleClockSampling()
    {
        using var f = new Fixture();
        HostCheck.Equal(Block.Status.Success, f.Start());
        uint clocks = f.Hardware->ClockCalls, calls = f.Hardware->Calls;
        HostCheck.Equal(Block.Status.Idle, Poll(f.State, out _));
        HostCheck.Equal(clocks, f.Hardware->ClockCalls);
        HostCheck.True(f.Hardware->Calls > calls);
        f.Hardware->Now = 200;
        HostCheck.Equal(Block.Status.Success, Submit(f.State, 51, out var token));
        clocks = f.Hardware->ClockCalls;
        f.Hardware->Now = 500;
        HostCheck.Equal(Block.Status.Pending, Poll(f.State, out _));
        HostCheck.Equal(clocks + 1, f.Hardware->ClockCalls);
        // Completion wins at the original deadline, after actual device reads.
        f.Hardware->Now = 1200;
        f.Complete();
        HostCheck.Equal(Block.Status.Ready, Poll(f.State, out _));
        HostCheck.Equal(Block.Status.Success, Block.Copy(f.State, token, new byte[512]));
        clocks = f.Hardware->ClockCalls;
        f.Hardware->Now = 100;
        HostCheck.Equal(Block.Status.Idle, Poll(f.State, out _));
        HostCheck.Equal(clocks, f.Hardware->ClockCalls);
        HostCheck.Equal(Block.Status.ClockRegressed, Submit(f.State, 52, out _));
        HostCheck.Equal(1UL, f.State->Submitted);
        HostCheck.Equal(Block.Status.Success, Block.Stop(f.State));
        f.CheckGuards();
    }
}
