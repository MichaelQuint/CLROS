using System.Buffers.Binary;
using System.Diagnostics;
using System.Reflection.Metadata;
using System.Reflection.PortableExecutable;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Text;
using OS.Kernel.Diagnostics;
using Leaf = OS.Kernel.Diagnostics.UnexpectedVectorPanicEvidenceDeterministicQEMUFailureClassification;

internal static unsafe partial class UnexpectedVectorPanicEvidenceDeterministicQEMUFailureClassificationTests
{
    private sealed class Machine
    {
        private readonly Fixture _f;
        private readonly ulong[] _r = Enumerable.Range(0, 16).Select(i => 0x7654321000000000UL + (ulong)i).ToArray();
        private readonly ulong _cr0, _cr4, _cr3;
        private readonly bool _badLar, _badLarValue;
        private ushort _cs, _ss = 0x30, _ds = 0x30, _es = 0x30, _tr;
        private ulong _gdt, _idt;
        private bool _zero, _df = true, _if = true;
        public bool Called, Halted, DescriptorsLoaded;
        public int CaptureDepth, CallbackDepth;
        public Machine(Fixture f, int invalid = 0)
        {
            _f = f;
            _cs = (ushort)(invalid == 1 ? 0x40 : f.R.FirmwareCs);
            _badLar = invalid == 2;
            _badLarValue = invalid == 12;
            _cr0 = invalid == 3 ? 0x80000005UL : invalid == 11 ? 0x80000009UL : invalid == 8 ? 1UL : 0x80000011UL;
            _cr4 = invalid == 4 ? 0x800600UL : invalid == 5 ? 0x20600UL : invalid == 6 ? 0x600UL | 0x1000UL : invalid == 9 ? 0x200UL : invalid == 10 ? 0x400UL : 0x600UL;
            _cr3 = invalid == 7 ? f.R.FirmwareCr3 + 0x1000 : f.R.FirmwareCr3;
        }

        private byte* Memory(ulong address, int count)
        {
            Require(address >= _f.R.DataAddress && address - _f.R.DataAddress <= 28672UL - (ulong)count, "modeled store within original D7");
            return _f.Data + (int)(address - _f.R.DataAddress);
        }

        private void Push(ulong value)
        {
            _r[4] -= 8;
            Q(Memory(_r[4], 8), 0, value);
        }

