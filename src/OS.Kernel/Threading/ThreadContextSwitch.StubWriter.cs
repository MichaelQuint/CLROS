namespace OS.Kernel.Threading;

public static unsafe partial class ThreadContextSwitch
{
    internal struct StubWriter
    {
        private byte* _output;
        private ulong _address;
        private uint _limit;
        private bool _compare, _valid;
        private uint _position;
        public StubWriter(byte* output, ulong address, bool compare, uint limit = StubBytes)
        {
            _output = output;
            _address = address;
            _compare = compare;
            _limit = limit;
            _valid = true;
            _position = 0;
        }

        public bool Valid => _valid && _position == _limit;

        public void Bytes(ulong value, int count)
        {
            // Little-endian x64 instruction fields; the unaligned read stays
            // inside the admitted, exclusively owned ordinary-memory extent.
            if (_compare && _output != null && count == 8 && _position <= _limit && _limit - _position >= 8)
            {
                if (*(ulong*)(_output + _position) != value)
                    _valid = false;
                _position += 8;
                return;
            }

            for (int i = 0; i < count; i++, _position++)
            {
                if (_position >= _limit)
                {
                    _valid = false;
                    continue;
                }

                byte next = (byte)(value >> (i * 8));
                if (_output == null)
                    continue;
                if (_compare)
                {
                    if (_output[_position] != next)
                        _valid = false;
                }
                else
                    _output[_position] = next;
            }
        }

        public void Pad() => Pad(_limit);
        public void Pad(uint target)
        {
            if (_position > target || target > _limit)
                _valid = false;
            uint end = target < _limit ? target : _limit;
            // Uniform INT3 bytes have the same word value in either byte order.
            // Unaligned x64 accesses stay entirely inside the admitted padding.
            while (_position < end && end - _position >= 8)
            {
                if (_output != null)
                {
                    ulong* word = (ulong*)(_output + _position);
                    if (_compare)
                    {
                        if (*word != 0xccccccccccccccccUL)
                            _valid = false;
                    }
                    else
                        *word = 0xccccccccccccccccUL;
                }

                _position += 8;
            }

            while (_position < end)
                Bytes(0xcc, 1);
        }

        public void Call(ulong target)
        {
            Bytes(0xe8, 1);
            Displacement(target, 4);
        }

        public void Jump(ulong target)
        {
            Bytes(0xe9, 1);
            Displacement(target, 4);
        }

        public void Below(ulong target)
        {
            Bytes(0x820f, 2);
            Displacement(target, 4);
        }

        public void PushMemory(ulong target)
        {
            Bytes(0x35ff, 2);
            Displacement(target, 4);
        }

        public void PopMemory(ulong target)
        {
            Bytes(0x058f, 2);
            Displacement(target, 4);
        }

        public void CompareByteLimit(ulong target, byte limit)
        {
            Bytes(0x3d83, 2);
            Displacement(target, 5);
            Bytes(limit, 1);
        } // cmp dword [rip+disp32],imm8

        public void Displacement(ulong target, uint trailingBytes)
        {
            ulong next = _address + _position + trailingBytes;
            uint bits;
            if (target >= next)
            {
                ulong distance = target - next;
                if (distance > 0x7fffffff)
                    _valid = false;
                bits = (uint)distance;
            }
            else
            {
                ulong distance = next - target;
                if (distance > 0x80000000)
                    _valid = false;
                bits = unchecked(0U - (uint)distance);
            }

            Bytes(bits, 4);
        }

        public void Memory(bool load, uint register, ulong address)
        {
            Bytes(register < 8 ? 0x48UL : 0x4cUL, 1);
            Bytes(load ? 0x8bUL : 0x89UL, 1);
            Bytes(5 | ((register & 7) << 3), 1);
            Displacement(address, 4);
        }

        public void Set(ulong address, uint value)
        {
            Bytes(0x05c748, 3);
            Displacement(address, 8);
            Bytes(value, 4);
        }

        public void Immediate(uint register, ulong value)
        {
            Bytes(register < 8 ? 0x48UL : 0x49UL, 1);
            Bytes(0xb8 + (register & 7), 1);
            Bytes(value, 8);
        }

        public void Fx(bool restore, ulong address)
        {
            Bytes(0xae0f48, 3);
            Bytes(restore ? 0x0dUL : 5UL, 1);
            Displacement(address, 4);
        }

        public void ReadMsr(uint index)
        {
            Bytes(0xb9, 1);
            Bytes(index, 4);
            Bytes(0x320f, 2);
            Bytes(0x20e2c148, 4);
            Bytes(0xd00948, 3);
        }

        public void WriteVerifyMsr(uint index, ulong reject)
        {
            Bytes(0xc28949, 3); // r10=expected RAX, before WRMSR scratch.
            Bytes(0xb9, 1);
            Bytes(index, 4);
            Bytes(0xc28948, 3);
            Bytes(0x20eac148, 4);
            Bytes(0x300f, 2);
            ReadMsr(index);
            Bytes(0xd0394c, 3);
            Reject(reject); // cmp rax,r10.
        }

        public void Reject(ulong target)
        {
            Bytes(0x850f, 2);
            Displacement(target, 4);
        }
    }
}
