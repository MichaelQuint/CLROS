using OS.Tooling;

internal static class NativeAotServiceSelectionTests
{
    [HostTest("service selector distinguishes dedicated source from ordinary web and rejects substitute names")]
    private static void Application()
    {
        var empty = new Dictionary<string, bool>(); HostCheck.Equal("", NativeAotServiceSelection.Application(empty));
        var web = EfiProfile.Create(null, new Dictionary<string, bool> { ["EnableM10004Probe"] = true });
        HostCheck.Equal("MinimalApiSmoke", NativeAotWebSelection.Application(web.Flags));
        HostCheck.Equal("", NativeAotServiceSelection.Application(web.Flags));
        var service = EfiProfile.Create(null, new Dictionary<string, bool> { ["EnableM16014Probe"] = true });
        HostCheck.Equal("", NativeAotWebSelection.Application(service.Flags));
        HostCheck.Equal("MinimalApiService", NativeAotServiceSelection.Application(service.Flags));
        HostCheck.Equal("MinimalApiService", NativeAotServiceSelection.Application(service.Flags, "MinimalApiService"));
        foreach (string name in new[] { "", "MinimalApiSmoke", "NativeAotRuntimeMatrix", "minimalapiservice" })
            Reject(() => NativeAotServiceSelection.Application(service.Flags, name), "service_profile_application");
    }

    [HostTest("service selector requires the owned stack and rejects diagnostic unions before execution")]
    private static void Conflicts()
    {
        var service = EfiProfile.Create(null, new Dictionary<string, bool> { ["EnableM16014Probe"] = true });
        foreach (string parent in new[] { "M10004", "M16012", "M16009" })
        {
            var flags = service.Flags.ToDictionary(); flags["Enable" + parent + "Probe"] = false;
            Reject(() => NativeAotServiceSelection.Application(flags), "service_profile_prerequisites");
        }
        foreach (string conflict in new[] { "M09011", "M09012", "M16010", "M16013", "M07027", "M07028", "M16019Unavailable", "M16019Invalid" })
        {
            var flags = service.Flags.ToDictionary(); flags["Enable" + conflict + "Probe"] = true;
            Reject(() => NativeAotServiceSelection.Application(flags), "service_profile_conflict");
        }
    }

    private static void Reject(Action action, string expected)
    {
        string? actual = null; try { action(); } catch (ToolFailure error) { actual = error.Reason; }
        HostCheck.Equal(expected, actual ?? "accepted");
    }
}
