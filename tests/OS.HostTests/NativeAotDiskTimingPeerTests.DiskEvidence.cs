using OS.Tooling;

internal static partial class NativeAotDiskTimingPeerTests
{
    [HostTest("disk evidence rejects unknown modes missing or unselected images and invalid image bounds before starting a runner")]
    private static void InvalidDiskEvidenceOptions()
    {
        foreach (var options in new[]
        {
            new NativeAotDiskTimingPeer.BenchmarkOptions { DiskEvidence = (NativeAotDiskTimingPeer.DiskEvidenceMode)99 },
            new NativeAotDiskTimingPeer.BenchmarkOptions { DiskEvidence = NativeAotDiskTimingPeer.DiskEvidenceMode.Trace },
            new NativeAotDiskTimingPeer.BenchmarkOptions { DiskEvidence = NativeAotDiskTimingPeer.DiskEvidenceMode.Summary },
            new NativeAotDiskTimingPeer.BenchmarkOptions { DiskImageBytes = new byte[512] },
            new NativeAotDiskTimingPeer.BenchmarkOptions { DiskEvidence = NativeAotDiskTimingPeer.DiskEvidenceMode.Trace, DiskImageBytes = new byte[1] },
            new NativeAotDiskTimingPeer.BenchmarkOptions { DiskEvidence = NativeAotDiskTimingPeer.DiskEvidenceMode.Summary, DiskImageBytes = new byte[513] }
        })
        {
            int starts = 0;
            bool rejected = false;
            string serial = Path.Combine(Path.GetTempPath(), "clros-invalid-evidence-" + Guid.NewGuid().ToString("N"));
            try
            {
                _ = NativeAotDiskTimingPeer.RunAsync(".", serial, (_, _) =>
                {
                    starts++;
                    throw new InvalidOperationException("invalid disk evidence started a runner");
                }, null!, options).GetAwaiter().GetResult();
            }
            catch (ToolFailure error) when (error.Reason is "disk_timing_evidence_mode" or "disk_timing_evidence_image")
            {
                rejected = true;
            }
            HostCheck.True(rejected && starts == 0 && !File.Exists(serial) && !Directory.Exists(serial + ".disk-timing"));
        }
    }

    [HostTest("actual profile evidence selection reads an image only for the explicitly selected mode")]
    private static void DiskEvidenceProfileSelection()
    {
        foreach (string selected in new[] { "", "EnableM10018TimingProbe", "EnableM10018EvidenceTraceProbe", "EnableM10018EvidenceSummaryProbe" })
        {
            var flags = new Dictionary<string, bool>();
            if (selected.Length != 0)
                flags.Add(selected, true);
            int reads = 0;
            byte[] image = [1, 2, 3];
            var options = NativeAotDiskTimingPeer.BenchmarkOptions.ForProfile(EfiProfile.Create("", flags).Flags, () =>
            {
                reads++;
                return image;
            });
            var expected = selected == "EnableM10018EvidenceTraceProbe" ? NativeAotDiskTimingPeer.DiskEvidenceMode.Trace :
                selected == "EnableM10018EvidenceSummaryProbe" ? NativeAotDiskTimingPeer.DiskEvidenceMode.Summary :
                NativeAotDiskTimingPeer.DiskEvidenceMode.Original;
            HostCheck.Equal(expected, options.DiskEvidence);
            HostCheck.Equal(expected == NativeAotDiskTimingPeer.DiskEvidenceMode.Original ? 0 : 1, reads);
            HostCheck.True(expected == NativeAotDiskTimingPeer.DiskEvidenceMode.Original ? options.DiskImageBytes.IsEmpty :
                options.DiskImageBytes.Span.SequenceEqual(image));
        }
        bool rejected = false;
        int invalidReads = 0;
        try
        {
            _ = NativeAotDiskTimingPeer.BenchmarkOptions.ForProfile(new Dictionary<string, bool>
            {
                ["EnableM10018EvidenceTraceProbe"] = true,
                ["EnableM10018EvidenceSummaryProbe"] = true
            }, () =>
            {
                invalidReads++;
                return [1];
            });
        }
        catch (ToolFailure error) when (error.Reason == "disk_timing_evidence_mode")
        {
            rejected = true;
        }
        HostCheck.True(rejected && invalidReads == 0);
    }

    [HostTest("selected disk evidence image must match the independently inspected inventory before artifacts or runner creation")]
    private static void DiskEvidenceImageIdentity() => WebRootImageFixtureTests.WithImage((root, dir, image, source) =>
    {
        string applied = Path.Combine(dir, "evidence-image.raw");
        var receipt = WebRootImageFixture.Apply(root, image, source, WebRootImageFixture.SnapshotPublishedRoot(source), applied);
        var inventory = WebRootDiskInventory.Inspect(applied, File.ReadAllBytes(applied), receipt);
        string serial = Path.Combine(root, "artifacts", "tests", "evidence-image-" + Guid.NewGuid().ToString("N") + ".serial.log");
        int starts = 0;
        bool rejected = false;
        try
        {
            _ = NativeAotDiskTimingPeer.RunAsync(root, serial, (_, _) =>
            {
                starts++;
                throw new InvalidOperationException("foreign image started a runner");
            }, inventory, new NativeAotDiskTimingPeer.BenchmarkOptions
            {
                DiskEvidence = NativeAotDiskTimingPeer.DiskEvidenceMode.Trace,
                DiskImageBytes = new byte[512]
            }).GetAwaiter().GetResult();
        }
        catch (ToolFailure error) when (error.Reason == "disk_timing_evidence_image")
        {
            rejected = true;
        }
        HostCheck.True(rejected && starts == 0 && !File.Exists(serial) && !Directory.Exists(serial + ".disk-timing"));
    });
}
