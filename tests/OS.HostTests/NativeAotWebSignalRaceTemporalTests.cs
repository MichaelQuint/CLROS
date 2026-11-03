using OS.Tooling;
using Q = OS.Tooling.QemuTranscripts;

internal static partial class NativeAotWebSignalRaceTemporalTests
{
    private static readonly Q.ServiceQuotaSocketIdentity Connection = new(4096, 1, 9, 0, 7, 0x0a00020f, 8080, 0x0a000202, 43000);
    private static Q.WebSignalRaceSocketCall Call(int at, ulong nr, ulong offset, uint bytes, ulong sent, ulong received, bool peek = false) => new(at, 0x100000001, (ulong)at, nr, 7, 4, 9, Connection, offset, bytes, peek, sent, received);
    private static Vector Fixture()
    {
        string first = NativeAotWebSignalRaceDateTests.Response(), second = NativeAotWebSignalRaceDateTests.Response("TWO");
        var joined = NativeAotWebSignalRaceDateTests.Build([first, second], ["ONE", "TWO"]);
        uint request = (uint)NativeAotWebSignalRaceDateTests.Request.Length, response = (uint)first.Length;
        Q.WebSignalRaceSocketCall[] calls = [Call(10, 288, 0, 0, 0, 0), Call(20, 45, 0, request - 2, 0, request - 2), Call(22, 47, request - 2, 2, 0, request - 2, true), Call(24, 45, request - 2, 2, 0, request), Call(30, 44, 0, response - 1, response - 1, request), Call(32, 46, response - 1, 1, response, request), Call(40, 45, request, request, response, request * 2), Call(50, 44, response, (uint)second.Length + 2, response + (uint)second.Length + 2, request * 2)];
        return new(joined, calls);
    }

    private static Q.WebSignalRaceTemporalInspection Read(Vector vector) => Q.InspectNativeAotWebSignalRaceTemporalForTests(vector.Joined, vector.Calls);
    private static void Reject(Vector vector, string? expected = null)
    {
        string? reason = null;
        try
        {
            _ = Read(vector);
        }
        catch (ToolFailure error)
        {
            reason = error.Reason;
        }

        HostCheck.True(reason is not null);
        if (expected is not null)
            HostCheck.Equal(expected, reason);
    }

    [HostTest("web race syscall slices preserve real CALL indices consuming bytes and accepted unsent tails")]
    private static void OriginalSlices()
    {
        var vector = Fixture();
        var original = vector.Calls.ToArray();
        var result = Read(vector);
        int request = NativeAotWebSignalRaceDateTests.Request.Length, response = NativeAotWebSignalRaceDateTests.Response().Length;
        HostCheck.True(result.SocketCalls == 8 && result.RequestBytesConsumed == (ulong)request * 2 && result.ResponseBytesAccepted == (ulong)response * 2 + 2);
        HostCheck.True(result.Intervals.Count == 4 && result.Intervals.All(i => i.Connection == Connection && i.Slices.All(s => !s.Original.Peek) && i.Slices.Sum(s => (long)s.ByteCount) == i.Packet.End - i.Packet.Start));
        var firstRequest = result.Intervals.Single(i => i.Packet.ExchangeId == 1 && !i.Packet.Response);
        HostCheck.True(firstRequest.Slices.Select(s => s.Original.CallRecordIndex).SequenceEqual(new[] { 20, 24 }) && firstRequest.Slices.Select(s => s.ByteCount).SequenceEqual(new[] { (uint)request - 2, 2U }));
        var last = result.Intervals.Single(i => i.Packet.ExchangeId == 2 && i.Packet.Response).Slices.Single();
        HostCheck.True(last.StreamOffset == (ulong)response && last.ByteCount == response && last.Original.Bytes == response + 2 && last.Original.CallRecordIndex == 50);
        HostCheck.True(vector.Calls.SequenceEqual(original));
        Array.Clear(vector.Calls);
        HostCheck.True(firstRequest.Slices[1].Original is { Identity: 0x100000001, DescriptorGeneration: 4, OpenDescription: 9 });
        bool immutable = false;
        try
        {
            ((IList<Q.WebSignalRaceHttpSyscallSlice>)firstRequest.Slices).Clear();
        }
        catch (NotSupportedException)
        {
            immutable = true;
        }

        HostCheck.True(immutable);
    }

