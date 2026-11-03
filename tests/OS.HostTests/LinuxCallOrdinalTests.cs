using OS.Tooling;

internal static class LinuxCallOrdinalTests
{
    [HostTest("linux call ordinals bind independent generations and leave original records immutable")]
    private static void Generations()
    {
        string[] rows = ["header","M16_008_CALL identity=4294967297 nr=39 ordinal=1","M16_008_CALL identity=4294967298 nr=39 ordinal=1",
            "M16_008_CALL identity=4294967297 nr=186 ordinal=2","M16_008_CALL identity=8589934594 nr=39 ordinal=1"];
        var parsed = QemuTranscripts.ValidateLinuxCallOrdinals(rows);
        HostCheck.True(parsed[0] == "header" && parsed[3] == "M16_008_CALL identity=4294967297 nr=186" && rows[3].EndsWith(" ordinal=2", StringComparison.Ordinal));
    }
    [HostTest("linux call ordinals reject missing lost duplicate reordered overflow and noncanonical counters")]
    private static void Rejections()
    {
        foreach (string value in new[] { "", " ordinal=0", " ordinal=01", " ordinal=2", " ordinal=4097", " ordinal=18446744073709551616", " ordinal=1 extra" })
            Reject(["M16_008_CALL identity=4294967297 nr=39" + value]);
        Reject(["M16_008_CALL identity=4294967297 nr=39 ordinal=1", "M16_008_CALL identity=4294967297 nr=186 ordinal=1"]);
        Reject(["M16_008_CALL identity=4294967297 nr=39 ordinal=1", "M16_008_CALL identity=4294967297 nr=186 ordinal=3"]);
    }
    internal static string[] Annotate(IEnumerable<string> rows)
    {
        Dictionary<string, int> counts = [];
        return rows.Select(row =>
        {
            if (!row.StartsWith("M16_008_CALL ", StringComparison.Ordinal))
                return row;
            string identity = row.Split(' ')[1];
            counts.TryGetValue(identity, out int count);
            counts[identity] = ++count;
            return row + " ordinal=" + count;
        }).ToArray();
    }
    [HostTest("linux call current wait observations preserve position generation deadline sequence and immutable values")]
    private static void WaitObservations()
    {
        string[] rows = ["header",
            "M16_008_CALL identity=4294967297 nr=202 ordinal=1 waitSource=1 waitDeadline=1050 waitSequence=7",
            "M16_008_CALL identity=4294967298 nr=0 ordinal=1 waitSource=2 waitDeadline=0 waitSequence=8",
            "M16_008_CALL identity=4294967297 nr=202 ordinal=2 waitSource=1 waitDeadline=0 waitSequence=9",
            "M16_008_CALL identity=8589934594 nr=39 ordinal=1 waitSource=0 waitDeadline=0 waitSequence=0"];
        string[] before = (string[])rows.Clone();
        var values = QemuTranscripts.ReadLinuxCallWaits(rows);
        HostCheck.Equal(4, values.Count);
        HostCheck.Equal(new QemuTranscripts.LinuxCallWaitObservation(1, 4294967297, 1, 1, 1050, 7), values[0]);
        HostCheck.Equal(new QemuTranscripts.LinuxCallWaitObservation(2, 4294967298, 1, 2, 0, 8), values[1]);
        HostCheck.True(values[2].Ordinal == 2 && values[2].Deadline == 0 && values[2].Sequence == 9 && values[3].Identity == 8589934594 && values[3].Source == 0);
        var stripped = QemuTranscripts.ValidateLinuxCallOrdinals(rows);
        HostCheck.Equal("M16_008_CALL identity=4294967297 nr=202", stripped[1]);
        HostCheck.True(rows.SequenceEqual(before));
        Array.Fill(rows, "changed");
        HostCheck.True(values[0].Deadline == 1050 && values is not QemuTranscripts.LinuxCallWaitObservation[]);
        if (values is IList<QemuTranscripts.LinuxCallWaitObservation> list)
        {
            bool denied = false;
            try
            {
                list[0] = default;
            }
            catch (NotSupportedException) { denied = true; }
            HostCheck.True(denied);
        }
        // Old consumer compatibility never supplies a missing witness to the new reader.
        RejectWait(["M16_008_CALL identity=4294967297 nr=39 ordinal=1"]);
        RejectWait([before[1], "M16_008_CALL identity=4294967298 nr=39 ordinal=1"]);
    }
    [HostTest("linux call wait observations reject partial duplicated inconsistent noncanonical and overflowing witnesses")]
    private static void WaitFailures()
    {
        const string row = "M16_008_CALL identity=4294967297 nr=202 ordinal=1";
        foreach (string suffix in new[]{
            " waitSource=0", " waitSource=1 waitDeadline=1050", " waitSource=1 waitDeadline=1050 waitSequence=0",
            " waitSource=0 waitDeadline=1 waitSequence=0", " waitSource=0 waitDeadline=0 waitSequence=1",
            " waitSource=2 waitDeadline=1050 waitSequence=1", " waitSource=3 waitDeadline=0 waitSequence=1",
            " waitSource=01 waitDeadline=0 waitSequence=1", " waitSource=1 waitDeadline=01050 waitSequence=1",
            " waitSource=1 waitDeadline=0 waitSequence=01", " waitSource=1 waitDeadline=-1 waitSequence=1",
            " waitSource=1 waitDeadline=9223372036854775808 waitSequence=1",
            " waitSource=1 waitDeadline=18446744073709551616 waitSequence=1",
            " waitSource=1 waitDeadline=0 waitSequence=18446744073709551616",
            " waitSource=0 waitDeadline=0 waitSequence=0 extra",
            " waitSource=0 waitDeadline=0 waitSequence=0 waitSource=0",
            " ordinal=1 waitSource=0 waitDeadline=0 waitSequence=0"})
        {
            Reject([row + suffix]);
            RejectWait([row + suffix]);
        }
        RejectWait(["M16_008_CALL identity=4294967297 nr=202 waitSource=1 ordinal=1 waitSource=0 waitDeadline=0 waitSequence=0"]);
        RejectWait([row + " waitSource=0 waitDeadline=0 waitSequence=0", row + " waitSource=0 waitDeadline=0 waitSequence=0"]);
    }
    private static void RejectWait(string[] rows)
    {
        bool rejected = false;
        try
        {
            QemuTranscripts.ReadLinuxCallWaits(rows);
        }
        catch (ToolFailure) { rejected = true; }
        HostCheck.True(rejected);
    }
    [HostTest("linux call return observations require actual frame shape canonical restored envelope and immutable generation binding")]
    private static void ReturnObservations()
    {
        const string prefix = "M16_008_CALL identity=4294967297 nr=15 ordinal=1 waitSource=0 waitDeadline=0 waitSequence=0";
        const string suffix = " signalDepth=0 signalFrame=0 blockedSignals=0 returnRip=4194818 returnRsp=1879048176 returnFlags=514 returnFs=4294967584 returnGs=0 returnValueBits=18446744073709551605";
        var rows = new[] { prefix + suffix };
        var values = QemuTranscripts.ReadLinuxCallReturns(rows);
        HostCheck.True(values.Count == 1 && values[0].Identity == 4294967297 && values[0].Rip == 0x400202 && values[0].ValueBits == unchecked((ulong)-11L));
        HostCheck.Equal("M16_008_CALL identity=4294967297 nr=15", QemuTranscripts.ValidateLinuxCallOrdinals(rows)[0]);
        rows[0] = "changed";
        HostCheck.True(values[0].Rsp == 1879048176 && values is not QemuTranscripts.LinuxCallReturnObservation[]);
        foreach (string changed in new[]{"",suffix.Replace("signalDepth=0","signalDepth=5",StringComparison.Ordinal),
            suffix.Replace("signalDepth=0","signalDepth=1",StringComparison.Ordinal),suffix.Replace("signalFrame=0","signalFrame=4104",StringComparison.Ordinal),
            suffix.Replace("returnRip=4194818","returnRip=04194818",StringComparison.Ordinal),suffix.Replace("returnFs=4294967584","returnFs=18446744073709551616",StringComparison.Ordinal),
            suffix.Replace(" returnValueBits=18446744073709551605","",StringComparison.Ordinal),suffix+" extra"})
        {
            bool rejected = false;
            try
            {
                QemuTranscripts.ReadLinuxCallReturns([prefix + changed]);
            }
            catch (ToolFailure) { rejected = true; }
            HostCheck.True(rejected);
        }
        var nested = QemuTranscripts.ReadLinuxCallReturns([prefix + suffix.Replace("signalDepth=0 signalFrame=0", "signalDepth=1 signalFrame=4104", StringComparison.Ordinal)]);
        HostCheck.True(nested[0].SignalDepth == 1 && nested[0].SignalFrame == 4104);
    }
    private static void Reject(string[] rows)
    {
        bool rejected = false;
        try
        {
            QemuTranscripts.ValidateLinuxCallOrdinals(rows);
        }
        catch (ToolFailure) { rejected = true; }
        HostCheck.True(rejected);
    }

