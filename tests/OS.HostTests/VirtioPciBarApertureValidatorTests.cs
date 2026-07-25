using System.Runtime.CompilerServices;
using OS.Drivers;
using OS.Std.Binary;

internal static class VirtioPciBarApertureValidatorTests
{
    [ModuleInitializer]
    internal static void RunAll()
    {
        int failures = 0;

        Run("drivers.virtio.pci.bar validates required apertures", TestValidatesRequiredApertures, ref failures);
        Run("drivers.virtio.pci.bar accepts 64bit memory bar", TestAccepts64BitMemoryBar, ref failures);
        Run("drivers.virtio.pci.bar rejects io bar capability", TestRejectsIoBarCapability, ref failures);
        Run("drivers.virtio.pci.bar rejects absent capability bar", TestRejectsAbsentCapabilityBar, ref failures);
        Run("drivers.virtio.pci.bar rejects region beyond bar size", TestRejectsRegionBeyondBarSize, ref failures);
        Run("drivers.virtio.pci.bar rejects failed discovery input", TestRejectsFailedDiscoveryInput, ref failures);
        Run("drivers.virtio.pci.bar default result is not success", TestDefaultResultIsNotSuccess, ref failures);

        if (failures != 0)
        {
            throw new InvalidOperationException("Virtio PCI BAR aperture validator host tests failed: " + failures);
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

    private static bool TestValidatesRequiredApertures()
    {
        PciBarDescriptorSet bars = DecodeBars(
            bar0: 0x80000000,
            bar1: 0x80001000,
            bar2: 0x80002000,
            bar3: 0x80003000);
        ulong[] barSizes = CreateBarSizes(0x1000, 0x2000, 0x100, 0x400);
        VirtioPciDeviceDiscoveryResult discovery = CreateBlockDiscovery(
            Common(bar: 0, offset: 0x20),
            Notify(bar: 1, offset: 0x100, length: 0x100, multiplier: 4),
            Isr(bar: 2, offset: 0),
            Device(bar: 3, offset: 0x80));

        VirtioPciBarApertureValidationResult result =
            VirtioPciBarApertureValidator.Validate(discovery, bars, barSizes);

        return result.Succeeded
            && result.IsInitialized
            && result.Status == VirtioPciBarApertureValidationStatus.Success
            && result.FailingBarIndex == PciBarDecoder.NoBarIndex
            && result.Apertures.CommonConfiguration.CapabilityType == VirtioPciCapabilityType.CommonConfiguration
            && result.Apertures.CommonConfiguration.BarIndex == 0
            && result.Apertures.CommonConfiguration.BarKind == PciBarKind.Memory32
            && result.Apertures.CommonConfiguration.BarBaseAddressBits == 0x80000000
            && result.Apertures.CommonConfiguration.Offset == 0x20
            && result.Apertures.CommonConfiguration.Length == VirtioPciDeviceDiscovery.CommonConfigurationMinimumLength
            && result.Apertures.CommonConfiguration.EndOffset
                == 0x20 + VirtioPciDeviceDiscovery.CommonConfigurationMinimumLength
            && result.Apertures.NotifyConfiguration.BarIndex == 1
            && result.Apertures.NotifyConfiguration.NotifyOffsetMultiplier == 4
            && result.Apertures.IsrStatus.BarIndex == 2
            && result.Apertures.DeviceConfiguration.BarIndex == 3;
    }

    private static bool TestAccepts64BitMemoryBar()
    {
        PciBarDescriptorSet bars = DecodeBars(
            bar0: 0x80000004,
            bar1: 0x00000001);
        ulong[] barSizes = CreateBarSizes(0x1000);
        VirtioPciDeviceDiscoveryResult discovery = CreateBlockDiscovery(
            Common(bar: 0, offset: 0x00),
            Notify(bar: 0, offset: 0x80, length: 0x100, multiplier: 8),
            Isr(bar: 0, offset: 0x200),
            Device(bar: 0, offset: 0x300));

        VirtioPciBarApertureValidationResult result =
            VirtioPciBarApertureValidator.Validate(discovery, bars, barSizes);

        if (!result.Succeeded ||
            result.Apertures.CommonConfiguration.BarKind != PciBarKind.Memory64 ||
            result.Apertures.CommonConfiguration.BarBaseAddressBits != 0x0000000180000000UL ||
            result.Apertures.NotifyConfiguration.BarKind != PciBarKind.Memory64)
        {
            return false;
        }

        VirtioPciDeviceDiscoveryResult consumedUpperHalfDiscovery = CreateBlockDiscovery(
            Common(bar: 1, offset: 0x00),
            Notify(bar: 0, offset: 0x80, length: 0x100, multiplier: 8),
            Isr(bar: 0, offset: 0x200),
            Device(bar: 0, offset: 0x300));
        VirtioPciBarApertureValidationResult consumedUpperHalf =
            VirtioPciBarApertureValidator.Validate(consumedUpperHalfDiscovery, bars, barSizes);

        return !consumedUpperHalf.Succeeded
            && consumedUpperHalf.Status
                == VirtioPciBarApertureValidationStatus.BarConsumedByPrevious64BitBar
            && consumedUpperHalf.FailingCapabilityType == VirtioPciCapabilityType.CommonConfiguration
            && consumedUpperHalf.FailingBarIndex == 1
            && consumedUpperHalf.FailingBarKind == PciBarKind.ConsumedByPrevious64BitBar;
    }

    private static bool TestRejectsIoBarCapability()
    {
        PciBarDescriptorSet bars = DecodeBars(bar0: 0x0000C001);
        ulong[] barSizes = CreateBarSizes(0x100);
        VirtioPciDeviceDiscoveryResult discovery = CreateBlockDiscovery(
            Common(bar: 0, offset: 0),
            Notify(bar: 0, offset: 0x40, length: 0x40, multiplier: 4),
            Isr(bar: 0, offset: 0x80),
            Device(bar: 0, offset: 0x90));

        VirtioPciBarApertureValidationResult result =
            VirtioPciBarApertureValidator.Validate(discovery, bars, barSizes);

        return !result.Succeeded
            && result.Status == VirtioPciBarApertureValidationStatus.BarIsIo
            && result.FailingCapabilityType == VirtioPciCapabilityType.CommonConfiguration
            && result.FailingBarIndex == 0
            && result.FailingBarKind == PciBarKind.Io;
    }

    private static bool TestRejectsAbsentCapabilityBar()
    {
        PciBarDescriptorSet absentBars = DecodeBars();
        ulong[] barSizes = CreateBarSizes(0x100);
        VirtioPciDeviceDiscoveryResult absentDiscovery = CreateBlockDiscovery(
            Common(bar: 0, offset: 0),
            Notify(bar: 0, offset: 0x40, length: 0x40, multiplier: 4),
            Isr(bar: 0, offset: 0x80),
            Device(bar: 0, offset: 0x90));

        VirtioPciBarApertureValidationResult absent =
            VirtioPciBarApertureValidator.Validate(absentDiscovery, absentBars, barSizes);
        if (absent.Succeeded ||
            absent.Status != VirtioPciBarApertureValidationStatus.BarAbsent ||
            absent.FailingCapabilityType != VirtioPciCapabilityType.CommonConfiguration ||
            absent.FailingBarIndex != 0 ||
            absent.FailingBarKind != PciBarKind.Absent)
        {
            return false;
        }

        PciBarDescriptorSet memoryBars = DecodeBars(bar0: 0x80000000);
        VirtioPciDeviceDiscoveryResult outOfRangeDiscovery = CreateBlockDiscovery(
            Common(bar: 6, offset: 0),
            Notify(bar: 0, offset: 0x40, length: 0x40, multiplier: 4),
            Isr(bar: 0, offset: 0x80),
            Device(bar: 0, offset: 0x90));
        VirtioPciBarApertureValidationResult outOfRange =
            VirtioPciBarApertureValidator.Validate(outOfRangeDiscovery, memoryBars, barSizes);
        if (outOfRange.Succeeded ||
            outOfRange.Status != VirtioPciBarApertureValidationStatus.BarIndexOutOfRange ||
            outOfRange.FailingBarIndex != 6)
        {
            return false;
        }

        VirtioPciBarApertureValidationResult missingSize =
            VirtioPciBarApertureValidator.Validate(absentDiscovery, memoryBars, ReadOnlySpan<ulong>.Empty);
        return !missingSize.Succeeded
            && missingSize.Status == VirtioPciBarApertureValidationStatus.BarSizeUnavailable
            && missingSize.FailingBarIndex == 0
            && missingSize.FailingBarKind == PciBarKind.Memory32;
    }

    private static bool TestRejectsRegionBeyondBarSize()
    {
        PciBarDescriptorSet bars = DecodeBars(bar0: 0x80000000);
        ulong[] barSizes = CreateBarSizes(0x40);
        VirtioPciDeviceDiscoveryResult beyondSizeDiscovery = CreateBlockDiscovery(
            Common(bar: 0, offset: 0x20),
            Notify(bar: 0, offset: 0x10, length: 0x20, multiplier: 4),
            Isr(bar: 0, offset: 0x30),
            Device(bar: 0, offset: 0x38));

        VirtioPciBarApertureValidationResult beyondSize =
            VirtioPciBarApertureValidator.Validate(beyondSizeDiscovery, bars, barSizes);
        if (beyondSize.Succeeded ||
            beyondSize.Status != VirtioPciBarApertureValidationStatus.CapabilityRegionBeyondBarSize ||
            beyondSize.FailingCapabilityType != VirtioPciCapabilityType.CommonConfiguration ||
            beyondSize.FailingBarSize != 0x40 ||
            beyondSize.FailingRegionEndOffset
                != 0x20 + VirtioPciDeviceDiscovery.CommonConfigurationMinimumLength)
        {
            return false;
        }

        VirtioPciDeviceDiscoveryResult overflowDiscovery = CreateBlockDiscovery(
            Common(bar: 0, offset: uint.MaxValue, length: 1),
            Notify(bar: 0, offset: 0x10, length: 0x20, multiplier: 4),
            Isr(bar: 0, offset: 0x30),
            Device(bar: 0, offset: 0x38));
        VirtioPciBarApertureValidationResult overflow =
            VirtioPciBarApertureValidator.Validate(overflowDiscovery, bars, CreateBarSizes(ulong.MaxValue));

        return !overflow.Succeeded
            && overflow.Status == VirtioPciBarApertureValidationStatus.CapabilityRegionOverflow
            && overflow.FailingCapabilityType == VirtioPciCapabilityType.CommonConfiguration;
    }

    private static bool TestRejectsFailedDiscoveryInput()
    {
        VirtioPciDeviceDiscoveryResult failedDiscovery = VirtioPciDeviceDiscovery.Discover(
            new PciDeviceIdentity(0x1234, 0x1042),
            new VirtioPciCapabilityMetadata[]
            {
                Common(bar: 0, offset: 0),
                Notify(bar: 0, offset: 0x40, length: 0x40, multiplier: 4),
                Isr(bar: 0, offset: 0x80),
                Device(bar: 0, offset: 0x90)
            });

        VirtioPciBarApertureValidationResult result =
            VirtioPciBarApertureValidator.Validate(
                failedDiscovery,
                DecodeBars(bar0: 0x80000000),
                CreateBarSizes(0x1000));

        return !result.Succeeded
            && result.IsInitialized
            && result.Status == VirtioPciBarApertureValidationStatus.DiscoveryFailed
            && result.DiscoveryStatus == VirtioPciDeviceDiscoveryStatus.UnsupportedVendor
            && result.FailingBarIndex == PciBarDecoder.NoBarIndex;
    }

    private static bool TestDefaultResultIsNotSuccess()
    {
        VirtioPciBarApertureValidationResult result = default;

        return !result.IsInitialized
            && !result.Succeeded
            && result.Status == VirtioPciBarApertureValidationStatus.Success
            && result.Apertures.CommonConfiguration.Length == 0;
    }

    private static VirtioPciDeviceDiscoveryResult CreateBlockDiscovery(
        VirtioPciCapabilityMetadata commonConfiguration,
        VirtioPciCapabilityMetadata notifyConfiguration,
        VirtioPciCapabilityMetadata isrStatus,
        VirtioPciCapabilityMetadata deviceConfiguration)
    {
        return VirtioPciDeviceDiscoveryResult.Success(
            new PciDeviceIdentity(
                VirtioPciDeviceDiscovery.VirtioVendorId,
                VirtioPciDeviceDiscovery.ModernDeviceIdStart + VirtioPciDeviceDiscovery.BlockDeviceId),
            VirtioPciDeviceKind.Block,
            VirtioPciDeviceDiscovery.BlockDeviceId,
            VirtioPciCapabilitySet.Create(
                commonConfiguration,
                notifyConfiguration,
                isrStatus,
                deviceConfiguration));
    }

    private static VirtioPciCapabilityMetadata Common(
        byte bar,
        uint offset,
        uint length = VirtioPciDeviceDiscovery.CommonConfigurationMinimumLength)
    {
        return new VirtioPciCapabilityMetadata(
            VirtioPciCapabilityType.CommonConfiguration,
            bar,
            offset,
            length,
            notifyOffsetMultiplier: 0);
    }

    private static VirtioPciCapabilityMetadata Notify(
        byte bar,
        uint offset,
        uint length,
        uint multiplier)
    {
        return new VirtioPciCapabilityMetadata(
            VirtioPciCapabilityType.NotifyConfiguration,
            bar,
            offset,
            length,
            multiplier);
    }

    private static VirtioPciCapabilityMetadata Isr(byte bar, uint offset)
    {
        return new VirtioPciCapabilityMetadata(
            VirtioPciCapabilityType.IsrStatus,
            bar,
            offset,
            VirtioPciDeviceDiscovery.IsrStatusMinimumLength,
            notifyOffsetMultiplier: 0);
    }

    private static VirtioPciCapabilityMetadata Device(byte bar, uint offset)
    {
        return new VirtioPciCapabilityMetadata(
            VirtioPciCapabilityType.DeviceConfiguration,
            bar,
            offset,
            VirtioPciDeviceDiscovery.BlockDeviceConfigurationMinimumLength,
            notifyOffsetMultiplier: 0);
    }

    private static PciBarDescriptorSet DecodeBars(
        uint bar0 = 0,
        uint bar1 = 0,
        uint bar2 = 0,
        uint bar3 = 0,
        uint bar4 = 0,
        uint bar5 = 0)
    {
        byte[] headerBytes = CreateBaseHeader();
        WriteUInt32(headerBytes, 0x10, bar0);
        WriteUInt32(headerBytes, 0x14, bar1);
        WriteUInt32(headerBytes, 0x18, bar2);
        WriteUInt32(headerBytes, 0x1C, bar3);
        WriteUInt32(headerBytes, 0x20, bar4);
        WriteUInt32(headerBytes, 0x24, bar5);

        PciConfigurationHeaderResult headerResult = PciConfigurationHeader.ParseType0(headerBytes);
        if (!headerResult.Succeeded)
        {
            throw new InvalidOperationException("test header parse failed: " + headerResult.Status);
        }

        return PciBarDecoder.Decode(headerResult.Header).Bars;
    }

    private static byte[] CreateBaseHeader()
    {
        byte[] headerBytes = new byte[PciConfigurationHeader.Type0HeaderLength];
        EndianPrimitives.WriteUInt16LittleEndian(headerBytes.AsSpan(0x00, sizeof(ushort)), 0x1AF4);
        EndianPrimitives.WriteUInt16LittleEndian(headerBytes.AsSpan(0x02, sizeof(ushort)), 0x1042);
        headerBytes[0x0E] = PciConfigurationHeader.Type0HeaderType;
        return headerBytes;
    }

    private static ulong[] CreateBarSizes(
        ulong bar0 = 0,
        ulong bar1 = 0,
        ulong bar2 = 0,
        ulong bar3 = 0,
        ulong bar4 = 0,
        ulong bar5 = 0)
    {
        return new ulong[]
        {
            bar0,
            bar1,
            bar2,
            bar3,
            bar4,
            bar5
        };
    }

    private static void WriteUInt32(byte[] destination, int offset, uint value)
    {
        EndianPrimitives.WriteUInt32LittleEndian(destination.AsSpan(offset, sizeof(uint)), value);
    }
}
