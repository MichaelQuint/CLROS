namespace OS.Kernel.Diagnostics;

public static unsafe partial class PanicSafeFormattingSerialReportingOwnedCom1
{
    private struct PanicSafeAsciiPointerWriter
    {
        private readonly byte* _destination;
        private int _written;
        public PanicSafeAsciiPointerWriter(byte* destination)
        {
            _destination = destination;
            _written = 0;
        }

        public int Written => _written;

        public void Write(byte* value, int length)
        {
            for (int index = 0; index < length; index++)
            {
                WriteByte(value[index]);
            }
        }

        public void WriteByte(byte value)
        {
            _destination[_written] = value;
            _written++;
        }

        public void WriteUnsignedDecimal(uint value)
        {
            if (value >= 100)
            {
                WriteByte((byte)('0' + (value / 100)));
                value %= 100;
                WriteByte((byte)('0' + (value / 10)));
                WriteByte((byte)('0' + (value % 10)));
                return;
            }

            if (value >= 10)
            {
                WriteByte((byte)('0' + (value / 10)));
            }

            WriteByte((byte)('0' + (value % 10)));
        }

        public void WriteHex(ulong value)
        {
            WriteByte((byte)'0');
            WriteByte((byte)'x');
            for (int shift = 60; shift >= 0; shift -= 4)
            {
                int nibble = (int)((value >> shift) & 0x0f);
                WriteByte((byte)(nibble < 10 ? '0' + nibble : 'A' + nibble - 10));
            }
        }
    }
}
