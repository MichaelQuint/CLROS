using System.Runtime.CompilerServices;
using OS.Drivers;
using OS.Kernel.Platform;
using OS.Std.Binary;

internal static class PciEcamPhysicalHeaderReaderTests
{
    [ModuleInitializer]
    internal static void RunAll()
    {
        int failures = 0;

        Run("pci.ecam.physical_header reads present device", TestReadsPresentDevice, ref failures);
        Run("pci.ecam.physical_header surfaces absent vendor through scanner", TestSurfacesAbsentVendorThroughScanner, ref failures);
        Run("pci.ecam.physical_header rejects invalid address tuple", TestRejectsInvalidAddressTuple, ref failures);
        Run("pci.ecam.physical_header reports destination capacity", TestReportsDestinationCapacity, ref failures);
        Run("pci.ecam.physical_header reports physical read failure", TestReportsPhysicalReadFailure, ref failures);
        Run("pci.ecam.physical_header reports short read", TestReportsShortRead, ref failures);
        Run("pci.ecam.physical_header adapts into pci bus scanner", TestAdaptsIntoPciBusScanner, ref failures);
        Run("pci.ecam.physical_header default result is not success", TestDefaultResultIsNotSuccess, ref failures);
        Run("pci.ecam.physical_header result contains no managed references", TestResultContainsNoManagedReferences, ref failures);

        if (failures != 0)
        {
            throw new InvalidOperationException("PCI ECAM physical header reader host tests failed: " + failures);
        }
    }

    private static void Run(string name, Func<bool> test, ref int failures)
    {
        Console.WriteLine("TEST_START " + name);

        try
        {
            if (test())
            {
                Console.WriteLine("TEST_PASS " + name);
                return;
            }

            failures++;
            Console.WriteLine("TEST_FAIL " + name);
        }
        catch (Exception ex)
        {
            failures++;
            Console.WriteLine("TEST_FAIL " + name + " " + ex.GetType().Name + ": " + ex.Message);
        }
    }

    private static bool TestReadsPresentDevice()
    {
        PciEcamSegment segment = DefaultSegment();
        byte[] header = CreateType0Header(0x1AF4, 0x1001);
        ulong physicalAddress = PhysicalAddress(segment, bus: 2, device: 3, function: 1);
        PhysicalMemoryDouble physicalMemory = PhysicalMemoryDouble.Success(
            new PhysicalMemoryEntry(physicalAddress, header));
        Span<byte> destination = stackalloc byte[PciConfigurationHeader.Type0HeaderLength];

        PciEcamPhysicalHeaderReadResult result = PciEcamPhysicalHeaderReader.ReadType0Header(
            segment,
            physicalMemory,
            bus: 2,
            device: 3,
            function: 1,
            destination);

        PciConfigurationHeaderResult headerResult = PciConfigurationHeader.ParseType0(destination);

        return result.Succeeded
            && result.Status == PciEcamPhysicalHeaderReadStatus.Success
            && result.AddressStatus == PciEcamConfigurationSpaceStatus.Success
            && result.PhysicalReadStatus == PlatformPhysicalMemoryStatus.Success
            && result.PhysicalAddress == physicalAddress
            && result.RequiredBytes == PciConfigurationHeader.Type0HeaderLength
            && result.BytesRead == PciConfigurationHeader.Type0HeaderLength
            && physicalMemory.ReadCount == 1
            && physicalMemory.RequestedAddress == physicalAddress
            && headerResult.Succeeded
            && headerResult.Header.VendorId == 0x1AF4
            && headerResult.Header.DeviceId == 0x1001;
    }

