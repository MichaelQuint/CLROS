using System.Runtime.InteropServices;
using OS.Kernel.Compatibility.LinuxX64;
using OS.Tooling;

internal static unsafe class X64LinuxUserTransitionTests
{
    private static X64LinuxUserTransitionPlan Plan() => new()
    {
        CodeAddress = 0x100000, ContextAddress = 0x200000, UserRoot = 0x300000,
        UserRip = 0x400100, UserRsp = 0x70001000, Dispatcher = 0x800100, FaultReporter = 0x800200,
        CallbackContext = 0x800000, TssRsp0Address = 0x900004, OriginalGp = 0x801000, OriginalPf = 0x802000
    };
    private static void Descriptors(byte* gdt, byte* idt)
    {
        new Span<byte>(gdt, 4096).Clear(); new Span<byte>(idt, 4096).Clear();
        *(ulong*)(gdt + 8) = 0x00af9b000000ffff; // Original processor-set accessed bits.
        *(ulong*)(gdt + 16) = 0x00cf93000000ffff;
        *(ulong*)(gdt + 24) = 0x00008b9000000067; // Busy original TSS at 0x900000.
        for (int vector = 0; vector < 256; vector++)
        {
            uint handler = vector == 13 ? 0x801000U : vector == 14 ? 0x802000U : 0x803000U;
            byte* gate = idt + vector * 16;
            *(ushort*)gate = (ushort)handler; *(ushort*)(gate + 2) = 8;
            gate[4] = vector == 2 ? (byte)3 : vector == 8 ? (byte)1 : vector == 14 ? (byte)2 : (byte)0;
            gate[5] = 0x8e; *(ushort*)(gate + 6) = (ushort)(handler >> 16);
        }
    }
    private static byte* Allocate(uint bytes) => (byte*)NativeMemory.AlignedAlloc(bytes, 4096);

    [HostTest("m16-006 high transition RIP RSP FS GS preserve qwords while kernel pointers stay low", HostLane.Fast, 4)]
    private static void HighAddresses()
    {
        byte* data = Allocate(X64LinuxUserTransition.DataBytes), gdt = Allocate(4096), idt = Allocate(4096), code = Allocate(4096);
        try
        {
            Descriptors(gdt, idt); var plan = Plan();
            plan.UserRip += 0x100000000; plan.UserRsp += 0x100000000; plan.UserFs = plan.UserGs = 0x170000000;
            HostCheck.True(X64LinuxUserTransition.Prepare(data, X64LinuxUserTransition.DataBytes, plan, gdt, 4096, idt, 4096));
            HostCheck.Equal(plan.UserRsp, *(ulong*)(data + 8)); HostCheck.Equal(plan.UserRip, *(ulong*)(data + 0x18));
            HostCheck.Equal(plan.UserFs, *(ulong*)(data + 0x58)); HostCheck.Equal(plan.UserGs, *(ulong*)(data + 0x60));
            HostCheck.True(X64LinuxUserTransition.Emit(code, 4096, plan) && X64LinuxUserTransition.ValidateReadBack(code, 4096, plan));
            plan.UserRoot = 0x100000000;
            HostCheck.True(!X64LinuxUserTransition.Emit(code, 4096, plan));
            plan = Plan(); plan.Dispatcher += 0x100000000;
            HostCheck.True(!X64LinuxUserTransition.Emit(code, 4096, plan));
        }
        finally { NativeMemory.AlignedFree(data); NativeMemory.AlignedFree(gdt); NativeMemory.AlignedFree(idt); NativeMemory.AlignedFree(code); }
    }

