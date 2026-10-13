using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using OS.Kernel.Threading;

internal static unsafe class ThreadContextSwitchTests
{
    private sealed class Buffer : IDisposable
    {
        public readonly byte* Bytes = (byte*)NativeMemory.AlignedAlloc(4096, 4096);
        public X64ThreadContext* First => (X64ThreadContext*)Bytes;
        public X64ThreadContext* Second => (X64ThreadContext*)(Bytes + 1024);
        public Buffer() => new Span<byte>(Bytes, 4096).Fill(0xa5);
        public void Dispose() => NativeMemory.AlignedFree(Bytes);
        public byte[] Snapshot() => new ReadOnlySpan<byte>(Bytes, 4096).ToArray();
    }
    private static ThreadContextSwitch.Owner Owner(Buffer buffer) => new()
    {
        Initialized = true, GuardsVerified = true, MappingsVerified = true,
        Key = 1, CurrentKey = 1, Root = 0x1000, CurrentRoot = 0x1000,
        Cr0 = 0x80000001, Cr4 = 0x220, Efer = 0xd00, Flags = 2,
        CpuCount = 1, BasicEdx = 0x07000000, ExtendedEdx = 0x20100000, ObservedMxcsrMask = 0xffbf,
        Cs = 8, Ss = 16, Tr = 24,
        ContextArena = (ulong)buffer.Bytes, ContextArenaBytes = 4096,
        CodeStart = 0x400000, CodeBytes = 4096, SwitchStackGuardStart = 0x800000, SwitchStackBytes = 3 * 4096
    };
    private static ThreadContextSwitch.Descriptor Descriptor(X64ThreadContext* context, uint slot, ulong stack,
        ThreadContextSwitch.ResumeKind kind = ThreadContextSwitch.ResumeKind.CapturedCall)
    {
        ulong rip = 0x400040 + slot * 16;
        ulong rsp = stack + 8192 - (kind == ThreadContextSwitch.ResumeKind.Entry ? 40UL : 64UL);
        HostCheck.Equal(X64ThreadContext.Status.Ok, X64ThreadContext.Initialize(context, 688, rip, rsp, 0xffbf));
        return new() { Identity = (1UL << 32) | slot, CurrentIdentity = (1UL << 32) | slot,
            Context = context, ContextBytes = 688, StackGuardStart = stack, StackBytes = 12288, ResumeRip = rip, Kind = kind };
    }
    private static void Reject(ThreadContextSwitch.Status expected, ThreadContextSwitch.Owner owner,
        ThreadContextSwitch.Descriptor first, ThreadContextSwitch.Descriptor second, Buffer buffer)
    {
        byte[] before = buffer.Snapshot();
        HostCheck.Equal(expected, ThreadContextSwitch.Admit(owner, first, second));
        HostCheck.True(before.SequenceEqual(buffer.Snapshot()));
    }

    [HostTest("m09.004", HostLane.Fast, 0)]
    private static void Valid()
    {
        HostCheck.True(!RuntimeHelpers.IsReferenceOrContainsReferences<ThreadContextSwitch.Owner>() &&
            !RuntimeHelpers.IsReferenceOrContainsReferences<ThreadContextSwitch.Descriptor>());
        using Buffer buffer = new(); var owner = Owner(buffer);
        var first = Descriptor(buffer.First, 1, 0x900000);
        var second = Descriptor(buffer.Second, 2, 0xa00000, ThreadContextSwitch.ResumeKind.Entry);
        HostCheck.Equal(ThreadContextSwitch.Status.Ok, ThreadContextSwitch.Admit(owner, first, second));
        HostCheck.Equal(ThreadContextSwitch.Status.Ok, ThreadContextSwitch.Admit(owner, second, first));
        buffer.Second->Rsp = 0xa01fc0; second.Kind = ThreadContextSwitch.ResumeKind.CapturedCall;
        HostCheck.Equal(ThreadContextSwitch.Status.Ok, ThreadContextSwitch.Admit(owner, first, second));
    }

