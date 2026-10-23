namespace OS.Tooling;

public static partial class QemuTranscripts
{
    /// <summary>Copied original transport generation and tuple. Slot is the
    /// native network slot; Index belongs to its owned connection table.</summary>
    public readonly record struct ServiceQuotaSocketIdentity(ulong Owner, ulong Epoch, ulong Generation,
        uint Index, uint Slot, ulong Local, uint LocalPort, ulong Remote, uint RemotePort);

    /// <summary>One original post-CALL accept or positive socket I/O observation.
    /// Offsets/counts measure application queue acceptance/consumption, not ACKs
    /// or transmission. Peek views bytes without advancing Received. Record
    /// indices address the supplied original normalized serial rows.</summary>
    public readonly record struct ServiceQuotaSocketIo(int RecordIndex, int CallRecordIndex, ulong Identity,
        ulong Ordinal, ulong Number, int Descriptor, uint DescriptorGeneration, uint Description, ulong Inode,
        ServiceQuotaSocketIdentity Connection, ulong Offset, uint Bytes, bool Peek, ulong Sent, ulong Received)
    {
        public bool Accept => Number is 43 or 288;
        public bool Response => Number is 1 or 20 or 44 or 46;
    }

    /// <summary>Inspect bounded borrowed original normalized rows beside a
    /// fully validated transport census. Require each successful accept/positive
    /// socket call's immediately following observation, original return identity,
    /// tuple/generation and continuous per-lifetime byte counters; peeks consume
    /// no bytes. Track socket aliases and closes without assigning byte identity
    /// to a reused fd. Copy results into a read-only array. Full scheduler and
    /// descriptor generation/open-description replay, source/native/process/file
    /// ownership and original packet joins remain mandatory. This value seam
    /// establishes no execution, physical memory, cleanup or guest acceptance.</summary>
    public static IReadOnlyList<ServiceQuotaSocketIo> InspectNativeAotServiceQuotaSocketIoForTests(
        string[] rows, IReadOnlyList<ServiceQuotaTransportLifetime> lifetimes) => ReadServiceQuotaSocketIo(rows, lifetimes);

    private static readonly string[] QuotaSocketFields =
        "identity ordinal nr fd fdGeneration description inode slot owner epoch generation index local localPort remote remotePort offset bytes peek sent received"
        .Split(' ').Select(s => s + ":dec").ToArray();

    private static ServiceQuotaSocketIo QuotaSocketRow(string row, int index)
    {
        var r = ReadRecord(row, "M16_016_SOCKET", QuotaSocketFields);
        Need(r["identity"] > 0 && r["ordinal"] > 0 && r["fd"] < 16 && r["fdGeneration"] is > 0 and <= uint.MaxValue &&
            r["description"] < 16 && r["inode"] > 0 && r["slot"] < 8 && r["index"] < 8 &&
            r["owner"] > 0 && r["epoch"] > 0 && r["generation"] > 0 && r["local"] == 0x0a00020f &&
            r["localPort"] == 8080 && r["remote"] == 0x0a000202 && r["remotePort"] is > 0 and <= 65535 &&
            r["bytes"] <= 8192 && r["peek"] <= 1 && r["offset"] <= ulong.MaxValue - r["bytes"], "service_quota_socket_shape");
        return new(index, index - 1, r["identity"], r["ordinal"], r["nr"], (int)r["fd"], (uint)r["fdGeneration"],
            (uint)r["description"], r["inode"], new(r["owner"], r["epoch"], r["generation"], (uint)r["index"],
                (uint)r["slot"], r["local"], (uint)r["localPort"], r["remote"], (uint)r["remotePort"]),
            r["offset"], (uint)r["bytes"], r["peek"] != 0, r["sent"], r["received"]);
    }

    private static ServiceQuotaSocketIdentity QuotaSocketIdentity(ServiceQuotaTransportLifetime value) =>
        new(value.Owner, value.Epoch, value.Generation, checked((uint)value.Index), checked((uint)value.Slot),
            value.Local, checked((uint)value.LocalPort), value.Remote, checked((uint)value.RemotePort));

    private static void QuotaSocketCallBinding(ServiceQuotaSocketIo value, Dictionary<string, ulong> call,
        LinuxCallReturnObservation returned)
    {
        bool accept = call["nr"] is 43 or 288;
        ulong flags = call["nr"] is 44 or 45 ? call["a3"] : call["nr"] is 46 or 47 ? call["a2"] : 0;
        Need(value.Identity == returned.Identity && value.Ordinal == returned.Ordinal && value.Number == call["nr"] &&
            returned.ValueBits == call["valueBits"] && call["action"] == 1 && call["status"] == 1 &&
            (ulong)value.Descriptor == (accept ? call["valueBits"] : call["a0"]) &&
            (accept ? unchecked((long)call["valueBits"]) >= 0 && value.Bytes == 0 && !value.Peek :
                value.Number is 0 or 1 or 20 or 44 or 45 or 46 or 47 && value.Bytes > 0 &&
                value.Bytes == call["valueBits"] && value.Peek == (!value.Response && (flags & 2) != 0)),
            "service_quota_socket_call");
    }

