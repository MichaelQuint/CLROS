using OS.Tooling;

internal static partial class HttpWorkloadBenchmarkTests
{
    [HostTest("stopped HTTP diagnostic holds serial and packets without requiring a natural summary")]
    private static void StoppedCaptureInputs()
    {
        var (root, options) = CaptureFiles(true);
        using AuthorityFiles captures = new();
        var held = QemuSmoke.HoldHttpBenchmarkCaptures(root, options, captures);
        HostCheck.Equal(2, held.Length);
        HostCheck.True(held[0].Path == options.SerialLog && held[1].Path == options.SerialLog + ".pcap");
        HostCheck.True(!Path.Exists(options.SummaryLog));
        captures.VerifyUnchanged();
    }

    [HostTest("stopped HTTP diagnostic still rejects missing packet capture")]
    private static void RequiredCaptureInputs()
    {
        var (root, options) = CaptureFiles(false);
        using AuthorityFiles captures = new();
        bool rejected = false;
        try
        {
            _ = QemuSmoke.HoldHttpBenchmarkCaptures(root, options, captures);
        }
        catch (ToolFailure error)
        {
            rejected = error.Reason.StartsWith("http_benchmark_capture_", StringComparison.Ordinal);
        }
        HostCheck.True(rejected);
    }

    private static (string Root, QemuSmokeOptions Options) CaptureFiles(bool packets)
    {
        string root = Repository.FindRoot();
        string directory = Fresh(root);
        Directory.CreateDirectory(directory);
        var options = QemuSmokeOptions.Parse("-SerialLog", Path.Combine(directory, "serial.log"),
            "-SummaryLog", Path.Combine(directory, "summary.log"));
        File.WriteAllText(options.SerialLog, "retained diagnostic prefix\n");
        if (packets)
            File.WriteAllBytes(options.SerialLog + ".pcap",
                [0xd4, 0xc3, 0xb2, 0xa1, 2, 0, 4, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0xff, 0xff, 0, 0, 1, 0, 0, 0]);
        return (root, options);
    }
}
