using System.Text;
using OS.Tooling;

internal static class NativeAotThreadPoolStressTests
{
    private static string Valid()
    {
        StringBuilder text=new("TEST_START nativeaot.tp-stress\n");
        for(int life=1;life<=3;life++)
        {
            int before=life*100;
            text.Append(FormattableString.Invariant($"NATIVEAOT_TP_STRESS_WORK lifetime={life} tasks=8 completed=8 checksum=204 activeMax=2 inFlight=0 mask=255 workers=2 threadPool=true\n"));
            text.Append(FormattableString.Invariant($"NATIVEAOT_TP_STRESS_CANCEL lifetime={life} before=2 during=2 workCallbacks=0 recovered=1\n"));
            text.Append(FormattableString.Invariant($"NATIVEAOT_TP_STRESS_RACE lifetime={life} outcome=result winners=1 duplicates=2 continuations=3 inline=0 value=43\n"));
            text.Append(FormattableString.Invariant($"NATIVEAOT_TP_STRESS_TIMER lifetime={life} before={before} fired={before+25} drained={before+45} after={before+70} frequency=1000 callbacks=1 completed=1 stale=0 never=0 pendingDrain=true\n"));
        }
        return text.Append("TEST_PASS nativeaot.tp-stress\n").ToString();
    }

    [HostTest("nativeaot.tp-stress complete observations require bounded work exclusive completion cancellation recovery and measured callback drain")]
    private static void Observations()
    {
        string valid=Valid();NativeAotThreadPoolStressGuest.ValidateOutput(valid);
        NativeAotThreadPoolStressGuest.ValidateOutput(valid.TrimEnd('\n'));
        NativeAotThreadPoolStressGuest.ValidateOutput(valid.Replace("\n","\r\n",StringComparison.Ordinal));
        NativeAotThreadPoolStressGuest.ValidateOutput(valid.Replace("outcome=result","outcome=cancel",StringComparison.Ordinal).Replace("value=43","value=0",StringComparison.Ordinal));
        foreach(string workers in new[]{"1","8"})NativeAotThreadPoolStressGuest.ValidateOutput(valid.Replace("workers=2","workers="+workers,StringComparison.Ordinal));
        foreach(string bad in new[]{"",new string('x',4097),valid+"\n",valid+"TEST_FAIL nativeaot.tp-stress late\n",
            valid.Replace("completed=8","completed=7",StringComparison.Ordinal),valid.Replace("checksum=204","checksum=0",StringComparison.Ordinal),
            valid.Replace("activeMax=2","activeMax=3",StringComparison.Ordinal),valid.Replace("inFlight=0","inFlight=1",StringComparison.Ordinal),
            valid.Replace("mask=255","mask=254",StringComparison.Ordinal),valid.Replace("workers=2","workers=0",StringComparison.Ordinal),
            valid.Replace("workers=2","workers=9",StringComparison.Ordinal),valid.Replace("threadPool=true","threadPool=false",StringComparison.Ordinal),
            valid.Replace("during=2","during=1",StringComparison.Ordinal),valid.Replace("workCallbacks=0","workCallbacks=1",StringComparison.Ordinal),
            valid.Replace("recovered=1","recovered=0",StringComparison.Ordinal),valid.Replace("winners=1","winners=2",StringComparison.Ordinal),
            valid.Replace("duplicates=2","duplicates=1",StringComparison.Ordinal),valid.Replace("continuations=3","continuations=2",StringComparison.Ordinal),
            valid.Replace("inline=0","inline=1",StringComparison.Ordinal),valid.Replace("outcome=result","outcome=cancel",StringComparison.Ordinal),
            valid.Replace("callbacks=1","callbacks=2",StringComparison.Ordinal),valid.Replace("completed=1","completed=0",StringComparison.Ordinal),
            valid.Replace("stale=0","stale=1",StringComparison.Ordinal),valid.Replace("never=0","never=1",StringComparison.Ordinal),
            valid.Replace("pendingDrain=true","pendingDrain=false",StringComparison.Ordinal),valid.Replace("fired=125","fired=124",StringComparison.Ordinal),
            valid.Replace("after=170","after=169",StringComparison.Ordinal),valid.Replace("drained=145","drained=124",StringComparison.Ordinal),
            valid.Replace("before=200","before=169",StringComparison.Ordinal),
            valid.Replace("frequency=1000","frequency=0",StringComparison.Ordinal),valid.Replace("frequency=1000","frequency=01000",StringComparison.Ordinal),
            valid.Replace("after=370","after=9223372036854775808",StringComparison.Ordinal),valid.Replace("lifetime=1","lifetime=01",StringComparison.Ordinal),
            valid.Replace("TEST_PASS","TEST_FAIL",StringComparison.Ordinal)})Reject(bad);
        string[] rows=valid.TrimEnd('\n').Split('\n');
        for(int i=0;i<rows.Length;i++)
        {
            Reject(string.Join('\n',rows.Where((_,at)=>at!=i)));
            string[] changed=(string[])rows.Clone();changed[i]+=" extra=1";Reject(string.Join('\n',changed));
            if(i+1<rows.Length){changed=(string[])rows.Clone();(changed[i],changed[i+1])=(changed[i+1],changed[i]);Reject(string.Join('\n',changed));}
        }
    }

