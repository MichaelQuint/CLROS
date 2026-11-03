using System.Buffers.Binary;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using OS.Drivers;

internal static unsafe partial class MSIMSIXSetupInterruptDrivenVirtqueueCompletionTests
{
    [HostTest("msix.native wait handler and spurious leaves retain exact reviewed ABI", HostLane.Fast, 4)]
    private static void NativeBytes()
    {
        HostCheck.Equal(128, sizeof(MsixInterruptContext));
        string[] fields = [nameof(MsixInterruptContext.EoiAddress), nameof(MsixInterruptContext.Count), nameof(MsixInterruptContext.InterruptedRip), nameof(MsixInterruptContext.InterruptedFlags), nameof(MsixInterruptContext.EntryFlags), nameof(MsixInterruptContext.ExitFlags), nameof(MsixInterruptContext.Iterations), nameof(MsixInterruptContext.OriginalIdtr), nameof(MsixInterruptContext.TemporaryIdtr), nameof(MsixInterruptContext.RestoredIdtr), nameof(MsixInterruptContext.SpuriousCount), nameof(MsixInterruptContext.BeforeEoiIsr), nameof(MsixInterruptContext.AfterEoiIsr)];
        int[] offsets = [0, 8, 16, 24, 32, 40, 48, 56, 72, 88, 104, 112, 120];
        for (int i = 0; i < fields.Length; i++)
            HostCheck.Equal(offsets[i], Marshal.OffsetOf<MsixInterruptContext>(fields[i]).ToInt32());
        byte* code = stackalloc byte[194];
        new Span<byte>(code, 194).Fill(0xa5);
        HostCheck.True(!X64MsixInterrupt.Emit(null, 192) && !X64MsixInterrupt.Validate(null, 192));
        HostCheck.True(!X64MsixInterrupt.Emit(code + 1, 191) && !X64MsixInterrupt.Validate(code + 1, 191));
        HostCheck.True(!X64MsixInterrupt.Emit((byte*)(ulong.MaxValue - 16), 192));
        HostCheck.True(new ReadOnlySpan<byte>(code, 194).IndexOfAnyExcept((byte)0xa5) == -1);
        HostCheck.True(X64MsixInterrupt.Emit(code + 1, 192) && X64MsixInterrupt.Validate(code + 1, 192));
        // Independent instruction review: R15 is saved before SIDT/LIDT and
        // restored only after CLI/restored-IDTR capture. Handler changes only
        // saved RAX and RFLAGS (restored by IRETQ); spurious never emits EOI.
        byte[] expected = Convert.FromHexString("F30F1EFA9C5848894120A900020000755185D2744D81FA40420F0077454883790800753E" + "41574989CF410F014F38410F015F4831C0FBF39048FFC049837F0800750439D072F0FA" + "49894730410F015F38410F014F589C5849894728418B4708415FC331C0C30000000000000000000000" + "F30F1EFA50488B44240849894710488B44241849894718498B078B80C000000049894770" + "498B07C700000000008B80C00000004989477849FF47085848CF0000" + "F30F1EFA49FF476848CF000000000000");
        HostCheck.True(new ReadOnlySpan<byte>(code + 1, 192).SequenceEqual(expected) && code[0] == 0xa5 && code[193] == 0xa5);
        HostCheck.True(X64MsixInterrupt.HandlerOffset == 112 && X64MsixInterrupt.SpuriousOffset == 176 && code[1 + X64MsixInterrupt.InterruptStart] == 0xf3 && code[1 + X64MsixInterrupt.InterruptEnd] == 0xfa);
        for (int i = 0; i < 192; i++)
        {
            code[1 + i] ^= 1;
            HostCheck.True(!X64MsixInterrupt.Validate(code + 1, 192));
            code[1 + i] ^= 1;
        }
    }

