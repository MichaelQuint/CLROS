using System.Buffers.Binary;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Text;
using System.Diagnostics;
using System.Reflection.Metadata;
using System.Reflection.PortableExecutable;
using OS.Boot;
using OS.Kernel.Diagnostics;
using OS.Kernel.Memory;
using Emitter = OS.Kernel.Diagnostics.DoubleFaultISTPathSilentRebootOrTriple;

internal static unsafe class DoubleFaultISTPathSilentRebootOrTripleTests
{
    private const ulong Pat = 0x0007040600070406;
    private const ulong C = 0x0000000100000000, D = 0x0000000200000000, T = 0x0000000300000000;

    [ModuleInitializer]
    internal static void RunAll()
    {
        Console.WriteLine("TEST_START m02.015");
        try
        {
            using Fixture fixture = new();
            fixture.Prepare();
            Require(!default(DoubleFaultIstResult).Succeeded, "default fails closed");
            Require(!RuntimeHelpers.IsReferenceOrContainsReferences<DoubleFaultIstRequest>() &&
                !RuntimeHelpers.IsReferenceOrContainsReferences<DoubleFaultIstResult>(), "POD request/result");
            Group("TestPreparedBytes", () => TestPreparedBytes(fixture));
            Group("TestStaticBytes", () => TestStaticBytes(fixture));
            Group("TestSyntheticExecution", () => TestSyntheticExecution(fixture));
            Group("TestSyntheticFailures", () => TestSyntheticFailures(fixture));
            Group("TestRequestsAndCorruption", () => TestRequestsAndCorruption(fixture));
            Group("TestMappingAndBounds", () => TestMappingAndBounds(fixture));
            Group("TestArmedAndSynthetic", () => TestArmedAndSynthetic(fixture));
            Group("nostdlib", CheckNoStdLibCompilation);
            Console.WriteLine("CLROS_M02_015_HOST_READY nonExecuting=true guestIntegrated=false");
        }
        catch (Exception exception)
        {
            Console.WriteLine("TEST_FAIL m02.015 " + exception.Message);
            throw;
        }
        Console.WriteLine("TEST_PASS m02.015");
    }

    private static void Group(string name, Action action)
    {
        Console.WriteLine("TEST_START m02.015." + name);
        try { action(); Console.WriteLine("TEST_PASS m02.015." + name); }
        catch (Exception error) { Console.WriteLine("TEST_FAIL m02.015." + name + " " + error.GetType().Name); throw; }
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
        Require(f.StreamBytes.AsSpan(2698, 3).SequenceEqual(new byte[] { 0x48, 0x8b, 0x10 }), "exact null read");
        Require(Emitter.ValidateStreamReadBack(f.Buffers, f.Request).ReadBackValidated, "readback");
        byte[] expected = new byte[4096];
        void W16(int offset, ushort value) => BinaryPrimitives.WriteUInt16LittleEndian(expected.AsSpan(offset), value);
        void W64(int offset, ulong value) => BinaryPrimitives.WriteUInt64LittleEndian(expected.AsSpan(offset), value);
        W16(0, 39); W64(2, f.Data + 4120); W64(32, 0x00af9a000000ffff); W64(40, 0x00cf92000000ffff);
        ulong expectedTss = f.Data + 4160;
        W16(48, 103); W16(50, (ushort)expectedTss); expected[52] = (byte)(expectedTss >> 16); expected[53] = 0x89;
        expected[55] = (byte)(expectedTss >> 24); BinaryPrimitives.WriteUInt32LittleEndian(expected.AsSpan(56), (uint)(expectedTss >> 32));
        W64(100, f.Data + 0x4000); W64(108, f.Data + 0x6000); W64(116, f.Data + 0x8000); W16(166, 104);
        W16(176, 4095); W64(178, f.Data);
        ulong[] geometry = [f.Code, f.Data, f.Root, f.Code + 4096 + 2698, f.Request.Observation.Cr0,
            f.Request.Observation.RawCr3, f.Request.Observation.RawCr4, f.Request.Observation.Ia32Efer, f.Request.Pat,
            f.Request.Observation.PhysicalAddressWidth, ((1UL << f.Request.Observation.PhysicalAddressWidth) - 1) & ~4095UL,
            f.Request.Observation.RawCr4 & ~0x80UL];
        for (int i = 0; i < geometry.Length; i++) W64(256 + i * 8, geometry[i]);
        Require(context.AsSpan().SequenceEqual(expected), "every context byte independently encoded");
        static ulong Fnv(byte[] bytes) { ulong h = 14695981039346656037; foreach (byte b in bytes) h = unchecked((h ^ b) * 1099511628211); return h; }
        var readback = Emitter.ValidateStreamReadBack(f.Buffers, f.Request);
        Require(readback.BytesWritten == 0 && readback.BytesCompared == 8192 && readback.FirstMismatchOffset == -1 &&
            readback.ContextFingerprint == Fnv(context) && readback.StreamFingerprint == Fnv(f.StreamBytes), "independent fingerprints/counts");
        f.CheckCanaries();
    }

    private static void TestSyntheticExecution(Fixture f)
    {
        Machine machine = new(f); string expected = ExpectedTranscript(machine); machine.Run();
        Require(machine.Output.ToString() == expected && expected.Length == 2643, "synthetic transcript: " + machine.Output);
        Require(machine.Faults == 1 && machine.EarlyCr2 && machine.SavedRegistersChecked && machine.Polls == 2643, "raw capture and serial");
        Require(machine.HardwareWords == 6 && machine.NormalizerWords == 2 && machine.CaptureWords == 16 &&
            (machine.HardwareWords + machine.NormalizerWords + machine.CaptureWords) * 8 == 192, "independent 48+16+128 frame derivation");
        Require(machine.Events.IndexOf("lgdt") < machine.Events.IndexOf("ltr") && machine.Events.IndexOf("ltr") < machine.Events.IndexOf("lidt") &&
            machine.Events.IndexOf("lidt") < machine.Events.IndexOf("cr3") && machine.Events.IndexOf("cr3") < machine.Events.IndexOf("pf-delivery") &&
            machine.Events.IndexOf("pf-delivery") < machine.Events.IndexOf("np-delivery") && machine.Events.IndexOf("np-delivery") < machine.Events.IndexOf("fault") &&
            machine.Events.IndexOf("fault") < machine.Events.IndexOf("cr2"), "descriptor, delivery escalation and raw capture order");
        Require(Emitter.AuditSyntheticObservation(f.Request, Observation(machine)).Succeeded, "synthetic captured scalar audit");
        Machine lateCr2 = new(f) { ReplaceCr2AfterEarlyRead = 0x123456789ABCDEF0 }; lateCr2.Run();
        Require(lateCr2.Output.ToString() == expected, "early CR2 retained");
        Machine delayed = new(f) { InitialNotReadyPolls = 65535 }; delayed.Run();
        Require(delayed.Output.ToString() == expected && delayed.Polls == 65535 + 2643, "last allowed THRE poll");
        Machine varied = new(f) { SavedRip = 0, SavedCs = ulong.MaxValue, SavedRsp = 0x123,
            SavedSs = ulong.MaxValue, SavedFlags = 0, DiagnosticCr2 = 0 };
        for (int i = 0; i < 16; i++) varied.DiagnosticRegisters[i] = ulong.MaxValue - (ulong)i;
        string variedExpected = ExpectedTranscript(varied); varied.Run();
        Require(varied.Output.ToString() == variedExpected && Emitter.AuditSyntheticObservation(f.Request, Observation(varied)).Succeeded,
            "undefined saved state and untrusted R15 vary independently of trusted context/current IST");
        var original = f.Request;
        f.Request = new(f.Code, f.Data, f.Root, Mode(cr4: 0x620), Pat, f.Image); f.Prepare();
        Machine noPge = new(f); string noPgeExpected = ExpectedTranscript(noPge); noPge.Run();
        Require(noPge.Output.ToString() == noPgeExpected && !noPge.Events.Contains("cr4"), "PGE-clear path");
        f.Request = original; f.Prepare();
        foreach ((ulong code, ulong data, ulong tables, byte width) in new[]
        { (0x100000UL, 0x200000UL, 0x300000UL, (byte)36),
          (0x00007fffffffc000UL, 0x00007ffffff00000UL, 0x00007fffffe00000UL, (byte)48) })
        {
            using Fixture other = new(code, data, tables, width); other.Prepare(); TestStaticBytes(other);
            Machine relocated = new(other); string text = ExpectedTranscript(relocated); relocated.Run();
            Require(relocated.Output.ToString() == text && Emitter.AuditSyntheticObservation(other.Request, Observation(relocated)).Succeeded, "full-width relocation");
        }
    }

