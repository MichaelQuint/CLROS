namespace OS.Kernel.Compatibility.LinuxX64;
public static unsafe partial class X64LinuxUserTransition
{
    // Small fixed-layout encoder. Every memory operand uses supervisor R14+disp32.
    // Count/compare passes share encodings; independent host vectors audit ABI bytes.
    private struct Writer
    {
        private byte* _code;
        private bool _compare, _valid;
        private uint _limit;
        public uint Position;
        public Writer(byte* code, bool compare, uint limit = CodeBytes)
        {
            _code = code;
            _compare = compare;
            _valid = true;
            _limit = limit;
            Position = 0;
        }

        public bool Valid => _valid && Position == _limit;

        public void Bytes(ulong value, int count)
        {
            for (int i = 0; i < count; i++, Position++)
            {
                if (Position >= _limit)
                {
                    _valid = false;
                    continue;
                }

                byte valueByte = (byte)(value >> (8 * i));
                if (_code == null)
                    continue;
                if (_compare)
                {
                    if (_code[Position] != valueByte)
                        _valid = false;
                }
                else
                    _code[Position] = valueByte;
            }
        }

        public void Pad(uint target)
        {
            if (Position > target)
                _valid = false;
            while (Position < target)
                Bytes(0xcc, 1);
        }

        public void Immediate(uint register, ulong value)
        {
            Bytes(register < 8 ? 0x48UL : 0x49UL, 1);
            Bytes(0xb8 + (register & 7), 1);
            Bytes(value, 8);
        }

        public void Load(uint register, uint offset) => Memory(0x8b, register, offset);
        public void Store(uint register, uint offset) => Memory(0x89, register, offset);
        public void Lea(uint register, uint offset) => Memory(0x8d, register, offset);
        private void Memory(byte opcode, uint register, uint offset)
        {
            Bytes(register < 8 ? 0x49UL : 0x4dUL, 1);
            Bytes(opcode, 1);
            Bytes(0x86 | ((register & 7) << 3), 1);
            Bytes(offset, 4);
        }

        public void Fx(bool restore, uint offset)
        {
            Bytes(0x49, 1);
            Bytes(0xae0f, 2);
            Bytes(restore ? 0x8eUL : 0x86UL, 1);
            Bytes(offset, 4);
        }

        public void Descriptor(uint operation, uint offset)
        {
            Bytes(0x41, 1);
            Bytes(0x010f, 2);
            Bytes(0x86 | operation * 8, 1);
            Bytes(offset, 4);
        }

        public void Set(uint offset, uint value)
        {
            Bytes(0x49, 1);
            Bytes(0xc7, 1);
            Bytes(0x86, 1);
            Bytes(offset, 4);
            Bytes(value, 4);
        }

        public void ReadMsr(uint index)
        {
            Bytes(0xb9, 1);
            Bytes(index, 4);
            Bytes(0x320f, 2);
            Bytes(0x20e2c148, 4);
            Bytes(0xd00948, 3);
        }

        public void WriteMsr(uint index)
        {
            Bytes(0xb9, 1);
            Bytes(index, 4);
            Bytes(0xc28948, 3);
            Bytes(0x20eac148, 4);
            Bytes(0x300f, 2);
        }

        public void WriteVerifyMsr(uint index)
        {
            Bytes(0xc48949, 3);
            WriteMsr(index);
            ReadMsr(index);
            Bytes(0xe0394c, 3);
            Branch(0x85, ExitOffset);
        }

        public void CompareField(uint register, uint offset) => Memory(0x3b, register, offset);
        public void Branch(byte condition, uint target)
        {
            Bytes(0x0f, 1);
            Bytes(condition, 1);
            Bytes(unchecked(target - (Position + 4)), 4);
        }

        public void Jump(uint target)
        {
            Bytes(0xe9, 1);
            Bytes(unchecked(target - (Position + 4)), 4);
        }

        public void PushField(uint offset)
        {
            Load(0, offset);
            Bytes(0x50, 1);
        }

        public void Zero(uint register)
        {
            if (register >= 8)
                Bytes(0x45, 1);
            Bytes(0x31, 1);
            Bytes(0xc0 | ((register & 7) << 3) | (register & 7), 1);
        }

        public void GsMemory(bool load, uint register, uint offset)
        {
            Bytes(0x65, 1);
            Bytes(register < 8 ? 0x48UL : 0x4cUL, 1);
            Bytes(load ? 0x8bUL : 0x89UL, 1);
            Bytes(0x04 | ((register & 7) << 3), 1);
            Bytes(0x25, 1);
            Bytes(offset, 4);
        }

        public void Call(uint offset)
        {
            Load(0, offset);
            Bytes(0x20ec8348, 4);
            Bytes(0xd0ff, 2);
            Bytes(0x20c48348, 4);
        }

        public void StackLoad(byte offset)
        {
            Bytes(0x48, 1);
            Bytes(0x8b, 1);
            Bytes(0x2444, 2);
            Bytes(offset, 1);
        }

        public void StoreWord(uint offset)
        {
            Bytes(0x66, 1);
            Bytes(0x41, 1);
            Bytes(0x89, 1);
            Bytes(0x86, 1);
            Bytes(offset, 4);
        }

        private static uint SegmentCode(uint segment) => segment switch
        {
            0 => 3,
            1 => 0,
            2 => 4,
            3 => 5,
            _ => 2
        };
        public void ObservationStore(uint offset)
        {
            Bytes(0x878948, 3);
            Bytes(offset, 4);
        }

        public void ObservationSegment(uint segment)
        {
            Bytes(0x66, 1);
            Bytes(0x8c, 1);
            Bytes(0xc0 | SegmentCode(segment) * 8, 1);
        }

        private static uint SegmentOffset(uint segment) => segment == 4 ? SavedSsOffset : 0x1d0 + segment * 2;
        public void SaveSegment(uint segment)
        {
            Bytes(0x66, 1);
            Bytes(0x8c, 1);
            Bytes(0xc0 | SegmentCode(segment) * 8, 1);
            StoreWord(SegmentOffset(segment));
        }

        public void RestoreSegment(uint segment)
        {
            Bytes(0x66, 1);
            Bytes(0x41, 1);
            Bytes(0x8b, 1);
            Bytes(0x86, 1);
            Bytes(SegmentOffset(segment), 4);
            Bytes(0x8e, 1);
            Bytes(0xc0 | SegmentCode(segment) * 8, 1);
        }
    }
}