    [HostTest("m16-005 user transition preserves original descriptor inputs and prepares private selectors FP and guard", HostLane.Fast, 0)]
    private static void Layout()
    {
        byte* data = Allocate(X64LinuxUserTransition.DataBytes), gdt = Allocate(4096), idt = Allocate(4096);
        try
        {
            Descriptors(gdt, idt); byte[] originalGdt = new ReadOnlySpan<byte>(gdt, 4096).ToArray();
            byte[] originalIdt = new ReadOnlySpan<byte>(idt, 4096).ToArray();
            var plan = Plan(); new Span<byte>(data, (int)X64LinuxUserTransition.DataBytes).Fill(0xa5);
            long before = GC.GetAllocatedBytesForCurrentThread();
            bool prepared = X64LinuxUserTransition.Prepare(data, X64LinuxUserTransition.DataBytes, plan, gdt, 4096, idt, 4096);
            long allocated = GC.GetAllocatedBytesForCurrentThread() - before;
            HostCheck.True(prepared && allocated == 0);
            HostCheck.True(originalGdt.AsSpan().SequenceEqual(new ReadOnlySpan<byte>(gdt, 4096)));
            HostCheck.True(originalIdt.AsSpan().SequenceEqual(new ReadOnlySpan<byte>(idt, 4096)));
            HostCheck.Equal(0x214000UL, *(ulong*)data);
            HostCheck.Equal(0x70001000UL, *(ulong*)(data + 8));
            HostCheck.Equal(0x400100UL, *(ulong*)(data + 0x18));
            HostCheck.Equal((ushort)63, *(ushort*)(data + 0x98));
            HostCheck.Equal(0x200600UL, *(ulong*)(data + 0x9a));
            HostCheck.Equal((ushort)4095, *(ushort*)(data + 0xb8));
            HostCheck.Equal(0x201000UL, *(ulong*)(data + 0xba));
            HostCheck.True(new ReadOnlySpan<byte>(data + 0x600, 40).SequenceEqual(new ReadOnlySpan<byte>(gdt, 40)));
            HostCheck.Equal(0x00cff2000000ffffUL, *(ulong*)(data + 0x630));
            HostCheck.Equal(0x00affa000000ffffUL, *(ulong*)(data + 0x638));
            HostCheck.Equal(0UL, *(ulong*)(data + 0x628)); // reserved STAR base slot
            for (int vector = 0; vector < 256; vector++)
            {
                byte* gate = data + 0x1000 + vector * 16;
                if (vector != 13 && vector != 14)
                    HostCheck.True(new ReadOnlySpan<byte>(gate, 16).SequenceEqual(new ReadOnlySpan<byte>(idt + vector * 16, 16)));
                else
                {
                    HostCheck.Equal((byte)0, gate[4]); // Faults from CPL3 use original TSS.RSP0.
                    HostCheck.Equal((ushort)8, *(ushort*)(gate + 2));
                    uint address = *(ushort*)gate | ((uint)*(ushort*)(gate + 6) << 16);
                    HostCheck.Equal(vector == 13 ? 0x100c00U : 0x100d00U, address);
                }
            }
            HostCheck.Equal((ushort)0x37f, *(ushort*)(data + 0x400));
            HostCheck.Equal(0x1f80U, *(uint*)(data + 0x418));
            HostCheck.True(new ReadOnlySpan<byte>(data + 0x420, 480).IndexOfAnyExcept((byte)0) == -1);
            HostCheck.True(new ReadOnlySpan<byte>(data + 0x2000, 4096).IndexOfAnyExcept((byte)0) == -1);
            HostCheck.True(new ReadOnlySpan<byte>(data + 0x3000, 4096).IndexOfAnyExcept((byte)0xa5) == -1);
            HostCheck.True(new ReadOnlySpan<byte>(data + 0x4000, (int)X64LinuxUserTransition.DataBytes - 0x4000).IndexOfAnyExcept((byte)0) == -1);
            HostCheck.Equal(96, sizeof(LinuxSyscallFrame));
        }
        finally { NativeMemory.AlignedFree(data); NativeMemory.AlignedFree(gdt); NativeMemory.AlignedFree(idt); }
    }