    [HostTest("msix.native completion requires one ISR EOI IF boundary and restored IDTR", HostLane.Fast, 5)]
    private static void Completion()
    {
        MsixInterruptContext c = CompletionContext();
        HostCheck.True(Complete(&c));
        c.InterruptedRip = 0x100000 + 54;
        c.Iterations = 1;
        HostCheck.True(Complete(&c));
        c.InterruptedRip = 0x100000 + 70;
        c.Iterations = 1000000;
        HostCheck.True(Complete(&c));
        foreach ((int field, ulong value) in new (int, ulong)[]
        {
            (0, 0),
            (0, 0xfee000b4),
            (1, 0),
            (1, 2),
            (1, 0x100000001),
            (2, 0x100000 + 53),
            (2, 0x100000 + 71),
            (3, 2),
            (3, 0x200),
            (4, 0x202),
            (4, 0),
            (5, 0x202),
            (5, 0),
            (6, 0),
            (6, 1000001),
            (13, 1),
            (14, 0),
            (14, 3),
            (15, 1)
        }

        )
        {
            c = CompletionContext();
            ((ulong*)&c)[field] = value;
            HostCheck.True(!Complete(&c));
        }

        // Every descriptor byte is part of either its limit/base or required zero tail.
        for (int i = 56; i < 104; i++)
        {
            c = CompletionContext();
            ((byte*)&c)[i] ^= 1;
            HostCheck.True(!Complete(&c));
        }

        c = CompletionContext();
        HostCheck.True(!Complete(null) && !Complete((MsixInterruptContext*)((byte*)&c + 1)));
        foreach (uint limit in new uint[]
        {
            0,
            1000001,
            uint.MaxValue
        }

        )
            HostCheck.True(!X64MsixInterrupt.ValidateCompletion(&c, 0x100000, 0x200000, 0x300000, 0xfee00000, limit, 1));
        foreach (uint returned in new uint[]
        {
            0,
            2,
            uint.MaxValue
        }

        )
            HostCheck.True(!X64MsixInterrupt.ValidateCompletion(&c, 0x100000, 0x200000, 0x300000, 0xfee00000, 1000000, returned));
        foreach (ulong bad in new ulong[]
        {
            0,
            4095,
            0x800000000000,
            ulong.MaxValue
        }

        )
        {
            HostCheck.True(!X64MsixInterrupt.ValidateCompletion(&c, bad, 0x200000, 0x300000, 0xfee00000, 1000000, 1));
            HostCheck.True(!X64MsixInterrupt.ValidateCompletion(&c, 0x100000, bad, 0x300000, 0xfee00000, 1000000, 1));
            HostCheck.True(!X64MsixInterrupt.ValidateCompletion(&c, 0x100000, 0x200000, bad, 0xfee00000, 1000000, 1));
            HostCheck.True(!X64MsixInterrupt.ValidateCompletion(&c, 0x100000, 0x200000, 0x300000, bad, 1000000, 1));
        }

        HostCheck.True(!X64MsixInterrupt.ValidateCompletion(&c, 0x100000, 0x200000, 0x200800, 0xfee00000, 1000000, 1));
    }

    private static bool Complete(MsixInterruptContext* c) => X64MsixInterrupt.ValidateCompletion(c, 0x100000, 0x200000, 0x300000, 0xfee00000, 1000000, 1);
    private static MsixInterruptContext CompletionContext()
    {
        MsixInterruptContext c = default;
        c.EoiAddress = 0xfee000b0;
        c.Count = 1;
        c.InterruptedRip = 0x100038;
        c.InterruptedFlags = 0x202;
        c.EntryFlags = 2;
        c.ExitFlags = 2;
        c.Iterations = 10;
        c.BeforeEoiIsr = 1;
        Idtr(c.OriginalIdtr, 0x200000);
        Idtr(c.TemporaryIdtr, 0x300000);
        Idtr(c.RestoredIdtr, 0x200000);
        return c;
    }

    private static void Idtr(byte* b, ulong address)
    {
        b[0] = 255;
        b[1] = 15;
        BinaryPrimitives.WriteUInt64LittleEndian(new Span<byte>(b + 2, 8), address);
    }

    [HostTest("msix.program masks all entries and programs one physical edge vector before arming", HostLane.Fast, 6)]
    private static void Programming()
    {
        foreach (ushort entries in new ushort[]
        {
            1,
            65,
            2048
        }

        )
        {
            ProgramModel m = default;
            m.Entries = entries;
            m.Control = (ushort)(entries - 1);
            PrepareProgram(entries, out var cap, out var regions, out var common);
            var a = ProgramAccess(&m);
            var r = BoundedMsixProgramming.Prepare(cap, regions, 0x600000, common, 3, a);
            HostCheck.True(r.Status == MsixStatus.Success && r.MaskedEntries == entries && r.PendingWords == ((uint)entries + 63) / 64 * 2 && r.MessageAddress == 0xfee03000 && r.MessageData == 0xe0 && r.ConfigVector == 0xffff && r.Control == (0xc000 | (entries - 1)));
            HostCheck.True(m.Bad == 0 && m.Control == r.Control && m.Table[0] == 0xfee03000 && m.Table[1] == 0 && m.Table[2] == 0xe0 && m.Table[3] == 0);
            for (int i = 1; i < entries; i++)
                HostCheck.Equal(1U, m.Table[i * 4 + 3]);
            HostCheck.Equal(MsixStatus.Success, BoundedMsixProgramming.SetFunctionMask(cap, 0x600000, false, a));
            HostCheck.Equal((ushort)(0x8000 | (entries - 1)), m.Control);
            HostCheck.Equal(MsixStatus.Success, BoundedMsixProgramming.SetFunctionMask(cap, 0x600000, true, a));
            HostCheck.Equal(r.Control, m.Control);
            HostCheck.Equal(MsixStatus.Success, BoundedMsixProgramming.Disable(cap, 0x600000, a));
            HostCheck.Equal(cap.Control, m.Control);
            HostCheck.Equal(0U, m.Bad);
        }
    }