    [HostTest("nativeaot.tp-stress capture file rejects invalid encoding size and late failure then releases its input")]
    private static void Files()
    {
        string root=HostToolProcess.FindRepositoryRoot(),directory=Path.Combine(root,"artifacts","tp-stress-files-"+Guid.NewGuid().ToString("N"));Directory.CreateDirectory(directory);
        string path=Path.Combine(directory,"stdout.log");
        try
        {
            File.WriteAllText(path,Valid(),new UTF8Encoding(false));NativeAotThreadPoolStressGuest.ValidateFile(path);HostCheck.Equal(Valid(),File.ReadAllText(path));
            File.AppendAllText(path,"TEST_FAIL nativeaot.tp-stress late\n");
            bool rejected=false;try{NativeAotThreadPoolStressGuest.ValidateFile(path);}catch(ToolFailure){rejected=true;}HostCheck.True(rejected);
            File.WriteAllBytes(path,[0xc0,0xaf]);rejected=false;
            try{NativeAotThreadPoolStressGuest.ValidateFile(path);}catch(DecoderFallbackException){rejected=true;}HostCheck.True(rejected);
            File.WriteAllBytes(path,new byte[4097]);rejected=false;
            try{NativeAotThreadPoolStressGuest.ValidateFile(path);}catch(ToolFailure){rejected=true;}HostCheck.True(rejected);
            using FileStream exclusive=new(path,FileMode.Open,FileAccess.ReadWrite,FileShare.None);
        }
        finally{Directory.Delete(directory,true);}
    }

    [HostTest("nativeaot.tp-stress managed Windows execution completes three real work cancellation publication and timer-drain rounds")]
    private static void ManagedExecution()
    {
        if(!OperatingSystem.IsWindows())return;
        string root=HostToolProcess.FindRepositoryRoot(),project="apps/NativeAotThreadPoolStress/NativeAotThreadPoolStress.csproj";
        string evidence=Path.Combine(root,"artifacts","tp-stress-managed-"+Guid.NewGuid().ToString("N"));Directory.CreateDirectory(evidence);
        var build=ProcessRunner.RunAsync(new("dotnet",["build",project,"-c","Release","--nologo","-m:1","-p:UseSharedCompilation=false","-nr:false"],root)
            {Timeout=TimeSpan.FromMinutes(2),OutputCharacterLimit=256*1024}).GetAwaiter().GetResult();
        File.WriteAllText(Path.Combine(evidence,"build.stdout.log"),build.StandardOutput);File.WriteAllText(Path.Combine(evidence,"build.stderr.log"),build.StandardError);
        HostCheck.True(build.Succeeded,build.StandardOutput+build.StandardError);
        string dll=Path.Combine(root,"apps/NativeAotThreadPoolStress/bin/Release/net11.0/NativeAotThreadPoolStress.dll");
        var run=ProcessRunner.RunAsync(new("dotnet",[dll],root){Timeout=TimeSpan.FromSeconds(90),OutputCharacterLimit=8192}).GetAwaiter().GetResult();
        File.WriteAllText(Path.Combine(evidence,"run.stdout.log"),run.StandardOutput);File.WriteAllText(Path.Combine(evidence,"run.stderr.log"),run.StandardError);
        Console.WriteLine("TP_STRESS_MANAGED_EVIDENCE path="+evidence);
        HostCheck.True(run.Succeeded && run.StandardError.Length==0,run.StandardOutput+run.StandardError);NativeAotThreadPoolStressGuest.ValidateOutput(run.StandardOutput);
        var invalid=ProcessRunner.RunAsync(new("dotnet",[dll,"unexpected"],root){Timeout=TimeSpan.FromSeconds(10),OutputCharacterLimit=8192}).GetAwaiter().GetResult();
        File.WriteAllText(Path.Combine(evidence,"invalid.stdout.log"),invalid.StandardOutput);File.WriteAllText(Path.Combine(evidence,"invalid.stderr.log"),invalid.StandardError);
        HostCheck.True(invalid.ExitCode!=0 && invalid.TerminationConfirmed && invalid.StandardOutput.Contains("TEST_FAIL nativeaot.tp-stress invalid_arguments",StringComparison.Ordinal) &&
            !invalid.StandardOutput.Contains("NATIVEAOT_TP_STRESS_WORK",StringComparison.Ordinal));
    }

    [HostTest("nativeaot.tp-stress remains excluded from original qualified payload and prepared-program admission")]
    private static void AdmissionBoundary()
    {
        HostCheck.True(!NativeAotThreadingPayload.Applications.Contains("NativeAotThreadPoolStress",StringComparer.Ordinal));
        bool rejected=false;
        try{NativeAotThreadingProgram.Read(HostToolProcess.FindRepositoryRoot(),"NativeAotThreadPoolStress","absent.pkg","absent.dump");}
        catch(ArgumentException error){rejected=error.Message.Contains("threading_package_app",StringComparison.Ordinal);}HostCheck.True(rejected);
    }
    private static void Reject(string text)
    {bool rejected=false;try{NativeAotThreadPoolStressGuest.ValidateOutput(text);}catch(ToolFailure){rejected=true;}HostCheck.True(rejected);}
}
