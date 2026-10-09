using System.Runtime.CompilerServices;
using OS.Drivers;

internal static class PciEcamConfigurationSpaceTests
{

    [HostTest("drivers.pci.ecam calculates config address", HostLane.Fast, 0)]
    private static bool TestCalculatesConfigAddress()
    {
        PciEcamSegment segment = new PciEcamSegment(
            basePhysicalAddress: 0xE0000000,
            segmentGroup: 2,
            startBus: 0x20,
            endBus: 0x2F);

        PciEcamConfigurationAddressResult result = PciEcamConfigurationSpace.CalculateAddress(
            segment,
            bus: 0x24,
            device: 3,
            function: 5,
            registerOffset: 0x10,
            accessWidth: 4);

        ulong expectedOffset =
            (4UL * PciEcamConfigurationSpace.BytesPerBus) +
            (3UL * PciEcamConfigurationSpace.BytesPerDevice) +
            (5UL * PciEcamConfigurationSpace.BytesPerFunction) +
            0x10UL;

        return HostCheck.Result(result.Succeeded
            && result.IsInitialized
            && result.Status == PciEcamConfigurationSpaceStatus.Success
            && result.Segment.SegmentGroup == 2
            && result.Bus == 0x24
            && result.Device == 3
            && result.Function == 5
            && result.RegisterOffset == 0x10
            && result.AccessWidth == 4
            && result.EcamOffset == expectedOffset
            && result.PhysicalAddress == 0xE0000000UL + expectedOffset);
    }

    [HostTest("drivers.pci.ecam rejects bus outside segment", HostLane.Fast, 1)]
    private static bool TestRejectsBusOutsideSegment()
    {
        PciEcamSegment segment = new PciEcamSegment(
            basePhysicalAddress: 0xE0000000,
            segmentGroup: 0,
            startBus: 8,
            endBus: 12);

        PciEcamConfigurationAddressResult beforeStart = PciEcamConfigurationSpace.CalculateAddress(
            segment,
            bus: 7,
            device: 0,
            function: 0,
            registerOffset: 0,
            accessWidth: 4);

        PciEcamConfigurationAddressResult afterEnd = PciEcamConfigurationSpace.CalculateAddress(
            segment,
            bus: 13,
            device: 0,
            function: 0,
            registerOffset: 0,
            accessWidth: 4);

        PciEcamConfigurationAddressResult invalidRange = PciEcamConfigurationSpace.CalculateAddress(
            new PciEcamSegment(0xE0000000, segmentGroup: 0, startBus: 12, endBus: 8),
            bus: 10,
            device: 0,
            function: 0,
            registerOffset: 0,
            accessWidth: 4);

        return HostCheck.Result(!beforeStart.Succeeded
            && beforeStart.IsInitialized
            && beforeStart.Status == PciEcamConfigurationSpaceStatus.BusOutsideSegment
            && !afterEnd.Succeeded
            && afterEnd.IsInitialized
            && afterEnd.Status == PciEcamConfigurationSpaceStatus.BusOutsideSegment
            && !invalidRange.Succeeded
            && invalidRange.IsInitialized
            && invalidRange.Status == PciEcamConfigurationSpaceStatus.InvalidBusRange);
    }

    [HostTest("drivers.pci.ecam rejects invalid device function register", HostLane.Fast, 2)]
    private static bool TestRejectsInvalidDeviceFunctionRegister()
    {
        PciEcamSegment segment = new PciEcamSegment(
            basePhysicalAddress: 0xE0000000,
            segmentGroup: 0,
            startBus: 0,
            endBus: 0);

        PciEcamConfigurationAddressResult invalidDevice = PciEcamConfigurationSpace.CalculateAddress(
            segment,
            bus: 0,
            device: 32,
            function: 0,
            registerOffset: 0,
            accessWidth: 4);

        PciEcamConfigurationAddressResult invalidFunction = PciEcamConfigurationSpace.CalculateAddress(
            segment,
            bus: 0,
            device: 0,
            function: 8,
            registerOffset: 0,
            accessWidth: 4);

        PciEcamConfigurationAddressResult invalidRegister = PciEcamConfigurationSpace.CalculateAddress(
            segment,
            bus: 0,
            device: 0,
            function: 0,
            registerOffset: 4096,
            accessWidth: 1);

        PciEcamConfigurationAddressResult crossingRegister = PciEcamConfigurationSpace.CalculateAddress(
            segment,
            bus: 0,
            device: 0,
            function: 0,
            registerOffset: 4095,
            accessWidth: 2);

        PciEcamConfigurationAddressResult invalidWidth = PciEcamConfigurationSpace.CalculateAddress(
            segment,
            bus: 0,
            device: 0,
            function: 0,
            registerOffset: 0,
            accessWidth: 8);

        PciEcamConfigurationAddressResult invalidAlignment = PciEcamConfigurationSpace.CalculateAddress(
            segment,
            bus: 0,
            device: 0,
            function: 0,
            registerOffset: 2,
            accessWidth: 4);

        return HostCheck.Result(!invalidDevice.Succeeded
            && invalidDevice.IsInitialized
            && invalidDevice.Status == PciEcamConfigurationSpaceStatus.InvalidDevice
            && !invalidFunction.Succeeded
            && invalidFunction.IsInitialized
            && invalidFunction.Status == PciEcamConfigurationSpaceStatus.InvalidFunction
            && !invalidRegister.Succeeded
            && invalidRegister.IsInitialized
            && invalidRegister.Status == PciEcamConfigurationSpaceStatus.InvalidRegister
            && !crossingRegister.Succeeded
            && crossingRegister.IsInitialized
            && crossingRegister.Status == PciEcamConfigurationSpaceStatus.InvalidRegister
            && !invalidWidth.Succeeded
            && invalidWidth.IsInitialized
            && invalidWidth.Status == PciEcamConfigurationSpaceStatus.InvalidAccessWidth
            && !invalidAlignment.Succeeded
            && invalidAlignment.IsInitialized
            && invalidAlignment.Status == PciEcamConfigurationSpaceStatus.InvalidRegisterAlignment);
    }

    [HostTest("drivers.pci.ecam rejects address overflow", HostLane.Fast, 3)]
    private static bool TestRejectsAddressOverflow()
    {
        PciEcamSegment segment = new PciEcamSegment(
            basePhysicalAddress: ulong.MaxValue - 0x1000,
            segmentGroup: 0,
            startBus: 0,
            endBus: 1);

        PciEcamConfigurationAddressResult result = PciEcamConfigurationSpace.CalculateAddress(
            segment,
            bus: 1,
            device: 0,
            function: 0,
            registerOffset: 0,
            accessWidth: 4);

        return HostCheck.Result(!result.Succeeded
            && result.IsInitialized
            && result.Status == PciEcamConfigurationSpaceStatus.AddressOverflow
            && result.PhysicalAddress == 0);
    }

    [HostTest("drivers.pci.ecam default result is not success", HostLane.Fast, 4)]
    private static bool TestDefaultResultIsNotSuccess()
    {
        PciEcamConfigurationAddressResult result = default;

        return HostCheck.Result(!result.IsInitialized
            && !result.Succeeded
            && result.Status == PciEcamConfigurationSpaceStatus.Success
            && result.Segment.BasePhysicalAddress == 0
            && result.Segment.SegmentGroup == 0
            && result.Segment.StartBus == 0
            && result.Segment.EndBus == 0
            && result.Bus == 0
            && result.Device == 0
            && result.Function == 0
            && result.RegisterOffset == 0
            && result.AccessWidth == 0
            && result.EcamOffset == 0
            && result.PhysicalAddress == 0);
    }
}
