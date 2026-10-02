using System.Buffers.Binary;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Text;
using OS.Boot;
using OS.Kernel.Diagnostics;
using OS.Kernel.Memory;
using Emitter = OS.Kernel.Diagnostics.SeparateDisabledDefaultPageFaultTransition;

internal static unsafe class SeparateDisabledDefaultPageFaultTransitionTests
{
    private const ulong Pat = 0x0007040600070406;
    private const ulong C = 0x0000000100000000, D = 0x0000000200000000, T = 0x0000000300000000;

    [ModuleInitializer]
    internal static void RunAll()
    {
        Console.WriteLine("TEST_START m02.014.transition");
        try
        {
            using Fixture fixture = new();
            fixture.Prepare();
            Require(!default(SeparatePageFaultTransitionResult).Succeeded, "default fails closed");
            Require(!RuntimeHelpers.IsReferenceOrContainsReferences<SeparatePageFaultTransitionRequest>() &&
                !RuntimeHelpers.IsReferenceOrContainsReferences<SeparatePageFaultTransitionResult>(), "POD request/result");
            TestPreparedBytes(fixture);
            TestStaticBytes(fixture);
            TestSyntheticExecution(fixture);
            TestSyntheticFailures(fixture);
            TestRequestsAndCorruption(fixture);
            TestMappingAndBounds(fixture);
        }
        catch (Exception exception)
        {
            Console.WriteLine("TEST_FAIL m02.014.transition " + exception.Message);
            throw;
        }
        Console.WriteLine("TEST_PASS m02.014.transition");
    }

    private static void TestPreparedBytes(Fixture f)
    {
        byte[] context = f.ContextBytes;
        Require(U16(context, 0) == 39 && U64(context, 2) == f.Data + 4096 + 24, "full GDTR");
        Require(U64(context, 32) == 0x00af9a000000ffff && U64(context, 40) == 0x00cf92000000ffff, "code/data descriptors");
        ulong tss = U16(context, 50) | ((ulong)context[52] << 16) | ((ulong)context[55] << 24) | ((ulong)U32(context, 56) << 32);
        Require(tss == f.Data + 4160 && context[53] == 0x89 && U16(context, 48) == 103 && U16(context, 166) == 104, "full TSS");
        Require(U64(context, 100) == f.Data + 0x4000 && U64(context, 108) == f.Data + 0x6000 &&
            U64(context, 116) == f.Data + 0x8000, "guarded IST tops");
        Require(U16(context, 176) == 4095 && U64(context, 178) == f.Data, "full IDTR");
        for (int i = 352; i < 4096; i++) Require(context[i] == 0, "observations/output initially zero");
        Require(U64(f.StreamBytes, 3600) == f.Data + 4096, "full context literal");
        Require(f.StreamBytes.AsSpan(2841, 3).SequenceEqual(new byte[] { 0x48, 0x8b, 0x10 }), "exact null read");
        Require(Emitter.ValidateStreamReadBack(f.Stream, 8192, f.Context, 4096, f.Request).ReadBackValidated, "readback");
        byte[] expected = new byte[4096];
        void W16(int offset, ushort value) => BinaryPrimitives.WriteUInt16LittleEndian(expected.AsSpan(offset), value);
        void W64(int offset, ulong value) => BinaryPrimitives.WriteUInt64LittleEndian(expected.AsSpan(offset), value);
        W16(0, 39); W64(2, f.Data + 4120); W64(32, 0x00af9a000000ffff); W64(40, 0x00cf92000000ffff);
        ulong expectedTss = f.Data + 4160;
        W16(48, 103); W16(50, (ushort)expectedTss); expected[52] = (byte)(expectedTss >> 16); expected[53] = 0x89;
        expected[55] = (byte)(expectedTss >> 24); BinaryPrimitives.WriteUInt32LittleEndian(expected.AsSpan(56), (uint)(expectedTss >> 32));
        W64(100, f.Data + 0x4000); W64(108, f.Data + 0x6000); W64(116, f.Data + 0x8000); W16(166, 104);
        W16(176, 4095); W64(178, f.Data);
        ulong[] geometry = [f.Code, f.Data, f.Root, f.Code + 4096 + 2841, f.Request.Observation.Cr0,
            f.Request.Observation.RawCr3, f.Request.Observation.RawCr4, f.Request.Observation.Ia32Efer, f.Request.Pat,
            f.Request.Observation.PhysicalAddressWidth, ((1UL << f.Request.Observation.PhysicalAddressWidth) - 1) & ~4095UL,
            f.Request.Observation.RawCr4 & ~0x80UL];
        for (int i = 0; i < geometry.Length; i++) W64(256 + i * 8, geometry[i]);
        Require(context.AsSpan().SequenceEqual(expected), "every context byte independently encoded");
        static ulong Fnv(byte[] bytes) { ulong h = 14695981039346656037; foreach (byte b in bytes) h = unchecked((h ^ b) * 1099511628211); return h; }
        var readback = Emitter.ValidateStreamReadBack(f.Stream, 8192, f.Context, 4096, f.Request);
        Require(readback.BytesWritten == 0 && readback.BytesCompared == 8192 && readback.FirstMismatchOffset == -1 &&
            readback.ContextFingerprint == Fnv(context) && readback.StreamFingerprint == Fnv(f.StreamBytes), "independent fingerprints/counts");
        f.CheckCanaries();
    }

    private static void TestSyntheticExecution(Fixture f)
    {
        Machine machine = new(f);
        string expected = ExpectedTranscript(machine);
        machine.Run();
        Require(machine.Output.ToString() == expected && expected.Length == 2053,
            "synthetic execution: " + machine.Output + " pc=" + machine.Pc.ToString("X"));
        Require(machine.Faults == 1 && machine.EarlyCr2 && machine.SavedRegistersChecked &&
            machine.Polls == 2053 && machine.Cr0 == f.Request.Observation.Cr0 && machine.Efer == f.Request.Observation.Ia32Efer && machine.RawPat == Pat,
            "captured GPRs, bounded serial and preserved mode");
        Require(machine.Events.IndexOf("lgdt") < machine.Events.IndexOf("ltr") && machine.Events.IndexOf("ltr") < machine.Events.IndexOf("lidt") &&
            machine.Events.IndexOf("lidt") < machine.Events.IndexOf("cr3") && machine.Events.IndexOf("cr3") < machine.Events.IndexOf("fault") &&
            machine.Events.IndexOf("fault") < machine.Events.IndexOf("cr2"), "descriptor/root/fault/capture order");
        Machine lateCr2 = new(f) { ReplaceCr2AfterEarlyRead = 0xfedcba9876543210 };
        lateCr2.Run(); Require(lateCr2.Output.ToString() == expected, "early CR2 retained despite later synthetic value change");
        Machine delayed = new(f) { InitialNotReadyPolls = 65535 }; delayed.Run();
        Require(delayed.Output.ToString() == expected && delayed.Polls == 65535 + 2053, "last allowed THRE poll succeeds");
        Machine volatileFlags = new(f) { Flags = 0x246 }; volatileFlags.Run();
        Require(volatileFlags.Output.ToString() == expected, "required flag policy ignores volatile arithmetic flags");
        var originalRequest = f.Request;
        f.Request = new(f.Code, f.Data, f.Root, Mode(cr4: 0x620), Pat, f.Image); f.Prepare();
        Machine noPge = new(f); string noPgeExpected = ExpectedTranscript(noPge); noPge.Run();
        Require(noPge.Output.ToString() == noPgeExpected && !noPge.Events.Contains("cr4"), "PGE-clear path writes no CR4");
        f.Request = originalRequest; f.Prepare();
        foreach ((ulong code, ulong data, ulong tables, byte width) in new[]
        {
            (0x100000UL, 0x200000UL, 0x300000UL, (byte)36),
            (0x00007fffffffc000UL, 0x00007ffffff00000UL, 0x00007fffffe00000UL, (byte)48)
        })
        {
            using Fixture other = new(code, data, tables, width); other.Prepare(); TestStaticBytes(other);
            Machine relocated = new(other); string relocatedExpected = ExpectedTranscript(relocated); relocated.Run();
            Require(relocated.Output.ToString() == relocatedExpected, "relocated/width synthetic transcript");
        }
    }

