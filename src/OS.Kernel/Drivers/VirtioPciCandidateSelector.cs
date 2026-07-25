namespace OS.Drivers;

public enum VirtioPciCandidateSelectionStatus
{
    Success = 0,
    DestinationTooSmall = 1
}

public readonly struct VirtioPciCandidate
{
    public VirtioPciCandidate(
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
        VirtioPciDeviceKind deviceKind,
        byte virtioDeviceId,
        byte capabilityPointer)
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
        DeviceKind = deviceKind;
        VirtioDeviceId = virtioDeviceId;
        CapabilityPointer = capabilityPointer;
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

    public VirtioPciDeviceKind DeviceKind { get; }

    public byte VirtioDeviceId { get; }

    public byte CapabilityPointer { get; }
}

public readonly struct VirtioPciCandidateSelectionResult
{
    private VirtioPciCandidateSelectionResult(
        VirtioPciCandidateSelectionStatus status,
        int inspectedDeviceCount,
        int candidateCount,
        int requiredCandidateCapacity,
        int failingSourceIndex,
        bool isInitialized)
    {
        Status = status;
        InspectedDeviceCount = inspectedDeviceCount;
        CandidateCount = candidateCount;
        RequiredCandidateCapacity = requiredCandidateCapacity;
        FailingSourceIndex = failingSourceIndex;
        IsInitialized = isInitialized;
    }

    public const int NoFailingSourceIndex = -1;

    public VirtioPciCandidateSelectionStatus Status { get; }

    public int InspectedDeviceCount { get; }

    public int CandidateCount { get; }

    public int RequiredCandidateCapacity { get; }

    public int FailingSourceIndex { get; }

    public bool IsInitialized { get; }

    public bool Succeeded => IsInitialized && Status == VirtioPciCandidateSelectionStatus.Success;

    public static VirtioPciCandidateSelectionResult Success(
        int inspectedDeviceCount,
        int candidateCount)
    {
        return new VirtioPciCandidateSelectionResult(
            VirtioPciCandidateSelectionStatus.Success,
            inspectedDeviceCount,
            candidateCount,
            candidateCount,
            NoFailingSourceIndex,
            isInitialized: true);
    }

    public static VirtioPciCandidateSelectionResult DestinationTooSmall(
        int inspectedDeviceCount,
        int candidateCount,
        int requiredCandidateCapacity,
        int failingSourceIndex)
    {
        return new VirtioPciCandidateSelectionResult(
            VirtioPciCandidateSelectionStatus.DestinationTooSmall,
            inspectedDeviceCount,
            candidateCount,
            requiredCandidateCapacity,
            failingSourceIndex,
            isInitialized: true);
    }
}

public static class VirtioPciCandidateSelector
{
    public static VirtioPciCandidateSelectionResult Select(
        ReadOnlySpan<PciEcamScannedDevice> devices,
        Span<VirtioPciCandidate> destination)
    {
        int writtenCandidateCount = 0;
        int requiredCandidateCapacity = 0;
        int failingSourceIndex = VirtioPciCandidateSelectionResult.NoFailingSourceIndex;

        for (int index = 0; index < devices.Length; index++)
        {
            PciEcamScannedDevice device = devices[index];
            if (!TryGetCandidateKind(device, out VirtioPciDeviceKind deviceKind, out byte virtioDeviceId))
            {
                continue;
            }

            requiredCandidateCapacity++;
            if (writtenCandidateCount >= destination.Length)
            {
                if (failingSourceIndex == VirtioPciCandidateSelectionResult.NoFailingSourceIndex)
                {
                    failingSourceIndex = index;
                }

                continue;
            }

            destination[writtenCandidateCount] = new VirtioPciCandidate(
                index,
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
                deviceKind,
                virtioDeviceId,
                device.CapabilityPointer);
            writtenCandidateCount++;
        }

        if (requiredCandidateCapacity > destination.Length)
        {
            return VirtioPciCandidateSelectionResult.DestinationTooSmall(
                devices.Length,
                writtenCandidateCount,
                requiredCandidateCapacity,
                failingSourceIndex);
        }

        return VirtioPciCandidateSelectionResult.Success(
            devices.Length,
            writtenCandidateCount);
    }

    private static bool TryGetCandidateKind(
        PciEcamScannedDevice device,
        out VirtioPciDeviceKind deviceKind,
        out byte virtioDeviceId)
    {
        deviceKind = VirtioPciDeviceKind.Unknown;
        virtioDeviceId = 0;

        if (device.VendorId != VirtioPciDeviceDiscovery.VirtioVendorId)
        {
            return false;
        }

        if (!device.HasCapabilitiesList || device.CapabilityPointer == 0)
        {
            return false;
        }

        if (device.DeviceId < VirtioPciDeviceDiscovery.ModernDeviceIdStart ||
            device.DeviceId > VirtioPciDeviceDiscovery.ModernDeviceIdEnd)
        {
            return false;
        }

        virtioDeviceId = (byte)(device.DeviceId - VirtioPciDeviceDiscovery.ModernDeviceIdStart);
        if (virtioDeviceId == VirtioPciDeviceDiscovery.NetworkDeviceId)
        {
            deviceKind = VirtioPciDeviceKind.Network;
            return true;
        }

        if (virtioDeviceId == VirtioPciDeviceDiscovery.BlockDeviceId)
        {
            deviceKind = VirtioPciDeviceKind.Block;
            return true;
        }

        deviceKind = VirtioPciDeviceKind.Unknown;
        return false;
    }
}