    private static string ExpectedTranscript(Machine m)
    {
        var f = m.Fixture;
        static string H(ulong x) => "0x" + x.ToString("X16", System.Globalization.CultureInfo.InvariantCulture);
        StringBuilder text = new();
        text.Append("TEST_START m02.015\r\n");
        text.Append($"M02_DF_LAYOUT code={H(f.Code)} data={H(f.Data)} tables={H(f.Root)} faultRip={H(f.Code + 4096 + 2698)} codePages=3 dataPages=10 tablePages=43 mappedPages=52\r\n");
        text.Append($"M02_DF_MODE firmwareCs={H(m.Cs)} cr0={H(m.Cr0)} firmwareCr3={H(m.Cr3)} cr4Before={H(m.Cr4)} efer={H(m.Efer)} pat={H(m.RawPat)} physicalBits={H(m.AddressSizes & 255)}\r\n");
        text.Append($"M02_DF_ROOT cr3={H(f.Root)} expectedCr3={H(f.Root)} cr4={H(m.Cr4 & ~0x80UL)} expectedCr4={H(m.Cr4 & ~0x80UL)}\r\n");
        string[] roles = ["zero", "df", "pf", "nmi", "transition"];
        for (int role = 0; role < 5; role++)
        {
            ulong linear = role == 0 ? 0 : f.Data + (ulong)role * 8192;
            ulong[] entries = new ulong[4]; ulong table = f.Root; int levels = 0;
            for (int level = 0; level < 4; level++)
            {
                entries[level] = m.Read(table + ((linear >> (39 - level * 9)) & 511) * 8, 64); levels++;
                if (entries[level] == 0) break;
                Require((entries[level] & 3) == 3 && level < 3, "independent absence fixture"); table = entries[level] & 0x000ffffffffff000;
            }
            text.Append($"M02_DF_ABSENT role={roles[role]} linear={H(linear)} levelsRead={H((ulong)levels)} pml4e={H(entries[0])} pdpte={H(entries[1])} pde={H(entries[2])} pte={H(entries[3])}\r\n");
        }
        text.Append($"M02_DF_FRAME vector={H(8)} hasErrorCode={H(1)} error={H(0)} cr2={H(m.DiagnosticCr2)} rip={H(m.SavedRip)} cs={H(m.SavedCs)} rflags={H(m.SavedFlags)} savedRsp={H(m.SavedRsp)} savedSs={H(m.SavedSs)} handlerRsp={H(f.Data + 0x4000 - 192)} handlerSs={H(0)} cr3={H(f.Root)} handlerCs={H(8)}\r\n");
        text.Append($"M02_DF_DESCRIPTORS gdtrLimit={H(39)} gdtrBase={H(f.Data + 4120)} tr={H(24)} idtrLimit={H(4095)} idtrBase={H(f.Data)} tssAccess={H(0x8b)} ist1={H(f.Data + 0x4000)} dfGateLow={H(ExpectedGate(f.Code, 8, false))} dfGateHigh={H(ExpectedGate(f.Code, 8, true))} pfGateLow={H(ExpectedGate(f.Code, 14, false))} pfGateHigh={H(ExpectedGate(f.Code, 14, true))}\r\n");
        ulong[] g = m.DiagnosticRegisters;
        text.Append($"M02_DF_GPRS rax={H(g[0])} rbx={H(g[3])} rcx={H(g[1])} rdx={H(g[2])} rbp={H(g[5])} rsi={H(g[6])} rdi={H(g[7])} r8={H(g[8])} r9={H(g[9])} r10={H(g[10])} r11={H(g[11])} r12={H(g[12])} r13={H(g[13])} r14={H(g[14])} r15={H(g[15])}\r\n");
        text.Append("CLROS_M02_015_READY vector=8 hardwareErrorCode=true errorCode=0 ist=1 pageZeroAbsent=true guardPagesAbsent=4 savedState=diagnosticOnly terminalHalt=true postExitBootServices=false\r\nTEST_PASS m02.015\r\n");
        return text.ToString();
    }
    private static ulong ExpectedGate(ulong code, int vector, bool high)
    {
        ulong target = code + (ulong)vector * 16;
        return high ? target >> 32 : (target & 65535) | (8UL << 16) |
            ((vector == 8 ? 1UL : 2UL) << 32) | ((vector == 14 ? 0x0eUL : 0x8eUL) << 40) | (((target >> 16) & 65535) << 48);
    }