    private static string ExpectedTranscript(Machine m)
    {
        var f = m.Fixture;
        static string H(ulong x) => "0x" + x.ToString("X16", System.Globalization.CultureInfo.InvariantCulture);
        StringBuilder text = new();
        text.Append("TEST_START m02.014\r\n");
        text.Append($"M02_PAGE_FAULT_LAYOUT code={H(f.Code)} data={H(f.Data)} tables={H(f.Root)} faultRip={H(f.Code + 4096 + 2841)} codePages=3 dataPages=10 tablePages=43 mappedPages=52\r\n");
        text.Append($"M02_PAGE_FAULT_MODE firmwareCs={H(m.Cs)} cr0={H(m.Cr0)} firmwareCr3={H(m.Cr3)} cr4Before={H(m.Cr4)} efer={H(m.Efer)} pat={H(m.RawPat)} physicalBits={H(m.AddressSizes & 255)}\r\n");
        text.Append($"M02_PAGE_FAULT_ROOT cr3={H(f.Root)} expectedCr3={H(f.Root)} cr4={H(m.Cr4 & ~0x80UL)} expectedCr4={H(m.Cr4 & ~0x80UL)}\r\n");
        string[] roles = ["zero", "df", "pf", "nmi", "transition"];
        for (int role = 0; role < 5; role++)
        {
            ulong linear = role == 0 ? 0 : f.Data + (ulong)role * 8192;
            ulong[] entries = new ulong[4]; ulong table = f.Root; int levels = 0;
            for (int level = 0; level < 4; level++)
            {
                entries[level] = m.Read(table + ((linear >> (39 - level * 9)) & 511) * 8, 64); levels++;
                if (entries[level] == 0) break;
                Require((entries[level] & 3) == 3 && level < 3, "independent absence fixture");
                table = entries[level] & 0x000ffffffffff000;
            }
            text.Append($"M02_PAGE_FAULT_ABSENT role={roles[role]} linear={H(linear)} levelsRead={H((ulong)levels)} pml4e={H(entries[0])} pdpte={H(entries[1])} pde={H(entries[2])} pte={H(entries[3])}\r\n");
        }
        text.Append($"M02_PAGE_FAULT_FRAME vector={H(14)} hasErrorCode={H(1)} error={H(0)} cr2={H(0)} rip={H(f.Code + 4096 + 2841)} cs={H(m.Cs)} rflags={H(0x10046)} savedRsp={H(f.Data + 0xa000)} savedSs={H(16)} handlerRsp={H(f.Data + 0x6000 - 192)} handlerSs={H(0)} cr3={H(f.Root)}\r\n");
        text.Append("CLROS_M02_014_READY vector=14 hardwareErrorCode=true errorCode=0 cr2=0 pageZeroAbsent=true guardPagesAbsent=4 ist=2 rf=true ownedCr3=true hardwareStateCaptured=true faultTriggered=true terminalHalt=true postExitBootServices=false\r\nTEST_PASS m02.014\r\n");
        return text.ToString();
    }

    private static void TestStaticBytes(Fixture f)
    {
        Machine m = new(f); byte[] bytes = f.StreamBytes; bool[] covered = new bool[8192];
        int[] starts = [0, 768, 2048, 2104, 2168, 2528, 2688, 2944, 3016, 3064, 3200, 3232, 3264,
            3328, 3360, 3392, 3424, 3456, 3488, 3520, 3584];
        int[] ends = [768, 2048, 2104, 2168, 2528, 2688, 2944, 3016, 3064, 3200, 3232, 3264, 3328,
            3360, 3392, 3424, 3456, 3488, 3520, 3584, 3600];
        Dictionary<ulong, Instruction> decoded = new();
        for (int block = 0; block < starts.Length; block++)
        {
            int offset = starts[block];
            while (offset < ends[block] && (bytes[offset] != 0xf4 || offset == 3585))
            {
                ulong address = f.Code + 4096 + (ulong)offset;
                Instruction instruction = m.Decode(address);
                Require(instruction.Op != "unknown" && instruction.Next > address && instruction.Next <= f.Code + 4096 + (ulong)ends[block], "complete bounded instruction decode");
                decoded.Add(address, instruction);
                while (f.Code + 4096 + (ulong)offset < instruction.Next) covered[offset++] = true;
            }
            for (; offset < ends[block]; offset++) { Require(bytes[offset] == 0xf4, "halt padding"); covered[offset] = true; }
        }
        foreach (var pair in decoded)
        {
            Instruction i = pair.Value;
            if (i.Op is "jmp" or "jcc") Require(decoded.ContainsKey(i.Immediate), "branch must land on local instruction boundary");
            if (i.Op == "lea") Require(i.Source.Rip && i.Source.Base == -1 && i.Source.Index == -1, "local RIP-relative literal");
        }
        void Data(int offset, byte[] expected)
        {
            Require(bytes.AsSpan(offset, expected.Length).SequenceEqual(expected), "independent literal bytes at " + offset);
            for (int i = offset; i < offset + expected.Length; i++) { Require(!covered[i], "no code/literal overlap"); covered[i] = true; }
        }
        Data(3600, BitConverter.GetBytes(f.Data + 4096));
        string expected = ExpectedTranscript(m);
        var fields = System.Text.RegularExpressions.Regex.Matches(expected, "0x[0-9A-F]{16}");
        Require(fields.Count == 57, "57 numeric slots");
        string template = System.Text.RegularExpressions.Regex.Replace(expected, "0x[0-9A-F]{16}", "0x0000000000000000");
        Data(4096, Encoding.ASCII.GetBytes(template));
        int[] sources = [256,264,272,280,352,360,368,376,384,392,400,408,272,416,344,
            512,520,528,536,544,552,576,584,592,600,608,616,640,648,656,664,672,680,
            704,712,720,728,736,744,768,776,784,792,800,808,
            960,968,976,944,984,992,1000,1008,1016,1024,1032,1040];
        byte[] table = new byte[57 * 4];
        for (int i = 0; i < 57; i++)
        {
            BinaryPrimitives.WriteUInt16LittleEndian(table.AsSpan(i * 4), (ushort)sources[i]);
            BinaryPrimitives.WriteUInt16LittleEndian(table.AsSpan(i * 4 + 2), (ushort)(fields[i].Index + 2));
            Require(sources[i] + 8 <= 1048 && fields[i].Index + 18 <= 2053, "bounded source/destination field");
        }
        Data(6160, table); Data(6400, Encoding.ASCII.GetBytes("0123456789ABCDEF"));
        string[] reasons = ["preflight_failed", "descriptor_mismatch", "root_mismatch", "absence_mismatch", "frame_mismatch", "unexpected_completion", "reentered"];
        int failure = 6448;
        foreach (string reason in reasons)
        { byte[] literal = Encoding.ASCII.GetBytes("M02_PAGE_FAULT_CR2_ERROR_CODE_FAILED\r\nTEST_FAIL m02.014 " + reason + "\r\n"); Data(failure, literal); failure += literal.Length; }
        for (int i = 0; i < bytes.Length; i++) if (!covered[i]) Require(bytes[i] == 0xf4, "data gap padding " + i);
        Require(failure == 6962 && decoded.Count > 300 && decoded.Count < 450, "measured code/data budget");
    }

