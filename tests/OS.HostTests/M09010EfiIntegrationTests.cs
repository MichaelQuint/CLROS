internal static class M09010EfiIntegrationTests
{
    [HostTest("m09.010 default off profile requires exact owned APIC parents", HostLane.Fast, 0)]
    private static void Profile() => M09009EfiIntegrationTests.CheckProfile(true);
    [HostTest("m09.010 protocol rejects cross thread TLS generation wake and cleanup corruption", HostLane.Fast, 1)]
    private static void Protocol() => M09009EfiIntegrationTests.CheckProtocol(true);
    [HostTest("m09.010 summary retains all repeated transitions and complete single records", HostLane.Fast, 2)]
    private static void Summary() => M09009EfiIntegrationTests.CheckSummary(true);
}