    private static bool TestSurfacesAbsentVendorThroughScanner()
    {
        PciEcamSegment segment = DefaultSegment(startBus: 2, endBus: 2);
        byte[] absentHeader = CreateType0Header(PciConfigurationHeader.InvalidVendorId, 0xFFFF);
        ulong physicalAddress = PhysicalAddress(segment, bus: 2, device: 0, function: 0);
        PhysicalMemoryDouble physicalMemory = PhysicalMemoryDouble.Success(
            new PhysicalMemoryEntry(physicalAddress, absentHeader));
        PciEcamPhysicalConfigurationSpaceHeaderReader reader =
            new PciEcamPhysicalConfigurationSpaceHeaderReader(segment, physicalMemory);
        PciScannedDevice[] devices = new PciScannedDevice[1];

        PciBusScanResult scan = PciBusScanner.Scan(
            new PciBusScanRange(2, 2, endDevice: 0, maximumFunction: 0),
            reader.Read,
            devices);

        return scan.Succeeded
            && scan.DeviceCount == 0
            && scan.FunctionReadCount == 1
            && reader.LastResult.Succeeded
            && reader.LastResult.PhysicalAddress == physicalAddress;
    }

    private static bool TestRejectsInvalidAddressTuple()
    {
        PciEcamSegment segment = DefaultSegment(startBus: 4, endBus: 4);
        PhysicalMemoryDouble physicalMemory = PhysicalMemoryDouble.Success();
        Span<byte> destination = stackalloc byte[PciConfigurationHeader.Type0HeaderLength];

        PciEcamPhysicalHeaderReadResult result = PciEcamPhysicalHeaderReader.ReadType0Header(
            segment,
            physicalMemory,
            bus: 3,
            device: 0,
            function: 0,
            destination);

        return !result.Succeeded
            && result.Status == PciEcamPhysicalHeaderReadStatus.AddressPlanningFailed
            && result.AddressStatus == PciEcamConfigurationSpaceStatus.BusOutsideSegment
            && result.ToConfigurationSpaceReadStatus() == PciConfigurationSpaceReadStatus.UnsupportedTuple
            && physicalMemory.ReadCount == 0;
    }

    private static bool TestReportsDestinationCapacity()
    {
        PciEcamSegment segment = DefaultSegment();
        PhysicalMemoryDouble physicalMemory = PhysicalMemoryDouble.Success();
        Span<byte> destination = stackalloc byte[PciConfigurationHeader.Type0HeaderLength - 1];

        PciEcamPhysicalHeaderReadResult result = PciEcamPhysicalHeaderReader.ReadType0Header(
            segment,
            physicalMemory,
            bus: 2,
            device: 0,
            function: 0,
            destination);

        return !result.Succeeded
            && result.Status == PciEcamPhysicalHeaderReadStatus.DestinationTooSmall
            && result.RequiredBytes == PciConfigurationHeader.Type0HeaderLength
            && result.ToConfigurationSpaceReadStatus() == PciConfigurationSpaceReadStatus.DestinationTooSmall
            && physicalMemory.ReadCount == 0;
    }

    private static bool TestReportsPhysicalReadFailure()
    {
        PciEcamSegment segment = DefaultSegment();
        PhysicalMemoryDouble physicalMemory = PhysicalMemoryDouble.Fail(
            PlatformPhysicalMemoryStatus.SourceFailed);
        Span<byte> destination = stackalloc byte[PciConfigurationHeader.Type0HeaderLength];

        PciEcamPhysicalHeaderReadResult result = PciEcamPhysicalHeaderReader.ReadType0Header(
            segment,
            physicalMemory,
            bus: 2,
            device: 0,
            function: 0,
            destination);

        return !result.Succeeded
            && result.Status == PciEcamPhysicalHeaderReadStatus.PhysicalReadFailed
            && result.PhysicalReadStatus == PlatformPhysicalMemoryStatus.SourceFailed
            && result.ToConfigurationSpaceReadStatus() == PciConfigurationSpaceReadStatus.ReadFailed;
    }

