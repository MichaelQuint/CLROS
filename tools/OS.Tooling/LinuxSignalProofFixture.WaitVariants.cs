using System.Buffers.Binary;
using System.Text;
using Writer = OS.Tooling.LinuxThreadProofFixture.Writer;

namespace OS.Tooling;

public static partial class LinuxSignalProofFixture
{
    public static IReadOnlyList<string> WaitVariants { get; } = Array.AsReadOnly<string>(["SignalPipeEintr", "SignalPipeRestart", "SignalFutexEintr", "SignalFutexRestart", "SignalPipePartial"]);

    public const int WaitElfBytes = 0xa000, WaitChildOffset = 0x800;
    /// <summary>Canonical mechanical pipe race: wake, queue directed signal,
    /// consume readiness before the parent retries. Construction grants no execution.</summary>

    public const string StolenWaitVariant = "SignalPipeStolen";
    // HandlerStackOut occupies [0x11a0,0x11b8), including its ss_size word.
    public const int WaitPair = 0x11c0, WaitTid = 0x11b8, WaitClearTid = 0x11bc, WaitWord = 0x11d0;
    public const int WaitPayload = 0x7000, WaitRead = 0x9000;
    /// <summary>Exact canonical blocked-wait selector; a name alone grants no package authority.</summary>

    public static bool IsWaitVariant(string variant) => variant is StolenWaitVariant or EpollStolenWaitVariant || WaitVariants.Contains(variant, StringComparer.Ordinal);
    /// <summary>Actual flattened user call order: parked parent, sending child,
    /// resumed handler/restorer, optional real reissued syscall, then parent exit.
    /// Reissued calls repeat the original instruction RIP and receive a new ordinal.
    /// The inventory is fresh and immutable; it establishes no guest execution.</summary>

