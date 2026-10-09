using System.Runtime.CompilerServices;
using OS.Drivers;
using OS.Std.Binary;

internal static class VirtioPciIsrStatusTests
{

    [HostTest("drivers.virtiopci.isr parses queue interrupt", HostLane.Fast, 0)]
    private static bool TestParsesQueueInterrupt()
    {
        VirtioPciIsrStatus status =
            VirtioPciIsrStatusParser.Parse(VirtioPciIsrStatusParser.QueueInterruptMask);

        return HostCheck.Result(status.RawStatus == VirtioPciIsrStatusParser.QueueInterruptMask
            && status.HasQueueInterrupt
            && !status.HasDeviceConfigurationChange
            && status.ReservedBits == 0);
    }

    [HostTest("drivers.virtiopci.isr parses config change", HostLane.Fast, 1)]
    private static bool TestParsesConfigChange()
    {
        VirtioPciIsrStatus status =
            VirtioPciIsrStatusParser.Parse(VirtioPciIsrStatusParser.DeviceConfigurationChangeMask);

        return HostCheck.Result(status.RawStatus == VirtioPciIsrStatusParser.DeviceConfigurationChangeMask
            && !status.HasQueueInterrupt
            && status.HasDeviceConfigurationChange
            && status.ReservedBits == 0);
    }

    [HostTest("drivers.virtiopci.isr preserves reserved bits", HostLane.Fast, 2)]
    private static bool TestPreservesReservedBits()
    {
        const byte rawStatus = 0xAD;
        VirtioPciIsrStatus status = VirtioPciIsrStatusParser.Parse(rawStatus);

        return HostCheck.Result(status.RawStatus == rawStatus
            && status.HasQueueInterrupt
            && !status.HasDeviceConfigurationChange
            && status.ReservedBits == (rawStatus & VirtioPciIsrStatusParser.ReservedBitsMask));
    }

    [HostTest("drivers.virtiopci.isr plans one byte read", HostLane.Fast, 3)]
    private static bool TestPlansOneByteRead()
    {
        VirtioPciBarApertureSet apertures = CreateApertures();

        VirtioPciIsrReadPlanResult result =
            VirtioPciIsrStatusParser.CreateReadPlan(apertures.IsrStatus);
        VirtioPciIsrReadPlanResult wrongAperture =
            VirtioPciIsrStatusParser.CreateReadPlan(apertures.CommonConfiguration);

        return HostCheck.Result(result.Succeeded
            && result.Status == VirtioPciIsrReadPlanStatus.Success
            && result.Plan.Aperture.CapabilityType == VirtioPciCapabilityType.IsrStatus
            && result.Plan.RelativeOffset == VirtioPciIsrStatusParser.ReadOffset
            && result.Plan.RelativeOffset == 0
            && result.Plan.WidthBytes == VirtioPciIsrStatusParser.ReadWidthBytes
            && result.Plan.WidthBytes == 1
            && result.Plan.ReadClearsStatus
            && VirtioPciIsrStatusParser.IntxReadClearsStatus
            && !VirtioPciIsrStatusParser.ConfiguresMsix
            && !VirtioPciIsrStatusParser.ConfiguresApicOrIoApic
            && !VirtioPciIsrStatusParser.DrainsQueues
            && !VirtioPciIsrStatusParser.DispatchesPackets
            && !wrongAperture.Succeeded
            && wrongAperture.Status == VirtioPciIsrReadPlanStatus.InvalidApertureCapability);
    }

    [HostTest("drivers.virtiopci.isr default read plan result is not success", HostLane.Fast, 4)]
    private static bool TestDefaultReadPlanResultIsNotSuccess()
    {
        VirtioPciIsrReadPlanResult result = default;

        return HostCheck.Result(!result.IsInitialized
            && !result.Succeeded
            && result.Status == VirtioPciIsrReadPlanStatus.Success
            && result.Aperture.CapabilityType == default
            && result.Plan.Aperture.CapabilityType == default
            && result.Plan.RelativeOffset == VirtioPciIsrStatusParser.ReadOffset
            && result.Plan.WidthBytes == VirtioPciIsrStatusParser.ReadWidthBytes
            && result.Plan.ReadClearsStatus == VirtioPciIsrStatusParser.IntxReadClearsStatus
            && !RuntimeHelpers.IsReferenceOrContainsReferences<VirtioPciIsrReadPlanResult>());
    }