    [HostTest("m09.004 admission default wrong owner missing proof and wrong root reject before access", HostLane.Fast, 1)]
    private static void Ownership()
    {
        using Buffer buffer = new(); var owner = Owner(buffer);
        Reject(ThreadContextSwitch.Status.Uninitialized, default, default, default, buffer);
        foreach (int field in new[] { 0, 1, 2, 3, 4, 5 })
        {
            var invalid = owner;
            if (field == 0) invalid.Key = 0;
            if (field == 1) invalid.CurrentKey = 2;
            if (field == 2) invalid.GuardsVerified = false;
            if (field == 3) invalid.MappingsVerified = false;
            if (field == 4) invalid.Root = 0x1001;
            if (field == 5) invalid.CurrentRoot = 0x2000;
            Reject(field < 4 ? ThreadContextSwitch.Status.InvalidOwner : ThreadContextSwitch.Status.InvalidRoot,
                invalid, default, default, buffer);
        }
    }

    [HostTest("m09.004 admission unsupported CPU selectors kernel GS extended state and IRQ flags", HostLane.Fast, 2)]
    private static void Cpu()
    {
        using Buffer buffer = new(); var owner = Owner(buffer);
        for (int field = 0; field < 17; field++)
        {
            var invalid = owner;
            switch (field)
            {
                case 0: invalid.CpuCount = 2; break;
                case 1: invalid.Cs = 16; break;
                case 2: invalid.Fs = 8; break;
                case 3: invalid.Gs = 8; break;
                case 4: invalid.Tr = 0; break;
                case 5: invalid.CurrentKernelGs = 1; break;
                case 6: invalid.Cr0 |= 8; break;
                case 7: invalid.Cr4 |= 1UL << 18; break;
                case 8: invalid.Cr4 &= ~0x200UL; break;
                case 9: invalid.Efer &= ~0x800UL; break;
                case 10: invalid.BasicEdx = 0; break;
                case 11: invalid.ExtendedEdx = 0; break;
                case 12: invalid.Flags |= 0x200; break;
                case 13: invalid.Flags |= 0x100; break;
                case 14: invalid.Flags = 0; break;
                case 15: invalid.ObservedMxcsrMask = 0x10000; break;
                case 16: invalid.ObservedMxcsrMask = 1; break;
            }
            Reject(ThreadContextSwitch.Status.UnsupportedCpu, invalid, default, default, buffer);
        }
    }

    [HostTest("m09.004 admission malformed alias and overflow extents are rejected without dereference", HostLane.Fast, 3)]
    private static void Ranges()
    {
        using Buffer buffer = new(); var owner = Owner(buffer);
        var first = Descriptor(buffer.First, 1, 0x900000); var second = Descriptor(buffer.Second, 2, 0xa00000);
        var invalidOwner = owner; invalidOwner.CodeStart = ulong.MaxValue - 4095;
        invalidOwner.CodeBytes = 8192;
        Reject(ThreadContextSwitch.Status.InvalidRange, invalidOwner, first, second, buffer);
        invalidOwner = owner; invalidOwner.SwitchStackGuardStart = owner.CodeStart;
        Reject(ThreadContextSwitch.Status.Overlap, invalidOwner, first, second, buffer);
        var invalid = second; invalid.Context = (X64ThreadContext*)1;
        Reject(ThreadContextSwitch.Status.InvalidContext, owner, first, invalid, buffer);
        invalid = second; invalid.ContextBytes = 687;
        Reject(ThreadContextSwitch.Status.InvalidContext, owner, first, invalid, buffer);
        invalid = second; invalid.Context = first.Context;
        Reject(ThreadContextSwitch.Status.Overlap, owner, first, invalid, buffer);
        invalid = second; invalid.StackGuardStart = first.StackGuardStart;
        Reject(ThreadContextSwitch.Status.Overlap, owner, first, invalid, buffer);
        invalid = second; invalid.StackGuardStart = owner.CodeStart;
        Reject(ThreadContextSwitch.Status.Overlap, owner, first, invalid, buffer);
    }

