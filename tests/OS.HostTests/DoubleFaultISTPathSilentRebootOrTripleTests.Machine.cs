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

internal static unsafe partial class DoubleFaultISTPathSilentRebootOrTripleTests
{
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
        public ulong SavedRip = 0xFEDCBA9876543210, SavedCs = 0xFEFF, SavedFlags = ulong.MaxValue, SavedRsp = ulong.MaxValue - 7, SavedSs = 0xAAFE, DiagnosticCr2 = 0xBADC0FFEE0DDF00D;
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
            Fixture = fixture;
            Pc = fixture.Code + 4096 + 768;
            var mode = fixture.Request.Observation;
            Cr0 = mode.Cr0;
            Cr3 = mode.RawCr3;
            Cr4 = mode.RawCr4;
            Efer = mode.Ia32Efer;
            Flags = mode.Rflags;
            AddressSizes = mode.CpuidAddressSizesEax;
            MaximumLeaf = mode.CpuidMaximumExtendedLeaf;
            Memory.Add((fixture.Code + 4096, fixture.StreamBytes));
            Memory.Add((fixture.Data + 4096, fixture.ContextBytes));
            Memory.Add((fixture.Root, fixture.TableBytes));
            Memory.Add((OldStack, new byte[4096]));
            for (int i = 0; i < 4; i++)
                Memory.Add((fixture.Data + (ulong)(3 + 2 * i) * 4096, new byte[4096]));
            Memory.Add((fixture.Code, new Span<byte>(fixture.Slots, 4096).ToArray()));
            Memory.Add((fixture.Data, new Span<byte>(fixture.Idt, 4096).ToArray()));
            Registers[1] = fixture.Data + 4096;
            Registers[4] = OldStack + 4088;
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
            for (int i = 0; i < width / 8; i++)
                result |= (ulong)ByteAt(address + (ulong)i) << (i * 8);
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
                    {
                        region.Bytes[(int)(address + (ulong)i - region.Base)] = (byte)(value >> (i * 8));
                        found = true;
                        break;
                    }

