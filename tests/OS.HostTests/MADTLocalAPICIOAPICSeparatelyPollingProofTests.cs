using System.Buffers.Binary;
using System.Runtime.InteropServices;
using OS.Drivers;

internal static unsafe class MADTLocalAPICIOAPICSeparatelyPollingProofTests
{
    [HostTest("madt.polling parses retained legacy topology and inactive optional records", HostLane.Fast, 0)]
    private static void Valid()
    {
        HostCheck.True(default(MadtPollingStatus) != MadtPollingStatus.Success);
        var basic = Success(Table(Cpu(), Io()));
        HostCheck.True(basic.LocalApicAddress == 0xfee00000 && basic.IoApicAddress == 0xfec00000 &&
            basic.BspApicId == 3 && basic.ProcessorUid == 7 && basic.IoApicId == 2 && basic.IoApicGsiBase == 0 &&
            basic.HeaderFlags == 1 && basic.RecordCount == 2 && basic.IsoCount == 0 && basic.NmiCount == 0 && !basic.HasLocalApicOverride);
        // NMI targets may precede processor declarations. These records are validated,
        // never enabled/routed, and the parser never follows their physical addresses.
        var optional = Success(Table(Nmi(7, 1, 15), Override(0xfed00000), Cpu(9, 4, 2),
            Iso(0, 2, 0), Cpu(), Io(), Nmi(255, 0, 0), Iso(9, 9, 15)));
        HostCheck.True(optional.LocalApicAddress == 0xfed00000 && optional.HasLocalApicOverride &&
            optional.RecordCount == 8 && optional.IsoCount == 2 && optional.NmiCount == 2);
        foreach (ushort flags in new ushort[] { 0, 1, 3, 4, 5, 7, 12, 13, 15 })
            Success(Table(Cpu(), Io(), Iso(0, 2, flags), Nmi(7, 1, flags)));
        var edges = Success(Table(Cpu(), Io(2, 0xfffff000, 16), Override(0x100000)));
        HostCheck.True(edges.LocalApicAddress == 0x100000 && edges.IoApicAddress == 0xfffff000 && edges.IoApicGsiBase == 16);
    }

    [HostTest("madt.polling rejects malformed headers checksums storage and record bounds atomically", HostLane.Fast, 1)]
    private static void Malformed()
    {
        foreach ((Action<byte[]> change, MadtPollingStatus expected) in new (Action<byte[]>, MadtPollingStatus)[]
        {
            (b => b[0] = 0, MadtPollingStatus.InvalidHeader),
            (b => Write32(b, 4, 44), MadtPollingStatus.InvalidHeader),
            (b => Write32(b, 4, uint.MaxValue), MadtPollingStatus.InvalidHeader),
            (b => b[8] = 0, MadtPollingStatus.InvalidHeader),
            (b => b[8] = 7, MadtPollingStatus.InvalidHeader),
            (b => Write32(b, 40, 2), MadtPollingStatus.InvalidHeader),
            (b => b[45] = 0, MadtPollingStatus.InvalidRecord),
            (b => b[45] = 7, MadtPollingStatus.InvalidRecord),
            (b => b[45] = 255, MadtPollingStatus.InvalidRecord),
            (b => b[53] = 13, MadtPollingStatus.InvalidRecord),
            (b => Write32(b, 48, 4), MadtPollingStatus.InvalidRecord),
            (b => b[55] = 1, MadtPollingStatus.InvalidRecord),
            (b => Write32(b, 36, 0), MadtPollingStatus.InvalidAddress),
            (b => Write32(b, 56, 0x1000), MadtPollingStatus.InvalidAddress),
            (b => Write32(b, 56, 0xfec00001), MadtPollingStatus.InvalidAddress),
            (b => Write32(b, 56, 0xfee00000), MadtPollingStatus.InvalidAddress)
        })
        {
            byte[] table = Table(Cpu(), Io()); change(table); Checksum(table); Reject(table, expected);
        }
        byte[] badChecksum = Table(Cpu(), Io()); badChecksum[9] ^= 1;
        Reject(badChecksum, MadtPollingStatus.ChecksumFailed);
        Reject(Table(Cpu(), Io(), new byte[] { 4 }), MadtPollingStatus.InvalidRecord);
        byte[] valid = Table(Cpu(), Io());
        fixed (byte* bytes = valid)
        {
            foreach (ulong length in new ulong[] { 0, 43, 4097, ulong.MaxValue })
            {
                HostCheck.Equal(MadtPollingStatus.InvalidStorage,
                    MADTLocalAPICIOAPICSeparatelyPollingProof.Parse(bytes, length, 3, out var empty)); Empty(empty);
            }
        }
        HostCheck.Equal(MadtPollingStatus.InvalidStorage,
            MADTLocalAPICIOAPICSeparatelyPollingProof.Parse(null, 64, 3, out var result)); Empty(result);
        HostCheck.Equal(MadtPollingStatus.InvalidStorage,
            MADTLocalAPICIOAPICSeparatelyPollingProof.Parse((byte*)(ulong.MaxValue - 7), 64, 3, out result)); Empty(result);
    }

