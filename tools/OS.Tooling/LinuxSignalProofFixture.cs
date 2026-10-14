using System.Buffers.Binary;
using System.Text;
using Writer = OS.Tooling.LinuxThreadProofFixture.Writer;

namespace OS.Tooling;

/// <summary>C#-authored mechanical signal ELF/package for the M16-009 consumer.
/// One actual thread checks pending/unmask delivery, Linux frame arguments,
/// stack selection, masks, GPR/FP restoration and edited saved RAX. Negative
/// inputs require owned SIGSEGV termination. Construction/byte inventories are
/// host evidence only; no handler, native runtime or guest execution is implied.
/// Geometry matches the existing seven-page mechanical ELF, not an upstream app.</summary>
public static partial class LinuxSignalProofFixture
{
    public static IReadOnlyList<string> SyscallVariants { get; } = Array.AsReadOnly<string>(["AlternateStack", "UserStack", "BadReturn", "BadRestorer"]);
    public static IReadOnlyList<string> FaultVariants { get; } = Array.AsReadOnly<string>(["FaultRead", "FaultWrite", "FaultExecute", "FaultStack", "FaultGp", "FaultReturn", "FaultFatal"]);
    public static IReadOnlyList<string> Variants { get; } = Array.AsReadOnly<string>([.. SyscallVariants, .. FaultVariants, IrqVariant]);
    public const string AppPath = "apps/m16signals.elf", Witness = "M16_SIGNALS_OK!", HandlerWitness = "M16_HANDLER_OK!";
    public const ulong ImageBase = 0x400000, ParentTls = 0x402000, ParentStackTop = 0x405000, AlternateBase = 0x406000;
    public const int ElfBytes = 0x7000, EntryOffset = 0x200, HandlerOffset = 0xa00, RestorerOffset = 0xe00, FailureOffset = 0xf00;
    public const int WitnessOffset = 0x180, HandlerWitnessOffset = 0x1a0;
    public const int ActionOffset = 0x1100, StackOffset = 0x1120, BlockedOffset = 0x1140, EmptyOffset = 0x1148;
    public const int MaskOut = 0x1150, HandlerMaskOut = 0x1158, PidOffset = 0x1160, CountOffset = 0x1164;
    public const int ControlOffset = 0x1168, MxcsrOffset = 0x116c, FpOut = 0x1170, HandlerFpOut = 0x1178;
    public const int StackOut = 0x1180, HandlerStackOut = 0x11a0, RedZoneOffset = 0x4fc0;
    public const uint EditedRax = 0x12345678;

    /// <summary>Actual user SYSCALL instruction/continuation in execution order.
    /// Handler includes its restorer; synthetic kernel exits and exit99 assertion
    /// failures are excluded. The complete transcript must additionally bind
    /// dynamic PID, RSP and independently checked arguments/results.</summary>
    public readonly record struct Invocation(uint Number, ulong ReturnRip, bool Handler);

    public static uint ExpectedExitCode(string variant) { Validate(variant); return variant is "BadReturn" or "BadRestorer" or "FaultReturn" or "FaultFatal" ? 139U : 42U; }
    public static IReadOnlyList<Invocation> ExpectedInvocations(string variant) => IsIrqVariant(variant) ?
        throw new ToolFailure("m16_009_irq_thread_inventory_required") : Array.AsReadOnly(Build(variant).Calls);
    public static byte[] CreateElf(string variant) => IsIrqVariant(variant) ? BuildIrq().Elf : Build(variant).Elf;
    public static byte[] CreatePackage(string variant) => LinuxThreadProofFixture.CreateMechanicalPackage(CreateElf(variant), variant, AppPath, IsIrqVariant(variant));

    /// <summary>Create a new output atomically. Existing paths fail unchanged;
    /// callers retain the package and must separately obtain guest evidence.</summary>
    public static void WritePackage(string path, string variant = "AlternateStack")
    {
        byte[] package = CreatePackage(variant); path = Path.GetFullPath(path);
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        using FileStream output = new(path, FileMode.CreateNew, FileAccess.Write, FileShare.None);
        output.Write(package); output.Flush(true);
    }

    /// <summary>Identify only complete canonical package bytes. Manifest names,
    /// altered/truncated packages and other mechanical inputs grant no identity.</summary>
    public static string PackageVariant(ReadOnlySpan<byte> package)
    {
        foreach (string variant in Variants) if (package.SequenceEqual(CreatePackage(variant))) return variant;
        throw new ToolFailure("m16_009_package_identity");
    }