    [HostTest("msix.program rejects pending state malformed bindings and every uncertain access", HostLane.Fast, 7)]
    private static void ProgrammingFailures()
    {
        PrepareProgram(3, out var cap, out var regions, out var common);
        ProgramModel baseline = default;
        baseline.Entries = 3;
        baseline.Control = 2;
        HostCheck.Equal(MsixStatus.Success, BoundedMsixProgramming.Prepare(cap, regions, 0x600000, common, 3, ProgramAccess(&baseline)).Status);
        for (uint fail = 1; fail <= baseline.Calls; fail++)
        {
            ProgramModel m = default;
            m.Entries = 3;
            m.Control = 2;
            m.FailAt = fail;
            var r = BoundedMsixProgramming.Prepare(cap, regions, 0x600000, common, 3, ProgramAccess(&m));
            HostCheck.True(r.Status is MsixStatus.ReadFailed or MsixStatus.WriteFailed);
            HostCheck.True(m.Calls == fail && m.Bad == 0);
        }

        foreach (ulong corrupt in new ulong[]
        {
            0x600042,
            0x400010,
            0x80000000,
            0x80000004,
            0x80000008,
            0x8000000c
        }

        )
        {
            ProgramModel m = default;
            m.Entries = 3;
            m.Control = 2;
            m.Corrupt = corrupt;
            HostCheck.Equal(MsixStatus.ReadBackFailed, BoundedMsixProgramming.Prepare(cap, regions, 0x600000, common, 3, ProgramAccess(&m)).Status);
        }

        for (int word = 0; word < 2; word++)
        {
            ProgramModel m = default;
            m.Entries = 3;
            m.Control = 2;
            m.Pba[word] = 1;
            HostCheck.Equal(MsixStatus.PendingInterrupt, BoundedMsixProgramming.Prepare(cap, regions, 0x600000, common, 3, ProgramAccess(&m)).Status);
            HostCheck.True(m.Table[0] == 0 && m.Table[3] == 1 && m.ConfigVector == 0 && m.Bad == 0);
        }

        for (int missing = 0; missing < 4; missing++)
        {
            ProgramModel m = default;
            var a = ProgramAccess(&m);
            if (missing == 0)
                a.Read16 = null;
            else if (missing == 1)
                a.Write16 = null;
            else if (missing == 2)
                a.Read32 = null;
            else
                a.Write32 = null;
            HostCheck.Equal(MsixStatus.InvalidAccess, BoundedMsixProgramming.Prepare(cap, regions, 0x600000, common, 3, a).Status);
            HostCheck.Equal(0U, m.Calls);
        }

        ProgramModel invalid = default;
        var access = ProgramAccess(&invalid);
        HostCheck.Equal(MsixStatus.InvalidAccess, BoundedMsixProgramming.Prepare(default, regions, 0x600000, common, 3, access).Status);
        HostCheck.Equal(MsixStatus.InvalidAccess, BoundedMsixProgramming.Prepare(cap, regions, 0x600001, common, 3, access).Status);
        HostCheck.Equal(MsixStatus.InvalidAccess, BoundedMsixProgramming.Prepare(cap, regions, 0x600000, default, 3, access).Status);
        HostCheck.Equal(MsixStatus.InvalidAccess, BoundedMsixProgramming.Prepare(cap, regions, 0x600000, common, 255, access).Status);
        HostCheck.Equal(MsixStatus.InvalidAddress, BoundedMsixProgramming.Prepare(cap, default, 0x600000, common, 3, access).Status);
        HostCheck.Equal(0U, invalid.Calls);
        invalid.Entries = 3;
        invalid.Control = 2;
        HostCheck.Equal(MsixStatus.ReadBackFailed, BoundedMsixProgramming.SetFunctionMask(cap, 0x600000, false, access));
        invalid.Control = 0x8802;
        HostCheck.Equal(MsixStatus.ReadBackFailed, BoundedMsixProgramming.Disable(cap, 0x600000, access));
        invalid.Control = 0x4002;
        HostCheck.Equal(MsixStatus.Success, BoundedMsixProgramming.Disable(cap, 0x600000, access));
        HostCheck.Equal((ushort)2, invalid.Control);
    }