    private static bool TestReportsShortRead()
    {
        PciEcamSegment segment = DefaultSegment();
        byte[] shortHeader = new byte[PciConfigurationHeader.Type0HeaderLength - 1];
        PhysicalMemoryDouble physicalMemory = PhysicalMemoryDouble.Success(
            new PhysicalMemoryEntry(PhysicalAddress(segment, 2, 0, 0), shortHeader));
        Span<byte> destination = stackalloc byte[PciConfigurationHeader.Type0HeaderLength];

        PciEcamPhysicalHeaderReadResult result = PciEcamPhysicalHeaderReader.ReadType0Header(
            segment,
            physicalMemory,
            bus: 2,
            device: 0,
            function: 0,
            destination);

        return !result.Succeeded
            && result.Status == PciEcamPhysicalHeaderReadStatus.PhysicalReadFailed
            && result.PhysicalReadStatus == PlatformPhysicalMemoryStatus.ShortRead
            && result.BytesRead == PciConfigurationHeader.Type0HeaderLength - 1
            && result.RequiredBytes == PciConfigurationHeader.Type0HeaderLength;
    }

    private static bool TestAdaptsIntoPciBusScanner()
    {
        PciEcamSegment segment = DefaultSegment(startBus: 2, endBus: 2);
        ulong presentAddress = PhysicalAddress(segment, bus: 2, device: 0, function: 0);
        ulong absentAddress = PhysicalAddress(segment, bus: 2, device: 1, function: 0);
        PhysicalMemoryDouble physicalMemory = PhysicalMemoryDouble.Success(
            new PhysicalMemoryEntry(presentAddress, CreateType0Header(0x1AF4, 0x1000)),
            new PhysicalMemoryEntry(absentAddress, CreateType0Header(PciConfigurationHeader.InvalidVendorId, 0xFFFF)));
        PciEcamPhysicalConfigurationSpaceHeaderReader reader =
            new PciEcamPhysicalConfigurationSpaceHeaderReader(segment, physicalMemory);
        PciScannedDevice[] devices = new PciScannedDevice[1];

        PciBusScanResult scan = PciBusScanner.Scan(
            new PciBusScanRange(2, 2, endDevice: 1, maximumFunction: 0),
            reader.Read,
            devices);

        return scan.Succeeded
            && scan.DeviceCount == 1
            && scan.FunctionReadCount == 2
            && devices[0].VendorId == 0x1AF4
            && devices[0].DeviceId == 0x1000
            && reader.LastResult.Succeeded
            && reader.LastResult.PhysicalAddress == absentAddress;
    }

    private static bool TestDefaultResultIsNotSuccess()
    {
        PciEcamPhysicalHeaderReadResult result = default;
        PciEcamPhysicalConfigurationSpaceHeaderReader reader =
            new PciEcamPhysicalConfigurationSpaceHeaderReader(DefaultSegment(), physicalMemory: null);

        return !result.IsInitialized
            && !result.Succeeded
            && result.Status == PciEcamPhysicalHeaderReadStatus.Success
            && result.ToConfigurationSpaceReadStatus() == PciConfigurationSpaceReadStatus.ReadFailed
            && !reader.LastResult.IsInitialized
            && !reader.LastResult.Succeeded
            && reader.LastResult.Status == PciEcamPhysicalHeaderReadStatus.Success
            && reader.LastResult.ToConfigurationSpaceReadStatus() ==
                PciConfigurationSpaceReadStatus.ReadFailed;
    }

    private static bool TestResultContainsNoManagedReferences()
    {
        return !RuntimeHelpers.IsReferenceOrContainsReferences<PciEcamPhysicalHeaderReadResult>();
    }

    private static PciEcamSegment DefaultSegment(byte startBus = 2, byte endBus = 4)
    {
        return new PciEcamSegment(
            basePhysicalAddress: 0x00000000E0000000,
            segmentGroup: 0,
            startBus,
            endBus);
    }