    private static void TestSyntheticFailures(Fixture f)
    {
        void Reject(Action<Machine> alter, string reason, bool silent = false)
        {
            Machine m = new(f); alter(m); m.Run(); string text = m.Output.ToString();
            Require(!text.Contains("CLROS_M02_014_READY", StringComparison.Ordinal) && !text.Contains("TEST_PASS", StringComparison.Ordinal) &&
                (silent ? text.Length == 0 : text.Contains("TEST_FAIL m02.014 " + reason + "\r\n", StringComparison.Ordinal)), "synthetic rejected " + reason);
        }
        foreach (ulong bit in new ulong[] { 1, 0x10000, 0x80000000 }) Reject(m => m.Cr0 &= ~bit, "preflight_failed");
        foreach (ulong bit in new ulong[] { 0x1000, 0x20000, 0x800000, 0x1000000 }) Reject(m => m.Cr4 |= bit, "preflight_failed");
        Reject(m => m.Cr4 &= ~0x20UL, "preflight_failed");
        foreach (ulong bit in new ulong[] { 0x100, 0x400, 0x800 }) Reject(m => m.Efer &= ~bit, "preflight_failed");
        foreach (ulong bit in new ulong[] { 0x100, 0x400 }) Reject(m => m.Flags |= bit, "preflight_failed");
        Reject(m => m.Flags &= ~2UL, "preflight_failed");
        Reject(m => m.MaximumLeaf = 0x80000007, "preflight_failed");
        foreach (uint width in new uint[] { 0, 35, 53, 255 }) Reject(m => m.AddressSizes = width, "preflight_failed");
        Reject(m => m.RawPat ^= 1UL << 56, "preflight_failed");
        Reject(m => m.Cs |= 3, "preflight_failed"); Reject(m => m.LarSucceeds = false, "preflight_failed");
        foreach (ulong mask in new ulong[] { 0x200000, 0x800000, 0x8000, 0x1000, 0x2000, 0x200, 0x800, 0x400000 })
            Reject(m => m.AccessRights ^= mask, "preflight_failed");
        Reject(m => m.Write(f.Data + 4096 + 432, 1, 64), "reentered");
        Reject(m => m.CompleteRead = true, "unexpected_completion");
        Reject(m => m.SerialReady = false, "serial_timeout", true);
        Machine exhausted = new(f) { SerialReady = false }; exhausted.Run();
        Require(exhausted.Polls == 65536, "finite exact per-byte poll bound");
        Reject(m => m.AfterFault = x => x.Cr2 = 1, "frame_mismatch");
        Reject(m => m.AfterFault = x => x.Cr2 = 1UL << 63, "frame_mismatch");
        Reject(m => m.AfterFault = x => x.Cr3 ^= 4096, "frame_mismatch");
        Reject(m => m.AfterFault = x => x.Write(f.Data + 4096 + 440, 1, 64), "reentered");
        Reject(m => m.AfterRoot = x => x.Cr3 ^= 4096, "root_mismatch");
        Reject(m => m.AfterRoot = x => x.Cr4 |= 0x80, "root_mismatch");
        Reject(m => m.AfterRoot = x => x.Cr0 ^= 0x10000, "root_mismatch");
        Reject(m => m.AfterRoot = x => x.Efer ^= 0x800, "root_mismatch");
        Reject(m => m.AfterRoot = x => x.RawPat ^= 1, "root_mismatch");
        Reject(m => m.CorruptDescriptorReadBack = true, "descriptor_mismatch");
        Reject(m => m.CorruptTrReadBack = true, "descriptor_mismatch");
        Reject(m => m.AfterFault = x => x.Ss = 16, "frame_mismatch");
        Reject(m => m.AfterFault = x => x.Cs = 16, "frame_mismatch");
        Reject(m => m.BeforeHandler = x => x.Write(x.Registers[4], 13, 64), "frame_mismatch");
        Reject(m => m.BeforeHandler = x => x.Write(x.Registers[4] + 8, 0, 64), "frame_mismatch");
        Reject(m => m.Write(f.Data + 4096 + 108, f.Data + 0x6000 - 16, 64), "frame_mismatch");
        Machine partialSerial = new(f) { SerialAvailableBytes = 10 }; partialSerial.Run();
        Require(partialSerial.Output.Length == 10 && partialSerial.Polls == 10 + 65536, "each later byte also has a finite failure bound");
        foreach ((int offset, ulong value) in new[] { (0,1UL), (0,1UL<<63), (8,f.Code+4096+2842), (16,3UL), (24,0x46UL), (24,0x10047UL), (32,f.Data+0xa008), (40,0UL) })
            Reject(m => m.AfterFault = x => x.Write(x.Registers[4] + (ulong)offset, value, 64), "frame_mismatch");
        foreach (ulong bad in new ulong[] { 1, 2, 0x40, f.Root | 3, (f.Root + 43 * 4096) | 3, (1UL << 36) | 3,
            (f.Root + 4096) | 0x83, (f.Root + 4096) | 0x43, (f.Root + 4096) | 7, (f.Root + 4096) | 0x8000000000000003 })
            Reject(m => m.Write(f.Root, bad, 64), "absence_mismatch");
        // Same pristine image plus legal ancestor accessed bits still yields captured raw evidence.
        Machine accessed = new(f);
        for (ulong at = f.Root; at < f.Root + 43 * 4096; at += 8)
        {
            ulong entry = accessed.Read(at, 64);
            if ((entry & 1) != 0) accessed.Write(at, entry | 0x20, 64);
        }
        string expected = ExpectedTranscript(accessed); accessed.Run();
        Require(accessed.Output.ToString() == expected, "accessed variations captured exactly");
    }

