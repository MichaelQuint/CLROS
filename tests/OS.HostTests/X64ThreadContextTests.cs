using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using OS.Kernel.Threading;

internal static unsafe partial class X64ThreadContextTests
{
    private static X64ThreadContext.Status Init(X64ThreadContext* value, uint mask = 0xffbf) => X64ThreadContext.Initialize(value, 688, 0x400100, 0x70000108, mask);
    [HostTest("m09.001", HostLane.Fast, 0)]
    private static void Geometry()
    {
        HostCheck.Equal(688, sizeof(X64ThreadContext));
        HostCheck.Equal(688, Marshal.SizeOf<X64ThreadContext>());
        HostCheck.True(!RuntimeHelpers.IsReferenceOrContainsReferences<X64ThreadContext>());
        string[] names = ["Rax", "Rbx", "Rcx", "Rdx", "Rsi", "Rdi", "Rbp", "Rsp", "R8", "R9", "R10", "R11", "R12", "R13", "R14", "R15", "Rip", "Rflags", "FsBase", "UserGsBase"];
        for (int i = 0; i < names.Length; i++)
        {
            HostCheck.Equal(16 + i * 8, (int)Marshal.OffsetOf<X64ThreadContext>(names[i]));
            HostCheck.True(typeof(X64ThreadContext).GetField(names[i])!.FieldType == typeof(ulong));
        }

        HostCheck.Equal(0, (int)Marshal.OffsetOf<X64ThreadContext>("_version"));
        HostCheck.Equal(8, (int)Marshal.OffsetOf<X64ThreadContext>("_reserved"));
        HostCheck.Equal(176, (int)Marshal.OffsetOf<X64ThreadContext>("FxSave"));
        uint[] offsets = [X64ThreadContext.RaxOffset, X64ThreadContext.RbxOffset, X64ThreadContext.RcxOffset, X64ThreadContext.RdxOffset, X64ThreadContext.RsiOffset, X64ThreadContext.RdiOffset, X64ThreadContext.RbpOffset, X64ThreadContext.RspOffset, X64ThreadContext.R8Offset, X64ThreadContext.R9Offset, X64ThreadContext.R10Offset, X64ThreadContext.R11Offset, X64ThreadContext.R12Offset, X64ThreadContext.R13Offset, X64ThreadContext.R14Offset, X64ThreadContext.R15Offset, X64ThreadContext.RipOffset, X64ThreadContext.RflagsOffset, X64ThreadContext.FsBaseOffset, X64ThreadContext.UserGsBaseOffset];
        HostCheck.True(offsets.SequenceEqual(Enumerable.Range(0, 20).Select(i => (uint)(16 + i * 8))));
        HostCheck.Equal(176U, X64ThreadContext.FxSaveOffset);
        HostCheck.Equal(512U, X64ThreadContext.FxSaveBytes);
        HostCheck.Equal(16U, X64ThreadContext.Alignment);
        HostCheck.Equal(688U, X64ThreadContext.Bytes);
        HostCheck.Equal(24U, X64ThreadContext.FxMxcsrOffset);
        HostCheck.Equal(28U, X64ThreadContext.FxMxcsrMaskOffset);
        HostCheck.Equal(32U, X64ThreadContext.FxSt0Offset);
        HostCheck.Equal(160U, X64ThreadContext.FxXmm0Offset);
    }

