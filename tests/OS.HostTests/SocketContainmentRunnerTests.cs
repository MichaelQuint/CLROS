using OS.Tooling;

internal static class SocketContainmentRunnerTests
{
    private static string[] Args(string leaf="M07027")=>["--BuildEfi","--Enable"+leaf+"Probe","--LinuxThreadingDisassembly","original-containment-llvm.txt",
        "--LinuxElfPackage","original-containment.pkg","--DryRun"];
    [HostTest("containment runner selects only the original paired app raw peer and exact issue markers")]
    private static void Plan()
    {
        foreach(string leaf in new[]{"M07027","M07028"})
        {
            var options=QemuSmokeOptions.Parse(Args(leaf));var plan=QemuProfiles.Create(options);
            HostCheck.Equal("NativeAotSocketContainment",options.LinuxThreadingApplication);HostCheck.Equal(180,options.TimeoutSeconds);
            HostCheck.Equal("virtio",options.FirmwareRngSource);HostCheck.Equal("TEST_PASS m07.027",plan.Ordered[^1]);
            foreach(string marker in new[]{"TEST_START m07.027","TEST_START m07.028","TEST_PASS m07.028","TEST_PASS m07.027"})
                HostCheck.True(plan.Ordered.Contains(marker));
            foreach(string marker in new[]{"M07_027_TIMEOUT","M07_028_DROP"})HostCheck.True(plan.Required.Contains(marker)&&plan.Unique.Contains(marker));
            foreach(string[] extra in new string[][]{["--LinuxThreadingApplication","NativeAotSocketSmoke"],["--LinuxThreadingApplication","NativeAotSocketSmokeAsync"],
                ["--TimeoutSeconds","119"],["--TimeoutSeconds","221"],["--TimeoutSeconds","295"],["--TimeoutSeconds","296"],
                ["--LinuxElfImageVariant","DigestFault"],["--LinuxHelloDisassembly","hello.txt"],
                ["--RequireMarker","CLROS_M16_011_READY"]})Reject(()=>QemuProfiles.Create(QemuSmokeOptions.Parse([..Args(leaf),..extra])));
            foreach(int seconds in new[]{120,220})
            {
                var bounded=QemuSmokeOptions.Parse([..Args(leaf),"--TimeoutSeconds",seconds.ToString()]);
                _=QemuProfiles.Create(bounded);
                HostCheck.True(bounded.TimeoutSeconds+QemuCaptureSession.RunnerCaptureAllowanceSeconds<=295);
            }
        }
        foreach(string leaf in new[]{"M16011","M16012"})Reject(()=>QemuProfiles.Create(QemuSmokeOptions.Parse([..Args(leaf),"--LinuxThreadingApplication","NativeAotSocketContainment"])));
    }

    [HostTest("containment dryrun owns no peer process and passes the bounded exact command to the original runner")]
    private static void DryRun()
    {
        string root=HostToolProcess.FindRepositoryRoot();var options=QemuSmokeOptions.Parse(Args());
        List<string>? command=null;
        int result=QemuSmoke.RunAsync(root,options,TextWriter.Null,(name,args)=>
        {
            HostCheck.Equal("OS.QemuRunner",name);command=args.ToList();
            return Task.FromResult(new ProcessResult(0,"","",false,false,false,TimeSpan.Zero));
        }).GetAwaiter().GetResult();
        HostCheck.True(result==0&&command is not null);var captured=command!;
        HostCheck.True(captured.Contains("--socket-containment-peer")&&captured.Contains("--ethernet-capture")&&
            !captured.Contains("--socket-forward-port")&&!captured.Contains("--continuing-network-peer"));
        HostCheck.True(captured.Sum(value=>value.Length+3)<30000);
        foreach(int guestSeconds in new[]{120,220})
        {
            var expanded=captured.ToList();
            expanded[expanded.IndexOf("--timeout")+1]=(guestSeconds+QemuCaptureSession.RunnerCaptureAllowanceSeconds).ToString();
            var direct=ProcessRunner.RunAsync(new("dotnet",[HostTools.ManagedTool(root,"OS.QemuRunner"),..expanded],root)
                {Timeout=TimeSpan.FromSeconds(30),OutputCharacterLimit=128*1024},CancellationToken.None).GetAwaiter().GetResult();
            HostCheck.True(direct.Succeeded,direct.FailureReason+direct.StandardOutput+direct.StandardError);
            HostCheck.True(direct.StandardOutput.Contains("-netdev socket,id=n0,connect=127.0.0.1:<ephemeral>",StringComparison.Ordinal)&&
                !direct.StandardOutput.Contains("hostfwd=",StringComparison.Ordinal));
        }
    }

    [HostTest("containment full runner rejects missing sealed program before reading public guest evidence")]
    private static void Authority()
    {
        var options=QemuSmokeOptions.Parse(Args());var plan=QemuProfiles.Create(options);
        var result=new ProcessResult(0,"","",false,false,false,TimeSpan.Zero);
        string? reason=null;
        try{_=QemuTranscripts.ValidateRun(HostToolProcess.FindRepositoryRoot(),options,plan,result,TextWriter.Null);}
        catch(ToolFailure error){reason=error.Reason;}
        HostCheck.Equal("socket_containment_prepared_program_required",reason);
    }
    private static void Reject(Action action)
    {try{action();}catch(ToolFailure){return;}throw new InvalidOperationException("containment_invalid_selection_accepted");}
}
