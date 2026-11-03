using OS.Tooling;

internal static class QemuProcessAuthorityTests
{
    [HostTest("qemu firmware discovery preserves explicit path and environment priority")]
    private static void FirmwareDiscovery()
    {
        string root = Path.Combine(HostToolProcess.FindRepositoryRoot(), "artifacts", "qemu-discovery-controls", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        string[] names = ["OS_OVMF_CODE", "OVMF_CODE", "QEMU_OVMF_CODE"];
        string?[] original = names.Select(Environment.GetEnvironmentVariable).ToArray();
        string[] candidates = names.Select(n => Path.Combine(root, "environment-" + n + ".fd")).ToArray();
        string local = Path.Combine(root, "OVMF.fd"), qemu = Path.Combine(root, "qemu-system-x86_64.exe");
        File.WriteAllBytes(local, []);
        try
        {
            for (int i = 0; i < names.Length; i++)
            {
                File.WriteAllBytes(candidates[i], []);
                Environment.SetEnvironmentVariable(names[i], candidates[i]);
            }
            HostCheck.Equal(local, QemuSmoke.ResolveOvmf(local, qemu));
            HostCheck.Equal<string?>(null, QemuSmoke.ResolveOvmf(Path.Combine(root, "absent.fd"), qemu));
            for (int i = 0; i < names.Length; i++)
            {
                HostCheck.Equal(candidates[i], QemuSmoke.ResolveOvmf(null, qemu));
                Environment.SetEnvironmentVariable(names[i], Path.Combine(root, "absent-" + i + ".fd"));
            }
            HostCheck.Equal(local, QemuSmoke.ResolveOvmf(null, qemu));
        }
        finally { for (int i = 0; i < names.Length; i++) Environment.SetEnvironmentVariable(names[i], original[i]); }
    }

    [HostTest("qemu refuses incomplete process authority before ordinary or expected panic classification")]
    private static void IncompleteProcesses()
    {
        string root = HostToolProcess.FindRepositoryRoot();
        foreach (int code in new[] { 0, 5 })
        {
            ProcessResult complete = new(code, "", "", false, false, false, TimeSpan.Zero);
            ProcessResult[] incomplete =
            [
                complete with { TerminationConfirmed = false },
                complete with { FailureReason = "CaptureFailed" },
                complete with { TimedOut = true },
                complete with { Cancelled = true },
                complete with { OutputLimitExceeded = true }
            ];
            foreach (ProcessResult result in incomplete)
            {
                var ordinary = QemuSmokeOptions.Parse("--dry-run", "--boot-policy", "Diagnostic");
                int exit = QemuSmoke.RunAsync(root, ordinary, TextWriter.Null,
                    (_, _) => Task.FromResult(result)).GetAwaiter().GetResult();
                HostCheck.Equal(1, exit);
                foreach (var options in new[]
                {
                    ordinary,
                    QemuSmokeOptions.Parse("--dry-run", "--build-efi", "--EnableM02UnexpectedVectorPanicProbe")
                })
                {
                    var plan = QemuProfiles.Create(options);
                    bool rejected = false;
                    try
                    {
                        QemuTranscripts.ValidateRun(root, options, plan, result, TextWriter.Null);
                    }
                    catch (ToolFailure error) when (error.Reason == "qemu_runner_incomplete") { rejected = true; }
                    HostCheck.True(rejected, "incomplete process status cannot reach a guest transcript");
                }
            }
            QemuTranscripts.RequireCompleteProcess(complete);
        }
    }
}