    [HostTest("madt.polling rejects conflicting optional records and unsupported or missing topology", HostLane.Fast, 2)]
    private static void Topology()
    {
        Reject(Table(Io()), MadtPollingStatus.MissingProcessor);
        Reject(Table(Cpu()), MadtPollingStatus.MissingIoApic);
        Reject(Table(Cpu(7, 3, 0), Io()), MadtPollingStatus.MissingProcessor);
        Reject(Table(Cpu(7, 4, 1), Io()), MadtPollingStatus.BspMismatch);
        Reject(Table(Cpu(7, 255, 1), Io()), MadtPollingStatus.UnsupportedTopology, 255);
        Reject(Table(Cpu(), Io()), MadtPollingStatus.UnsupportedTopology, 256);
        foreach (byte kind in new byte[] { 3, 6, 7, 8, 9, 10, 11, 0x80, 0xff })
            Reject(Table(Cpu(), Io(), new byte[] { kind, 2 }), MadtPollingStatus.UnsupportedTopology);
        Reject(Table(Cpu(), Io(), Cpu(8, 4, 1)), MadtPollingStatus.UnsupportedTopology);
        foreach (byte[][] extra in new[]
        {
            new[] { Cpu(7, 4, 0) }, new[] { Cpu(8, 3, 0) }, new[] { Io() }, new[] { Io(3, 0xfec01000, 24) },
            new[] { Iso(0, 2, 0), Iso(0, 3, 0) }, new[] { Iso(0, 2, 0), Iso(1, 2, 0) },
            new[] { Nmi(7, 1, 0), Nmi(7, 1, 15) }, new[] { Nmi(255, 1, 0), Nmi(7, 1, 0) },
            new[] { Nmi(7, 1, 0), Nmi(255, 1, 0) }, new[] { Override(0xfed00000), Override(0xfed01000) }
        }) Reject(Table([Cpu(), Io(), .. extra]), MadtPollingStatus.DuplicateRecord);
        foreach (ushort flags in new ushort[] { 2, 8, 0x10, ushort.MaxValue })
        {
            Reject(Table(Cpu(), Io(), Iso(0, 2, flags)), MadtPollingStatus.InvalidRecord);
            Reject(Table(Cpu(), Io(), Nmi(7, 1, flags)), MadtPollingStatus.InvalidRecord);
        }
        byte[] nonIsa = Iso(0, 2, 0); nonIsa[2] = 1;
        byte[] reservedOverride = Override(0xfed00000); reservedOverride[2] = 1;
        foreach (byte[] record in new[] { nonIsa, Iso(16, 16, 0), Nmi(7, 2, 0), Nmi(9, 1, 0), reservedOverride })
            Reject(Table(Cpu(), Io(), record), MadtPollingStatus.InvalidRecord);
        Reject(Table(Cpu(), Io(2, 0xfec00000, 16), Iso(0, 2, 0)), MadtPollingStatus.InvalidRecord);
        foreach (ulong address in new ulong[] { 0, 0xfed00001, 0x100000000, ulong.MaxValue })
            Reject(Table(Cpu(), Io(), Override(address)), MadtPollingStatus.InvalidAddress);
    }

    [HostTest("madt.polling accepts exactly 256 bounded records and rejects the next record", HostLane.Fast, 3)]
    private static void RecordLimit()
    {
        byte[][] records = new byte[257][];
        for (int i = 0; i < 255; i++) records[i] = Cpu((byte)i, (byte)i, i == 3 ? 1U : 0U);
        records[255] = Io(); records[256] = Nmi(255, 1, 0);
        var topology = Success(Table(records[..256]));
        HostCheck.Equal((ushort)256, topology.RecordCount);
        HostCheck.Equal((byte)3, topology.ProcessorUid);
        Reject(Table(records), MadtPollingStatus.TooManyRecords);
    }