    private static void TestRequestsAndCorruption(Fixture f)
    {
        byte[] context = f.ContextBytes, stream = f.StreamBytes;
        void Reject(SeparatePageFaultTransitionRequest request)
        {
            Require(!Emitter.PrepareContext(f.Context, 4096, request).Succeeded, "request rejected");
            Require(new Span<byte>(f.Context, 4096).SequenceEqual(context) && new Span<byte>(f.Stream, 8192).SequenceEqual(stream), "request unchanged storage");
        }
        Reject(default);
        foreach (ulong bad in new ulong[] { 0, 1, ulong.MaxValue - 4095, 0x0000800000000000, f.Data, f.Root, (1UL << 36) - 4096 })
            Reject(new(bad, f.Data, f.Root, Mode(), Pat, f.Image));
        foreach (byte width in new byte[] { 0, 35, 53, 255 }) Reject(new(f.Code, f.Data, f.Root, Mode(width), Pat, f.Image));
        foreach (ulong cr4 in new ulong[] { 0, 0x6a0 | 0x1000, 0x6a0 | 0x20000, 0x6a0 | 0x800000, 0x6a0 | 0x1000000 })
            Reject(new(f.Code, f.Data, f.Root, Mode(cr4: cr4), Pat, f.Image));
        Reject(new(f.Code, f.Data, f.Root, default, Pat, f.Image));
        Reject(new(f.Code, f.Data, f.Root, Mode(), 0, f.Image));
        Reject(new(f.Code, f.Data, f.Root, Mode(), Pat, default));
        Reject(new(f.Code, f.Data, f.Root + 0x100000, Mode(), Pat, f.Image));
        Reject(new(f.Code, f.Data, f.Root, Mode(52), Pat, f.Image));
        foreach (ulong cr0 in new ulong[] { 0x80010032, 0x80000033, 0x10033 }) Reject(new(f.Code, f.Data, f.Root, Mode(cr0: cr0), Pat, f.Image));
        foreach (ulong efer in new ulong[] { 0xc00, 0x900, 0x500 }) Reject(new(f.Code, f.Data, f.Root, Mode(efer: efer), Pat, f.Image));
        foreach (ulong flags in new ulong[] { 0, 0x302, 0x602 }) Reject(new(f.Code, f.Data, f.Root, Mode(flags: flags), Pat, f.Image));
        Reject(new(f.Code, f.Data, f.Root, Mode(leaf: 0x80000007), Pat, f.Image));
        for (int slot = 0; slot < 8; slot++) Reject(new(f.Code, f.Data, f.Root, Mode(), (Pat & ~(255UL << (slot * 8))) | (2UL << (slot * 8)), f.Image));
        foreach (string name in new[] { "<RangeCount>k__BackingField", "<UsedTablePages>k__BackingField", "<MappedPages>k__BackingField" })
        {
            foreach (ulong value in new ulong[] { 0, 1, 44, ulong.MaxValue })
            {
                var snapshot = f.Image;
                Span<byte> raw = MemoryMarshal.AsBytes(MemoryMarshal.CreateSpan(ref snapshot, 1));
                BinaryPrimitives.WriteUInt64LittleEndian(raw.Slice(Marshal.OffsetOf<SeparatePageFaultPageTableResult>(name).ToInt32()), value);
                Reject(new(f.Code, f.Data, f.Root, Mode(), Pat, snapshot));
            }
        }
        foreach (ulong capacity in new ulong[] { 0, 4095, ulong.MaxValue }) Require(!Emitter.PrepareContext(f.Context, capacity, f.Request).Succeeded, "capacity");
        Require(!Emitter.PrepareContext(null, 4096, f.Request).Succeeded && !Emitter.PrepareContext(f.Context + 1, 4096, f.Request).Succeeded, "null/misaligned");
        Require(!Emitter.EmitStream(f.Context, 8192, f.Context, 4096, f.Request).Succeeded, "overlap");
        foreach (ulong pointer in new ulong[] { 0x0000800000000000, 0x00007ffffffff800, ulong.MaxValue - 7 })
        {
            Require(!Emitter.PrepareContext((byte*)pointer, 4096, f.Request).Succeeded, "invalid pointer span before access");
            Require(!Emitter.EmitStream((byte*)pointer, 8192, f.Context, 4096, f.Request).Succeeded, "invalid stream span before access");
        }
        foreach (ulong capacity in new ulong[] { 0, 8191, ulong.MaxValue }) Require(!Emitter.EmitStream(f.Stream, capacity, f.Context, 4096, f.Request).Succeeded, "stream capacity");
        Require(new Span<byte>(f.Context, 4096).SequenceEqual(context) && new Span<byte>(f.Stream, 8192).SequenceEqual(stream), "storage rejection unchanged");
        for (int i = 0; i < 4096; i++)
        {
            f.Context[i] ^= 1;
            var result = Emitter.EmitStream(f.Stream, 8192, f.Context, 4096, f.Request);
            Require(result.Status == SeparatePageFaultTransitionStatus.ContextMismatch && result.FirstMismatchOffset == i && result.BytesWritten == 0, "every context byte mismatch");
            f.Context[i] ^= 1;
        }
        for (int i = 0; i < 8192; i++)
        {
            f.Stream[i] ^= 1;
            var result = Emitter.ValidateStreamReadBack(f.Stream, 8192, f.Context, 4096, f.Request);
            Require(result.Status == SeparatePageFaultTransitionStatus.StreamMismatch && result.FirstMismatchOffset == i && result.BytesWritten == 0, "every stream byte mismatch");
            f.Stream[i] ^= 1;
        }
        using Fixture different = new(f.Code + 0x100000, f.Data + 0x100000, f.Root);
        Require(different.Image.UsedTablePages == f.Image.UsedTablePages && different.Image.MappedPages == f.Image.MappedPages &&
            !different.TableBytes.AsSpan().SequenceEqual(f.TableBytes), "distinct exact mappings same-count snapshot");
        var summaryOnly = new SeparatePageFaultTransitionRequest(f.Code, f.Data, f.Root, Mode(), Pat, different.Image);
        Require(Emitter.PrepareContext(f.Context, 4096, summaryOnly).ContextPrepared, "snapshot deliberately cannot bind C/D mappings");
        Require(new Span<byte>(f.Context, 4096).SequenceEqual(context), "no invented mapping association");
        Emitter.PrepareContext(f.Context, 4096, f.Request); Emitter.EmitStream(f.Stream, 8192, f.Context, 4096, f.Request);
        long before = GC.GetAllocatedBytesForCurrentThread();
        for (int i = 0; i < 8; i++)
        {
            Emitter.PrepareContext(f.Context, 4096, f.Request); Emitter.EmitStream(f.Stream, 8192, f.Context, 4096, f.Request);
            Emitter.ValidateStreamReadBack(f.Stream, 8192, f.Context, 4096, f.Request); Emitter.PrepareContext(f.Context, 4096, default);
        }
        Require(GC.GetAllocatedBytesForCurrentThread() == before, "zero managed hot-path allocation");
        f.CheckCanaries();
    }

    private static void TestMappingAndBounds(Fixture f)
    {
        HashSet<ulong> expected = new();
        for (ulong i = 0; i < 3; i++) expected.Add(f.Code + i * 4096);
        expected.Add(f.Data); expected.Add(f.Data + 4096);
        for (ulong i = 0; i < 4; i++) expected.Add(f.Data + (3 + 2 * i) * 4096);
        for (ulong i = 0; i < 43; i++) expected.Add(f.Root + i * 4096);
        HashSet<ulong> actual = new(), tables = new();
        void Walk(ulong table, int shift, ulong prefix)
        {
            Require(table >= f.Root && table < f.Root + 43 * 4096 && (table & 4095) == 0 && tables.Add(table), "independent page graph");
            for (ulong i = 0; i < 512; i++)
            {
                ulong entry = f.Tables[(table - f.Root) / 8 + i]; if (entry == 0) continue;
                ulong linear = prefix | (i << shift), target = entry & 0x000ffffffffff000;
                if (shift > 12) { Require((entry & ~0x000ffffffffff000UL) == 3, "pristine ancestor"); Walk(target, shift - 9, linear); }
                else
                {
                    bool code = linear >= f.Code && linear < f.Code + 3 * 4096;
                    Require(target == linear && (entry & ~0x000ffffffffff000UL) == (code ? 1UL : 0x8000000000000003), "identity leaf permissions");
                    Require(actual.Add(linear), "unique leaf");
                }
            }
        }
        Walk(f.Root, 39, 0);
        Require(actual.SetEquals(expected) && actual.Count == 52 && tables.Count == (int)f.Image.UsedTablePages, "exact six-range coverage");
        for (ulong i = 0; i < 43; i++) if (!tables.Contains(f.Root + i * 4096))
            for (ulong j = 0; j < 512; j++) Require(f.Tables[i * 512 + j] == 0, "zero unused arena");
        for (ulong role = 0; role < 5; role++) Require(!actual.Contains(role == 0 ? 0 : f.Data + role * 8192), "guard absence supplied image");

        // A present final leaf, including A/D, never proves absence; malformed ancestors never dereference foreign memory.
        for (int role = 1; role < 5; role++)
        {
            Machine m = new(f); ulong linear = f.Data + (ulong)role * 8192, table = f.Root;
            for (int shift = 39; shift > 12; shift -= 9) table = m.Read(table + ((linear >> shift) & 511) * 8, 64) & 0x000ffffffffff000;
            ulong slot = table + ((linear >> 12) & 511) * 8;
            foreach (ulong raw in new ulong[] { 0x40, linear | 0x8000000000000063 })
            {
                Machine corrupt = new(f); corrupt.Write(slot, raw, 64); corrupt.Run();
                Require(corrupt.Output.ToString().Contains("TEST_FAIL m02.014 absence_mismatch", StringComparison.Ordinal) && corrupt.Faults == 0, "guard malformed/present leaf");
            }
        }
        byte[] before = f.ContextBytes;
        foreach (ulong data in new ulong[] { f.Code, f.Root, (1UL << 36) - 0xa000, 0x0000800000000000 - 0xa000, ulong.MaxValue - 4095 })
            Require(!Emitter.PrepareContext(f.Context, 4096, new(f.Code, data, f.Root, Mode(), Pat, f.Image)).Succeeded, "exclusive stack top/bounds");
        Require(f.ContextBytes.AsSpan().SequenceEqual(before), "modeled boundary rejection unchanged");
    }