    [HostTest("m16-005 transition admission rejects malformed descriptors and aliases before mutation", HostLane.Fast, 1)]
    private static void Rejections()
    {
        byte* data = Allocate(X64LinuxUserTransition.DataBytes), gdt = Allocate(4096), idt = Allocate(4096);
        try
        {
            for (int variant = 0; variant < 12; variant++)
            {
                Descriptors(gdt, idt); new Span<byte>(data, (int)X64LinuxUserTransition.DataBytes).Fill(0xa5);
                var plan = Plan(); byte* sourceGdt = gdt; uint capacity = X64LinuxUserTransition.DataBytes;
                switch (variant)
                {
                    case 0: plan.ContextAddress++; break;
                    case 1: plan.ContextAddress = 0xffff0000; break;
                    case 2: plan.CodeAddress = plan.ContextAddress; break;
                    case 3: plan.UserRoot = plan.CodeAddress; break;
                    case 4: plan.UserRip = LinuxSyscallGateway.UserEnd; break;
                    case 5: plan.TssRsp0Address++; break;
                    case 6: gdt[13] = 0xfa; break; // user privilege on original kernel CS
                    case 7: gdt[29] = 0x89; break; // available TSS is not the loaded busy TSS
                    case 8: idt[32 + 4] = 0x80; break;
                    case 9: plan.OriginalPf++; break;
                    case 10: sourceGdt = data; break;
                    default: capacity--; break;
                }
                HostCheck.True(!X64LinuxUserTransition.Prepare(data, capacity, plan, sourceGdt, 4096, idt, 4096));
                HostCheck.True(new ReadOnlySpan<byte>(data, (int)X64LinuxUserTransition.DataBytes).IndexOfAnyExcept((byte)0xa5) == -1);
            }
        }
        finally { NativeMemory.AlignedFree(data); NativeMemory.AlignedFree(gdt); NativeMemory.AlignedFree(idt); }
    }

