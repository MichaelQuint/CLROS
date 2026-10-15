using System.Buffers.Binary;
using System.Text;
using Writer = OS.Tooling.LinuxThreadProofFixture.Writer;

namespace OS.Tooling;

public static partial class LinuxSignalProofFixture
{
    public static IReadOnlyList<string> NestedVariants { get; } = Array.AsReadOnly<string>(["NestedAlternateStack", "AlternateStackExhaustion"]);
    public const int NestedHandlerOffset = 0x600, NestedFinishOffset = 0xc00;
    // Unused control-page bytes: depth/count at1200h/1204h, then independent
    // three-qword tables for entry/installed masks and saved frame assertions.
    public const int NestedDepthOffset = 0x1200, NestedCountOffset = 0x1204, NestedMaskInputsOffset = 0x1260;
    public const uint NestedInnerRax = 0x23456789;
    private const int NestedEntryMasks = 0x1240, NestedFrames = 0x1280, NestedSavedRsps = 0x12a0,
        NestedSavedRips = 0x12c0, NestedReturnValues = 0x12e0, NestedSavedMasks = 0x1300,
        NestedSavedR12 = 0x1320, NestedSavedR13 = 0x1340, NestedSavedR14 = 0x1360, NestedStackFlags = 0x1380;

    /// <summary>One actual syscall in nested execution order. Depth0 belongs to
    /// the parent. A restorer executes at that depth's handler RSP+8 after RET;
    /// all other handler calls retain its entry RSP. Inventories describe bytes,
    /// not observed delivery, restoration or native execution.</summary>
    public readonly record struct NestedInvocation(uint Number, ulong ReturnRip, uint Depth, bool Restorer);

    public static bool IsNestedVariant(string variant) => variant is "NestedAlternateStack" or "AlternateStackExhaustion";
    public static IReadOnlyList<NestedInvocation> ExpectedNestedInvocations(string variant) =>
        Array.AsReadOnly(BuildNested(variant).Metadata);

    /// <summary>Fixed legacy FX/frame geometry on the four-KiB alternate stack.
    /// Deeper delivery preserves128 bytes below the interrupted handler RSP,
    /// aligns the512-byte FX image to64, then the440-byte header to16 minus8.
    /// Depth4 returns the rejected candidate below the alternate-stack base.</summary>
    public static ulong NestedHandlerRsp(uint depth)
    {
        if (depth is < 1 or > 4) throw new ToolFailure("m16_009_nested_depth");
        ulong frame = 0;
        for (uint current = 1; current <= depth; current++)
        {
            ulong top = current == 1 ? AlternateBase + 4096 : frame - 128;
            ulong fp = (top - 512) & ~63UL;
            frame = ((fp - 440) & ~15UL) - 8;
        }
        return frame;
    }

