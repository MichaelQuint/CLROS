using OS.Kernel.Compatibility.LinuxX64;

namespace OS.Tooling;
public static partial class QemuTranscripts
{
    // Literal Linux ABI vectors and recursive order are independent of the
    // emitter. Only the instruction RIP is supplied by the canonical package.
    private static void ValidateLinuxNestedSignalCalls(Func<string> take, string variant, out ulong identity)
    {
        bool exhaustion = variant == "AlternateStackExhaustion";
        List<SignalNestedCall> expected = [new(39, Value: 1), new(186, Value: 1), new(158, A0: 0x1002, A1: 0x402000), new(13, A0: 10, A1: 0x401100, A3: 8), new(131, A0: 0x401120), new(14, A0: 2, A1: 0x401140, A3: 8), new(234, A0: 1, A1: 1, A2: 10)];
        for (uint depth = 1; depth <= (exhaustion ? 3U : 2U); depth++)
        {
            expected.AddRange([new(186, depth, Value: 1), new(14, depth, 2, 0, 0x401158, 8), new(131, depth, 0, 0x4011a0), new(14, depth, 2, 0x401260 + (depth - 1) * 8, 0, 8)]);
            if (exhaustion || depth == 1)
                expected.Add(new(234, depth, 1, 1, 10));
        }

        if (!exhaustion)
        {
            for (uint depth = 2; depth > 0; depth--)
                expected.AddRange([new(14, depth, 2, 0, 0x401158, 8), new(131, depth, 0, 0x4011a0), new(1, depth, 1, 0x4001a0, 15, Value: 15), new(15, depth, 1, 0x4001a0, 15, Value: depth == 2 ? 0x23456789UL : 0x12345678UL, Restorer: true)]);
            expected.AddRange([new(14, A0: 2, A2: 0x401150, A3: 8), new(131, A1: 0x401180), new(1, A0: 1, A1: 0x400180, A2: 15, Value: 15), new(231, A0: 42)]);
        }

        var instructions = LinuxSignalProofFixture.ExpectedNestedInvocations(variant);
        byte[] elf = LinuxSignalProofFixture.CreateElf(variant);
        Need(expected.Count == instructions.Count && expected.Count == (exhaustion ? 22 : 28), "m16_009_nested_inventory");
        for (int i = 0; i < expected.Count; i++)
        {
            var call = expected[i];
            var instruction = instructions[i];
            if (call.Nr == 1)
                Need(take() == "M16_005_WRITE fd=1 bytes=15 data=" + (call.Depth == 0 ? "M16_SIGNALS_OK!" : "M16_HANDLER_OK!"), "m16_009_nested_witness");
            var row = ReadRecord(take(), "M16_008_CALL", "identity:dec", "nr:dec", "a0:dec", "a1:dec", "a2:dec", "a3:dec", "a4:dec", "a5:dec", "rip:dec", "rsp:dec", "flags:dec", "fs:dec", "gs:dec", "clock:dec", "valueBits:dec", "action:dec", "status:dec");
            ulong rip = instruction.ReturnRip;
            Need(instruction.Number == call.Nr && instruction.Depth == call.Depth && instruction.Restorer == call.Restorer && row["identity"] == 0x100000001 && row["nr"] == call.Nr && row["rip"] == rip && rip is >= 0x400202 and < 0x400f00 && elf[(int)(rip - 0x400002)] == 15 && elf[(int)(rip - 0x400001)] == 5 && row["a0"] == call.A0 && row["a1"] == call.A1 && row["a2"] == call.A2 && row["a3"] == call.A3 && row["a4"] == 0 && row["a5"] == 0, "m16_009_nested_call_binding");
            ulong rsp = call.Depth switch
            {
                0 => 0x405000,
                1 => 0x406c38,
                2 => 0x4067b8,
                3 => 0x406338,
                _ => 0
            };
            Need(row["rsp"] == rsp + (call.Restorer ? 8UL : 0) && row["fs"] == (i < 2 ? 0UL : 0x402000UL) && row["gs"] == 0 && row["clock"] == 0 && LinuxSyscallGateway.ValidateUserFlags(row["flags"], false) && (row["flags"] & 0x400) == 0, "m16_009_nested_envelope");
            ulong action = call.Restorer ? 5UL : call.Nr == 231 ? 2UL : 1UL;
            Need(row["valueBits"] == call.Value && row["action"] == action && row["status"] == (action == 2 ? 2UL : 1UL), "m16_009_nested_outcome");
        }

        identity = 0x100000001;
    }
}