    // Restricted independent x64 decoder/interpreter. This interprets data using
    // ordinary managed operations; it never creates or invokes an executable pointer.
    private readonly record struct Operand(int Register, int Base = -1, int Index = -1,
        int Scale = 1, long Displacement = 0, bool Rip = false)
    {
        public static Operand Reg(int register) => new(register);
    }
    private readonly record struct Instruction(string Op, int Width, Operand Destination, Operand Source,
        ulong Immediate, ulong Next, byte Condition = 0);

    private sealed class Machine
    {
        public readonly Fixture Fixture;
        public readonly ulong[] Registers = new ulong[16];
        public readonly List<(ulong Base, byte[] Bytes)> Memory = new();
        public readonly StringBuilder Output = new();
        public readonly List<string> Events = new();
        public ulong Pc, Flags = 0x202, Cr0 = 0x80010033, Cr2 = 0xdeadbeef, Cr3 = 0x400000, Cr4 = 0x6a0, Efer = 0xd00, RawPat = Pat;
        public ulong Cs = 0x38, Ss = 0x30, Ds = 0x30, Es = 0x30, Tr;
        public ulong GdtBase, IdtBase;
        public ushort GdtLimit, IdtLimit;
        public uint MaximumLeaf = 0x80000008, AddressSizes = 0x3024;
        public ulong AccessRights = 0x00af9b00;
        public bool LarSucceeds = true, SerialReady = true, CompleteRead, Halted;
        public bool EarlyCr2, SavedRegistersChecked;
        public bool CorruptDescriptorReadBack, CorruptTrReadBack;
        public ulong? ReplaceCr2AfterEarlyRead;
        public int InitialNotReadyPolls, SerialAvailableBytes = int.MaxValue;
        public int Steps, Polls, Faults;
        public Action<Machine>? AfterFault, AfterRoot, BeforeHandler;
        private bool _ownedRoot;
        private bool _inHandler;
        private ulong _hardwareStackTop, _earlyCr2Value;
        private const ulong OldStack = 0x70000000;

        public Machine(Fixture fixture)
        {
            Fixture = fixture; Pc = fixture.Code + 4096 + 768;
            var mode = fixture.Request.Observation;
            Cr0 = mode.Cr0; Cr3 = mode.RawCr3; Cr4 = mode.RawCr4; Efer = mode.Ia32Efer;
            Flags = mode.Rflags; AddressSizes = mode.CpuidAddressSizesEax; MaximumLeaf = mode.CpuidMaximumExtendedLeaf;
            Memory.Add((fixture.Code + 4096, fixture.StreamBytes));
            Memory.Add((fixture.Data + 4096, fixture.ContextBytes));
            Memory.Add((fixture.Root, fixture.TableBytes));
            Memory.Add((OldStack, new byte[4096]));
            for (int i = 0; i < 4; i++) Memory.Add((fixture.Data + (ulong)(3 + 2 * i) * 4096, new byte[4096]));
            byte* vectors = (byte*)NativeMemory.AlignedAlloc(4096, 4096);
            byte* idt = (byte*)NativeMemory.AlignedAlloc(4096, 4096);
            try
            {
                Require(NormalizeErrorCodeNonErrorCodeInterruptFrame.Materialize(vectors, 4096, fixture.Code, fixture.Code + 4096).Succeeded, "vector fixture");
                Require(PopulateAllN256IDTVectorsOwnedGatesNever.Materialize(idt, 4096, fixture.Data, fixture.Code).Succeeded, "IDT fixture");
                Memory.Add((fixture.Code, new Span<byte>(vectors, 4096).ToArray()));
                Memory.Add((fixture.Data, new Span<byte>(idt, 4096).ToArray()));
            }
            finally { NativeMemory.AlignedFree(vectors); NativeMemory.AlignedFree(idt); }
            Registers[1] = fixture.Data + 4096; Registers[4] = OldStack + 4088;
        }

        public byte ByteAt(ulong address)
        {
            CheckCaptureBeforeContext(address);
            Require(!_ownedRoot || address < OldStack || address >= OldStack + 4096, "old stack used after CR3");
            foreach (var region in Memory)
                if (address >= region.Base && address - region.Base < (ulong)region.Bytes.Length)
                    return region.Bytes[(int)(address - region.Base)];
            throw new InvalidOperationException("synthetic unmapped read " + address.ToString("X") + " pc=" + Pc.ToString("X"));
        }
        public ulong Read(ulong address, int width)
        {
            ulong result = 0;
            for (int i = 0; i < width / 8; i++) result |= (ulong)ByteAt(address + (ulong)i) << (i * 8);
            return result;
        }
        public void Write(ulong address, ulong value, int width)
        {
            CheckCaptureBeforeContext(address);
            Require(address < Fixture.Code || address >= Fixture.Code + 3 * 4096, "write to RX code");
            Require(!_ownedRoot || address < OldStack || address >= OldStack + 4096, "write old stack after CR3");
            for (int i = 0; i < width / 8; i++)
            {
                bool found = false;
                foreach (var region in Memory)
                    if (address + (ulong)i >= region.Base && address + (ulong)i - region.Base < (ulong)region.Bytes.Length)
                    { region.Bytes[(int)(address + (ulong)i - region.Base)] = (byte)(value >> (i * 8)); found = true; break; }
                Require(found, "synthetic unmapped write " + address.ToString("X"));
            }
        }
        private void CheckCaptureBeforeContext(ulong address)
        {
            if (!_inHandler || address < Fixture.Data + 4096 || address >= Fixture.Data + 8192 || SavedRegistersChecked) return;
            Require(EarlyCr2 && Registers[4] == _hardwareStackTop - 192, "early CR2 and all GPR saves before context");
            int[] savedRegisters = [15,14,13,12,11,10,9,8,7,6,5,3,2,1];
            for (int i = 0; i < savedRegisters.Length; i++) Require(Read(Registers[4] + (ulong)i * 8, 64) == 0x4D30323134000000UL + (ulong)savedRegisters[i], "full GPR seed preserved " + savedRegisters[i]);
            Require(Read(Registers[4] + 120, 64) == 0, "original RAX saved");
            Require(Read(Registers[4] + 112, 64) == _earlyCr2Value, "early CR2 value retained in stack slot");
            SavedRegistersChecked = true;
        }
        public ulong Address(Operand op, ulong next) => unchecked((op.Rip ? next : op.Base >= 0 ? Registers[op.Base] : 0) +
            (op.Index >= 0 ? Registers[op.Index] * (ulong)op.Scale : 0) + (ulong)op.Displacement);
        private ulong Get(Operand op, int width, ulong next) => op.Register >= 0 ? Registers[op.Register] & Mask(width) : Read(Address(op, next), width);
        private void Set(Operand op, int width, ulong value, ulong next)
        {
            value &= Mask(width);
            if (op.Register < 0) Write(Address(op, next), value, width);
            else if (width >= 32) Registers[op.Register] = value;
            else Registers[op.Register] = (Registers[op.Register] & ~Mask(width)) | value;
        }
        private static ulong Mask(int width) => width == 64 ? ulong.MaxValue : (1UL << width) - 1;
        private void FlagsFor(ulong result, int width, bool carry = false)
            => Flags = (Flags & ~0x41UL) | ((result & Mask(width)) == 0 ? 0x40UL : 0) | (carry ? 1UL : 0);
        private bool Condition(byte condition) => condition switch
        { 0x82 => (Flags & 1) != 0, 0x83 => (Flags & 1) == 0, 0x84 => (Flags & 0x40) != 0,
          0x85 => (Flags & 0x40) == 0, 0x87 => (Flags & 0x41) == 0, _ => throw new InvalidOperationException("condition") };
        private void Push(ulong value) { Registers[4] -= 8; Write(Registers[4], value, 64); }
        private ulong Pop() { ulong value = Read(Registers[4], 64); Registers[4] += 8; return value; }