    private static (byte[] Elf, Invocation[] Calls, NestedInvocation[] Metadata) BuildNested(string variant)
    {
        if (!IsNestedVariant(variant)) throw new ToolFailure("m16_009_fixture_variant");
        bool exhaustion = variant == "AlternateStackExhaustion";
        byte[] elf = LinuxThreadProofFixture.CreateMechanicalImage(Witness);
        Encoding.ASCII.GetBytes(HandlerWitness).CopyTo(elf, HandlerWitnessOffset);
        void U64(int at, ulong value) => BinaryPrimitives.WriteUInt64LittleEndian(elf.AsSpan(at), value);
        void Table(int at, params ulong[] values) { for (int i = 0; i < values.Length; i++) U64(at + i * 8, values[i]); }
        ulong first = NestedHandlerRsp(1), second = NestedHandlerRsp(2), third = NestedHandlerRsp(3);
        U64(ActionOffset, Address(NestedHandlerOffset)); U64(ActionOffset + 8, 0x4c000004); // ONSTACK|NODEFER|RESTORER|SIGINFO.
        U64(ActionOffset + 16, Address(RestorerOffset)); U64(ActionOffset + 24, 0x1000); // Block SIG13, never the recursive SIG10.
        U64(StackOffset, AlternateBase); U64(StackOffset + 16, 4096);
        U64(BlockedOffset, 0x800); U64(0x2000, 0x13579bdf);
        Table(NestedEntryMasks, 0x1800, 0x3800, 0x7800);
        Table(NestedMaskInputsOffset, 0x3800, 0x7800, 0xf800);
        Table(NestedFrames, first, second, third);
        Table(NestedSavedRsps, ParentStackTop, first, second);
        Table(NestedReturnValues, EditedRax, NestedInnerRax, 0x3456789a);
        Table(NestedSavedMasks, 0x800, 0x3800, 0x7800);
        Table(NestedSavedR12, 0x13579bdf, 1, 2);
        Table(NestedSavedR13, 0x2468ace0, first + 8, second + 8);
        Table(NestedSavedR14, 0x31415926, first, second);
        Table(NestedStackFlags, 0, 1, 1);

        Writer p = new(elf, EntryOffset);
        p.Immediate(4, (uint)ParentStackTop); p.Store32(RedZoneOffset, 0x13579bdf);
        p.Store32(NestedDepthOffset, 0); p.Store32(NestedCountOffset, 0);
        p.Call(39, 0, 0, 0); p.Positive(); p.StoreRax(PidOffset);
        p.Call(186, 0, 0, 0); p.CompareMemory(PidOffset);
        p.Call(158, 0x1002, ParentTls, 0); p.Compare(0);
        p.Call(13, 10, Address(ActionOffset), 0, 8); p.Compare(0);
        p.Call(131, Address(StackOffset), 0, 0); p.Compare(0);
        p.Call(14, 2, Address(BlockedOffset), 0, 8); p.Compare(0);
        p.Immediate(12, 0x13579bdf); p.Immediate(13, 0x2468ace0); p.Immediate(14, 0x31415926);
        NestedSend(p);
        if (exhaustion) p.JumpFailure(); // No original user continuation may run after the fourth attempted delivery.
        else
        {
            p.Compare(EditedRax);
            foreach (var saved in new (uint Register, uint Value)[] { (12, 0x13579bdf), (13, 0x2468ace0), (14, 0x31415926), (4, (uint)ParentStackTop) })
            { ToRax(p, saved.Register); p.Compare(saved.Value); }
            p.Memory(NestedDepthOffset, 0); p.Memory(NestedCountOffset, 2); p.Memory(RedZoneOffset, 0x13579bdf);
            p.Call(14, 2, 0, Address(MaskOut), 8); p.Compare(0); p.Memory(MaskOut, 0x800, true);
            p.Call(131, 0, Address(StackOut), 0); p.Compare(0); NestedStack(p, StackOut, 0);
            p.Call(1, 1, Address(WitnessOffset), (uint)Witness.Length); p.Compare(Witness.Length);
            p.Call(231, 42, 0, 0); p.JumpFailure();
        }
        if (p.Position > NestedHandlerOffset) throw new ToolFailure("m16_009_nested_parent_overlap");

        Writer h = new(elf, NestedHandlerOffset);
        h.Op(0xf88948, 3); h.Compare(10);
        h.Op(0x068b, 2); h.Compare(10); h.Op(0x08466348, 4); h.Compare(-6);
        h.Op(0x10468b, 3); h.CompareMemory(PidOffset);
        h.Rip(0x058b, 2, NestedDepthOffset); h.Op(0xc0ff, 2); h.Positive();
        h.Op(0x3d48, 2); h.Op(exhaustion ? 3UL : 2UL, 4); h.Branch(0x87);
        h.StoreRax(NestedDepthOffset); h.Op(0xc48941, 3); // R12D=depth, restored independently by an inner sigreturn.
        h.Rip(0x05ff, 2, NestedCountOffset); h.Rip(0x058b, 2, NestedCountOffset);
        h.Op(0xe03944, 3); h.Branch(0x85); // Count equals depth until the deepest entered handler returns.
        h.Op(0xd58949, 3); h.Op(0xe68949, 3); // R13=ucontext, R14=actual handler RSP.
        NestedExpected(h, NestedFrames); ToRax(h, 4); NestedCompare(h);
        h.Op(0x08468d49, 4); h.Op(0xd03948, 3); h.Branch(0x85); // ucontext == RSP+8.
        h.Op(0x868d49, 3); h.Op(312, 4); h.Op(0xf03948, 3); h.Branch(0x85); // siginfo == RSP+312.
        h.Op(0x068b49, 3); h.Compare((long)Address(RestorerOffset));
        NestedContext(h, 0); h.Compare(6); NestedContext(h, 16); h.Compare((long)AlternateBase);
        NestedContext(h, 32); h.Compare(4096);
        foreach (var field in new (uint Offset, int Table)[]
        {
            (24, NestedStackFlags), (72, NestedSavedR12), (80, NestedSavedR13), (88, NestedSavedR14),
            (160, NestedSavedRsps), (168, NestedSavedRips), (296, NestedSavedMasks)
        }) { NestedExpected(h, field.Table); NestedContext(h, field.Offset); NestedCompare(h); }
        NestedContext(h, 144); h.Compare(0);
        NestedExpected(h, NestedReturnValues); h.Op(0x9d894d, 3); h.Op(144, 4); // Checked inner/outer RAX edits.
        h.Fs(); h.Compare(0x13579bdf);
        h.Call(186, 0, 0, 0); h.CompareMemory(PidOffset);
        h.Call(14, 2, 0, Address(HandlerMaskOut), 8); h.Compare(0);
        NestedExpected(h, NestedEntryMasks); h.Rip(0x058b48, 3, HandlerMaskOut); NestedCompare(h);
        h.Call(131, 0, Address(HandlerStackOut), 0); h.Compare(0); NestedStack(h, HandlerStackOut, 1);
        h.Arguments(14, 2, 0, 0, 8, 0, 0);
        h.Rip(0x358d48, 3, NestedMaskInputsOffset); h.Op(0xf8e6748d4a, 5); // RSI=&nextMasks[depth-1].
        h.Finish(); h.Compare(0);
        if (!exhaustion) { h.Op(0x02fc8341, 4); h.Branch(0x84, NestedFinishOffset); }
        NestedSend(h);
        if (exhaustion) h.JumpFailure();
        else
        {
            h.Compare(NestedInnerRax);
            foreach (var restored in new (uint Register, ulong Value)[] { (12, 1), (13, first + 8), (14, first), (4, first) })
            { ToRax(h, restored.Register); h.Compare((long)restored.Value); }
            h.Memory(NestedDepthOffset, 1); h.Memory(NestedCountOffset, 2);
            NestedJump(h, NestedFinishOffset);
        }
        if (h.Position > NestedFinishOffset) throw new ToolFailure("m16_009_nested_handler_overlap");
        Table(NestedSavedRips, p.Rips[6], h.Rips[4], h.Rips[4]);

        Writer finish = new(elf, NestedFinishOffset);
        if (!exhaustion)
        {
            finish.Call(14, 2, 0, Address(HandlerMaskOut), 8); finish.Compare(0);
            NestedExpected(finish, NestedMaskInputsOffset); finish.Rip(0x058b48, 3, HandlerMaskOut); NestedCompare(finish);
            finish.Call(131, 0, Address(HandlerStackOut), 0); finish.Compare(0); NestedStack(finish, HandlerStackOut, 1);
            finish.Call(1, 1, Address(HandlerWitnessOffset), (uint)HandlerWitness.Length); finish.Compare(HandlerWitness.Length);
            finish.Rip(0x0dff, 2, NestedDepthOffset); finish.Op(0xc3, 1);
        }
        if (finish.Position > RestorerOffset) throw new ToolFailure("m16_009_nested_finish_overlap");
        Writer r = new(elf, RestorerOffset); r.Immediate(0, 15); r.Finish(); r.Op(0x0b0f, 2);
        Writer failure = new(elf, FailureOffset); failure.Op(0xfc, 1); failure.Call(231, 99, 0, 0); failure.Op(0x0b0f, 2);

        List<Invocation> calls = []; List<NestedInvocation> metadata = [];
        void Add(uint number, ulong rip, uint depth, bool restorer = false)
        { calls.Add(new(number, rip, depth != 0)); metadata.Add(new(number, rip, depth, restorer)); }
        uint[] parent = [39, 186, 158, 13, 131, 14, 234, 14, 131, 1, 231];
        uint[] entry = [186, 14, 131, 14], ending = [14, 131, 1];
        for (int i = 0; i < 7; i++) Add(parent[i], p.Rips[i], 0);
        for (uint depth = 1; depth <= (exhaustion ? 3U : 2U); depth++)
        {
            for (int i = 0; i < entry.Length; i++) Add(entry[i], h.Rips[i], depth);
            if (exhaustion || depth == 1) Add(234, h.Rips[4], depth);
        }
        if (!exhaustion)
        {
            for (uint depth = 2; depth > 0; depth--)
            {
                for (int i = 0; i < ending.Length; i++) Add(ending[i], finish.Rips[i], depth);
                Add(15, r.Rips[0], depth, true);
            }
            for (int i = 7; i < parent.Length; i++) Add(parent[i], p.Rips[i], 0);
        }
        if (p.Rips.Count != (exhaustion ? 7 : 11) || h.Rips.Count != 5 || finish.Rips.Count != (exhaustion ? 0 : 3))
            throw new ToolFailure("m16_009_nested_instruction_inventory");
        return (elf, calls.ToArray(), metadata.ToArray());
    }

    private static void NestedSend(Writer w)
    {
        w.Arguments(234, 0, 0, 10, 0, 0, 0);
        w.Rip(0x3d8b, 2, PidOffset); w.Rip(0x358b, 2, PidOffset); w.Finish();
    }
    private static void NestedExpected(Writer w, int table)
    { w.Rip(0x158d4c, 3, table); w.Op(0xf8e25c8b4f, 5); } // R11=[R10+R12*8-8].
    private static void NestedCompare(Writer w) { w.Op(0xd8394c, 3); w.Branch(0x85); }
    private static void NestedContext(Writer w, uint offset) { w.Op(0x858b49, 3); w.Op(offset, 4); }
    private static void NestedStack(Writer w, int output, uint flags)
    { w.Memory(output, AlternateBase, true); w.Memory(output + 8, flags); w.Memory(output + 16, 4096, true); }
    private static void NestedJump(Writer w, int target)
    { w.Op(0xe9, 1); w.Op(unchecked((uint)(target - (w.Position + 4))), 4); }
}