    [HostTest("msix.idt preserves every original gate except two reviewed handler addresses", HostLane.Fast, 8)]
    private static void TemporaryIdt()
    {
        byte* tables = (byte*)NativeMemory.AlignedAlloc(12288, 4096);
        try
        {
            byte* original = tables;
            byte* temporary = tables + 4096;
            HostCheck.True(OS.Kernel.Diagnostics.PopulateAllN256IDTVectorsOwnedGatesNever.Materialize(original, 4096, (ulong)original, 0x100000).Succeeded);
            byte[] before = new ReadOnlySpan<byte>(original, 4096).ToArray();
            HostCheck.True(X64MsixInterrupt.PrepareTemporaryIdt(original, temporary, 4096, 0x100000, 0x1012b0));
            HostCheck.True(new ReadOnlySpan<byte>(original, 4096).SequenceEqual(before));
            for (uint vector = 0; vector < 256; vector++)
            {
                byte* gate = temporary + vector * 16;
                ulong address = *(ushort*)gate | ((ulong)*(ushort*)(gate + 6) << 16) | ((ulong)*(uint*)(gate + 8) << 32);
                ulong expected = vector == 0xe0 ? 0x101320UL : vector == 255 ? 0x101360UL : 0x100000 + vector * 16;
                HostCheck.Equal(expected, address);
                foreach (int offset in new[]
                {
                    2,
                    3,
                    4,
                    5,
                    12,
                    13,
                    14,
                    15
                }

                )
                    HostCheck.Equal(before[vector * 16 + offset], gate[offset]);
            }

            // Whole-table comparison protects unmodified fault/IST gates and padding.
            for (int i = 0; i < 4096; i++)
            {
                temporary[i] ^= 1;
                HostCheck.True(!X64MsixInterrupt.ValidateTemporaryIdt(original, temporary, 4096, 0x100000, 0x1012b0));
                temporary[i] ^= 1;
            }

            byte[] saved = new ReadOnlySpan<byte>(temporary, 4096).ToArray();
            original[5] ^= 1;
            HostCheck.True(!X64MsixInterrupt.PrepareTemporaryIdt(original, temporary, 4096, 0x100000, 0x1012b0));
            HostCheck.True(new ReadOnlySpan<byte>(temporary, 4096).SequenceEqual(saved));
            original[5] ^= 1;
            HostCheck.True(!X64MsixInterrupt.PrepareTemporaryIdt(original, original, 4096, 0x100000, 0x1012b0));
            HostCheck.True(!X64MsixInterrupt.PrepareTemporaryIdt(original, original + 16, 4096, 0x100000, 0x1012b0));
            HostCheck.True(!X64MsixInterrupt.PrepareTemporaryIdt(original, temporary + 1, 4096, 0x100000, 0x1012b0));
            HostCheck.True(!X64MsixInterrupt.PrepareTemporaryIdt(original, temporary, 4095, 0x100000, 0x1012b0));
            HostCheck.True(!X64MsixInterrupt.PrepareTemporaryIdt(original, temporary, 4096, 0x100000, (ulong)temporary));
            HostCheck.True(!X64MsixInterrupt.PrepareTemporaryIdt(original, temporary, 4096, 0x100000, (ulong)original));
            HostCheck.True(!X64MsixInterrupt.PrepareTemporaryIdt(null, temporary, 4096, 0x100000, 0x1012b0));
        }
        finally
        {
            NativeMemory.AlignedFree(tables);
        }
    }

    private static void PrepareProgram(ushort entries, out MsixCapability cap, out MsixRegions regions, out VirtioMappedRegion common)
    {
        cap = Discover(Config(entries));
        PciSizedBar* bars = stackalloc PciSizedBar[6];
        Bars(bars);
        regions = Plan(cap, bars);
        var caps = VirtioPciCapabilitySet.Create(new(VirtioPciCapabilityType.CommonConfiguration, 0, 0x1000, 4096, 0), new(VirtioPciCapabilityType.NotifyConfiguration, 0, 0x2000, 4096, 4), new(VirtioPciCapabilityType.IsrStatus, 0, 0x3000, 4096, 0), new(VirtioPciCapabilityType.DeviceConfiguration, 0, 0x4000, 4096, 0));
        VirtioConfigurationRegion* declared = stackalloc VirtioConfigurationRegion[4];
        HostCheck.Equal(VirtioRegionStatus.Success, CommonNotifyISRDeviceConfigurationRegions.Plan(caps, bars, 6, declared, 4));
        HostCheck.Equal(VirtioRegionStatus.Success, CommonNotifyISRDeviceConfigurationRegions.Bind(declared[0], 0x400000, out common));
    }

