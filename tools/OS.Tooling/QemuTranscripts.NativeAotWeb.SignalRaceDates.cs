using System.Globalization;

namespace OS.Tooling;

public static partial class QemuTranscripts
{
    /// <summary>Borrow an already strict race stream join and copied producer
    /// window. Recheck/copy bounded packet/binding values, then frame every
    /// contiguous guest response, including unread or incomplete entity suffixes.
    /// Each complete header needs canonical RFC1123 Date within the whole positive
    /// <=45s UTC producer window plus/minus the existing5s tolerance. Dates cannot
    /// precede the ordinary last Date or regress on a persistent stream; concurrent
    /// streams have no global completion/Date order or invented per-request UTC.
    /// Retain pending headers and exact clipped original header ranges. Bounds
    /// remain504 streams/4MiB aggregate extents/65536 ranges and observations.
    /// Return immutable values or ToolFailure; wire ticks are never converted to
    /// UTC. This performs no independent clock/file/process/native/guest admission.</summary>

    public static WebSignalRaceDateInspection InspectNativeAotWebSignalRaceDatesForTests(WebSignalRaceStreamJoinInspection joined, WebUtcObservation producerWindow, DateTimeOffset ordinaryLastDateUtc) => ReadWebSignalRaceDates(joined, producerWindow, ordinaryLastDateUtc, 45);
    /// <summary>Explicit event-owned copied Date-window inspection. Apply the
    /// historical reader's framing, tolerance, ordering and immutable range
    /// checks with a positive whole producer window at most165s. The caller must
    /// separately bind the event profile and original owner/runner evidence;
    /// this seam grants no native, process, source, clock or guest authority.</summary>

    public static WebSignalRaceDateInspection InspectNativeAotWebEventOwnedSignalRaceDatesForTests(WebSignalRaceStreamJoinInspection joined, WebUtcObservation producerWindow, DateTimeOffset ordinaryLastDateUtc) => ReadWebSignalRaceDates(joined, producerWindow, ordinaryLastDateUtc, 165);
    private static WebSignalRaceDateInspection ReadWebSignalRaceDates(WebSignalRaceStreamJoinInspection joined, WebUtcObservation producerWindow, DateTimeOffset ordinaryLastDateUtc, int maximumSeconds)
    {
        const long tolerance = WebUtcToleranceSeconds * TimeSpan.TicksPerSecond;
        long start = producerWindow.StartedUtc.UtcTicks, end = producerWindow.CompletedUtc.UtcTicks;
        Need(producerWindow.StartedUtc.Offset == TimeSpan.Zero && producerWindow.CompletedUtc.Offset == TimeSpan.Zero && start > DateTimeOffset.UnixEpoch.UtcTicks + tolerance && end <= DateTimeOffset.MaxValue.UtcTicks - tolerance && end > start && end - start <= maximumSeconds * TimeSpan.TicksPerSecond, "web_race_date_window");
        Need(ordinaryLastDateUtc.Offset == TimeSpan.Zero && ordinaryLastDateUtc > DateTimeOffset.UnixEpoch && ordinaryLastDateUtc.UtcTicks % TimeSpan.TicksPerSecond == 0 && ordinaryLastDateUtc.UtcTicks <= start + tolerance, "web_race_date_ordinary");
        var (guest, hosts, indexes) = CopyWebRaceBoundPackets(joined);
        List<WebSignalRaceDateObservation> responses = [];
        int pending = 0;
        foreach (var stream in guest.Streams)
        {
            var messages = ReadServiceQuotaHttpPrefixes(Convert.FromBase64String(stream.Responses.KnownPrefixBase64), true, webDiagnostics: true).Messages;
            DateTimeOffset prior = ordinaryLastDateUtc;
            foreach (var message in messages)
            {
                Need(responses.Count + pending < MaximumServiceWireRanges, "web_race_date_bound");
                if (message.HeaderEnd < 0)
                {
                    pending++;
                    continue;
                }

                string? text = message.Headers.GetValueOrDefault("Date");
                Need(text is { Length: 29 } && DateTimeOffset.TryParseExact(text, "r", CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal, out _), "web_race_date_header");
                var date = DateTimeOffset.ParseExact(text!, "r", CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal);
                Need(text == date.ToString("r", CultureInfo.InvariantCulture), "web_race_date_header");
                Need(date.UtcTicks >= start - tolerance && date.UtcTicks <= end + tolerance, "web_race_date_window_value");
                Need(date >= prior, "web_race_date_regression");
                prior = date;
                Need(hosts.TryGetValue(stream.RemotePort, out int host), "web_race_join_binding");
                responses.Add(new(host, stream.RemotePort, message.Start, message.HeaderEnd, message.End, message.State, date, WebRaceHttpRanges(stream, true, message.Start, message.HeaderEnd, indexes[(stream.RemotePort, true)])));
            }
        }

        return new(producerWindow, ordinaryLastDateUtc, pending, Array.AsReadOnly(responses.ToArray()));
    }