    [HostTest("m09.001 initialization writes legal controls complete marker and only bounded storage", HostLane.Fast, 1)]
    private static void Initialization()
    {
        using Owner owner = new();
        foreach (uint mask in new uint[]
        {
            0,
            0xffbf,
            0xffff
        }

        )
        {
            HostCheck.Equal(X64ThreadContext.Status.Ok, Init(owner.First, mask));
            HostCheck.True(owner.First->IsInitialized);
            HostCheck.Equal(X64ThreadContext.Status.Ok, X64ThreadContext.Validate(owner.First, 688, mask));
            HostCheck.Equal(0UL, (ulong)owner.First->FxSave & 15);
            HostCheck.Equal((ushort)0x027f, *(ushort*)owner.First->FxSave);
            HostCheck.Equal(0x1f80U, *(uint*)(owner.First->FxSave + 24));
            HostCheck.Equal(mask, *(uint*)(owner.First->FxSave + 28));
            byte[] expected = new byte[688];
            fixed (byte* p = expected)
            {
                *(ulong*)p = 1;
                *(ulong*)(p + 72) = 0x70000108;
                *(ulong*)(p + 144) = 0x400100;
                *(ulong*)(p + 152) = 2;
                *(ushort*)(p + 176) = 0x027f;
                *(uint*)(p + 200) = 0x1f80;
                *(uint*)(p + 204) = mask;
            }

            HostCheck.True(new ReadOnlySpan<byte>(owner.First, 688).SequenceEqual(expected));
            HostCheck.True(new ReadOnlySpan<byte>(owner.Buffer, 16).ToArray().All(b => b == 0xa5));
            HostCheck.True(new ReadOnlySpan<byte>(owner.Buffer + 704, 4096 - 704).ToArray().All(b => b == 0xa5));
        }
    }

    [HostTest("m09.001 full GPR x87 MMX and sixteen XMM sentinel snapshots copy including overlap", HostLane.Fast, 2)]
    private static void RoundTrips()
    {
        using Owner owner = new();
        HostCheck.Equal(X64ThreadContext.Status.Ok, Init(owner.First));
        ulong* registers = (ulong*)((byte*)owner.First + 16);
        for (int i = 0; i < 16; i++)
            registers[i] = 0x100000UL + (ulong)i * 0x1110;
        owner.First->Rip = 0xffff800000001000;
        owner.First->Rflags = 0x202;
        owner.First->FsBase = 0x70001234;
        owner.First->UserGsBase = 0xffff800000002345;
        for (int i = 32; i < 512; i++)
            owner.First->FxSave[i] = (byte)(i * 37 + 11);
        byte[] snapshot = new ReadOnlySpan<byte>(owner.First, 688).ToArray();
        HostCheck.Equal(X64ThreadContext.Status.Ok, X64ThreadContext.Copy(owner.Second, 688, owner.First, 688, 0xffbf));
        HostCheck.True(new ReadOnlySpan<byte>(owner.Second, 688).SequenceEqual(snapshot));
        HostCheck.Equal(X64ThreadContext.Status.Ok, X64ThreadContext.Copy(owner.First, 688, owner.First, 688, 0xffbf));
        HostCheck.True(new ReadOnlySpan<byte>(owner.First, 688).SequenceEqual(snapshot));
        X64ThreadContext* overlap = (X64ThreadContext*)((byte*)owner.First + 16);
        HostCheck.Equal(X64ThreadContext.Status.Ok, X64ThreadContext.Copy(overlap, 688, owner.First, 688, 0xffbf));
        HostCheck.True(new ReadOnlySpan<byte>(overlap, 688).SequenceEqual(snapshot));
    }

    [HostTest("m09.001 malformed borrowed ranges reject before access and preserve complete destination", HostLane.Fast, 3)]
    private static void Buffers()
    {
        using Owner owner = new();
        HostCheck.Equal(X64ThreadContext.Status.Ok, Init(owner.First));
        foreach ((ulong Address, uint Bytes) bad in new (ulong, uint)[]
        {
            (0, 688),
            ((ulong)owner.Second + 1, 688),
            ((ulong)owner.Second, 687),
            ((ulong)owner.Second, 0),
            (0xfffffffffffffff0, 688),
            (0x0000800000000000, 688),
            (0x00007fffffffff00, 688)
        }

        )
        {
            byte[] before = owner.Snapshot();
            X64ThreadContext* pointer = (X64ThreadContext*)bad.Address;
            HostCheck.True(X64ThreadContext.Initialize(pointer, bad.Bytes, 0x400100, 0x70000108, 0xffbf) != X64ThreadContext.Status.Ok);
            HostCheck.True(X64ThreadContext.Validate(pointer, bad.Bytes, 0xffbf) != X64ThreadContext.Status.Ok);
            HostCheck.True(X64ThreadContext.Copy(pointer, bad.Bytes, owner.First, 688, 0xffbf) != X64ThreadContext.Status.Ok);
            HostCheck.True(X64ThreadContext.Copy(owner.Second, 688, pointer, bad.Bytes, 0xffbf) != X64ThreadContext.Status.Ok);
            HostCheck.True(owner.Snapshot().SequenceEqual(before));
        }
    }

