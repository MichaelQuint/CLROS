using System.Text.Json;
using OS.Tooling;

internal static class HostResumeTests
{
    [HostTest("runner checkpoint reuses only intact completed evidence")]
    private static void CheckpointEvidence()
    {
        string root = HostToolProcess.FindRepositoryRoot(), directory = Scratch();
        string origin;
        using (var checkpoint = new HostRunCheckpoint(root, directory, false))
        {
            string log = checkpoint.Start("passed");
            File.WriteAllText(log, "TEST_START control\nTEST_PASS control\n");
            var result = new HostTestRunner.FixtureResult("passed", "Fast", 0, false, 1, log, 1, 1, 0, null);
            string output = Path.Combine(directory, "owned.bin");
            File.WriteAllBytes(output, [1, 2, 3]);
            checkpoint.Finish("passed", "identity", result, [output]);
            File.WriteAllBytes(output, [9]); // A later producer cannot overwrite retained evidence.
            HostCheck.True(checkpoint.TryReuse("passed", "identity", out var entry));
            origin = entry!.Origin;
            HostCheck.True(!checkpoint.TryReuse("passed", "changed", out _));
            HostCheck.True(!checkpoint.TryReuse("passed", "fresh:identity", out _));
            checkpoint.Finish("failed", "identity", result with { ExitCode = 1 });
            checkpoint.Start("interrupted");
            HostCheck.True(!checkpoint.TryReuse("failed", "identity", out _));
            HostCheck.True(!checkpoint.TryReuse("interrupted", "identity", out _));
            bool locked = false;
            try { using var second = new HostRunCheckpoint(root, directory, true); }
            catch (IOException) { locked = true; }
            HostCheck.True(locked, "simultaneous resume must refuse the active receipt");
        }
        using (var checkpoint = new HostRunCheckpoint(root, directory, true))
        {
            HostCheck.True(checkpoint.TryReuse("passed", "identity", out var entry));
            HostCheck.Equal(origin, entry!.Origin);
            File.Delete(entry.Evidence.Keys.Single());
            HostCheck.True(!checkpoint.TryReuse("passed", "identity", out _));
        }
        using (var worker = File.Open(Path.Combine(directory, "active.worker.lock"), FileMode.OpenOrCreate, FileAccess.Read, FileShare.Read))
        {
            bool locked = false;
            try { using var checkpoint = new HostRunCheckpoint(root, directory, true); }
            catch (IOException) { locked = true; }
            HostCheck.True(locked, "surviving worker must prevent resume");
        }
    }

    [HostTest("runner fingerprint follows test dependencies and exact selection")]
    private static void DependencyInvalidation()
    {
        Dictionary<string, string> sources = new(StringComparer.Ordinal)
        {
            ["First"] = "class First { int Test() => Shared.Value; }",
            ["Sibling"] = "class Sibling { int Test() => 1; }",
            ["Helpers"] = "class Shared { public const int Value = 1; }",
            ["External"] = "class External { string Input => \"artifacts/input.bin\"; }"
        };
        var before = new HostTestFingerprint(sources, "production-a");
        string first = before.For("First", "all"), sibling = before.For("Sibling", "all");
        sources["Helpers"] = "class Shared { public const int Value = 2; }";
        var helperChanged = new HostTestFingerprint(sources, "production-a");
        HostCheck.True(first != helperChanged.For("First", "all"));
        HostCheck.Equal(sibling, helperChanged.For("Sibling", "all"));
        HostCheck.True(sibling != helperChanged.For("Sibling", "one-case"));
        HostCheck.True(sibling != new HostTestFingerprint(sources, "production-b").For("Sibling", "all"));
        HostCheck.True(before.For("External", "all").StartsWith("fresh:", StringComparison.Ordinal));
        HostCheck.True(before.For("Missing", "all").StartsWith("fresh:", StringComparison.Ordinal));
        HostCheck.True(before.For("Sibling", "all", native: true).StartsWith("fresh:", StringComparison.Ordinal));
        sources["First"] = "class First { bool Test() => 1.Valid(); }";
        sources["Extensions"] = "static class Checks { public static bool Valid(this int value) => true; }";
        var extensionBefore = new HostTestFingerprint(sources, "production-a").For("First", "all");
        sources["Extensions"] = "static class Checks { public static bool Valid(this int value) => false; }";
        HostCheck.True(extensionBefore != new HostTestFingerprint(sources, "production-a").For("First", "all"));
        HostCheck.True(HostTestFingerprint.AffectsAllFixtures("global /* comment */ using System;"));
        HostCheck.True(HostTestFingerprint.AffectsAllFixtures("#if DEBUG\nglobal using Chosen = System.Int32;\n#endif", ["DEBUG"]));
        HostCheck.True(HostTestFingerprint.AffectsAllFixtures("[assembly: System.CLSCompliant(true)]"));
        HostCheck.True(HostTestFingerprint.AffectsAllFixtures("static class Extensions { extension(int value) { public bool Valid() => true; } }"));
    }

