using System.Runtime.CompilerServices;
using System.Text;
using OS.Kernel.Diagnostics;

internal static partial class PanicSerialReporterTests
{
    private sealed class CapturingByteSink : IPanicSerialByteSink
    {
        private readonly byte[] _buffer;
        private readonly int _failAfterBytes;
        private int _written;
        public CapturingByteSink(int capacity) : this(capacity, -1)
        {
        }

        public CapturingByteSink(int capacity, int failAfterBytes)
        {
            _buffer = new byte[capacity];
            _failAfterBytes = failAfterBytes;
        }

        public int BytesWritten => _written;
        public string Text => Encoding.ASCII.GetString(_buffer, 0, _written);

        public bool TryWriteByte(byte value)
        {
            if (_failAfterBytes >= 0 && _written >= _failAfterBytes)
            {
                return false;
            }

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
