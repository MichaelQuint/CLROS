namespace OS.Tooling;

public static partial class QemuTranscripts
{
    // QemuSmoke supplies the private creator-owned completion and identical
    // runner, plus its exact four pre-run input records. Only the output set is
    // owned here; input/native locks stay with the caller through final success.
    // No public copied-value factory can construct this execution authority.
    internal static int InspectOwnedWebRace(string root,QemuSmokeOptions options,QemuSmokePlan plan,
        NativeAotWebProgram program,NativeAotWebRacePeer.Completion completion,ProcessResult runner,WebRunnerContext context,
        AuthorityFiles inputs,AuthorityFile packageInput,AuthorityFile dumpInput,AuthorityFile imageInput,AuthorityFile efiInput,
        TextWriter output,Action validateCapture,Action verifyCurrentInputs)
    {
        ArgumentNullException.ThrowIfNull(validateCapture);
        ArgumentNullException.ThrowIfNull(verifyCurrentInputs);
        root=Path.GetFullPath(root);
        var comparison=OperatingSystem.IsWindows()?StringComparison.OrdinalIgnoreCase:StringComparison.Ordinal;
        string serial=Path.GetFullPath(options.SerialLog,root),race=serial+".race.json",summary=Path.GetFullPath(options.SummaryLog,root);
        Need(options.HasWebSignalRace&&!(options.WebSignalRace&&options.WebEventOwnedSignalRace)&&
            completion.EventOwned==options.WebEventOwnedSignalRace&&completion.Gate.EventOwned==completion.EventOwned&&
            !options.DryRun&&plan.IsEnabled("EnableM10004Probe")&&
            string.Equals(root,program.SourceRoot,comparison)&&string.Equals(root,completion.Root,comparison)&&
            string.Equals(race,completion.Path,comparison)&&string.Equals(summary,completion.SummaryPath,comparison)&&
            completion.Port==context.ForwardPort&&ReferenceEquals(runner,completion.Runner),"web_race_owned_owner");
        RequireCompleteProcess(runner,"web_race_owned_runner");
        Need(runner.Succeeded&&runner.StandardError.Length==0,"web_race_owned_disposition");
        AuthorityFile[] sources=[packageInput,dumpInput,imageInput,efiInput];
        string[] sourcePaths=[options.LinuxElfPackage,options.LinuxThreadingDisassembly,options.Image,options.EfiArtifact];
        long[] sourceLimits=[OS.Manifest.LinuxElfPackage.MaximumBytes,NativeAotWebProgram.MaximumDisassemblyBytes,64*1024*1024,16*1024*1024];
        inputs.VerifyUnchanged();
        for(int i=0;i<sources.Length;i++)
        {
            Need(sourcePaths[i].Length!=0&&string.Equals(Path.GetFullPath(sourcePaths[i],root),sources[i].Path,comparison),"web_race_owned_input_path");
            sources[i].CheckSize(sourceLimits[i],allowEmpty:false);sources[i].VerifyUnchanged();
        }
        using AuthorityFiles held=new();
        string[] paths=[serial,summary,serial+".pcap",race,race+".attempts.json",race+".wire.json",completion.RunnerOutputPath,completion.RunnerErrorPath];
        long[] limits=[8*1024*1024,16*1024*1024,4*1024*1024,64*1024,8*1024*1024,32*1024*1024,64*1024*1024,64*1024*1024];
        AuthorityFile[] originals=new AuthorityFile[paths.Length];
        for(int i=0;i<paths.Length;i++)
            originals[i]=held.Open(root,paths[i],"original web race output "+i,"web_race_owned_output",limits[i],allowEmpty:i>=6,singleLink:true);
        AuthorityFiles.RequireDistinct([..originals,..sources]);
        void Verify()
        {
            inputs.VerifyUnchanged();foreach(var source in sources)source.VerifyUnchanged();
            completion.Verify(held);held.VerifyUnchanged();
        }
        Verify();
        try
        {
            // Hold originals before the optional capture validator and every
            // subsequent read. Never reopen a path to replace a checked input.
            validateCapture();
            byte[] raw=originals[0].ReadBytes(limits[0],"web_race_owned_serial"),
                summaryBytes=originals[1].ReadBytes(limits[1],"web_race_owned_summary"),
                packets=originals[2].ReadBytes(limits[2],"web_race_owned_packets"),
                wire=originals[5].ReadBytes(limits[5],"web_race_owned_wire"),
                package=packageInput.ReadBytes(sourceLimits[0],"web_race_owned_package");
            string selected=originals[3].ReadUtf8(limits[3],"web_race_owned_selected"),attempts=originals[4].ReadUtf8(limits[4],"web_race_owned_attempts");
            var configuration=NativeAotWebProgram.DescribeConfiguration(package);
            Need(!configuration.LogQuota&&configuration.Body==completion.Health,"web_race_owned_configuration");
            var window=new WebUtcObservation(completion.ProducerStartedUtc,completion.ProducerCompletedUtc);
            var ownedRunner=InspectNativeAotWebRaceRunnerForTests(options,plan,runner,raw,summaryBytes,context,
                configuration.Body,configuration.Json,completion.Gate,window,completion.EventOwned);
            var controls=ownedRunner.Runner.Controls;
            Need(controls is not null,"web_race_owned_controls");
            var capture=ValidateNativeAotWebCapturePrefix(root,package,program,raw,controls,completion.Selection,completion.EventOwned);
            var replay=capture.Leaf;
            Need(replay.SignalRace is not null&&replay.SignalRace.EventOwned==completion.EventOwned&&replay.TransportControlsChecked,"web_race_owned_replay");
            int at=raw.AsSpan().IndexOf("TEST_START m10.004\r\n"u8);
            var framed=NativeAotThreadingOutput.ReadWeb(raw.AsSpan(at));
            string complete=framed.NormalizedTail[..(framed.NormalizedTail.Length-replay.Observations.TrailingFragment.Length)];
            string[] rows=complete[..^2].Split("\r\n",StringSplitOptions.None);
            var lifetimes=SocketLifetimes(rows,true,8,controls!.Sockets,signalRace:true);
            var whole=InspectNativeAotWebRacePacketsForTests(packets,capture.Parent.FrameAnchor,capture.Parent.Arp,
                lifetimes.Select(l=>l.RemotePort).ToArray(),context.ForwardPort,configuration.Body,configuration.Json,ownedRunner.Runner.UtcObservations,controls);
            Need(lifetimes.Count==replay.SocketAccepts&&whole.Ordinary.Connections+whole.Race.Streams.Count==lifetimes.Count,
                "web_race_owned_lifetimes");
            Need(whole.Ordinary.TimeWaitPorts.All(port=>lifetimes.Any(l=>l.RemotePort==port&&l.Waits>0)),"web_race_owned_time_wait");
            string endpoint="http://127.0.0.1:"+context.ForwardPort.ToString(System.Globalization.CultureInfo.InvariantCulture);
            var roles=InspectNativeAotWebSignalRaceRolesForTests(wire,attempts,selected,endpoint,configuration.Body,whole.Race,replay.SignalRace!);
            var dates=completion.EventOwned?
                InspectNativeAotWebEventOwnedSignalRaceDatesForTests(roles.Streams,window,whole.Ordinary.Responses[^1].DateUtc):
                InspectNativeAotWebSignalRaceDatesForTests(roles.Streams,window,whole.Ordinary.Responses[^1].DateUtc);
            output.WriteLine($"WEB_RACE_PREFIX parentRecords={capture.Parent.Records} calls={replay.Observations.Calls} accepts={replay.SocketAccepts} ordinary={whole.Ordinary.Connections} added={whole.Race.Streams.Count} roles={roles.Samples.Count} orderEdges={roles.Order.Count} dates={dates.Responses.Count} pendingHeaders={dates.PendingHeaders} acceptance=pending");
            // This continuing capture proves neither guest termination nor
            // actual GC/ThreadPool/offender cleanup. QemuSmoke's final native,
            // source and cancellation checks run while all outputs stay held.
            verifyCurrentInputs();
            return 0;
        }
        finally{Verify();}
    }
}
