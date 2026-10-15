using OS.Kernel.Compatibility.LinuxX64;

namespace OS.Tooling;

public static partial class QemuTranscripts
{
    // Called only after canonical package identity and original shared parent
    // geometry admission. The caller independently checks exit/reap/cleanup.
    private static void ValidateLinuxFaultSignalCalls(Func<string> take,string variant,out ulong identity)
    {
        bool fatal = variant == "FaultFatal", badReturn = variant == "FaultReturn", nx = variant is "FaultExecute" or "FaultReturn";
        (ulong Vector,ulong Error,ulong Address,ulong Code,ulong Rsp) expectedFault = variant switch
        {
            "FaultRead" or "FaultFatal" => (14,4,0x150000000,1,0x405000),
            "FaultStack" => (14,4,0x150000000,1,0),
            "FaultWrite" => (14,7,0x400180,2,0x405000),
            "FaultExecute" or "FaultReturn" => (14,21,0x403000,2,0x405000),
            "FaultGp" => (13,0,0,128,0x405000),
            _ => throw new ToolFailure("m16_009_fault_variant")
        };
        LinuxThreadExpected[] expected = [new(39),new(186),new(158,0x1002,0x402000),new(158,0x1001,0x402100),
            new(13,11,0x401100,0,8),new(131,0x401120),new(186),new(14,2,0,0x401158,8),new(131,0,0x4011a0),
            new(1,1,0x4001a0,15),new(15,1,0x4001a0,15),new(14,2,0,0x401150,8),new(131,0,0x401180),
            new(1,1,0x400180,15),new(231,42)];
        var instructions = LinuxSignalProofFixture.ExpectedInvocations(variant);
        var fault = LinuxSignalProofFixture.ExpectedFault(variant);
        byte[] elf = LinuxSignalProofFixture.CreateElf(variant);
        Need(instructions.Count == (fatal ? 6 : badReturn ? 11 : 15) && fault.SavedRsp == expectedFault.Rsp &&
            fault.TriggerRip is >= 0x400202 and < 0x400800 && fault.ResumeRip > fault.TriggerRip && fault.ResumeRip < 0x400800 &&
            fault.SavedRip == (nx ? 0x403000UL : fault.TriggerRip), "m16_009_fault_instruction_inventory");
        Dictionary<string,ulong> Row(string prefix,params string[] fields) => ReadRecord(take(),prefix,fields);
        ulong pid = 0, owner = 0;
        void Fault()
        {
            var row = Row("M16_009_FAULT","identity:dec","vector:dec","error:dec","rip:dec","rsp:dec","address:dec","code:dec","delivery:dec");
            Need(row["identity"] == owner && row["vector"] == expectedFault.Vector && row["error"] == expectedFault.Error &&
                row["rip"] == fault.SavedRip && row["rsp"] == expectedFault.Rsp && row["address"] == expectedFault.Address &&
                row["code"] == expectedFault.Code && row["delivery"] == (fatal ? 4UL : 3UL), "m16_009_fault_capture");
        }
        for (int i=0;i<instructions.Count;i++)
        {
            if (i==6) Fault();
            if (i is 9 or 13) Need(take() == "M16_005_WRITE fd=1 bytes=15 data=" + (i==9 ? "M16_HANDLER_OK!" : "M16_SIGNALS_OK!"), "m16_009_fault_witness");
            var row = Row("M16_008_CALL","identity:dec","nr:dec","a0:dec","a1:dec","a2:dec","a3:dec","a4:dec","a5:dec",
                "rip:dec","rsp:dec","flags:dec","fs:dec","gs:dec","clock:dec","valueBits:dec","action:dec","status:dec");
            if (i==0)
            {
                pid = row["valueBits"]; owner = row["identity"];
                Need(pid is > 0 and < int.MaxValue && owner == 0x100000001,"m16_009_fault_identity");
            }
            var call = expected[i]; var instruction = instructions[i];
            ulong rip = instruction.ReturnRip, rsp = i==10 ? 0x406c40UL : i is >=6 and <=9 ? 0x406c38UL : 0x405000UL;
            Need(instruction.Number == call.Nr && instruction.Handler == (i is >=6 and <=10) && row["identity"] == owner &&
                row["nr"] == call.Nr && row["a0"] == call.A0 && row["a1"] == call.A1 && row["a2"] == call.A2 &&
                row["a3"] == call.A3 && row["a4"] == 0 && row["a5"] == 0 && row["rip"] == rip &&
                rip is >= 0x400202 and < 0x400f00 && elf[(int)(rip-0x400002)] == 15 && elf[(int)(rip-0x400001)] == 5,
                "m16_009_fault_call_binding");
            Need(row["rsp"] == rsp && row["fs"] == (i<2 ? 0UL : 0x402000UL) && row["gs"] == (i<3 ? 0UL : 0x402100UL) &&
                row["clock"] == 0 && (row["flags"] & 2) != 0 && (row["flags"] & ~LinuxSyscallGateway.UserRflagsMask) == 0 &&
                (row["flags"] & 0x400) == 0,"m16_009_fault_call_envelope");
            ulong action = i==14 || i==10 && badReturn ? 2UL : i==10 ? 5UL : 1UL;
            ulong value = i is 0 or 1 or 6 ? pid : i is 9 or 13 ? 15UL : i==10 && !badReturn ? 0x12345678UL : 0;
            Need(row["valueBits"] == value && row["action"] == action && row["status"] == (action==2 ? 2UL : 1UL),"m16_009_fault_call_outcome");
        }
        if (fatal) Fault();
        identity = owner;
    }
}