    private static void TestStaticBytes(Fixture f)
    {
        Machine m = new(f); byte[] bytes = f.StreamBytes; bool[] covered = new bool[8192];
        int[] starts = [0, 768, 2048, 2104, 2168, 2528, 2688, 2944, 3016, 3064, 3200, 3232, 3264,
            3328, 3360, 3392, 3424, 3456, 3488, 3520, 3584, 3616];
        int[] ends = [768, 2048, 2104, 2168, 2528, 2688, 2944, 3016, 3064, 3200, 3232, 3264, 3328,
            3360, 3392, 3424, 3456, 3488, 3520, 3584, 3600, 4096];
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
        Require(fields.Count == 84, "84 numeric slots");
        string template = System.Text.RegularExpressions.Regex.Replace(expected, "0x[0-9A-F]{16}", "0x0000000000000000");
        Data(4096, Encoding.ASCII.GetBytes(template));
        int[] sources = [256,264,272,280,352,360,368,376,384,392,400,408,272,416,344,512,520,528,536,544,552,576,584,592,600,608,616,640,648,656,664,672,680,704,712,720,728,736,744,768,776,784,792,800,808,960,968,976,944,984,992,1000,1008,1016,1024,1032,1040,1048,448,456,464,472,480,488,496,1056,1064,1072,1080,952,920,936,928,912,904,896,888,880,872,864,856,848,840,832];
        byte[] table = new byte[84 * 4];
        for (int i = 0; i < 84; i++)
        {
            BinaryPrimitives.WriteUInt16LittleEndian(table.AsSpan(i * 4), (ushort)sources[i]);
            BinaryPrimitives.WriteUInt16LittleEndian(table.AsSpan(i * 4 + 2), (ushort)(fields[i].Index + 2));
            Require(sources[i] + 8 <= 1088 && fields[i].Index + 18 <= 2643, "bounded source/destination field");
        }
        Data(7104, table); Data(7488, Encoding.ASCII.GetBytes("0123456789ABCDEF"));
        string[] reasons = ["preflight_failed", "descriptor_mismatch", "root_mismatch", "absence_mismatch", "frame_mismatch", "unexpected_completion", "reentered"];
        int failure = 7520;
        foreach (string reason in reasons)
        { byte[] literal = Encoding.ASCII.GetBytes("M02_DOUBLE_FAULT_FAILED\r\nTEST_FAIL m02.015 " + reason + "\r\n"); Data(failure, literal); failure += literal.Length; }
        for (int i = 0; i < bytes.Length; i++) if (!covered[i]) Require(bytes[i] == 0xf4, "data gap padding " + i);
        if (f.Code == C && f.Data == D && f.Root == T)
        {
            string evidence = Path.Combine(FindRoot(), "artifacts", "issue55", "byte-audit", Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(evidence);
            File.WriteAllBytes(Path.Combine(evidence,"stream.bin"),bytes);
            File.WriteAllBytes(Path.Combine(evidence,"context.bin"),f.ContextBytes);
            File.WriteAllBytes(Path.Combine(evidence,"slots.bin"),new Span<byte>(f.Slots,4096).ToArray());
            File.WriteAllBytes(Path.Combine(evidence,"armed-idt.bin"),new Span<byte>(f.Idt,4096).ToArray());
            File.WriteAllBytes(Path.Combine(evidence,"synthetic-transcript.txt"),Encoding.ASCII.GetBytes(expected));
            var records = decoded.OrderBy(x=>x.Key).Select(x=>new { Region="stream", Offset=(int)(x.Key-f.Code-4096),
                Hex=Convert.ToHexString(bytes.AsSpan((int)(x.Key-f.Code-4096),(int)(x.Value.Next-x.Key))), Operation=x.Value.Op }).ToList();
            for (int vector=0;vector<256;vector++)
            {
                ulong cursor=f.Code+(ulong)vector*16, end=cursor+16;
                while(cursor<end)
                {
                    Instruction i=m.Decode(cursor); Require(i.Next<=end && i.Op is "pushi" or "jmp" or "nop", "closed normalizer instructions");
                    if(i.Op=="jmp") Require(i.Immediate==f.Code+4096,"normalizer local target");
                    records.Add(new { Region="slots", Offset=(int)(cursor-f.Code),
                        Hex=Convert.ToHexString(new ReadOnlySpan<byte>(f.Slots+(int)(cursor-f.Code),(int)(i.Next-cursor))), Operation=i.Op });
                    cursor=i.Next;
                }
            }
            File.WriteAllText(Path.Combine(evidence,"decoded.json"),System.Text.Json.JsonSerializer.Serialize(records),new UTF8Encoding(false));
            Console.WriteLine("M02_DF_HOST_BYTES streamInstructions="+decoded.Count+" streamInstructionBytes="+decoded.Sum(x=>(long)(x.Value.Next-x.Key))+" transcriptBytes=2643 fields=84 path="+evidence);
        }
        Require(failure == 7943 && decoded.Count > 300 && decoded.Count < 650, "measured code/data budget");
    }

    private static void TestSyntheticFailures(Fixture f)
    {
        void Reject(Action<Machine> alter, string reason, bool silent = false)
        {
            Machine m = new(f); alter(m); m.Run(); string text = m.Output.ToString();
            Require(!text.Contains("CLROS_M02_015_READY", StringComparison.Ordinal) && !text.Contains("TEST_PASS", StringComparison.Ordinal) &&
                (silent ? text.Length == 0 : text.Contains("TEST_FAIL m02.015 " + reason + "\r\n", StringComparison.Ordinal)), "synthetic rejected " + reason);
        }
        Reject(m => m.Registers[1]++, "preflight_failed");
        foreach (ulong mask in new ulong[] { 1, 0x10000, 0x80000000 }) Reject(m => m.Cr0 ^= mask, "preflight_failed");
        Reject(m => m.Cr3 ^= 4096, "preflight_failed");
        foreach (ulong mask in new ulong[] { 0x20, 0x1000, 0x20000, 0x800000, 0x1000000 }) Reject(m => m.Cr4 ^= mask, "preflight_failed");
        foreach (ulong mask in new ulong[] { 0x100, 0x400, 0x800 }) Reject(m => m.Efer ^= mask, "preflight_failed");
        foreach (ulong flag in new ulong[] { 0x302, 0x602, 0 }) Reject(m => m.Flags = flag, "preflight_failed");
        foreach (uint width in new uint[] { 0, 35, 53, 255 }) Reject(m => m.AddressSizes = width, "preflight_failed");
        Reject(m => m.MaximumLeaf = 0x80000007, "preflight_failed"); Reject(m => m.RawPat ^= 1, "preflight_failed");
        Reject(m => m.Cs |= 3, "preflight_failed"); Reject(m => m.LarSucceeds = false, "preflight_failed");
        foreach (ulong mask in new ulong[] { 0x200000, 0x800000, 0x8000, 0x1000, 0x2000, 0x200, 0x800, 0x400000 }) Reject(m => m.AccessRights ^= mask, "preflight_failed");
        Reject(m => m.Write(f.Data + 4096 + 432, 1, 64), "reentered"); Reject(m => m.CompleteRead = true, "unexpected_completion");
        Reject(m => m.SerialReady = false, "serial_timeout", true);
        Machine exhausted = new(f) { SerialReady = false }; exhausted.Run(); Require(exhausted.Polls == 65536, "finite per-byte poll bound");
        Machine partial = new(f) { SerialAvailableBytes = 10 }; partial.Run(); Require(partial.Output.Length == 10 && partial.Polls == 65546, "later byte finite timeout");
        Reject(m => m.AfterFault = x => x.Cr3 ^= 4096, "frame_mismatch");
        Reject(m => m.AfterFault = x => x.Write(f.Data + 4096 + 440, 1, 64), "reentered");
        Reject(m => m.AfterRoot = x => x.Cr3 ^= 4096, "root_mismatch");
        Reject(m => m.AfterRoot = x => x.Cr4 |= 0x80, "root_mismatch");
        Reject(m => m.AfterRoot = x => x.Cr0 ^= 0x10000, "root_mismatch");
        Reject(m => m.AfterRoot = x => x.Efer ^= 0x800, "root_mismatch");
        Reject(m => m.AfterRoot = x => x.RawPat ^= 1, "root_mismatch");
        Reject(m => m.CorruptDescriptorReadBack = true, "descriptor_mismatch"); Reject(m => m.CorruptTrReadBack = true, "descriptor_mismatch");
        Reject(m => m.AfterFault = x => x.Ss = 16, "frame_mismatch"); Reject(m => m.AfterFault = x => x.Cs = 16, "frame_mismatch");
        Reject(m => m.BeforeHandler = x => x.Write(x.Registers[4], 14, 64), "frame_mismatch");
        Reject(m => m.BeforeHandler = x => x.Write(x.Registers[4] + 8, 0, 64), "frame_mismatch");
        foreach (ulong error in new ulong[] { 1, 1UL << 63, ulong.MaxValue }) Reject(m => m.AfterFault = x => x.Write(x.Registers[4], error, 64), "frame_mismatch");
        Reject(m => m.Write(f.Data + 4096 + 100, f.Data + 0x4000 - 16, 64), "frame_mismatch");
        foreach (int offset in new[] { 128, 136, 224, 232 })
            Reject(m => m.AfterFault = x => x.Write(f.Data + (ulong)offset, x.Read(f.Data + (ulong)offset, 64) ^ 1, 64), "frame_mismatch");
        Reject(m => m.AfterFault = x => x.GdtBase += 16, "frame_mismatch");
        Reject(m => m.AfterFault = x => x.IdtLimit--, "frame_mismatch");
        Reject(m => m.AfterFault = x => x.Tr = 32, "frame_mismatch");
        Reject(m => m.AfterFault = x => x.Write(f.Data + 4096 + 53, 0x89, 8), "frame_mismatch");
        foreach (ulong bad in new ulong[] { 0, 1, 2, 0x40, f.Root | 3, (f.Root + 43 * 4096) | 3, (1UL << 36) | 3,
            (f.Root + 4096) | 0x83, (f.Root + 4096) | 0x43, (f.Root + 4096) | 7, (f.Root + 4096) | 0x8000000000000003 })
            Reject(m => m.Write(f.Root, bad, 64), "absence_mismatch");
        Machine accessed = new(f);
        for (ulong at = f.Root; at < f.Root + 43 * 4096; at += 8)
        { ulong entry = accessed.Read(at, 64); if ((entry & 1) != 0) accessed.Write(at, entry | 0x20, 64); }
        string expected = ExpectedTranscript(accessed); accessed.Run();
        Require(accessed.Output.ToString() == expected && Emitter.AuditSyntheticObservation(f.Request, Observation(accessed)).Succeeded, "accessed-bit raw observations");
    }

    private static void TestRequestsAndCorruption(Fixture f)
    {
        byte[] context = f.ContextBytes, stream = f.StreamBytes;
        void Reject(DoubleFaultIstRequest request)
        {
            Require(!Emitter.PrepareContext(f.Buffers, request).Succeeded, "request rejected");
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
        foreach (ulong capacity in new ulong[] { 0, 4095, ulong.MaxValue }) Require(!Emitter.PrepareContext(f.With(contextCapacity: capacity), f.Request).Succeeded, "capacity");
        Require(!Emitter.PrepareContext(f.With(context: 0), f.Request).Succeeded && !Emitter.PrepareContext(f.With(context: (ulong)(f.Context + 1)), f.Request).Succeeded, "null/misaligned");
        Require(!Emitter.EmitStream(f.With(stream: (ulong)f.Context), f.Request).Succeeded, "overlap");
        foreach (ulong pointer in new ulong[] { 0x0000800000000000, 0x00007ffffffff800, ulong.MaxValue - 7 })
        {
            Require(!Emitter.PrepareContext(f.With(context: pointer), f.Request).Succeeded, "invalid pointer span before access");
            Require(!Emitter.EmitStream(f.With(stream: pointer), f.Request).Succeeded, "invalid stream span before access");
        }
        foreach (ulong capacity in new ulong[] { 0, 8191, ulong.MaxValue }) Require(!Emitter.EmitStream(f.With(streamCapacity: capacity), f.Request).Succeeded, "stream capacity");
        Require(new Span<byte>(f.Context, 4096).SequenceEqual(context) && new Span<byte>(f.Stream, 8192).SequenceEqual(stream), "storage rejection unchanged");
        for (int i = 0; i < 4096; i++)
        {
            f.Context[i] ^= 1;
            var result = Emitter.EmitStream(f.Buffers, f.Request);
            Require(result.Status == DoubleFaultIstStatus.ContextMismatch && result.FirstMismatchOffset == i && result.BytesWritten == 0, "every context byte mismatch");
            f.Context[i] ^= 1;
        }
        for (int i = 0; i < 8192; i++)
        {
            f.Stream[i] ^= 1;
            var result = Emitter.ValidateStreamReadBack(f.Buffers, f.Request);
            Require(result.Status == DoubleFaultIstStatus.StreamMismatch && result.FirstMismatchOffset == i && result.BytesWritten == 0, "every stream byte mismatch");
            f.Stream[i] ^= 1;
        }
        using Fixture different = new(f.Code + 0x100000, f.Data + 0x100000, f.Root);
        Require(different.Image.UsedTablePages == f.Image.UsedTablePages && different.Image.MappedPages == f.Image.MappedPages &&
            !different.TableBytes.AsSpan().SequenceEqual(f.TableBytes), "distinct exact mappings same-count snapshot");
        var summaryOnly = new DoubleFaultIstRequest(f.Code, f.Data, f.Root, Mode(), Pat, different.Image);
        Require(Emitter.PrepareContext(f.Buffers, summaryOnly).ContextPrepared, "snapshot deliberately cannot bind C/D mappings");
        Require(new Span<byte>(f.Context, 4096).SequenceEqual(context), "no invented mapping association");
        Emitter.PrepareContext(f.Buffers, f.Request); Emitter.EmitStream(f.Buffers, f.Request);
        long before = GC.GetAllocatedBytesForCurrentThread();
        for (int i = 0; i < 8; i++)
        {
            Emitter.PrepareContext(f.Buffers, f.Request); Emitter.EmitStream(f.Buffers, f.Request);
            Emitter.ValidateStreamReadBack(f.Buffers, f.Request); Emitter.PrepareContext(f.Buffers, default);
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
                Require(corrupt.Output.ToString().Contains("TEST_FAIL m02.015 absence_mismatch", StringComparison.Ordinal) && corrupt.Faults == 0, "guard malformed/present leaf");
            }
        }
        byte[] before = f.ContextBytes;
        foreach (ulong data in new ulong[] { f.Code, f.Root, (1UL << 36) - 0xa000, 0x0000800000000000 - 0xa000, ulong.MaxValue - 4095 })
            Require(!Emitter.PrepareContext(f.Buffers, new(f.Code, data, f.Root, Mode(), Pat, f.Image)).Succeeded, "exclusive stack top/bounds");
        Require(f.ContextBytes.AsSpan().SequenceEqual(before), "modeled boundary rejection unchanged");
    }

    private static DoubleFaultIstObservation Observation(Machine m)
    {
        ulong Q(int at) => m.Read(m.Fixture.Data + 4096 + (ulong)at, 64);
        DoubleFaultIstWalk Walk(int i) { int at = 512 + 64 * i; return new(Q(at), Q(at+8), Q(at+16), Q(at+24), Q(at+32), Q(at+40)); }
        CpuFaultFrame frame = new((int)Q(960), Q(968) == 1, Q(976), Q(984), Q(1008), Q(912), Q(1000), Q(944), Q(1040),
            Q(952), Q(920), Q(936), Q(928), Q(904), Q(896), Q(888), Q(880), Q(872), Q(864), Q(856), Q(848), Q(840), Q(832));
        return new(frame, Q(992), Q(1016), Q(1024), Q(1048), Q(1032), Q(456), Q(448), Q(464), Q(480), Q(472), Q(488), Q(496),
            Q(1056), Q(1064), Q(1072), Q(1080), Walk(0), Walk(1), Walk(2), Walk(3), Walk(4));
    }
    private static void SetField<T>(ref T value, string field, ulong raw) where T : struct
    {
        int offset = Marshal.OffsetOf<T>("<" + field + ">k__BackingField").ToInt32();
        BinaryPrimitives.WriteUInt64LittleEndian(MemoryMarshal.AsBytes(MemoryMarshal.CreateSpan(ref value, 1)).Slice(offset), raw);
    }
    private static void TestArmedAndSynthetic(Fixture f)
    {
        Require(!RuntimeHelpers.IsReferenceOrContainsReferences<DoubleFaultIstObservation>() && !RuntimeHelpers.IsReferenceOrContainsReferences<DoubleFaultIstBuffers>(), "extra POD surfaces");
        for (int vector=0;vector<256;vector++)
        {
            bool hardware=vector is 8 or 10 or 11 or 12 or 13 or 14 or 17 or 21 or 30;
            byte[] expected=Enumerable.Repeat((byte)0x90,16).ToArray(); int position=0;
            if(!hardware) { expected[position++]=0x6a; expected[position++]=0; }
            expected[position++]=0x6a; expected[position++]=hardware?(byte)1:(byte)0;
            expected[position++]=0x68; BinaryPrimitives.WriteInt32LittleEndian(expected.AsSpan(position),vector); position+=4;
            expected[position++]=0xe9; BinaryPrimitives.WriteInt32LittleEndian(expected.AsSpan(position),4096-vector*16-position-4);
            Require(new ReadOnlySpan<byte>(f.Slots+vector*16,16).SequenceEqual(expected),"independent full normalized slot "+vector);
        }
        byte* canonical = (byte*)NativeMemory.AlignedAlloc(4096,4096);
        try
        {
            Require(PopulateAllN256IDTVectorsOwnedGatesNever.Materialize(canonical,4096,f.Data,f.Code).Succeeded, "canonical before arm");
            int differences = 0, present = 0;
            for (int i = 0; i < 4096; i++)
            {
                int v = i/16, at = i%16; ulong target = f.Code + (ulong)v*16;
                byte expected = at switch { 0 => (byte)target, 1 => (byte)(target>>8), 2 => 8,
                    4 => v == 8 ? (byte)1 : v == 14 ? (byte)2 : v == 2 ? (byte)3 : (byte)0,
                    5 => v == 14 ? (byte)0x0e : (byte)0x8e, 6 => (byte)(target>>16), 7 => (byte)(target>>24),
                    8 => (byte)(target>>32), 9 => (byte)(target>>40), 10 => (byte)(target>>48), 11 => (byte)(target>>56), _ => 0 };
                Require(f.Idt[i] == expected, "independent complete armed gate byte " + i);
                if (f.Idt[i] != canonical[i]) { differences++; Require(i == 229 && (f.Idt[i] ^ canonical[i]) == 128, "sole Present difference"); }
                if (at == 5 && (f.Idt[i] & 128) != 0) present++;
            }
            Require(differences == 1 && present == 255 && !PopulateAllN256IDTVectorsOwnedGatesNever.ValidateReadBack(f.Idt,4096,f.Data,f.Code).Succeeded, "frozen all-present audit still rejects armed table");
        }
        finally { NativeMemory.AlignedFree(canonical); }
        for (int i = 0; i < 4096; i++)
        {
            f.Idt[i] ^= 1; var result = Emitter.AuditArmedIdt(f.Buffers, f.Request);
            Require(result.Status == DoubleFaultIstStatus.IdtMismatch && result.FirstMismatchOffset == i && result.BytesWritten == 0, "every armed byte corruption"); f.Idt[i] ^= 1;
            f.Slots[i] ^= 1; result = Emitter.AuditArmedIdt(f.Buffers, f.Request);
            Require(result.Status == DoubleFaultIstStatus.SlotMismatch && result.FirstMismatchOffset == i && result.BytesWritten == 0, "every normalized byte corruption"); f.Slots[i] ^= 1;
        }
        byte[] before = f.ContextBytes, stream = f.StreamBytes, idt = new Span<byte>(f.Idt,4096).ToArray(), slots = new Span<byte>(f.Slots,4096).ToArray();
        void Reject(DoubleFaultIstBuffers buffers, DoubleFaultIstRequest request)
        {
            foreach (var operation in new Func<DoubleFaultIstBuffers, DoubleFaultIstRequest, DoubleFaultIstResult>[]
            { Emitter.PrepareContext, Emitter.MaterializeArmedIdt, Emitter.AuditArmedIdt, Emitter.EmitStream, Emitter.ValidateStreamReadBack })
                Require(!operation(buffers, request).Succeeded, "pre-mutation bundle rejection");
            Require(f.ContextBytes.AsSpan().SequenceEqual(before) && f.StreamBytes.AsSpan().SequenceEqual(stream) &&
                new Span<byte>(f.Idt,4096).SequenceEqual(idt) && new Span<byte>(f.Slots,4096).SequenceEqual(slots), "all four buffers unchanged");
        }
        Reject(default,f.Request);
        foreach (ulong capacity in new ulong[] { 0, 4095, 4097, ulong.MaxValue }) { Reject(f.With(slotCapacity:capacity),f.Request); Reject(f.With(idtCapacity:capacity),f.Request); }
        foreach (ulong pointer in new ulong[] { 0, 1, (ulong)f.Slots+8, 0x0000800000000000, ulong.MaxValue-4095 })
        { Reject(f.With(slots:pointer),f.Request); Reject(f.With(idt:pointer),f.Request); }
        Reject(f.With(context:(ulong)f.Slots),f.Request); Reject(f.With(context:(ulong)f.Idt),f.Request);
        Reject(f.With(stream:(ulong)f.Slots),f.Request); Reject(f.With(stream:(ulong)f.Idt),f.Request);
        Reject(f.With(slots:(ulong)f.Idt),f.Request); Reject(f.With(context:(ulong)f.Stream+8),f.Request);
        for (int i=0;i<4;i++) Reject(f.Buffers,new(f.Code,f.Data,f.Root,Mode(),Pat,f.Image,i==0,i==1,i==2,i==3));
        foreach (ulong cr3 in new ulong[] { 0, 1, 0x400001, 1UL<<36, ulong.MaxValue })
            Reject(f.Buffers,new(f.Code,f.Data,f.Root,X64PagingModePreflightObservation.CreateObserved(0x80010033,cr3,0x6a0,0xd00,0x80000008,0x3024,0x202),Pat,f.Image));
        Machine m = new(f); m.Run(); var good = Observation(m);
        Require(!Emitter.AuditSyntheticObservation(f.Request, default).Succeeded && Emitter.AuditSyntheticObservation(f.Request,good).Succeeded, "observation default fail");
        foreach (string field in new[] { "CurrentCs", "CurrentSs", "GdtrBase", "GdtrLimit", "Tr", "IdtrBase", "IdtrLimit", "TssAccess", "Ist1", "DfGateLow", "DfGateHigh", "PfGateLow", "PfGateHigh" })
        { var bad=good; SetField(ref bad,field,ulong.MaxValue); Require(!Emitter.AuditSyntheticObservation(f.Request,bad).Succeeded,"observation field "+field); }
        foreach (ulong rsp in new ulong[] { 0, f.Data+0x3000-8, f.Data+0x4000-184, f.Data+0x3001, f.Data+0x6000-192, f.Data+0x8000-192, f.Data+0xa000-192, ulong.MaxValue-7, good.Frame.Rsp })
        { var bad=good; SetField(ref bad,"CurrentRsp",rsp); Require(!Emitter.AuditSyntheticObservation(f.Request,bad).Succeeded,"separate current full span"); }
        foreach (ulong rsp in new ulong[] { f.Data+0x3000, f.Data+0x4000-192 })
        { var ok=good; SetField(ref ok,"CurrentRsp",rsp); Require(Emitter.AuditSyntheticObservation(f.Request,ok).Succeeded,"inclusive full span endpoints"); }
        foreach (string field in new[] { "SavedCs", "SavedSs" })
        { var ok=good; SetField(ref ok,field,0); Require(Emitter.AuditSyntheticObservation(f.Request,ok).Succeeded,"undefined saved selector"); }
        var claimed=new DoubleFaultIstObservation(good.Frame,good.SavedCs,good.SavedSs,good.CurrentRsp,good.CurrentCs,good.CurrentSs,
            good.GdtrBase,good.GdtrLimit,good.Tr,good.IdtrBase,good.IdtrLimit,good.TssAccess,good.Ist1,
            good.DfGateLow,good.DfGateHigh,good.PfGateLow,good.PfGateHigh,good.Zero,good.Df,good.Pf,good.Nmi,good.Transition,true);
        Require(claimed.ClaimedGuestCapture && !Emitter.AuditSyntheticObservation(f.Request,claimed).Succeeded,"synthetic capture never guest authority");
        foreach (string field in new[] { "Vector", "HasErrorCode", "ErrorCode", "Cr3" })
        {
            var bad=good; int offset=Marshal.OffsetOf<DoubleFaultIstObservation>("<Frame>k__BackingField").ToInt32()+Marshal.OffsetOf<CpuFaultFrame>("<"+field+">k__BackingField").ToInt32();
            MemoryMarshal.AsBytes(MemoryMarshal.CreateSpan(ref bad,1))[offset] ^= 1;
            Require(!Emitter.AuditSyntheticObservation(f.Request,bad).Succeeded,"frame invariant "+field);
        }
        foreach (string field in new[] { "Rip", "Rsp", "Rbp", "Rflags", "Cr2", "Rax", "Rbx", "Rcx", "Rdx", "Rsi", "Rdi", "R8", "R9", "R10", "R11", "R12", "R13", "R14", "R15" })
        {
            var ok=good; int offset=Marshal.OffsetOf<DoubleFaultIstObservation>("<Frame>k__BackingField").ToInt32()+Marshal.OffsetOf<CpuFaultFrame>("<"+field+">k__BackingField").ToInt32();
            BinaryPrimitives.WriteUInt64LittleEndian(MemoryMarshal.AsBytes(MemoryMarshal.CreateSpan(ref ok,1)).Slice(offset),0);
            Require(Emitter.AuditSyntheticObservation(f.Request,ok).Succeeded,"diagnostic only "+field);
        }
        // Each declared role and raw field is independently bounded. No table pointer is dereferenced.
        foreach (string role in new[] { "Zero", "Df", "Pf", "Nmi", "Transition" })
        {
            int at=Marshal.OffsetOf<DoubleFaultIstObservation>("<"+role+">k__BackingField").ToInt32();
            foreach (string field in new[] { "Linear", "LevelsRead", "Pml4e", "Pdpte", "Pde", "Pte" })
            {
                var bad=good; int offset=at+Marshal.OffsetOf<DoubleFaultIstWalk>("<"+field+">k__BackingField").ToInt32();
                BinaryPrimitives.WriteUInt64LittleEndian(MemoryMarshal.AsBytes(MemoryMarshal.CreateSpan(ref bad,1)).Slice(offset),ulong.MaxValue);
                Require(!Emitter.AuditSyntheticObservation(f.Request,bad).Succeeded,"raw walk corruption "+role+" "+field);
            }
            var absent=good; var raw=MemoryMarshal.AsBytes(MemoryMarshal.CreateSpan(ref absent,1));
            raw.Slice(at+8,40).Clear(); BinaryPrimitives.WriteUInt64LittleEndian(raw.Slice(at+8),1);
            Require(!Emitter.AuditSyntheticObservation(f.Request,absent).Succeeded,"zero ancestor contradicts known mapping "+role);
        }
        // Independent same-parent and single-parent controls, distinct from a generic corrupt bit.
        var alias=good; var aliasBytes=MemoryMarshal.AsBytes(MemoryMarshal.CreateSpan(ref alias,1));
        int zeroOffset=Marshal.OffsetOf<DoubleFaultIstObservation>("<Zero>k__BackingField").ToInt32();
        BinaryPrimitives.WriteUInt64LittleEndian(aliasBytes.Slice(zeroOffset+8),3);
        BinaryPrimitives.WriteUInt64LittleEndian(aliasBytes.Slice(zeroOffset+24),good.Df.Pdpte);
        Require(!Emitter.AuditSyntheticObservation(f.Request,alias).Succeeded,"different incoming parent slots cannot alias a table");
        var cycle=good; int dfOffset=Marshal.OffsetOf<DoubleFaultIstObservation>("<Df>k__BackingField").ToInt32();
        BinaryPrimitives.WriteUInt64LittleEndian(MemoryMarshal.AsBytes(MemoryMarshal.CreateSpan(ref cycle,1)).Slice(dfOffset+16),f.Root|3);
        Require(!Emitter.AuditSyntheticObservation(f.Request,cycle).Succeeded,"root cycle rejected");
        var accessed=good;
        BinaryPrimitives.WriteUInt64LittleEndian(MemoryMarshal.AsBytes(MemoryMarshal.CreateSpan(ref accessed,1)).Slice(dfOffset+16),good.Df.Pml4e|0x20);
        Require(Emitter.AuditSyntheticObservation(f.Request,accessed).Succeeded,"shared entry accessed bit may differ");
        foreach ((ulong code,ulong data,ulong tables,byte width,ulong depth) in new[]
        { (1UL<<39,2UL<<39,3UL<<39,(byte)48,1UL), (1UL<<30,2UL<<30,3UL<<30,(byte)36,2UL),
          (1UL<<21,2UL<<21,3UL<<21,(byte)36,3UL), (0x1000UL,0x10000UL,0x20000UL,(byte)36,4UL) })
        {
            using Fixture legal=new(code,data,tables,width); legal.Prepare(); Machine model=new(legal); string text=ExpectedTranscript(model); model.Run();
            var observed=Observation(model); Require(observed.Zero.LevelsRead==depth && observed.Df.LevelsRead==4 &&
                model.Output.ToString()==text && Emitter.AuditSyntheticObservation(legal.Request,observed).Succeeded,"legal null depth and leaf guard absence "+depth);
        }
        foreach (ulong low in new ulong[] { 8,16,24 })
        {
            var mode=X64PagingModePreflightObservation.CreateObserved(0x80010033,0x400000|low,0x6a0,0xd00,0x80000008,0x3024,0x202);
            Require(Emitter.PrepareContext(f.Buffers,new(f.Code,f.Data,f.Root,mode,Pat,f.Image)).Succeeded,"legal firmware CR3 PWT/PCD");
        }
        f.Prepare();
        long allocation = GC.GetAllocatedBytesForCurrentThread();
        for(int i=0;i<8;i++) { Emitter.AuditSyntheticObservation(f.Request,good); Emitter.AuditArmedIdt(f.Buffers,f.Request); Emitter.MaterializeArmedIdt(f.Buffers,f.Request); }
        Require(GC.GetAllocatedBytesForCurrentThread()==allocation,"synthetic/armed warmed zero allocation"); f.CheckCanaries();
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
        public readonly ulong[] DiagnosticRegisters = Enumerable.Range(0, 16).Select(i => 0xFEDD000000000000UL + (ulong)i).ToArray();
        public ulong SavedRip = 0xFEDCBA9876543210, SavedCs = 0xFEFF, SavedFlags = ulong.MaxValue,
            SavedRsp = ulong.MaxValue - 7, SavedSs = 0xAAFE, DiagnosticCr2 = 0xBADC0FFEE0DDF00D;
        public int HardwareWords, NormalizerWords, CaptureWords;

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
            Memory.Add((fixture.Code, new Span<byte>(fixture.Slots, 4096).ToArray()));
            Memory.Add((fixture.Data, new Span<byte>(fixture.Idt, 4096).ToArray()));
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
            for (int i = 0; i < savedRegisters.Length; i++) Require(Read(Registers[4] + (ulong)i * 8, 64) == DiagnosticRegisters[savedRegisters[i]], "raw GPR preserved " + savedRegisters[i]);
            Require(Read(Registers[4] + 120, 64) == DiagnosticRegisters[0], "original RAX saved");
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
          0x85 => (Flags & 0x40) == 0, 0x86 => (Flags & 0x41) != 0, 0x87 => (Flags & 0x41) == 0, _ => throw new InvalidOperationException("condition") };
        private void Push(ulong value) { Registers[4] -= 8; Write(Registers[4], value, 64); if (_inHandler) CaptureWords++; else if (Faults != 0) NormalizerWords++; }
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
                        Registers[8] is >= 1 and <= 84 && Registers[3] == Fixture.Code + 4096 + 7104 + (84 - Registers[8]) * 4,
                        "formatter source/cursor/count lifetimes");
                ulong before = Pc;
                Instruction i = Decode(Pc); Pc = i.Next;
                ulong a = 0, b = 0, value;
                if (i.Op is "mov" or "add" or "sub" or "or" or "and" or "xor" or "cmp" or "test" or "bts")
                {
                    if (i.Op == "mov" && i.Source.Register < 0 && Address(i.Source, i.Next) == 0)
                    {
                        Require(before == Fixture.Code + 4096 + 2698 && Registers[0] == 0, "exact null-read trigger");
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
                        if (i.Immediate == 2) { Require(Registers[4] == _hardwareStackTop - 72 && Read(Registers[4], 64) == DiagnosticRegisters[0], "original RAX before early CR2"); EarlyCr2 = true; _earlyCr2Value = Cr2; Events.Add("cr2"); }
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
                        Require(Registers[1] <= 2643 && (Flags & 0x400) == 0, "bounded forward copy");
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
            // Independently model the Intel #PF -> nonpresent interrupt gate #NP -> #DF combination.
            Require(Read(IdtBase + 14 * 16 + 5, 8) == 0x0e, "#PF gate nonpresent causes #NP during delivery");
            Events.Add("pf-delivery"); Events.Add("np-delivery");
            ulong gate = IdtBase + 8 * 16;
            Require(Read(gate + 2, 16) == 8 && Read(gate + 4, 8) == 1 && Read(gate + 5, 8) == 0x8e,
                "#DF uses valid present IST1 interrupt gate");
            ulong target = Read(gate, 16) | (Read(gate + 6, 16) << 16) | (Read(gate + 8, 32) << 32);
            Require(target == Fixture.Code + 8 * 16 && Tr == 24, "owned original DF gate and TSS");
            _hardwareStackTop = Read(Fixture.Data + 4096 + 100, 64); Registers[4] = _hardwareStackTop;
            for (int i = 0; i < 16; i++) if (i != 4) Registers[i] = DiagnosticRegisters[i];
            Push(SavedSs); Push(SavedRsp); Push(SavedFlags); Push(SavedCs); Push(SavedRip); Push(0);
            HardwareWords = NormalizerWords; NormalizerWords = 0;
            Cs = 8; Ss = 0; Cr2 = DiagnosticCr2; Flags &= ~0x200UL;
            Pc = target; Events.Add("fault");
            AfterFault?.Invoke(this);
        }
    }

