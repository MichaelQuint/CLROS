namespace OS.Drivers;

public enum EnaPciDeviceKind
{
    Unknown = 0,
    VirtualFunction = 1,
    LowLatencyQueueVirtualFunction = 2
}

public enum EnaPciCandidateValidationStatus
{
    Success = 0,
    UnsupportedVendor = 1,
    UnsupportedDeviceId = 2,
    UnsupportedHeaderType = 3,
    UnsupportedClassCode = 4,
    UnsupportedSubclass = 5,
    UnsupportedProgrammingInterface = 6,
    MissingCapabilitiesList = 7,
    InvalidCapabilityPointer = 8
}

public enum EnaPciCandidateSelectionStatus
{
    Success = 0,
    DestinationTooSmall = 1
}

public readonly struct EnaPciCandidateValidationResult
{
    private EnaPciCandidateValidationResult(
        bool isInitialized,
        EnaPciCandidateValidationStatus status,
        EnaPciDeviceKind deviceKind,
        PciEcamScannedDevice device)
    {
        IsInitialized = isInitialized;
        Status = status;
        DeviceKind = deviceKind;
        Device = device;
    }

    public EnaPciCandidateValidationStatus Status { get; }

    public EnaPciDeviceKind DeviceKind { get; }

    public PciEcamScannedDevice Device { get; }

    public bool IsInitialized { get; }

    public bool Succeeded => IsInitialized && Status == EnaPciCandidateValidationStatus.Success;

    public static EnaPciCandidateValidationResult Success(
        EnaPciDeviceKind deviceKind,
        PciEcamScannedDevice device)
    {
        return new EnaPciCandidateValidationResult(
            isInitialized: true,
            EnaPciCandidateValidationStatus.Success,
            deviceKind,
            device);
    }

    public static EnaPciCandidateValidationResult Fail(
        EnaPciCandidateValidationStatus status,
        EnaPciDeviceKind deviceKind,
        PciEcamScannedDevice device)
    {
        return new EnaPciCandidateValidationResult(
            isInitialized: true,
            status,
            deviceKind,
            device);
    }
}

public readonly struct EnaPciCandidate
{
    public EnaPciCandidate(
        int sourceIndex,
        int segmentIndex,
        ushort segmentGroup,
        ulong ecamBasePhysicalAddress,
        byte segmentStartBus,
        byte segmentEndBus,
        byte bus,
        byte device,
        byte function,
        ushort vendorId,
        ushort deviceId,
        byte revisionId,
        byte classCode,
        byte subclass,
        byte programmingInterface,
        byte headerType,
        byte capabilityPointer,
        EnaPciDeviceKind deviceKind)
    {
        SourceIndex = sourceIndex;
        SegmentIndex = segmentIndex;
        SegmentGroup = segmentGroup;
        EcamBasePhysicalAddress = ecamBasePhysicalAddress;
        SegmentStartBus = segmentStartBus;
        SegmentEndBus = segmentEndBus;
        Bus = bus;
        Device = device;
        Function = function;
        VendorId = vendorId;
        DeviceId = deviceId;
        RevisionId = revisionId;
        ClassCode = classCode;
        Subclass = subclass;
        ProgrammingInterface = programmingInterface;
        HeaderType = headerType;
        CapabilityPointer = capabilityPointer;
        DeviceKind = deviceKind;
    }

    public int SourceIndex { get; }

    public int SegmentIndex { get; }

    public ushort SegmentGroup { get; }

    public ulong EcamBasePhysicalAddress { get; }

    public byte SegmentStartBus { get; }

    public byte SegmentEndBus { get; }

    public byte Bus { get; }

    public byte Device { get; }

    public byte Function { get; }

    public ushort VendorId { get; }

    public ushort DeviceId { get; }

    public byte RevisionId { get; }

    public byte ClassCode { get; }

    public byte Subclass { get; }

    public byte ProgrammingInterface { get; }

    public byte HeaderType { get; }

    public byte CapabilityPointer { get; }

    public EnaPciDeviceKind DeviceKind { get; }
}