    private static VirtioPciBarApertureSet CreateApertures()
    {
        VirtioPciDeviceDiscoveryResult discovery = VirtioPciDeviceDiscoveryResult.Success(
            new PciDeviceIdentity(
                VirtioPciDeviceDiscovery.VirtioVendorId,
                VirtioPciDeviceDiscovery.ModernDeviceIdStart + VirtioPciDeviceDiscovery.NetworkDeviceId),
            VirtioPciDeviceKind.Network,
            VirtioPciDeviceDiscovery.NetworkDeviceId,
            VirtioPciCapabilitySet.Create(
                Capability(VirtioPciCapabilityType.CommonConfiguration, bar: 0, offset: 0x20, length: 0x40),
                Capability(VirtioPciCapabilityType.NotifyConfiguration, bar: 1, offset: 0x40, length: 0x80),
                Capability(VirtioPciCapabilityType.IsrStatus, bar: 2, offset: 0, length: 1),
                Capability(VirtioPciCapabilityType.DeviceConfiguration, bar: 3, offset: 0, length: 0x10)));

        VirtioPciBarApertureValidationResult validation = VirtioPciBarApertureValidator.Validate(
            discovery,
            DecodeBars(
                bar0: 0x80000000,
                bar1: 0x80001000,
                bar2: 0x80002000,
                bar3: 0x80003000),
            new ulong[] { 0x1000, 0x1000, 0x1000, 0x1000, 0, 0 });

        if (!validation.Succeeded)
        {
            throw new InvalidOperationException("Failed to create deterministic ISR aperture.");
        }

        return validation.Apertures;
    }

    private static VirtioPciCapabilityMetadata Capability(
        VirtioPciCapabilityType type,
        byte bar,
        uint offset,
        uint length)
    {
        return new VirtioPciCapabilityMetadata(
            type,
            bar,
            offset,
            length,
            notifyOffsetMultiplier: type == VirtioPciCapabilityType.NotifyConfiguration ? 4U : 0U);
    }

    private static PciBarDescriptorSet DecodeBars(
        uint bar0,
        uint bar1,
        uint bar2,
        uint bar3)
    {
        byte[] headerBytes = new byte[PciConfigurationHeader.Type0HeaderLength];
        EndianPrimitives.WriteUInt16LittleEndian(
            headerBytes.AsSpan(0x00, sizeof(ushort)),
            VirtioPciDeviceDiscovery.VirtioVendorId);
        EndianPrimitives.WriteUInt16LittleEndian(
            headerBytes.AsSpan(0x02, sizeof(ushort)),
            (ushort)(VirtioPciDeviceDiscovery.ModernDeviceIdStart + VirtioPciDeviceDiscovery.NetworkDeviceId));
        headerBytes[0x0E] = PciConfigurationHeader.Type0HeaderType;
        WriteUInt32(headerBytes, 0x10, bar0);
        WriteUInt32(headerBytes, 0x14, bar1);
        WriteUInt32(headerBytes, 0x18, bar2);
        WriteUInt32(headerBytes, 0x1C, bar3);

        PciConfigurationHeaderResult headerResult = PciConfigurationHeader.ParseType0(headerBytes);
        if (!headerResult.Succeeded)
        {
            throw new InvalidOperationException("Failed to parse deterministic PCI header.");
        }

        return PciBarDecoder.Decode(headerResult.Header).Bars;
    }

    private static void WriteUInt32(byte[] destination, int offset, uint value)
    {
        EndianPrimitives.WriteUInt32LittleEndian(destination.AsSpan(offset, sizeof(uint)), value);
    }
}
