namespace OS.Kernel.Diagnostics;

public static unsafe partial class UnexpectedVectorPanicEvidenceDeterministicQEMUFailureClassification
{
    private struct MetadataWriter(byte* destination)
    {
        private readonly byte* _destination = destination;
        public int Position;
        public void Ascii(ulong packed, int count)
        {
            for (int i = 0; i < count; i++)
                _destination[Position++] = (byte)(packed >> (i * 8));
        }

        public void Hex(ulong value)
        {
            for (int shift = 60; shift >= 0; shift -= 4)
            {
                int digit = (int)((value >> shift) & 15);
                _destination[Position++] = (byte)(digit < 10 ? 48 + digit : 55 + digit);
            }
        }
    }
}
