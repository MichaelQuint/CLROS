namespace OS.Drivers;

public static class VirtioPciCandidateSelector
{
    public static VirtioPciCandidateSelectionResult Select(ReadOnlySpan<PciEcamScannedDevice> devices, Span<VirtioPciCandidate> destination)
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

            destination[writtenCandidateCount] = new VirtioPciCandidate(index, device.SegmentIndex, device.SegmentGroup, device.EcamBasePhysicalAddress, device.SegmentStartBus, device.SegmentEndBus, device.Bus, device.DeviceNumber, device.Function, device.VendorId, device.DeviceId, deviceKind, virtioDeviceId, device.CapabilityPointer);
            writtenCandidateCount++;
        }

        if (requiredCandidateCapacity > destination.Length)
        {
            return VirtioPciCandidateSelectionResult.DestinationTooSmall(devices.Length, writtenCandidateCount, requiredCandidateCapacity, failingSourceIndex);
        }

        return VirtioPciCandidateSelectionResult.Success(devices.Length, writtenCandidateCount);
    }

    private static bool TryGetCandidateKind(PciEcamScannedDevice device, out VirtioPciDeviceKind deviceKind, out byte virtioDeviceId)
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

        if (device.DeviceId < VirtioPciDeviceDiscovery.ModernDeviceIdStart || device.DeviceId > VirtioPciDeviceDiscovery.ModernDeviceIdEnd)
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