    [HostTest("m09.001 every declared canonical address and null policy is independently checked", HostLane.Fast, 4)]
    private static void Addresses()
    {
        using Owner owner = new();
        foreach (int offset in new[]
        {
            144,
            72,
            160,
            168
        }

        )
            foreach (ulong address in new ulong[]
            {
                0,
                1,
                0x7fffffffffff,
                0xffff800000000000,
                ulong.MaxValue,
                0x800000000000,
                0xffff7fffffffffff,
                0x1000000000000
            }

            )
            {
                HostCheck.Equal(X64ThreadContext.Status.Ok, Init(owner.First));
                *(ulong*)((byte*)owner.First + offset) = address;
                bool canonical = address is 0 or 1 or 0x7fffffffffff or 0xffff800000000000 or ulong.MaxValue;
                bool accepted = canonical && (address != 0 || offset is 160 or 168);
                byte[] before = owner.Snapshot();
                var status = X64ThreadContext.Validate(owner.First, 688, 0xffbf);
                HostCheck.True((status == X64ThreadContext.Status.Ok) == accepted);
                HostCheck.True(owner.Snapshot().SequenceEqual(before));
                HostCheck.True((X64ThreadContext.Copy(owner.Second, 688, owner.First, 688, 0xffbf) == X64ThreadContext.Status.Ok) == accepted);
                if (!accepted)
                    HostCheck.True(owner.Snapshot().SequenceEqual(before));
                HostCheck.Equal(X64ThreadContext.Status.Ok, Init(owner.First));
                before = owner.Snapshot();
                var initialized = X64ThreadContext.Initialize(owner.Second, 688, offset == 144 ? address : 0x400100, offset == 72 ? address : 0x70000108, 0xffbf, 2, offset == 160 ? address : 0, offset == 168 ? address : 0);
                HostCheck.True((initialized == X64ThreadContext.Status.Ok) == accepted);
                if (!accepted)
                    HostCheck.True(owner.Snapshot().SequenceEqual(before));
            }
    }

    [HostTest("m09.001 all flag bits reject privilege reserved and virtual state without mutation", HostLane.Fast, 5)]
    private static void Flags()
    {
        using Owner owner = new();
        const ulong independentlyExpected = 0x00250fd7;
        HostCheck.Equal(independentlyExpected, X64ThreadContext.AllowedRflags);
        for (int bit = 0; bit < 64; bit++)
        {
            ulong flags = 2 | (1UL << bit);
            bool valid = (independentlyExpected & (1UL << bit)) != 0;
            HostCheck.Equal(X64ThreadContext.Status.Ok, Init(owner.First));
            owner.First->Rflags = flags;
            byte[] before = owner.Snapshot();
            HostCheck.True((X64ThreadContext.Validate(owner.First, 688, 0xffbf) == X64ThreadContext.Status.Ok) == valid);
            HostCheck.True(owner.Snapshot().SequenceEqual(before));
            HostCheck.True((X64ThreadContext.Initialize(owner.Second, 688, 0x400100, 0x70000108, 0xffbf, flags) == X64ThreadContext.Status.Ok) == valid);
            if (!valid)
                HostCheck.True(owner.Snapshot().SequenceEqual(before));
        }

        HostCheck.Equal(X64ThreadContext.Status.Ok, Init(owner.First));
        owner.First->Rflags = 0;
        HostCheck.Equal(X64ThreadContext.Status.InvalidFlags, X64ThreadContext.Validate(owner.First, 688, 0xffbf));
    }