    [HostTest("runner resumes a focused selection without replaying passing checks")]
    private static void ResumeCommand()
    {
        string directory = Scratch();
        var first = Run("--filter", "BootInfoTests", "--results", directory);
        HostCheck.True(first.Succeeded, first.StandardError + first.StandardOutput);
        using var initial = JsonDocument.Parse(File.ReadAllText(Path.Combine(directory, "results.json")));
        string id = initial.RootElement.GetProperty("Results")[0].GetProperty("Id").GetString()!;
        var resumed = Run("--resume", Path.Combine(directory, "results.json"));
        HostCheck.True(resumed.Succeeded, resumed.StandardError + resumed.StandardOutput);
        HostCheck.True(resumed.StandardOutput.Contains("HOST_FIXTURE_REUSED " + id, StringComparison.Ordinal));
        HostCheck.True(!resumed.StandardOutput.Contains("TEST_START ", StringComparison.Ordinal));
        HostCheck.True(resumed.StandardOutput.Contains("TEST_PASS host.tests.focused", StringComparison.Ordinal));
        HostCheck.True(!resumed.StandardOutput.Split('\n').Any(line => line.Trim() == "TEST_PASS host.tests"));
        using var receipt = JsonDocument.Parse(File.ReadAllText(Path.Combine(directory, "results.json")));
        HostCheck.Equal(0, receipt.RootElement.GetProperty("Executed").GetInt32());
        HostCheck.True(receipt.RootElement.GetProperty("Reused").GetInt32() > 0);
        string log = receipt.RootElement.GetProperty("Results")[0].GetProperty("Log").GetString()!;
        File.AppendAllText(log, "tampered\n");
        var repaired = Run("--resume", directory);
        HostCheck.True(repaired.Succeeded && repaired.StandardOutput.Contains("HOST_FIXTURE_START " + id, StringComparison.Ordinal), repaired.StandardError + repaired.StandardOutput);
        File.WriteAllText(Path.Combine(directory, "checkpoint.json"), "{invalid");
        var corrupt = Run("--resume", directory);
        HostCheck.True(!corrupt.Succeeded && !corrupt.StandardOutput.Contains("HOST_FIXTURE_START", StringComparison.Ordinal));
    }

    [HostTest("runner native reuse requires reviewed ownership and exact selection")]
    private static void NativeOwnership()
    {
        var all = HostTestRunner.Discover();
        var owned = all.Where(f => f.Method.DeclaringType == typeof(M02OwnedIdtGatePopulationEfiIntegrationTests)).ToArray();
        var idtr = all.Where(f => f.Method.DeclaringType == typeof(M02IdtrContinuedExecutionEfiIntegrationTests)).ToArray();
        HostCheck.True(HostNativeResumePolicy.OwnsOutputs(owned));
        HostCheck.True(!HostNativeResumePolicy.OwnsOutputs(idtr));
        HostCheck.True(HostNativeResumePolicy.OwnsOutputs(idtr.Where(f => f.Method.Name == "TestDefaultAndEnabledEfiArtifacts").ToArray()));
        var result = new HostTestRunner.FixtureResult("owned", "Native", 0, false, 1, "log", 1, 1, 0, null) { ProcessId = 12345 };
        var outputs = HostNativeResumePolicy.Outputs(HostToolProcess.FindRepositoryRoot(), owned, result).ToArray();
        HostCheck.Equal(12, outputs.Length); // Five exported files and one producer log per variant.
        HostCheck.True(outputs.All(path => path.Contains("12345", StringComparison.Ordinal)));
        HostCheck.Equal(0, HostNativeResumePolicy.Outputs("unused", owned, result with { ExitCode = 1 }).Count());
    }