    private static void CheckNoStdLibCompilation()
    {
        string root = FindRoot();
        string directory = Path.Combine(root, "artifacts", "issue55", "nostdlib", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        File.Copy(Path.Combine(root, "global.json"), Path.Combine(directory, "global.json"));
        string[] sources =
        [
            "src/OS/Runtime/MinimalRuntime.cs",
            "src/OS/Runtime/RuntimeImportAttribute.cs",
            "src/OS/Runtime/MethodImplAttribute.cs",
            "src/OS.Kernel/Boot/TypedPhysicalVirtualDeviceAddressPODWrappers.cs",
            "src/OS.Kernel/Boot/UefiMemoryType.cs",
            "src/OS.Kernel/Boot/BootMemoryRegionMaterializer.cs",
            "src/OS.Kernel/Memory/BootReservedRange.cs",
            "src/OS.Kernel/Memory/PageProtection.cs",
            "src/OS.Kernel/Memory/PageCachePolicy.cs",
            "src/OS.Kernel/Memory/X64PageTableEntry.cs",
            "src/OS.Kernel/Memory/X64TlbInvalidationPlan.cs",
            "src/OS.Kernel/Memory/X64PageMap.cs",
            "src/OS.Kernel/Memory/PageZeroMappingPolicy.cs",
            "src/OS.Kernel/Diagnostics/SeparateDisabledDefaultPageFaultPageTables.cs",
            "src/OS.Kernel/Memory/X64PagingModePreflightStubPlan.cs",
            "src/OS.Kernel/Diagnostics/OwnedGDTTSSGeneratedAuditedCSharpStubBytes.cs",
            "src/OS.Kernel/Diagnostics/DoubleFaultISTPathSilentRebootOrTriple.cs",
            "src/OS.Kernel/Diagnostics/CpuFaultFrame.cs",
            "src/OS.Kernel/Diagnostics/NormalizeErrorCodeNonErrorCodeInterruptFrame.cs",
            "src/OS.Kernel/Diagnostics/PopulateAllN256IDTVectorsOwnedGatesNever.cs"
        ];
        string includes = string.Join(Environment.NewLine, sources.Select(p =>
            "<Compile Include=\"" + System.Security.SecurityElement.Escape(Path.Combine(root, p)) + "\" />"));
        File.WriteAllText(Path.Combine(directory, "Compile.csproj"), $$"""
            <Project Sdk="Microsoft.NET.Sdk">
              <PropertyGroup>
                <TargetFramework>net10.0</TargetFramework><LangVersion>14.0</LangVersion>
                <OutputType>Library</OutputType><AssemblyName>CLROS.M02.DoubleFault.NoStdLib</AssemblyName>
                <EnableDefaultCompileItems>false</EnableDefaultCompileItems><AllowUnsafeBlocks>true</AllowUnsafeBlocks>
                <NoStdLib>true</NoStdLib><DefineConstants>NOSTDLIB_EFI</DefineConstants><NoConfig>true</NoConfig>
                <DisableImplicitFrameworkReferences>true</DisableImplicitFrameworkReferences>
                <GenerateAssemblyInfo>false</GenerateAssemblyInfo><GenerateTargetFrameworkAttribute>false</GenerateTargetFrameworkAttribute>
                <ComputeNETCoreBuildOutputFiles>false</ComputeNETCoreBuildOutputFiles><RuntimeMetadataVersion>v4.0.30319</RuntimeMetadataVersion>
                <ProduceReferenceAssembly>false</ProduceReferenceAssembly><EnableNETAnalyzers>false</EnableNETAnalyzers>
                <RunAnalyzersDuringBuild>false</RunAnalyzersDuringBuild><TreatWarningsAsErrors>true</TreatWarningsAsErrors>
                <DebugType>none</DebugType><RestoreIgnoreFailedSources>true</RestoreIgnoreFailedSources>
                <UseSharedCompilation>false</UseSharedCompilation>
              </PropertyGroup>
              <ItemGroup>{{includes}}</ItemGroup>
              <Target Name="RemoveFrameworkReferences" BeforeTargets="BeforeCompile" AfterTargets="FindReferenceAssembliesForReferences">
                <ItemGroup><ReferencePathWithRefAssemblies Remove="@(ReferencePathWithRefAssemblies)" />
                  <ReferencePath Remove="@(ReferencePath)" /></ItemGroup>
              </Target>
            </Project>
            """, new UTF8Encoding(false));
        var identities = sources.Select(p => new { Path = p, Sha256 = Convert.ToHexString(
            System.Security.Cryptography.SHA256.HashData(File.ReadAllBytes(Path.Combine(root, p)))) });
        File.WriteAllText(Path.Combine(directory, "source-identities.json"),
            System.Text.Json.JsonSerializer.Serialize(identities), new UTF8Encoding(false));
        ProcessStartInfo start = new("dotnet")
        {
            WorkingDirectory = directory, UseShellExecute = false, CreateNoWindow = true,
            RedirectStandardOutput = true, RedirectStandardError = true
        };
        foreach (string arg in new[] { "build", "Compile.csproj", "--nologo", "-m:1", "-c", "Release", "-nodeReuse:false" }) start.ArgumentList.Add(arg);
        using Process process = Process.Start(start) ?? throw new InvalidOperationException("no-stdlib compile start");
        Task<string> stdout = process.StandardOutput.ReadToEndAsync(); Task<string> stderr = process.StandardError.ReadToEndAsync();
        bool exited = process.WaitForExit(90000);
        if (!exited) { process.Kill(entireProcessTree: true); process.WaitForExit(); }
        Task.WaitAll(stdout, stderr);
        File.WriteAllText(Path.Combine(directory, "build.stdout.log"), stdout.Result);
        File.WriteAllText(Path.Combine(directory, "build.stderr.log"), stderr.Result);
        File.WriteAllText(Path.Combine(directory, "build.exitcode.txt"), process.ExitCode.ToString(System.Globalization.CultureInfo.InvariantCulture));
        Require(exited && process.ExitCode == 0, "actual-source no-stdlib compile: " + directory + " " + stdout.Result + stderr.Result);
        using var stream = File.OpenRead(Path.Combine(directory, "bin", "Release", "net10.0", "CLROS.M02.DoubleFault.NoStdLib.dll"));
        using PEReader image = new(stream); MetadataReader metadata = image.GetMetadataReader();
        Require(metadata.AssemblyReferences.Count == 0, "actual source closure has zero assembly references");
        Require(metadata.MethodDefinitions.Select(metadata.GetMethodDefinition).Any(m => metadata.GetString(m.Name) == "EmitStream" && m.RelativeVirtualAddress != 0),
            "actual emitter method is present");
        Console.WriteLine("M02_DF_HOST_NOSTDLIB inputs=20 assemblyReferences=0");
    }
    private static string FindRoot()
    {
        DirectoryInfo? current = new(AppContext.BaseDirectory);
        while (current is not null)
        {
            if (File.Exists(Path.Combine(current.FullName, "CLROS.slnx"))) return current.FullName;
            current = current.Parent;
        }
        throw new InvalidOperationException("CLROS root not found");
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
        private readonly byte* _stream, _context, _slots, _idt;
        public readonly ulong* Tables;
        public readonly ulong Code, Data, Root;
        public readonly SeparatePageFaultPageTableResult Image;
        public DoubleFaultIstRequest Request;
        public byte* Slots => _slots + 4096;
        public byte* Idt => _idt + 4096;
        public DoubleFaultIstBuffers Buffers => With();
        public DoubleFaultIstBuffers With(ulong? context = null, ulong contextCapacity = 4096, ulong? stream = null, ulong streamCapacity = 8192,
            ulong? slots = null, ulong slotCapacity = 4096, ulong? idt = null, ulong idtCapacity = 4096)
            => new((byte*)(context ?? (ulong)Context), contextCapacity, (byte*)(stream ?? (ulong)Stream), streamCapacity,
                (byte*)(slots ?? (ulong)Slots), slotCapacity, (byte*)(idt ?? (ulong)Idt), idtCapacity);
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
            _slots = (byte*)NativeMemory.AlignedAlloc(12288, 4096);
            _idt = (byte*)NativeMemory.AlignedAlloc(12288, 4096);
            new Span<byte>(_slots, 12288).Fill(0xa5); new Span<byte>(_idt, 12288).Fill(0xa5);
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
            var context = Emitter.PrepareContext(Buffers, Request);
            Require(context.ContextPrepared, "prepare context " + context.Status + " offset=" + context.FirstMismatchOffset);
            Require(Emitter.MaterializeArmedIdt(Buffers, Request).Succeeded, "armed materialization");
            var stream = Emitter.EmitStream(Buffers, Request);
            Require(stream.StreamEmitted, "emit stream " + stream.Status);
            Require(!stream.GuestExecution && !stream.HardwareStateCaptured && !stream.FaultTriggered && !stream.PostExitBootServices, "no hardware claims");
        }
        public void CheckCanaries()
        {
            for (int i = 0; i < 4096; i++) Require(_slots[i] == 0xa5 && _slots[8192+i] == 0xa5 && _idt[i] == 0xa5 && _idt[8192+i] == 0xa5, "slot/IDT canaries");
            for (int i = 0; i < 8; i++) Require(_stream[i] == 0xa5 && _stream[8192 + 8 + i] == 0xa5 &&
                _context[i] == 0xa5 && _context[4096 + 8 + i] == 0xa5, "canaries");
        }
        public void Dispose()
        { NativeMemory.AlignedFree(_stream); NativeMemory.AlignedFree(_context); NativeMemory.AlignedFree(Tables); NativeMemory.AlignedFree(_slots); NativeMemory.AlignedFree(_idt); }
    }
}
