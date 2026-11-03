namespace OS.Tooling;

public static partial class QemuSmoke
{
    // Successful observation deliberately stops and joins the runner before
    // natural completion can write its summary. Hold the diagnostic's required
    // serial/packet originals through the receipt and final capture checks.
    internal static AuthorityFile[] HoldHttpBenchmarkCaptures(string root,
        QemuSmokeOptions options, AuthorityFiles captures) =>
        new (string Path, int Maximum)[]
        {
            (options.SerialLog, DiskStaticCapturePolicy.SerialBytes),
            (options.SerialLog + ".pcap", DiskStaticCapturePolicy.PacketBytes)
        }.Select(input => captures.Open(root, Path.GetFullPath(input.Path, root),
            "HTTP diagnostic capture", "http_benchmark_capture", input.Maximum, singleLink: true)).ToArray();
}