                Require(found, "synthetic unmapped write " + address.ToString("X"));
            }
        }

        private void CheckCaptureBeforeContext(ulong address)
        {
            if (!_inHandler || address < Fixture.Data + 4096 || address >= Fixture.Data + 8192 || SavedRegistersChecked)
                return;
            Require(EarlyCr2 && Registers[4] == _hardwareStackTop - 192, "early CR2 and all GPR saves before context");
            int[] savedRegisters = [15, 14, 13, 12, 11, 10, 9, 8, 7, 6, 5, 3, 2, 1];
            for (int i = 0; i < savedRegisters.Length; i++)
                Require(Read(Registers[4] + (ulong)i * 8, 64) == DiagnosticRegisters[savedRegisters[i]], "raw GPR preserved " + savedRegisters[i]);
            Require(Read(Registers[4] + 120, 64) == DiagnosticRegisters[0], "original RAX saved");
            Require(Read(Registers[4] + 112, 64) == _earlyCr2Value, "early CR2 value retained in stack slot");
            SavedRegistersChecked = true;
        }

        public ulong Address(Operand op, ulong next) => unchecked((op.Rip ? next : op.Base >= 0 ? Registers[op.Base] : 0) + (op.Index >= 0 ? Registers[op.Index] * (ulong)op.Scale : 0) + (ulong)op.Displacement);
        private ulong Get(Operand op, int width, ulong next) => op.Register >= 0 ? Registers[op.Register] & Mask(width) : Read(Address(op, next), width);
        private void Set(Operand op, int width, ulong value, ulong next)
        {
            value &= Mask(width);
            if (op.Register < 0)
                Write(Address(op, next), value, width);
            else if (width >= 32)
                Registers[op.Register] = value;
            else
                Registers[op.Register] = (Registers[op.Register] & ~Mask(width)) | value;
        }

        private static ulong Mask(int width) => width == 64 ? ulong.MaxValue : (1UL << width) - 1;
        private void FlagsFor(ulong result, int width, bool carry = false) => Flags = (Flags & ~0x41UL) | ((result & Mask(width)) == 0 ? 0x40UL : 0) | (carry ? 1UL : 0);
        private bool Condition(byte condition) => condition switch
        {
            0x82 => (Flags & 1) != 0,
            0x83 => (Flags & 1) == 0,
            0x84 => (Flags & 0x40) != 0,
            0x85 => (Flags & 0x40) == 0,
            0x86 => (Flags & 0x41) != 0,
            0x87 => (Flags & 0x41) == 0,
            _ => throw new InvalidOperationException("condition")};
        private void Push(ulong value)
        {
            Registers[4] -= 8;
            Write(Registers[4], value, 64);
            if (_inHandler)
                CaptureWords++;
            else if (Faults != 0)
                NormalizerWords++;
        }

        private ulong Pop()
        {
            ulong value = Read(Registers[4], 64);
            Registers[4] += 8;
            return value;
        }

        public Instruction Decode(ulong at)
        {
            ulong p = at;
            byte Next() => ByteAt(p++);
            ulong Number(int count)
            {
                ulong value = 0;
                for (int i = 0; i < count; i++)
                    value |= (ulong)Next() << (8 * i);
                return value;
            }

            bool operand16 = false, repeat = false;
            byte rex = 0, opcode = Next();
            while (opcode == 0x66 || opcode == 0xf3 || (opcode >= 0x40 && opcode <= 0x4f))
            {
                if (opcode == 0x66)
                    operand16 = true;
                else if (opcode == 0xf3)
                    repeat = true;
                else
                    rex = opcode;
                opcode = Next();
            }

            int width = (rex & 8) != 0 ? 64 : operand16 ? 16 : 32;
            (Operand Rm, Operand Reg, int Extension) ModRm()
            {
                byte m = Next();
                int mod = m >> 6, low = m & 7;
                Operand reg = Operand.Reg(((m >> 3) & 7) | ((rex & 4) != 0 ? 8 : 0));
                if (mod == 3)
                    return (Operand.Reg(low | ((rex & 1) != 0 ? 8 : 0)), reg, (m >> 3) & 7);
                int b = low | ((rex & 1) != 0 ? 8 : 0), index = -1, scale = 1;
                bool rip = false;
                long displacement = 0;
                if (low == 4)
                {
                    byte sib = Next();
                    scale = 1 << (sib >> 6);
                    index = ((sib >> 3) & 7) | ((rex & 2) != 0 ? 8 : 0);
                    if (index == 4)
                        index = -1;
                    b = (sib & 7) | ((rex & 1) != 0 ? 8 : 0);
                    if (mod == 0 && (sib & 7) == 5)
                    {
                        b = -1;
                        displacement = (int)Number(4);
                    }
                }
                else if (mod == 0 && low == 5)
                {
                    b = -1;
                    rip = true;
                    displacement = (int)Number(4);
                }

                if (mod == 1)
                    displacement = (sbyte)Next();
                if (mod == 2)
                    displacement = (int)Number(4);
                return (new Operand(-1, b, index, scale, displacement, rip), reg, (m >> 3) & 7);
            }

            Instruction Make(string op, Operand d = default, Operand s = default, ulong imm = 0, byte cc = 0, int bits = 0) => new(op, bits == 0 ? width : bits, d, s, imm, p, cc);
            if (opcode >= 0xb8 && opcode <= 0xbf)
                return Make("imm", Operand.Reg((opcode & 7) | ((rex & 1) != 0 ? 8 : 0)), imm: Number(width / 8));
            if (opcode >= 0x50 && opcode <= 0x57)
                return Make("push", Operand.Reg((opcode & 7) | ((rex & 1) != 0 ? 8 : 0)), bits: 64);
            if (opcode >= 0x58 && opcode <= 0x5f)
                return Make("pop", Operand.Reg((opcode & 7) | ((rex & 1) != 0 ? 8 : 0)), bits: 64);
            if (opcode is 0x6a)
                return Make("pushi", imm: unchecked((ulong)(long)(sbyte)Next()), bits: 64);
            if (opcode is 0x68)
                return Make("pushi", imm: unchecked((ulong)(long)(int)Number(4)), bits: 64);
            if (opcode is 0xe9 or 0xeb)
            {
                long rel = opcode == 0xe9 ? (int)Number(4) : (sbyte)Next();
                return Make("jmp", imm: unchecked(p + (ulong)rel));
            }

            if (opcode is 0x25 or 0x3d or 0xa9 or 0xa8)
            {
                int bytes = opcode == 0xa8 ? 1 : width == 16 ? 2 : 4;
                ulong imm = Number(bytes);
                if (width == 64)
                    imm = unchecked((ulong)(long)(int)imm);
                return Make(opcode == 0x25 ? "andi" : opcode == 0x3d ? "cmpi" : "testi", Operand.Reg(0), imm: imm, bits: opcode == 0xa8 ? 8 : width);
            }

            if (opcode is 0x89 or 0x8b or 0x88 or 0x8a or 0x8d or 0x01 or 0x09 or 0x21 or 0x29 or 0x31 or 0x39 or 0x85 or 0x8c or 0x8e)
            {
                var m = ModRm();
                string op = opcode switch
                {
                    0x89 or 0x8b or 0x88 or 0x8a => "mov",
                    0x8d => "lea",
                    0x01 => "add",
                    0x09 => "or",
                    0x21 => "and",
                    0x29 => "sub",
                    0x31 => "xor",
                    0x39 => "cmp",
                    0x85 => "test",
                    0x8c => "segread",
                    _ => "segwrite"
                };
                bool reverse = opcode is 0x8b or 0x8a or 0x8d;
                return Make(op, reverse ? m.Reg : m.Rm, reverse ? m.Rm : m.Reg, (ulong)m.Extension, bits: opcode is 0x88 or 0x8a ? 8 : opcode is 0x8c or 0x8e ? 16 : width);
            }

            if (opcode is 0x80 or 0x81 or 0x83 or 0xc1 or 0xd3 or 0xff or 0xf7)
            {
                var m = ModRm();
                int bits = opcode == 0x80 ? 8 : width;
                ulong imm = opcode is 0x80 or 0xc1 ? Next() : opcode == 0x83 ? unchecked((ulong)(long)(sbyte)Next()) : opcode == 0x81 ? Number(width == 16 ? 2 : 4) : 0;
                if (opcode == 0x81 && width == 64)
                    imm = unchecked((ulong)(long)(int)imm);
                string op = opcode switch
                {
                    0xc1 or 0xd3 => m.Extension == 4 ? "shl" : m.Extension == 5 ? "shr" : "unknown",
                    0xff => m.Extension == 0 ? "inc" : m.Extension == 1 ? "dec" : "unknown",
                    0xf7 => m.Extension == 2 ? "not" : "unknown",
                    _ => m.Extension switch
                    {
                        0 => "addi",
                        1 => "ori",
                        4 => "andi",
                        5 => "subi",
                        7 => "cmpi",
                        _ => "unknown"
                    }};
                return Make(op, m.Rm, imm: imm, cc: opcode == 0xd3 ? (byte)1 : (byte)0, bits: bits);
            }

            if (opcode == 0x0f)
            {
                byte second = Next();
                if (second >= 0x80 && second <= 0x8f)
                {
                    long rel = (int)Number(4);
                    return Make("jcc", imm: unchecked(p + (ulong)rel), cc: second);
                }

                if (second == 0xa2)
                    return Make("cpuid");
                if (second == 0x32)
                    return Make("rdmsr");
                var m = ModRm();
                return second switch
                {
                    0xb7 => Make("zx", m.Reg, m.Rm),
                    0x02 => Make("lar", m.Reg, m.Rm),
                    0xab => Make("bts", m.Rm, m.Reg),
                    0x20 => Make("crread", m.Rm, imm: (ulong)m.Extension, bits: 64),
                    0x22 => Make("crwrite", m.Rm, imm: (ulong)m.Extension, bits: 64),
                    0x01 => Make(m.Extension switch
                    {
                        0 => "sgdt",
                        1 => "sidt",
                        2 => "lgdt",
                        3 => "lidt",
                        _ => "unknown"
                    }, m.Rm),
                    0x00 => Make(m.Extension == 3 ? "ltr" : m.Extension == 1 ? "str" : "unknown", m.Rm, bits: 16),
                    _ => throw new InvalidOperationException("unknown 0F opcode " + second.ToString("X"))};
            }

            return opcode switch
            {
                0x9c => Make("pushf"),
                0x9d => Make("popf"),
                0xfa => Make("cli"),
                0xfc => Make("cld"),
                0xf4 => Make("hlt"),
                0x90 => Make("nop"),
                0xec => Make("in"),
                0xee => Make("out"),
                0xac => Make("lodsb"),
                0xa4 when repeat => Make("copy", bits: 8),
                0xa5 when repeat => Make("copy", bits: width),
                _ => throw new InvalidOperationException("unknown opcode " + opcode.ToString("X") + " at " + at.ToString("X"))};
        }

        public void Run()
        {
            while (!Halted)
            {
                Require(++Steps < 2000000, "bounded synthetic instruction count");
                if (Pc == Fixture.Code + 4096)
                {
                    BeforeHandler?.Invoke(this);
                    _inHandler = true;
                }

                // Assert live loop values independently of the emitter's byte builder.
                ulong offset = Pc - Fixture.Code - 4096;
                if (offset == 2168)
                    Require(Registers[8] < 4 && Registers[1] == 39 - Registers[8] * 9 && Registers[9] >= Fixture.Root && Registers[9] < Fixture.Root + 43 * 4096 && (Registers[9] & 4095) == 0 && Registers[14] == Fixture.Root && Registers[13] == Fixture.Root + 43 * 4096, "walk register lifetimes and pre-dereference bounds");
                if (offset == 3016)
                    Require(Registers[15] == Fixture.Data + 4096 && Registers[12] == Fixture.Data + 4096 + 1152 && Registers[8] is >= 1 and <= 84 && Registers[3] == Fixture.Code + 4096 + 7104 + (84 - Registers[8]) * 4, "formatter source/cursor/count lifetimes");
                ulong before = Pc;
                Instruction i = Decode(Pc);
                Pc = i.Next;
                ulong a = 0, b = 0, value;
                if (i.Op is "mov" or "add" or "sub" or "or" or "and" or "xor" or "cmp" or "test" or "bts")
                {
                    if (i.Op == "mov" && i.Source.Register < 0 && Address(i.Source, i.Next) == 0)
                    {
                        Require(before == Fixture.Code + 4096 + 2698 && Registers[0] == 0, "exact null-read trigger");
                        if (!CompleteRead)
                        {
                            DeliverFault(before);
                            continue;
                        }

                        b = 0;
                    }
                    else
                        b = Get(i.Source, i.Width, i.Next);
                    if (i.Op != "mov")
                        a = Get(i.Destination, i.Width, i.Next);
                }
                else if (i.Op is "addi" or "subi" or "andi" or "ori" or "cmpi" or "testi" or "inc" or "dec" or "not" or "shl" or "shr")
                {
                    a = Get(i.Destination, i.Width, i.Next);
                    b = i.Immediate;
                }

                switch (i.Op)
                {
                    case "imm":
                        Set(i.Destination, i.Width, i.Immediate, i.Next);
                        break;
                    case "mov":
                        Set(i.Destination, i.Width, b, i.Next);
                        break;
                    case "zx":
                        Set(i.Destination, i.Width, Get(i.Source, 16, i.Next), i.Next);
                        break;
                    case "lea":
                        Set(i.Destination, i.Width, Address(i.Source, i.Next), i.Next);
                        break;
                    case "push":
                        Push(Get(i.Destination, 64, i.Next));
                        break;
                    case "pop":
                        Set(i.Destination, 64, Pop(), i.Next);
                        break;
                    case "pushi":
                        Push(i.Immediate);
                        break;
                    case "pushf":
                        Push(Flags);
                        break;
                    case "popf":
                        Flags = Pop();
                        break;
                    case "jmp":
                        Pc = i.Immediate;
                        break;
                    case "jcc":
                        if (Condition(i.Condition))
                            Pc = i.Immediate;
                        break;
                    case "cmp":
                    case "cmpi":
                        FlagsFor(unchecked(a - b), i.Width, a < (b & Mask(i.Width)));
                        break;
                    case "test":
                    case "testi":
                        FlagsFor(a & b, i.Width);
                        break;
                    case "and":
                    case "andi":
                        value = a & b;
                        Set(i.Destination, i.Width, value, i.Next);
                        FlagsFor(value, i.Width);
                        break;
                    case "or":
                    case "ori":
                        value = a | b;
                        Set(i.Destination, i.Width, value, i.Next);
                        FlagsFor(value, i.Width);
                        break;
                    case "xor":
                        value = a ^ b;
                        Set(i.Destination, i.Width, value, i.Next);
                        FlagsFor(value, i.Width);
                        break;
                    case "add":
                    case "addi":
                        value = unchecked(a + b);
                        Set(i.Destination, i.Width, value, i.Next);
                        FlagsFor(value, i.Width);
                        break;
                    case "sub":
                    case "subi":
                        value = unchecked(a - b);
                        Set(i.Destination, i.Width, value, i.Next);
                        FlagsFor(value, i.Width, a < b);
                        break;
                    case "inc":
                    case "dec":
                        value = unchecked(i.Op == "inc" ? a + 1 : a - 1);
                        Set(i.Destination, i.Width, value, i.Next);
                        FlagsFor(value, i.Width, (Flags & 1) != 0);
                        break;
                    case "not":
                        Set(i.Destination, i.Width, ~a, i.Next);
                        break;
                    case "shl":
                    case "shr":
                        int shift = (int)(i.Condition == 1 ? Registers[1] : b) & (i.Width == 64 ? 63 : 31);
                        value = i.Op == "shl" ? a << shift : a >> shift;
                        Set(i.Destination, i.Width, value, i.Next);
                        FlagsFor(value, i.Width);
                        break;
                    case "bts":
                        ulong bit = 1UL << ((int)b & 63);
                        Flags = (Flags & ~1UL) | ((a & bit) != 0 ? 1UL : 0);
                        Set(i.Destination, i.Width, a | bit, i.Next);
                        break;
                    case "cli":
                        Flags &= ~0x200UL;
                        Events.Add("cli");
                        break;
                    case "cld":
                        Flags &= ~0x400UL;
                        break;
                    case "hlt":
                        Require((Flags & 0x200) == 0, "terminal IF disabled");
                        Halted = true;
                        break;
                    case "nop":
                        break;
                    case "cpuid":
                        uint leaf = (uint)Registers[0];
                        Registers[0] = leaf == 0x80000000 ? MaximumLeaf : AddressSizes;
                        Registers[3] = 0x89abcdef;
                        Registers[1] = 0x76543210;
                        Registers[2] = 0xfedcba98;
                        break;
                    case "rdmsr":
                        Require((Cs & 3) == 0 && Events.Contains("lar"), "CPL/CS checked before MSR");
                        value = (uint)Registers[1] == 0x277 ? RawPat : (uint)Registers[1] == 0xc0000080 ? Efer : throw new InvalidOperationException("MSR");
                        Registers[0] = (uint)value;
                        Registers[2] = value >> 32;
                        break;
                    case "lar":
                        Set(i.Destination, i.Width, AccessRights, i.Next);
                        Flags = (Flags & ~0x40UL) | (LarSucceeds ? 0x40UL : 0);
                        Events.Add("lar");
                        break;
                    case "segread":
                        Set(i.Destination, 16, i.Immediate switch
                        {
                            0 => Es,
                            1 => Cs,
                            2 => Ss,
                            3 => Ds,
                            _ => throw new InvalidOperationException("segment")}, i.Next);
                        break;
                    case "segwrite":
                        value = Get(i.Destination, 16, i.Next);
                        if (i.Immediate == 0)
                            Es = value;
                        else if (i.Immediate == 2)
                            Ss = value;
                        else if (i.Immediate == 3)
                            Ds = value;
                        else
                            throw new InvalidOperationException("segment write");
                        break;
                    case "crread":
                        Require((Cs & 3) == 0 && Events.Contains("lar"), "CPL/CS checked before control register");
                        if (i.Immediate == 2)
                        {
                            Require(Registers[4] == _hardwareStackTop - 72 && Read(Registers[4], 64) == DiagnosticRegisters[0], "original RAX before early CR2");
                            EarlyCr2 = true;
                            _earlyCr2Value = Cr2;
                            Events.Add("cr2");
                        }

                        Set(i.Destination, 64, i.Immediate switch
                        {
                            0 => Cr0,
                            2 => Cr2,
                            3 => Cr3,
                            4 => Cr4,
                            _ => throw new InvalidOperationException("CR read")}, i.Next);
                        if (i.Immediate == 2 && ReplaceCr2AfterEarlyRead.HasValue)
                            Cr2 = ReplaceCr2AfterEarlyRead.Value;
                        break;
                    case "crwrite":
                        value = Get(i.Destination, 64, i.Next);
                        if (i.Immediate == 4)
                        {
                            Require((value ^ Cr4) == (Cr4 & 0x80), "clear only PGE");
                            Cr4 = value;
                            Events.Add("cr4");
                        }
                        else if (i.Immediate == 3)
                        {
                            Require(value == Fixture.Root && Registers[4] == Fixture.Data + 0xa000 && Tr == 24 && IdtBase == Fixture.Data && GdtBase == Fixture.Data + 4120, "descriptor/stack before CR3");
                            Cr3 = value;
                            _ownedRoot = true;
                            Events.Add("cr3");
                            AfterRoot?.Invoke(this);
                        }
                        else
                            throw new InvalidOperationException("forbidden CR write");
                        break;
                    case "lgdt":
                    case "lidt":
                        ulong address = Address(i.Destination, i.Next);
                        ushort limit = (ushort)Read(address, 16);
                        ulong table = Read(address + 2, 64);
                        if (i.Op == "lgdt")
                        {
                            GdtBase = table;
                            GdtLimit = limit;
                        }
                        else
                        {
                            IdtBase = table;
                            IdtLimit = limit;
                        }

                        Events.Add(i.Op);
                        break;
                    case "sgdt":
                    case "sidt":
                        address = Address(i.Destination, i.Next);
                        Write(address, i.Op == "sgdt" ? GdtLimit : IdtLimit, 16);
                        Write(address + 2, (i.Op == "sgdt" ? GdtBase : IdtBase) ^ (CorruptDescriptorReadBack ? 1UL : 0), 64);
                        break;
                    case "ltr":
                        Tr = Get(i.Destination, 16, i.Next);
                        Require(Tr == 24 && Read(GdtBase + 29, 8) == 0x89, "LTR available TSS");
                        Write(GdtBase + 29, 0x8b, 8);
                        Events.Add("ltr");
                        break;
                    case "str":
                        Set(i.Destination, 16, CorruptTrReadBack ? Tr ^ 8 : Tr, i.Next);
                        break;
                    case "copy":
                        Require(Registers[1] <= 2643 && (Flags & 0x400) == 0, "bounded forward copy");
                        while (Registers[1] != 0)
                        {
                            value = Read(Registers[6], i.Width);
                            Write(Registers[7], value, i.Width);
                            Registers[6] += (ulong)i.Width / 8;
                            Registers[7] += (ulong)i.Width / 8;
                            Registers[1]--;
                        }

                        break;
                    case "in":
                        Require((Registers[2] & 65535) == 0x3fd, "COM1 THRE port");
                        Polls++;
                        Registers[0] = (Registers[0] & ~255UL) | (SerialReady && Polls > InitialNotReadyPolls && Output.Length < SerialAvailableBytes ? 0x20UL : 0);
                        break;
                    case "lodsb":
                        Require((Flags & 0x400) == 0, "serial forward");
                        Registers[0] = (Registers[0] & ~255UL) | Read(Registers[6]++, 8);
                        break;
                    case "out":
                        Require((Registers[2] & 65535) == 0x3f8, "COM1 data port");
                        Output.Append((char)(Registers[0] & 255));
                        break;
                    default:
                        throw new InvalidOperationException("unsupported decoder operation " + i.Op + " at " + before.ToString("X"));
                }
            }
        }

        private void DeliverFault(ulong rip)
        {
            Require(_ownedRoot && ++Faults == 1, "one synthetic owned-root fault");
            // Independently model the Intel #PF -> nonpresent interrupt gate #NP -> #DF combination.
            Require(Read(IdtBase + 14 * 16 + 5, 8) == 0x0e, "#PF gate nonpresent causes #NP during delivery");
            Events.Add("pf-delivery");
            Events.Add("np-delivery");
            ulong gate = IdtBase + 8 * 16;
            Require(Read(gate + 2, 16) == 8 && Read(gate + 4, 8) == 1 && Read(gate + 5, 8) == 0x8e, "#DF uses valid present IST1 interrupt gate");
            ulong target = Read(gate, 16) | (Read(gate + 6, 16) << 16) | (Read(gate + 8, 32) << 32);
            Require(target == Fixture.Code + 8 * 16 && Tr == 24, "owned original DF gate and TSS");
            _hardwareStackTop = Read(Fixture.Data + 4096 + 100, 64);
            Registers[4] = _hardwareStackTop;
            for (int i = 0; i < 16; i++)
                if (i != 4)
                    Registers[i] = DiagnosticRegisters[i];
            Push(SavedSs);
            Push(SavedRsp);
            Push(SavedFlags);
            Push(SavedCs);
            Push(SavedRip);
            Push(0);
            HardwareWords = NormalizerWords;
            NormalizerWords = 0;
            Cs = 8;
            Ss = 0;
            Cr2 = DiagnosticCr2;
            Flags &= ~0x200UL;
            Pc = target;
            Events.Add("fault");
            AfterFault?.Invoke(this);
        }
    }
}
