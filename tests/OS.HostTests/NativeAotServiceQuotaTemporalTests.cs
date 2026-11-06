using OS.Tooling;

internal static partial class NativeAotServiceQuotaTemporalTests
{
    private const uint Port = 43000;
    private static QemuTranscripts.ServiceQuotaSocketIdentity Connection(uint port) => new(4096, 1, 1, port - Port, port - Port, 0x0a00020f, 8080, 0x0a000202, port);
    private static QemuTranscripts.ServiceQuotaSocketIo Call(int at, uint port, ulong nr, ulong offset, uint bytes, ulong sent, ulong received, bool peek = false) => new(at, at - 1, 4294967297, (ulong)at, nr, (int)(port - Port + 4), 1, port - Port + 5, 200 + port, Connection(port), offset, bytes, peek, sent, received);
    private static QemuTranscripts.ServiceQuotaPacketDirection Direction(uint port, bool response, int count)
    {
        string bytes = Convert.ToBase64String(Enumerable.Repeat((byte)'A', count).ToArray());
        QemuTranscripts.ServiceQuotaPacketRange[] ranges = count == 0 ? [] : [new(new(port, response, 0, count, response ? 2 : 1, 1, 0), bytes)];
        return new(count, count, count, bytes, ranges);
    }

    private static QemuTranscripts.ServiceQuotaPacketStream Stream(uint port, int request, int response) => new(port, Direction(port, false, request), Direction(port, true, response), default);
    private static QemuTranscripts.ServiceQuotaHttpPacketInterval Interval(int exchange, uint port, bool response, string kind, int start, int end, int full) => new(exchange, (int)(port - Port + 1), port, response, kind, start, end, [new(new(port, response, 0, full, response ? 2 : 1, 1, 0), start, end - start)]);
    private static Vector Fixture()
    {
        // Independent copied values isolate this seam; these are not an owned
        // peer/PCAP capture or a complete scheduler/native replay fixture.
        QemuTranscripts.ServiceQuotaPacketStream[] streams = [Stream(Port, 12, 8), Stream(Port + 1, 4, 3), Stream(Port + 2, 7, 0), Stream(Port + 3, 5, 0)];
        QemuTranscripts.ServiceQuotaFlowBinding[] bindings = Enumerable.Range(0, 4).Select(i => new QemuTranscripts.ServiceQuotaFlowBinding(i + 1, Port + (uint)i, new(true, 1, 0, 0))).ToArray();
        QemuTranscripts.ServiceQuotaHttpPacketInterval[] intervals = [Interval(1, Port, false, "complete-request", 0, 6, 12), Interval(1, Port, true, "complete-response", 0, 5, 8), Interval(2, Port, false, "complete-request", 6, 10, 12), Interval(2, Port, true, "complete-response", 5, 8, 8), Interval(3, Port + 1, false, "complete-request", 0, 4, 4), Interval(3, Port + 1, true, "cancel-header", 0, 3, 3), Interval(4, Port + 2, false, "echo-prefix", 0, 7, 7), Interval(5, Port + 3, false, "pressure-request", 0, 5, 5)];
        var prefixes = new QemuTranscripts.ServiceQuotaHostPrefixes(0, 0, 0, []);
        var host = new QemuTranscripts.ServiceQuotaHostHttpInspection(prefixes, [], []);
        var packets = new QemuTranscripts.ServiceQuotaPacketStreams(1, 0, 39, streams);
        var joined = new QemuTranscripts.ServiceQuotaStreamJoinInspection(host, packets, bindings, [], [], intervals);
        QemuTranscripts.ServiceQuotaSocketIo[] calls = [Call(2, Port, 288, 0, 0, 0, 0), Call(4, Port, 45, 0, 2, 0, 2), Call(6, Port, 47, 2, 2, 0, 2, true), Call(8, Port, 47, 2, 4, 0, 6), Call(10, Port, 44, 0, 3, 3, 6), Call(12, Port, 46, 3, 2, 5, 6), Call(14, Port, 45, 6, 4, 5, 10), Call(16, Port, 44, 5, 5, 10, 10), Call(18, Port + 1, 43, 0, 0, 0, 0), Call(20, Port + 1, 45, 0, 4, 0, 4), Call(22, Port + 1, 44, 0, 3, 3, 4), Call(24, Port + 2, 43, 0, 0, 0, 0), Call(26, Port + 2, 45, 0, 7, 0, 7), Call(28, Port + 3, 43, 0, 0, 0, 0), Call(30, Port + 3, 45, 0, 5, 0, 5)];
        return new(joined, calls);
    }