    private static byte[] Cpu(byte uid = 7, byte id = 3, uint flags = 1)
    { byte[] r = [0, 8, uid, id, 0, 0, 0, 0]; Write32(r, 4, flags); return r; }
    private static byte[] Io(byte id = 2, uint address = 0xfec00000, uint gsi = 0)
    { byte[] r = [1, 12, id, 0, 0, 0, 0, 0, 0, 0, 0, 0]; Write32(r, 4, address); Write32(r, 8, gsi); return r; }
    private static byte[] Iso(byte source, uint gsi, ushort flags)
    { byte[] r = [2, 10, 0, source, 0, 0, 0, 0, 0, 0]; Write32(r, 4, gsi); BinaryPrimitives.WriteUInt16LittleEndian(r.AsSpan(8), flags); return r; }
    private static byte[] Nmi(byte uid, byte lint, ushort flags)
    { byte[] r = [4, 6, uid, 0, 0, lint]; BinaryPrimitives.WriteUInt16LittleEndian(r.AsSpan(3), flags); return r; }
    private static byte[] Override(ulong address)
    { byte[] r = [5, 12, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0]; BinaryPrimitives.WriteUInt64LittleEndian(r.AsSpan(4), address); return r; }
    private static byte[] Table(params byte[][] records)
    {
        int length = 44; foreach (byte[] record in records) length += record.Length;
        byte[] table = new byte[length]; table[0] = 0x41; table[1] = 0x50; table[2] = 0x49; table[3] = 0x43; table[8] = 6;
        Write32(table, 4, (uint)length); Write32(table, 36, 0xfee00000); Write32(table, 40, 1);
        int offset = 44; foreach (byte[] record in records) { record.CopyTo(table, offset); offset += record.Length; }
        Checksum(table); return table;
    }
    private static void Write32(byte[] bytes, int offset, uint value) => BinaryPrimitives.WriteUInt32LittleEndian(bytes.AsSpan(offset), value);
    private static void Checksum(byte[] table)
    { table[9] = 0; int sum = 0; foreach (byte value in table) sum += value; table[9] = unchecked((byte)-sum); }
    private static MadtPollingTopology Success(byte[] table)
    {
        // Deliberately unaligned retained bytes with independent surrounding canaries.
        byte[] storage = new byte[table.Length + 2]; storage[0] = 0xa5; storage[^1] = 0x5a; table.CopyTo(storage, 1);
        byte[] before = (byte[])storage.Clone();
        fixed (byte* bytes = storage)
        {
            HostCheck.Equal(MadtPollingStatus.Success,
                MADTLocalAPICIOAPICSeparatelyPollingProof.Parse(bytes + 1, (ulong)table.Length, 3, out var topology));
            HostCheck.True(storage.AsSpan().SequenceEqual(before)); return topology;
        }
    }
    private static void Reject(byte[] table, MadtPollingStatus expected, uint bsp = 3)
    {
        var topology = Success(Table(Cpu(), Io())); byte[] before = (byte[])table.Clone();
        fixed (byte* bytes = table)
            HostCheck.Equal(expected, MADTLocalAPICIOAPICSeparatelyPollingProof.Parse(bytes, (ulong)table.Length, bsp, out topology));
        Empty(topology); HostCheck.True(table.AsSpan().SequenceEqual(before));
    }
    private static void Empty(MadtPollingTopology t) => HostCheck.True(t.LocalApicAddress == 0 && t.IoApicAddress == 0 &&
        t.IoApicGsiBase == 0 && t.HeaderFlags == 0 && t.MaximumIsoGsi == 0 && t.BspApicId == 0 && t.ProcessorUid == 0 && t.IoApicId == 0 &&
        t.RecordCount == 0 && t.IsoCount == 0 && t.NmiCount == 0 && !t.HasLocalApicOverride);