    [HostTest("runner refuses an interrupted pass with changed post-fixture inputs")]
    private static void InterruptedInputValidation()
    {
        string root = HostToolProcess.FindRepositoryRoot(), directory = Scratch();
        Directory.CreateDirectory(directory);
        string source = Path.Combine(directory, "input.txt");
        File.WriteAllText(source, "A");
        var files = new Dictionary<string, string> { [source] = HostRunCheckpoint.HashFile(source) };
        string resultDirectory = Path.Combine(directory, "results");
        using (var checkpoint = new HostRunCheckpoint(root, resultDirectory, false))
        {
            string log = checkpoint.Start("changed");
            File.WriteAllText(log, "TEST_START control\nTEST_PASS control\n");
            File.WriteAllText(source, "B");
            var result = new HostTestRunner.FixtureResult("changed", "Fast", 0, false, 1, log, 1, 1, 0, null);
            if (!HostTestFingerprint.FilesMatch(files)) result = result with { ExitCode = 1, Error = "inputs_changed_during_fixture" };
            checkpoint.Finish("changed", "A", result);
            // No whole-run completion occurs before disposal/interruption.
        }
        File.WriteAllText(source, "A");
        using var resumed = new HostRunCheckpoint(root, resultDirectory, true);
        HostCheck.True(!resumed.TryReuse("changed", "A", out _));
    }

    [HostTest("runner invalidation cannot be undone by a late passing fixture")]
    private static void InvalidationRejectsLateFinish()
    {
        string root = HostToolProcess.FindRepositoryRoot(), directory = Scratch();
        using (var checkpoint = new HostRunCheckpoint(root, directory, false))
        {
            string log = checkpoint.Start("before");
            File.WriteAllText(log, "TEST_START control\nTEST_PASS control\n");
            var result = new HostTestRunner.FixtureResult("before", "Fast", 0, false, 0, log, 1, 1, 0, null);
            checkpoint.Finish("before", "identity", result);
            HostCheck.True(checkpoint.TryReuse("before", "identity", out _));
            string late = checkpoint.Start("late");
            File.WriteAllText(late, "TEST_START control\nTEST_PASS control\n");
            checkpoint.InvalidateRun();
            checkpoint.Finish("late", "identity", result with { Id = "late", Log = late });
            HostCheck.True(!checkpoint.TryReuse("before", "identity", out _) && !checkpoint.TryReuse("late", "identity", out _));
        }
        using var resumed = new HostRunCheckpoint(root, directory, true);
        HostCheck.True(!resumed.TryReuse("before", "identity", out _) && !resumed.TryReuse("late", "identity", out _));
    }

