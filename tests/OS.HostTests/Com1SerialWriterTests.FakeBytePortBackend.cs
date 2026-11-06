using System.Runtime.CompilerServices;
using OS.Kernel.Diagnostics;

internal static partial class Com1SerialWriterTests
{
    private sealed class FakeBytePortBackend : IBytePortBackend
    {
        private readonly SerialPortWrite[] _writes = new SerialPortWrite[32];
        private readonly ushort[] _readPorts = new ushort[32];
        public byte LineStatusValue
        {
            get; set;
        }
        public int FailWriteAfter { get; set; } = -1;
        public bool FailReads
        {
            get; set;
        }
        public int WriteCount
        {
            get; private set;
        }
        public int ReadCount
        {
            get; private set;
        }

        public bool TryReadByte(ushort port, out byte value)
        {
            _readPorts[ReadCount] = port;
            ReadCount++;
            if (FailReads)
            {
                value = 0;
                return false;
            }

            value = LineStatusValue;
            return true;
        }

        public bool TryWriteByte(ushort port, byte value)
        {
            if (FailWriteAfter >= 0 && WriteCount >= FailWriteAfter)
            {
                return false;
            }

            _writes[WriteCount] = new SerialPortWrite(port, value);
            WriteCount++;
            return true;
        }

        public SerialPortWrite GetWrite(int index)
        {
            return _writes[index];
        }

        public ushort GetReadPort(int index)
        {
            return _readPorts[index];
        }

        public void Clear()
        {
            WriteCount = 0;
            ReadCount = 0;
            FailWriteAfter = -1;
            FailReads = false;
        }
    }
}