    private const string DiskPrefix = "M16_008_CALL identity=4294967297 nr=17 ordinal=1 waitSource=4 waitDeadline=0 waitSequence=9";
    private const string DiskReturn = " signalDepth=0 signalFrame=0 blockedSignals=0 returnRip=4194818 returnRsp=1879048176 returnFlags=514 returnFs=0 returnGs=0 returnValueBits=0";

    [HostTest("disk call observations require explicit mode and copy complete source4 generation witnesses")]
    private static void DiskObservations()
    {
        string[] rows = ["header", DiskPrefix + DiskReturn];
        var parsed = QemuTranscripts.ReadLinuxDiskCallObservations(rows);
        HostCheck.Equal(new QemuTranscripts.LinuxCallWaitObservation(1, 4294967297, 1, 4, 0, 9), parsed.Waits[0]);
        HostCheck.True(parsed.Returns.Count == 1 && parsed.Returns[0].Rip == 4194818 && parsed.Returns[0].RecordIndex == 1);
        HostCheck.Equal("M16_008_CALL identity=4294967297 nr=17", QemuTranscripts.ValidateLinuxDiskCallOrdinals(rows)[1]);
        Reject(rows);
        RejectWait(rows);
        foreach (Action historical in new Action[] {
            () => QemuTranscripts.ValidateLinuxCallOrdinals(rows, allowEpoll: true),
            () => QemuTranscripts.ReadLinuxCallReturns(rows, allowEpoll: true),
            () => QemuTranscripts.ReadLinuxServiceCallWaits(rows),
            () => QemuTranscripts.ReadLinuxServiceCallReturns(rows) })
            RejectAction(historical);
        rows[1] = "changed";
        HostCheck.True(parsed.Waits[0].Sequence == 9 && parsed.Returns[0].Rsp == 1879048176 &&
            parsed.Waits is not QemuTranscripts.LinuxCallWaitObservation[] && parsed.Returns is not QemuTranscripts.LinuxCallReturnObservation[]);
        if (parsed.Waits is IList<QemuTranscripts.LinuxCallWaitObservation> list)
        {
            bool denied = false;
            try
            {
                list[0] = default;
            }
            catch (NotSupportedException) { denied = true; }
            HostCheck.True(denied);
        }
    }