    private static QemuTranscripts.ServiceQuotaTemporalInspection Inspect(Vector v) => QemuTranscripts.InspectNativeAotServiceQuotaTemporalForTests(v.Joined, v.Calls);
    [HostTest("quota HTTP syscall slices clip every required interval and retain unsent unconsumed and peek distinctions")]
    private static void Slices()
    {
        var v = Fixture();
        var original = v.Calls.ToArray();
        var report = Inspect(v);
        HostCheck.True(report.SocketObservations == 15 && report.RequestBytesConsumed == 26 && report.ResponseBytesAccepted == 13);
        HostCheck.True(report.Intervals.Count == 8 && report.Intervals[0].Slices.Select(s => s.ByteCount).SequenceEqual(new uint[] { 2, 4 }));
        HostCheck.True(report.Intervals[1].Slices.Select(s => s.ByteCount).SequenceEqual(new uint[] { 3, 2 }));
        var clipped = report.Intervals[3].Slices.Single();
        HostCheck.True(clipped.StreamOffset == 5 && clipped.ByteCount == 3 && clipped.Original.Bytes == 5 && clipped.Original.CallRecordIndex == 15);
        HostCheck.True(report.Intervals.All(i => i.Slices.All(s => !s.Original.Peek) && i.Slices.Sum(s => (long)s.ByteCount) == i.End - i.Start));
        HostCheck.True(report.Intervals.Select(i => i.Kind).Contains("cancel-header") && report.Intervals[^1].Kind == "pressure-request");
        HostCheck.True(v.Calls.SequenceEqual(original));
        Array.Clear(v.Calls);
        Array.Clear((QemuTranscripts.ServiceQuotaHttpPacketInterval[])v.Joined.Intervals);
        HostCheck.True(report.Intervals[0].Slices[1].Original.Identity == 4294967297 && report.Intervals[0].Slices[1].Original.RecordIndex == 8);
        bool immutable = false;
        try
        {
            ((IList<QemuTranscripts.ServiceQuotaHttpSyscallSlice>)report.Intervals[0].Slices).Clear();
        }
        catch (NotSupportedException)
        {
            immutable = true;
        }

        HostCheck.True(immutable);
    }

    [HostTest("quota HTTP syscall coverage rejects peek-only consumption and native or captured fronts beyond actual bytes")]
    private static void Coverage()
    {
        var v = Fixture();
        // Counter-consistent native prefix contains only two consumed bytes,
        // then a four-byte peek; peeking cannot cover the six-byte request.
        var joined = v.Joined with
        {
            Guest = new(1, 0, 20, [Stream(Port, 12, 8)]),
            Bindings = [v.Joined.Bindings[0]],
            Intervals = v.Joined.Intervals.Take(2).ToArray()
        };
        QemuTranscripts.ServiceQuotaSocketIo[] peekOnly = [Call(2, Port, 43, 0, 0, 0, 0), Call(4, Port, 45, 0, 2, 0, 2), Call(6, Port, 45, 2, 4, 0, 2, true), Call(8, Port, 44, 0, 8, 8, 2)];
        Reject(new(joined, peekOnly), "quota_temporal_coverage");
        var over = v.Calls.Append(Call(32, Port, 45, 10, 3, 10, 10, true)).ToArray();
        Reject(v with
        {
            Calls = over
        }, "quota_temporal_request_front");
        var streams = v.Joined.Guest.Streams.ToArray();
        streams[0] = Stream(Port, 12, 11);
        Reject(v with
        {
            Joined = v.Joined with
            {
                Guest = v.Joined.Guest with
                {
                    AggregateExtent = 42,
                    Streams = streams
                }
            }
        }, "quota_temporal_response_front");
        var gap = v.Calls.ToArray();
        gap[3] = gap[3] with
        {
            Offset = 3
        };
        Reject(v with
        {
            Calls = gap
        }, "quota_temporal_socket_offset");
        var wrong = v.Calls.ToArray();
        wrong[1] = wrong[1] with
        {
            Connection = wrong[1].Connection with
            {
                RemotePort = 45000
            }
        };
        Reject(v with
        {
            Calls = wrong
        }, "quota_temporal_socket_shape");
    }

    [HostTest("quota HTTP syscall order rejects response before consumption FIFO crossing and orphan or duplicate intervals")]
    private static void Ordering()
    {
        var v = Fixture();
        // Original counters still agree: the receive finishes between two
        // sends, so the first response send illegally precedes full consumption.
        var calls = v.Calls.ToArray();
        calls[3] = calls[3] with
        {
            RecordIndex = 11,
            CallRecordIndex = 10,
            Sent = 3
        };
        calls[4] = calls[4] with
        {
            Received = 2
        };
        Reject(v with
        {
            Calls = calls.OrderBy(c => c.RecordIndex).ToArray()
        }, "quota_temporal_request_before_response");
        // Request2 consumption occurs before response1's final accepted send.
        calls = v.Calls.ToArray();
        calls[6] = calls[6] with
        {
            RecordIndex = 11,
            CallRecordIndex = 10,
            Sent = 3
        };
        calls[5] = calls[5] with
        {
            Received = 10
        };
        Reject(v with
        {
            Calls = calls.OrderBy(c => c.RecordIndex).ToArray()
        }, "quota_temporal_fifo");
        Reject(v with
        {
            Joined = v.Joined with
            {
                Intervals = v.Joined.Intervals.Skip(1).ToArray()
            }
        }, "quota_temporal_message");
        Reject(v with
        {
            Joined = v.Joined with
            {
                Intervals = v.Joined.Intervals.Append(v.Joined.Intervals[0]).ToArray()
            }
        }, "quota_temporal_message");
        var bindings = v.Joined.Bindings.ToArray();
        bindings[1] = bindings[1] with
        {
            ConnectionId = 1
        };
        Reject(v with
        {
            Joined = v.Joined with
            {
                Bindings = bindings
            }
        }, "quota_temporal_binding");
    }

    private static void Reject(Vector v, string reason)
    {
        try
        {
            _ = Inspect(v);
        }
        catch (ToolFailure error) when (error.Reason == reason)
        {
            return;
        }

        throw new InvalidOperationException("expected quota temporal rejection " + reason);
    }
}
