using System.Runtime.CompilerServices;
using OS.Drivers;
using OS.Std.Binary;

internal static class PciBusScannerTests
{

    [HostTest("drivers.pci.scan records single device", HostLane.Fast, 0)]
    private static bool TestRecordsSingleDevice()
    {
        FakeConfigSpace configSpace = new FakeConfigSpace();
        configSpace.SetHeader(
            bus: 2,
            device: 3,
            function: 0,
            CreateHeader(
                vendorId: 0x1AF4,
                deviceId: 0x1042,
                revisionId: 7,
                classCode: 0x02,
                subclass: 0x00,
                programmingInterface: 0x01,
                hasCapabilitiesList: true));

        PciScannedDevice[] devices = new PciScannedDevice[2];
        PciBusScanResult result = PciBusScanner.Scan(
            new PciBusScanRange(2, 2, startDevice: 3, endDevice: 3, maximumFunction: 7),
            configSpace.Read,
            devices);

        PciScannedDevice device = devices[0];
        return HostCheck.Result(result.Succeeded
            && result.Status == PciBusScanStatus.Success
            && result.DeviceCount == 1
            && result.FunctionReadCount == 1
            && device.Bus == 2
            && device.Device == 3
            && device.Function == 0
            && device.VendorId == 0x1AF4
            && device.DeviceId == 0x1042
            && device.RevisionId == 7
            && device.ClassCode == 0x02
            && device.Subclass == 0x00
            && device.ProgrammingInterface == 0x01
            && device.HeaderType == PciConfigurationHeader.Type0HeaderType
            && !device.IsMultifunction
            && device.HasCapabilitiesList
            && device.CapabilityPointer == 0x40);
    }

    [HostTest("drivers.pci.scan skips absent functions", HostLane.Fast, 1)]
    private static bool TestSkipsAbsentFunctions()
    {
        FakeConfigSpace configSpace = new FakeConfigSpace();
        PciScannedDevice[] devices = new PciScannedDevice[1];

        PciBusScanResult result = PciBusScanner.Scan(
            new PciBusScanRange(0, 0, startDevice: 0, endDevice: 2, maximumFunction: 7),
            configSpace.Read,
            devices);

        return HostCheck.Result(result.Succeeded
            && result.DeviceCount == 0
            && result.FunctionReadCount == 3
            && configSpace.ReadCount == 3
            && configSpace.HasRead(0, 0, 0)
            && configSpace.HasRead(0, 1, 0)
            && configSpace.HasRead(0, 2, 0)
            && !configSpace.HasRead(0, 0, 1)
            && !configSpace.HasRead(0, 1, 1)
            && !configSpace.HasRead(0, 2, 1));
    }

    [HostTest("drivers.pci.scan honors multifunction bit", HostLane.Fast, 2)]
    private static bool TestHonorsMultifunctionBit()
    {
        FakeConfigSpace configSpace = new FakeConfigSpace();
        configSpace.SetHeader(
            bus: 0,
            device: 5,
            function: 0,
            CreateHeader(0x1234, 0x5678, headerType: PciConfigurationHeader.HeaderTypeMultifunctionBit));
        configSpace.SetHeader(
            bus: 0,
            device: 5,
            function: 2,
            CreateHeader(
                vendorId: 0x1AF4,
                deviceId: 0x1041,
                classCode: 0x01,
                subclass: 0x00,
                programmingInterface: 0x00));

        PciScannedDevice[] devices = new PciScannedDevice[4];
        PciBusScanResult result = PciBusScanner.Scan(
            new PciBusScanRange(0, 0, startDevice: 5, endDevice: 5, maximumFunction: 3),
            configSpace.Read,
            devices);

        return HostCheck.Result(result.Succeeded
            && result.DeviceCount == 2
            && result.FunctionReadCount == 4
            && devices[0].Function == 0
            && devices[0].IsMultifunction
            && devices[1].Function == 2
            && devices[1].VendorId == 0x1AF4
            && devices[1].ClassCode == 0x01
            && configSpace.HasRead(0, 5, 0)
            && configSpace.HasRead(0, 5, 1)
            && configSpace.HasRead(0, 5, 2)
            && configSpace.HasRead(0, 5, 3)
            && !configSpace.HasRead(0, 5, 4));
    }

    [HostTest("drivers.pci.scan skips single-function extras", HostLane.Fast, 3)]
    private static bool TestSkipsSingleFunctionExtras()
    {
        FakeConfigSpace configSpace = new FakeConfigSpace();
        configSpace.SetHeader(
            bus: 0,
            device: 6,
            function: 0,
            CreateHeader(0x1111, 0x2222));
        configSpace.SetHeader(
            bus: 0,
            device: 6,
            function: 1,
            CreateHeader(0x3333, 0x4444));

        PciScannedDevice[] devices = new PciScannedDevice[4];
        PciBusScanResult result = PciBusScanner.Scan(
            new PciBusScanRange(0, 0, startDevice: 6, endDevice: 6, maximumFunction: 7),
            configSpace.Read,
            devices);

        return HostCheck.Result(result.Succeeded
            && result.DeviceCount == 1
            && result.FunctionReadCount == 1
            && devices[0].VendorId == 0x1111
            && !configSpace.HasRead(0, 6, 1));
    }