    [HostTest("disk call observations reject deadlines missing returns unknown sources and lost ordinals")]
    private static void DiskFailures()
    {
        foreach (string changed in new[] { DiskPrefix, (DiskPrefix + DiskReturn).Replace("waitDeadline=0", "waitDeadline=1", StringComparison.Ordinal),
            (DiskPrefix + DiskReturn).Replace("waitSequence=9", "waitSequence=0", StringComparison.Ordinal),
            (DiskPrefix + DiskReturn).Replace("waitSource=4", "waitSource=5", StringComparison.Ordinal),
            (DiskPrefix + DiskReturn).Replace("ordinal=1", "ordinal=2", StringComparison.Ordinal),
            (DiskPrefix + DiskReturn).Replace("waitSequence=9", "waitSequence=18446744073709551616", StringComparison.Ordinal) })
        {
            RejectAction(() => QemuTranscripts.ReadLinuxDiskCallObservations([changed]));
            RejectAction(() => QemuTranscripts.ValidateLinuxDiskCallOrdinals([changed]));
        }
        RejectAction(() => QemuTranscripts.ReadLinuxDiskCallObservations([DiskPrefix + DiskReturn, DiskPrefix + DiskReturn]));
        string epoll = (DiskPrefix + DiskReturn).Replace("waitSource=4 waitDeadline=0", "waitSource=3 waitDeadline=100", StringComparison.Ordinal);
        HostCheck.Equal(3U, QemuTranscripts.ReadLinuxDiskCallObservations([epoll]).Waits[0].Source);
    }

