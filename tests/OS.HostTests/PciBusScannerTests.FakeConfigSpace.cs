using System.Runtime.CompilerServices;
using OS.Drivers;
using OS.Std.Binary;

internal static partial class PciBusScannerTests
{
    private sealed class FakeConfigSpace
    {
        private readonly HeaderSlot[] _headers = new HeaderSlot[16];
        private readonly ReadRequest[] _reads = new ReadRequest[128];
        private int _headerCount;
        private bool _hasFailure;
        private byte _failureBus;
        private byte _failureDevice;
        private byte _failureFunction;
        private PciConfigurationSpaceReadStatus _failureStatus;
        public int ReadCount { get; private set; }

        public void SetHeader(byte bus, byte device, byte function, byte[] headerBytes)
        {
            _headers[_headerCount] = new HeaderSlot(bus, device, function, headerBytes);
            _headerCount++;
        }

        public void FailRead(byte bus, byte device, byte function, PciConfigurationSpaceReadStatus status)
        {
            _hasFailure = true;
            _failureBus = bus;
            _failureDevice = device;
            _failureFunction = function;
            _failureStatus = status;
        }

        public bool HasRead(byte bus, byte device, byte function)
        {
            for (int index = 0; index < ReadCount; index++)
            {
                ReadRequest read = _reads[index];
                if (read.Bus == bus && read.Device == device && read.Function == function)
                {
                    return true;
                }
            }

            return false;
        }

        public PciConfigurationSpaceReadStatus Read(byte bus, byte device, byte function, Span<byte> destination)
        {
            if (ReadCount < _reads.Length)
            {
                _reads[ReadCount] = new ReadRequest(bus, device, function);
            }

            ReadCount++;
            if (destination.Length < PciConfigurationHeader.Type0HeaderLength)
            {
                return PciConfigurationSpaceReadStatus.DestinationTooSmall;
            }

            if (_hasFailure && bus == _failureBus && device == _failureDevice && function == _failureFunction)
            {
                return _failureStatus;
            }

            for (int index = 0; index < _headerCount; index++)
            {
                HeaderSlot header = _headers[index];
                if (header.Bus == bus && header.Device == device && header.Function == function)
                {
                    header.Bytes.AsSpan().CopyTo(destination);
                    return PciConfigurationSpaceReadStatus.Success;
                }
            }

            destination.Clear();
            EndianPrimitives.WriteUInt16LittleEndian(destination.Slice(0x00, sizeof(ushort)), PciConfigurationHeader.InvalidVendorId);
            return PciConfigurationSpaceReadStatus.Success;
        }
    }
}
