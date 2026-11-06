internal static partial class NativeAotDiskStaticReplayTests
{
    [HostTest("Full disk replay expires existing waits only at the observed deadline advance", HostLane.Native)]
    private static void RegistrationClockDoesNotExpireOtherWaits() => WithInventory((inventory, image) =>
    {
        var original = Inspect(Vector(inventory, image, full: true), inventory, true);
        var rows = Vector(inventory, image, full: true, registrationClockCrossing: true);
        var crossing = Inspect(rows, inventory, true);
        HostCheck.True(crossing.FullProfile && !crossing.GrantsOriginalExecutionAuthority);
        HostCheck.Equal(original.Static.Protocol.Calls + 1, crossing.Static.Protocol.Calls);
    });

    [HostTest("Full disk replay rejects premature or missing deadline completion and a forged timeout result", HostLane.Native)]
    private static void RegistrationClockDeadlineRejections() => WithInventory((inventory, image) =>
    {
        var rows = Vector(inventory, image, full: true, registrationClockCrossing: true);
        _ = Inspect(rows, inventory, true);
        Reject(Change(rows, "M16_008_SUPERVISOR identity=4294967299 action=3 ", "ready", 2), inventory, "threading_supervisor", true);
        Reject(Change(rows, "M16_012_POLL identity=0 clock=2020 ", "ready", 2), inventory, "socket_poll_ready", true);
        Reject(Change(rows, "M16_012_POLL identity=0 clock=2021 ", "ready", 1), inventory, "socket_poll_ready", true);
        Reject(Change(rows, "M16_008_RESUME identity=4294967298 ", "valueBits", 0), inventory, null, true);
    });
}