    [HostTest("m16-005 syscall entry switches from hostile RSP before GPR FP and Win64 bridge use", HostLane.Fast, 2)]
    private static void EntryBytes()
    {
        byte* code = Allocate(8192);
        try
        {
            var plan = Plan(); new Span<byte>(code, 8192).Fill(0xa5);
            long before = GC.GetAllocatedBytesForCurrentThread();
            bool emitted = X64LinuxUserTransition.Emit(code, 4096, plan);
            long allocated = GC.GetAllocatedBytesForCurrentThread() - before;
            HostCheck.True(emitted && allocated == 0 && X64LinuxUserTransition.ValidateReadBack(code, 4096, plan));
            HostCheck.True(new ReadOnlySpan<byte>(code + 4096, 4096).IndexOfAnyExcept((byte)0xa5) == -1);
            ReadOnlySpan<byte> observer = new(code + 0x400, 0x200);
            HostCheck.True(observer[..9].SequenceEqual(new byte[] {0xf3,0x0f,0x1e,0xfa,0x53,0x57,0x48,0x89,0xcf}));
            HostCheck.True(observer.IndexOf(new byte[] {0x0f,0x20,0xc0,0x48,0x89,0x87,0,0,0,0}) >= 0);
            HostCheck.True(observer.IndexOf(new byte[] {0x66,0x0f,0,0xc8,0x48,0x89,0x87,0x30,0,0,0}) >= 0);
            HostCheck.True(observer.IndexOf(new byte[] {0x0f,1,0x87,0x88,0,0,0,0x0f,1,0x8f,0x98,0,0,0}) >= 0);
            HostCheck.True(observer.IndexOf(new byte[] {0x5f,0x5b,0xc3}) >= 0);
            ReadOnlySpan<byte> establish = new(code + 0xb00, 0x100);
            HostCheck.True(establish[..5].SequenceEqual(new byte[] {0xf3,0x0f,0x1e,0xfa,0x9c}));
            HostCheck.True(establish.IndexOf(new byte[] {0x49,0x89,0xc0,0x31,0xc0,0x8e,0xe0,0x4c,0x89,0xc0}) >= 0);
            HostCheck.True(establish.IndexOf(new byte[] {0x49,0x89,0xc0,0x31,0xc0,0x8e,0xe8,0x4c,0x89,0xc0}) >= 0);
            HostCheck.True(establish.IndexOf(new byte[] {0x0f,0x30,0x9d,0xc3}) >= 0);
            ReadOnlySpan<byte> entry = new(code + 0x600, 0x600);
            byte[] switchBytes = [0x0f,0x01,0xf8, 0x65,0x48,0x89,0x24,0x25,8,0,0,0,
                0x65,0x48,0x8b,0x24,0x25,0,0,0,0, 0x65,0x4c,0x89,0x34,0x25,0xb0,1,0,0];
            HostCheck.True(entry[..switchBytes.Length].SequenceEqual(switchBytes));
            // Linux arg3 is R10, distinct from Win64 RCX; arg4/5 are R8/R9.
            HostCheck.True(entry.IndexOf(new byte[] {0x4d,0x89,0x96,0x20,1,0,0}) >= 0);
            HostCheck.True(entry.IndexOf(new byte[] {0x4d,0x89,0x86,0x28,1,0,0}) >= 0);
            HostCheck.True(entry.IndexOf(new byte[] {0x4d,0x89,0x8e,0x30,1,0,0}) >= 0);
            HostCheck.True(entry.IndexOf(new byte[] {0x49,0x8b,0x8e,0xd8,0,0,0,0x49,0x8d,0x96,0,1,0,0}) >= 0);
            HostCheck.True(entry.IndexOf(new byte[] {0x48,0x83,0xec,0x20,0xff,0xd0,0x48,0x83,0xc4,0x20,0x3c,1,0x0f,0x85}) >= 0);
            HostCheck.True(entry.IndexOf(new byte[] {0x49,0x0f,0xae,0x86,0,4,0,0,0x49,0x0f,0xae,0x8e,0,2,0,0}) >= 0);
            HostCheck.True(entry.IndexOf(new byte[] {0x0f,0x01,0xf8,0x48,0xcf}) >= 0);
            HostCheck.Equal(0x801000UL, *(ulong*)(code + 0xff0)); HostCheck.Equal(0x802000UL, *(ulong*)(code + 0xff8));
            // The byte helper is intentionally usable for an independent local
            // disassembly review, never executable in this host process.
        }
        finally { NativeMemory.AlignedFree(code); }
    }

    [HostTest("m16-005 fault branches preserve kernel frames and user cleanup restores roots MSRs selectors and FP", HostLane.Fast, 3)]
    private static void FaultAndRestore()
    {
        byte* code = Allocate(4096);
        try
        {
            HostCheck.True(X64LinuxUserTransition.Emit(code, 4096, Plan()));
            foreach (int start in new[] {0xc00, 0xd00})
            {
                ReadOnlySpan<byte> fault = new(code + start, 256);
                HostCheck.True(fault[..10].SequenceEqual(new byte[] {0x48,0x83,0x7c,0x24,0x10,0x3b,0x74,6,0xff,0x25}));
                int target = start + 14 + *(int*)(code + start + 10);
                HostCheck.Equal(start == 0xc00 ? 0xff0 : 0xff8, target);
                HostCheck.True(fault.IndexOf(new byte[] {0x49,0x8b,0xa6,0,0,0,0}) >= 0); // callback runs on owned top
                HostCheck.True(fault.IndexOf(new byte[] {0x4c,0x89,0xf2}) >= 0); // callback RDX=context
            }
            ReadOnlySpan<byte> cleanup = new(code + 0xe00, 0x1f0);
            HostCheck.True(new ReadOnlySpan<byte>(code, 0x600).IndexOf(new byte[] {0x66,0x8c,0xd0,0x66,0x41,0x89,0x86,0xfc,1,0,0}) >= 0);
            HostCheck.True(cleanup.IndexOf(new byte[] {0x66,0x41,0x8b,0x86,0xfc,1,0,0,0x8e,0xd0}) >= 0);
            HostCheck.True(cleanup.IndexOf(new byte[] {0x49,0x8b,0x86,0x20,0,0,0,0x0f,0x22,0xd8}) >= 0);
            HostCheck.True(cleanup.IndexOf(new byte[] {0x41,0x0f,0x01,0x96,0x88,0,0,0,0x41,0x0f,0x01,0x9e,0xa8,0,0,0}) >= 0);
            HostCheck.True(cleanup.IndexOf(new byte[] {0x49,0x0f,0xae,0x8e,0,2,0,0}) >= 0);
            HostCheck.True(cleanup.IndexOf(new byte[] {0x41,0x5f,0x41,0x5e,0x41,0x5d,0x41,0x5c,0x5f,0x5e,0x5d,0x5b,0x9d,0xb8,1,0,0,0,0xc3}) >= 0);
            // All emitted MSR checks target cleanup, including a failed pre-user setup.
            int conditional = 0;
            for (int i = 0; i < 0xc00 - 6; i++) if (code[i] == 0x0f && code[i + 1] == 0x85)
            { HostCheck.Equal(0xe00, i + 6 + *(int*)(code + i + 2)); conditional++; }
            HostCheck.Equal(9, conditional);
        }
        finally { NativeMemory.AlignedFree(code); }
    }