        public void Run()
        {
            int pc = 4608;
            int budget = 512;
            while (!Halted && budget-- > 0)
            {
                Instruction i = Decode(_f.Code, pc);
                pc = i.End;
                switch (i.Op)
                {
                    case "nop":
                        break;
                    case "mov64":
                        _r[i.Register] = i.Value;
                        break;
                    case "zero-eax":
                        _r[0] = 0;
                        _zero = true;
                        break;
                    case "mov-ax":
                        _r[0] = (_r[0] & ~65535UL) | i.Value;
                        break;
                    case "read-selector":
                        _r[0] = (_r[0] & ~65535UL) | (i.Value switch
                        {
                            0xc8 => _cs,
                            0xd0 => _ss,
                            0xd8 => _ds,
                            0xc0 => _es,
                            _ => throw new InvalidOperationException("selector")});
                        break;
                    case "load-selector":
                        Require(_r[0] == 16 && DescriptorsLoaded, "owned data selector");
                        if (i.Value == 0xd8)
                            _ds = 16;
                        else if (i.Value == 0xc0)
                            _es = 16;
                        else if (i.Value == 0xd0)
                            _ss = 16;
                        else
                            throw new InvalidOperationException("load selector");
                        _f.Context[45] |= 1;
                        break;
                    case "cmp-eax":
                        _zero = (uint)_r[0] == (uint)i.Value;
                        break;
                    case "and-eax":
                        _r[0] = (uint)_r[0] & (uint)i.Value;
                        _zero = _r[0] == 0;
                        break;
                    case "test-eax":
                        _zero = ((uint)_r[0] & (uint)i.Value) == 0;
                        break;
                    case "cmp-r8":
                        _zero = _r[0] == _r[8];
                        break;
                    case "jne":
                        if (!_zero)
                            pc = (int)i.Value;
                        break;
                    case "jump":
                        pc = (int)i.Value;
                        break;
                    case "lar":
                        _zero = !_badLar;
                        if (_zero)
                            _r[1] = _badLarValue ? 0xc09b00UL : 0xa09b00UL;
                        break;
                    case "read-control":
                        _r[0] = i.Value switch
                        {
                            0xc0 => _cr0,
                            0xe0 => _cr4,
                            0xd8 => _cr3,
                            0xd0 => 0xfedcba9876543210,
                            _ => throw new InvalidOperationException("control")};
                        break;
                    case "store64":
                        Q(Memory(_r[2] + i.Value, 8), 0, _r[i.Register]);
                        break;
                    case "store-eax":
                        BinaryPrimitives.WriteUInt32LittleEndian(new Span<byte>(Memory(_r[2] + i.Value, 4), 4), (uint)_r[0]);
                        break;
                    case "store-ax":
                        BinaryPrimitives.WriteUInt16LittleEndian(new Span<byte>(Memory(_r[2] + i.Value, 2), 2), (ushort)_r[0]);
                        break;
                    case "rcx-to-rax":
                        _r[0] = _r[1];
                        break;
                    case "rsp-to-rcx":
                        _r[1] = _r[4];
                        CaptureDepth = (int)(_f.R.DataAddress + 28672 - _r[4]);
                        break;
                    case "push":
                        Push(_r[i.Register]);
                        break;
                    case "push-immediate":
                        Push(i.Value);
                        break;
                    case "cli":
                        _if = false;
                        break;
                    case "cld":
                        _df = false;
                        break;
                    case "sub-rsp":
                        _r[4] -= i.Value;
                        break;
                    case "lgdt":
                        Require(!_if && _r[4] == _f.R.DataAddress + 28672, "CLI and new stack precede LGDT");
                        _gdt = U(Memory(_r[2], 10), 2);
                        DescriptorsLoaded = true;
                        break;
                    case "lidt":
                        _idt = U(Memory(_r[2] + i.Value, 10), 2);
                        break;
                    case "ltr":
                        Require(_r[0] == 24, "LTR selector");
                        _tr = 24;
                        _f.Context[53] = 0x8b;
                        break;
                    case "str":
                        _r[0] = _tr;
                        break;
                    case "sgdt":
                        Memory(_r[2] + i.Value, 10)[0] = 39;
                        Q(Memory(_r[2] + i.Value, 10), 2, _gdt);
                        break;
                    case "sidt":
                        Memory(_r[2] + i.Value, 10)[0] = 255;
                        Memory(_r[2] + i.Value, 10)[1] = 15;
                        Q(Memory(_r[2] + i.Value, 10), 2, _idt);
                        break;
                    case "interrupt":
                        Require(i.Value == 32 && _idt == _f.R.DataAddress && _ss == 16 && (_r[4] & 15) == 0, "INT32 owned route");
                        ulong[] original = (ulong[])_r.Clone();
                        ulong top = _r[4];
                        Push(_ss);
                        Push(top);
                        Push(2 | (_if ? 0x200UL : 0) | (_df ? 0x400UL : 0));
                        Push(_cs);
                        Push(_f.R.CodeAddress + (ulong)pc);
                        _cs = 8;
                        _f.Context[37] |= 1;
                        _if = false;
                        pc = 32 * 16;
                        // All saved registers are independently compared at the actual CALL below.
                        _interrupted = original;
                        break;
                    case "call-rax":
                        Require(_r[0] == _f.R.CallbackAddress && _r[1] == _f.R.DataAddress + 28488 && _r[2] == _f.R.DataAddress + 4096 && !_df && !_if && (_r[4] & 15) == 0, "scalar callback register lifetime/alignment");
                        Push(_f.R.CodeAddress + (ulong)pc);
                        CallbackDepth = (int)(_f.R.DataAddress + 28672 - _r[4]);
                        Require(CallbackDepth == 232 && _r[4] == _f.R.DataAddress + 28440 && _r[4] + 8 + 32 <= _r[1] && _r[1] - (_r[4] + 8 + 32) == 8, "return/home/padding/capture disjoint");
                        int[] registers = [15, 14, 13, 12, 11, 10, 9, 8, 5, 7, 6, 2, 1, 3, 0];
                        for (int n = 0; n < 15; n++)
                            Require(U(_f.Capture, n * 8) == _interrupted![registers[n]], "actual push register identity");
                        Require(Leaf.ValidateCapturedState(_f.Code, 8192, _f.Data, 28672, _f.R, out _).Succeeded, "model callback input valid");
                        Called = true;
                        _r[4] += 8;
                        _r[0] = 0x12345678;
                        _r[2] = 0xdeadbeef;
                        break;
                    case "halt":
                        Halted = true;
                        break;
                    default:
                        throw new InvalidOperationException("unmodeled " + i.Op);
                }
            }

            Require(Halted && budget > 0, "bounded model completion");
        }

        private ulong[]? _interrupted;
    }
}
