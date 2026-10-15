using System.Text;
using OS.Tooling;

internal static class NativeAotRuntimeMatrixTests
{
    private static string Valid()
    {
        StringBuilder text=new("TEST_START nativeaot.runtime-matrix\n");
        for(int round=1;round<=3;round++)
        {
            int before=round*3-2,progress=round*100,clock=round*1000;
            text.Append(FormattableString.Invariant($"NATIVEAOT_MATRIX_EH round={round} filters=1 catches=2 cleanups=2 order=12 identity=true\n"));
            text.Append(FormattableString.Invariant($"NATIVEAOT_MATRIX_CCTOR round={round} attempts=2 total={round*2} runs=1 typed=true innerIdentity=true\n"));
            text.Append(FormattableString.Invariant($"NATIVEAOT_MATRIX_DELEGATES round={round} closed={round+50} closure={round+47} calls=2 actions=3 order=122 sum=19\n"));
            text.Append(FormattableString.Invariant($"NATIVEAOT_MATRIX_GC round={round} collections=2 before={before} after={before+2} workers=2 joined=2 freshTls=2 roots=2 interior=2 tls=2 progressBefore={progress} progressReturned={progress+10} progressAfter={progress+20} finalizedMask={(1<<round)-1} duplicateFinalizers=0\n"));
            text.Append(FormattableString.Invariant($"NATIVEAOT_MATRIX_ASYNC round={round} tasks=4 completed=4 checksum=30 workers=2 canceledBefore=1 canceledDuring=1 workCallbacks=0 delayBefore={clock} delayAfter={clock+50} timerBefore={clock+100} fired={clock+150} drained={clock+160} after={clock+210} frequency=1000 callbacks=1 stale=0 disposed=true\n"));
        }
        return text.Append("TEST_PASS nativeaot.runtime-matrix\n").ToString();
    }