    [HostTest("apic.setup masks every source with actual identity readback and polling isolation", HostLane.Fast, 4)]
    private static void Setup()
    {
        var topology = Success(Table(Cpu(), Io(), Iso(9, 23, 15)));
        HostCheck.Equal(23U, topology.MaximumIsoGsi);
        for (uint max = 3; max <= 6; max++)
        foreach (uint ioVersion in new uint[] { 0x170011, 0x170020 })
        {
            ApicModel model = CreateModel(max);
            model.Io[1] = ioVersion;
            var result = Setup(topology, &model);
            HostCheck.Equal(XApicSetupStatus.Success, result.Status);
            HostCheck.True(result.MaskedLocalEntries == max + 1 && result.MaskedIoEntries == 24 &&
                result.LegacyPicMasked && result.TaskPriority == 0xd0 && result.Spurious == 0x1ff);
            HostCheck.True(model.BadAccess == 0 && model.Master == 255 && model.Slave == 255 &&
                model.Local[0x380 / 16] == 0 && model.Local[0x80 / 16] == 0xd0 && model.Local[0xf0 / 16] == 0x1ff);
            foreach (uint offset in new uint[] { 0x320, 0x350, 0x360, 0x370 })
                HostCheck.Equal(0x10051U, model.Local[offset / 16]);
            for (int i = 0; i < 24; i++)
            {
                HostCheck.Equal(0x10051U, model.Io[0x10 + 2 * i]);
                HostCheck.Equal(0x03000000U, model.Io[0x11 + 2 * i]);
            }
            HostCheck.Equal(max >= 4 ? 0x10051U : 0U, model.Local[0x340 / 16]);
            HostCheck.Equal(max >= 5 ? 0x10051U : 0U, model.Local[0x330 / 16]);
            HostCheck.Equal(max >= 6 ? 0x10051U : 0U, model.Local[0x2f0 / 16]);
        }
        byte[] noPic = Table(Cpu(), Io()); Write32(noPic, 40, 0); Checksum(noPic);
        var noPicTopology = Success(noPic); ApicModel noPicModel = CreateModel();
        var access = Access(&noPicModel); access.ReadPort8 = null; access.WritePort8 = null;
        var noPicResult = BoundedXApicSetup.Run(noPicTopology, 0x220, 3, 0xfee00900, 2, access);
        HostCheck.True(noPicResult.Status == XApicSetupStatus.Success && !noPicResult.LegacyPicMasked);
    }

    [HostTest("apic.setup rejects invalid actual CPU observations before device access", HostLane.Fast, 5)]
    private static void SetupAdmission()
    {
        var topology = Success(Table(Cpu(), Io()));
        foreach ((uint features, uint id, ulong msr, ulong flags) in new (uint, uint, ulong, ulong)[]
        {
            (0,3,0xfee00900,2), (0x200,3,0xfee00900,2), (0x20,3,0xfee00900,2),
            (0x220,4,0xfee00900,2), (0x220,259,0xfee00900,2), (0x220,3,0xfee00d00,2),
            (0x220,3,0xfee00100,2), (0x220,3,0xfee00800,2), (0x220,3,0xfed00900,2),
            (0x220,3,0x1fee00900,2), (0x220,3,0xfee00901,2), (0x220,3,0xfee00900,0),
            (0x220,3,0xfee00900,0x202)
        })
        {
            ApicModel model = CreateModel();
            HostCheck.Equal(XApicSetupStatus.InvalidCpu, BoundedXApicSetup.Run(topology, features, id, msr, flags, Access(&model)).Status);
            HostCheck.Equal(0U, model.Operations);
        }
        ApicModel invalid = CreateModel();
        HostCheck.Equal(XApicSetupStatus.InvalidTopology, Setup(default, &invalid).Status);
        HostCheck.Equal(0U, invalid.Operations);
        for (int i = 0; i < 4; i++)
        {
            var access = Access(&invalid);
            if (i == 0) access.Read32 = null; else if (i == 1) access.Write32 = null;
            else if (i == 2) access.ReadPort8 = null; else access.WritePort8 = null;
            HostCheck.Equal(XApicSetupStatus.InvalidAccess, BoundedXApicSetup.Run(topology, 0x220, 3, 0xfee00900, 2, access).Status);
            HostCheck.Equal(0U, invalid.Operations);
        }
    }