    /// <summary>Preflight the caller's retained seekable read lock before build
    /// or launch. Success rewinds to zero; failure grants no execution authority.</summary>
    public static void RequirePackage(Stream input)
    {
        if (!input.CanRead || !input.CanSeek || input.Position != 0 || input.Length is < ElfBytes or > ElfBytes + 4096)
            throw new ToolFailure("m16_009_package_identity");
        byte[] observed = new byte[checked((int)input.Length)]; input.ReadExactly(observed);
        _ = PackageVariant(observed); input.Position = 0;
    }

    private static (byte[] Elf, Invocation[] Calls) Build(string variant)
    {
        Validate(variant);
        if (IsFaultVariant(variant)) { var fault = BuildFault(variant); return (fault.Elf, fault.Calls); }
        bool alternate = variant != "UserStack";
        byte[] elf = LinuxThreadProofFixture.CreateMechanicalImage(Witness);
        Encoding.ASCII.GetBytes(HandlerWitness).CopyTo(elf, HandlerWitnessOffset);
        void U16(int at, ushort value) => BinaryPrimitives.WriteUInt16LittleEndian(elf.AsSpan(at), value);
        void U32(int at, uint value) => BinaryPrimitives.WriteUInt32LittleEndian(elf.AsSpan(at), value);
        void U64(int at, ulong value) => BinaryPrimitives.WriteUInt64LittleEndian(elf.AsSpan(at), value);
        U64(ActionOffset, Address(HandlerOffset));
        U64(ActionOffset + 8, 0x04000004UL | (alternate ? 0x08000000UL : 0));
        U64(ActionOffset + 16, variant == "BadRestorer" ? 0x500000UL : Address(RestorerOffset));
        U64(StackOffset, AlternateBase); U64(StackOffset + 16, 4096);
        U64(BlockedOffset, 0x200); U64(0x2000, 0x13579bdf);
        U16(ControlOffset, 0x27f); U32(MxcsrOffset, 0x5f80);

        Writer p = new(elf, EntryOffset);
        p.Op(0xc031, 2); p.Op(0xc88c66, 3); p.Compare(0x3b); // Actual CPL3 CS.
        p.Immediate(4, (uint)ParentStackTop); p.Store32(RedZoneOffset, 0x13579bdf);
        p.Call(39, 0, 0, 0); p.Positive(); p.StoreRax(PidOffset);
        p.Call(186, 0, 0, 0); p.CompareMemory(PidOffset);
        p.Call(158, 0x1002, ParentTls, 0); p.Compare(0);
        p.Call(13, 10, Address(ActionOffset), 0, 8); p.Compare(0);
        p.Call(131, Address(StackOffset), 0, 0); p.Compare(0);
        p.Call(14, 2, Address(BlockedOffset), 0, 8); p.Compare(0);
        p.Arguments(234, 0, 0, 10, 0, 0, 0);
        p.Rip(0x3d8b, 2, PidOffset); p.Rip(0x358b, 2, PidOffset); p.Finish(); p.Compare(0);
        p.Memory(CountOffset, 0); // The blocked send must not execute a handler.
        p.Call(14, 2, 0, Address(MaskOut), 8); p.Compare(0); p.Memory(MaskOut, 0x200, true);
        p.Immediate(3, 0x13579bdf); p.Immediate(0, 0x11223344); p.Op(0xc06e0f66, 4); // MOVD XMM0,EAX.
        p.Rip(0x2dd9, 2, ControlOffset); p.Rip(0x15ae0f, 3, MxcsrOffset); // FLDCW; LDMXCSR.
        p.Op(0xfd, 1); // Interrupted DF must restore; the handler must start clear.
        p.Call(14, 2, Address(EmptyOffset), 0, 8); p.Compare(EditedRax);
        p.Op(0xd889, 2); p.Compare(0x13579bdf); // Handler really clobbers RBX.
        p.Op(0xc07e0f66, 4); p.Compare(0x11223344); // Restored XMM0.
        Fp(p, FpOut); p.Memory(FpOut, 0x27f); p.Memory(FpOut + 4, 0x5f80);
        p.Memory(RedZoneOffset, 0x13579bdf); Direction(p, 0x400); p.Op(0xfc, 1);
        p.Fs(); p.Compare(0x13579bdf); p.Memory(CountOffset, 1);
        p.Call(14, 2, 0, Address(MaskOut), 8); p.Compare(0); p.Memory(MaskOut, 0, true);
        p.Call(131, 0, Address(StackOut), 0); p.Compare(0); p.Memory(StackOut + 8, 0);
        p.Call(1, 1, Address(WitnessOffset), (uint)Witness.Length); p.Compare(Witness.Length);
        p.Call(231, 42, 0, 0); p.JumpFailure();
        if (p.Position > HandlerOffset) throw new ToolFailure("m16_009_parent_overlap");

        Writer h = new(elf, HandlerOffset);
        h.Op(0xf88948, 3); h.Compare(10); // RDI = signo.
        h.Op(0x068b, 2); h.Compare(10); // siginfo.si_signo.
        h.Op(0x08466348, 4); h.Compare(-6); // MOVSXD RAX,[RSI+8]: SI_TKILL.
        h.Op(0x10468b, 3); h.CompareMemory(PidOffset);
        h.Op(0x028b48, 3); h.Compare(6); // ucontext flags.
        h.Op(0x828b48, 3); h.Op(160, 4); h.Compare((long)ParentStackTop); // Saved RSP.
        h.Op(0xe08948, 3); h.Op(0x3d48, 2); h.Op(alternate ? AlternateBase : ParentStackTop - 4096, 4); h.Branch(0x82);
        h.Op(0x3d48, 2); h.Op(alternate ? AlternateBase + 4096 : ParentStackTop - 128, 4); h.Branch(0x83);
        h.Memory(CountOffset, 0); h.Store32(CountOffset, 1); Direction(h, 0);
        Fp(h, HandlerFpOut); h.Memory(HandlerFpOut, 0x37f); h.Memory(HandlerFpOut + 4, 0x1f80);
        h.Op(0xc07e0f66, 4); h.Compare(0); // Fresh handler XMM0.
        h.Fs(); h.Compare(0x13579bdf);
        // ucontext.mcontext.RAX at +144. A real sigreturn must retain this edit.
        h.Op(0x82c748, 3); h.Op(144, 4); h.Op(EditedRax, 4);
        if (variant == "BadReturn")
        { h.Op(0x82c766, 3); h.Op(184, 4); h.Op(8, 2); } // Malicious saved CS=CPL0.
        h.Immediate(3, 0x76543210); h.Op(0xc0760f66, 4); // RBX and XMM0 clobbers.
        h.Call(186, 0, 0, 0); h.CompareMemory(PidOffset);
        h.Call(14, 2, 0, Address(HandlerMaskOut), 8); h.Compare(0); h.Memory(HandlerMaskOut, 0x200, true);
        h.Call(131, 0, Address(HandlerStackOut), 0); h.Compare(0); h.Memory(HandlerStackOut + 8, alternate ? 1UL : 0);
        h.Call(1, 1, Address(HandlerWitnessOffset), (uint)HandlerWitness.Length); h.Compare(HandlerWitness.Length);
        h.Op(0xc3, 1); // RET through the kernel-installed user restorer.
        if (h.Position > RestorerOffset) throw new ToolFailure("m16_009_handler_overlap");
        Writer r = new(elf, RestorerOffset); r.Immediate(0, 15); r.Finish(); r.Op(0x0b0f, 2);
        Writer f = new(elf, FailureOffset); f.Op(0xfc, 1); f.Call(231, 99, 0, 0); f.Op(0x0b0f, 2);

        uint[] pn = [39, 186, 158, 13, 131, 14, 234, 14, 14, 14, 131, 1, 231], hn = [186, 14, 131, 1];
        if (p.Rips.Count != pn.Length || h.Rips.Count != hn.Length) throw new ToolFailure("m16_009_instruction_inventory");
        List<Invocation> calls = [];
        for (int i = 0; i < 9; i++) calls.Add(new(pn[i], p.Rips[i], false));
        if (variant != "BadRestorer")
        {
            for (int i = 0; i < hn.Length; i++) calls.Add(new(hn[i], h.Rips[i], true));
            calls.Add(new(15, r.Rips[0], true));
        }
        if (ExpectedExitCode(variant) == 42)
            for (int i = 9; i < pn.Length; i++) calls.Add(new(pn[i], p.Rips[i], false));
        return (elf, calls.ToArray());
    }

    private static void Fp(Writer w, int offset)
    { w.Rip(0x3dd9, 2, offset); w.Rip(0x1dae0f, 3, offset + 4); } // FNSTCW; STMXCSR.
    private static void Direction(Writer w, uint expected)
    { w.Op(0x589c, 2); w.Op(0x25, 1); w.Op(0x400, 4); w.Compare(expected); } // PUSHFQ; POP RAX; AND EAX,DF.
    private static ulong Address(int offset) => ImageBase + (uint)offset;
    private static void Validate(string variant)
    { if (!Variants.Contains(variant, StringComparer.Ordinal)) throw new ToolFailure("m16_009_fixture_variant"); }
}