    private const string WritablePrefix = "M16_008_CALL identity=4294967297 nr=17 ordinal=1 waitSource=5 waitDeadline=0 waitSequence=9";

    [HostTest("writable disk call observations explicitly copy paired sources zero through five with independent generation ordinals")]
    private static void WritableObservations()
    {
        string[] rows = ["header", WritablePrefix + DiskReturn,
            "M16_008_CALL identity=4294967298 nr=4 ordinal=1 waitSource=4 waitDeadline=0 waitSequence=8" + DiskReturn,
            "M16_008_CALL identity=4294967297 nr=281 ordinal=2 waitSource=3 waitDeadline=100 waitSequence=10" + DiskReturn,
            "M16_008_CALL identity=8589934594 nr=202 ordinal=1 waitSource=1 waitDeadline=1050 waitSequence=1" + DiskReturn,
            "M16_008_CALL identity=4294967298 nr=0 ordinal=2 waitSource=2 waitDeadline=0 waitSequence=2" + DiskReturn,
            "M16_008_CALL identity=4294967297 nr=39 ordinal=3 waitSource=0 waitDeadline=0 waitSequence=0" + DiskReturn];
        string[] before = (string[])rows.Clone();
        var parsed = QemuTranscripts.ReadLinuxWritableDiskCallObservations(rows);
        HostCheck.Equal(new QemuTranscripts.LinuxCallWaitObservation(1, 4294967297, 1, 5, 0, 9), parsed.Waits[0]);
        HostCheck.True(parsed.Waits.Count == 6 && parsed.Returns.Count == 6 &&
            parsed.Waits.Select(value => value.Source).SequenceEqual(new uint[] { 5, 4, 3, 1, 2, 0 }) &&
            parsed.Waits[3].Identity == 8589934594 && parsed.Waits[3].Ordinal == 1 && parsed.Waits[4].Ordinal == 2 &&
            parsed.Returns[0].RecordIndex == 1 && parsed.Returns[5].RecordIndex == 6 && parsed.Returns[0].Rip == 4194818);
        var stripped = QemuTranscripts.ValidateLinuxWritableDiskCallOrdinals(rows);
        HostCheck.True(stripped[0] == "header" && stripped[1] == "M16_008_CALL identity=4294967297 nr=17" &&
            stripped[6] == "M16_008_CALL identity=4294967297 nr=39" && rows.SequenceEqual(before));
        Array.Fill(rows, "changed");
        stripped[1] = "changed";
        HostCheck.True(parsed.Waits[0].Sequence == 9 && parsed.Returns[0].Rsp == 1879048176 &&
            parsed.Waits is not QemuTranscripts.LinuxCallWaitObservation[] && parsed.Returns is not QemuTranscripts.LinuxCallReturnObservation[]);
        if (parsed.Waits is IList<QemuTranscripts.LinuxCallWaitObservation> waits)
        {
            bool denied = false;
            try
            {
                waits[0] = default;
            }
            catch (NotSupportedException) { denied = true; }
            HostCheck.True(denied);
        }
        if (parsed.Returns is IList<QemuTranscripts.LinuxCallReturnObservation> returns)
        {
            bool denied = false;
            try
            {
                returns[0] = default;
            }
            catch (NotSupportedException) { denied = true; }
            HostCheck.True(denied);
        }
    }

