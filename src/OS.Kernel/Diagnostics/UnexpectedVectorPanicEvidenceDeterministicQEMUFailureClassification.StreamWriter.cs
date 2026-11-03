namespace OS.Kernel.Diagnostics;

public static unsafe partial class UnexpectedVectorPanicEvidenceDeterministicQEMUFailureClassification
{
    private struct StreamWriter(byte* bytes, bool compare)
    {
        private readonly byte* _bytes = bytes;
        private readonly bool _compare = compare;
        public int Position;
        public int Mismatch = -1;
        public void B(byte value)
        {
            if (_compare)
            {
                if (_bytes[Position] != value && Mismatch < 0)
                    Mismatch = Position;
            }
            else
                _bytes[Position] = value;
            Position++;
        }

        public void D(uint value)
        {
            for (int i = 0; i < 4; i++)
                B((byte)(value >> (i * 8)));
        }

        public void Q(ulong value)
        {
            for (int i = 0; i < 8; i++)
                B((byte)(value >> (i * 8)));
        }

        public void Pad(int end)
        {
            while (Position < end)
                B(0x90);
        }

        public void Jump(int target)
        {
            B(0xe9);
            D(unchecked((uint)(target - Position - 4)));
        }

        public void NotEqual(int target)
        {
            B(0x0f);
            B(0x85);
            D(unchecked((uint)(target - Position - 4)));
        }

        public void StoreRax(int offset)
        {
            B(0x48);
            B(0x89);
            B(0x82);
            D((uint)offset);
        }

        public void Selector(byte modRm, int offset)
        {
            B(0x31);
            B(0xc0);
            B(0x66);
            B(0x8c);
            B(modRm);
            StoreRax(offset);
        }
    }
}