    [HostTest("m09.004 admission invalid stale and same-slot identities reject complete contexts", HostLane.Fast, 4)]
    private static void Generations()
    {
        using Buffer buffer = new(); var owner = Owner(buffer);
        var first = Descriptor(buffer.First, 1, 0x900000); var second = Descriptor(buffer.Second, 2, 0xa00000);
        foreach (ulong tag in new ulong[] { 0, 1, 1UL << 32 })
        {
            var invalid = second; invalid.Identity = tag; invalid.CurrentIdentity = tag;
            Reject(ThreadContextSwitch.Status.InvalidIdentity, owner, first, invalid, buffer);
        }
        var stale = second; stale.CurrentIdentity += 1UL << 32;
        Reject(ThreadContextSwitch.Status.StaleIdentity, owner, first, stale, buffer);
        stale = second; stale.Identity = (2UL << 32) | 1; stale.CurrentIdentity = stale.Identity;
        Reject(ThreadContextSwitch.Status.StaleIdentity, owner, first, stale, buffer);
    }

    [HostTest("m09.004 admission exact instruction full completion FP and strict flags precede transfer", HostLane.Fast, 5)]
    private static void Context()
    {
        using Buffer buffer = new(); var owner = Owner(buffer);
        var first = Descriptor(buffer.First, 1, 0x900000); var second = Descriptor(buffer.Second, 2, 0xa00000);
        ulong rip = buffer.Second->Rip;
        buffer.Second->Rip++;
        Reject(ThreadContextSwitch.Status.InvalidContinuation, owner, first, second, buffer);
        buffer.Second->Rip = rip;
        foreach (ulong flags in new ulong[] { 0x102, 0x202, 0x10002, 0x40002 })
        {
            buffer.Second->Rflags = flags;
            Reject(ThreadContextSwitch.Status.InvalidFlags, owner, first, second, buffer);
        }
        buffer.Second->Rflags = 0x402;
        HostCheck.Equal(ThreadContextSwitch.Status.Ok, ThreadContextSwitch.Admit(owner, first, second));
        *(ulong*)buffer.Second = 0;
        Reject(ThreadContextSwitch.Status.InvalidContext, owner, first, second, buffer);
        *(ulong*)buffer.Second = 1;
        *(uint*)(buffer.Second->FxSave + 24) = 0x80000000;
        Reject(ThreadContextSwitch.Status.InvalidContext, owner, first, second, buffer);
    }

    [HostTest("m09.004 admission guard margins ABI alignment and owned TLS bases", HostLane.Fast, 6)]
    private static void StackAndTls()
    {
        using Buffer buffer = new(); var owner = Owner(buffer);
        var first = Descriptor(buffer.First, 1, 0x900000); var second = Descriptor(buffer.Second, 2, 0xa00000);
        ulong valid = buffer.Second->Rsp;
        foreach (ulong rsp in new ulong[] { 0xa01000, 0xa02000, 0xa01ff0, valid + 8 })
        {
            buffer.Second->Rsp = rsp; Reject(ThreadContextSwitch.Status.InvalidStack, owner, first, second, buffer);
        }
        buffer.Second->Rsp = valid;
        buffer.Second->FsBase = owner.CodeStart;
        Reject(ThreadContextSwitch.Status.InvalidTls, owner, first, second, buffer);
        buffer.Second->FsBase = (ulong)buffer.Bytes + 3072; buffer.Second->UserGsBase = (ulong)buffer.Bytes + 3088;
        HostCheck.Equal(ThreadContextSwitch.Status.Ok, ThreadContextSwitch.Admit(owner, first, second));
    }

    [HostTest("m09.004 admission warmed checks allocate no managed storage", HostLane.Fast, 7)]
    private static void Allocation()
    {
        using Buffer buffer = new(); var owner = Owner(buffer);
        var first = Descriptor(buffer.First, 1, 0x900000); var second = Descriptor(buffer.Second, 2, 0xa00000);
        for (int i = 0; i < 500; i++) _ = ThreadContextSwitch.Admit(owner, first, second);
        long before = GC.GetAllocatedBytesForCurrentThread(); bool passed = true;
        for (int i = 0; i < 1000; i++) passed &= ThreadContextSwitch.Admit(owner, first, second) == ThreadContextSwitch.Status.Ok;
        long allocated = GC.GetAllocatedBytesForCurrentThread() - before;
        HostCheck.True(passed); HostCheck.Equal(0L, allocated);
    }