    [HostTest("writable source five never broadens default runtime service epoll or readonly disk call readers")]
    private static void WritableRequiresExplicitReader()
    {
        string[] rows = [WritablePrefix + DiskReturn];
        foreach (Action historical in new Action[] {
            () => QemuTranscripts.ValidateLinuxCallOrdinals(rows), () => QemuTranscripts.ValidateLinuxCallOrdinals(rows, true),
            () => QemuTranscripts.ReadLinuxCallWaits(rows), () => QemuTranscripts.ReadLinuxCallWaits(rows, true),
            () => QemuTranscripts.ReadLinuxCallReturns(rows), () => QemuTranscripts.ReadLinuxCallReturns(rows, true),
            () => QemuTranscripts.ValidateLinuxServiceCallOrdinals(rows), () => QemuTranscripts.ReadLinuxServiceCallWaits(rows),
            () => QemuTranscripts.ReadLinuxServiceCallReturns(rows), () => QemuTranscripts.ReadLinuxDiskCallObservations(rows),
            () => QemuTranscripts.ReadLinuxDiskCallObservations(rows, true), () => QemuTranscripts.ValidateLinuxDiskCallOrdinals(rows),
            () => QemuTranscripts.ValidateLinuxDiskCallOrdinals(rows, true) })
            RejectAction(historical);
        RejectWritable([rows[0].Replace("waitSource=5", "waitSource=6", StringComparison.Ordinal)]);
        string source4 = rows[0].Replace("waitSource=5", "waitSource=4", StringComparison.Ordinal);
        HostCheck.Equal(QemuTranscripts.ReadLinuxDiskCallObservations([source4]).Waits[0],
            QemuTranscripts.ReadLinuxWritableDiskCallObservations([source4]).Waits[0]);
    }