    private static (ServiceQuotaPacketStreams Guest, Dictionary<uint, int> Hosts, Dictionary<(uint Port, bool Response), WebRaceIndexedRange[]> Indexes) CopyWebRaceBoundPackets(WebSignalRaceStreamJoinInspection joined)
    {
        if (joined is null)
            throw new ToolFailure("web_race_join_input");
        var guest = CopyWebSignalRacePacketStreams(joined.Guest);
        var streams = guest.Streams.ToDictionary(s => s.RemotePort);
        Dictionary<(uint, bool), WebRaceIndexedRange[]> indexes = [];
        foreach (var stream in guest.Streams)
        {
            _ = PrepareQuotaPairGuest(stream);
            indexes.Add((stream.RemotePort, false), Index(stream.Requests));
            indexes.Add((stream.RemotePort, true), Index(stream.Responses));
        }

        static WebRaceIndexedRange[] Index(ServiceQuotaPacketDirection direction) => direction.FirstRanges.Select((r, i) => new WebRaceIndexedRange(i, r)).OrderBy(r => r.Range.First.StreamOffset).ToArray();
        var bindings = QuotaPairCopy(joined.Bindings, 504);
        Dictionary<uint, int> hosts = [];
        HashSet<int> ids = [];
        foreach (var binding in bindings)
        {
            if (!streams.TryGetValue(binding.RemotePort, out var stream))
                throw new ToolFailure("web_race_join_binding");
            var compared = binding.Compared;
            Need(binding.ConnectionId is > 0 and <= 8192 && ids.Add(binding.ConnectionId) && hosts.TryAdd(binding.RemotePort, binding.ConnectionId) && compared.Compatible && compared.HasComparedBytes && compared.RequestBytesCompared == stream.Requests.KnownBytes && compared.ResponseBytesCompared >= 0 && compared.ResponseBytesCompared <= stream.Responses.ContiguousBytes && compared.MinimumAttemptedWritePrefixBytes >= 0 && compared.MinimumAttemptedWritePrefixBytes <= stream.Requests.Extent && stream.Requests.KnownBytes + stream.Responses.KnownBytes > 0, "web_race_join_binding");
        }

        Need(guest.Streams.All(s => s.Requests.KnownBytes + s.Responses.KnownBytes == 0 || hosts.ContainsKey(s.RemotePort)), "web_race_join_binding");
        return (guest, hosts, indexes);
    }

    private static IReadOnlyList<ServiceQuotaHttpPacketRange> WebRaceHttpRanges(ServiceQuotaPacketStream stream, bool response, int start, int end, WebRaceIndexedRange[] sorted)
    {
        var direction = response ? stream.Responses : stream.Requests;
        Need(start >= 0 && end > start && end <= direction.ContiguousBytes, "web_race_join_interval");
        int first = 0, last = sorted.Length;
        while (first < last)
        {
            int mid = first + (last - first) / 2;
            var r = sorted[mid].Range.First;
            if (r.StreamOffset + r.ByteCount <= start)
                first = mid + 1;
            else
                last = mid;
        }

        List<(int Order, ServiceQuotaHttpPacketRange Value)> clipped = [];
        for (int i = first; i < sorted.Length && sorted[i].Range.First.StreamOffset < end; i++)
        {
            var r = sorted[i].Range.First;
            int at = Math.Max(start, r.StreamOffset);
            clipped.Add((sorted[i].Order, new(r, at, Math.Min(end, r.StreamOffset + r.ByteCount) - at)));
        }

        var ranges = clipped.OrderBy(r => r.Order).Select(r => r.Value).ToArray();
        Need(ranges.Sum(r => r.ByteCount) == end - start, "web_race_join_interval");
        return Array.AsReadOnly(ranges);
    }
}