    private static ThreadContextSwitch.StubPlan Stub(Buffer arena, out ThreadContextSwitch.Owner owner,
        out ThreadContextSwitch.Descriptor source, out ThreadContextSwitch.Descriptor destination, bool below = false)
    {
        owner = Owner(arena); owner.CodeStart = below ? (ulong)arena.Bytes - 65536 : (ulong)arena.Bytes + 65536;
        source = Descriptor(arena.First, 1, 0x900000);
        destination = Descriptor(arena.Second, 2, 0xa00000, ThreadContextSwitch.ResumeKind.Entry);
        source.ResumeRip = owner.CodeStart + 0x110; source.Context->Rip = source.ResumeRip;
        destination.ResumeRip = owner.CodeStart + 0x120; destination.Context->Rip = destination.ResumeRip;
        X64ThreadContext* helper = (X64ThreadContext*)(arena.Bytes + 2048);
        HostCheck.Equal(X64ThreadContext.Status.Ok, X64ThreadContext.Initialize(helper, 688,
            owner.CodeStart + 0x130, 0xb01fc0, owner.ObservedMxcsrMask));
        return new() { CodeOffset = 1024, AdmissionCallback = 0x2000000,
            CallbackContext = (ulong)arena.Bytes + 3072, CallbackBytes = 128,
            HelperFx = (ulong)helper->FxSave, JumpSlot = (ulong)arena.Bytes + 3200, RejectRip = owner.CodeStart + 0xe00 };
    }
    private static ulong Target(ReadOnlySpan<byte> code, ulong instructionAddress, int displacementOffset, int instructionBytes) =>
        unchecked((ulong)((long)instructionAddress + instructionBytes + BitConverter.ToInt32(code.Slice(displacementOffset, 4))));

    [HostTest("m09.004 emitter independent RIP-relative GPR FP ABI and exact jump byte vectors", HostLane.Fast, 8)]
    private static void EmitVectors()
    {
        foreach (bool below in new[] { false, true })
        {
            using Buffer arena = new(); using Buffer code = new();
            var plan = Stub(arena, out var owner, out var source, out var destination, below);
            byte[] unchanged = arena.Snapshot(); ulong address = owner.CodeStart + plan.CodeOffset;
            HostCheck.True(ThreadContextSwitch.EmitStub(code.Bytes, 4096, owner, source, destination, plan));
            HostCheck.True(ThreadContextSwitch.VerifyStub(code.Bytes, 4096, owner, source, destination, plan));
            ReadOnlySpan<byte> bytes = new(code.Bytes, 1024);
            HostCheck.True(bytes[..3].SequenceEqual(new byte[] { 0x48, 0xc7, 0x05 }));
            HostCheck.Equal((ulong)source.Context, Target(bytes, address, 3, 11));
            HostCheck.Equal(0, BitConverter.ToInt32(bytes.Slice(7, 4)));
            // Architectural register order differs from the context field order.
            uint[] offsets = { 16, 32, 40, 24, 72, 64, 48, 56, 80, 88, 96, 104, 112, 120, 128, 136 };
            for (int register = 0; register < 16; register++)
            {
                int position = 11 + register * 7;
                HostCheck.Equal(register < 8 ? (byte)0x48 : (byte)0x4c, bytes[position]);
                HostCheck.Equal((byte)0x89, bytes[position + 1]);
                HostCheck.Equal((byte)(5 | ((register & 7) << 3)), bytes[position + 2]);
                HostCheck.Equal((ulong)source.Context + offsets[register], Target(bytes[position..], address + (uint)position, 3, 7));
            }
            HostCheck.True(bytes.Slice(123, 5).SequenceEqual(new byte[] { 0x4c, 0x8d, 0x5c, 0x24, 8 }));
            HostCheck.Equal((ulong)source.Context + 72, Target(bytes[128..], address + 128, 3, 7));
            HostCheck.True(bytes.Slice(135, 4).SequenceEqual(new byte[] { 0x4c, 0x8b, 0x1c, 0x24 }));
            HostCheck.Equal((ulong)source.Context + 144, Target(bytes[139..], address + 139, 3, 7));
            int captureFp = bytes.IndexOf(new byte[] { 0x48, 0x0f, 0xae, 0x05 });
            HostCheck.True(captureFp > 146);
            HostCheck.Equal((ulong)source.Context + 176, Target(bytes[captureFp..], address + (uint)captureFp, 4, 8));
            int helper = bytes.IndexOf(new byte[] { 0xfc, 0x48, 0x0f, 0xae, 0x0d });
            HostCheck.True(helper > captureFp);
            HostCheck.Equal(plan.HelperFx, Target(bytes[(helper + 1)..], address + (uint)helper + 1, 4, 8));
            HostCheck.True(bytes.IndexOf(new byte[] { 0x48, 0x83, 0xec, 32, 0x41, 0xff, 0xd3, 0x48, 0x83, 0xc4, 32 }) > helper);
            int end = 1024; while (bytes[end - 1] == 0xcc) end--;
            HostCheck.Equal((byte)0xff, bytes[end - 6]); HostCheck.Equal((byte)0x25, bytes[end - 5]);
            HostCheck.Equal(plan.JumpSlot, Target(bytes[(end - 6)..], address + (uint)end - 6, 2, 6));
            HostCheck.Equal((byte)0x9d, bytes[end - 119]); // POPFQ followed solely by16 MOVs and indirect JMP.
            int[] restoreOrder = { 0, 1, 2, 3, 5, 6, 7, 8, 9, 10, 12, 13, 14, 15, 4, 11 };
            for (int index = 0; index < restoreOrder.Length; index++)
            {
                int register = restoreOrder[index], position = end - 118 + index * 7;
                HostCheck.Equal(register < 8 ? (byte)0x48 : (byte)0x4c, bytes[position]);
                HostCheck.Equal((byte)0x8b, bytes[position + 1]);
                HostCheck.Equal((byte)(5 | ((register & 7) << 3)), bytes[position + 2]);
                HostCheck.Equal((ulong)destination.Context + offsets[register], Target(bytes[position..], address + (uint)position, 3, 7));
            }
            HostCheck.True(bytes.Slice(end - 13, 3).SequenceEqual(new byte[] { 0x4c, 0x8b, 0x1d }));
            HostCheck.Equal((ulong)destination.Context + 104, Target(bytes[(end - 13)..], address + (uint)end - 13, 3, 7));
            HostCheck.True(bytes.Slice(end - 20, 3).SequenceEqual(new byte[] { 0x48, 0x8b, 0x25 }));
            HostCheck.Equal((ulong)destination.Context + 72, Target(bytes[(end - 20)..], address + (uint)end - 20, 3, 7));
            HostCheck.True(unchanged.SequenceEqual(arena.Snapshot()));
            HostCheck.True(new ReadOnlySpan<byte>(code.Bytes + 1024, 3072).ToArray().All(value => value == 0xa5));
        }
    }

