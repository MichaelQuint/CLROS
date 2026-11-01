namespace OS.Kernel.Diagnostics;
public static unsafe partial class DoubleFaultISTPathSilentRebootOrTriple
{
    private struct Writer
    {
        private readonly byte* _pointer;
        private readonly byte _mode;
        public int Position, Mismatch;
        public ulong Hash;
        public bool Valid;
        public Writer(byte* pointer, byte mode)
        {
            _pointer = pointer;
            _mode = mode;
            Position = 0;
            Mismatch = -1;
            Hash = 14695981039346656037;
            Valid = true;
        }

        public void Byte(byte value)
        {
            if (_mode == 1)
                _pointer[Position] = value;
            byte actual = _mode == 2 ? _pointer[Position] : value;
            if (actual != value && Mismatch < 0)
                Mismatch = Position;
            Hash = (Hash ^ actual) * 1099511628211;
            Position++;
        }

        public void Bytes(byte a, byte b)
        {
            Byte(a);
            Byte(b);
        }

        public void Bytes(byte a, byte b, byte c)
        {
            Bytes(a, b);
            Byte(c);
        }

        public void Bytes(byte a, byte b, byte c, byte d)
        {
            Bytes(a, b, c);
            Byte(d);
        }

        public void Word(int value)
        {
            Byte((byte)value);
            Byte((byte)(value >> 8));
        }

        public void Dword(uint value)
        {
            for (int i = 0; i < 4; i++)
                Byte((byte)(value >> (i * 8)));
        }

        public void Qword(ulong value)
        {
            for (int i = 0; i < 8; i++)
                Byte((byte)(value >> (i * 8)));
        }

        public void Pad(int target, byte fill = 0xf4)
        {
            if (Position > target)
            {
                Valid = false;
                if (Mismatch < 0)
                    Mismatch = Position;
            }

            while (Position < target)
                Byte(fill);
        }

        public void Relative(int target) => Dword((uint)(target - Position - 4));
        public void Jump(int target)
        {
            Byte(0xe9);
            Relative(target);
        }

        public void Branch(byte condition, int target)
        {
            Bytes(0x0f, condition);
            Relative(target);
        }

        public void MovAbs(int register, ulong value)
        {
            Bytes((byte)(0x48 | (register >> 3)), (byte)(0xb8 | (register & 7)));
            Qword(value);
        }

        public void Load(int offset)
        {
            Bytes(0x49, 0x8b, 0x87);
            Dword((uint)offset);
        }

        public void Store(int offset)
        {
            Bytes(0x49, 0x89, 0x87);
            Dword((uint)offset);
        }

        public void Compare(int offset, int failure)
        {
            Bytes(0x49, 0x39, 0x87);
            Dword((uint)offset);
            Branch(0x85, failure);
        }

        public void Immediate(int offset, uint value, int failure)
        {
            Bytes(0x49, 0x81, 0xbf);
            Dword((uint)offset);
            Dword(value);
            Branch(0x85, failure);
        }

        public void Lea(int register, int target)
        {
            Bytes((byte)(0x48 | ((register >> 3) << 2)), 0x8d, (byte)(0x05 | ((register & 7) << 3)));
            Relative(target);
        }
    }
}
