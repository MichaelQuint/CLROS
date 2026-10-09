using System.Runtime.CompilerServices;
using OS.Drivers;
using OS.Std.Binary;

internal static class PciEcamSegmentScannerTests
{

    [HostTest("drivers.pci.ecam scans one segment", HostLane.Fast, 0)]
    private static bool TestScansOneSegment()
    {
        FakeConfigSpace configSpace = new FakeConfigSpace();
        configSpace.SetHeader(
            bus: 2,
            device: 3,
            function: 0,
            CreateHeader(
                vendorId: 0x1AF4,
                deviceId: 0x1042,
                classCode: 0x01,
                subclass: 0x00));

        PciEcamSegment[] segments =
        [
            new PciEcamSegment(
                basePhysicalAddress: 0xE0000000,
                segmentGroup: 0,
                startBus: 2,
                endBus: 2)
        ];
        PciEcamScannedDevice[] devices = new PciEcamScannedDevice[2];
        PciScannedDevice[] scratch = new PciScannedDevice[devices.Length];

        PciEcamSegmentScanResult result = PciEcamSegmentScanner.Scan(
            segments,
            configSpace.Read,
            devices,
            scratch);

        return HostCheck.Result(result.Succeeded
            && result.Status == PciEcamSegmentScanStatus.Success
            && result.CompletedSegmentCount == 1
            && result.DeviceCount == 1
            && result.FunctionReadCount == 32
            && result.RequiredDeviceCapacity == 1
            && result.FailingSegmentIndex == PciEcamSegmentScanResult.NoFailingSegmentIndex
            && devices[0].SegmentIndex == 0
            && devices[0].SegmentGroup == 0
            && devices[0].EcamBasePhysicalAddress == 0xE0000000UL
            && devices[0].Bus == 2
            && devices[0].DeviceNumber == 3
            && devices[0].Function == 0
            && devices[0].VendorId == 0x1AF4
            && devices[0].DeviceId == 0x1042
            && configSpace.HasRead(2, 3, 0));
    }

    [HostTest("drivers.pci.ecam scans multiple segments", HostLane.Fast, 1)]
    private static bool TestScansMultipleSegments()
    {
        FakeConfigSpace configSpace = new FakeConfigSpace();
        configSpace.SetHeader(0, 1, 0, CreateHeader(0x1234, 0x0001));
        configSpace.SetHeader(4, 2, 0, CreateHeader(0x1AF4, 0x1041));

        PciEcamSegment[] segments =
        [
            new PciEcamSegment(0xE0000000, segmentGroup: 0, startBus: 0, endBus: 0),
            new PciEcamSegment(0xF0000000, segmentGroup: 7, startBus: 4, endBus: 4)
        ];
        PciEcamScannedDevice[] devices = new PciEcamScannedDevice[4];
        PciScannedDevice[] scratch = new PciScannedDevice[devices.Length];

        PciEcamSegmentScanResult result = PciEcamSegmentScanner.Scan(
            segments,
            configSpace.Read,
            devices,
            scratch);

        return HostCheck.Result(result.Succeeded
            && result.CompletedSegmentCount == 2
            && result.DeviceCount == 2
            && result.FunctionReadCount == 64
            && devices[0].SegmentIndex == 0
            && devices[0].SegmentGroup == 0
            && devices[0].Bus == 0
            && devices[0].DeviceNumber == 1
            && devices[0].DeviceId == 0x0001
            && devices[1].SegmentIndex == 1
            && devices[1].SegmentGroup == 7
            && devices[1].EcamBasePhysicalAddress == 0xF0000000UL
            && devices[1].Bus == 4
            && devices[1].DeviceNumber == 2
            && devices[1].DeviceId == 0x1041);
    }

    [HostTest("drivers.pci.ecam preserves segment metadata", HostLane.Fast, 2)]
    private static bool TestPreservesSegmentMetadata()
    {
        FakeConfigSpace configSpace = new FakeConfigSpace();
        configSpace.SetHeader(
            bus: 0x20,
            device: 0,
            function: 0,
            CreateHeader(
                vendorId: 0x1AF4,
                deviceId: 0x1041,
                revisionId: 5,
                classCode: 0x02,
                subclass: 0x00,
                programmingInterface: 0x01,
                hasCapabilitiesList: true));

        PciEcamSegment[] segments =
        [
            new PciEcamSegment(
                basePhysicalAddress: 0x00000008_80000000,
                segmentGroup: 0x1234,
                startBus: 0x20,
                endBus: 0x20)
        ];
        PciEcamScannedDevice[] devices = new PciEcamScannedDevice[1];
        PciScannedDevice[] scratch = new PciScannedDevice[devices.Length];

        PciEcamSegmentScanResult result = PciEcamSegmentScanner.Scan(
            segments,
            configSpace.Read,
            devices,
            scratch);

        PciEcamScannedDevice device = devices[0];
        return HostCheck.Result(result.Succeeded
            && device.Segment.BasePhysicalAddress == 0x00000008_80000000UL
            && device.EcamBasePhysicalAddress == 0x00000008_80000000UL
            && device.SegmentGroup == 0x1234
            && device.SegmentStartBus == 0x20
            && device.SegmentEndBus == 0x20
            && device.Bus == 0x20
            && device.DeviceNumber == 0
            && device.Function == 0
            && device.VendorId == 0x1AF4
            && device.DeviceId == 0x1041
            && device.RevisionId == 5
            && device.ClassCode == 0x02
            && device.Subclass == 0x00
            && device.ProgrammingInterface == 0x01
            && device.HasCapabilitiesList
            && device.CapabilityPointer == 0x40);
    }