    [HostTest("runner boundary stamps verify changed bytes and allow exact fixture restoration")]
    private static void BoundaryStampValidation()
    {
        string directory = Scratch();
        Directory.CreateDirectory(directory);
        string source = Path.Combine(directory, "input.cs");
        File.WriteAllText(source, "original");
        DateTime originalTime = new(2020, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        File.SetLastWriteTimeUtc(source, originalTime);
        var files = new Dictionary<string, string> { [source] = HostRunCheckpoint.HashFile(source) };
        var stamps = new Dictionary<string, HostTestFingerprint.FileStamp> { [source] = HostTestFingerprint.FileStamp.Read(source) };
        HostCheck.True(HostTestFingerprint.StampedFilesMatch(files, stamps));
        File.SetLastWriteTimeUtc(source, originalTime.AddDays(1));
        HostCheck.True(HostTestFingerprint.StampedFilesMatch(files, stamps), "restoring original bytes may change timestamps");
        File.WriteAllText(source, "modified");
        File.SetLastWriteTimeUtc(source, originalTime.AddDays(2));
        HostCheck.True(!HostTestFingerprint.StampedFilesMatch(files, stamps));
        File.WriteAllText(source, "original");
        HostCheck.True(HostTestFingerprint.StampedFilesMatch(files, stamps));
        File.Delete(source);
        HostCheck.True(!HostTestFingerprint.StampedFilesMatch(files, stamps), "deleted source is an input change");
    }

    [HostTest("runner boundary inventory rejects new source and untracked done evidence")]
    private static void BoundaryInventoryValidation()
    {
        string directory = Scratch();
        foreach (string child in new[] { "src", "std", "tools", "tests", "apps", "scripts", "done", ".gitea/backlog" })
            Directory.CreateDirectory(Path.Combine(directory, child));
        File.WriteAllText(Path.Combine(directory, "CLROS.slnx"), "<Solution />");
        File.WriteAllText(Path.Combine(directory, ".gitea/backlog/clros-issues.json"), "{}");
        var initialized = ProcessRunner.RunAsync(new("git", ["init", "--quiet", directory], directory)).GetAwaiter().GetResult();
        HostCheck.True(initialized.Succeeded, initialized.StandardError);
        var recorded = HostBuildInputs.SourceInputs(directory).ToDictionary(Path.GetFullPath, _ => "unused", StringComparer.OrdinalIgnoreCase);
        HostCheck.True(HostTestFingerprint.InventoryRecorded(directory, recorded));
        foreach (string name in new[] { "src/Added.cs", "done/step-new.md" })
        {
            string added = Path.Combine(directory, name);
            File.WriteAllText(added, "new input");
            HostCheck.True(!HostTestFingerprint.InventoryRecorded(directory, recorded), name);
            File.Delete(added);
            HostCheck.True(HostTestFingerprint.InventoryRecorded(directory, recorded));
        }
    }

    [HostTest("runner exact input hashes reject a binary replacement with preserved file stamps")]
    private static void ExactBinarySnapshotValidation()
    {
        string directory = Scratch();
        Directory.CreateDirectory(directory);
        string binary = Path.Combine(directory, "host-producer.dll");
        File.WriteAllBytes(binary, [1, 2, 3, 4]);
        var original = HostTestFingerprint.FileStamp.Read(binary);
        var files = new Dictionary<string, string> { [binary] = HostRunCheckpoint.HashFile(binary) };
        var stamps = new Dictionary<string, HostTestFingerprint.FileStamp> { [binary] = original };
        File.WriteAllBytes(binary, [4, 3, 2, 1]);
        File.SetCreationTimeUtc(binary, new DateTime(original.Created, DateTimeKind.Utc));
        File.SetLastWriteTimeUtc(binary, new DateTime(original.Written, DateTimeKind.Utc));
        HostCheck.True(HostTestFingerprint.StampedFilesMatch(files, stamps), "cheap guard alone cannot establish byte identity");
        HostCheck.True(!HostTestFingerprint.FilesMatch(files), "final exact snapshot rejects the replaced binary");
    }

    [HostTest("runner source parsing and raw hash use the same BOM-aware byte snapshot")]
    private static void SourceByteSnapshot()
    {
        string directory = Scratch();
        Directory.CreateDirectory(directory);
        string source = Path.Combine(directory, "input.cs");
        const string text = "// café\r\ninternal class Original {}";
        foreach (var encoding in new System.Text.Encoding[] { new System.Text.UTF8Encoding(true), System.Text.Encoding.Unicode, System.Text.Encoding.BigEndianUnicode })
        {
            File.WriteAllBytes(source, [.. encoding.GetPreamble(), .. encoding.GetBytes(text)]);
            var captured = HostTestFingerprint.ReadSource(source);
            HostCheck.Equal(text, captured.Text);
            HostCheck.Equal(HostRunCheckpoint.HashFile(source), captured.Sha256);
            File.WriteAllText(source, "internal class Changed {}");
            HostCheck.True(!HostTestFingerprint.FilesMatch(new Dictionary<string, string> { [source] = captured.Sha256 }));
        }
    }

    private static string Scratch() => Path.Combine(HostToolProcess.FindRepositoryRoot(), "artifacts", "host-resume-controls", Guid.NewGuid().ToString("N"));
    private static ProcessResult Run(params string[] args) => ProcessRunner.RunAsync(new("dotnet",
        [typeof(HostResumeTests).Assembly.Location, .. args], HostToolProcess.FindRepositoryRoot())
        { Timeout = TimeSpan.FromSeconds(60) }).GetAwaiter().GetResult();
}