public readonly struct EnaPciCandidateSelectionResult
{
    private EnaPciCandidateSelectionResult(
        bool isInitialized,
        EnaPciCandidateSelectionStatus status,
        int inspectedDeviceCount,
        int candidateCount,
        int requiredCandidateCapacity,
        int failingSourceIndex)
    {
        IsInitialized = isInitialized;
        Status = status;
        InspectedDeviceCount = inspectedDeviceCount;
        CandidateCount = candidateCount;
        RequiredCandidateCapacity = requiredCandidateCapacity;
        FailingSourceIndex = failingSourceIndex;
    }

    public const int NoFailingSourceIndex = -1;

    public EnaPciCandidateSelectionStatus Status { get; }

    public int InspectedDeviceCount { get; }

    public int CandidateCount { get; }

    public int RequiredCandidateCapacity { get; }

    public int FailingSourceIndex { get; }

    public bool IsInitialized { get; }

    public bool Succeeded => IsInitialized && Status == EnaPciCandidateSelectionStatus.Success;

    public static EnaPciCandidateSelectionResult Success(
        int inspectedDeviceCount,
        int candidateCount)
    {
        return new EnaPciCandidateSelectionResult(
            isInitialized: true,
            EnaPciCandidateSelectionStatus.Success,
            inspectedDeviceCount,
            candidateCount,
            candidateCount,
            NoFailingSourceIndex);
    }

    public static EnaPciCandidateSelectionResult DestinationTooSmall(
        int inspectedDeviceCount,
        int candidateCount,
        int requiredCandidateCapacity,
        int failingSourceIndex)
    {
        return new EnaPciCandidateSelectionResult(
            isInitialized: true,
            EnaPciCandidateSelectionStatus.DestinationTooSmall,
            inspectedDeviceCount,
            candidateCount,
            requiredCandidateCapacity,
            failingSourceIndex);
    }
}

public static class EnaPciCandidateSelector
{
    public const ushort AmazonVendorId = 0x1D0F;
    public const ushort PhysicalFunctionDeviceId = 0x0EC2;
    public const ushort LowLatencyQueuePhysicalFunctionDeviceId = 0x1EC2;
    public const ushort VirtualFunctionDeviceId = 0xEC20;
    public const ushort LowLatencyQueueVirtualFunctionDeviceId = 0xEC21;
    public const ushort ReservedDeviceId = 0x0051;
    public const byte NetworkControllerClassCode = 0x02;
    public const byte EthernetControllerSubclass = 0x00;
    public const byte EthernetProgrammingInterface = 0x00;

    public static EnaPciCandidateValidationResult Validate(PciEcamScannedDevice device)
    {
        if (device.VendorId != AmazonVendorId)
        {
            return EnaPciCandidateValidationResult.Fail(
                EnaPciCandidateValidationStatus.UnsupportedVendor,
                EnaPciDeviceKind.Unknown,
                device);
        }

        if (!TryGetDeviceKind(device.DeviceId, out EnaPciDeviceKind deviceKind))
        {
            return EnaPciCandidateValidationResult.Fail(
                EnaPciCandidateValidationStatus.UnsupportedDeviceId,
                EnaPciDeviceKind.Unknown,
                device);
        }

        if ((device.HeaderType & PciConfigurationHeader.HeaderTypeMask) !=
            PciConfigurationHeader.Type0HeaderType)
        {
            return EnaPciCandidateValidationResult.Fail(
                EnaPciCandidateValidationStatus.UnsupportedHeaderType,
                deviceKind,
                device);
        }

        if (device.ClassCode != NetworkControllerClassCode)
        {
            return EnaPciCandidateValidationResult.Fail(
                EnaPciCandidateValidationStatus.UnsupportedClassCode,
                deviceKind,
                device);
        }

        if (device.Subclass != EthernetControllerSubclass)
        {
            return EnaPciCandidateValidationResult.Fail(
                EnaPciCandidateValidationStatus.UnsupportedSubclass,
                deviceKind,
                device);
        }

        if (device.ProgrammingInterface != EthernetProgrammingInterface)
        {
            return EnaPciCandidateValidationResult.Fail(
                EnaPciCandidateValidationStatus.UnsupportedProgrammingInterface,
                deviceKind,
                device);
        }

        if (!device.HasCapabilitiesList)
        {
            return EnaPciCandidateValidationResult.Fail(
                EnaPciCandidateValidationStatus.MissingCapabilitiesList,
                deviceKind,
                device);
        }

        if (!IsCapabilityPointerValid(device.CapabilityPointer))
        {
            return EnaPciCandidateValidationResult.Fail(
                EnaPciCandidateValidationStatus.InvalidCapabilityPointer,
                deviceKind,
                device);
        }

        return EnaPciCandidateValidationResult.Success(deviceKind, device);
    }

