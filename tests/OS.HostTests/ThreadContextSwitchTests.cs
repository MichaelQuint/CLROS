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
}