        public Instruction Decode(ulong at)
        {
            ulong p = at;
            byte Next() => ByteAt(p++);
            ulong Number(int count) { ulong value = 0; for (int i = 0; i < count; i++) value |= (ulong)Next() << (8 * i); return value; }
            bool operand16 = false, repeat = false;
            byte rex = 0, opcode = Next();
            while (opcode == 0x66 || opcode == 0xf3 || (opcode >= 0x40 && opcode <= 0x4f))
            { if (opcode == 0x66) operand16 = true; else if (opcode == 0xf3) repeat = true; else rex = opcode; opcode = Next(); }
            int width = (rex & 8) != 0 ? 64 : operand16 ? 16 : 32;
            (Operand Rm, Operand Reg, int Extension) ModRm()
            {
                byte m = Next(); int mod = m >> 6, low = m & 7;
                Operand reg = Operand.Reg(((m >> 3) & 7) | ((rex & 4) != 0 ? 8 : 0));
                if (mod == 3) return (Operand.Reg(low | ((rex & 1) != 0 ? 8 : 0)), reg, (m >> 3) & 7);
                int b = low | ((rex & 1) != 0 ? 8 : 0), index = -1, scale = 1;
                bool rip = false; long displacement = 0;
                if (low == 4)
                {
                    byte sib = Next(); scale = 1 << (sib >> 6);
                    index = ((sib >> 3) & 7) | ((rex & 2) != 0 ? 8 : 0);
                    if (index == 4) index = -1;
                    b = (sib & 7) | ((rex & 1) != 0 ? 8 : 0);
                    if (mod == 0 && (sib & 7) == 5) { b = -1; displacement = (int)Number(4); }
                }
                else if (mod == 0 && low == 5) { b = -1; rip = true; displacement = (int)Number(4); }
                if (mod == 1) displacement = (sbyte)Next();
                if (mod == 2) displacement = (int)Number(4);
                return (new Operand(-1, b, index, scale, displacement, rip), reg, (m >> 3) & 7);
            }
            Instruction Make(string op, Operand d = default, Operand s = default, ulong imm = 0, byte cc = 0, int bits = 0)
                => new(op, bits == 0 ? width : bits, d, s, imm, p, cc);
            if (opcode >= 0xb8 && opcode <= 0xbf) return Make("imm", Operand.Reg((opcode & 7) | ((rex & 1) != 0 ? 8 : 0)), imm: Number(width / 8));
            if (opcode >= 0x50 && opcode <= 0x57) return Make("push", Operand.Reg((opcode & 7) | ((rex & 1) != 0 ? 8 : 0)), bits: 64);
            if (opcode >= 0x58 && opcode <= 0x5f) return Make("pop", Operand.Reg((opcode & 7) | ((rex & 1) != 0 ? 8 : 0)), bits: 64);
            if (opcode is 0x6a) return Make("pushi", imm: unchecked((ulong)(long)(sbyte)Next()), bits: 64);
            if (opcode is 0x68) return Make("pushi", imm: unchecked((ulong)(long)(int)Number(4)), bits: 64);
            if (opcode is 0xe9 or 0xeb)
            { long rel = opcode == 0xe9 ? (int)Number(4) : (sbyte)Next(); return Make("jmp", imm: unchecked(p + (ulong)rel)); }
            if (opcode is 0x25 or 0x3d or 0xa9 or 0xa8)
            {
                int bytes = opcode == 0xa8 ? 1 : width == 16 ? 2 : 4;
                ulong imm = Number(bytes); if (width == 64) imm = unchecked((ulong)(long)(int)imm);
                return Make(opcode == 0x25 ? "andi" : opcode == 0x3d ? "cmpi" : "testi", Operand.Reg(0), imm: imm, bits: opcode == 0xa8 ? 8 : width);
            }
            if (opcode is 0x89 or 0x8b or 0x88 or 0x8a or 0x8d or 0x01 or 0x09 or 0x21 or 0x29 or 0x31 or 0x39 or 0x85 or 0x8c or 0x8e)
            {
                var m = ModRm();
                string op = opcode switch { 0x89 or 0x8b or 0x88 or 0x8a => "mov", 0x8d => "lea", 0x01 => "add", 0x09 => "or", 0x21 => "and",
                    0x29 => "sub", 0x31 => "xor", 0x39 => "cmp", 0x85 => "test", 0x8c => "segread", _ => "segwrite" };
                bool reverse = opcode is 0x8b or 0x8a or 0x8d;
                return Make(op, reverse ? m.Reg : m.Rm, reverse ? m.Rm : m.Reg, (ulong)m.Extension,
                    bits: opcode is 0x88 or 0x8a ? 8 : opcode is 0x8c or 0x8e ? 16 : width);
            }
            if (opcode is 0x80 or 0x81 or 0x83 or 0xc1 or 0xd3 or 0xff or 0xf7)
            {
                var m = ModRm(); int bits = opcode == 0x80 ? 8 : width;
                ulong imm = opcode is 0x80 or 0xc1 ? Next() : opcode == 0x83 ? unchecked((ulong)(long)(sbyte)Next()) : opcode == 0x81 ? Number(width == 16 ? 2 : 4) : 0;
                if (opcode == 0x81 && width == 64) imm = unchecked((ulong)(long)(int)imm);
                string op = opcode switch
                {
                    0xc1 or 0xd3 => m.Extension == 4 ? "shl" : m.Extension == 5 ? "shr" : "unknown",
                    0xff => m.Extension == 0 ? "inc" : m.Extension == 1 ? "dec" : "unknown",
                    0xf7 => m.Extension == 2 ? "not" : "unknown",
                    _ => m.Extension switch { 0 => "addi", 1 => "ori", 4 => "andi", 5 => "subi", 7 => "cmpi", _ => "unknown" }
                };
                return Make(op, m.Rm, imm: imm, cc: opcode == 0xd3 ? (byte)1 : (byte)0, bits: bits);
            }
            if (opcode == 0x0f)
            {
                byte second = Next();
                if (second >= 0x80 && second <= 0x8f) { long rel = (int)Number(4); return Make("jcc", imm: unchecked(p + (ulong)rel), cc: second); }
                if (second == 0xa2) return Make("cpuid");
                if (second == 0x32) return Make("rdmsr");
                var m = ModRm();
                return second switch
                {
                    0xb7 => Make("zx", m.Reg, m.Rm), 0x02 => Make("lar", m.Reg, m.Rm),
                    0xab => Make("bts", m.Rm, m.Reg), 0x20 => Make("crread", m.Rm, imm: (ulong)m.Extension, bits: 64),
                    0x22 => Make("crwrite", m.Rm, imm: (ulong)m.Extension, bits: 64),
                    0x01 => Make(m.Extension switch { 0 => "sgdt", 1 => "sidt", 2 => "lgdt", 3 => "lidt", _ => "unknown" }, m.Rm),
                    0x00 => Make(m.Extension == 3 ? "ltr" : m.Extension == 1 ? "str" : "unknown", m.Rm, bits: 16),
                    _ => throw new InvalidOperationException("unknown 0F opcode " + second.ToString("X"))
                };
            }
            return opcode switch
            {
                0x9c => Make("pushf"), 0x9d => Make("popf"), 0xfa => Make("cli"), 0xfc => Make("cld"),
                0xf4 => Make("hlt"), 0x90 => Make("nop"), 0xec => Make("in"), 0xee => Make("out"), 0xac => Make("lodsb"),
                0xa4 when repeat => Make("copy", bits: 8), 0xa5 when repeat => Make("copy", bits: width),
                _ => throw new InvalidOperationException("unknown opcode " + opcode.ToString("X") + " at " + at.ToString("X"))
            };
        }