    [HostTest("m09.001 externally trusted MXCSR mask rejects image widening DAZ and reserved bits", HostLane.Fast, 6)]
    private static void FpMask()
    {
        using Owner owner = new();
        foreach (uint observed in new uint[]
        {
            0,
            0xffbf,
            0xffff
        }

        )
        {
            HostCheck.Equal(X64ThreadContext.Status.Ok, Init(owner.First, observed));
            *(uint*)(owner.First->FxSave + 24) = 0x1fc0;
            HostCheck.True((X64ThreadContext.Validate(owner.First, 688, observed) == X64ThreadContext.Status.Ok) == (observed == 0xffff));
            *(uint*)(owner.First->FxSave + 24) = 0x80001f80;
            byte[] before = owner.Snapshot();
            HostCheck.Equal(X64ThreadContext.Status.InvalidFpState, X64ThreadContext.Copy(owner.Second, 688, owner.First, 688, observed));
            HostCheck.True(owner.Snapshot().SequenceEqual(before));
        }

        foreach (uint invalid in new uint[]
        {
            1,
            0xff7f,
            0x10000,
            0xffffffff
        }

        )
        {
            byte[] before = owner.Snapshot();
            HostCheck.Equal(X64ThreadContext.Status.InvalidMxcsrMask, X64ThreadContext.Initialize(owner.Second, 688, 0x400100, 0x70000108, invalid));
            HostCheck.True(owner.Snapshot().SequenceEqual(before));
        }

        HostCheck.Equal(X64ThreadContext.Status.Ok, Init(owner.First));
        *(uint*)(owner.First->FxSave + 28) = 0xffff;
        HostCheck.Equal(X64ThreadContext.Status.InvalidFpState, X64ThreadContext.Validate(owner.First, 688, 0xffbf));
    }

    [HostTest("m09.001 incomplete or corrupted completion metadata never becomes a copied context", HostLane.Fast, 7)]
    private static void Completion()
    {
        using Owner owner = new();
        *owner.First = default;
        HostCheck.True(!owner.First->IsInitialized);
        HostCheck.Equal(X64ThreadContext.Status.Uninitialized, X64ThreadContext.Validate(owner.First, 688, 0xffbf));
        foreach ((int Offset, ulong Value) bad in new (int, ulong)[]
        {
            (0, 0),
            (0, 2),
            (8, 1)
        }

        )
        {
            HostCheck.Equal(X64ThreadContext.Status.Ok, Init(owner.First));
            *(ulong*)((byte*)owner.First + bad.Offset) = bad.Value;
            byte[] before = owner.Snapshot();
            HostCheck.Equal(X64ThreadContext.Status.Uninitialized, X64ThreadContext.Copy(owner.Second, 688, owner.First, 688, 0xffbf));
            HostCheck.True(owner.Snapshot().SequenceEqual(before));
        }
    }

    [HostTest("m09.001 borrowed context operations perform no hidden managed allocation", HostLane.Fast, 8)]
    private static void Allocation()
    {
        using Owner owner = new();
        _ = Init(owner.First);
        _ = X64ThreadContext.Validate(owner.First, 688, 0xffbf);
        _ = X64ThreadContext.Copy(owner.Second, 688, owner.First, 688, 0xffbf);
        long before = GC.GetAllocatedBytesForCurrentThread();
        var initialized = Init(owner.First);
        var valid = X64ThreadContext.Validate(owner.First, 688, 0xffbf);
        var copied = X64ThreadContext.Copy(owner.Second, 688, owner.First, 688, 0xffbf);
        long allocated = GC.GetAllocatedBytesForCurrentThread() - before;
        HostCheck.True(initialized == X64ThreadContext.Status.Ok && valid == X64ThreadContext.Status.Ok && copied == X64ThreadContext.Status.Ok);
        HostCheck.Equal(0L, allocated);
    }
}