    public static IReadOnlyList<WaitInvocation> ExpectedWaitInvocations(string variant) => Array.AsReadOnly(BuildWait(variant).Steps);
    private static (byte[] Elf, Invocation[] Calls, WaitInvocation[] Steps) BuildWait(string variant)
    {
        if (!IsWaitVariant(variant))
            throw new ToolFailure("m16_009_wait_variant");
        if (variant == EpollStolenWaitVariant)
            return BuildEpollStolenWait();
        bool futex = variant.StartsWith("SignalFutex", StringComparison.Ordinal), partial = variant == "SignalPipePartial";
        bool stolen = variant == StolenWaitVariant;
        bool restart = variant.EndsWith("Restart", StringComparison.Ordinal), flag = restart || partial;
        byte[] elf = new byte[WaitElfBytes];
        LinuxThreadProofFixture.CreateMechanicalImage(Witness).CopyTo(elf, 0);
        // Three extra RW pages keep the 4100-byte write and 4096-byte read
        // disjoint from both user stacks, TLS, control words and the alternate stack.
        BinaryPrimitives.WriteUInt64LittleEndian(elf.AsSpan(152), 0x9000);
        BinaryPrimitives.WriteUInt64LittleEndian(elf.AsSpan(160), 0x9000);
        Encoding.ASCII.GetBytes(HandlerWitness).CopyTo(elf, HandlerWitnessOffset);
        void U64(int at, ulong value) => BinaryPrimitives.WriteUInt64LittleEndian(elf.AsSpan(at), value);
        U64(ActionOffset, Address(HandlerOffset));
        U64(ActionOffset + 8, 0x0c000004UL | (flag ? 0x10000000UL : 0));
        U64(ActionOffset + 16, Address(RestorerOffset));
        U64(StackOffset, AlternateBase);
        U64(StackOffset + 16, 4096);
        U64(0x2000, 0x13579bdf);
        U64(0x3000, 0x2468ace0);
        elf.AsSpan(WaitPayload, 4100).Fill(0x5a);
        Writer p = new(elf, EntryOffset), c = new(elf, WaitChildOffset), h = new(elf, HandlerOffset), r = new(elf, RestorerOffset);
        List<Invocation> parent = [], child = [], handler = [];
        void Call(Writer w, List<Invocation> list, uint nr, ulong a = 0, ulong b = 0, ulong n = 0, ulong d = 0, ulong e = 0)
        {
            w.Call(nr, a, b, n, d, e);
            list.Add(new(nr, w.Rips[^1], ReferenceEquals(w, h)));
        }

        void Fd(Writer w, List<Invocation> list, uint nr, bool read, ulong buffer, uint bytes)
        {
            w.FdCall(nr, WaitPair + (read ? 0 : 4), buffer, bytes);
            list.Add(new(nr, w.Rips[^1], ReferenceEquals(w, h)));
        }

        p.Immediate(4, (uint)ParentStackTop);
        p.Store32(WaitClearTid, 1);
        Call(p, parent, 39);
        p.Compare(1);
        p.StoreRax(PidOffset);
        Call(p, parent, 186);
        p.Compare(1);
        Call(p, parent, 158, 0x1002, ParentTls);
        p.Compare(0);
        Call(p, parent, 13, 10, Address(ActionOffset), 0, 8);
        p.Compare(0);
        Call(p, parent, 131, Address(StackOffset));
        p.Compare(0);
        if (!futex)
        {
            Call(p, parent, 293, Address(WaitPair));
            p.Compare(0);
        }

        Call(p, parent, 56, 0x7d0f00, 0x406000, Address(WaitTid), Address(WaitClearTid), 0x403000);
        p.Op(0xc08548, 3);
        p.Branch(0x84, WaitChildOffset);
        p.Compare(2);
        p.Memory(WaitTid, 2);
        p.Immediate(3, 0x13579bdf);
        if (futex)
            Call(p, parent, 202, Address(WaitWord), 128, 0, 0);
        else
            Fd(p, parent, partial ? 1U : 0U, !partial, Address(partial ? WaitPayload : WaitRead), partial ? 4100U : 1U);
        int blocked = parent.Count - 1;
        ulong waitRip = parent[blocked].ReturnRip;
        long expected = partial ? 4096 : restart ? futex ? -11 : 1 : -4;
        p.Compare(expected);
        p.Op(0xd889, 2);
        p.Compare(0x13579bdf);
        p.Memory(CountOffset, 1);
        p.Memory(WaitClearTid, 0);
        if (futex)
            p.Memory(WaitWord, 1);
        else if (partial)
        {
            Fd(p, parent, 0, true, Address(WaitRead), 4096);
            p.Compare(4096);
            p.Immediate(7, (uint)Address(WaitPayload));
            p.Immediate(6, (uint)Address(WaitRead));
            p.Immediate(1, 4096);
            p.Op(0xa6f3, 2);
            p.Branch(0x85); // REPE CMPSB checks the entire transferred prefix.
            Fd(p, parent, 1, false, Address(WaitPayload + 4096), 4);
            p.Compare(4);
            Fd(p, parent, 0, true, Address(WaitRead), 4);
            p.Compare(4);
            p.Memory(WaitRead, 0x5a5a5a5a);
        }
        else
        {
            if (stolen)
            {
                Fd(p, parent, 1, false, Address(WaitPayload), 1);
                p.Compare(1);
            }

            if (!restart)
            {
                Fd(p, parent, 0, true, Address(WaitRead), 1);
                p.Compare(1);
            }

            p.Memory(WaitRead, 0x5a);
        }

        Call(p, parent, 14, 2, 0, Address(MaskOut), 8);
        p.Compare(0);
        p.Memory(MaskOut, 0, true);
        Call(p, parent, 131, 0, Address(StackOut));
        p.Compare(0);
        p.Memory(StackOut + 8, 0);
        if (!futex)
        {
            Call(p, parent, 3, 3);
            p.Compare(0);
            Call(p, parent, 3, 4);
            p.Compare(0);
        }

        Call(p, parent, 1, 1, Address(WitnessOffset), 15);
        p.Compare(15);
        Call(p, parent, 231, 42);
        p.JumpFailure();
        if (p.Position > WaitChildOffset)
            throw new ToolFailure("m16_009_wait_parent_overlap");
        c.Fs();
        c.Compare(0x2468ace0);
        Call(c, child, 39);
        c.Compare(1);
        Call(c, child, 186);
        c.Compare(2);
        if (stolen)
        {
            Fd(c, child, 1, false, Address(WaitPayload), 1);
            c.Compare(1);
        }

        Call(c, child, 234, 1, 1, 10);
        c.Compare(0);
        if (futex)
        {
            c.Store32(WaitWord, 1);
            Call(c, child, 202, Address(WaitWord), 129, 1);
            c.Compare(0);
        }
        else if (stolen)
        {
            Fd(c, child, 0, true, Address(WaitRead), 1);
            c.Compare(1);
            c.Memory(WaitRead, 0x5a);
        }
        else if (!partial)
        {
            Fd(c, child, 1, false, Address(WaitPayload), 1);
            c.Compare(1);
        }

        Call(c, child, 60, 7);
        c.JumpFailure();
        if (c.Position > HandlerOffset)
            throw new ToolFailure("m16_009_wait_child_overlap");
        h.Op(0xf88948, 3);
        h.Compare(10);
        h.Op(0x068b, 2);
        h.Compare(10);
        h.Op(0x08466348, 4);
        h.Compare(-6);
        h.Op(0x10468b, 3);
        h.Compare(1);
        h.Op(0x028b48, 3);
        h.Compare(6); // Real Linux ucontext, including the precise restart decision.
        void Context(uint at, long value)
        {
            h.Op(0x828b48, 3);
            h.Op(at, 4);
            h.Compare(value);
        }

        Context(144, partial ? 4096 : restart ? futex ? 202 : 0 : -4);
        Context(168, (long)(restart ? waitRip - 2 : waitRip));
        Context(160, (long)ParentStackTop);
        Context(104, (long)(futex ? Address(WaitWord) : partial ? 4UL : 3UL));
        Context(112, (long)(futex ? 128UL : Address(partial ? WaitPayload : WaitRead)));
        Context(136, futex ? 0 : partial ? 4100 : 1);
        h.Op(0xe08948, 3);
        h.Compare(0x406c38);
        h.Memory(CountOffset, 0);
        h.Store32(CountOffset, 1);
        h.Fs();
        h.Compare(0x13579bdf);
        h.Immediate(3, 0x76543210); // Parent must receive its original RBX through the real sigreturn.
        Call(h, handler, 186);
        h.Compare(1);
        Call(h, handler, 14, 2, 0, Address(HandlerMaskOut), 8);
        h.Compare(0);
        h.Memory(HandlerMaskOut, 0x200, true);
        Call(h, handler, 131, 0, Address(HandlerStackOut));
        h.Compare(0);
        h.Memory(HandlerStackOut + 8, 1);
        Call(h, handler, 1, 1, Address(HandlerWitnessOffset), 15);
        h.Compare(15);
        h.Op(0xc3, 1);
        if (h.Position > RestorerOffset)
            throw new ToolFailure("m16_009_wait_handler_overlap");
        r.Immediate(0, 15);
        r.Finish();
        r.Op(0x0b0f, 2);
        Writer failure = new(elf, FailureOffset);
        failure.Call(231, 99, 0, 0);
        failure.Op(0x0b0f, 2);
        List<WaitInvocation> steps = [];
        void Add(IEnumerable<Invocation> list, bool isChild) => steps.AddRange(list.Select(i => new WaitInvocation(i.Number, i.ReturnRip, isChild, i.Handler)));
        Add(parent.Take(blocked + 1), false);
        Add(child, true);
        Add(handler, false);
        steps.Add(new(15, r.Rips[0], false, true));
        if (restart)
            steps.Add(new(parent[blocked].Number, waitRip, false, false));
        Add(parent.Skip(blocked + 1), false);
        return (elf, steps.Select(s => new Invocation(s.Number, s.ReturnRip, s.Handler)).ToArray(), steps.ToArray());
    }
}
