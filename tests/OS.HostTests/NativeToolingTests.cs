using System.Diagnostics;
using System.Text.Json;
using OS.Tooling.Native;

internal static class NativeToolingTests
{
    [HostTest("native publisher reaps descendants after root exit and retains the real exit")]
    private static void PublisherCompletion()
    {
        string directory = Path.Combine(HostToolProcess.FindRepositoryRoot(), "artifacts/native-publisher-completion", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        string executable = OS.Tooling.HostTools.FindExecutable(null, "DOTNET_HOST_PATH", "dotnet")!;
        var time = Stopwatch.StartNew();
        foreach (int code in new[] { 0, 23 })
        {
            string sentinel = Path.Combine(directory, "descendant-" + code + ".lock");
            var result = BoundedJobProcessRunner.Run(executable,
                [typeof(NativeToolingTests).Assembly.Location, "--runner-control", "spawn-locked-child", sentinel, "exit-parent", code.ToString()],
                directory, 5, 4096, terminateDescendantsAfterRootExit: true);
            HostCheck.Equal(code == 0 ? "Success" : "NonZeroExit", result.Status);
            HostCheck.Equal(code, result.ExitCode);
            HostCheck.True(result.TerminationConfirmed && result.Output.Contains("parent-output", StringComparison.Ordinal) && result.Output.Contains("parent-error", StringComparison.Ordinal));
            using var unlocked = File.Open(sentinel, FileMode.Open, FileAccess.ReadWrite, FileShare.None);
        }
        foreach ((string control, string status) in new[] { ("invalid-utf8", "CaptureFailed"), ("output", "OutputOverflow") })
            HostCheck.Equal(status, BoundedJobProcessRunner.Run(executable,
                [typeof(NativeToolingTests).Assembly.Location, "--runner-control", control], directory, 5, 4096,
                terminateDescendantsAfterRootExit: true).Status);
        Console.WriteLine("PUBLISHER_COMPLETION_CONTROLS seconds=" + time.Elapsed.TotalSeconds.ToString("F3", System.Globalization.CultureInfo.InvariantCulture));
    }

    [HostTest("bounded native child environment is isolated and validated")]
    private static void ChildEnvironment()
    {
        string directory = HostToolProcess.FindRepositoryRoot();
        string executable = OS.Tooling.HostTools.FindExecutable(null, "DOTNET_HOST_PATH", "dotnet")!;
        const string name = "CLROS_TEST_CHILD_ENVIRONMENT";
        string? parent = Environment.GetEnvironmentVariable(name);
        string? path = Environment.GetEnvironmentVariable("PATH");
        var result = BoundedJobProcessRunner.Run(executable,
            [typeof(NativeToolingTests).Assembly.Location, "--runner-control", "environment", name, "PATH", "SystemRoot"], directory, 5, 4096,
            new Dictionary<string, string?> { [name] = "Unicode-\u03bb two words=value", ["path"] = null });
        HostCheck.Equal("Success", result.Status);
        string?[] values = JsonSerializer.Deserialize<string?[]>(result.Output)!;
        HostCheck.True(values.SequenceEqual(new[] { "Unicode-\u03bb two words=value", null, Environment.GetEnvironmentVariable("SystemRoot") }));
        HostCheck.Equal(parent, Environment.GetEnvironmentVariable(name));
        HostCheck.Equal(path, Environment.GetEnvironmentVariable("PATH"));
        foreach (var invalid in new Dictionary<string, string?>[]
        {
            new() { ["bad=name"] = "value" }, new() { ["bad\0name"] = "value" },
            new() { [name] = "bad\0value" }, new() { [""] = "value" },
            new(StringComparer.Ordinal) { ["NAME"] = "one", ["name"] = "two" }
        })
            HostCheck.Equal("InvalidEnvironment", BoundedJobProcessRunner.Run(executable, [], directory, 5, 4096, invalid).Status);
    }

    internal static bool VerifyJobRunner(string directory)
    {
        Directory.CreateDirectory(directory);
        string executable = OS.Tooling.HostTools.FindExecutable(null, "DOTNET_HOST_PATH", "dotnet")
            ?? throw new FileNotFoundException("dotnet host missing");
        string assembly = typeof(NativeToolingTests).Assembly.Location;
        BoundedJobProcessResult Control(string control, params string[] values) =>
            BoundedJobProcessRunner.Run(executable, [assembly, "--runner-control", control, .. values], directory, 5, 4096);
        var positive = Control("streams");
        HostCheck.Equal("Success", positive.Status);
        HostCheck.Equal(0, positive.ExitCode);
        HostCheck.True(positive.TerminationConfirmed && positive.Output.Contains("stdout-ok", StringComparison.Ordinal) && positive.Output.Contains("stderr-ok", StringComparison.Ordinal));
        var nonzero = Control("crash");
        HostCheck.Equal("NonZeroExit", nonzero.Status);
        HostCheck.Equal(23, nonzero.ExitCode);
        HostCheck.True(nonzero.TerminationConfirmed);
        var overflow = Control("output");
        HostCheck.Equal("OutputOverflow", overflow.Status);
        HostCheck.True(overflow.TerminationConfirmed && overflow.Output.Length <= 4096 && overflow.OutputCharacters <= 4096);
        var captureFailure = Control("invalid-utf8");
        HostCheck.Equal("CaptureFailed", captureFailure.Status);
        HostCheck.True(captureFailure.TerminationConfirmed);
        string[] literalArguments = ["", "two words", "quote\"here", "trailing slash\\", "line\nbreak", "$(nothing)"];
        var literal = Control("echo", literalArguments);
        HostCheck.Equal("Success", literal.Status);
        HostCheck.True(JsonSerializer.Deserialize<string[]>(literal.Output)!.SequenceEqual(literalArguments));
        string missing = Path.Combine(directory, "missing.exe");
        foreach (int seconds in new[] { 4, 3601 })
            HostCheck.Equal("InvalidTimeout", BoundedJobProcessRunner.Run(missing, [], directory, seconds, 4096).Status);
        foreach (int limit in new[] { 4095, 67108865 })
            HostCheck.Equal("InvalidOutputLimit", BoundedJobProcessRunner.Run(missing, [], directory, 5, limit).Status);
        HostCheck.Equal("StartFailed", BoundedJobProcessRunner.Run(missing, [], directory, 5, 4096).Status);
        foreach (string parentMode in new[] { "wait", "exit-parent" })
        {
            string sentinel = Path.Combine(directory, "descendant-" + parentMode + ".lock");
            var timeout = Control("spawn-locked-child", sentinel, parentMode);
            HostCheck.Equal("TimedOut", timeout.Status);
            HostCheck.True(timeout.TerminationConfirmed && File.Exists(sentinel));
            // A parent exit must not orphan a child, an inherited pipe, or a file lock.
            using var reopened = File.Open(sentinel, FileMode.Open, FileAccess.ReadWrite, FileShare.None);
        }
        int Handles()
        {
            using var process = Process.GetCurrentProcess();
            return process.HandleCount;
        }
        int baseline = Handles();
        for (int i = 0; i < 8; i++)
            HostCheck.Equal("Success", Control("echo").Status);
        GC.Collect();
        GC.WaitForPendingFinalizers();
        Thread.Sleep(200);
        int final = Handles();
        HostCheck.True(final <= baseline + 4, $"runner handle leak baseline={baseline} final={final}");
        Console.WriteLine($"BOUNDED_RUNNER_PROBE_PASS baselineHandles={baseline} finalHandles={final}");
        return true;
    }

    [HostTest("native file identity remains bound to its opened handle")]
    private static void FileIdentity()
    {
        string directory = Path.Combine(HostToolProcess.FindRepositoryRoot(), "artifacts", "native-identity-tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        string path = Path.Combine(directory, "file.bin");
        File.WriteAllBytes(path, [1, 2, 3]);
        using FileStream original = File.Open(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
        var before = NativeFile.GetIdentity(original.SafeFileHandle);
        File.Move(path, path + ".original");
        File.WriteAllBytes(path, [4, 5, 6]);
        using FileStream replacement = File.OpenRead(path);
        var stillOriginal = NativeFile.GetIdentity(original.SafeFileHandle);
        var replaced = NativeFile.GetIdentity(replacement.SafeFileHandle);
        HostCheck.Equal(before.Key, stillOriginal.Key);
        HostCheck.Equal(3UL, stillOriginal.Length);
        HostCheck.True(before.Key != replaced.Key && before.LinkCount == 1 && replaced.LinkCount == 1);
    }
}