    private static MsixRegisterAccess ProgramAccess(ProgramModel* m) => new()
    {
        Context = m,
        Read16 = &ProgramRead16,
        Write16 = &ProgramWrite16,
        Read32 = &ProgramRead32,
        Write32 = &ProgramWrite32
    };
    [UnmanagedCallersOnly]
    private static byte ProgramRead16(void* context, ulong address, ushort* value)
    {
        ProgramModel* m = (ProgramModel*)context;
        m->Calls++;
        if (address == 0x600042)
            *value = m->Control;
        else if (address == 0x400010)
            *value = m->ConfigVector;
        else
        {
            m->Bad++;
            return 0;
        }

        if (m->Corrupt == address)
            *value ^= 1;
        return m->Calls == m->FailAt ? (byte)0 : (byte)1;
    }

    [UnmanagedCallersOnly]
    private static byte ProgramWrite16(void* context, ulong address, uint value)
    {
        ProgramModel* m = (ProgramModel*)context;
        m->Calls++;
        if (address == 0x600042 && value <= ushort.MaxValue)
            m->Control = (ushort)value;
        else if (address == 0x400010 && value == 0xffff && (m->Control & 0xc000) == 0xc000)
            m->ConfigVector = (ushort)value;
        else
        {
            m->Bad++;
            return 0;
        }

        return m->Calls == m->FailAt ? (byte)0 : (byte)1;
    }

    [UnmanagedCallersOnly]
    private static byte ProgramRead32(void* context, ulong address, uint* value)
    {
        ProgramModel* m = (ProgramModel*)context;
        m->Calls++;
        if (address >= 0x80000000 && address < 0x80000000 + (uint)m->Entries * 16 && (address & 3) == 0)
            *value = m->Table[(address - 0x80000000) / 4];
        else if (address >= 0x80010000 && address < 0x80010000 + ((uint)m->Entries + 63) / 64 * 8 && (address & 3) == 0)
            *value = m->Pba[(address - 0x80010000) / 4];
        else
        {
            m->Bad++;
            return 0;
        }

        if (m->Corrupt == address)
            *value ^= 1;
        return m->Calls == m->FailAt ? (byte)0 : (byte)1;
    }

    [UnmanagedCallersOnly]
    private static byte ProgramWrite32(void* context, ulong address, uint value)
    {
        ProgramModel* m = (ProgramModel*)context;
        m->Calls++;
        if ((m->Control & 0xc000) != 0xc000 || address < 0x80000000 || address >= 0x80000000 + (uint)m->Entries * 16 || (address & 3) != 0)
        {
            m->Bad++;
            return 0;
        }

        uint offset = (uint)(address - 0x80000000);
        if ((offset & 15) == 12 && value == 1)
            m->Masked++;
        else if (offset <= 8 && m->Masked == m->Entries)
        {
        }
        else if (offset == 12 && value == 0 && m->Masked == m->Entries && m->ConfigVector == 0xffff && m->Table[0] == 0xfee03000 && m->Table[1] == 0 && m->Table[2] == 0xe0)
        {
        }
        else
        {
            m->Bad++;
            return 0;
        }

        m->Table[offset / 4] = value;
        return m->Calls == m->FailAt ? (byte)0 : (byte)1;
    }

    [HostTest("msix.discovery decodes disabled controls full table count and opaque capabilities", HostLane.Fast, 0)]
    private static void Discovery()
    {
        HostCheck.True(default(MsixStatus) != MsixStatus.Success && !default(MsixCapability).IsValid);
        foreach (ushort count in new ushort[]
        {
            1,
            64,
            65,
            2048
        }

        )
        {
            byte[] snapshot = Config(count, 0xfffffff8, 0x12345005, true, 0xf4);
            snapshot[0x0e] = 0x80;
            snapshot[0x34] = 0x40;
            snapshot[0x40] = 0xfe;
            snapshot[0x41] = 0xf4;
            var cap = Discover(snapshot);
            HostCheck.True(cap.IsValid && cap.CapabilityOffset == 0xf4 && cap.TableSize == count && cap.Control == 0x4000 + count - 1 && cap.TableBar == 0 && cap.TableOffset == 0xfffffff8 && cap.PbaBar == 5 && cap.PbaOffset == 0x12345000);
        }
    }