    [HostTest("m09.004 emitter rejects invalid aliased incomplete and unreachable plans before output writes", HostLane.Fast, 9)]
    private static void EmitRejection()
    {
        using Buffer arena = new(); using Buffer code = new();
        var original = Stub(arena, out var owner, out var source, out var destination);
        byte[] unchanged = code.Snapshot();
        for (int field = 0; field < 10; field++)
        {
            var plan = original;
            switch (field)
            {
                case 0: plan.CallbackContext = (ulong)source.Context; break;
                case 1: plan.HelperFx += 8; break;
                case 2: plan.JumpSlot = (ulong)destination.Context; break;
                case 3: plan.CallbackBytes = 0; break;
                case 4: plan.RejectRip = owner.CodeStart + plan.CodeOffset; break;
                case 5: plan.CodeOffset = 4090; break;
                case 6: plan.AdmissionCallback = 0; break;
                case 7: plan.JumpSlot = plan.CallbackContext; break;
                case 8: plan.CallbackBytes = 1025; break;
                case 9: plan.AdmissionCallback = owner.CodeStart + plan.CodeOffset; break;
            }
            HostCheck.True(!ThreadContextSwitch.EmitStub(code.Bytes, 4096, owner, source, destination, plan));
            HostCheck.True(unchanged.SequenceEqual(code.Snapshot()));
        }
        HostCheck.True(!ThreadContextSwitch.EmitStub(code.Bytes, 1023, owner, source, destination, original));
        byte[] arenaUnchanged = arena.Snapshot();
        HostCheck.True(!ThreadContextSwitch.EmitStub(arena.Bytes, 4096, owner, source, destination, original));
        HostCheck.True(arenaUnchanged.SequenceEqual(arena.Snapshot()));
        var entrySource = source; entrySource.Kind = ThreadContextSwitch.ResumeKind.Entry;
        source.Context->Rsp -= 8;
        HostCheck.True(!ThreadContextSwitch.EmitStub(code.Bytes, 4096, owner, entrySource, destination, original));
        source.Context->Rsp += 8;
        ulong sourceRip = source.ResumeRip, destinationRip = destination.ResumeRip;
        source.ResumeRip = owner.CodeStart + original.CodeOffset; source.Context->Rip = source.ResumeRip;
        HostCheck.True(!ThreadContextSwitch.EmitStub(code.Bytes, 4096, owner, source, destination, original));
        source.ResumeRip = sourceRip; source.Context->Rip = sourceRip;
        destination.ResumeRip = owner.CodeStart + original.CodeOffset; destination.Context->Rip = destination.ResumeRip;
        HostCheck.True(!ThreadContextSwitch.EmitStub(code.Bytes, 4096, owner, source, destination, original));
        destination.ResumeRip = destinationRip; destination.Context->Rip = destinationRip;
        byte* helperFx = (byte*)original.HelperFx;
        helperFx[4] = 1;
        HostCheck.True(!ThreadContextSwitch.EmitStub(code.Bytes, 4096, owner, source, destination, original));
        helperFx[4] = 0;
        *(uint*)(helperFx + 28) ^= 1;
        HostCheck.True(!ThreadContextSwitch.EmitStub(code.Bytes, 4096, owner, source, destination, original));
        *(uint*)(helperFx + 28) ^= 1;
        HostCheck.True(unchanged.SequenceEqual(code.Snapshot()));
        foreach (bool below in new[] { false, true })
        {
            var far = owner; far.CodeStart = below ? owner.ContextArena - 0x100000000 : owner.ContextArena + 0x100000000;
            source.ResumeRip = far.CodeStart + 0x110; source.Context->Rip = source.ResumeRip;
            destination.ResumeRip = far.CodeStart + 0x120; destination.Context->Rip = destination.ResumeRip;
            var plan = original; plan.RejectRip = far.CodeStart + 0xe00;
            HostCheck.True(!ThreadContextSwitch.EmitStub(code.Bytes, 4096, far, source, destination, plan));
            HostCheck.True(unchanged.SequenceEqual(code.Snapshot()));
        }
    }

