using OS.Tooling;
using Reads = OS.Tooling.NativeDiskReadObservations;

internal static partial class NativeDiskReadObservationTests
{
    [HostTest("disk sixteen sector final counter join distinguishes one request from sixteen sector reads")]
    private static void LargeReadParentCounters()
    {
        var fixture = FullParentCounterVector(37, 16);
        var device = Reads.Inspect(fixture.Rows, fixture.Image, fixture.Boundary, allowMultiSector: true, maximumReadSectors: 16);
        QemuTranscripts.InspectNativeAotDiskStaticEndValues(FullParentCounterEnd(fixture.Boundary, 19), fixture.Boundary, device);
        bool rejected = false;
        try
        {
            QemuTranscripts.InspectNativeAotDiskStaticEndValues(FullParentCounterEnd(fixture.Boundary, 4), fixture.Boundary, device);
        }
        catch (ToolFailure)
        {
            rejected = true;
        }
        HostCheck.True(rejected);
    }

    [HostTest("disk sixteen sector evidence requires explicit layout and preserves exact chunks counters and diagnostic boundaries")]
    private static void LargeReadEvidence()
    {
        var fixture = MultiVector(16);
        var result = Reads.Inspect(fixture.Rows, fixture.Image, fixture.Boundary, allowMultiSector: true, maximumReadSectors: 16);
        HostCheck.Equal(18, result.Rows.Count);
        HostCheck.Equal(1UL, result.Counters.Submitted);
        HostCheck.Equal(1UL, result.Counters.Completed);
        HostCheck.Equal(1UL, result.Counters.Copied);
        HostCheck.Equal(16, result.Rows.Count(row => row.Kind is Reads.Kind.Copied or Reads.Kind.CopyChunk));
        foreach (bool payload in new[] { false, true })
        {
            var diagnostic = Reads.InspectDiagnostic(DiagnosticRows(fixture, payload), fixture.Image, fixture.Boundary, payload, maximumReadSectors: 16);
            HostCheck.Equal(16, diagnostic.CopiedSectors);
            HostCheck.Equal(18, diagnostic.Rows);
        }
        Deny(fixture.Rows, 4);
        Deny(fixture.Rows.ToArray()[..^1], 16);
        var oversized = MultiVector(17);
        bool rejected = false;
        try
        {
            _ = Reads.Inspect(oversized.Rows, oversized.Image, oversized.Boundary, allowMultiSector: true, maximumReadSectors: 16);
        }
        catch (ToolFailure)
        {
            rejected = true;
        }
        HostCheck.True(rejected);
        void Deny(IReadOnlyList<string> rows, uint limit)
        {
            bool denied = false;
            try
            {
                _ = Reads.Inspect(rows, fixture.Image, fixture.Boundary, allowMultiSector: true, maximumReadSectors: limit);
            }
            catch (ToolFailure)
            {
                denied = true;
            }
            HostCheck.True(denied);
        }
    }
}