        public void Run()
        {
            while (!Halted)
            {
                Require(++Steps < 2000000, "bounded synthetic instruction count");
                if (Pc == Fixture.Code + 4096) { BeforeHandler?.Invoke(this); _inHandler = true; }
                // Assert live loop values independently of the emitter's byte builder.
                ulong offset = Pc - Fixture.Code - 4096;
                if (offset == 2168)
                    Require(Registers[8] < 4 && Registers[1] == 39 - Registers[8] * 9 &&
                        Registers[9] >= Fixture.Root && Registers[9] < Fixture.Root + 43 * 4096 && (Registers[9] & 4095) == 0 &&
                        Registers[14] == Fixture.Root && Registers[13] == Fixture.Root + 43 * 4096, "walk register lifetimes and pre-dereference bounds");
                if (offset == 3016)
                    Require(Registers[15] == Fixture.Data + 4096 && Registers[12] == Fixture.Data + 4096 + 1152 &&
                        Registers[8] is >= 1 and <= 57 && Registers[3] == Fixture.Code + 4096 + 6160 + (57 - Registers[8]) * 4,
                        "formatter source/cursor/count lifetimes");
                ulong before = Pc;
                Instruction i = Decode(Pc); Pc = i.Next;
                ulong a = 0, b = 0, value;
                if (i.Op is "mov" or "add" or "sub" or "or" or "and" or "xor" or "cmp" or "test" or "bts")
                {
                    if (i.Op == "mov" && i.Source.Register < 0 && Address(i.Source, i.Next) == 0)
                    {
                        Require(before == Fixture.Code + 4096 + 2841 && Registers[0] == 0 && Flags == 0x46, "exact fault instruction/seed flags");
                        if (!CompleteRead) { DeliverFault(before); continue; }
                        b = 0;
                    }
                    else b = Get(i.Source, i.Width, i.Next);
                    if (i.Op != "mov") a = Get(i.Destination, i.Width, i.Next);
                }
                else if (i.Op is "addi" or "subi" or "andi" or "ori" or "cmpi" or "testi" or "inc" or "dec" or "not" or "shl" or "shr")
                { a = Get(i.Destination, i.Width, i.Next); b = i.Immediate; }
                switch (i.Op)
                {
                    case "imm": Set(i.Destination, i.Width, i.Immediate, i.Next); break;
                    case "mov": Set(i.Destination, i.Width, b, i.Next); break;
                    case "zx": Set(i.Destination, i.Width, Get(i.Source, 16, i.Next), i.Next); break;
                    case "lea": Set(i.Destination, i.Width, Address(i.Source, i.Next), i.Next); break;
                    case "push": Push(Get(i.Destination, 64, i.Next)); break;
                    case "pop": Set(i.Destination, 64, Pop(), i.Next); break;
                    case "pushi": Push(i.Immediate); break;
                    case "pushf": Push(Flags); break;
                    case "popf": Flags = Pop(); break;
                    case "jmp": Pc = i.Immediate; break;
                    case "jcc": if (Condition(i.Condition)) Pc = i.Immediate; break;
                    case "cmp": case "cmpi": FlagsFor(unchecked(a - b), i.Width, a < (b & Mask(i.Width))); break;
                    case "test": case "testi": FlagsFor(a & b, i.Width); break;
                    case "and": case "andi": value = a & b; Set(i.Destination, i.Width, value, i.Next); FlagsFor(value, i.Width); break;
                    case "or": case "ori": value = a | b; Set(i.Destination, i.Width, value, i.Next); FlagsFor(value, i.Width); break;
                    case "xor": value = a ^ b; Set(i.Destination, i.Width, value, i.Next); FlagsFor(value, i.Width); break;
                    case "add": case "addi": value = unchecked(a + b); Set(i.Destination, i.Width, value, i.Next); FlagsFor(value, i.Width); break;
                    case "sub": case "subi": value = unchecked(a - b); Set(i.Destination, i.Width, value, i.Next); FlagsFor(value, i.Width, a < b); break;
                    case "inc": case "dec": value = unchecked(i.Op == "inc" ? a + 1 : a - 1); Set(i.Destination, i.Width, value, i.Next); FlagsFor(value, i.Width, (Flags & 1) != 0); break;
                    case "not": Set(i.Destination, i.Width, ~a, i.Next); break;
                    case "shl": case "shr":
                        int shift = (int)(i.Condition == 1 ? Registers[1] : b) & (i.Width == 64 ? 63 : 31);
                        value = i.Op == "shl" ? a << shift : a >> shift; Set(i.Destination, i.Width, value, i.Next); FlagsFor(value, i.Width); break;
                    case "bts": ulong bit = 1UL << ((int)b & 63); Flags = (Flags & ~1UL) | ((a & bit) != 0 ? 1UL : 0); Set(i.Destination, i.Width, a | bit, i.Next); break;
                    case "cli": Flags &= ~0x200UL; Events.Add("cli"); break;
                    case "cld": Flags &= ~0x400UL; break;
                    case "hlt": Require((Flags & 0x200) == 0, "terminal IF disabled"); Halted = true; break;
                    case "nop": break;
                    case "cpuid":
                        uint leaf = (uint)Registers[0]; Registers[0] = leaf == 0x80000000 ? MaximumLeaf : AddressSizes;
                        Registers[3] = 0x89abcdef; Registers[1] = 0x76543210; Registers[2] = 0xfedcba98; break;
                    case "rdmsr":
                        Require((Cs & 3) == 0 && Events.Contains("lar"), "CPL/CS checked before MSR");
                        value = (uint)Registers[1] == 0x277 ? RawPat : (uint)Registers[1] == 0xc0000080 ? Efer : throw new InvalidOperationException("MSR");
                        Registers[0] = (uint)value; Registers[2] = value >> 32; break;
                    case "lar": Set(i.Destination, i.Width, AccessRights, i.Next); Flags = (Flags & ~0x40UL) | (LarSucceeds ? 0x40UL : 0); Events.Add("lar"); break;
                    case "segread": Set(i.Destination, 16, i.Immediate switch { 0 => Es, 1 => Cs, 2 => Ss, 3 => Ds, _ => throw new InvalidOperationException("segment") }, i.Next); break;
                    case "segwrite": value = Get(i.Destination, 16, i.Next); if (i.Immediate == 0) Es = value; else if (i.Immediate == 2) Ss = value; else if (i.Immediate == 3) Ds = value; else throw new InvalidOperationException("segment write"); break;
                    case "crread":
                        Require((Cs & 3) == 0 && Events.Contains("lar"), "CPL/CS checked before control register");
                        if (i.Immediate == 2) { Require(Registers[4] == _hardwareStackTop - 72 && Read(Registers[4], 64) == 0, "original RAX before early CR2"); EarlyCr2 = true; _earlyCr2Value = Cr2; Events.Add("cr2"); }
                        Set(i.Destination, 64, i.Immediate switch { 0 => Cr0, 2 => Cr2, 3 => Cr3, 4 => Cr4, _ => throw new InvalidOperationException("CR read") }, i.Next);
                        if (i.Immediate == 2 && ReplaceCr2AfterEarlyRead.HasValue) Cr2 = ReplaceCr2AfterEarlyRead.Value;
                        break;
                    case "crwrite":
                        value = Get(i.Destination, 64, i.Next);
                        if (i.Immediate == 4) { Require((value ^ Cr4) == (Cr4 & 0x80), "clear only PGE"); Cr4 = value; Events.Add("cr4"); }
                        else if (i.Immediate == 3)
                        {
                            Require(value == Fixture.Root && Registers[4] == Fixture.Data + 0xa000 && Tr == 24 && IdtBase == Fixture.Data && GdtBase == Fixture.Data + 4120, "descriptor/stack before CR3");
                            Cr3 = value; _ownedRoot = true; Events.Add("cr3"); AfterRoot?.Invoke(this);
                        }
                        else throw new InvalidOperationException("forbidden CR write"); break;
                    case "lgdt": case "lidt":
                        ulong address = Address(i.Destination, i.Next); ushort limit = (ushort)Read(address, 16); ulong table = Read(address + 2, 64);
                        if (i.Op == "lgdt") { GdtBase = table; GdtLimit = limit; } else { IdtBase = table; IdtLimit = limit; }
                        Events.Add(i.Op); break;
                    case "sgdt": case "sidt":
                        address = Address(i.Destination, i.Next); Write(address, i.Op == "sgdt" ? GdtLimit : IdtLimit, 16); Write(address + 2, (i.Op == "sgdt" ? GdtBase : IdtBase) ^ (CorruptDescriptorReadBack ? 1UL : 0), 64); break;
                    case "ltr": Tr = Get(i.Destination, 16, i.Next); Require(Tr == 24 && Read(GdtBase + 29, 8) == 0x89, "LTR available TSS"); Write(GdtBase + 29, 0x8b, 8); Events.Add("ltr"); break;
                    case "str": Set(i.Destination, 16, CorruptTrReadBack ? Tr ^ 8 : Tr, i.Next); break;
                    case "copy":
                        Require(Registers[1] <= 2053 && (Flags & 0x400) == 0, "bounded forward copy");
                        while (Registers[1] != 0) { value = Read(Registers[6], i.Width); Write(Registers[7], value, i.Width); Registers[6] += (ulong)i.Width / 8; Registers[7] += (ulong)i.Width / 8; Registers[1]--; }
                        break;
                    case "in": Require((Registers[2] & 65535) == 0x3fd, "COM1 THRE port"); Polls++; Registers[0] = (Registers[0] & ~255UL) | (SerialReady && Polls > InitialNotReadyPolls && Output.Length < SerialAvailableBytes ? 0x20UL : 0); break;
                    case "lodsb": Require((Flags & 0x400) == 0, "serial forward"); Registers[0] = (Registers[0] & ~255UL) | Read(Registers[6]++, 8); break;
                    case "out": Require((Registers[2] & 65535) == 0x3f8, "COM1 data port"); Output.Append((char)(Registers[0] & 255)); break;
                    default: throw new InvalidOperationException("unsupported decoder operation " + i.Op + " at " + before.ToString("X"));
                }
            }
        }