    [HostTest("msix.discovery rejects malformed disabled admission without partial metadata", HostLane.Fast, 1)]
    private static void InvalidDiscovery()
    {
        foreach ((Action<byte[]> change, MsixStatus expected) in new (Action<byte[]>, MsixStatus)[]
        {
            (b =>
            {
                b[0] = b[1] = 0xff;
            }, MsixStatus.AbsentDevice),
            (b => b[0x0e] = 1, MsixStatus.UnsupportedHeader),
            (b => b[0x0e] = 2, MsixStatus.UnsupportedHeader),
            (b => b[6] = 0, MsixStatus.MissingCapability),
            (b => b[0x34] = 0, MsixStatus.MissingCapability),
            (b => b[0x40] = 0xfe, MsixStatus.MissingCapability),
            (b => b[0x34] = 0x3c, MsixStatus.InvalidChain),
            (b => b[0x41] = 0x42, MsixStatus.InvalidChain),
            (b => b[0x41] = 0x40, MsixStatus.InvalidChain),
            (b =>
            {
                b[0x41] = 0x50;
                b[0x50] = 0xfe;
                b[0x51] = 0x40;
            }, MsixStatus.InvalidChain),
            (b =>
            {
                b[0x41] = 0x50;
                b[0x50] = 0x11;
            }, MsixStatus.DuplicateCapability),
            (b => b[0x41] = 0x44, MsixStatus.Overlap),
            (b =>
            {
                b[0x34] = 0x44;
                b[0x44] = 0xfe;
                b[0x45] = 0x40;
            }, MsixStatus.Overlap),
            (b => b[0x43] = 0x80, MsixStatus.AlreadyEnabled),
            (b => b[0x43] = 0xc0, MsixStatus.AlreadyEnabled),
            (b => b[0x43] = 8, MsixStatus.InvalidCapability),
            (b => b[0x43] = 0x10, MsixStatus.InvalidCapability),
            (b => b[0x43] = 0x20, MsixStatus.InvalidCapability),
            (b => b[0x44] = 6, MsixStatus.InvalidCapability),
            (b => b[0x48] = 7, MsixStatus.InvalidCapability)
        }

        )
        {
            byte[] snapshot = Config();
            change(snapshot);
            RejectDiscovery(snapshot, expected);
        }

        foreach (byte offset in new byte[]
        {
            0xf8,
            0xfc
        }

        )
        {
            byte[] snapshot = Config();
            snapshot[0x34] = offset;
            snapshot[offset] = 0x11;
            RejectDiscovery(snapshot, MsixStatus.TruncatedCapability);
        }

        byte[] valid = Config();
        fixed (byte* bytes = valid)
            foreach (uint length in new uint[]
            {
                0,
                255,
                257,
                uint.MaxValue
            }

            )
            {
                HostCheck.Equal(MsixStatus.InvalidStorage, MSIMSIXSetupInterruptDrivenVirtqueueCompletion.Discover(bytes, length, out var cap));
                Empty(cap);
            }

        HostCheck.Equal(MsixStatus.InvalidStorage, MSIMSIXSetupInterruptDrivenVirtqueueCompletion.Discover(null, 256, out var empty));
        Empty(empty);
        HostCheck.Equal(MsixStatus.InvalidStorage, MSIMSIXSetupInterruptDrivenVirtqueueCompletion.Discover((byte*)(ulong.MaxValue - 7), 256, out empty));
        Empty(empty);
    }

    [HostTest("msix.plan preserves BAR domains computes complete table and pending extents", HostLane.Fast, 2)]
    private static void Extents()
    {
        PciSizedBar* bars = stackalloc PciSizedBar[6];
        Bars(bars);
        foreach ((ushort count, uint tableBytes, uint pbaBytes) in new (ushort, uint, uint)[]
        {
            (1, 16, 8),
            (64, 1024, 8),
            (65, 1040, 16),
            (2048, 32768, 256)
        }

        )
        {
            var cap = Discover(Config(count, 8, 0x10000));
            var plan = Plan(cap, bars);
            HostCheck.True(plan.Table.PhysicalStart == 0x80000008 && plan.Table.PageStart == 0x80000000 && plan.Table.Bytes == tableBytes && plan.Table.PageCount == (8 + tableBytes + 4095) / 4096 && plan.Pba.PhysicalStart == 0x80010000 && plan.Pba.Bytes == pbaBytes && plan.Pba.PageCount == 1);
        }

        // Adjacent table/PBA byte extents may share a page. No 16-byte alignment is implied by BIR packing.
        var adjacent = Plan(Discover(Config(1, 8, 24)), bars);
        HostCheck.True(adjacent.Table.PageStart == adjacent.Pba.PageStart && adjacent.Pba.PhysicalStart == 0x80000018);
        HostCheck.Equal(PciBarProbeStatus.Success, BARPrefetchTypeRegressionTestsDerivedMOSAFailures.Decode(4, 0xc0, 0xfffe0004, 0xffffffff, 2, out bars[2]));
        Unsafe.AsRef(in bars[3].Kind) = PciSizedBarKind.UpperHalf;
        var separate = Plan(Discover(Config(65, 0, 0x1fff2)), bars);
        HostCheck.True(separate.Table.PhysicalStart == 0x80000000 && separate.Pba.PhysicalStart == 0xc00001fff0 && separate.Pba.Bytes == 16 && separate.Pba.PageCount == 1);
        // Widen before addition: a table may cross the 32-bit offset boundary in a large 64-bit BAR.
        HostCheck.Equal(PciBarProbeStatus.Success, BARPrefetchTypeRegressionTestsDerivedMOSAFailures.Decode(4, 2, 4, 0xfffffffe, 2, out bars[2]));
        var cross = Plan(Discover(Config(2, 0xfffffffa, 0)), bars);
        HostCheck.True(cross.Table.PhysicalStart == 0x2fffffff8 && cross.Table.Bytes == 32 && cross.Table.PageCount == 2);
    }