    [HostTest("drivers.pci.ecam reports destination exhaustion", HostLane.Fast, 3)]
    private static bool TestReportsDestinationExhaustion()
    {
        FakeConfigSpace configSpace = new FakeConfigSpace();
        configSpace.SetHeader(0, 0, 0, CreateHeader(0x1000, 0x0001));
        configSpace.SetHeader(0, 1, 0, CreateHeader(0x1000, 0x0002));

        PciEcamSegment[] segments =
        [
            new PciEcamSegment(0xE0000000, segmentGroup: 0, startBus: 0, endBus: 0)
        ];
        PciEcamScannedDevice[] devices = new PciEcamScannedDevice[1];
        PciScannedDevice[] scratch = new PciScannedDevice[devices.Length];

        PciEcamSegmentScanResult result = PciEcamSegmentScanner.Scan(
            segments,
            configSpace.Read,
            devices,
            scratch);

        return HostCheck.Result(!result.Succeeded
            && result.Status == PciEcamSegmentScanStatus.DestinationTooSmall
            && result.CompletedSegmentCount == 0
            && result.DeviceCount == 1
            && result.FunctionReadCount == 2
            && result.RequiredDeviceCapacity == 2
            && result.FailingSegmentIndex == 0
            && result.FailingSegment.BasePhysicalAddress == 0xE0000000UL
            && result.NestedResult.Status == PciBusScanStatus.DestinationTooSmall
            && result.NestedResult.RequiredDeviceCapacity == 2
            && devices[0].SegmentGroup == 0
            && devices[0].DeviceId == 0x0001);
    }

    [HostTest("drivers.pci.ecam reports nested scanner failure", HostLane.Fast, 4)]
    private static bool TestReportsNestedScannerFailure()
    {
        FakeConfigSpace configSpace = new FakeConfigSpace();
        configSpace.SetHeader(0, 0, 0, CreateHeader(0x1000, 0x0001));
        configSpace.FailRead(
            bus: 0,
            device: 1,
            function: 0,
            PciConfigurationSpaceReadStatus.ReadFailed);

        PciEcamSegment[] segments =
        [
            new PciEcamSegment(0xE0000000, segmentGroup: 0, startBus: 0, endBus: 0)
        ];
        PciEcamScannedDevice[] devices = new PciEcamScannedDevice[4];
        PciScannedDevice[] scratch = new PciScannedDevice[devices.Length];

        PciEcamSegmentScanResult result = PciEcamSegmentScanner.Scan(
            segments,
            configSpace.Read,
            devices,
            scratch);

        return HostCheck.Result(!result.Succeeded
            && result.Status == PciEcamSegmentScanStatus.NestedScannerFailed
            && result.CompletedSegmentCount == 0
            && result.DeviceCount == 1
            && result.FunctionReadCount == 2
            && result.RequiredDeviceCapacity == 1
            && result.FailingSegmentIndex == 0
            && result.NestedResult.Status == PciBusScanStatus.ReaderFailed
            && result.NestedResult.ReaderStatus == PciConfigurationSpaceReadStatus.ReadFailed
            && result.NestedResult.FailingBus == 0
            && result.NestedResult.FailingDevice == 1
            && result.NestedResult.FailingFunction == 0
            && devices[0].DeviceId == 0x0001);
    }

    [HostTest("drivers.pci.ecam rejects invalid segment range", HostLane.Fast, 5)]
    private static bool TestRejectsInvalidSegmentRange()
    {
        FakeConfigSpace configSpace = new FakeConfigSpace();
        PciEcamSegment[] segments =
        [
            new PciEcamSegment(0xE0000000, segmentGroup: 0, startBus: 8, endBus: 3)
        ];
        PciEcamScannedDevice[] devices = new PciEcamScannedDevice[1];
        PciScannedDevice[] scratch = new PciScannedDevice[devices.Length];

        PciEcamSegmentScanResult result = PciEcamSegmentScanner.Scan(
            segments,
            configSpace.Read,
            devices,
            scratch);

        return HostCheck.Result(!result.Succeeded
            && result.Status == PciEcamSegmentScanStatus.InvalidSegmentRange
            && result.CompletedSegmentCount == 0
            && result.DeviceCount == 0
            && result.FunctionReadCount == 0
            && result.FailingSegmentIndex == 0
            && result.FailingSegment.StartBus == 8
            && result.FailingSegment.EndBus == 3
            && configSpace.ReadCount == 0);
    }

    [HostTest("drivers.pci.ecam.segment default result is not success", HostLane.Fast, 6)]
    private static bool TestDefaultResultIsNotSuccess()
    {
        PciEcamSegmentScanResult result = default;

        return HostCheck.Result(!result.IsInitialized
            && !result.Succeeded
            && result.Status == PciEcamSegmentScanStatus.Success
            && !RuntimeHelpers.IsReferenceOrContainsReferences<PciEcamSegmentScanResult>());
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
        private readonly ReadRequest[] _reads = new ReadRequest[256];
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
            for (int index = 0; index < ReadCount && index < _reads.Length; index++)
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