        private void DeliverFault(ulong rip)
        {
            Require(_ownedRoot && ++Faults == 1, "one synthetic owned-root fault");
            ulong oldRsp = Registers[4], oldSs = Ss, oldCs = Cs, savedFlags = Flags | 0x10000;
            _hardwareStackTop = Read(Fixture.Data + 4096 + 108, 64); Registers[4] = _hardwareStackTop;
            Push(oldSs); Push(oldRsp); Push(savedFlags); Push(oldCs); Push(rip); Push(0);
            Cs = 8; Ss = 0; Cr2 = 0; Flags &= ~0x200UL;
            Pc = Fixture.Code + 14 * 16; Events.Add("fault");
            AfterFault?.Invoke(this);
        }
    }

    private static X64PagingModePreflightObservation Mode(byte width = 36, ulong cr0 = 0x80010033,
        ulong cr4 = 0x6a0, ulong efer = 0xd00, ulong flags = 0x202, uint leaf = 0x80000008)
        => X64PagingModePreflightObservation.CreateObserved(cr0, 0x00400000, cr4, efer, leaf, (uint)(0x3000 | width), flags);
    private static PhysicalAddress Address(ulong value)
    {
        Require(PhysicalAddress.TryCreateAlignedFromHardwareRegisterBoundary(value, 4096, out var address) == AddressValueStatus.Success, "address");
        return address;
    }
    private static void Require(bool condition, string reason)
    { if (!condition) throw new InvalidOperationException(reason); }
    private static ushort U16(byte[] bytes, int at) => BinaryPrimitives.ReadUInt16LittleEndian(bytes.AsSpan(at));
    private static uint U32(byte[] bytes, int at) => BinaryPrimitives.ReadUInt32LittleEndian(bytes.AsSpan(at));
    private static ulong U64(byte[] bytes, int at) => BinaryPrimitives.ReadUInt64LittleEndian(bytes.AsSpan(at));

    private sealed class Fixture : IDisposable
    {
        private readonly byte* _stream, _context;
        public readonly ulong* Tables;
        public readonly ulong Code, Data, Root;
        public readonly SeparatePageFaultPageTableResult Image;
        public SeparatePageFaultTransitionRequest Request;
        public byte* Stream => _stream + 8;
        public byte* Context => _context + 8;
        public byte[] StreamBytes => new Span<byte>(Stream, 8192).ToArray();
        public byte[] ContextBytes => new Span<byte>(Context, 4096).ToArray();
        public byte[] TableBytes => new Span<byte>(Tables, 43 * 4096).ToArray();
        public Fixture(ulong code = C, ulong data = D, ulong root = T, byte width = 36)
        {
            Code = code; Data = data; Root = root;
            _stream = (byte*)NativeMemory.AlignedAlloc(8192 + 16, 8);
            _context = (byte*)NativeMemory.AlignedAlloc(4096 + 16, 8);
            Tables = (ulong*)NativeMemory.AlignedAlloc(43 * 4096, 4096);
            new Span<byte>(_stream, 8192 + 16).Fill(0xa5);
            new Span<byte>(_context, 4096 + 16).Fill(0xa5);
            new Span<byte>(Tables, 43 * 4096).Clear();
            X64PatMappingToken.TryCreate(Pat, 0, 255, 255, out var token);
            var conditions = X64PageTableEncodingPreconditions.CreateObserved(width, true, true, false, false, token);
            SeparatePageFaultMappingRange* ranges = stackalloc SeparatePageFaultMappingRange[6];
            ranges[0] = new(code, 3, PageProtection.Read | PageProtection.Execute);
            ranges[1] = new(data, 2, PageProtection.Read | PageProtection.Write);
            for (int i = 2; i < 6; i++) ranges[i] = new(data + (ulong)(i * 2 - 1) * 4096, 1, PageProtection.Read | PageProtection.Write);
            X64PageTableImagePage* pages = stackalloc X64PageTableImagePage[43];
            Image = SeparateDisabledDefaultPageFaultPageTables.Build(new(Address(root), 43, conditions), ranges, 6, 6, pages, 43, Tables, 43 * 512);
            Require(Image.Succeeded && Image.MappedPages == 52, "real six-range image");
            Request = new(code, data, root, Mode(width), Pat, Image);
        }
        public void Prepare()
        {
            var context = Emitter.PrepareContext(Context, 4096, Request);
            Require(context.ContextPrepared, "prepare context " + context.Status + " offset=" + context.FirstMismatchOffset);
            var stream = Emitter.EmitStream(Stream, 8192, Context, 4096, Request);
            Require(stream.StreamEmitted, "emit stream " + stream.Status);
            Require(!stream.GuestExecution && !stream.HardwareStateCaptured && !stream.FaultTriggered && !stream.PostExitBootServices, "no hardware claims");
        }
        public void CheckCanaries()
        {
            for (int i = 0; i < 8; i++) Require(_stream[i] == 0xa5 && _stream[8192 + 8 + i] == 0xa5 &&
                _context[i] == 0xa5 && _context[4096 + 8 + i] == 0xa5, "canaries");
        }
        public void Dispose()
        { NativeMemory.AlignedFree(_stream); NativeMemory.AlignedFree(_context); NativeMemory.AlignedFree(Tables); }
    }
}