    private static ulong PhysicalAddress(PciEcamSegment segment, byte bus, byte device, byte function)
    {
        PciEcamConfigurationAddressResult result = PciEcamConfigurationSpace.CalculateAddress(
            segment,
            bus,
            device,
            function,
            registerOffset: 0,
            accessWidth: 4);

        if (!result.Succeeded)
        {
            throw new InvalidOperationException("invalid test ECAM address: " + result.Status);
        }

        return result.PhysicalAddress;
    }

    private static byte[] CreateType0Header(ushort vendorId, ushort deviceId)
    {
        byte[] headerBytes = new byte[PciConfigurationHeader.Type0HeaderLength];
        EndianPrimitives.WriteUInt16LittleEndian(headerBytes.AsSpan(0x00, sizeof(ushort)), vendorId);
        EndianPrimitives.WriteUInt16LittleEndian(headerBytes.AsSpan(0x02, sizeof(ushort)), deviceId);
        headerBytes[0x08] = 1;
        headerBytes[0x09] = 0;
        headerBytes[0x0A] = 0;
        headerBytes[0x0B] = 1;
        headerBytes[0x0E] = PciConfigurationHeader.Type0HeaderType;
        return headerBytes;
    }

    private readonly struct PhysicalMemoryEntry
    {
        public PhysicalMemoryEntry(ulong physicalAddress, byte[] bytes)
        {
            PhysicalAddress = physicalAddress;
            Bytes = bytes;
        }

        public ulong PhysicalAddress { get; }

        public byte[] Bytes { get; }
    }

    private sealed class PhysicalMemoryDouble : IPlatformPhysicalMemory
    {
        private readonly PhysicalMemoryEntry[] _entries;
        private readonly PlatformPhysicalMemoryStatus _failureStatus;

        private PhysicalMemoryDouble(
            PhysicalMemoryEntry[] entries,
            PlatformPhysicalMemoryStatus failureStatus)
        {
            _entries = entries;
            _failureStatus = failureStatus;
        }

        public int ReadCount { get; private set; }

        public ulong RequestedAddress { get; private set; }

        public static PhysicalMemoryDouble Success(params PhysicalMemoryEntry[] entries)
        {
            return new PhysicalMemoryDouble(entries, PlatformPhysicalMemoryStatus.Success);
        }

        public static PhysicalMemoryDouble Fail(PlatformPhysicalMemoryStatus failureStatus)
        {
            return new PhysicalMemoryDouble([], failureStatus);
        }

        public PlatformPhysicalMemoryResult ReadBytes(
            ulong physicalAddress,
            int byteCount,
            Span<byte> destination)
        {
            ReadCount++;
            RequestedAddress = physicalAddress;

            FirmwareCallResult guard = FirmwareCallResult.Allowed(
                PlatformPhysicalMemoryOperations.ReadBytes,
                FirmwareServicesMode.BootServicesAvailable);

            if (_failureStatus != PlatformPhysicalMemoryStatus.Success)
            {
                PlatformPhysicalMemorySourceResult failedSource =
                    _failureStatus == PlatformPhysicalMemoryStatus.SourceUnavailable
                        ? PlatformPhysicalMemorySourceResult.Unavailable()
                        : PlatformPhysicalMemorySourceResult.Fault(0x8000000000000009);

                return PlatformPhysicalMemoryResult.Read(
                    guard,
                    failedSource,
                    physicalAddress,
                    byteCount);
            }

            for (int index = 0; index < _entries.Length; index++)
            {
                if (_entries[index].PhysicalAddress != physicalAddress)
                {
                    continue;
                }

                int copyLength = Math.Min(byteCount, _entries[index].Bytes.Length);
                _entries[index].Bytes.AsSpan(0, copyLength).CopyTo(destination);
                return PlatformPhysicalMemoryResult.Read(
                    guard,
                    PlatformPhysicalMemorySourceResult.Read(copyLength),
                    physicalAddress,
                    byteCount);
            }

            return PlatformPhysicalMemoryResult.Read(
                guard,
                PlatformPhysicalMemorySourceResult.Unavailable(),
                physicalAddress,
                byteCount);
        }
    }
}
