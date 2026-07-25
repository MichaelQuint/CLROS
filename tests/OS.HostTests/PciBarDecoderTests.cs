using System.Runtime.CompilerServices;
using OS.Drivers;
using OS.Std.Binary;

internal static class PciBarDecoderTests
{
    [ModuleInitializer]
    internal static void RunAll()
    {
        int failures = 0;

        Run("drivers.pci.bar decodes 32bit mmio", TestDecodes32BitMmio, ref failures);
        Run("drivers.pci.bar decodes 64bit mmio pair", TestDecodes64BitMmioPair, ref failures);
        Run("drivers.pci.bar decodes io aperture", TestDecodesIoAperture, ref failures);
        Run("drivers.pci.bar records absent zero", TestRecordsAbsentZero, ref failures);
        Run("drivers.pci.bar rejects reserved memory type", TestRejectsReservedMemoryType, ref failures);
        Run("drivers.pci.bar rejects 64bit at index 5", TestRejects64BitAtIndex5, ref failures);
        Run("drivers.pci.bar default result is not success", TestDefaultResultIsNotSuccess, ref failures);

        if (failures != 0)
        {
            throw new InvalidOperationException("PCI BAR decoder host tests failed: " + failures);
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

    private static bool TestDecodes32BitMmio()
    {
        byte[] headerBytes = CreateBaseHeader();
        WriteUInt32(headerBytes, 0x10, 0xFE000008);

        PciBarDecodeResult result = PciBarDecoder.Decode(ParseHeader(headerBytes));
        PciBarDescriptor bar0 = result.Bars.Bar0;

        return result.Succeeded
            && result.IsInitialized
            && result.Status == PciBarDecodeStatus.Success
            && result.FailingBarIndex == PciBarDecoder.NoBarIndex
            && bar0.Index == 0
            && bar0.Kind == PciBarKind.Memory32
            && bar0.Status == PciBarDescriptorStatus.Success
            && bar0.RawValue == 0xFE000008
            && bar0.RawUpperValue == 0
            && bar0.BaseAddressBits == 0xFE000000
            && bar0.IsPrefetchable
            && bar0.IsPresent
            && bar0.IsMemory
            && !bar0.IsIo
            && !bar0.ConsumesNextSlot
            && bar0.PairBarIndex == PciBarDecoder.NoBarIndex
            && result.Bars.Bar1.Kind == PciBarKind.Absent;
    }

    private static bool TestDecodes64BitMmioPair()
    {
        byte[] headerBytes = CreateBaseHeader();
        WriteUInt32(headerBytes, 0x18, 0x8000000C);
        WriteUInt32(headerBytes, 0x1C, 0x00000001);

        PciBarDecodeResult result = PciBarDecoder.Decode(ParseHeader(headerBytes));
        PciBarDescriptor bar2 = result.Bars.Bar2;
        PciBarDescriptor bar3 = result.Bars.Bar3;

        return result.Succeeded
            && result.IsInitialized
            && bar2.Index == 2
            && bar2.Kind == PciBarKind.Memory64
            && bar2.Status == PciBarDescriptorStatus.Success
            && bar2.RawValue == 0x8000000C
            && bar2.RawUpperValue == 0x00000001
            && bar2.BaseAddressBits == 0x0000000180000000UL
            && bar2.IsPrefetchable
            && bar2.ConsumesNextSlot
            && bar2.PairBarIndex == 3
            && bar3.Index == 3
            && bar3.Kind == PciBarKind.ConsumedByPrevious64BitBar
            && bar3.Status == PciBarDescriptorStatus.ConsumedByPrevious64BitBar
            && bar3.RawValue == 0x00000001
            && bar3.BaseAddressBits == bar2.BaseAddressBits
            && bar3.PairBarIndex == 2
            && !bar3.IsPresent;
    }

    private static bool TestDecodesIoAperture()
    {
        byte[] headerBytes = CreateBaseHeader();
        WriteUInt32(headerBytes, 0x14, 0x0000C001);

        PciBarDecodeResult result = PciBarDecoder.Decode(ParseHeader(headerBytes));
        PciBarDescriptor bar1 = result.Bars.Bar1;

        return result.Succeeded
            && result.IsInitialized
            && bar1.Index == 1
            && bar1.Kind == PciBarKind.Io
            && bar1.Status == PciBarDescriptorStatus.Success
            && bar1.RawValue == 0x0000C001
            && bar1.BaseAddressBits == 0x0000C000
            && bar1.IsIo
            && bar1.IsPresent
            && !bar1.IsMemory
            && !bar1.IsPrefetchable
            && !bar1.ConsumesNextSlot;
    }

    private static bool TestRecordsAbsentZero()
    {
        byte[] headerBytes = CreateBaseHeader();

        PciBarDecodeResult result = PciBarDecoder.Decode(ParseHeader(headerBytes));
        Span<PciBarDescriptor> bars = stackalloc PciBarDescriptor[PciBarDecoder.BarCount];

        return result.Succeeded
            && result.IsInitialized
            && result.Bars.TryCopyTo(bars)
            && IsAbsent(bars[0], 0)
            && IsAbsent(bars[1], 1)
            && IsAbsent(bars[2], 2)
            && IsAbsent(bars[3], 3)
            && IsAbsent(bars[4], 4)
            && IsAbsent(bars[5], 5);
    }

    private static bool TestRejectsReservedMemoryType()
    {
        byte[] headerBytes = CreateBaseHeader();
        WriteUInt32(headerBytes, 0x10, 0xFE000002);
        WriteUInt32(headerBytes, 0x20, 0xFE100006);

        PciBarDecodeResult result = PciBarDecoder.Decode(ParseHeader(headerBytes));

        return !result.Succeeded
            && result.IsInitialized
            && result.Status == PciBarDecodeStatus.ReservedMemoryType
            && result.FailingBarIndex == 0
            && result.Bars.Bar0.Kind == PciBarKind.Malformed
            && result.Bars.Bar0.Status == PciBarDescriptorStatus.ReservedMemoryType
            && result.Bars.Bar0.RawValue == 0xFE000002
            && result.Bars.Bar0.BaseAddressBits == 0xFE000000
            && result.Bars.Bar4.Kind == PciBarKind.Malformed
            && result.Bars.Bar4.Status == PciBarDescriptorStatus.ReservedMemoryType
            && result.Bars.Bar4.RawValue == 0xFE100006
            && result.Bars.Bar4.BaseAddressBits == 0xFE100000;
    }

    private static bool TestRejects64BitAtIndex5()
    {
        byte[] headerBytes = CreateBaseHeader();
        WriteUInt32(headerBytes, 0x24, 0xFE000004);

        PciBarDecodeResult result = PciBarDecoder.Decode(ParseHeader(headerBytes));
        PciBarDescriptor bar5 = result.Bars.Bar5;

        return !result.Succeeded
            && result.IsInitialized
            && result.Status == PciBarDecodeStatus.Memory64AtLastIndex
            && result.FailingBarIndex == 5
            && bar5.Index == 5
            && bar5.Kind == PciBarKind.Malformed
            && bar5.Status == PciBarDescriptorStatus.Memory64AtLastIndex
            && bar5.RawValue == 0xFE000004
            && bar5.BaseAddressBits == 0xFE000000
            && !bar5.ConsumesNextSlot
            && bar5.PairBarIndex == PciBarDecoder.NoBarIndex;
    }

    private static bool TestDefaultResultIsNotSuccess()
    {
        PciBarDecodeResult result = default;

        return !result.IsInitialized
            && !result.Succeeded
            && result.Status == PciBarDecodeStatus.Success
            && result.FailingBarIndex == 0
            && result.Bars.Bar0.Index == 0
            && result.Bars.Bar0.Kind == PciBarKind.Absent
            && result.Bars.Bar0.Status == PciBarDescriptorStatus.Success
            && !result.Bars.Bar0.IsPresent
            && result.Bars.Bar0.BaseAddressBits == 0;
    }

    private static bool IsAbsent(PciBarDescriptor descriptor, byte expectedIndex)
    {
        return descriptor.Index == expectedIndex
            && descriptor.Kind == PciBarKind.Absent
            && descriptor.Status == PciBarDescriptorStatus.Absent
            && descriptor.RawValue == 0
            && descriptor.RawUpperValue == 0
            && descriptor.BaseAddressBits == 0
            && !descriptor.IsPresent
            && !descriptor.IsMemory
            && !descriptor.IsIo
            && !descriptor.IsPrefetchable
            && !descriptor.ConsumesNextSlot
            && descriptor.PairBarIndex == PciBarDecoder.NoBarIndex;
    }

    private static PciType0ConfigurationHeader ParseHeader(byte[] headerBytes)
    {
        PciConfigurationHeaderResult result = PciConfigurationHeader.ParseType0(headerBytes);
        if (!result.Succeeded)
        {
            throw new InvalidOperationException("test header parse failed: " + result.Status);
        }

        return result.Header;
    }

    private static byte[] CreateBaseHeader()
    {
        byte[] headerBytes = new byte[PciConfigurationHeader.Type0HeaderLength];
        EndianPrimitives.WriteUInt16LittleEndian(headerBytes.AsSpan(0x00, sizeof(ushort)), 0x1AF4);
        EndianPrimitives.WriteUInt16LittleEndian(headerBytes.AsSpan(0x02, sizeof(ushort)), 0x1041);
        headerBytes[0x0E] = PciConfigurationHeader.Type0HeaderType;
        return headerBytes;
    }

    private static void WriteUInt32(byte[] destination, int offset, uint value)
    {
        EndianPrimitives.WriteUInt32LittleEndian(destination.AsSpan(offset, sizeof(uint)), value);
    }
}
