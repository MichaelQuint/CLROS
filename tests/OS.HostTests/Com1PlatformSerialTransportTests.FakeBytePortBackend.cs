using System.Runtime.CompilerServices;
using OS.Kernel.Diagnostics;
using OS.Kernel.Platform;

internal static partial class Com1PlatformSerialTransportTests
{
    private sealed class FakeBytePortBackend : IBytePortBackend
    {
        private readonly SerialPortWrite[] _writes = new SerialPortWrite[1024];
        private readonly byte[] _dataBytes = new byte[1024];
        private readonly byte[] _lineStatusSequence = new byte[64];
        private int _lineStatusSequenceLength;
        public byte LineStatusValue
        {
            get; set;
        }
        public int FailWriteAfter { get; set; } = -1;
        public int WriteCount
        {
            get; private set;
        }
        public int ReadCount
        {
            get; private set;
        }
        public int InitializationWriteCount
        {
            get; private set;
        }
        public int DataWriteCount
        {
            get; private set;
        }

        public bool TryReadByte(ushort port, out byte value)
        {
            ReadCount++;
            if (_lineStatusSequenceLength != 0 && ReadCount <= _lineStatusSequenceLength)
            {
                value = _lineStatusSequence[ReadCount - 1];
                return true;
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
            if (InitializationWriteCount < SerialPortConfiguration.RequiredInitializationWriteCount)
            {
                InitializationWriteCount++;
            }
            else if (port == SerialPortConfiguration.Com1BasePort)
            {
                _dataBytes[DataWriteCount] = value;
                DataWriteCount++;
            }

            return true;
        }

        public byte DataByteAt(int index)
        {
            if (index < 0 || index >= DataWriteCount)
            {
                return 0;
            }

            return _dataBytes[index];
        }

        public bool DataStartsWith(ReadOnlySpan<byte> prefix)
        {
            if (DataWriteCount < prefix.Length)
            {
                return false;
            }

            for (int index = 0; index < prefix.Length; index++)
            {
                if (DataByteAt(index) != prefix[index])
                {
                    return false;
                }
            }

            return true;
        }

        public bool DataEndsWithCrLf()
        {
            return DataWriteCount >= 2 && DataByteAt(DataWriteCount - 2) == OwnedSerialDiagnostics.CarriageReturn && DataByteAt(DataWriteCount - 1) == OwnedSerialDiagnostics.LineFeed;
        }

        public void SetLineStatusSequence(byte first, byte second)
        {
            _lineStatusSequence[0] = first;
            _lineStatusSequence[1] = second;
            _lineStatusSequenceLength = 2;
        }
    }
}
