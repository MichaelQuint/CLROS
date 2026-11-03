using System.Security.Cryptography;
using OS.Tooling;

internal static class NativeAotThreadingPayloadTests
{
    [HostTest("nativeaot.threading publication fixes separate app identities static musl SDK runtime and diagnostics policy")]
    private static void Publication()
    {
        HostCheck.True(NativeAotThreadingPayload.Applications.SequenceEqual(new[] { "NativeAotSuspensionSmoke", "NativeAotThreadPoolSmoke" }));
        foreach (string app in NativeAotThreadingPayload.Applications)
        {
            var args = NativeAotThreadingPayload.PublishArguments(app, "output");
            foreach (string value in new[]{"publish","apps/"+app+"/"+app+".csproj","Release","net11.0","linux-musl-x64","-p:PublishAot=true",
                "-p:StaticExecutable=true","-p:PositionIndependentExecutable=false","-p:ServerGarbageCollection=false","-p:EnableDiagnostics=false",
                "-p:RuntimeFrameworkVersion=11.0.0-rc.3.26522.148","-p:TargetLatestRuntimePatch=false","-p:UseSharedCompilation=false","-nr:false"})
                HostCheck.True(args.Contains(value));
            HostCheck.True(!ReferenceEquals(args, NativeAotThreadingPayload.PublishArguments(app, "output")));
            bool immutable = false;
            try
            {
                ((IList<string>)args)[0] = "restore";
            }
            catch (NotSupportedException)
            {
                immutable = true;
            }
            HostCheck.True(immutable);
        }
        foreach (string app in new[] { "NativeAotHello", "NativeAotRuntimeSmoke", "MinimalApiSmoke", "../NativeAotSuspensionSmoke", "nativeaotsuspensionsmoke" })
            Reject(() => NativeAotThreadingPayload.PublishArguments(app, "output"));
    }

    [HostTest("nativeaot.threading caches are preserved once and reject output overlap or invalid cache nodes before moving")]
    private static void Caches()
    {
        string root = Directory.CreateTempSubdirectory("clros-threading-cache-").FullName;
        const string app = "NativeAotSuspensionSmoke";
        string appRoot = Directory.CreateDirectory(Path.Combine(root, "apps", app)).FullName, output = Directory.CreateDirectory(Path.Combine(root, "output")).FullName;
        try
        {
            using var lease = NativeAotPayload.OpenQualificationLease(root);
            Directory.CreateDirectory(Path.Combine(appRoot, "bin"));
            Directory.CreateDirectory(Path.Combine(appRoot, "obj", "native"));
            File.WriteAllText(Path.Combine(appRoot, "bin", "old.dll"), "preserved-managed");
            File.WriteAllText(Path.Combine(appRoot, "obj", "native", "old.o"), "preserved-native");
            string overlap = Directory.CreateDirectory(Path.Combine(appRoot, "nested")).FullName;
            Reject(() => NativeAotThreadingPayload.ArchiveBuildCache(root, app, overlap));
            string other = Directory.CreateDirectory(Path.Combine(root, "apps", "NativeAotThreadPoolSmoke", "nested")).FullName;
            Reject(() => NativeAotThreadingPayload.ArchiveBuildCache(root, app, other));
            HostCheck.True(File.Exists(Path.Combine(appRoot, "bin", "old.dll")) && File.Exists(Path.Combine(appRoot, "obj", "native", "old.o")));
            NativeAotThreadingPayload.ArchiveBuildCache(root, app, output);
            HostCheck.True(!Directory.Exists(Path.Combine(appRoot, "bin")) && !Directory.Exists(Path.Combine(appRoot, "obj")));
            HostCheck.Equal("preserved-managed", File.ReadAllText(Path.Combine(output, "retained-build-cache", app, "bin", "old.dll")));
            HostCheck.Equal("preserved-native", File.ReadAllText(Path.Combine(output, "retained-build-cache", app, "obj", "native", "old.o")));
            Reject(() => NativeAotThreadingPayload.ArchiveBuildCache(root, app, output));
            File.WriteAllText(Path.Combine(appRoot, "bin"), "not-directory");
            string fresh = Directory.CreateDirectory(Path.Combine(root, "second-output")).FullName;
            Reject(() => NativeAotThreadingPayload.ArchiveBuildCache(root, app, fresh));
            HostCheck.Equal("not-directory", File.ReadAllText(Path.Combine(appRoot, "bin")));
        }
        finally
        {
            Directory.Delete(root, true);
        }
    }

