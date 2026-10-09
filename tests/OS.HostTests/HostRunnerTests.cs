using System.Diagnostics;

internal static class HostRunnerTests
{
    [HostFixture]
    internal static void Run()
    {
        const string marker = "host.runner.checkpoints";
        Console.WriteLine("TEST_START " + marker);
        string root = Path.Combine(HostToolProcess.FindRepositoryRoot(), "artifacts", "host-runner-tests", Guid.NewGuid().ToString("N"));
        foreach (string area in new[] { "src", "std", "tools", "tests/OS.HostTests" }) Directory.CreateDirectory(Path.Combine(root, area));
        Write("tests/OS.HostTests/AlphaTests.cs", "class AlphaTests { HiddenType dependency; }");
        Write("tests/OS.HostTests/BetaTests.cs", "class HiddenType {}");
        Write("tests/OS.HostTests/OtherTests.cs", "class OtherTests {}");
        Write("tests/OS.HostTests/ExternalTests.cs", "class ExternalTests { /* Environment.GetEnvironmentVariable(variable) */ }");
        Write("tests/OS.HostTests/ScannerTests.cs", "class ScannerTests { /* Directory.EnumerateFiles(root) */ }");
        Write("src/input.txt", "original");
        ProcessStartInfo start = new("git") { WorkingDirectory = root, UseShellExecute = false, RedirectStandardOutput = true, RedirectStandardError = true };
        start.ArgumentList.Add("init");
        start.ArgumentList.Add("--quiet");
        using (Process process = Process.Start(start)!) { process.WaitForExit(); Need(process.ExitCode == 0, "temporary repository"); }
        string results = Path.Combine(root, "artifacts", "results"), alpha, scanner;
        using (HostRunCheckpoint checkpoint = new(root, results, false))
        {
            alpha = checkpoint.Fingerprint("AlphaTests");
            scanner = checkpoint.Fingerprint("ScannerTests");
            string log = checkpoint.Start("alpha", alpha);
            File.WriteAllText(log, "TEST_PASS synthetic.alpha\n");
            Write("artifacts/required.bin", "required evidence");
            checkpoint.Finish("alpha", true);
            Need(checkpoint.TryReuse("alpha", alpha, out _), "pass reused");
            string external = checkpoint.Fingerprint("ExternalTests");
            string externalLog = checkpoint.Start("external", external);
            File.WriteAllText(externalLog, "TEST_PASS synthetic.external\n");
            checkpoint.Finish("external", true);
            Need(!checkpoint.TryReuse("external", external, out _), "unknown external input always runs fresh");
            string failedLog = checkpoint.Start("failed", alpha);
            File.WriteAllText(failedLog, "TEST_FAIL synthetic.failed expected\n");
            checkpoint.Finish("failed", false);
            Need(!checkpoint.TryReuse("failed", alpha, out _), "failure rerun");
            checkpoint.Start("interrupted", alpha);
            Need(!checkpoint.TryReuse("interrupted", alpha, out _), "interrupted rerun");
        }
        using (HostRunCheckpoint checkpoint = new(root, results, true))
            Need(checkpoint.TryReuse("alpha", checkpoint.Fingerprint("AlphaTests"), out _), "pass survives restart");
        Write("tests/OS.HostTests/OtherTests.cs", "class OtherTests { /* unrelated fix */ }");
        using (HostRunCheckpoint checkpoint = new(root, results, true))
        {
            Need(checkpoint.Fingerprint("AlphaTests") == alpha, "unrelated fixture edit preserves pass");
            Need(checkpoint.Fingerprint("ScannerTests") != scanner, "broad source scanner invalidated");
        }
        Write("tests/OS.HostTests/BetaTests.cs", "class HiddenType { /* dependency fix */ }");
        using (HostRunCheckpoint checkpoint = new(root, results, true))
            Need(checkpoint.Fingerprint("AlphaTests") != alpha, "transitive fixture edit invalidated");
        Write("tests/OS.HostTests/BetaTests.cs", "class HiddenType {}");
        Write("src/input.txt", "changed");
        using (HostRunCheckpoint checkpoint = new(root, results, true))
            Need(checkpoint.Fingerprint("AlphaTests") != alpha, "shared input invalidated");
        Write("src/input.txt", "original");
        File.Delete(Path.Combine(root, "artifacts", "required.bin"));
        using (HostRunCheckpoint checkpoint = new(root, results, true))
            Need(!checkpoint.TryReuse("alpha", alpha, out _), "missing evidence rerun");
        Write("artifacts/required.bin", "required evidence");
        using (HostRunCheckpoint checkpoint = new(root, results, true))
        {
            Need(checkpoint.TryReuse("alpha", alpha, out _), "restored exact evidence");
            var receipt = System.Text.Json.JsonSerializer.Deserialize<HostRunCheckpoint.Receipt>(File.ReadAllText(Path.Combine(results, "result.json")))!;
            File.AppendAllText(receipt.Fixtures["alpha"].Log, "tampered");
            Need(!checkpoint.TryReuse("alpha", alpha, out _), "modified log rerun");
        }
        File.WriteAllText(Path.Combine(results, "result.json"), "{broken");
        bool corruptRejected = false;
        try { using HostRunCheckpoint invalid = new(root, results, true); }
        catch (System.Text.Json.JsonException) { corruptRejected = true; }
        Need(corruptRejected, "corrupt receipt rejected");
        Console.WriteLine("TEST_PASS " + marker);

        void Write(string path, string text) => File.WriteAllText(Path.Combine(root, path), text);
        static void Need(bool condition, string reason)
        {
            if (!condition) throw new InvalidOperationException("Checkpoint regression: " + reason);
        }
    }
}
