using System.Runtime.CompilerServices;
using OS.Boot;
using OS.Kernel.Diagnostics;
using OS.Kernel.Runtime;

internal static partial class DiagnosticsChainReadinessTests
{
    private sealed class CapturingByteSink : IPanicSerialByteSink
    {
        private readonly byte[] _buffer;
        private int _written;
        public CapturingByteSink(int capacity)
        {
            _buffer = new byte[capacity];
        }

        public bool TryWriteByte(byte value)
        {
            if (_written >= _buffer.Length)
            {
                return false;
            }

            _buffer[_written] = value;
            _written++;
            return true;
        }
    }
}
