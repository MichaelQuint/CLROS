using OS.Tooling;

internal static class LinuxHelloIntegrationTests
{
    [HostTest("nativeaot.hello native call logging and terminal memory cleanup retain exact source admission")]
    private static void NativeSourceAdmission()
    {
        string root=HostToolProcess.FindRepositoryRoot();
        foreach(string name in new[]{"EfiEntry.LinuxThreads.cs","EfiEntry.LinuxThreadRun.cs"})
        {
            string source=File.ReadAllText(Path.Combine(root,"src/OS/Boot",name));
            HostCheck.True(EfiSourceAdmission.Inspect(source,@"Boot\"+name).Succeeded);
            HostCheck.True(!EfiSourceAdmission.Inspect(source+"\n// changed producer\n",@"Boot\"+name).Succeeded);
        }
    }

    [HostTest("nativeaot.hello wrapper admits disassembly option only with the signal profile before any child")]
    private static void WrongProfile()
    {
        var options=QemuSmokeOptions.Parse("-LinuxHelloDisassembly","artifacts/absent.dump","-DryRun");
        HostCheck.Equal("artifacts/absent.dump",options.LinuxHelloDisassembly);
        string root=HostToolProcess.FindRepositoryRoot(); int children=0; using StringWriter output=new();
        int code=QemuSmoke.RunAsync(root,options,output,(_,_)=>{children++;throw new InvalidOperationException("unexpected child");}).GetAwaiter().GetResult();
        HostCheck.True(code!=0 && children==0 && output.ToString().Contains("hello_profile_requires_m16_009",StringComparison.Ordinal));
    }

    [HostTest("nativeaot.hello wrapper rejects substituted input and output-alias disassembly before build image or runner")]
    private static void InputFailures()
    {
        string root=HostToolProcess.FindRepositoryRoot();
        string directory=Path.Combine(root,"artifacts","hello-integration-"+Guid.NewGuid().ToString("N")); Directory.CreateDirectory(directory);
        try
        {
            string package=Path.Combine(directory,"mechanical.pkg"), dump=Path.Combine(directory,"input.dump");
            File.WriteAllBytes(package,LinuxSignalProofFixture.CreatePackage("AlternateStack")); File.WriteAllText(dump,"headers only");
            foreach(bool alias in new[]{false,true})
            {
                var options=QemuSmokeOptions.Parse("-BuildEfi","-EnableM16009Probe","-LinuxElfPackage",package,"-LinuxHelloDisassembly",dump);
                if(alias) options.SerialLog=dump;
                int children=0; using StringWriter output=new();
                int code=QemuSmoke.RunAsync(root,options,output,(_,_)=>{children++;throw new InvalidOperationException("unexpected child");}).GetAwaiter().GetResult();
                HostCheck.True(code!=0 && children==0 && output.ToString().Contains(alias?"hello_disassembly_output_alias":"hello_package_identity",StringComparison.Ordinal));
                HostCheck.Equal("headers only",File.ReadAllText(dump));
                // Both retained read locks must have been released on failure.
                using FileStream exclusivePackage=new(package,FileMode.Open,FileAccess.ReadWrite,FileShare.None);
                using FileStream exclusiveDump=new(dump,FileMode.Open,FileAccess.ReadWrite,FileShare.None);
            }
        }
        finally { Directory.Delete(directory,true); }
    }

    [HostTest("nativeaot.hello full caller requires sealed preparation and preserves mechanical parent and runner paths")]
    private static void PreparedBoundary()
    {
        string root=HostToolProcess.FindRepositoryRoot();
        var options=QemuSmokeOptions.Parse("-BuildEfi","-EnableM16009Probe","-LinuxHelloDisassembly","artifacts/unprepared.dump");
        using StringWriter output=new(); var plan=QemuProfiles.Create(options,output.WriteLine);
        bool rejected=false;
        try { QemuTranscripts.ValidateRun(root,options,plan,new(0,"","",false,false,false,TimeSpan.Zero),output); }
        catch(ToolFailure error) { rejected=error.Reason=="hello_prepared_program_required"; }
        HostCheck.True(rejected);
        // The model seam is deliberately absent from every production caller.
        foreach(string path in new[]{"QemuSmoke.cs","QemuTranscripts.Run.cs","QemuTranscripts.M03.cs"})
            HostCheck.True(!File.ReadAllText(Path.Combine(root,"tools/OS.Tooling",path)).Contains("InspectLinuxHelloProtocolForTests",StringComparison.Ordinal));
    }
}
