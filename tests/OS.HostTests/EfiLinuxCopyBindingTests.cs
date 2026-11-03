using OS.Tooling;

internal static class EfiLinuxCopyBindingTests
{
    private const string Prefix = "OS_OS_Boot_EfiEntry_M03FirstExitAdapter__";
    private const ulong Code = 0x180001040;
    private static readonly string[] Names =
        ["CopyLinuxUserBytes", "CopyDescriptorInput", "CopyDescriptorOutput", "LinuxThreadCopyInput", "LinuxThreadCopyOutput"];

    [HostTest("efi linux copy bindings select syscall descriptor and threaded callbacks")]
    private static void ValidProfiles()
    {
        foreach (string profile in new[] { "", "M16005", "M16006", "M16007", "M16008", "M16009" })
            Verify(profile, Expected(profile));
    }

    [HostTest("efi linux copy bindings reject missing duplicate and foreign callbacks")]
    private static void RequiredCallbacks()
    {
        foreach (string profile in new[] { "M16005", "M16006", "M16007", "M16008", "M16009" })
        {
            string[] required = Required(profile);
            foreach (string name in required)
            {
                string map = Expected(profile), row = Row(name);
                string reason = Group(name) + "_native_adapter_missing";
                Reject(profile, map.Replace(row, "", StringComparison.Ordinal), reason);
                Reject(profile, map + row, reason);
                Reject(profile, map.Replace(row, Row(name, owner: "foreign.obj"), StringComparison.Ordinal), reason);
            }
        }
    }

    [HostTest("efi linux copy bindings reject unrequested and superseded callbacks")]
    private static void UnrequestedCallbacks()
    {
        foreach (string profile in new[] { "", "M16005", "M16006", "M16007", "M16008", "M16009" })
            foreach (string name in Names.Except(Required(profile)))
                Reject(profile, Expected(profile) + Row(name), Group(name) + "_unrequested_native_adapter");
    }

    [HostTest("efi linux copy bindings require in-image file-backed executable addresses")]
    private static void CallbackAddresses()
    {
        foreach (string profile in new[] { "M16005", "M16007", "M16008", "M16009" })
            foreach (string name in Required(profile))
            {
                string map = Expected(profile), row = Row(name), group = Group(name);
                foreach (ulong address in new[] { 0x180000000UL, 0x180004000UL })
                    Reject(profile, map.Replace(row, Row(name, address), StringComparison.Ordinal), group + "_rva_invalid");
                Reject(profile, map.Replace(row, Row(name, 0x180001300), StringComparison.Ordinal), group + "_not_backed");
                Reject(profile, map.Replace(row, Row(name, 0x180002000), StringComparison.Ordinal), group + "_section_invalid");
            }
    }

    private static string[] Required(string profile) => profile switch
    {
        "M16005" or "M16006" => [Names[0]],
        "M16007" => [Names[0], Names[1], Names[2]],
        "M16008" or "M16009" => [Names[3], Names[4]],
        _ => []
    };
    private static string Group(string name) => name == Names[0] ? "m16_syscalls" :
        name.StartsWith("LinuxThread", StringComparison.Ordinal) ? "m16_threads" : "m16_descriptors";
    private static string Row(string name, ulong address = Code, string owner = "OS.obj")
        => $" 0001:00000040 {Prefix}{name} {address:x16} {owner}\n";
    private static string Expected(string profile) => string.Concat(Required(profile).Select(name => Row(name)));
    private static void Verify(string selected, string map)
    {
        Dictionary<string, bool> flags = [];
        if (selected.Length != 0)
            flags.Add("Enable" + selected + "Probe", true);
        EfiVerify.TestLinuxCopyBindings(EfiProfile.Create("Diagnostic", flags), map,
            EfiArtifactVerificationScriptTests.CreateValidPe());
    }
    private static void Reject(string profile, string map, string reason)
    {
        try
        {
            Verify(profile, map);
        }
        catch (ToolFailure failure)
        {
            HostCheck.True(failure.Message.StartsWith(reason + " ", StringComparison.Ordinal), failure.Message);
            return;
        }
        HostCheck.True(false, "accepted invalid copy binding: " + reason);
    }
}
