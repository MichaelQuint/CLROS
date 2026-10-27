namespace OS.Tooling;

/// <summary>Exact original socket profile/application pairing. Selection grants
/// no package, instruction or guest authority; complete preparation and captured
/// peer/packet/parent validation remain required.</summary>
public static class NativeAotSocketSelection
{
    /// <summary>Return the required unchanged app mode or empty for another
    /// profile. M16-012 includes M16-011 but selects --async. Explicit mismatches
    /// and broader runtime probe conflicts throw before child execution.</summary>
    public static string Application(IReadOnlyDictionary<string,bool> flags,string? selected=null)
    {
        bool On(string name)=>flags.TryGetValue("Enable"+name+"Probe",out bool enabled)&&enabled;
        if(NativeAotSocketContainmentSelection.Selected(flags)||NativeAotSocketVectorsSelection.Selected(flags))return "";
        if(On("M10004") || On("M16025"))return ""; // Shared owners do not select the finite socket fixture.
        string app=On("M16012")?"NativeAotSocketSmokeAsync":On("M16011")?"NativeAotSocketSmoke":"";
        if(app.Length==0)return "";
        if(On("M09011")||On("M09012")||On("M16010"))throw new ToolFailure("socket_runtime_conflict");
        if(selected is not null && selected!=app)throw new ToolFailure("socket_profile_application");
        return app;
    }

    /// <summary>Final exact ready/pass pair, emitted after actual NIC teardown.</summary>
    public static (string Ready,string Pass) Terminal(bool asynchronous)=>asynchronous?
        ("CLROS_M16_012_READY","TEST_PASS m16-012"):("CLROS_M16_011_READY","TEST_PASS m16-011");

    internal static bool Bind(QemuSmokeOptions options,IReadOnlyDictionary<string,bool> flags)
    {
        string app=Application(flags);if(app.Length==0)return false;
        if(options.LinuxThreadingApplication.Length==0&&!options.Explicit.Contains("LinuxThreadingApplication"))options.LinuxThreadingApplication=app;
        _=Application(flags,options.LinuxThreadingApplication);
        if(options.LinuxThreadingDisassembly.Length==0)throw new ToolFailure("socket_disassembly_required");
        if(options.LinuxElfPackage.Length==0||options.LinuxElfImageVariant!="Intact")throw new ToolFailure("socket_original_package_required");
        if(options.LinuxHelloDisassembly.Length!=0)throw new ToolFailure("socket_hello_conflict");
        return true;
    }
}