    [HostTest("m09.004 emitter compare detects changed executable and padding bytes without mutation", HostLane.Fast, 10)]
    private static void EmitIdentity()
    {
        using Buffer arena = new(); using Buffer code = new();
        var plan = Stub(arena, out var owner, out var source, out var destination);
        HostCheck.True(ThreadContextSwitch.EmitStub(code.Bytes, 4096, owner, source, destination, plan));
        foreach (int position in new[] { 0, 2, 3, 10, 11, 123, 500, 1023 })
        {
            code.Bytes[position] ^= 1; byte[] changed = code.Snapshot();
            HostCheck.True(!ThreadContextSwitch.VerifyStub(code.Bytes, 4096, owner, source, destination, plan));
            HostCheck.True(changed.SequenceEqual(code.Snapshot())); code.Bytes[position] ^= 1;
        }
        HostCheck.True(ThreadContextSwitch.VerifyStub(code.Bytes, 4096, owner, source, destination, plan));
    }

    [HostTest("m09.004 emitter warmed output and comparison allocate no managed storage", HostLane.Fast, 11)]
    private static void EmitAllocation()
    {
        using Buffer arena = new(); using Buffer code = new();
        var plan = Stub(arena, out var owner, out var source, out var destination);
        for (int i = 0; i < 100; i++) _ = ThreadContextSwitch.EmitStub(code.Bytes, 4096, owner, source, destination, plan);
        long before = GC.GetAllocatedBytesForCurrentThread(); bool passed = true;
        for (int i = 0; i < 100; i++)
        {
            passed &= ThreadContextSwitch.EmitStub(code.Bytes, 4096, owner, source, destination, plan);
            passed &= ThreadContextSwitch.VerifyStub(code.Bytes, 4096, owner, source, destination, plan);
        }
        long allocated = GC.GetAllocatedBytesForCurrentThread() - before;
        HostCheck.True(passed); HostCheck.Equal(0L, allocated);
    }
}
