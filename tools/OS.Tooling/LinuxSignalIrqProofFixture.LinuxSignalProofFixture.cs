using System.Buffers.Binary;
using System.Text;
using Writer = OS.Tooling.LinuxThreadProofFixture.Writer;

namespace OS.Tooling;

public static partial class LinuxSignalProofFixture
{
    public const string IrqVariant = "Activation";
    public const int IrqChildOffset = 0x800, IrqReadyOffset = 0x11c0, IrqSentOffset = 0x11c4;
    public const int IrqTidOffset = 0x11b8, IrqClearTidOffset = 0x11bc;
    public const ulong IrqChildStack = 0x406000, IrqChildTls = 0x403000, IrqChildGs = 0x403100;
    /// <summary>Exact canonical activation selector; no package or execution
    /// identity is granted by a name alone.</summary>

    public static bool IsIrqVariant(string variant) => variant == IrqVariant;
    /// <summary>Fresh actual syscall continuations in each worker's own order.
    /// Cross-thread order depends on real IRQs and is validated separately.
    /// Parent includes its handler/restorer; child excludes the clone return.</summary>

    public static IReadOnlyList<Invocation> ExpectedIrqInvocations(bool child) => Array.AsReadOnly(child ? BuildIrq().Child : BuildIrq().Parent);
    /// <summary>Fresh complete instruction-boundary/envelope inventory for
    /// this immutable ELF, including the child's shared clone continuation.
    /// Matching a boundary grants no native continuation or capture authority.</summary>

    public static IReadOnlyList<IrqPoint> ExpectedIrqPoints() => Array.AsReadOnly(BuildIrq().Points);
    /// <summary>The parent's syscall-free JMP $ instruction. Only the actual
    /// signal handler's checked RIP edit can advance this mechanical workload.</summary>

    public static ulong ActivationLoopRip => BuildIrq().Loop;
    /// <summary>The first post-spin assertion reached by checked sigreturn;
    /// this is a package instruction address, never a kernel continuation.</summary>

    public static ulong ActivationResumeRip => ActivationLoopRip + 2;

