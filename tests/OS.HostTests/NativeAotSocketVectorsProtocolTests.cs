using System.Text;
using System.Text.RegularExpressions;
using OS.Tooling;

internal static class NativeAotSocketVectorsProtocolTests
{
    private static List<string> Rows(bool policy=false)
    {
        void Policy(NativeAotThreadingTranscriptTests.Vector v)
        {
            void Create(int fd)=>v.Call(41,fd,[2,0x80001,6]);
            void Bind(int result,uint fd)=>v.Call(49,result,[fd,0x500200,16]);
            for(int i=0;i<2;i++){Create(6);Bind(-13,6);v.Call(3,0,[6]);}
            Create(6);Bind(0,6);v.Call(50,0,[6,1]);Create(7);Bind(-98,7);
            v.Call(3,0,[7]);v.Call(3,0,[6]);
            for(int fd=6;fd<16;fd++)Create(fd);Create(-24);
            for(uint fd=15;fd>=6;fd--)v.Call(3,0,[fd]);
        }
        void Connection(NativeAotThreadingTranscriptTests.Vector vector, int index, uint generation)
        {
            void Message(bool send, int result, uint first, uint second, uint gap = 0) =>
                NativeAotSocketProtocolTests.MessageIo(vector, send ? 46u : 47u, result, first + second, generation, first, gap);
            if (index == 0) { Message(false, -11, 17, 47); Message(false, 3, 17, 47); Message(false, 61, 14, 47); }
            else Message(false, 64, 17, 47);
            if (index == 2) { Message(false, -11, 3, 3, 2); vector.Call(233, 0, [5, 1, 7, 0x500300]); }
            else
            {
                Message(false, index == 1 ? -104 : 0, 1, 1, 1);
                if (index != 1)
                {
                    uint expected = index == 0 ? 524288u : 64u, batch = Math.Min(65536u, expected), split = batch / 3;
                    for (uint sent = 0; sent < expected;)
                    {
                        uint position = sent % batch;
                        uint first = position < split ? split - position : batch - position, second = position < split ? batch - split : 0;
                        if (sent == 8192) { Message(true, -11, first, second); Message(true, -11, first, second); }
                        uint count = Math.Min(8192u, batch - position); Message(true, (int)count, first, second); sent += count;
                    }
                    vector.Call(48, 0, [7, 1]);
                }
            }
        }
        return NativeAotSocketProtocolTests.Vector(asynchronous: true, vectorConnection: Connection,
            vectorOutput: policy?NativeAotSocketVectorsOutputTests.Output().Replace("TEST_START nativeaot.socket.vectors\n",
                "TEST_START nativeaot.socket.vectors\n"+NativeAotSocketVectorsOutputTests.PolicyRows,StringComparison.Ordinal):NativeAotSocketVectorsOutputTests.Output(),
            policyPrefix:policy?Policy:null).Rows;
    }

    private static QemuTranscripts.ThreadingProtocolReport Inspect(IEnumerable<string> rows) =>
        QemuTranscripts.InspectNativeAotSocketVectorsProtocolForTests(NativeAotThreadingTranscriptTests.Elf(),
            Encoding.UTF8.GetBytes(string.Join("\r\n", rows) + "\r\n"),
            address => address is 0x400200 or 0x400202, address => address == 0x400202, 8192, 12288);

    [HostTest("m16-013 complete synthetic vector leaf retains scheduling mappings generations native remainders and cancellation")]
    private static void Valid()
    {
        var result = Inspect(Rows()); HostCheck.Equal(3, result.Reaped); HostCheck.True(result.Calls > 40 && result.Transfers > 0);
    }

    [HostTest("m10-008 complete policy replay preserves failed-bind phase capacity generations and final cleanup")]
    private static void PolicyReplay()
    {
        var rows=Rows(policy:true);var result=Inspect(rows);HostCheck.Equal(3,result.Reaped);
        foreach(var change in new (string From,string To)[]{
            ("nr=50 a0=6 a1=1 ", "nr=50 a0=6 a1=2 "),
            ("valueBits=18446744073709551603", "valueBits=18446744073709551518"),
            ("valueBits=18446744073709551592", "valueBits=18446744073709551511"),
            ("generation=3 ","generation=1 ")})
            Reject(rows.Select(row=>row.Replace(change.From,change.To,StringComparison.Ordinal)),change.From);
        Reject(rows.Where(row=>!row.StartsWith("M16_011_CLOSE fd=5 ",StringComparison.Ordinal)));
    }

    [HostTest("m16-013 complete vector leaf rejects unjoined snapshots forged generations mappings and incomplete cleanup")]
    private static void Invalid()
    {
        var rows = Rows();
        Reject(rows.Where(row => !row.StartsWith("M16_013_", StringComparison.Ordinal)));
        Reject(rows.Select(row => row.StartsWith("M16_013_MESSAGE ", StringComparison.Ordinal) ? Regex.Replace(row, @"generation=\d+", "generation=99") : row));
        Reject(rows.Select(row => row.StartsWith("M16_013_VECTOR ", StringComparison.Ordinal) ? Regex.Replace(row, @"address=\d+", "address=140733193388032") : row));
        Reject(rows.Where(row => !row.StartsWith("M16_011_CLOSE fd=5 ", StringComparison.Ordinal)));
        Reject(rows.Select(row => row.Replace("sent=524352", "sent=524353", StringComparison.Ordinal)));
    }

    private static void Reject(IEnumerable<string> rows,string reason="replay")
    {
        bool rejected = false; try { _ = Inspect(rows); } catch (ToolFailure) { rejected = true; }
        HostCheck.True(rejected,reason);
    }
}