    [HostTest("drivers.pci.scan reports capacity exhaustion", HostLane.Fast, 4)]
    private static bool TestReportsCapacityExhaustion()
    {
        FakeConfigSpace configSpace = new FakeConfigSpace();
        configSpace.SetHeader(0, 0, 0, CreateHeader(0x1000, 0x0001));
        configSpace.SetHeader(0, 1, 0, CreateHeader(0x1000, 0x0002));

        PciScannedDevice[] devices = new PciScannedDevice[1];
        PciBusScanResult result = PciBusScanner.Scan(
            new PciBusScanRange(0, 0, startDevice: 0, endDevice: 1, maximumFunction: 7),
            configSpace.Read,
            devices);

        return HostCheck.Result(!result.Succeeded
            && result.Status == PciBusScanStatus.DestinationTooSmall
            && result.DeviceCount == 1
            && result.FunctionReadCount == 2
            && result.RequiredDeviceCapacity == 2
            && result.FailingBus == 0
            && result.FailingDevice == 1
            && result.FailingFunction == 0
            && devices[0].DeviceId == 0x0001);
    }

    [HostTest("drivers.pci.scan reports reader failure", HostLane.Fast, 5)]
    private static bool TestReportsReaderFailure()
    {
        FakeConfigSpace configSpace = new FakeConfigSpace();
        configSpace.SetHeader(
            bus: 0,
            device: 4,
            function: 0,
            CreateHeader(0x1234, 0x5678, headerType: PciConfigurationHeader.HeaderTypeMultifunctionBit));
        configSpace.FailRead(
            bus: 0,
            device: 4,
            function: 2,
            PciConfigurationSpaceReadStatus.UnsupportedTuple);

        PciScannedDevice[] devices = new PciScannedDevice[4];
        PciBusScanResult result = PciBusScanner.Scan(
            new PciBusScanRange(0, 0, startDevice: 4, endDevice: 4, maximumFunction: 3),
            configSpace.Read,
            devices);

        return HostCheck.Result(!result.Succeeded
            && result.Status == PciBusScanStatus.ReaderFailed
            && result.ReaderStatus == PciConfigurationSpaceReadStatus.UnsupportedTuple
            && result.DeviceCount == 1
            && result.FunctionReadCount == 3
            && result.FailingBus == 0
            && result.FailingDevice == 4
            && result.FailingFunction == 2
            && devices[0].Device == 4
            && devices[0].Function == 0);
    }

    [HostTest("drivers.pci.scan default result is not success", HostLane.Fast, 6)]
    private static bool TestDefaultResultIsNotSuccess()
    {
        PciBusScanResult result = default;

        return HostCheck.Result(!result.IsInitialized
            && !result.Succeeded
            && result.Status == PciBusScanStatus.Success
            && !RuntimeHelpers.IsReferenceOrContainsReferences<PciBusScanResult>());
    }

    private static byte[] CreateHeader(
        ushort vendorId,
        ushort deviceId,
        byte revisionId = 0,
        byte classCode = 0,
        byte subclass = 0,
        byte programmingInterface = 0,
        byte headerType = PciConfigurationHeader.Type0HeaderType,
        bool hasCapabilitiesList = false)
    {
        byte[] headerBytes = new byte[PciConfigurationHeader.Type0HeaderLength];
        EndianPrimitives.WriteUInt16LittleEndian(headerBytes.AsSpan(0x00, sizeof(ushort)), vendorId);
        EndianPrimitives.WriteUInt16LittleEndian(headerBytes.AsSpan(0x02, sizeof(ushort)), deviceId);
        headerBytes[0x08] = revisionId;
        headerBytes[0x09] = programmingInterface;
        headerBytes[0x0A] = subclass;
        headerBytes[0x0B] = classCode;
        headerBytes[0x0E] = headerType;

        if (hasCapabilitiesList)
        {
            EndianPrimitives.WriteUInt16LittleEndian(
                headerBytes.AsSpan(0x06, sizeof(ushort)),
                PciConfigurationHeader.StatusCapabilitiesListBit);
            headerBytes[0x34] = 0x40;
        }

        return headerBytes;
    }

    private readonly struct HeaderSlot
    {
        public HeaderSlot(byte bus, byte device, byte function, byte[] bytes)
        {
            Bus = bus;
            Device = device;
            Function = function;
            Bytes = bytes;
        }

        public byte Bus { get; }

        public byte Device { get; }

        public byte Function { get; }

        public byte[] Bytes { get; }
    }

    private readonly struct ReadRequest
    {
        public ReadRequest(byte bus, byte device, byte function)
        {
            Bus = bus;
            Device = device;
            Function = function;
        }

        public byte Bus { get; }

        public byte Device { get; }

        public byte Function { get; }
    }

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

        public void FailRead(
            byte bus,
            byte device,
            byte function,
            PciConfigurationSpaceReadStatus status)
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

        public PciConfigurationSpaceReadStatus Read(
            byte bus,
            byte device,
            byte function,
            Span<byte> destination)
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

            if (_hasFailure &&
                bus == _failureBus &&
                device == _failureDevice &&
                function == _failureFunction)
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
            EndianPrimitives.WriteUInt16LittleEndian(
                destination.Slice(0x00, sizeof(ushort)),
                PciConfigurationHeader.InvalidVendorId);
            return PciConfigurationSpaceReadStatus.Success;
        }
    }
}
