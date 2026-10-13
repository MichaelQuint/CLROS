using System.Buffers.Binary;
using System.Runtime.InteropServices;
using OS.Kernel.Threading;
using Irq = OS.Kernel.Threading.X64TimerInterrupt;

internal static unsafe class X64TimerInterruptTests
{
    private static Irq.Plan Plan => new()
    { CodeAddress = 0x100000, Callback = 0x200000, CallbackContext = 0x300000, CallbackBytes = 128, HelperFx = 0x400000 };
    private static byte[] Snapshot(byte* pointer, int bytes) => new ReadOnlySpan<byte>(pointer, bytes).ToArray();

    [HostTest("m09.005 IRQ frame retains fifteen GPRs aligned FX and five actual hardware words", HostLane.Fast, 0)]
    private static void Layout()
    {
        HostCheck.Equal(704, sizeof(Irq.Frame));
        string[] names = ["FxSave", "FsBase", "GsBase", "EntryFlags", "Complete", "R15", "R14", "R13", "R12", "R11", "R10",
            "R9", "R8", "Rdi", "Rsi", "Rbp", "Rbx", "Rdx", "Rcx", "Rax", "Rip", "Cs", "Rflags", "Rsp", "Ss"];
        int[] offsets = [0,512,520,528,536,544,552,560,568,576,584,592,600,608,616,624,632,640,648,656,664,672,680,688,696];
        for (int i = 0; i < names.Length; i++) HostCheck.Equal(offsets[i], Marshal.OffsetOf<Irq.Frame>(names[i]).ToInt32());
        // Hardware entry RSP is8mod16 after five words;15 pushes and544-byte
        // prefix yield0mod16, including the callback's32-byte shadow allocation.
        ulong top = 0x100000, entry = top - 40, captured = entry - 15 * 8 - 544;
        HostCheck.True(entry % 16 == 8 && captured % 16 == 0 && (captured - 32) % 16 == 0);
        HostCheck.Equal(top - 40, captured + 664);
        Irq.Frame frame = default;
        frame.Rsp = 0x123458; frame.Rip = 0x56789a; frame.Cs = 8; frame.Ss = 16; frame.Rflags = 0x602;
        HostCheck.Equal(0x123458UL, ((ulong*)((byte*)&frame + 664))[3]);
    }

    [HostTest("m09.005 IRQ independent capture ABI return and failure byte vectors", HostLane.Fast, 1)]
    private static void Vectors()
    {
        byte* code = stackalloc byte[514]; new Span<byte>(code, 514).Fill(0xa5);
        HostCheck.True(Irq.Emit(code + 1, 512, Plan) && Irq.Verify(code + 1, 512, Plan));
        ReadOnlySpan<byte> bytes = new(code + 1, 512);
        byte[] prefix = Convert.FromHexString(
            "F30F1EFA5051525355565741504151415241534154415541564157" +
            "4881EC20020000480FAE0424" +
            "B9000100C00F3248C1E2204809D04889842400020000" +
            "B9010100C00F3248C1E2204809D04889842408020000" +
            "9C58488984241002000048C784241802000001000000FC" +
            "48B80000400000000000480FAE08" +
            "48B900003000000000004889E24883EC20" +
            "48B80000200000000000FFD0FA4883C4203C01");
        HostCheck.True(bytes[..prefix.Length].SequenceEqual(prefix) && prefix.Length == 156);
        // Every failed callback/completion/selector/MSR readback reaches the same
        // IF-clear retaining loop. Displacements are checked by independent decode.
        int[] branches = [156,171,186,201,244,287];
        foreach (int position in branches)
        {
            HostCheck.True(bytes.Slice(position, 2).SequenceEqual(new byte[] { 0x0f, 0x85 }));
            HostCheck.Equal(330, position + 6 + BinaryPrimitives.ReadInt32LittleEndian(bytes.Slice(position + 2, 4)));
        }
        HostCheck.True(bytes.Slice(162, 9).SequenceEqual(Convert.FromHexString("4883BC241802000001")));
        HostCheck.True(bytes.Slice(177, 9).SequenceEqual(Convert.FromHexString("4883BC24A002000008")));
        HostCheck.True(bytes.Slice(192, 9).SequenceEqual(Convert.FromHexString("4883BC24B802000010")));
        byte[] restoreFs = Convert.FromHexString(
            "4C8B8424000200004C89C04889C248C1EA20B9000100C00F300F3248C1E2204809D04C39C0");
        byte[] restoreGs = Convert.FromHexString(
            "4C8B8424080200004C89C04889C248C1EA20B9010100C00F300F3248C1E2204809D04C39C0");
        HostCheck.True(bytes.Slice(207, 37).SequenceEqual(restoreFs) && bytes.Slice(250, 37).SequenceEqual(restoreGs));
        HostCheck.True(bytes.Slice(293, 42).SequenceEqual(Convert.FromHexString(
            "480FAE0C244881C420020000415F415E415D415C415B415A415941585F5E5D5B5A595848CFFAF390EBFC")));
        HostCheck.True(bytes[335..].IndexOfAnyExcept((byte)0) == -1 && code[0] == 0xa5 && code[513] == 0xa5);
        // Actual IRETQ, with no RET/JMP substitution or add40 to the saved old RSP.
        HostCheck.True(bytes.Slice(328, 2).SequenceEqual(new byte[] { 0x48, 0xcf }));
    }

