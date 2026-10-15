using OS.Tooling;

internal static class NativeAotThreadPoolSmokeTests
{
    internal const string Valid = "TEST_START nativeaot.threadpool\n" +
        "NATIVEAOT_THREADPOOL_WORK tasks=4 completed=4 checksum=30 workers=2 threadPool=true\n" +
        "NATIVEAOT_THREADPOOL_CONTINUATIONS before=1 race=1 after=1 count=3 value=43 publicationInline=0\n" +
        "NATIVEAOT_THREADPOOL_CANCELLATION before=2 during=1 callbacks=0\n" +
        "NATIVEAOT_THREADPOOL_DELAY requestedMs=50 before=100 after=150 frequency=1000 minimumMs=25\n" +
        "NATIVEAOT_THREADPOOL_TIMER dueMs=50 before=200 fired=250 frequency=1000 callbacks=1 stale=0 disposed=true\n" +
        "NATIVEAOT_THREADPOOL_DRAINED tasks=4 continuations=3 callbacks=1 stale=0\n" +
        "TEST_PASS nativeaot.threadpool\n";

    [HostTest("nativeaot.threadpool complete observations require real work continuations cancellation clocks and drained callbacks")]
    private static void Output()
    {
        NativeAotThreadingGuest.ValidateThreadPoolOutput(Valid);
        NativeAotThreadingGuest.ValidateThreadPoolOutput(Valid.TrimEnd('\n'));
        NativeAotThreadingGuest.ValidateThreadPoolOutput(Valid.Replace("\n","\r\n",StringComparison.Ordinal));
        foreach(string worker in new[]{"1","4"})NativeAotThreadingGuest.ValidateThreadPoolOutput(Valid.Replace("workers=2","workers="+worker,StringComparison.Ordinal));
        foreach(string invalid in new[]{"",new string('x',4097),Valid+"\n",Valid+"TEST_FAIL nativeaot.threadpool late\n",
            Valid.Replace("workers=2","workers=0",StringComparison.Ordinal),Valid.Replace("workers=2","workers=5",StringComparison.Ordinal),
            Valid.Replace("completed=4","completed=3",StringComparison.Ordinal),Valid.Replace("checksum=30","checksum=0",StringComparison.Ordinal),
            Valid.Replace("threadPool=true","threadPool=false",StringComparison.Ordinal),Valid.Replace("race=1","race=0",StringComparison.Ordinal),
            Valid.Replace("publicationInline=0","publicationInline=1",StringComparison.Ordinal),Valid.Replace("during=1","during=0",StringComparison.Ordinal),
            Valid.Replace("callbacks=0","callbacks=1",StringComparison.Ordinal),Valid.Replace("after=150","after=100",StringComparison.Ordinal),
            Valid.Replace("after=150","after=124",StringComparison.Ordinal),Valid.Replace("before=200","before=149",StringComparison.Ordinal),
            Valid.Replace("frequency=1000","frequency=0",StringComparison.Ordinal),Valid.Replace("frequency=1000","frequency=01000",StringComparison.Ordinal),
            Valid.Replace("fired=250","fired=224",StringComparison.Ordinal),Valid.Replace("fired=250","fired=9223372036854775808",StringComparison.Ordinal),
            Valid.Replace("stale=0","stale=1",StringComparison.Ordinal),Valid.Replace("disposed=true","disposed=false",StringComparison.Ordinal),
            Valid.Replace("TEST_PASS","TEST_FAIL",StringComparison.Ordinal)})Reject(invalid);
        string[] rows=Valid.TrimEnd('\n').Split('\n');
        for(int i=0;i<rows.Length;i++)
        {
            Reject(string.Join('\n',rows.Where((_,at)=>at!=i)));
            string[] changed=(string[])rows.Clone();changed[i]+=" extra=1";Reject(string.Join('\n',changed));
            if(i+1<rows.Length){changed=(string[])rows.Clone();(changed[i],changed[i+1])=(changed[i+1],changed[i]);Reject(string.Join('\n',changed));}
        }
    }

    [HostTest("nativeaot.threadpool capture command validates whole strict UTF8 output without runtime or guest authority")]
    private static void FileCommand()
    {
        string directory=Directory.CreateTempSubdirectory("clros-threadpool-output-").FullName,path=Path.Combine(directory,"stdout.log");
        try
        {
            File.WriteAllText(path,Valid);NativeAotThreadingGuest.ValidateThreadPoolFile(path);
            var result=HostToolProcess.RunTool("nativeaot","threading","validate-threadpool",path);
            HostCheck.True(result.ExitCode==0&&result.Output.Contains("TEST_PASS nativeaot.threadpool-output",StringComparison.Ordinal));
            File.AppendAllText(path,"TEST_FAIL nativeaot.threadpool late\n");
            result=HostToolProcess.RunTool("nativeaot","threading","validate-threadpool",path);
            HostCheck.True(result.ExitCode!=0&&!result.Output.Contains("TEST_PASS nativeaot.threadpool-output",StringComparison.Ordinal));
            File.WriteAllBytes(path,[0xc0,0xaf]);bool rejected=false;
            try{NativeAotThreadingGuest.ValidateThreadPoolFile(path);}catch(System.Text.DecoderFallbackException){rejected=true;}
            HostCheck.True(rejected);
        }
        finally{File.Delete(path);Directory.Delete(directory);}
    }

    [HostTest("nativeaot.threadpool managed Windows choreography executes owned tasks cancellation timer drain and invalid arguments")]
    private static void ManagedExecution()
    {
        if(!OperatingSystem.IsWindows())return; // The Linux NativeAOT qualifier owns Linux execution evidence.
        string root=HostToolProcess.FindRepositoryRoot(),project="apps/NativeAotThreadPoolSmoke/NativeAotThreadPoolSmoke.csproj";
        var build=ProcessRunner.RunAsync(new("dotnet",["build",project,"-c","Release","--nologo","-m:1","-p:UseSharedCompilation=false","-nr:false"],root)
            {Timeout=TimeSpan.FromMinutes(2),OutputCharacterLimit=256*1024}).GetAwaiter().GetResult();
        HostCheck.True(build.Succeeded,build.StandardOutput+build.StandardError);
        string dll=Path.Combine(root,"apps/NativeAotThreadPoolSmoke/bin/Release/net11.0/NativeAotThreadPoolSmoke.dll");
        var run=ProcessRunner.RunAsync(new("dotnet",[dll],root){Timeout=TimeSpan.FromSeconds(45),OutputCharacterLimit=8192}).GetAwaiter().GetResult();
        HostCheck.True(run.Succeeded&&run.StandardError.Length==0,run.StandardOutput+run.StandardError);
        NativeAotThreadingGuest.ValidateThreadPoolOutput(run.StandardOutput);
        var invalid=ProcessRunner.RunAsync(new("dotnet",[dll,"unexpected"],root){Timeout=TimeSpan.FromSeconds(10),OutputCharacterLimit=8192}).GetAwaiter().GetResult();
        HostCheck.True(invalid.ExitCode!=0&&invalid.TerminationConfirmed&&invalid.StandardOutput.Contains("TEST_FAIL nativeaot.threadpool invalid_arguments",StringComparison.Ordinal)&&
            !invalid.StandardOutput.Contains("NATIVEAOT_THREADPOOL_WORK",StringComparison.Ordinal));
    }

    private static void Reject(string text)
    {bool rejected=false;try{NativeAotThreadingGuest.ValidateThreadPoolOutput(text);}catch(ToolFailure){rejected=true;}HostCheck.True(rejected);}
}
