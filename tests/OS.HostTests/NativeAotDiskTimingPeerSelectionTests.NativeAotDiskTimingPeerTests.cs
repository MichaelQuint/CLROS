using OS.Tooling;

internal static partial class NativeAotDiskTimingPeerTests
{
    private const string SelectionFirst = "M16_008_SUPERVISOR identity=4294967297 action=1 transfers=2 ready=2 active=3\r\n" + "M16_008_TIMING identity=4294967297 epoch=7 start=10 arm=30 capture=40 ready=50 periodFs=100000000\r\n" + "M10_018_SELECTED version=1 sequence=1 identity=4294967297 selectionEpoch=6 returnEpoch=7 selected=20 periodFs=100000000\r\n";
    private const string SelectionSecond = "M16_008_SUPERVISOR identity=4294967298 action=1 transfers=4 ready=2 active=3\r\n" + "M10_018_SELECTED version=1 sequence=2 identity=4294967298 selectionEpoch=7 returnEpoch=7 selected=60 periodFs=100000000\r\n";
    private const string SelectionThird = "M16_008_SUPERVISOR identity=4294967297 action=1 transfers=6 ready=2 active=3\r\n" + "M16_008_TIMING identity=4294967297 epoch=9 start=65 arm=75 capture=80 ready=90 periodFs=100000000\r\n" + "M10_018_SELECTED version=1 sequence=3 identity=4294967297 selectionEpoch=7 returnEpoch=9 selected=70 periodFs=100000000\r\n";
    private const string SelectionFourth = "M16_008_SUPERVISOR identity=4294967298 action=1 transfers=8 ready=2 active=3\r\n" + "M10_018_SELECTED version=1 sequence=4 identity=4294967298 selectionEpoch=9 returnEpoch=9 selected=100 periodFs=100000000\r\n";
    private const string SelectionEnd = "M10_018_SELECTED_END version=1 lastSequence=4\r\n";
    private const string SelectionStream = SelectionFirst + SelectionSecond + SelectionThird + SelectionFourth + SelectionEnd;
    [HostTest("selected dispatch joins actual supervisor and IRQ epochs while retaining unpreempted turns")]
    private static void SelectionJoins()
    {
        var samples = NativeAotDiskTimingPeer.InspectSelections(SelectionStream);
        HostCheck.Equal(4, samples.Count);
        HostCheck.Equal(6UL, samples[0].SelectionEpoch);
        HostCheck.Equal(7UL, samples[0].ReturnEpoch);
        HostCheck.Equal(20UL, samples[0].Selected);
        HostCheck.Equal(7UL, samples[1].SelectionEpoch);
        HostCheck.Equal(7UL, samples[1].ReturnEpoch);
        HostCheck.Equal(9UL, samples[2].ReturnEpoch); // Do not assume exactly one epoch increment.
        HostCheck.Equal(100000000UL, samples[3].PeriodFemtoseconds);
        HostCheck.True(samples.SequenceEqual(NativeAotDiskTimingPeer.InspectSelections(SelectionStream + "unmeasured trailing text")));
    }

    [HostTest("selected dispatch rejects missing duplicate late partial and reordered completion records")]
    private static void SelectionCompleteness()
    {
        foreach (string bad in new[]
        {
            "",
            SelectionFirst,
            SelectionEnd,
            SelectionStream + SelectionEnd,
            SelectionStream.TrimEnd('\r', '\n'),
            SelectionStream + SelectionSecond,
            SelectionEnd + SelectionFirst,
            SelectionStream.Replace("lastSequence=4", "lastSequence=3"),
            SelectionStream.Replace("lastSequence=4", "lastSequence=5"),
            SelectionStream.Replace(SelectionSecond, ""),
            SelectionStream.Replace(SelectionSecond, SelectionSecond + SelectionSecond),
            SelectionStream.Replace("sequence=2", "sequence=3"),
            SelectionStream.Replace("M10_018_SELECTED version=1 sequence=2", "M10_018_SELECTED_UNKNOWN version=1 sequence=2"),
            SelectionSecond.Replace("sequence=2", "sequence=1") + "M10_018_SELECTED_END version=1 lastSequence=1\r\n"
        }

        )
            SelectionRejected(bad);
    }

    [HostTest("selected dispatch rejects wrong identity period epoch and timestamp joins without interpreting raw counts as duration")]
    private static void SelectionOwnership()
    {
        foreach (string bad in new[]
        {
            SelectionStream.Replace("SUPERVISOR identity=4294967297", "SUPERVISOR identity=4294967299"),
            SelectionStream.Replace("TIMING identity=4294967297", "TIMING identity=4294967299"),
            SelectionStream.Replace("epoch=7 start=10", "epoch=8 start=10"),
            SelectionStream.Replace("start=10", "start=21"),
            SelectionStream.Replace("arm=30", "arm=19"),
            SelectionStream.Replace("capture=40", "capture=31 ready=30").Replace(" ready=50", ""),
            SelectionStream.Replace("selectionEpoch=6 returnEpoch=7", "selectionEpoch=8 returnEpoch=7"),
            SelectionStream.Replace("selectionEpoch=7 returnEpoch=7", "selectionEpoch=6 returnEpoch=6"),
            SelectionStream.Replace("selected=60", "selected=19"),
            SelectionStream.Replace("selected=60 periodFs=100000000", "selected=60 periodFs=100000001"),
            SelectionStream.Replace("identity=4294967298 selectionEpoch", "identity=4294967296 selectionEpoch"),
            SelectionStream.Replace("identity=4294967298 selectionEpoch", "identity=2 selectionEpoch"),
            SelectionStream.Replace("identity=4294967298 selectionEpoch", "identity=4294967313 selectionEpoch"),
            SelectionStream.Replace(SelectionFirst, SelectionFirst.Replace("M16_008_TIMING", "other_timing")).Replace(SelectionThird, SelectionThird.Replace("M16_008_TIMING", "other_timing"))
        }

        )
            SelectionRejected(bad);
    }

    [HostTest("selected dispatch requires canonical bounded fields and preserves zero ticks when the joined clock permits them")]
    private static void SelectionNumbers()
    {
        foreach (string token in new[]
        {
            "-1",
            "+1",
            "01",
            "0x1",
            "18446744073709551616",
            "",
            "1 extra=1"
        }

        )
            SelectionRejected(SelectionStream.Replace("sequence=1 ", "sequence=" + token + " "));
        foreach (string bad in new[]
        {
            SelectionStream.Replace("version=1 sequence=1", "version=2 sequence=1"),
            SelectionStream.Replace("periodFs=100000000", "periodFs=0"),
            SelectionStream.Replace(" selectionEpoch=6", ""),
            SelectionStream.Replace("selected=20", "selected=20 selected=20"),
            SelectionStream.Replace("sequence=1 identity=4294967297", "identity=4294967297 sequence=1")
        }

        )
            SelectionRejected(bad);
        string zero = SelectionFirst.Replace("start=10", "start=0").Replace("selected=20", "selected=0") + "M10_018_SELECTED_END version=1 lastSequence=1\r\n";
        HostCheck.Equal(0UL, NativeAotDiskTimingPeer.InspectSelections(zero)[0].Selected);
    }

    private static void SelectionRejected(string text)
    {
        bool rejected = false;
        try
        {
            _ = NativeAotDiskTimingPeer.InspectSelections(text);
        }
        catch (ToolFailure)
        {
            rejected = true;
        }

        HostCheck.True(rejected, text);
    }
}