    [HostTest("m09.005 IRQ malformed and aliased plans reject before output mutation", HostLane.Fast, 2)]
    private static void Rejection()
    {
        byte* code = stackalloc byte[512]; new Span<byte>(code, 512).Fill(0xa5);
        byte[] original = Snapshot(code, 512);
        HostCheck.True(!Irq.Emit(null, 512, Plan) && !Irq.Emit(code, 511, Plan));
        HostCheck.True(!Irq.Emit((byte*)(ulong.MaxValue - 15), 512, Plan));
        for (int field = 0; field < 4; field++)
        foreach (ulong value in new ulong[] { 0, 0x800000000000, ulong.MaxValue })
        {
            if (field == 1 && value == ulong.MaxValue) continue; // Canonical one-byte pointer; live callback extent is externally admitted.
            Irq.Plan bad = Plan; ((ulong*)&bad)[field] = value;
            HostCheck.True(!Irq.Emit(code, 512, bad) && !Irq.Verify(code, 512, bad));
        }
        foreach (int field in new[] { 0, 2, 3 })
        {
            Irq.Plan bad = Plan; ((ulong*)&bad)[field]++;
            HostCheck.True(!Irq.Emit(code, 512, bad));
        }
        foreach (uint bytes in new uint[] { 0, 1, 7 })
        { Irq.Plan bad = Plan; bad.CallbackBytes = bytes; HostCheck.True(!Irq.Emit(code, 512, bad)); }
        foreach (var pair in new (int Field, ulong Address)[]
        {
            (1,Plan.CodeAddress),(1,Plan.CodeAddress+511),(1,Plan.CallbackContext),(1,Plan.HelperFx),
            (2,Plan.CodeAddress),(2,Plan.HelperFx),(3,Plan.CodeAddress),(3,Plan.CallbackContext),
            (1,(ulong)code),(2,(ulong)code),(3,(ulong)code)
        })
        {
            Irq.Plan bad = Plan; ((ulong*)&bad)[pair.Field] = pair.Address;
            HostCheck.True(!Irq.Emit(code, 512, bad));
        }
        HostCheck.True(original.SequenceEqual(Snapshot(code, 512)));
        Irq.Plan high = new() { CodeAddress = 0xffff800000100000, Callback = 0xffff800000200000,
            CallbackContext = 0xffff800000300000, CallbackBytes = 128, HelperFx = 0xffff800000400000 };
        HostCheck.True(Irq.Emit(code, 512, high) && Irq.Verify(code, 512, high));
    }

    [HostTest("m09.005 IRQ comparison detects every changed instruction immediate branch and padding byte", HostLane.Fast, 3)]
    private static void Comparison()
    {
        byte* code = stackalloc byte[512]; HostCheck.True(Irq.Emit(code, 512, Plan));
        for (uint i = 0; i < 512; i++)
        {
            code[i] ^= 1;
            HostCheck.True(!Irq.Verify(code, 512, Plan));
            code[i] ^= 1;
        }
        HostCheck.True(Irq.Verify(code, 512, Plan));
        Irq.Plan changed = Plan; changed.Callback += 16; HostCheck.True(!Irq.Verify(code, 512, changed));
        changed = Plan; changed.CallbackContext += 16; HostCheck.True(!Irq.Verify(code, 512, changed));
        changed = Plan; changed.HelperFx += 16; HostCheck.True(!Irq.Verify(code, 512, changed));
    }

    [HostTest("m09.005 IRQ warmed emission and comparison allocate no managed storage", HostLane.Fast, 4)]
    private static void NoAllocation()
    {
        byte* code = stackalloc byte[512]; bool good = true;
        for (int pass = 0; pass < 2; pass++)
        {
            long before = GC.GetAllocatedBytesForCurrentThread();
            for (int i = 0; i < 128; i++) good &= Irq.Emit(code, 512, Plan) && Irq.Verify(code, 512, Plan);
            if (pass == 1) HostCheck.Equal(0L, GC.GetAllocatedBytesForCurrentThread() - before);
        }
        HostCheck.True(good);
    }
}