    [HostTest("writable call suffix rejects invalid generations partial pairs forged output prefix bad decimals and reordered observations")]
    private static void WritableFailures()
    {
        string row = WritablePrefix + DiskReturn;
        foreach (var mutation in new (string Before, string After)[] {
            ("waitSource=5", "waitSource=05"), ("waitSource=5", "waitSource=-1"),
            ("waitSequence=9", "waitSequence=0"), ("waitSequence=9", "waitSequence=09"),
            ("waitSequence=9", "waitSequence=18446744073709551616"),
            ("waitDeadline=0", "waitDeadline=1"), ("waitDeadline=0", "waitDeadline=00"),
            ("waitDeadline=0", "waitDeadline=-1"), ("waitDeadline=0", "waitDeadline=9223372036854775808"),
            ("identity=4294967297", "identity=1"), ("identity=4294967297", "identity=4294967296"),
            ("identity=4294967297", "identity=0"), ("identity=4294967297", "identity=04294967297"),
            ("identity=4294967297", "identity=18446744073709551616"),
            ("ordinal=1", "ordinal=0"), ("ordinal=1", "ordinal=01"), ("ordinal=1", "ordinal=2"),
            ("ordinal=1", "ordinal=18446744073709551616"),
            ("signalDepth=0", "signalDepth=1"), ("signalDepth=0", "signalDepth=5"),
            ("returnRip=4194818", "returnRip=04194818"), ("returnValueBits=0", "returnValueBits=18446744073709551616") })
            RejectWritable([row.Replace(mutation.Before, mutation.After, StringComparison.Ordinal)]);
        foreach (string partial in new[] { WritablePrefix, "M16_008_CALL identity=4294967297 nr=17 ordinal=1",
            WritablePrefix[..WritablePrefix.IndexOf(" waitSequence=", StringComparison.Ordinal)],
            row.Replace(" returnRsp=1879048176", "", StringComparison.Ordinal), row + " extra", row + " waitSource=5",
            row.Replace(" ordinal=1", " ordinal=1 ordinal=1", StringComparison.Ordinal),
            row.Replace(" waitSource=5", " waitSource=5 waitSource=5", StringComparison.Ordinal) })
            RejectWritable([partial]);
        // CALL has no outputPrefix field. Capacity belongs to the later
        // authenticated BEGIN/WAKE owner, never to these copied suffix values.
        foreach (string field in new[] { "waitSource", "waitDeadline", "waitSequence", "signalDepth", "signalFrame", "blockedSignals",
            "returnRip", "returnRsp", "returnFlags", "returnFs", "returnGs", "returnValueBits", "outputPrefix" })
            RejectWritable([row.Replace(" ordinal=1", " " + field + "=0 ordinal=1", StringComparison.Ordinal)]);
        RejectWritable([row + " outputPrefix=8193"]);
        RejectWritable([row, row]);
        RejectWritable([row, row.Replace("ordinal=1", "ordinal=3", StringComparison.Ordinal)]);
        RejectWritable([row.Replace("ordinal=1", "ordinal=2", StringComparison.Ordinal), row]);
        RejectAction(() => QemuTranscripts.ReadLinuxWritableDiskCallObservations(null!));
        RejectAction(() => QemuTranscripts.ValidateLinuxWritableDiskCallOrdinals(null!));
    }

    [HostTest("writable call ordinals retain the finite4096 limit independently for each complete generation")]
    private static void WritableFiniteLimit()
    {
        string[] rows = Enumerable.Range(1, 4096).Select(ordinal =>
            "M16_008_CALL identity=4294967297 nr=17 ordinal=" + ordinal.ToString(System.Globalization.CultureInfo.InvariantCulture) +
            " waitSource=5 waitDeadline=0 waitSequence=" + ordinal.ToString(System.Globalization.CultureInfo.InvariantCulture) + DiskReturn).ToArray();
        var parsed = QemuTranscripts.ReadLinuxWritableDiskCallObservations(rows);
        HostCheck.True(parsed.Waits.Count == 4096 && parsed.Waits[^1].Ordinal == 4096 && parsed.Waits[^1].Sequence == 4096 &&
            QemuTranscripts.ValidateLinuxWritableDiskCallOrdinals(rows).Length == rows.Length);
        RejectWritable(rows.Append(rows[^1].Replace("ordinal=4096", "ordinal=4097", StringComparison.Ordinal)).ToArray());
        string next = rows[0].Replace("identity=4294967297", "identity=8589934593", StringComparison.Ordinal);
        var generations = QemuTranscripts.ReadLinuxWritableDiskCallObservations(rows.Append(next).ToArray());
        HostCheck.True(generations.Waits.Count == 4097 && generations.Waits[^1].Identity == 8589934593 && generations.Waits[^1].Ordinal == 1);
    }

    private static void RejectWritable(string[] rows)
    {
        RejectAction(() => QemuTranscripts.ReadLinuxWritableDiskCallObservations(rows));
        RejectAction(() => QemuTranscripts.ValidateLinuxWritableDiskCallOrdinals(rows));
    }

    private static void RejectAction(Action action)
    {
        bool rejected = false;
        try
        {
            action();
        }
        catch (ToolFailure) { rejected = true; }
        HostCheck.True(rejected);
    }
}