    [HostTest("nativeaot.threading per-thread trace inventory binds exact files and rejects incomplete fatal malformed or substituted traces")]
    private static void Traces()
    {
        string root = Directory.CreateTempSubdirectory("clros-threading-trace-").FullName;
        const string app = "NativeAotSuspensionSmoke";
        string first = Path.Combine(root, app + "-trace.41"), second = Path.Combine(root, app + "-trace.42");
        const string valid = "execve(\"/app\", [\"/app\"], 0) = 0\ngetpid() = 41\nexit_group(0) = ?\n+++ exited with 0 +++\n";
        try
        {
            Reject(() => NativeAotThreadingPayload.ReadTraceInventory(root, app));
            File.WriteAllText(first, valid);
            File.WriteAllText(second, "futex(0, FUTEX_WAIT, 0, NULL) = 0\nexit(0) = ?\n+++ exited with 0 +++\n");
            var report = NativeAotThreadingPayload.ReadTraceInventory(root, app);
            HostCheck.True(report.Files.Count == 2 && report.Syscalls.SequenceEqual(new[] { "execve", "exit", "exit_group", "futex", "getpid" }));
            HostCheck.Equal(Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(first))), report.Files[Path.GetFileName(first)]);
            foreach (string text in new[]{valid.Replace("exit_group","unsupported",StringComparison.Ordinal),valid.Replace("execve","unsupported",StringComparison.Ordinal),
                valid.Replace("with 0","with 1",StringComparison.Ordinal),valid+"+++ killed by SIGSEGV +++\n",valid[..^10]})
            {
                File.WriteAllText(first, text);
                Reject(() => NativeAotThreadingPayload.ReadTraceInventory(root, app));
            }
            File.WriteAllBytes(first, [0xc0, 0xaf]);
            Reject(() => NativeAotThreadingPayload.ReadTraceInventory(root, app));
            File.WriteAllText(first, valid);
            string bad = Path.Combine(root, app + "-trace.041");
            File.WriteAllText(bad, valid);
            Reject(() => NativeAotThreadingPayload.ReadTraceInventory(root, app));
        }
        finally
        {
            Directory.Delete(root, true);
        }
    }

    [HostTest("nativeaot.threading qualifier rejects unsupported host and malformed command without creating output or emitting success")]
    private static void Preflight()
    {
        string root = HostToolProcess.FindRepositoryRoot(), path = Path.Combine(Path.GetTempPath(), "clros-threading-uncreated-" + Guid.NewGuid().ToString("N"));
        using StringWriter invalid = new();
        int code = NativeAotThreadingPayload.RunAsync(root, ["qualify"], invalid).GetAwaiter().GetResult();
        HostCheck.True(code == 1 && !Path.Exists(path) && !invalid.ToString().Contains("TEST_PASS", StringComparison.Ordinal));
        if (!OperatingSystem.IsLinux())
        {
            using StringWriter output = new();
            code = NativeAotThreadingPayload.RunAsync(root, ["qualify", "--output", path], output).GetAwaiter().GetResult();
            HostCheck.True(code == 1 && !Path.Exists(path) && output.ToString().Contains("linux_builder_required", StringComparison.Ordinal) && !output.ToString().Contains("TEST_PASS", StringComparison.Ordinal));
        }
    }

    private static void Reject(Action action)
    {
        bool rejected = false;
        try
        {
            action();
        }
        catch (Exception error) when (error is InvalidDataException or ToolFailure or System.Text.DecoderFallbackException)
        {
            rejected = true;
        }
        HostCheck.True(rejected);
    }
}