    [HostTest("msix.plan rejects invalid BAR pairs bounds overlap storage and default atomically", HostLane.Fast, 3)]
    private static void InvalidPlans()
    {
        PciSizedBar* bars = stackalloc PciSizedBar[6];
        Bars(bars);
        var cap = Discover(Config());
        RejectPlan(default, bars, MsixStatus.InvalidCapability);
        RejectPlan(cap, null, MsixStatus.InvalidStorage);
        RejectPlan(cap, bars, MsixStatus.InvalidStorage, 5);
        RejectPlan(cap, (PciSizedBar*)((byte*)bars + 1), MsixStatus.InvalidStorage);
        RejectPlan(cap, (PciSizedBar*)(ulong.MaxValue - 7), MsixStatus.InvalidStorage);
        RejectPlan(Discover(Config(1, 0x1fff8, 0x10000)), bars, MsixStatus.OutsideBar);
        RejectPlan(Discover(Config(65, 0, 0x1fff8)), bars, MsixStatus.OutsideBar);
        RejectPlan(Discover(Config(1, 0xfffffff8, 0x10000)), bars, MsixStatus.OutsideBar);
        RejectPlan(Discover(Config(2, 0, 24)), bars, MsixStatus.Overlap);
        RejectPlan(Discover(Config(1, 16, 16)), bars, MsixStatus.Overlap);
        RejectPlan(Discover(Config(1, 1, 0x10000)), bars, MsixStatus.InvalidBar);
        // Deliberately malformed POD metadata: callers cannot evade pair and arithmetic checks.
        foreach (PciSizedBarKind kind in new[]
        {
            PciSizedBarKind.Uninitialized,
            PciSizedBarKind.Absent,
            PciSizedBarKind.Io32,
            PciSizedBarKind.UpperHalf
        }

        )
        {
            Bars(bars);
            Unsafe.AsRef(in bars[0].Kind) = kind;
            RejectPlan(cap, bars, MsixStatus.InvalidBar);
        }

        Bars(bars);
        Unsafe.AsRef(in bars[0].Bytes) = 0;
        RejectPlan(cap, bars, MsixStatus.InvalidBar);
        Bars(bars);
        Unsafe.AsRef(in bars[0].Bytes) = 48;
        RejectPlan(cap, bars, MsixStatus.InvalidBar);
        Bars(bars);
        Unsafe.AsRef(in bars[0].Base) = 0x80000008;
        RejectPlan(cap, bars, MsixStatus.InvalidBar);
        Bars(bars);
        Unsafe.AsRef(in bars[0].Base) = 0;
        RejectPlan(cap, bars, MsixStatus.InvalidBar);
        Bars(bars);
        Unsafe.AsRef(in bars[0].Base) = 0x100000000;
        RejectPlan(cap, bars, MsixStatus.InvalidBar);
        Bars(bars);
        Unsafe.AsRef(in bars[0].Base) = 0x80000;
        RejectPlan(cap, bars, MsixStatus.InvalidAddress);
        Bars(bars);
        Unsafe.AsRef(in bars[0].Kind) = PciSizedBarKind.Memory64;
        RejectPlan(cap, bars, MsixStatus.InvalidBar);
        Unsafe.AsRef(in bars[1].Kind) = PciSizedBarKind.UpperHalf;
        Unsafe.AsRef(in bars[0].Base) = 0x0000800000000000;
        RejectPlan(cap, bars, MsixStatus.InvalidAddress);
        Unsafe.AsRef(in bars[0].Base) = 0xfffffffffffe0000;
        RejectPlan(cap, bars, MsixStatus.InvalidBar);
        Bars(bars);
        bars[2] = bars[0];
        RejectPlan(Discover(Config(1, 0, 0x10002)), bars, MsixStatus.Overlap);
        Unsafe.AsRef(in bars[2].Kind) = PciSizedBarKind.Memory64;
        Unsafe.AsRef(in bars[3].Kind) = PciSizedBarKind.UpperHalf;
        RejectPlan(Discover(Config(1, 3, 0x10000)), bars, MsixStatus.InvalidBar);
        bars[3] = bars[0];
        RejectPlan(Discover(Config(1, 3, 0x10000)), bars, MsixStatus.InvalidBar);
        bars[5] = bars[2];
        RejectPlan(Discover(Config(1, 5, 0x10000)), bars, MsixStatus.InvalidBar);
    }