    private static (byte[] Elf, Invocation[] Parent, Invocation[] Child, IrqPoint[] Points, ulong Loop) BuildIrq()
    {
        byte[] elf = LinuxThreadProofFixture.CreateMechanicalImage(Witness);
        Encoding.ASCII.GetBytes(HandlerWitness).CopyTo(elf, HandlerWitnessOffset);
        void U64(int at, ulong value) => BinaryPrimitives.WriteUInt64LittleEndian(elf.AsSpan(at), value);
        U64(ActionOffset, Address(HandlerOffset));
        U64(ActionOffset + 8, 0x0c000004);
        U64(ActionOffset + 16, Address(RestorerOffset));
        U64(StackOffset, AlternateBase);
        U64(StackOffset + 16, 4096);
        U64(0x2000, 0x13579bdf);
        U64(0x2100, 0x2468ace0);
        U64(0x3000, 0x31415926);
        U64(0x3100, 0x76543210);
        BinaryPrimitives.WriteUInt16LittleEndian(elf.AsSpan(ControlOffset), 0x027f);
        BinaryPrimitives.WriteUInt32LittleEndian(elf.AsSpan(MxcsrOffset), 0x5f80);
        List<IrqPoint> points = [];
        IrqWriter p = new(elf, EntryOffset, points);
        p.Mov(4, (uint)ParentStackTop);
        p.Rsp = ParentStackTop;
        p.Store32(RedZoneOffset, 0x13579bdf);
        p.Store32(IrqClearTidOffset, 1);
        p.Call(39);
        p.Compare(1);
        p.Store(PidOffset);
        p.Call(186);
        p.Compare(1);
        p.Call(158, 0x1002, ParentTls);
        p.Fs = ParentTls;
        p.Compare(0);
        p.Call(158, 0x1001, UserGs);
        p.Gs = UserGs;
        p.Compare(0);
        p.Call(13, 35, Address(ActionOffset), 0, 8);
        p.Compare(0);
        p.Call(131, Address(StackOffset));
        p.Compare(0);
        p.Call(56, 0x7d0f00, IrqChildStack, Address(IrqTidOffset), Address(IrqClearTidOffset), IrqChildTls);
        // The child's first two instructions are the genuine clone continuation.
        int cloneTest = p.Position;
        p.I(0xc08548, 3);
        p.Branch(0x84, IrqChildOffset);
        p.Compare(2);
        p.Memory(IrqTidOffset, 2);
        points.Add(new(Address(cloneTest), 0, true, false, IrqChildStack, IrqChildTls, UserGs, false, false));
        points.Add(new(Address(cloneTest + 3), 0, true, false, IrqChildStack, IrqChildTls, UserGs, false, false));
        p.Rip(0x2dd9, 2, ControlOffset);
        p.Rip(0x15ae0f, 3, MxcsrOffset);
        p.Mov(0, 0x11223344);
        p.I(0xc06e0f66, 4);
        p.Mov(0, 0x55667788);
        p.I(0xf86e0f4466, 5);
        for (uint register = 0; register < 16; register++)
            if (register != 4)
                p.Mov64(register, RegisterSeed(register));
        p.I(0xfd, 1);
        p.Direction = true;
        p.Store32(IrqReadyOffset, 1);
        ulong loop = Address(p.Position);
        p.Loop = true;
        p.I(0xfeeb, 2);
        p.Loop = false;
        // Only the real handler's checked sigreturn edits RIP past JMP $.
        p.Calls = 11;
        p.Compare(EditedRax);
        foreach (uint register in new uint[]
        {
            11,
            1,
            2,
            3,
            5,
            6,
            7,
            8,
            9,
            10,
            12,
            13,
            14,
            15
        }

        )
        {
            p.ToRax(register);
            p.Compare64(RegisterSeed(register));
        }

        p.I(0xc07e0f66, 4);
        p.Compare(0x11223344);
        p.I(0xf87e0f4466, 5);
        p.Compare(0x55667788);
        p.Fp(FpOut);
        p.Memory(FpOut, 0x027f);
        p.Memory(FpOut + 4, 0x5f80);
        p.CheckDirection(0x600);
        p.I(0xfc, 1);
        p.Direction = false;
        p.Segment(false);
        p.Compare(0x13579bdf);
        p.Segment(true);
        p.Compare(0x2468ace0);
        p.Memory(RedZoneOffset, 0x13579bdf);
        p.Memory(CountOffset, 1);
        p.Memory(IrqSentOffset, 1);
        int join = p.Position;
        p.CmpMemoryZero(IrqClearTidOffset);
        p.Branch(0x85, join);
        p.Call(1, 1, Address(WitnessOffset), (uint)Witness.Length);
        p.Compare(Witness.Length);
        p.Call(231, 42);
        p.JumpFailure();
        if (p.Position > IrqChildOffset)
            throw new ToolFailure("m16_009_irq_parent_overlap");
        IrqWriter c = new(elf, IrqChildOffset, points)
        {
            Child = true,
            Rsp = IrqChildStack,
            Fs = IrqChildTls,
            Gs = UserGs
        };
        c.Call(158, 0x1001, IrqChildGs);
        c.Gs = IrqChildGs;
        c.Compare(0);
        c.Segment(false);
        c.Compare(0x31415926);
        c.Segment(true);
        c.Compare(0x76543210);
        c.Call(39);
        c.Compare(1);
        c.Call(186);
        c.Compare(2);
        int wait = c.Position;
        c.CmpMemoryZero(IrqReadyOffset);
        c.Branch(0x84, wait);
        c.Store32(IrqSentOffset, 1);
        c.Call(234, 1, 1, 35);
        c.Compare(0);
        c.Call(60, 7);
        c.JumpFailure();
        if (c.Position > HandlerOffset)
            throw new ToolFailure("m16_009_irq_child_overlap");
        IrqWriter h = new(elf, HandlerOffset, points)
        {
            Handler = true,
            Calls = 7,
            Rsp = 0x406c38,
            Fs = ParentTls,
            Gs = UserGs
        };
        h.ToRax(7);
        h.Compare(35);
        h.I(0x068b, 2);
        h.Compare(35);
        h.I(0x08466348, 4);
        h.Compare(-6);
        h.I(0x10468b, 3);
        h.Compare(1);
        h.Context(0);
        h.Compare(6);
        uint[] order = [8, 9, 10, 11, 12, 13, 14, 15, 7, 6, 5, 3, 2, 0, 1];
        for (uint i = 0; i < order.Length; i++)
        {
            h.Context(40 + i * 8);
            h.Compare64(RegisterSeed(order[i]));
        }

        h.Context(160);
        h.Compare((long)ParentStackTop);
        h.Context(168);
        h.Compare((long)loop);
        h.Context(176);
        h.I(0x25, 1, 0x600, 4);
        h.Compare(0x600);
        h.Context(192);
        h.Compare(0);
        h.Context(200);
        h.Compare(0);
        h.Edit(144, EditedRax);
        h.Edit(168, (uint)(loop + 2));
        h.I(0x928b4c, 3, 224, 4); // R10=fpstate from ucontext.
        h.I(0x02b70f41, 4);
        h.Compare(0x027f);
        h.I(0x18428b41, 4);
        h.Compare(0x5f80);
        h.I(0x828b41, 3, 160, 4);
        h.Compare(0x11223344);
        h.I(0x828b41, 3, 400, 4);
        h.Compare(0x55667788);
        h.CheckDirection(0x200);
        h.Fp(HandlerFpOut);
        h.Memory(HandlerFpOut, 0x37f);
        h.Memory(HandlerFpOut + 4, 0x1f80);
        h.I(0xc07e0f66, 4);
        h.Compare(0);
        h.I(0xf87e0f4466, 5);
        h.Compare(0);
        h.Memory(CountOffset, 0);
        h.Store32(CountOffset, 1);
        h.Mov(3, 0x76543210);
        h.I(0xc0760f66, 4);
        h.I(0xff760f4566, 5);
        h.I(0xe3db, 2);
        h.Call(186);
        h.Compare(1);
        h.Call(14, 2, 0, Address(HandlerMaskOut), 8);
        h.Compare(0);
        h.Load(HandlerMaskOut, true);
        h.Compare64(1UL << 34);
        h.Call(1, 1, Address(HandlerWitnessOffset), (uint)HandlerWitness.Length);
        h.Compare(HandlerWitness.Length);
        h.I(0xc3, 1);
        if (h.Position > RestorerOffset)
            throw new ToolFailure("m16_009_irq_handler_overlap");
        IrqWriter r = new(elf, RestorerOffset, points)
        {
            Handler = true,
            Calls = 10,
            Rsp = 0x406c40,
            Fs = ParentTls,
            Gs = UserGs
        };
        r.Mov(0, 15);
        r.Syscall(15);
        r.I(0x0b0f, 2);
        Writer failure = new(elf, FailureOffset);
        failure.Op(0xfc, 1);
        failure.Call(231, 99, 0, 0);
        failure.Op(0x0b0f, 2);
        Invocation[] parent = [.. p.Invocations.Take(7), .. h.Invocations, .. r.Invocations, .. p.Invocations.Skip(7)];
        if (parent.Length != 13 || c.Invocations.Count != 5 || points.DistinctBy(x => (x.Child, x.Rip)).Count() != points.Count)
            throw new ToolFailure("m16_009_irq_inventory");
        return (elf, parent, c.Invocations.ToArray(), points.ToArray(), loop);
    }
}