    [HostTest("web race syscall coverage refuses peek consumption wrong lifetimes counters fronts and FIFO")]
    private static void Refusals()
    {
        var vector = Fixture();
        Q.WebSignalRaceSocketCall[] Mutate(int index, Func<Q.WebSignalRaceSocketCall, Q.WebSignalRaceSocketCall> change)
        {
            var copy = vector.Calls.ToArray();
            copy[index] = change(copy[index]);
            return copy;
        }

        Reject(vector with
        {
            Calls = Mutate(3, c => c with { Connection = Connection with { Generation = 10 } })
        });
        Reject(vector with
        {
            Calls = Mutate(3, c => c with { OpenDescription = 10 })
        });
        Reject(vector with
        {
            Calls = Mutate(3, c => c with { Received = c.Received + 1 })
        });
        Reject(vector with
        {
            Calls = Mutate(3, c => c with { CallRecordIndex = 22 })
        });
        Reject(vector with
        {
            Calls = Mutate(3, c => c with { DescriptorGeneration = 0 })
        });
        uint request = (uint)NativeAotWebSignalRaceDateTests.Request.Length, response = (uint)NativeAotWebSignalRaceDateTests.Response().Length;
        var onlyFirst = vector.Joined with
        {
            Intervals = vector.Joined.Intervals.Take(2).ToArray()
        };
        Q.WebSignalRaceSocketCall[] peeks = [Call(10, 43, 0, 0, 0, 0), Call(20, 45, 0, request - 2, 0, request - 2), Call(22, 47, request - 2, 2, 0, request - 2, true), Call(30, 44, 0, response * 2, response * 2, request - 2)];
        Reject(new(onlyFirst, peeks), "web_race_temporal_coverage");
        var over = vector.Calls.Append(Call(60, 47, request * 2, 1, response * 2 + 2, request * 2, true)).ToArray();
        Reject(vector with
        {
            Calls = over
        }, "web_race_temporal_request_front");
        Reject(vector with
        {
            Calls = Mutate(7, c => c with { Bytes = c.Bytes - 3, Sent = c.Sent - 3 })
        }, "web_race_temporal_response_front");
        var order = vector.Calls.ToArray();
        order[3] = order[3] with
        {
            CallRecordIndex = 35,
            Ordinal = 35,
            Sent = response
        };
        order[4] = order[4] with
        {
            Received = request - 2
        };
        order[5] = order[5] with
        {
            Received = request - 2
        };
        Reject(vector with
        {
            Calls = order.OrderBy(c => c.CallRecordIndex).ToArray()
        }, "web_race_temporal_request_before_response");
        var fifo = vector.Calls.ToArray();
        fifo[6] = fifo[6] with
        {
            CallRecordIndex = 31,
            Ordinal = 31,
            Sent = response - 1
        };
        fifo[5] = fifo[5] with
        {
            Received = request * 2
        };
        Reject(vector with
        {
            Calls = fifo.OrderBy(c => c.CallRecordIndex).ToArray()
        }, "web_race_temporal_fifo");
        var ranges = vector.Joined.Intervals[0].Ranges.ToArray();
        ranges[0] = ranges[0] with
        {
            ByteCount = ranges[0].ByteCount - 1
        };
        Reject(vector with
        {
            Joined = vector.Joined with
            {
                Intervals = [vector.Joined.Intervals[0] with { Ranges = ranges }, .. vector.Joined.Intervals.Skip(1)]
            }
        }, "web_race_temporal_packet_ranges");
    }

    [HostTest("web temporal copied identities permit slot fifteen and reject either sixteen boundary")]
    private static void SixteenSlotBoundary()
    {
        var vector = Fixture();
        Vector At(uint slot, uint index) => vector with
        {
            Calls = vector.Calls.Select(c => c with { Connection = c.Connection with { Slot = slot, Index = index } }).ToArray()
        };
        var accepted = Read(At(15, 15));
        HostCheck.True(accepted.Intervals.All(i => i.Connection.Slot == 15 && i.Connection.Index == 15));
        Reject(At(16, 15), "web_race_temporal_call_shape");
        Reject(At(15, 16), "web_race_temporal_call_shape");
    }
}