    private static IReadOnlyList<ServiceQuotaSocketIo> ReadServiceQuotaSocketIo(string[] rows,
        IReadOnlyList<ServiceQuotaTransportLifetime> lifetimes)
    {
        Need(rows is not null && rows.Length is > 0 and <= 1_000_000 && rows.All(r => r is not null &&
            r.Length is > 0 and <= 4096 && r.All(c => c is >= ' ' and <= '~')) &&
            rows.Sum(r => (long)r.Length + 2) <= 128L * 1024 * 1024 && lifetimes is not null &&
            lifetimes.Count is >= 0 and <= 512, "service_quota_socket_input");
        var census = lifetimes!.ToArray();
        Need(census.All(l => l.RowIndex >= 0 && l.RowIndex < rows!.Length && l.Slot < 8 && l.Index < 8 &&
            l.LocalPort <= 65535 && l.RemotePort <= 65535), "service_quota_socket_census");
        var identities = census.Select(QuotaSocketIdentity).ToArray();
        Need(identities.Distinct().Count() == identities.Length, "service_quota_socket_census");
        var normalized = ValidateLinuxServiceCallOrdinals(rows!);
        var returns = ReadLinuxServiceCallReturns(rows!).ToDictionary(r => r.RecordIndex);
        Dictionary<int, ServiceQuotaSocketIdentity> descriptors = [];
        Dictionary<ServiceQuotaSocketIdentity, (ulong Sent, ulong Received, uint Description, ulong Inode)> counters = [];
        List<ServiceQuotaSocketIo> result = []; HashSet<int> consumed = [];
        foreach (int at in returns.Keys.Order())
        {
            var call = ReadRecord(normalized[at], "M16_008_CALL", WebCallFields);
            ulong nr = call["nr"], fd = call["a0"], v = call["valueBits"];
            bool success = call["action"] == 1 && call["status"] == 1 && unchecked((long)v) >= 0;
            bool accept = success && nr is 43 or 288;
            bool io = success && v > 0 && (nr is 44 or 45 or 46 or 47 ||
                nr is 0 or 1 or 20 && fd < 16 && descriptors.ContainsKey((int)fd));
            if (accept || io)
            {
                Need(at + 1 < rows!.Length && rows[at + 1].StartsWith("M16_016_SOCKET ", StringComparison.Ordinal), "service_quota_socket_missing");
                var value = QuotaSocketRow(rows![at + 1], at + 1); QuotaSocketCallBinding(value, call, returns[at]);
                int found = Array.IndexOf(identities, value.Connection);
                Need(found >= 0 && census[found].RowIndex < at && !census.Any(l => l.Slot == value.Connection.Slot &&
                    l.RowIndex > census[found].RowIndex && l.RowIndex < at), "service_quota_socket_lifetime");
                if (accept)
                {
                    Need(!descriptors.ContainsKey(value.Descriptor) && counters.TryAdd(value.Connection, (0, 0, value.Description, value.Inode)),
                        "service_quota_socket_accept");
                    descriptors.Add(value.Descriptor, value.Connection);
                }
                Need(descriptors.TryGetValue(value.Descriptor, out var connection) && connection == value.Connection,
                    "service_quota_socket_descriptor");
                Need(counters.TryGetValue(connection, out var prior), "service_quota_socket_descriptor");
                Need(prior.Description == value.Description && prior.Inode == value.Inode, "service_quota_socket_descriptor");
                ulong sent = prior.Sent, received = prior.Received;
                Need(value.Offset == (value.Response ? sent : received), "service_quota_socket_offset");
                if (!accept && !value.Peek)
                { if (value.Response) sent = checked(sent + value.Bytes); else received = checked(received + value.Bytes); }
                Need(value.Sent == sent && value.Received == received, "service_quota_socket_counters");
                counters[connection] = (sent, received, prior.Description, prior.Inode);
                consumed.Add(at + 1); result.Add(value);
            }
            if (!success) continue;
            if (nr == 3 && fd < 16) descriptors.Remove((int)fd);
            bool duplicate = nr is 32 or 33 or 292 || nr == 72 && call["a1"] is 0 or 1030;
            if (duplicate && fd < 16 && v < 16 && v != fd)
            {
                descriptors.Remove((int)v);
                if (descriptors.TryGetValue((int)fd, out var source)) descriptors.Add((int)v, source);
            }
        }
        for (int i = 0; i < rows!.Length; i++)
            // Component lease rows have their own strict reader. Keep malformed
            // CALL marker aliases rejected rather than relaxing the whole prefix.
            Need(!rows[i].StartsWith("M16_016_SOCKET", StringComparison.Ordinal) ||
                rows[i].StartsWith("M16_016_SOCKET_LEASES ", StringComparison.Ordinal) ||
                rows[i].StartsWith("M16_016_SOCKET_LEASE ", StringComparison.Ordinal) || consumed.Contains(i), "service_quota_socket_orphan");
        return Array.AsReadOnly(result.ToArray());
    }
}
