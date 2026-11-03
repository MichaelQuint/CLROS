namespace OS.Kernel.Threading;

public static unsafe partial class X64TimerInterrupt
{
    private struct Writer(byte* output, bool compare)
    {
        public uint Offset;
        public bool Valid = true;
        private readonly byte* _output = output;
        private readonly bool _compare = compare;
        public void Byte(byte value)
        {
            if (Offset >= Bytes)
            {
                Valid = false;
                return;
            }

            if (_output != null)
            {
                if (_compare)
                    Valid &= _output[Offset] == value;
                else
                    _output[Offset] = value;
            }

            Offset++;
        }

        public void Data(ulong value, uint bytes)
        {
            for (uint i = 0; i < bytes; i++)
                Byte((byte)(value >> (int)(8 * i)));
        }

        public uint Jne()
        {
            Data(0x850f, 2);
            uint position = Offset;
            Offset += 4;
            return position;
        }

        public void Patch(uint position, uint target)
        {
            if (position > Offset || Offset - position < 4 || target >= Bytes)
            {
                Valid = false;
                return;
            }

            uint displacement = unchecked(target - (position + 4));
            for (uint i = 0; i < 4; i++)
            {
                byte value = (byte)(displacement >> (int)(8 * i));
                if (_output != null)
                {
                    if (_compare)
                        Valid &= _output[position + i] == value;
                    else
                        _output[position + i] = value;
                }
            }
        }
    }
}
