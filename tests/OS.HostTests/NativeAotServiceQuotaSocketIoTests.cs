using OS.Tooling;

internal static class NativeAotServiceQuotaSocketIoTests
{
    private const ulong Identity = 4294967297;
    private sealed record Vector(string[] Rows, QemuTranscripts.ServiceQuotaTransportLifetime[] Census);
    private static string Call(int ordinal, int nr, int fd, long value, ulong flags = 0, ulong command = 0) =>
        $"M16_008_CALL identity={Identity} nr={nr} a0={fd} a1={command} a2={(nr == 47 ? flags : 0)} a3={(nr == 45 ? flags : 0)} a4=0 a5=0 rip=4198402 rsp=1879048184 flags=514 fs=0 gs=0 clock={ordinal} valueBits={unchecked((ulong)value)} action=1 status=1 ordinal={ordinal} waitSource=0 waitDeadline=0 waitSequence=0 signalDepth=0 signalFrame=0 blockedSignals=0 returnRip=4198402 returnRsp=1879048184 returnFlags=514 returnFs=0 returnGs=0 returnValueBits={unchecked((ulong)value)}";
    private static string Row(int ordinal, int nr, int fd, uint generation, uint connection, ulong offset,
        uint bytes, bool peek, ulong sent, ulong received) =>
        $"M16_016_SOCKET identity={Identity} ordinal={ordinal} nr={nr} fd={fd} fdGeneration={generation} description=5 inode={200 + connection} slot=0 owner=4096 epoch=1 generation={connection} index=0 local=167772687 localPort=8080 remote=167772674 remotePort={42999 + connection} offset={offset} bytes={bytes} peek={(peek ? 1 : 0)} sent={sent} received={received}";
    private static QemuTranscripts.ServiceQuotaTransportLifetime Lifetime(int row, ulong generation) =>
        new(row, 0, 4096, 1, generation, 0, 0x0a00020f, 8080, 0x0a000202, 42999 + generation, 1, 0, 0);
    private static Vector Fixture()
    {
        // Isolated original-row controls: no complete scheduler/parent authority.
        List<string> rows = ["M16_011_SLOT event=0 value=first"];
        rows.Add(Call(1, 288, 3, 4)); rows.Add(Row(1, 288, 4, 1, 1, 0, 0, false, 0, 0));
        rows.Add(Call(2, 46, 4, 4)); rows.Add(Row(2, 46, 4, 1, 1, 0, 4, false, 4, 0));
        rows.Add(Call(3, 47, 4, 3, 2)); rows.Add(Row(3, 47, 4, 1, 1, 0, 3, true, 4, 0));
        rows.Add(Call(4, 45, 4, 2)); rows.Add(Row(4, 45, 4, 1, 1, 0, 2, false, 4, 2));
        rows.Add(Call(5, 72, 4, 5, command: 0)); // Alias shares the transport counters.
        rows.Add(Call(6, 0, 5, 2)); rows.Add(Row(6, 0, 5, 1, 1, 2, 2, false, 4, 4));
        rows.Add(Call(7, 3, 4, 0));
        rows.Add(Call(8, 20, 5, 2)); rows.Add(Row(8, 20, 5, 1, 1, 4, 2, false, 6, 4));
        rows.Add(Call(9, 3, 5, 0));
        int fresh = rows.Count; rows.Add("M16_011_SLOT event=0 value=second");
        rows.Add(Call(10, 43, 3, 4)); rows.Add(Row(10, 43, 4, 2, 2, 0, 0, false, 0, 0));
        rows.Add(Call(11, 1, 4, 1)); rows.Add(Row(11, 1, 4, 2, 2, 0, 1, false, 1, 0));
        return new(rows.ToArray(), [Lifetime(0, 1), Lifetime(fresh, 2)]);
    }

    [HostTest("quota socket offsets retain short message I/O peeks shared aliases and reused fd generations")]
    private static void Offsets()
    {
        var v = Fixture(); string[] before = v.Rows.ToArray();
        var result = QemuTranscripts.InspectNativeAotServiceQuotaSocketIoForTests(v.Rows, v.Census);
        HostCheck.Equal(8, result.Count); HostCheck.True(result[2].Peek && result[2].Received == 0 && result[2].Bytes == 3);
        HostCheck.True(result[4].Descriptor == 5 && result[4].Offset == 2 && result[4].Received == 4);
        HostCheck.True(result[5].Response && result[5].Offset == 4 && result[5].Sent == 6);
        HostCheck.True(result[^1].DescriptorGeneration == 2 && result[^1].Connection.Generation == 2 && result[^1].Offset == 0);
        HostCheck.True(v.Rows.SequenceEqual(before)); Array.Clear(v.Rows); Array.Clear(v.Census);
        HostCheck.Equal(1UL, result[^1].Sent);
        bool readOnly = false; try { ((IList<QemuTranscripts.ServiceQuotaSocketIo>)result)[0] = default; } catch (NotSupportedException) { readOnly = true; }
        HostCheck.True(readOnly);
    }

    [HostTest("quota socket offsets reject absent orphan reordered crossed and discontinuous original observations")]
    private static void Controls()
    {
        var v = Fixture(); int[] observations = Enumerable.Range(0, v.Rows.Length).Where(i => v.Rows[i].StartsWith("M16_016_SOCKET ", StringComparison.Ordinal)).ToArray();
        foreach (int at in observations)
        {
            var missing = v.Rows.ToList(); missing.RemoveAt(at); Reject(missing.ToArray(), v.Census);
            var duplicate = v.Rows.ToList(); duplicate.Insert(at, duplicate[at]); Reject(duplicate.ToArray(), v.Census);
            foreach (var mutation in new[] { ("identity=4294967297", "identity=4294967298"), ("owner=4096", "owner=8192"),
                ("index=0", "index=1"), ("description=5", "description=16"), ("fdGeneration=1", "fdGeneration=0") })
            {
                var changed = v.Rows.ToArray(); changed[at] = changed[at].Replace(mutation.Item1, mutation.Item2, StringComparison.Ordinal);
                if (changed[at] != v.Rows[at]) Reject(changed, v.Census);
            }
        }
        foreach (var mutation in new[] { (6, "received=0", "received=3"), (8, "offset=0", "offset=1"),
            (11, "inode=201", "inode=202"), (14, "sent=6", "sent=7"), (20, "offset=0", "offset=6") })
        { var changed = v.Rows.ToArray(); changed[mutation.Item1] = changed[mutation.Item1].Replace(mutation.Item2, mutation.Item3, StringComparison.Ordinal); HostCheck.True(changed[mutation.Item1] != v.Rows[mutation.Item1]); Reject(changed, v.Census); }
        var wrong = v.Census.ToArray(); wrong[1] = wrong[1] with { Generation = 3 }; Reject(v.Rows, wrong);
        Reject(v.Rows, [v.Census[0], v.Census[0]]);
        // Move the second fresh generation before an old generation's live I/O.
        wrong = v.Census.ToArray(); wrong[1] = wrong[1] with { RowIndex = 3 }; Reject(v.Rows, wrong);
    }

    private static void Reject(string[] rows, IReadOnlyList<QemuTranscripts.ServiceQuotaTransportLifetime> census)
    {
        try { _ = QemuTranscripts.InspectNativeAotServiceQuotaSocketIoForTests(rows, census); }
        catch (ToolFailure) { return; }
        catch (InvalidDataException) { return; }
        throw new InvalidOperationException("expected quota socket rejection");
    }
}