    public static EnaPciCandidateSelectionResult Select(
        ReadOnlySpan<PciEcamScannedDevice> devices,
        Span<EnaPciCandidate> destination)
    {
        int writtenCandidateCount = 0;
        int requiredCandidateCapacity = 0;
        int failingSourceIndex = EnaPciCandidateSelectionResult.NoFailingSourceIndex;

        for (int index = 0; index < devices.Length; index++)
        {
            EnaPciCandidateValidationResult validation = Validate(devices[index]);
            if (!validation.Succeeded)
            {
                continue;
            }

            requiredCandidateCapacity++;
            if (writtenCandidateCount >= destination.Length)
            {
                if (failingSourceIndex == EnaPciCandidateSelectionResult.NoFailingSourceIndex)
                {
                    failingSourceIndex = index;
                }

                continue;
            }

            destination[writtenCandidateCount] = CreateCandidate(
                index,
                validation.Device,
                validation.DeviceKind);
            writtenCandidateCount++;
        }

        if (requiredCandidateCapacity > destination.Length)
        {
            return EnaPciCandidateSelectionResult.DestinationTooSmall(
                devices.Length,
                writtenCandidateCount,
                requiredCandidateCapacity,
                failingSourceIndex);
        }

        return EnaPciCandidateSelectionResult.Success(
            devices.Length,
            writtenCandidateCount);
    }

    private static EnaPciCandidate CreateCandidate(
        int sourceIndex,
        PciEcamScannedDevice device,
        EnaPciDeviceKind deviceKind)
    {
        return new EnaPciCandidate(
            sourceIndex,
            device.SegmentIndex,
            device.SegmentGroup,
            device.EcamBasePhysicalAddress,
            device.SegmentStartBus,
            device.SegmentEndBus,
            device.Bus,
            device.DeviceNumber,
            device.Function,
            device.VendorId,
            device.DeviceId,
            device.RevisionId,
            device.ClassCode,
            device.Subclass,
            device.ProgrammingInterface,
            device.HeaderType,
            device.CapabilityPointer,
            deviceKind);
    }

    private static bool TryGetDeviceKind(
        ushort deviceId,
        out EnaPciDeviceKind deviceKind)
    {
        if (deviceId == VirtualFunctionDeviceId)
        {
            deviceKind = EnaPciDeviceKind.VirtualFunction;
            return true;
        }

        if (deviceId == LowLatencyQueueVirtualFunctionDeviceId)
        {
            deviceKind = EnaPciDeviceKind.LowLatencyQueueVirtualFunction;
            return true;
        }

        deviceKind = EnaPciDeviceKind.Unknown;
        return false;
    }

    private static bool IsCapabilityPointerValid(byte capabilityPointer)
    {
        if ((capabilityPointer & 0x03) != 0)
        {
            return false;
        }

        return capabilityPointer >= PciConfigurationHeader.FirstCapabilityPointer &&
            capabilityPointer <= PciConfigurationHeader.LastCapabilityPointer;
    }
}