    [HostTest("nativeaot.runtime-matrix entire observations require compiler EH cached failure delegates roots finalization and async teardown")]
    private static void Observations()
    {
        string valid=Valid();NativeAotRuntimeMatrixGuest.ValidateOutput(valid);
        NativeAotRuntimeMatrixGuest.ValidateOutput(valid.TrimEnd('\n'));
        NativeAotRuntimeMatrixGuest.ValidateOutput(valid.Replace("\n","\r\n",StringComparison.Ordinal));
        foreach(string workers in new[]{"1","4"})NativeAotRuntimeMatrixGuest.ValidateOutput(valid.Replace("checksum=30 workers=2","checksum=30 workers="+workers,StringComparison.Ordinal));
        foreach(string bad in new[]{"",new string('x',4097),valid+"\n",valid+"TEST_FAIL nativeaot.runtime-matrix late\n",
            valid.Replace("filters=1","filters=0",StringComparison.Ordinal),valid.Replace("catches=2","catches=1",StringComparison.Ordinal),
            valid.Replace("cleanups=2","cleanups=1",StringComparison.Ordinal),valid.Replace("order=12 ","order=21 ",StringComparison.Ordinal),
            valid.Replace("identity=true","identity=false",StringComparison.Ordinal),valid.Replace("runs=1","runs=2",StringComparison.Ordinal),
            valid.Replace("total=4","total=3",StringComparison.Ordinal),valid.Replace("innerIdentity=true","innerIdentity=false",StringComparison.Ordinal),
            valid.Replace("calls=2","calls=1",StringComparison.Ordinal),valid.Replace("sum=19","sum=18",StringComparison.Ordinal),
            valid.Replace("closure=48","closure=47",StringComparison.Ordinal),valid.Replace("joined=2","joined=1",StringComparison.Ordinal),
            valid.Replace("freshTls=2","freshTls=1",StringComparison.Ordinal),valid.Replace("interior=2","interior=1",StringComparison.Ordinal),
            valid.Replace("roots=2","roots=0",StringComparison.Ordinal),valid.Replace("finalizedMask=3","finalizedMask=1",StringComparison.Ordinal),
            valid.Replace("duplicateFinalizers=0","duplicateFinalizers=1",StringComparison.Ordinal),valid.Replace("before=4","before=0",StringComparison.Ordinal),
            valid.Replace("after=3 ","after=2 ",StringComparison.Ordinal),valid.Replace("progressBefore=100","progressBefore=0",StringComparison.Ordinal),
            valid.Replace("progressReturned=110","progressReturned=99",StringComparison.Ordinal),valid.Replace("progressAfter=120","progressAfter=110",StringComparison.Ordinal),
            valid.Replace("completed=4","completed=3",StringComparison.Ordinal),valid.Replace("checksum=30 workers=2","checksum=30 workers=0",StringComparison.Ordinal),
            valid.Replace("checksum=30 workers=2","checksum=30 workers=5",StringComparison.Ordinal),valid.Replace("canceledDuring=1","canceledDuring=0",StringComparison.Ordinal),
            valid.Replace("workCallbacks=0","workCallbacks=1",StringComparison.Ordinal),valid.Replace("stale=0","stale=1",StringComparison.Ordinal),
            valid.Replace("disposed=true","disposed=false",StringComparison.Ordinal),valid.Replace("delayAfter=1050","delayAfter=1024",StringComparison.Ordinal),
            valid.Replace("fired=1150","fired=1124",StringComparison.Ordinal),valid.Replace("drained=1160","drained=1149",StringComparison.Ordinal),
            valid.Replace("after=1210","after=1184",StringComparison.Ordinal),valid.Replace("delayBefore=2000","delayBefore=1209",StringComparison.Ordinal),
            valid.Replace("frequency=1000","frequency=0",StringComparison.Ordinal),valid.Replace("frequency=1000","frequency=01000",StringComparison.Ordinal),
            valid.Replace("after=3210","after=24001",StringComparison.Ordinal),valid.Replace("after=3210","after=9223372036854775808",StringComparison.Ordinal),
            valid.Replace("round=1","round=01",StringComparison.Ordinal),valid.Replace("TEST_PASS","TEST_FAIL",StringComparison.Ordinal)})Reject(bad);
        string[] rows=valid.TrimEnd('\n').Split('\n'),frequency=(string[])rows.Clone();
        frequency[10]=frequency[10].Replace("frequency=1000","frequency=1001",StringComparison.Ordinal);Reject(string.Join('\n',frequency));
        for(int i=0;i<rows.Length;i++)
        {
            Reject(string.Join('\n',rows.Where((_,at)=>at!=i)));
            string[] changed=(string[])rows.Clone();changed[i]+=" extra=1";Reject(string.Join('\n',changed));
            if(i+1<rows.Length){changed=(string[])rows.Clone();(changed[i],changed[i+1])=(changed[i+1],changed[i]);Reject(string.Join('\n',changed));}
        }
    }

    [HostTest("nativeaot.runtime-matrix file observations reject malformed encoding size and late failures then release input")]
    private static void Files()
    {
        string root=HostToolProcess.FindRepositoryRoot(),directory=Path.Combine(root,"artifacts","matrix-files-"+Guid.NewGuid().ToString("N"));Directory.CreateDirectory(directory);
        string path=Path.Combine(directory,"stdout.log");
        try
        {
            File.WriteAllText(path,Valid(),new UTF8Encoding(false));NativeAotRuntimeMatrixGuest.ValidateFile(path);HostCheck.Equal(Valid(),File.ReadAllText(path));
            File.AppendAllText(path,"TEST_FAIL nativeaot.runtime-matrix late\n");bool rejected=false;
            try{NativeAotRuntimeMatrixGuest.ValidateFile(path);}catch(ToolFailure){rejected=true;}HostCheck.True(rejected);
            File.WriteAllBytes(path,[0xc0,0xaf]);rejected=false;
            try{NativeAotRuntimeMatrixGuest.ValidateFile(path);}catch(DecoderFallbackException){rejected=true;}HostCheck.True(rejected);
            File.WriteAllBytes(path,new byte[4097]);rejected=false;
            try{NativeAotRuntimeMatrixGuest.ValidateFile(path);}catch(ToolFailure){rejected=true;}HostCheck.True(rejected);
            using FileStream exclusive=new(path,FileMode.Open,FileAccess.ReadWrite,FileShare.None);
        }
        finally{Directory.Delete(directory,true);}
    }

