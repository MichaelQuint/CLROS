namespace OS.Tooling;

/// <summary>Explicit mechanical epoll race selection. Default false preserves
/// the original NativeAOT socket consumer and its owned peer. Selection grants
/// no package, native, firmware or guest authority.</summary>
public static class LinuxSignalEpollSelection
{
    internal static bool Bind(QemuSmokeOptions options,IReadOnlyDictionary<string,bool> flags)
    {
        if(!options.SignalEpollRace)return false;
        bool On(string key)=>flags.TryGetValue("Enable"+key+"Probe",out bool enabled)&&enabled;
        if(!On("M16012")||!On("M16011")||!On("M16009")||
            new[]{"M16013","M07027","M07028","M09011","M09012","M16010","M10004","M16014"}.Any(On)||options.ServiceLifecycle)
            throw new ToolFailure("signal_epoll_profile_conflict");
        if(options.HasThreadingSelection||options.LinuxHelloDisassembly.Length!=0||options.Explicit.Contains("LinuxHelloDisassembly"))
            throw new ToolFailure("signal_epoll_prepared_application_conflict");
        if(options.LinuxElfPackage.Length==0||options.LinuxElfImageVariant!="Intact")
            throw new ToolFailure("signal_epoll_original_package_required");
        return true;
    }

    /// <summary>Check exact canonical bytes through the retained seekable read
    /// lock before launch; rewind on success. Names grant no input authority.</summary>
    internal static void RequirePackage(Stream input)
    {
        byte[] expected=LinuxSignalProofFixture.CreatePackage(LinuxSignalProofFixture.EpollStolenWaitVariant);
        if(!input.CanRead||!input.CanSeek||input.Position!=0||input.Length!=expected.Length)
            throw new ToolFailure("signal_epoll_package_identity");
        byte[] observed=new byte[expected.Length];input.ReadExactly(observed);
        if(!observed.AsSpan().SequenceEqual(expected))throw new ToolFailure("signal_epoll_package_identity");
        input.Position=0;
    }
}