    private static byte[] Config(ushort count = 1, uint table = 0, uint pba = 0x10000, bool mask = false, byte offset = 0x40)
    {
        byte[] b = new byte[256];
        b[0] = 0x34;
        b[1] = 0x12;
        b[6] = 0x10;
        b[0x34] = offset;
        b[offset] = 0x11;
        BinaryPrimitives.WriteUInt16LittleEndian(b.AsSpan(offset + 2), (ushort)((count - 1) | (mask ? 0x4000 : 0)));
        BinaryPrimitives.WriteUInt32LittleEndian(b.AsSpan(offset + 4), table);
        BinaryPrimitives.WriteUInt32LittleEndian(b.AsSpan(offset + 8), pba);
        return b;
    }

    private static MsixCapability Discover(byte[] snapshot)
    {
        byte[] before = (byte[])snapshot.Clone();
        fixed (byte* bytes = snapshot)
        {
            HostCheck.Equal(MsixStatus.Success, MSIMSIXSetupInterruptDrivenVirtqueueCompletion.Discover(bytes, 256, out var cap));
            HostCheck.True(snapshot.AsSpan().SequenceEqual(before));
            return cap;
        }
    }

    private static void RejectDiscovery(byte[] snapshot, MsixStatus expected)
    {
        var cap = Discover(Config());
        byte[] before = (byte[])snapshot.Clone();
        fixed (byte* bytes = snapshot)
            HostCheck.Equal(expected, MSIMSIXSetupInterruptDrivenVirtqueueCompletion.Discover(bytes, 256, out cap));
        Empty(cap);
        HostCheck.True(snapshot.AsSpan().SequenceEqual(before));
    }

    private static void Empty(MsixCapability cap) => HostCheck.True(!cap.IsValid && cap.CapabilityOffset == 0 && cap.Control == 0 && cap.TableSize == 0 && cap.TableBar == 0 && cap.TableOffset == 0 && cap.PbaBar == 0 && cap.PbaOffset == 0);
    private static void Bars(PciSizedBar* bars)
    {
        new Span<PciSizedBar>(bars, 6).Clear();
        HostCheck.Equal(PciBarProbeStatus.Success, BARPrefetchTypeRegressionTestsDerivedMOSAFailures.Decode(0x80000000, 0, 0xfffe0000, 0, 0, out bars[0]));
    }

    private static MsixRegions Plan(MsixCapability cap, PciSizedBar* bars)
    {
        byte[] before = new ReadOnlySpan<byte>(bars, 6 * sizeof(PciSizedBar)).ToArray();
        HostCheck.Equal(MsixStatus.Success, MSIMSIXSetupInterruptDrivenVirtqueueCompletion.Plan(cap, bars, 6, out var regions));
        HostCheck.True(new ReadOnlySpan<byte>(bars, before.Length).SequenceEqual(before));
        return regions;
    }

    private static void RejectPlan(MsixCapability cap, PciSizedBar* bars, MsixStatus expected, uint count = 6)
    {
        PciSizedBar* valid = stackalloc PciSizedBar[6];
        Bars(valid);
        var regions = Plan(Discover(Config()), valid);
        HostCheck.Equal(expected, MSIMSIXSetupInterruptDrivenVirtqueueCompletion.Plan(cap, bars, count, out regions));
        HostCheck.True(regions.Table.PhysicalStart == 0 && regions.Table.PageStart == 0 && regions.Table.Bytes == 0 && regions.Table.PageCount == 0 && regions.Pba.PhysicalStart == 0 && regions.Pba.PageStart == 0 && regions.Pba.Bytes == 0 && regions.Pba.PageCount == 0);
    }
}