    [HostTest("nativeaot.runtime-matrix Windows managed matrix and unchanged original runtime regression execute under bounded process owners")]
    private static void ManagedExecution()
    {
        if(!OperatingSystem.IsWindows())return;
        string root=HostToolProcess.FindRepositoryRoot(),evidence=Path.Combine(root,"artifacts","matrix-managed-"+Guid.NewGuid().ToString("N"));Directory.CreateDirectory(evidence);
        string Build(string name)
        {
            var build=ProcessRunner.RunAsync(new("dotnet",["build",$"apps/{name}/{name}.csproj","-c","Release","--nologo","-m:1","-p:UseSharedCompilation=false","-nr:false"],root)
                {Timeout=TimeSpan.FromMinutes(2),OutputCharacterLimit=256*1024}).GetAwaiter().GetResult();
            Save(name+".build",build);HostCheck.True(build.Succeeded,build.StandardOutput+build.StandardError);
            return Path.Combine(root,"apps",name,"bin/Release/net11.0",name+".dll");
        }
        void Save(string name,ProcessResult result)
        {File.WriteAllText(Path.Combine(evidence,name+".stdout.log"),result.StandardOutput);File.WriteAllText(Path.Combine(evidence,name+".stderr.log"),result.StandardError);}
        string matrix=Build("NativeAotRuntimeMatrix");
        var run=ProcessRunner.RunAsync(new("dotnet",[matrix],root){Timeout=TimeSpan.FromSeconds(120),OutputCharacterLimit=8192}).GetAwaiter().GetResult();
        Save("matrix.run",run);Console.WriteLine("RUNTIME_MATRIX_MANAGED_EVIDENCE path="+evidence);
        HostCheck.True(run.Succeeded && run.StandardError.Length==0,run.StandardOutput+run.StandardError);NativeAotRuntimeMatrixGuest.ValidateOutput(run.StandardOutput);
        var invalid=ProcessRunner.RunAsync(new("dotnet",[matrix,"unexpected"],root){Timeout=TimeSpan.FromSeconds(10),OutputCharacterLimit=8192}).GetAwaiter().GetResult();
        Save("matrix.invalid",invalid);
        HostCheck.True(invalid.ExitCode!=0 && invalid.TerminationConfirmed && !invalid.TimedOut && !invalid.Cancelled && !invalid.OutputLimitExceeded && invalid.FailureReason.Length==0 &&
            invalid.StandardOutput.Contains("TEST_FAIL nativeaot.runtime-matrix invalid_arguments",StringComparison.Ordinal) && !invalid.StandardOutput.Contains("NATIVEAOT_MATRIX_EH",StringComparison.Ordinal));
        string original=Build("NativeAotRuntimeSmoke");
        var regression=ProcessRunner.RunAsync(new("dotnet",[original],root){Timeout=TimeSpan.FromSeconds(120),OutputCharacterLimit=8192}).GetAwaiter().GetResult();Save("original.run",regression);
        HostCheck.True(regression.Succeeded && regression.StandardError.Length==0,regression.StandardOutput+regression.StandardError);
        HostCheck.Equal("TEST_START nativeaot.runtime\nNATIVEAOT_RUNTIME_EH_OK\nNATIVEAOT_RUNTIME_GC_THREADS_TLS_OK\nNATIVEAOT_RUNTIME_TASK_TIMER_OK\nTEST_PASS nativeaot.runtime\n",
            regression.StandardOutput.Replace("\r\n","\n",StringComparison.Ordinal));
    }

    [HostTest("nativeaot.runtime-matrix stays excluded from original canonical payload and prepared-program selectors")]
    private static void AdmissionBoundary()
    {
        HostCheck.True(!NativeAotThreadingPayload.Applications.Contains("NativeAotRuntimeMatrix",StringComparer.Ordinal));
        bool rejected=false;
        try{NativeAotThreadingProgram.Read(HostToolProcess.FindRepositoryRoot(),"NativeAotRuntimeMatrix","absent.pkg","absent.dump");}
        catch(ArgumentException error){rejected=error.Message.Contains("threading_package_app",StringComparison.Ordinal);}HostCheck.True(rejected);
    }
    private static void Reject(string text)
    {bool rejected=false;try{NativeAotRuntimeMatrixGuest.ValidateOutput(text);}catch(ToolFailure){rejected=true;}HostCheck.True(rejected);}
}
