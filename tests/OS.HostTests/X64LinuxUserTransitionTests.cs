using System.Runtime.InteropServices;
using OS.Kernel.Compatibility.LinuxX64;
using OS.Tooling;
using Transition = OS.Kernel.Compatibility.LinuxX64.X64LinuxUserTransition;

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

    [HostTest("m16-009 user IRQ private IDT borrows only E1 FF and fault gates with exact IST ownership")]
    private static void UserInterruptGates()
    {
        byte* data = Allocate(Transition.DataBytes), gdt = Allocate(4096), idt = Allocate(4096);
        try
        {
            HostCheck.True(data != null && gdt != null && idt != null); Descriptors(gdt, idt);
            byte[] original = new ReadOnlySpan<byte>(idt, 4096).ToArray(); var plan = Plan();
            plan.ResumeUser = plan.UserInterrupts = true; plan.KernelRoot = 0x500000;
            plan.UserFaultEntry = 0x600000; plan.UserSpuriousEntry = 0x7001e0;
            HostCheck.True(Transition.Prepare(data, Transition.DataBytes, plan, gdt, 4096, idt, 4096));
            for (uint vector = 0; vector < 256; vector++)
            {
                byte* gate = data + Transition.IdtOffset + vector * 16;
                if (vector is not (13 or 14 or 0xe1 or 0xff))
                { HostCheck.True(new ReadOnlySpan<byte>(gate, 16).SequenceEqual(original.AsSpan((int)vector * 16, 16))); continue; }
                ulong expected = vector switch { 13 => plan.CodeAddress + Transition.GpOffset, 14 => plan.CodeAddress + Transition.PfOffset,
                    0xe1 => plan.UserFaultEntry + Transition.UserInterruptOffset, _ => plan.UserSpuriousEntry };
                ulong handler = *(ushort*)gate | (ulong)*(ushort*)(gate + 6) << 16 | (ulong)*(uint*)(gate + 8) << 32;
                HostCheck.True(handler == expected && *(ushort*)(gate + 2) == 8 && gate[4] == (vector >= 0xe1 ? 4 : 0) &&
                    gate[5] == 0x8e && *(uint*)(gate + 12) == 0);
            }
            HostCheck.True(original.AsSpan().SequenceEqual(new ReadOnlySpan<byte>(idt, 4096)));
            new Span<byte>(data, (int)Transition.DataBytes).Fill(0xa5); plan.UserSpuriousEntry = 0;
            HostCheck.True(!Transition.Prepare(data, Transition.DataBytes, plan, gdt, 4096, idt, 4096));
            HostCheck.True(new ReadOnlySpan<byte>(data, (int)Transition.DataBytes).IndexOfAnyExcept((byte)0xa5) < 0);
        }
        finally { NativeMemory.AlignedFree(data); NativeMemory.AlignedFree(gdt); NativeMemory.AlignedFree(idt); }
    }

    [HostTest("m16-006 dispatcher adopts original kernel root and reloads user root before checked IRET", HostLane.Fast, 5)]
    private static void KernelDispatchRoot()
    {
        byte* code = Allocate(4096);
        try
        {
            var plan = Plan(); plan.KernelRoot = 0x500000;
            HostCheck.True(X64LinuxUserTransition.Emit(code, 4096, plan) && X64LinuxUserTransition.ValidateReadBack(code, 4096, plan));
            var bytes = new ReadOnlySpan<byte>(code + X64LinuxUserTransition.SyscallOffset, 0x500);
            // MOV RAX,[R14+20h]; MOV CR3,RAX before the callback; then the exact
            // R14+28h root reload after cmp al,1's cleanup branch.
            byte[] kernel = [0x49,0x8b,0x86,0x20,0,0,0,0x0f,0x22,0xd8];
            byte[] user = [0x49,0x8b,0x86,0x28,0,0,0,0x0f,0x22,0xd8];
            int enter = bytes.IndexOf(kernel), restore = bytes.IndexOf(user);
            HostCheck.True(enter >= 0 && restore > enter && bytes[(enter + kernel.Length)..restore].IndexOf((byte)0xff) >= 0);
            HostCheck.Equal(0x640U, X64LinuxUserTransition.KernelRootOffset);
            ReadOnlySpan<byte> setup = new(code, 0x400);
            // Independent saved-selector displacements occupy 1D0h..1D6h;
            // the root comparison reads a disjoint qword after the private GDT.
            HostCheck.True(setup.IndexOf(new byte[] { 0x49, 0x3b, 0x86, 0x40, 6, 0, 0 }) >= 0);
            for (byte segment = 0; segment < 4; segment++)
                HostCheck.True(setup.IndexOf(new byte[] { 0x66, 0x41, 0x89, 0x86, (byte)(0xd0 + segment * 2), 1, 0, 0 }) >= 0);
            plan.KernelRoot = plan.UserRoot; HostCheck.True(!X64LinuxUserTransition.Emit(code, 4096, plan));
            plan.KernelRoot = 0x100000000; HostCheck.True(!X64LinuxUserTransition.Emit(code, 4096, plan));
        }
        finally { NativeMemory.AlignedFree(code); }
    }

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
    private static LinuxSyscallFrame ImageFrame() => new()
    {
        Number = 0xa001000000000001, Argument0 = 0xa002000000000002, Argument1 = 0xa003000000000003,
        Argument2 = 0xa004000000000004, Argument3 = 0xa005000000000005, Argument4 = 0xa006000000000006,
        Argument5 = 0xa007000000000007, Rip = 0x400178, Rsp = 0x70001000, Rflags = 0x803,
        FsBase = 0x600080, GsBase = 0x610040
    };

    [HostTest("m16-009 fault emitter preserves complete user state before root adoption and uses only checked IRET or immutable cleanup")]
    private static void FullFaultEmitter()
    {
        byte* code = Allocate(4096), fault = Allocate(4096);
        try
        {
            var p = Plan(); p.ResumeUser = true; p.KernelRoot = 0x500000; p.UserFaultEntry = 0xa00000;
            HostCheck.True(Transition.Emit(code,4096,p) && Transition.ValidateReadBack(code,4096,p));
            HostCheck.True(Transition.EmitFaultCapture(fault,2048,p) && Transition.VerifyFaultCapture(fault,2048,p));
            foreach ((uint offset,byte vector) in new[] {(0xc00U,(byte)13),(0xd00U,(byte)14)})
            {
                ReadOnlySpan<byte> stub = new(code + offset,32);
                HostCheck.True(stub[..6].SequenceEqual(new byte[] {0x48,0x83,0x7c,0x24,0x10,0x3b}));
                HostCheck.True(stub.Slice(6,2).SequenceEqual(new byte[] {0x74,6}));
                HostCheck.True(stub.Slice(14,8).SequenceEqual(new byte[] {0x6a,vector,0xff,0x25,0,0,0,0}));
                HostCheck.Equal(p.UserFaultEntry,*(ulong*)(code + offset + 22));
            }
            ReadOnlySpan<byte> bytes = new(fault,2048);
            byte[] rax = [0x49,0x89,0x86,0xe8,0,0,0], kernel = [0x49,0x8b,0x86,0x20,0,0,0,0x0f,0x22,0xd8];
            byte[] call = [0x49,0x8b,0x86,0x38,0,0,0,0x48,0x83,0xec,0x20,0xff,0xd0,0x48,0x83,0xc4,0x20];
            int saved = bytes.IndexOf(rax), adopted = bytes.IndexOf(kernel), callback = bytes.IndexOf(call);
            HostCheck.True(saved == 26 && adopted > saved && callback > adopted);
            (uint register,uint offset)[] stores = [(3,0x160),(5,0x168),(6,0x170),(7,0x178),(2,0x180),(8,0x188),(9,0x190),
                (10,0x198),(12,0x1a0),(13,0x1a8),(15,0x1b8),(1,0x1c0),(11,0x1c8)];
            foreach (var store in stores)
            {
                byte[] expected = [(byte)(store.register < 8 ? 0x49 : 0x4d),0x89,(byte)(0x86 | ((store.register & 7) << 3)),
                    (byte)store.offset,(byte)(store.offset >> 8),0,0];
                HostCheck.True(bytes[..adopted].IndexOf(expected) > saved);
            }
            HostCheck.True(bytes[..saved].IndexOf(new byte[] {0x65,0x4c,0x89,0x34,0x25,0xb0,1,0,0}) >= 0); // R14 before scratch.
            foreach (byte offset in new byte[] {0x48,0x50})
            {
                byte[] witness=[0x49,0x89,0x86,offset,6,0,0];
                HostCheck.True(bytes[..adopted].IndexOf(witness)>=0);
                HostCheck.True(new ReadOnlySpan<byte>(code+0x600,0x4f0).IndexOf(witness)>=0);
            }
            foreach (byte stackOffset in new byte[] {0,8,16,24,32,40,48})
                HostCheck.True(bytes[..adopted].IndexOf(new byte[] {0x48,0x8b,0x44,0x24,stackOffset}) >= 0);
            HostCheck.True(bytes[(callback + call.Length)..].IndexOf(new byte[] {0x0f,0x01,0xf8,0x48,0xcf}) >= 0);
            HostCheck.True(bytes.Slice(2032,6).SequenceEqual(new byte[] {0xff,0x25,0,0,0,0}));
            HostCheck.Equal(p.CodeAddress + 0xe00,*(ulong*)(fault + 2038));
            for (uint i = 0; i < 2048; i++) { fault[i] ^= 1; HostCheck.True(!Transition.VerifyFaultCapture(fault,2048,p)); fault[i] ^= 1; }
            byte[] baseline = bytes.ToArray();
            foreach (ulong invalid in new ulong[] {0,p.CodeAddress,p.ContextAddress,p.UserRoot,p.KernelRoot,0x100000000,0xa00001})
            { p.UserFaultEntry = invalid; HostCheck.True(!Transition.EmitFaultCapture(fault,2048,p)); HostCheck.True(bytes.SequenceEqual(baseline)); }
        }
        finally { NativeMemory.AlignedFree(code); NativeMemory.AlignedFree(fault); }
    }

    [HostTest("m16-009 active gateway admits actual swapped user bases and rejects every mismatched CPU or retained root field")]
    private static void CallbackEnvelope()
    {
        byte* data = Allocate(Transition.DataBytes);
        try
        {
            new Span<byte>(data,(int)Transition.DataBytes).Clear();
            *(ulong*)(data + 0x1f0) = 1; *(ulong*)(data + 0x20) = *(ulong*)(data + 0x640) = 0x500000;
            *(ulong*)(data + 0x648) = *(ulong*)(data + 0x150) = 0x123456789;
            *(ulong*)(data + 0x650) = *(ulong*)(data + 0x158) = 0x234567890;
            ulong* cpu = stackalloc ulong[11] {0x500000,2,8,0,24,0x123456789,(ulong)data,0x234567890,0,0,0x500000};
            HostCheck.True(Check(data,cpu)); cpu[3] = 16; HostCheck.True(Check(data,cpu)); cpu[3] = 0;
            for (int i = 0; i < 11; i++)
            { ulong old = cpu[i]; cpu[i] ^= i == 1 ? 0x200UL : 1UL; HostCheck.True(!Check(data,cpu), "cpu " + i); cpu[i] = old; }
            foreach (uint offset in new uint[] {0x1f0,0x20,0x640,0x648,0x650})
            { *(ulong*)(data + offset) ^= 1; HostCheck.True(!Check(data,cpu)); *(ulong*)(data + offset) ^= 1; }
            // A checked setter changes the return frame before WRMSR. CPU
            // ownership must still admit original bases, never the desired ones.
            *(ulong*)(data + 0x150)=0x345678901; *(ulong*)(data + 0x158)=0x456789012;
            HostCheck.True(Check(data,cpu));
            cpu[5]=*(ulong*)(data+0x150); HostCheck.True(!Check(data,cpu)); cpu[5]=0x123456789;
            cpu[7]=*(ulong*)(data+0x158); HostCheck.True(!Check(data,cpu));
            static bool Check(byte* d,ulong* c) => Transition.ValidateCallback(d,Transition.DataBytes,c[10],c[0],c[1],c[2],c[3],c[4],c[5],c[6],c[7],c[8],c[9]);
        }
        finally { NativeMemory.AlignedFree(data); }
    }

    private static LinuxSyscallAccess ImageAccess(ulong* denied) => new() { Context = denied, PageAccess = &ImagePageAccess };

    [UnmanagedCallersOnly]
    private static byte ImagePageAccess(void* context, ulong page)
    {
        if (page == *(ulong*)context) return 0;
        return page == 0x400000 ? (byte)5 : page is 0x70000000 or 0x600000 or 0x610000 ? (byte)3 : (byte)0;
    }

    private static void ImageFp(byte* fx, uint mask)
    {
        for (int i = 0; i < 512; i++) fx[i] = (byte)(i * 37 + 11);
        *(ushort*)fx = 0x37f; fx[4] = 0;
        *(uint*)(fx + 24) = 0x1f80; *(uint*)(fx + 28) = mask;
    }

    private static void GatewayUser(byte* data, uint mask)
    {
        *(LinuxSyscallFrame*)(data + 0x100) = ImageFrame();
        *(long*)(data + 0xe8) = -0x102030405060708L;
        for (uint i = 0; i < 14; i++) *(ulong*)(data + 0x160 + i * 8) = 0xb000000000000001UL + i * 0x0101010101010101UL;
        ImageFp(data + 0x400, mask);
    }

    [HostTest("m16-008 unpublished user image initializes only validated user state", HostLane.Fast, 0)]
    private static void UserImageInitialization()
    {
        byte* storage = Allocate(4096); ulong denied = ulong.MaxValue; var access = ImageAccess(&denied);
        try
        {
            var image = (Transition.UserImage*)storage;
            HostCheck.Equal(728, sizeof(Transition.UserImage));
            HostCheck.Equal(728U, Transition.UserImageBytes);
            HostCheck.Equal(8UL, (ulong)image->Fx & 15); // Copied image is deliberately not an FXRSTOR operand.
            foreach (uint mask in new uint[] { 0, 0xffff })
            {
                new Span<byte>(storage, 4096).Fill(0xa5); LinuxSyscallFrame frame = ImageFrame();
                HostCheck.True(Transition.InitializeUserImage(image, Transition.UserImageBytes, frame, mask, access));
                HostCheck.True(new ReadOnlySpan<byte>(&frame, 96).SequenceEqual(new ReadOnlySpan<byte>(&image->Frame, 96)));
                HostCheck.Equal(0L, image->Value);
                for (int i = 0; i < 14; i++) HostCheck.Equal(0UL, image->Gpr[i]);
                byte[] expected = new byte[512]; expected[0] = 0x7f; expected[1] = 3;
                BitConverter.GetBytes(0x1f80U).CopyTo(expected, 24); BitConverter.GetBytes(mask).CopyTo(expected, 28);
                HostCheck.True(new ReadOnlySpan<byte>(image->Fx, 512).SequenceEqual(expected));
                HostCheck.True(new ReadOnlySpan<byte>(storage + 728, 4096 - 728).IndexOfAnyExcept((byte)0xa5) == -1);
            }
            for (int variant = 0; variant < 7; variant++)
            {
                new Span<byte>(storage, 4096).Fill(0xa5); var frame = ImageFrame(); uint mask = 0xffff, bytes = Transition.UserImageBytes;
                Transition.UserImage* target = image;
                switch (variant)
                {
                    case 0: frame.Rip = LinuxSyscallGateway.UserEnd; break;
                    case 1: frame.Rflags |= 0x200; break;
                    case 2: mask = 0x1ffff; break;
                    case 3: mask = 0x1f00; break;
                    case 4: bytes--; break;
                    case 5: target = (Transition.UserImage*)(storage + 1); break;
                    default: target = null; break;
                }
                HostCheck.True(!Transition.InitializeUserImage(target, bytes, frame, mask, access));
                HostCheck.True(new ReadOnlySpan<byte>(storage, 4096).IndexOfAnyExcept((byte)0xa5) == -1);
            }
        }
        finally { NativeMemory.AlignedFree(storage); }
    }

    [HostTest("m16-008 user image copies every frame GPR return and FP byte without supervisor mutation", HostLane.Fast, 1)]
    private static void UserImageRoundTrip()
    {
        byte* data = Allocate(Transition.DataBytes), storage = Allocate(4096), gdt = Allocate(4096), idt = Allocate(4096);
        ulong denied = ulong.MaxValue; var access = ImageAccess(&denied);
        try
        {
            Descriptors(gdt, idt); new Span<byte>(data, (int)Transition.DataBytes).Fill(0xa5);
            HostCheck.True(Transition.Prepare(data, Transition.DataBytes, Plan(), gdt, 4096, idt, 4096));
            GatewayUser(data, 0xffff); *(ulong*)(data + 0x1f0) = 1;
            byte[] captured = new ReadOnlySpan<byte>(data, (int)Transition.DataBytes).ToArray();
            new Span<byte>(storage, 4096).Fill(0xa5); var image = (Transition.UserImage*)storage;
            HostCheck.True(Transition.CaptureUserImage(data, Transition.DataBytes, image, Transition.UserImageBytes, 0xffff, access));
            HostCheck.True(new ReadOnlySpan<byte>(data, (int)Transition.DataBytes).SequenceEqual(captured));
            LinuxSyscallFrame frame = ImageFrame();
            HostCheck.True(new ReadOnlySpan<byte>(&frame, 96).SequenceEqual(new ReadOnlySpan<byte>(&image->Frame, 96)));
            HostCheck.Equal(-0x102030405060708L, image->Value);
            for (uint i = 0; i < 14; i++) HostCheck.Equal(0xb000000000000001UL + i * 0x0101010101010101UL, image->Gpr[i]);
            HostCheck.True(new ReadOnlySpan<byte>(data + 0x400, 512).SequenceEqual(new ReadOnlySpan<byte>(image->Fx, 512)));
            HostCheck.True(new ReadOnlySpan<byte>(storage + 728, 4096 - 728).IndexOfAnyExcept((byte)0xa5) == -1);
            byte[] imageBefore = new ReadOnlySpan<byte>(storage, 4096).ToArray();
            foreach (ulong phase in new ulong[] { 0, 1, 2 })
            {
                new Span<byte>(data, (int)Transition.DataBytes).Fill(0x5a);
                HostCheck.True(Transition.Prepare(data, Transition.DataBytes, Plan(), gdt, 4096, idt, 4096));
                *(ulong*)(data + 0x1f0) = phase;
                byte[] expected = new ReadOnlySpan<byte>(data, (int)Transition.DataBytes).ToArray();
                new ReadOnlySpan<byte>(&image->Value, 8).CopyTo(expected.AsSpan(0xe8, 8));
                new ReadOnlySpan<byte>(&image->Frame, 96).CopyTo(expected.AsSpan(0x100, 96));
                new ReadOnlySpan<byte>(image->Gpr, 112).CopyTo(expected.AsSpan(0x160, 112));
                new ReadOnlySpan<byte>(image->Fx, 512).CopyTo(expected.AsSpan(0x400, 512));
                HostCheck.True(phase == 1
                    ? Transition.RestoreActiveUserImage(data, Transition.DataBytes, image, Transition.UserImageBytes, 0xffff, access)
                    : Transition.InstallUserImage(data, Transition.DataBytes, image, Transition.UserImageBytes, 0xffff, access));
                // Whole-buffer equality includes the kernel header/FP image, selectors,
                // private descriptors, guard sentinel and every kernel-stack byte.
                HostCheck.True(new ReadOnlySpan<byte>(data, (int)Transition.DataBytes).SequenceEqual(expected));
                HostCheck.True(new ReadOnlySpan<byte>(storage, 4096).SequenceEqual(imageBefore));
            }
        }
        finally { NativeMemory.AlignedFree(data); NativeMemory.AlignedFree(storage); NativeMemory.AlignedFree(gdt); NativeMemory.AlignedFree(idt); }
    }

    [HostTest("m16-008 capture requires active phase and installation requires inactive phase", HostLane.Fast, 2)]
    private static void UserImagePhases()
    {
        byte* data = Allocate(Transition.DataBytes), storage = Allocate(4096); ulong denied = ulong.MaxValue; var access = ImageAccess(&denied);
        try
        {
            new Span<byte>(data, (int)Transition.DataBytes).Fill(0x5a); GatewayUser(data, 0xffff);
            new Span<byte>(storage, 4096).Fill(0xa5);
            var image = (Transition.UserImage*)storage; *(ulong*)(data + 0x1f0) = 1;
            HostCheck.True(Transition.CaptureUserImage(data, Transition.DataBytes, image, Transition.UserImageBytes, 0xffff, access));
            foreach (int mode in new[] { 0, 1, 2 })
            foreach (ulong phase in mode is 0 or 2 ? new ulong[] { 0, 2, 3, ulong.MaxValue } : new ulong[] { 1, 3, ulong.MaxValue })
            {
                *(ulong*)(data + 0x1f0) = phase;
                byte[] before = new ReadOnlySpan<byte>(data, (int)Transition.DataBytes).ToArray();
                byte[] imageBefore = new ReadOnlySpan<byte>(storage, 4096).ToArray();
                bool accepted = mode == 0
                    ? Transition.CaptureUserImage(data, Transition.DataBytes, image, Transition.UserImageBytes, 0xffff, access)
                    : mode == 1 ? Transition.InstallUserImage(data, Transition.DataBytes, image, Transition.UserImageBytes, 0xffff, access)
                    : Transition.RestoreActiveUserImage(data, Transition.DataBytes, image, Transition.UserImageBytes, 0xffff, access);
                HostCheck.True(!accepted);
                HostCheck.True(new ReadOnlySpan<byte>(data, (int)Transition.DataBytes).SequenceEqual(before));
                HostCheck.True(new ReadOnlySpan<byte>(storage, 4096).SequenceEqual(imageBefore));
            }
        }
        finally { NativeMemory.AlignedFree(data); NativeMemory.AlignedFree(storage); }
    }

    [HostTest("m16-008 invalid user image return mask capacity and aliases reject before mutation", HostLane.Fast, 3)]
    private static void UserImageRejections()
    {
        byte* data = Allocate(Transition.DataBytes), storage = Allocate(4096); var image = (Transition.UserImage*)storage;
        try
        {
            foreach (int mode in new[] { 0, 1, 2 })
            for (int variant = 0; variant < 19; variant++)
            {
                bool capture = mode == 0;
                ulong denied = ulong.MaxValue; var access = ImageAccess(&denied);
                new Span<byte>(data, (int)Transition.DataBytes).Fill(0x5a); new Span<byte>(storage, 4096).Fill(0xa5);
                GatewayUser(data, 0xffff); *(ulong*)(data + 0x1f0) = 1;
                HostCheck.True(Transition.CaptureUserImage(data, Transition.DataBytes, image, Transition.UserImageBytes, 0xffff, access));
                *(ulong*)(data + 0x1f0) = mode == 1 ? 2UL : 1UL;
                byte* gateway = data; Transition.UserImage* target = image; uint dataBytes = Transition.DataBytes, imageBytes = Transition.UserImageBytes, mask = 0xffff;
                LinuxSyscallFrame* frame = capture ? (LinuxSyscallFrame*)(data + 0x100) : &image->Frame;
                byte* fx = capture ? data + 0x400 : image->Fx;
                switch (variant)
                {
                    case 0: dataBytes--; break;
                    case 1: imageBytes--; break;
                    case 2: gateway = null; break;
                    case 3: target = null; break;
                    case 4: gateway++; break;
                    case 5: target = (Transition.UserImage*)(storage + 1); break;
                    case 6: target = (Transition.UserImage*)(data + 0x2000); break;
                    case 7: target = (Transition.UserImage*)(data + Transition.DataBytes - 8); break;
                    case 8: frame->Rip = LinuxSyscallGateway.UserEnd; break;
                    case 9: frame->Rsp = 4096; break;
                    case 10: frame->Rflags |= 0x200; break;
                    case 11: frame->FsBase = LinuxSyscallGateway.UserEnd; break;
                    case 12: frame->GsBase = LinuxSyscallGateway.UserEnd; break;
                    case 13: denied = frame->Rip & ~4095UL; break;
                    case 14: denied = (frame->Rsp - 8) & ~4095UL; break;
                    case 15: mask = 0x1ffff; break;
                    case 16: mask = 0x1f00; break;
                    case 17: *(uint*)(fx + 28) ^= 0x40; break;
                    default: *(uint*)(fx + 24) |= 0x80000000; break;
                }
                byte[] before = new ReadOnlySpan<byte>(data, (int)Transition.DataBytes).ToArray();
                byte[] imageBefore = new ReadOnlySpan<byte>(storage, 4096).ToArray();
                bool accepted = capture
                    ? Transition.CaptureUserImage(gateway, dataBytes, target, imageBytes, mask, access)
                    : mode == 1 ? Transition.InstallUserImage(gateway, dataBytes, target, imageBytes, mask, access)
                    : Transition.RestoreActiveUserImage(gateway, dataBytes, target, imageBytes, mask, access);
                HostCheck.True(!accepted, (capture ? "capture " : "install ") + variant);
                HostCheck.True(new ReadOnlySpan<byte>(data, (int)Transition.DataBytes).SequenceEqual(before));
                HostCheck.True(new ReadOnlySpan<byte>(storage, 4096).SequenceEqual(imageBefore));
            }
        }
        finally { NativeMemory.AlignedFree(data); NativeMemory.AlignedFree(storage); }
    }

    [HostTest("m16-008 resume entry restores Frame FS GS IRET and all GPRs with R14 last while legacy stays unchanged", HostLane.Fast, 4)]
    private static void ResumeUserEntryBytes()
    {
        byte* code = Allocate(4096), legacy = Allocate(4096), explicitLegacy = Allocate(4096);
        try
        {
            byte[] originalReturn = [0x6a,0x33, 0x49,0x8b,0x86,8,0,0,0,0x50, 0x6a,2,0x6a,0x3b,
                0x49,0x8b,0x86,0x18,0,0,0,0x50, 0x31,0xc0,0x31,0xc9,0x31,0xd2,0x31,0xdb,0x31,0xed,0x31,0xf6,0x31,0xff,
                0x45,0x31,0xc0,0x45,0x31,0xc9,0x45,0x31,0xd2,0x45,0x31,0xdb,0x45,0x31,0xe4,0x45,0x31,0xed,0x45,0x31,0xf6,0x45,0x31,0xff,0x48,0xcf];
            foreach (ulong kernelRoot in new ulong[] { 0, 0x500000 })
            {
                var old = Plan(); old.KernelRoot = kernelRoot;
                HostCheck.True(!old.ResumeUser && Transition.Emit(legacy, 4096, old)); old.ResumeUser = false;
                HostCheck.True(Transition.Emit(explicitLegacy, 4096, old));
                HostCheck.True(new ReadOnlySpan<byte>(legacy, 4096).SequenceEqual(new ReadOnlySpan<byte>(explicitLegacy, 4096)));
                ReadOnlySpan<byte> oldEntry = new(legacy, 0x400);
                HostCheck.True(oldEntry.IndexOf(originalReturn) >= 0);
                HostCheck.True(oldEntry.IndexOf(new byte[] {0x49,0x8b,0x86,0x58,0,0,0,0x49,0x89,0xc4,0xb9,0,1,0,0xc0}) >= 0);
                HostCheck.True(oldEntry.IndexOf(new byte[] {0x49,0x8b,0x86,0x60,0,0,0,0x49,0x89,0xc4,0xb9,1,1,0,0xc0}) >= 0);
            }
            var plan = Plan(); plan.KernelRoot = 0x500000; plan.ResumeUser = true;
            HostCheck.True(Transition.Emit(code, 4096, plan) && Transition.ValidateReadBack(code, 4096, plan));
            ReadOnlySpan<byte> entry = new(code, 0x400);
            HostCheck.True(entry.IndexOf(new byte[] {0x49,0x8b,0x86,0x50,1,0,0,0x49,0x89,0xc4,0xb9,0,1,0,0xc0}) >= 0);
            HostCheck.True(entry.IndexOf(new byte[] {0x49,0x8b,0x86,0x58,1,0,0,0x49,0x89,0xc4,0xb9,1,1,0,0xc0}) >= 0);
            byte[] resumeReturn =
            [
                0x49,0x0f,0xae,0x8e,0,4,0,0, 0x49,0x8b,0x86,0x28,0,0,0,0x0f,0x22,0xd8,
                0x49,0x8b,0xa6,0,0,0,0, 0x49,0xc7,0x86,0xf0,1,0,0,1,0,0,0,
                0x6a,0x33, 0x49,0x8b,0x86,0x40,1,0,0,0x50, 0x49,0x8b,0x86,0x48,1,0,0,0x50, 0x6a,0x3b,
                0x49,0x8b,0x86,0x38,1,0,0,0x50,
                0x49,0x8b,0x8e,0xc0,1,0,0, 0x49,0x8b,0x96,0x80,1,0,0, 0x49,0x8b,0x9e,0x60,1,0,0,
                0x49,0x8b,0xae,0x68,1,0,0, 0x49,0x8b,0xb6,0x70,1,0,0, 0x49,0x8b,0xbe,0x78,1,0,0,
                0x4d,0x8b,0x86,0x88,1,0,0, 0x4d,0x8b,0x8e,0x90,1,0,0, 0x4d,0x8b,0x96,0x98,1,0,0,
                0x4d,0x8b,0x9e,0xc8,1,0,0, 0x4d,0x8b,0xa6,0xa0,1,0,0, 0x4d,0x8b,0xae,0xa8,1,0,0,
                0x4d,0x8b,0xbe,0xb8,1,0,0, 0x49,0x8b,0x86,0xe8,0,0,0, 0x4d,0x8b,0xb6,0xb0,1,0,0, 0x48,0xcf
            ];
            HostCheck.True(entry.IndexOf(resumeReturn) >= 0 && entry.IndexOf(originalReturn) == -1);
            HostCheck.True(new ReadOnlySpan<byte>(code+0x400,0x200).SequenceEqual(new ReadOnlySpan<byte>(legacy+0x400,0x200)));
            HostCheck.True(new ReadOnlySpan<byte>(code+0xaf0,4096-0xaf0).SequenceEqual(new ReadOnlySpan<byte>(legacy+0xaf0,4096-0xaf0)));
            // The resumable syscall body adds exactly two immutable entry-base
            // stores. Only the two forward cleanup displacements and INT3 tail
            // padding move; compare every remaining byte to the legacy body.
            ReadOnlySpan<byte> oldSyscall=new(legacy+0x600,0x4f0);
            int fsEnd=oldSyscall.IndexOf(new byte[] {0x49,0x89,0x86,0x50,1,0,0})+7;
            int gsEnd=oldSyscall.IndexOf(new byte[] {0x49,0x89,0x86,0x58,1,0,0})+7;
            HostCheck.True(fsEnd>=7 && gsEnd>fsEnd && oldSyscall[^14..].IndexOfAnyExcept((byte)0xcc)<0);
            byte[] expectedSyscall=[..oldSyscall[..fsEnd],0x49,0x89,0x86,0x48,6,0,0,
                ..oldSyscall[fsEnd..gsEnd],0x49,0x89,0x86,0x50,6,0,0,..oldSyscall[gsEnd..^14]];
            int cleanupBranches=0;
            for (int at=0;at<expectedSyscall.Length-5;at++) if (expectedSyscall[at]==15 && expectedSyscall[at+1]==0x85)
            {
                int displacement=BitConverter.ToInt32(expectedSyscall,at+2);
                HostCheck.Equal(0xe00+14,0x600+at+6+displacement);
                BitConverter.GetBytes(displacement-14).CopyTo(expectedSyscall,at+2); cleanupBranches++;
            }
            HostCheck.Equal(2,cleanupBranches);
            HostCheck.True(new ReadOnlySpan<byte>(code+0x600,0x4f0).SequenceEqual(expectedSyscall));
            foreach (ulong root in new ulong[] { 0, plan.UserRoot, 0x500001 })
            {
                plan.KernelRoot = root; new Span<byte>(code, 4096).Fill(0xa5);
                HostCheck.True(!Transition.Emit(code, 4096, plan) && !Transition.ValidateReadBack(code, 4096, plan));
                HostCheck.True(new ReadOnlySpan<byte>(code, 4096).IndexOfAnyExcept((byte)0xa5) == -1);
            }
        }
        finally { NativeMemory.AlignedFree(code); NativeMemory.AlignedFree(legacy); NativeMemory.AlignedFree(explicitLegacy); }
    }
}