    [HostTest("m16-005 transition code capacity corruption and unsupported CPU controls fail closed", HostLane.Fast, 4)]
    private static void ReadBackAndCpu()
    {
        HostCheck.True(X64LinuxUserTransition.SingleLogicalProcessor(0x800, 0x07000000));
        HostCheck.True(X64LinuxUserTransition.SingleLogicalProcessor(0x10800, 0x17000000));
        HostCheck.True(!X64LinuxUserTransition.SingleLogicalProcessor(0x800, 0x17000000));
        HostCheck.True(!X64LinuxUserTransition.SingleLogicalProcessor(0x20800, 0x17000000));
        byte* code = Allocate(4096);
        try
        {
            var plan = Plan(); new Span<byte>(code, 4096).Fill(0xa5);
            HostCheck.True(!X64LinuxUserTransition.Emit(code, 4095, plan));
            HostCheck.True(new ReadOnlySpan<byte>(code, 4096).IndexOfAnyExcept((byte)0xa5) == -1);
            HostCheck.True(X64LinuxUserTransition.Emit(code, 4096, plan));
            foreach (int offset in new[] {0, 4, 128, 0x400, 0x600, 0x800, 0xbff, 0xc00, 0xd00, 0xe00, 0xff0, 0xfff})
            { code[offset] ^= 1; HostCheck.True(!X64LinuxUserTransition.ValidateReadBack(code, 4096, plan)); code[offset] ^= 1; }
            plan.Dispatcher++; HostCheck.True(X64LinuxUserTransition.ValidateReadBack(code, 4096, plan)); // callback lives in prepared data
            plan.OriginalGp++; HostCheck.True(!X64LinuxUserTransition.ValidateReadBack(code, 4096, plan));
            HostCheck.True(X64LinuxUserTransition.AdmitCpu(0x80000001, 0x220, 0xd00, 0x07000000, 0x20100800, 2));
            foreach (ulong bit in new ulong[] {1UL << 12, 1UL << 16, 1UL << 17, 1UL << 18, 1UL << 20, 1UL << 21, 1UL << 23})
                HostCheck.True(!X64LinuxUserTransition.AdmitCpu(0x80000001, 0x220 | bit, 0xd00, 0x07000000, 0x20100800, 2));
            HostCheck.True(!X64LinuxUserTransition.AdmitCpu(0x80000009, 0x220, 0xd00, 0x07000000, 0x20100800, 2));
            HostCheck.True(!X64LinuxUserTransition.AdmitCpu(0x80000001, 0x220, 0xd00, 0x07000000, 0x20100000, 2));
            HostCheck.True(!X64LinuxUserTransition.AdmitCpu(0x80000001, 0x220, 0xd00, 0x07000000, 0x20100800, 0x202));
        }
        finally { NativeMemory.AlignedFree(code); }
    }

