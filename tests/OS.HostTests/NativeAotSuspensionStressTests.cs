using System.Text;
using OS.Tooling;

internal static class NativeAotSuspensionStressTests
{
    // Independent observation vector: counts advance globally, whereas each
    // fresh running worker has its own progress sequence.
    private static string Valid()
    {
        StringBuilder text = new("TEST_START nativeaot.gc-stress\n");
        for (int life = 1; life <= 3; life++)
        {
            for (int epoch = 1; epoch <= 3; epoch++)
            {
                int before = (life - 1) * 3 + epoch - 1, progress = epoch * 10;
                text.Append(FormattableString.Invariant($"NATIVEAOT_GC_STRESS_COLLECT lifetime={life} epoch={epoch} before={before} after={before + 1} progressBefore={progress} progressReturned={progress + 1} progressAfter={progress + 2} waiting=3\n"));
            }
            text.Append(FormattableString.Invariant($"NATIVEAOT_GC_STRESS_JOIN lifetime={life} started=5 joined=5 completed=5 roots=5 interior=5 tls=5 sleepStarts=1 exitRequests=1\n"));
        }
        return text.Append("TEST_PASS nativeaot.gc-stress\n").ToString();
    }

    [HostTest("nativeaot.gc-stress complete observations require global collections per-lifetime progress and all joined root witnesses")]
    private static void Observations()
    {
        string valid = Valid();
        NativeAotSuspensionStressGuest.ValidateOutput(valid);
        NativeAotSuspensionStressGuest.ValidateOutput(valid.TrimEnd('\n'));
        NativeAotSuspensionStressGuest.ValidateOutput(valid.Replace("\n", "\r\n", StringComparison.Ordinal));
        foreach (string bad in new[]{"",new string('x',4097),valid+"TEST_FAIL nativeaot.gc-stress late\n",valid+"\n",
            valid.Replace("waiting=3","waiting=2",StringComparison.Ordinal),valid.Replace("joined=5","joined=4",StringComparison.Ordinal),
            valid.Replace("completed=5","completed=6",StringComparison.Ordinal),valid.Replace("interior=5","interior=4",StringComparison.Ordinal),
            valid.Replace("tls=5","tls=0",StringComparison.Ordinal),valid.Replace("roots=5","roots=0",StringComparison.Ordinal),
            valid.Replace("sleepStarts=1","sleepStarts=0",StringComparison.Ordinal),valid.Replace("exitRequests=1","exitRequests=0",StringComparison.Ordinal),
            valid.Replace("before=3","before=0",StringComparison.Ordinal),valid.Replace("after=1 ","after=0 ",StringComparison.Ordinal),
            valid.Replace("progressBefore=10","progressBefore=0",StringComparison.Ordinal),valid.Replace("progressReturned=11","progressReturned=9",StringComparison.Ordinal),
            valid.Replace("progressAfter=12","progressAfter=11",StringComparison.Ordinal),valid.Replace("epoch=1","epoch=01",StringComparison.Ordinal),
            valid.Replace("before=0","before=-1",StringComparison.Ordinal),valid.Replace("progressAfter=12","progressAfter=9223372036854775808",StringComparison.Ordinal),
            valid.Replace("TEST_PASS","TEST_FAIL",StringComparison.Ordinal)})
            Reject(bad);
        string[] rows = valid.TrimEnd('\n').Split('\n');
        for (int i = 0; i < rows.Length; i++)
        {
            Reject(string.Join('\n', rows.Where((_, at) => at != i)));
            string[] duplicate = (string[])rows.Clone();
            duplicate[i] += " extra=1";
            Reject(string.Join('\n', duplicate));
            if (i + 1 < rows.Length)
            {
                duplicate = (string[])rows.Clone();
                (duplicate[i], duplicate[i + 1]) = (duplicate[i + 1], duplicate[i]);
                Reject(string.Join('\n', duplicate));
            }
        }
    }

