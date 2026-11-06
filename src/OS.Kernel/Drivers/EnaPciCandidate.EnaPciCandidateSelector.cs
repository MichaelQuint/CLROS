namespace OS.Drivers;

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
            return EnaPciCandidateValidationResult.Fail(EnaPciCandidateValidationStatus.UnsupportedVendor, EnaPciDeviceKind.Unknown, device);
        }

        if (!TryGetDeviceKind(device.DeviceId, out EnaPciDeviceKind deviceKind))
        {
            return EnaPciCandidateValidationResult.Fail(EnaPciCandidateValidationStatus.UnsupportedDeviceId, EnaPciDeviceKind.Unknown, device);
        }

        if ((device.HeaderType & PciConfigurationHeader.HeaderTypeMask) != PciConfigurationHeader.Type0HeaderType)
        {
            return EnaPciCandidateValidationResult.Fail(EnaPciCandidateValidationStatus.UnsupportedHeaderType, deviceKind, device);
        }

        if (device.ClassCode != NetworkControllerClassCode)
        {
            return EnaPciCandidateValidationResult.Fail(EnaPciCandidateValidationStatus.UnsupportedClassCode, deviceKind, device);
        }

        if (device.Subclass != EthernetControllerSubclass)
        {
            return EnaPciCandidateValidationResult.Fail(EnaPciCandidateValidationStatus.UnsupportedSubclass, deviceKind, device);
        }

        if (device.ProgrammingInterface != EthernetProgrammingInterface)
        {
            return EnaPciCandidateValidationResult.Fail(EnaPciCandidateValidationStatus.UnsupportedProgrammingInterface, deviceKind, device);
        }

        if (!device.HasCapabilitiesList)
        {
            return EnaPciCandidateValidationResult.Fail(EnaPciCandidateValidationStatus.MissingCapabilitiesList, deviceKind, device);
        }

        if (!IsCapabilityPointerValid(device.CapabilityPointer))
        {
            return EnaPciCandidateValidationResult.Fail(EnaPciCandidateValidationStatus.InvalidCapabilityPointer, deviceKind, device);
        }

        return EnaPciCandidateValidationResult.Success(deviceKind, device);
    }

    public static EnaPciCandidateSelectionResult Select(ReadOnlySpan<PciEcamScannedDevice> devices, Span<EnaPciCandidate> destination)
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

            destination[writtenCandidateCount] = CreateCandidate(index, validation.Device, validation.DeviceKind);
            writtenCandidateCount++;
        }

        if (requiredCandidateCapacity > destination.Length)
        {
            return EnaPciCandidateSelectionResult.DestinationTooSmall(devices.Length, writtenCandidateCount, requiredCandidateCapacity, failingSourceIndex);
        }

        return EnaPciCandidateSelectionResult.Success(devices.Length, writtenCandidateCount);
    }

    private static EnaPciCandidate CreateCandidate(int sourceIndex, PciEcamScannedDevice device, EnaPciDeviceKind deviceKind)
    {
        return new EnaPciCandidate(sourceIndex, device.SegmentIndex, device.SegmentGroup, device.EcamBasePhysicalAddress, device.SegmentStartBus, device.SegmentEndBus, device.Bus, device.DeviceNumber, device.Function, device.VendorId, device.DeviceId, device.RevisionId, device.ClassCode, device.Subclass, device.ProgrammingInterface, device.HeaderType, device.CapabilityPointer, deviceKind);
    }

    private static bool TryGetDeviceKind(ushort deviceId, out EnaPciDeviceKind deviceKind)
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

        return capabilityPointer >= PciConfigurationHeader.FirstCapabilityPointer && capabilityPointer <= PciConfigurationHeader.LastCapabilityPointer;
    }
}