    [HostTest("apic.setup rejects unsupported hardware pending interrupts and failed readback", HostLane.Fast, 6)]
    private static void SetupHardware()
    {
        var topology = Success(Table(Cpu(), Io()));
        foreach ((int space, int index, uint value) in new (int, int, uint)[]
        {
            (0,2,0x04000000), (0,3,0x5000f), (0,3,0x50016), (0,3,0x20014),
            (0,3,0x70014), (0,3,0x2050014), (1,0,0x03000000), (1,0,0x12000000),
            (1,1,0x170012), (1,1,0x180011), (1,1,0x170111), (1,1,0x170021), (1,1,0x180020), (1,1,0x170120)
        })
        {
            ApicModel m = CreateModel(); if (space == 0) m.Local[index] = value; else m.Io[index] = value;
            HostCheck.Equal(XApicSetupStatus.UnsupportedHardware, Setup(topology, &m).Status);
            HostCheck.True(m.Master == 0 && m.Slave == 0 && m.Io[0x10] == 0 && m.BadAccess == 0);
        }
        ApicModel outside = CreateModel();
        HostCheck.Equal(XApicSetupStatus.UnsupportedHardware,
            Setup(Success(Table(Cpu(), Io(), Iso(9, 24, 15))), &outside).Status);
        HostCheck.Equal(XApicSetupStatus.UnsupportedHardware,
            Setup(Success(Table(Cpu(), Io(2, 0xfec00000, uint.MaxValue))), &outside).Status);
        for (uint i = 0; i < 8; i++)
        {
            ApicModel m = CreateModel(); m.Local[0x10 + i] = 1;
            HostCheck.Equal(XApicSetupStatus.PendingInterrupt, Setup(topology, &m).Status);
        }
        ApicModel pending = CreateModel(); pending.Local[0x27] = 1;
        HostCheck.Equal(XApicSetupStatus.PendingInterrupt, Setup(topology, &pending).Status);
        ApicModel lower = CreateModel(); lower.Local[0x26] = uint.MaxValue;
        HostCheck.Equal(XApicSetupStatus.Success, Setup(topology, &lower).Status);
        foreach (uint corrupt in new uint[] { 0x10010, 0x10011, 0x320, 0x380, 0x80, 0xf0, 0x20021, 0x200a1 })
        {
            ApicModel m = CreateModel(); m.Corrupt = corrupt;
            HostCheck.Equal(XApicSetupStatus.ReadBackFailed, Setup(topology, &m).Status);
        }
    }

    [HostTest("apic.setup stops at every failed callback including writes with uncertain side effects", HostLane.Fast, 7)]
    private static void SetupFailures()
    {
        var topology = Success(Table(Cpu(), Io())); ApicModel baseline = CreateModel();
        HostCheck.Equal(XApicSetupStatus.Success, Setup(topology, &baseline).Status);
        for (uint fail = 1; fail <= baseline.Operations; fail++)
        {
            ApicModel m = CreateModel(); m.FailAt = fail;
            var result = Setup(topology, &m);
            HostCheck.True(result.Status is XApicSetupStatus.ReadFailed or XApicSetupStatus.WriteFailed);
            HostCheck.True(m.Operations == fail && m.BadAccess == 0);
        }
        ApicModel invalid = CreateModel();
        HostCheck.Equal(XApicSetupStatus.InvalidAccess, BoundedXApicSetup.CheckQuiescent(0, Access(&invalid)));
        HostCheck.Equal(XApicSetupStatus.InvalidAccess, BoundedXApicSetup.CheckQuiescent(0xfee00000, default));
        HostCheck.Equal(0U, invalid.Operations);
    }