    [HostTest("nativeaot.gc-stress file observations reject invalid encoding size and late failure without changing input")]
    private static void Files()
    {
        string root = HostToolProcess.FindRepositoryRoot(), directory = Path.Combine(root, "artifacts", "gc-stress-files-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        string path = Path.Combine(directory, "stdout.log");
        try
        {
            File.WriteAllText(path, Valid(), new UTF8Encoding(false));
            NativeAotSuspensionStressGuest.ValidateFile(path);
            string original = File.ReadAllText(path);
            HostCheck.Equal(Valid(), original);
            File.AppendAllText(path, "TEST_FAIL nativeaot.gc-stress late\n");
            bool rejected = false;
            try
            {
                NativeAotSuspensionStressGuest.ValidateFile(path);
            }
            catch (ToolFailure) { rejected = true; }
            HostCheck.True(rejected);
            File.WriteAllBytes(path, [0xc0, 0xaf]);
            rejected = false;
            try
            {
                NativeAotSuspensionStressGuest.ValidateFile(path);
            }
            catch (DecoderFallbackException) { rejected = true; }
            HostCheck.True(rejected);
            File.WriteAllBytes(path, new byte[4097]);
            rejected = false;
            try
            {
                NativeAotSuspensionStressGuest.ValidateFile(path);
            }
            catch (ToolFailure) { rejected = true; }
            HostCheck.True(rejected);
            using FileStream exclusive = new(path, FileMode.Open, FileAccess.ReadWrite, FileShare.None);
        }
        finally { Directory.Delete(directory, true); }
    }

    [HostTest("nativeaot.gc-stress managed Windows workload performs collections across five contexts and three joined lifetimes")]
    private static void ManagedExecution()
    {
        if (!OperatingSystem.IsWindows())
            return;
        string root = HostToolProcess.FindRepositoryRoot(), project = "apps/NativeAotSuspensionStress/NativeAotSuspensionStress.csproj";
        string evidence = Path.Combine(root, "artifacts", "gc-stress-managed-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(evidence);
        var build = ProcessRunner.RunAsync(new("dotnet", ["build", project, "-c", "Release", "--nologo", "-m:1", "-p:UseSharedCompilation=false", "-nr:false"], root)
        {
            Timeout = TimeSpan.FromMinutes(2),
            OutputCharacterLimit = 256 * 1024
        }).GetAwaiter().GetResult();
        File.WriteAllText(Path.Combine(evidence, "build.stdout.log"), build.StandardOutput);
        File.WriteAllText(Path.Combine(evidence, "build.stderr.log"), build.StandardError);
        HostCheck.True(build.Succeeded, build.StandardOutput + build.StandardError);
        string dll = Path.Combine(root, "apps/NativeAotSuspensionStress/bin/Release/net11.0/NativeAotSuspensionStress.dll");
        var run = ProcessRunner.RunAsync(new("dotnet", [dll], root)
        {
            Timeout = TimeSpan.FromSeconds(90),
            OutputCharacterLimit = 8192
        }).GetAwaiter().GetResult();
        File.WriteAllText(Path.Combine(evidence, "run.stdout.log"), run.StandardOutput);
        File.WriteAllText(Path.Combine(evidence, "run.stderr.log"), run.StandardError);
        Console.WriteLine("GC_STRESS_MANAGED_EVIDENCE path=" + evidence);
        HostCheck.True(run.Succeeded && run.StandardError.Length == 0, run.StandardOutput + run.StandardError);
        NativeAotSuspensionStressGuest.ValidateOutput(run.StandardOutput);
        var invalid = ProcessRunner.RunAsync(new("dotnet", [dll, "unexpected"], root)
        {
            Timeout = TimeSpan.FromSeconds(10),
            OutputCharacterLimit = 8192
        }).GetAwaiter().GetResult();
        File.WriteAllText(Path.Combine(evidence, "invalid.stdout.log"), invalid.StandardOutput);
        File.WriteAllText(Path.Combine(evidence, "invalid.stderr.log"), invalid.StandardError);
        HostCheck.True(invalid.ExitCode != 0 && invalid.TerminationConfirmed && invalid.StandardOutput.Contains("TEST_FAIL nativeaot.gc-stress invalid_arguments", StringComparison.Ordinal) &&
            !invalid.StandardOutput.Contains("NATIVEAOT_GC_STRESS_COLLECT", StringComparison.Ordinal));
    }

    [HostTest("nativeaot.gc-stress separate workload is excluded from the original pinned package and program selectors")]
    private static void AdmissionBoundary()
    {
        HostCheck.True(!NativeAotThreadingPayload.Applications.Contains("NativeAotSuspensionStress", StringComparer.Ordinal));
        bool rejected = false;
        try
        {
            NativeAotThreadingProgram.Read(HostToolProcess.FindRepositoryRoot(), "NativeAotSuspensionStress", "absent.pkg", "absent.dump");
        }
        catch (ArgumentException error) { rejected = error.Message.Contains("threading_package_app", StringComparison.Ordinal); }
        HostCheck.True(rejected);
    }

    private static void Reject(string text)
    {
        bool rejected = false;
        try
        {
            NativeAotSuspensionStressGuest.ValidateOutput(text);
        }
        catch (ToolFailure) { rejected = true; }
        HostCheck.True(rejected);
    }
}
