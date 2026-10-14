using System.Buffers.Binary;
using System.Text;
using Writer = OS.Tooling.LinuxThreadProofFixture.Writer;

namespace OS.Tooling;

public static partial class LinuxSignalProofFixture
{
    public const int FaultHandlerOffset = 0x800;
    public const ulong UserGs = ParentTls + 0x100;
    public const ulong UnmappedFaultAddress = 0x150000000; // Above every retained physical identity, below the 64-GiB user end.

    /// <summary>Canonical actual fault instruction, saved hardware RIP/RSP and
    /// explicit repair continuation. These addresses bind mechanical package
    /// instructions only; trap/error/code expectations are independently checked
    /// by the transcript validator. Construction implies no guest execution.</summary>
    public readonly record struct FaultInvocation(ulong TriggerRip, ulong SavedRip, ulong ResumeRip, ulong SavedRsp);
    public static bool IsFaultVariant(string variant) => FaultVariants.Contains(variant, StringComparer.Ordinal);
    public static FaultInvocation ExpectedFault(string variant)
    {
        if (!IsFaultVariant(variant)) throw new ToolFailure("m16_009_fixture_variant");
        return BuildFault(variant).Fault;
    }

    private static ulong RegisterSeed(uint register) => 0x1122334400000000UL + register * 0x01010101UL;
    private static void Mov64(Writer w, uint register, ulong value)
    { w.Op(register < 8 ? 0x48UL : 0x49UL,1); w.Op(0xb8 + (register & 7),1); w.Op(value,8); }
    private static void Compare64(Writer w, ulong expected)
    { Mov64(w,11,expected); w.Op(0xd8394c,3); w.Branch(0x85); } // CMP RAX,R11.
    private static void ToRax(Writer w, uint register)
    { w.Op(register < 8 ? 0x48UL : 0x4cUL,1); w.Op(0x89,1); w.Op(0xc0 | ((register & 7) << 3),1); }
    private static void ContextValue(Writer w, uint offset)
    { w.Op(0x828b48,3); w.Op(offset,4); } // MOV RAX,[RDX+disp32], RDX=ucontext.
    private static void ContextEdit(Writer w, uint offset, uint value)
    { w.Op(0x82c748,3); w.Op(offset,4); w.Op(value,4); }
    private static void GsValue(Writer w) { w.Op(0x65,1); w.Op(0x25048b48,8); }