    private struct ApicModel
    {
        public fixed uint Local[256];
        public fixed uint Io[256];
        public uint Selected, Operations, FailAt, BadAccess, Corrupt;
        public byte Master, Slave;
    }
    [HostTest("apic.native observation has reviewed CPL IF CPUID RDMSR ABI and rejects modified bytes", HostLane.Fast, 8)]
    private static void ObservationBytes()
    {
        HostCheck.Equal(24, sizeof(XApicCpuObservation));
        HostCheck.Equal(0, Marshal.OffsetOf<XApicCpuObservation>(nameof(XApicCpuObservation.FeaturesEdx)).ToInt32());
        HostCheck.Equal(4, Marshal.OffsetOf<XApicCpuObservation>(nameof(XApicCpuObservation.BspId)).ToInt32());
        HostCheck.Equal(8, Marshal.OffsetOf<XApicCpuObservation>(nameof(XApicCpuObservation.ApicBaseMsr)).ToInt32());
        HostCheck.Equal(16, Marshal.OffsetOf<XApicCpuObservation>(nameof(XApicCpuObservation.Rflags)).ToInt32());
        byte* code = stackalloc byte[98]; new Span<byte>(code, 98).Fill(0xa5);
        HostCheck.True(!X64ApicObservation.Emit(null, 96) && !X64ApicObservation.Validate(null, 96));
        HostCheck.True(!X64ApicObservation.Emit(code + 1, 95) && !X64ApicObservation.Validate(code + 1, 95));
        HostCheck.True(!X64ApicObservation.Emit((byte*)(ulong.MaxValue - 16), 96));
        HostCheck.True(new ReadOnlySpan<byte>(code, 98).IndexOfAnyExcept((byte)0xa5) == -1);
        HostCheck.True(X64ApicObservation.Emit(code + 1, 96) && X64ApicObservation.Validate(code + 1, 96));
        // Independently decoded with LLVM: CS/IF guards branch to the restoring
        // failure epilogue; CPUID preserves RBX; feature check dominates RDMSR.
        byte[] expected = Convert.FromHexString(
            "F30F1EFA534989C8668CC8A80375489C415941F7C100020000753CB8010000000FA2" +
            "418910C1EB18418958044D89481081E22002000081FA200200007519B91B0000000F32" +
            "48C1E2204809D049894008B8010000005BC331C05BC30000000000");
        HostCheck.True(new ReadOnlySpan<byte>(code + 1, 96).SequenceEqual(expected) && code[0] == 0xa5 && code[97] == 0xa5);
        for (int i = 0; i < 96; i++)
        { code[1 + i] ^= 1; HostCheck.True(!X64ApicObservation.Validate(code + 1, 96)); code[1 + i] ^= 1; }
    }
    private static ApicModel CreateModel(uint max = 5)
    {
        ApicModel m = default; m.Local[2] = 0x03000000; m.Local[3] = (max << 16) | 0x14;
        m.Io[0] = 0x02000000; m.Io[1] = 0x00170011; return m;
    }
    private static XApicSetupAccess Access(ApicModel* m) => new()
    { Context = m, Read32 = &ApicRead, Write32 = &ApicWrite, ReadPort8 = &PicRead, WritePort8 = &PicWrite };
    private static XApicSetupResult Setup(MadtPollingTopology t, ApicModel* m) =>
        BoundedXApicSetup.Run(t, 0x220, 3, 0xfee00900, 2, Access(m));
    [UnmanagedCallersOnly]
    private static byte ApicRead(void* context, ulong address, uint* value)
    {
        var m = (ApicModel*)context; m->Operations++;
        uint key;
        if (address >= 0xfee00000 && address < 0xfee01000 && (address & 15) == 0)
        { key = (uint)(address - 0xfee00000); *value = m->Local[key / 16]; }
        else if (address == 0xfec00010 && m->Selected < 256)
        { key = 0x10000 + m->Selected; *value = m->Io[m->Selected]; }
        else { m->BadAccess++; return 0; }
        if (m->Corrupt == key && m->Corrupt != 0) *value ^= 1;
        return m->Operations == m->FailAt ? (byte)0 : (byte)1;
    }
    [UnmanagedCallersOnly]
    private static byte ApicWrite(void* context, ulong address, uint value)
    {
        var m = (ApicModel*)context; m->Operations++;
        if (address >= 0xfee00000 && address < 0xfee01000 && (address & 15) == 0)
        {
            uint offset = (uint)(address - 0xfee00000);
            if (offset is not (0x320 or 0x330 or 0x340 or 0x350 or 0x360 or 0x370 or 0x2f0 or 0x380 or 0x80 or 0xf0))
            { m->BadAccess++; return 0; }
            m->Local[offset / 16] = value;
        }
        else if (address == 0xfec00000 && value < 256) m->Selected = value;
        else if (address == 0xfec00010 && m->Selected >= 0x10 && m->Selected < 0x40)
        {
            if ((m->Selected & 1) != 0 && (m->Io[m->Selected - 1] & 0x10000) == 0) m->BadAccess++;
            m->Io[m->Selected] = value;
        }
        else { m->BadAccess++; return 0; }
        return m->Operations == m->FailAt ? (byte)0 : (byte)1;
    }
    [UnmanagedCallersOnly]
    private static byte PicRead(void* context, ushort port, byte* value)
    {
        var m = (ApicModel*)context; m->Operations++;
        if (port == 0x21) *value = m->Master; else if (port == 0xa1) *value = m->Slave;
        else { m->BadAccess++; return 0; }
        if (m->Corrupt == 0x20000 + port) *value ^= 1;
        return m->Operations == m->FailAt ? (byte)0 : (byte)1;
    }
    [UnmanagedCallersOnly]
    private static byte PicWrite(void* context, ushort port, byte value)
    {
        var m = (ApicModel*)context; m->Operations++;
        if (port == 0x21) m->Master = value; else if (port == 0xa1) m->Slave = value;
        else { m->BadAccess++; return 0; }
        return m->Operations == m->FailAt ? (byte)0 : (byte)1;
    }
}
