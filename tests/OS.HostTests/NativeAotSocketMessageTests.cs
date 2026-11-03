using OS.Tooling;

internal static class NativeAotSocketMessageTests
{
    private const ulong Identity = 0x100000001;
    private const string Message = "M16_013_MESSAGE identity=4294967297 ordinal=1 nr=46 fd=6 generation=2 header=8192 flags=16384 table=12288 vectors=2 total=5";
    private const string First = "M16_013_VECTOR identity=4294967297 ordinal=1 index=0 address=16384 bytes=2";
    private const string Second = "M16_013_VECTOR identity=4294967297 ordinal=1 index=1 address=20480 bytes=3";
    private static string Call(ulong number = 46, ulong identity = Identity, ulong ordinal = 1, ulong fd = 6,
        ulong header = 8192, ulong flags = 16384) =>
        $"M16_008_CALL identity={identity} nr={number} a0={fd} a1={header} a2={flags} a3=0 a4=0 a5=0 rip=4194818 rsp=1879048176 flags=514 fs=0 gs=0 clock=10 valueBits=4 action=1 status=1 ordinal={ordinal} waitSource=0 waitDeadline=0 waitSequence=0 signalDepth=0 signalFrame=0 blockedSignals=0 returnRip=4194818 returnRsp=1879048176 returnFlags=514 returnFs=0 returnGs=0 returnValueBits=4";

    [HostTest("m16-013 value-only message reader joins owned immutable vectors to current call")]
    private static void Joined()
    {
        string[] rows = [Message, First, Second, Call()];
        var before = (string[])rows.Clone();
        var values = NativeAotSocketMessages.Read(rows);
        HostCheck.Equal(1, values.Count);
        var value = values[0];
        HostCheck.Equal(0, value.RecordIndex);
        HostCheck.Equal(3, value.CallRecordIndex);
        HostCheck.Equal(Identity, value.Identity);
        HostCheck.Equal(1UL, value.Ordinal);
        HostCheck.Equal(46UL, value.Number);
        HostCheck.Equal(6U, value.Descriptor);
        HostCheck.Equal(2U, value.Generation);
        HostCheck.Equal(5U, value.Total);
        HostCheck.Equal(new NativeAotSocketMessages.Vector(20480, 3), value.Vectors[1]);
        HostCheck.True(rows.SequenceEqual(before));
        rows[1] = "changed";
        HostCheck.Equal(new NativeAotSocketMessages.Vector(16384, 2), value.Vectors[0]);
        HostCheck.Equal(0, NativeAotSocketMessages.Read([Call()]).Count); // Complete consumer requires message use.
    }

    [HostTest("socket message witnesses retain bounded larger count and aggregate with complete original rows")]
    private static void LargerMessages()
    {
        foreach (var (count, bytes) in new[] { (17, 8192), (1024, 1024) })
        {
            string[] rows = new string[count + 2];
            rows[0] = $"M16_013_MESSAGE identity={Identity} ordinal=1 nr=46 fd=6 generation=2 header=8192 flags=16384 table=12288 vectors={count} total={count * bytes}";
            for (int i = 0; i < count; i++)
                rows[i + 1] = $"M16_013_VECTOR identity={Identity} ordinal=1 index={i} address=32768 bytes={bytes}";
            rows[^1] = Call();
            var original = rows.ToArray();
            var message = NativeAotSocketMessages.Read(rows).Single();
            HostCheck.Equal((uint)(count * bytes), message.Total);
            HostCheck.Equal(count, message.Vectors.Count);
            HostCheck.True(rows.SequenceEqual(original));
            rows[1] = "changed";
            HostCheck.Equal(new NativeAotSocketMessages.Vector(32768, (ulong)bytes), message.Vectors[0]);
        }
    }

    [HostTest("m16-013 value-only message reader rejects malformed missing reordered duplicate and mismatched joins")]
    private static void Negatives()
    {
        foreach (string[] rows in new string[][]
        {
            [First, Call()], [Message, First, Call()], [Message, Second, First, Call()],
            [Message, First, Second], [Message, First, Second, Message, First, Second, Call()],
            [Message.Replace("total=5", "total=6"), First, Second, Call()],
            [Message.Replace("vectors=2", "vectors=17"), First, Second, Call()],
            [Message.Replace("vectors=2", "vectors=1025"), First, Second, Call()],
            [Message.Replace("total=5", "total=1048577"), First, Second, Call()],
            [Message.Replace("generation=2", "generation=0"), First, Second, Call()],
            [Message.Replace("flags=16384", "flags=1"), First, Second, Call(flags: 1)],
            [Message.Replace("ordinal=1", "ordinal=01"), First, Second, Call()],
            [Message + " extra=0", First, Second, Call()],
            [Message, First.Replace("bytes=2", "bytes=65537"), Second, Call()],
            [Message, First.Replace("address=16384", "address=18446744073709551615"), Second, Call()],
            [Message, First, Second, Call(number: 47)], [Message, First, Second, Call(fd: 7)],
            [Message, First, Second, Call(header: 8193)], [Message, First, Second, Call(flags: 0)],
            [Message, First, Second, Call(identity: Identity + 1)], [Message, First, Second, Call(ordinal: 2)],
            [Message, First, Second, Call().Replace("returnValueBits=4", "returnValueBits=5")],
            [Message, First, Second, Call(), "M16_013_UNKNOWN value=0"]
        })
        {
            bool rejected = false;
            try
            {
                NativeAotSocketMessages.Read(rows);
            }
            catch (ToolFailure) { rejected = true; }
            HostCheck.True(rejected, string.Join(" | ", rows));
        }
    }
}
