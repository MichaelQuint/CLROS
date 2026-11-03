namespace OS.Kernel.Diagnostics;

public static unsafe partial class DisabledDefaultUd2VectorStateTransition
{
#if !NOSTDLIB_EFI
#endif
    private unsafe struct StreamBuilder
    {
        private byte* _destination;
        private byte* _source;
        private byte _mode;
        private int _firstMismatchOffset;
        public int Offset;
        public ulong Fingerprint;
        public int FirstMismatchOffset => _firstMismatchOffset;
        public int BytesCompared => _mode == 2 ? (_firstMismatchOffset >= 0 ? _firstMismatchOffset + 1 : Offset) : 0;

        public static StreamBuilder CreateCount()
        {
            StreamBuilder builder = default;
            builder.Fingerprint = Fnv1A64OffsetBasis;
            builder._firstMismatchOffset = -1;
            return builder;
        }

        public static StreamBuilder CreateWriter(byte* destination)
        {
            StreamBuilder builder = CreateCount();
            builder._destination = destination;
            builder._mode = 1;
            return builder;
        }

        public static StreamBuilder CreateReader(byte* source)
        {
            StreamBuilder builder = CreateCount();
            builder._source = source;
            builder._mode = 2;
            return builder;
        }

        public void Emit(byte value)
        {
            if (_mode == 1)
            {
                _destination[Offset] = value;
            }
            else if (_mode == 2 && _firstMismatchOffset < 0 && _source[Offset] != value)
            {
                _firstMismatchOffset = Offset;
            }

            Fingerprint ^= value;
            Fingerprint = unchecked(Fingerprint * Fnv1A64Prime);
            Offset++;
        }

        public void Emit(byte first, byte second)
        {
            Emit(first);
            Emit(second);
        }

        public void Emit(byte first, byte second, byte third)
        {
            Emit(first);
            Emit(second);
            Emit(third);
        }

        public void Emit(byte first, byte second, byte third, byte fourth)
        {
            Emit(first);
            Emit(second);
            Emit(third);
            Emit(fourth);
        }

        public void Emit(byte first, byte second, byte third, byte fourth, byte fifth)
        {
            Emit(first);
            Emit(second);
            Emit(third);
            Emit(fourth);
            Emit(fifth);
        }

        public void Emit(byte first, byte second, byte third, byte fourth, byte fifth, byte sixth)
        {
            Emit(first);
            Emit(second);
            Emit(third);
            Emit(fourth);
            Emit(fifth);
            Emit(sixth);
        }

        public void Emit(byte first, byte second, byte third, byte fourth, byte fifth, byte sixth, byte seventh)
        {
            Emit(first);
            Emit(second);
            Emit(third);
            Emit(fourth);
            Emit(fifth);
            Emit(sixth);
            Emit(seventh);
        }

        public void Emit(byte b0, byte b1, byte b2, byte b3, byte b4, byte b5, byte b6, byte b7, byte b8, byte b9, byte b10, byte b11, byte b12, byte b13)
        {
            Emit(b0);
            Emit(b1);
            Emit(b2);
            Emit(b3);
            Emit(b4);
            Emit(b5);
            Emit(b6);
            Emit(b7);
            Emit(b8);
            Emit(b9);
            Emit(b10);
            Emit(b11);
            Emit(b12);
            Emit(b13);
        }

        public void Emit(byte b0, byte b1, byte b2, byte b3, byte b4, byte b5, byte b6, byte b7, byte b8, byte b9, byte b10, byte b11, byte b12, byte b13, byte b14, byte b15)
        {
            Emit(b0);
            Emit(b1);
            Emit(b2);
            Emit(b3);
            Emit(b4);
            Emit(b5);
            Emit(b6);
            Emit(b7);
            Emit(b8);
            Emit(b9);
            Emit(b10);
            Emit(b11);
            Emit(b12);
            Emit(b13);
            Emit(b14);
            Emit(b15);
        }

        public void EmitU16(ushort value)
        {
            Emit((byte)value);
            Emit((byte)(value >> 8));
        }

        public void EmitU32(int value)
        {
            EmitU32(unchecked((uint)value));
        }

        public void EmitU32(uint value)
        {
            Emit((byte)value);
            Emit((byte)(value >> 8));
            Emit((byte)(value >> 16));
            Emit((byte)(value >> 24));
        }

        public void EmitU64(ulong value)
        {
            for (int index = 0; index < 8; index++)
            {
                Emit((byte)(value >> (index * 8)));
            }
        }

        public void EmitRel32(int targetOffset)
        {
            int displacement = targetOffset - (Offset + 4);
            EmitU32(displacement);
        }
    }
}