    [HostTest("m16-005 syscall transcript binds write result exit accounting and rejects forged or partial receipts", HostLane.Fast, 5)]
    private static void TranscriptControls()
    {
        int parentCount = QemuContracts.M03OwnedSubsystemMarkers.Count + 7 + 7 + 17;
        string[] parents = Enumerable.Range(0, parentCount).Select(i => "parent " + i).ToArray();
        string[] tail =
        [
            "TEST_START m16-005",
            "M16_005_ARMED root=3145728 cs=59 ss=51 kernelStack=2179072 guard=absent syscall=sce-star-lstar-fmask return=iretq fp=x87-sse kernelFsGs=owned-null",
            "M16_005_WRITE fd=2 bytes=5 data=HELLO",
            "M16_005_CALL nr=1 a0=2 a1=4202496 a2=5 a3=16 a4=32 a5=48 rip=4194560 rsp=1879048176 flags=2 valueBits=5 action=1 status=1",
            "M16_005_CALL nr=231 a0=42 a1=0 a2=0 a3=0 a4=0 a5=0 rip=4194576 rsp=1879048176 flags=2 valueBits=0 action=2 status=2",
            "M16_005_RESTORE root=true descriptors=true ss=true msrs=true fsGs=true fp=true stack=true",
            "M16_005_EXIT code=42 calls=2 bytes=5 writes=1", "CLROS_M16_005_READY", "TEST_PASS m16-005"
        ];
        HostCheck.True(QemuTranscripts.ValidateLinuxSyscallTail([.. parents, .. tail]).SequenceEqual(parents));
        for (int variant = 0; variant < 10; variant++)
        {
            string[] bad = (string[])tail.Clone();
            switch (variant)
            {
                case 0: bad[3] = bad[3].Replace("valueBits=5", "valueBits=0"); break;
                case 1: bad[3] = bad[3].Replace("a0=2", "a0=1"); break;
                case 2: bad[3] = bad[3].Replace("a2=5", "a2=6"); break;
                case 3: bad[4] = bad[4].Replace("action=2", "action=1"); break;
                case 4: bad[6] = bad[6].Replace("calls=2", "calls=3"); break;
                case 5: bad[6] = bad[6].Replace("bytes=5", "bytes=6"); break;
                case 6: bad[5] = bad[5].Replace("ss=true", "ss=false"); break;
                case 7: bad[3] = bad[3].Replace("flags=2", "flags=514"); break;
                case 8: bad[2] = bad[2].Replace("HELLO", "HELL"); break;
                default:
                    bad[4] = bad[4].Replace("a0=42", "a0=99");
                    bad[6] = bad[6].Replace("code=42", "code=99"); break;
            }
            bool rejected = false;
            try { _ = QemuTranscripts.ValidateLinuxSyscallTail([.. parents, .. bad]); }
            catch (ToolFailure) { rejected = true; }
            HostCheck.True(rejected, "transcript corruption " + variant);
        }
        string unsupported = "M16_005_CALL nr=999 a0=0 a1=0 a2=0 a3=0 a4=0 a5=0 rip=4194544 rsp=1879048176 flags=2 valueBits=18446744073709551578 action=1 status=1";
        string[] withUnsupported = [tail[0], tail[1], unsupported, .. tail[2..]];
        withUnsupported[^3] = withUnsupported[^3].Replace("calls=2", "calls=3");
        HostCheck.True(QemuTranscripts.ValidateLinuxSyscallTail([.. parents, .. withUnsupported]).SequenceEqual(parents));
        withUnsupported[2] = unsupported.Replace("18446744073709551578", "0");
        bool wrongErrnoRejected = false;
        try { _ = QemuTranscripts.ValidateLinuxSyscallTail([.. parents, .. withUnsupported]); }
        catch (ToolFailure) { wrongErrnoRejected = true; }
        HostCheck.True(wrongErrnoRejected);
    }
}