    private static (byte[] Elf, Invocation[] Calls, FaultInvocation Fault) BuildFault(string variant)
    {
        bool fatal = variant == "FaultFatal", badReturn = variant == "FaultReturn";
        bool nx = variant is "FaultExecute" or "FaultReturn", gp = variant == "FaultGp", write = variant == "FaultWrite";
        ulong savedRsp = variant == "FaultStack" ? 0UL : ParentStackTop;
        ulong address = gp ? 0 : write ? Address(WitnessOffset) : nx ? 0x403000UL : UnmappedFaultAddress;
        uint vector = gp ? 13U : 14U, error = gp ? 0U : write ? 7U : nx ? 21U : 4U;
        int code = gp ? 128 : write || nx ? 2 : 1;
        byte[] elf = LinuxThreadProofFixture.CreateMechanicalImage(Witness);
        Encoding.ASCII.GetBytes(HandlerWitness).CopyTo(elf,HandlerWitnessOffset);
        void U16(int at,ushort value) => BinaryPrimitives.WriteUInt16LittleEndian(elf.AsSpan(at),value);
        void U32(int at,uint value) => BinaryPrimitives.WriteUInt32LittleEndian(elf.AsSpan(at),value);
        void U64(int at,ulong value) => BinaryPrimitives.WriteUInt64LittleEndian(elf.AsSpan(at),value);
        U64(ActionOffset,fatal ? 0 : Address(FaultHandlerOffset)); U64(ActionOffset+8,0x0c000004);
        U64(ActionOffset+16,Address(RestorerOffset));
        U64(StackOffset,AlternateBase); U64(StackOffset+16,4096);
        U64(0x2000,0x13579bdf); U64(0x2100,0x2468ace0);
        U16(ControlOffset,0x027f); U32(MxcsrOffset,0x5f80);

        Writer p = new(elf,EntryOffset);
        p.Op(0xc031,2); p.Op(0xc88c66,3); p.Compare(0x3b);
        p.Immediate(4,(uint)ParentStackTop); p.Store32(RedZoneOffset,0x13579bdf);
        p.Call(39,0,0,0); p.Positive(); p.StoreRax(PidOffset);
        p.Call(186,0,0,0); p.CompareMemory(PidOffset);
        p.Call(158,0x1002,ParentTls,0); p.Compare(0);
        p.Call(158,0x1001,UserGs,0); p.Compare(0);
        p.Call(13,11,Address(ActionOffset),0,8); p.Compare(0);
        p.Call(131,Address(StackOffset),0,0); p.Compare(0);
        p.Rip(0x2dd9,2,ControlOffset); p.Rip(0x15ae0f,3,MxcsrOffset);
        p.Immediate(0,0x11223344); p.Op(0xc06e0f66,4); // XMM0.
        p.Immediate(0,0x55667788); p.Op(0xf86e0f4466,5); // XMM15.
        for (uint register=0;register<16;register++) if (register!=4) Mov64(p,register,RegisterSeed(register));
        p.Immediate(4,(uint)savedRsp); p.Op(0xfd,1);
        ulong trigger = Address(p.Position);
        if (gp) p.Op(0xfa,1); // CLI at CPL3: actual GP/error0, no GPR scratch.
        else if (write) p.Store32(WitnessOffset,0xdeadbeef); // RX text remains immutable.
        else if (nx) { p.Op(0xe9,1); p.Op(unchecked((uint)(0x3000-(p.Position+4))),4); }
        else { p.Op(0xa148,2); p.Op(UnmappedFaultAddress,8); } // MOV RAX,moffs64: fault leaves full RAX intact.
        FaultInvocation fault = new(trigger,nx ? 0x403000UL : trigger,Address(p.Position),savedRsp);
        p.Compare(EditedRax);
        // Check R11 before using it as the comparison scratch. All other user
        // registers are still untouched; RAX has already proved its saved edit.
        foreach (uint register in new uint[] {11,1,2,3,5,6,7,8,9,10,12,13,14,15})
        { ToRax(p,register); Compare64(p,RegisterSeed(register)); }
        ToRax(p,4); p.Compare((long)ParentStackTop);
        p.Op(0xc07e0f66,4); p.Compare(0x11223344);
        p.Op(0xf87e0f4466,5); p.Compare(0x55667788);
        Fp(p,FpOut); p.Memory(FpOut,0x027f); p.Memory(FpOut+4,0x5f80);
        Direction(p,0x400); p.Op(0xfc,1);
        p.Fs(); p.Compare(0x13579bdf); GsValue(p); p.Compare(0x2468ace0);
        p.Memory(CountOffset,1); p.Memory(RedZoneOffset,0x13579bdf);
        p.Call(14,2,0,Address(MaskOut),8); p.Compare(0); p.Memory(MaskOut,0,true);
        p.Call(131,0,Address(StackOut),0); p.Compare(0); p.Memory(StackOut+8,0);
        p.Call(1,1,Address(WitnessOffset),(uint)Witness.Length); p.Compare(Witness.Length);
        p.Call(231,42,0,0); p.JumpFailure();
        if (p.Position>FaultHandlerOffset) throw new ToolFailure("m16_009_fault_parent_overlap");

        Writer h = new(elf,FaultHandlerOffset);
        ToRax(h,7); h.Compare(11); h.Op(0x068b,2); h.Compare(11);
        h.Op(0x08466348,4); h.Compare(code); h.Op(0x10468b48,4); Compare64(h,address);
        ContextValue(h,0); h.Compare(6);
        // Linux sigcontext register order is independent of the native gateway.
        uint[] scRegisters = [8,9,10,11,12,13,14,15,7,6,5,3,2,0,1];
        for (uint i=0;i<scRegisters.Length;i++)
        { ContextValue(h,40+i*8); Compare64(h,RegisterSeed(scRegisters[i])); }
        ContextValue(h,160); h.Compare((long)savedRsp);
        ContextValue(h,168); h.Compare((long)fault.SavedRip);
        ContextValue(h,176); h.Op(0x25,1); h.Op(0x10400,4); h.Compare(0x10400);
        ContextValue(h,192); h.Compare(error); ContextValue(h,200); h.Compare(vector);
        ContextValue(h,216); Compare64(h,address);
        ContextEdit(h,144,EditedRax);
        if (!badReturn) ContextEdit(h,168,(uint)fault.ResumeRip);
        ContextEdit(h,160,(uint)ParentStackTop);
        // Independently inspect the saved FP image as well as the fresh handler
        // state; do not mistake successful frame construction for restoration.
        h.Op(0x928b4c,3); h.Op(224,4); // R10=uc.mcontext.fpstate.
        h.Op(0x02b70f41,4); h.Compare(0x027f);
        h.Op(0x18428b41,4); h.Compare(0x5f80);
        h.Op(0x828b41,3); h.Op(160,4); h.Compare(0x11223344);
        h.Op(0x828b41,3); h.Op(400,4); h.Compare(0x55667788);
        h.Memory(CountOffset,0); h.Store32(CountOffset,1); Direction(h,0);
        Fp(h,HandlerFpOut); h.Memory(HandlerFpOut,0x37f); h.Memory(HandlerFpOut+4,0x1f80);
        h.Op(0xc07e0f66,4); h.Compare(0); h.Op(0xf87e0f4466,5); h.Compare(0);
        h.Fs(); h.Compare(0x13579bdf); GsValue(h); h.Compare(0x2468ace0);
        h.Op(0xc0760f66,4); h.Op(0xff760f4566,5); // Clobber XMM0/15 before checked return.
        h.Call(186,0,0,0); h.CompareMemory(PidOffset);
        h.Call(14,2,0,Address(HandlerMaskOut),8); h.Compare(0); h.Memory(HandlerMaskOut,0x400,true);
        h.Call(131,0,Address(HandlerStackOut),0); h.Compare(0); h.Memory(HandlerStackOut+8,1);
        h.Call(1,1,Address(HandlerWitnessOffset),(uint)HandlerWitness.Length); h.Compare(HandlerWitness.Length);
        h.Op(0xc3,1);
        if (h.Position>RestorerOffset) throw new ToolFailure("m16_009_fault_handler_overlap");
        Writer r = new(elf,RestorerOffset); r.Immediate(0,15); r.Finish(); r.Op(0x0b0f,2);
        Writer f = new(elf,FailureOffset); f.Op(0xfc,1); f.Call(231,99,0,0); f.Op(0x0b0f,2);
        uint[] parent = [39,186,158,158,13,131,14,131,1,231], handler = [186,14,131,1];
        if (p.Rips.Count!=parent.Length || h.Rips.Count!=handler.Length) throw new ToolFailure("m16_009_fault_instruction_inventory");
        List<Invocation> calls=[];
        for (int i=0;i<6;i++) calls.Add(new(parent[i],p.Rips[i],false));
        if (!fatal)
        {
            for (int i=0;i<handler.Length;i++) calls.Add(new(handler[i],h.Rips[i],true));
            calls.Add(new(15,r.Rips[0],true));
            if (!badReturn) for (int i=6;i<parent.Length;i++) calls.Add(new(parent[i],p.Rips[i],false));
        }
        return (elf,calls.ToArray(),fault);
    }
}
