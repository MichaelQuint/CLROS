using System.Runtime.CompilerServices;
using OS.Drivers;
using OS.Std.Binary;

internal static class PciConfigurationHeaderTests
{
    [ModuleInitializer]
    internal static void RunAll()
    {
        int failures = 0;

        Run("drivers.pci.header parses identity and class", TestParsesIdentityAndClass, ref failures);
        Run(
            "drivers.pci.header records bars and capability pointer",
            TestRecordsBarsAndCapabilityPointer,
            ref failures);
        Run("drivers.pci.header rejects invalid vendor", TestRejectsInvalidVendor, ref failures);
        Run(
            "drivers.pci.header rejects invalid capability pointer",
            TestRejectsInvalidCapabilityPointer,
            ref failures);
        Run("drivers.pci.header default result is not success", TestDefaultResultIsNotSuccess, ref failures);

        if (failures != 0)
        {
            throw new InvalidOperationException("PCI configuration header host tests failed: " + failures);
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

    private static bool TestParsesIdentityAndClass()
    {
        byte[] headerBytes = CreateBaseHeader();
        headerBytes[0x08] = 0x03;
        headerBytes[0x09] = 0x02;
        headerBytes[0x0A] = 0x00;
        headerBytes[0x0B] = 0x02;
        headerBytes[0x0E] = PciConfigurationHeader.HeaderTypeMultifunctionBit;

        PciConfigurationHeaderResult result = PciConfigurationHeader.ParseType0(headerBytes);

        byte[] unsupportedHeaderBytes = CreateBaseHeader();
        unsupportedHeaderBytes[0x0E] = 0x01;
        PciConfigurationHeaderResult unsupportedHeader =
            PciConfigurationHeader.ParseType0(unsupportedHeaderBytes);
        PciConfigurationHeaderResult tooSmall =
            PciConfigurationHeader.ParseType0(headerBytes.AsSpan(0, PciConfigurationHeader.Type0HeaderLength - 1));

        return result.Succeeded
            && result.IsInitialized
            && result.Status == PciConfigurationHeaderStatus.Success
            && result.Header.VendorId == 0x1AF4
            && result.Header.DeviceId == 0x1041
            && result.Header.RevisionId == 0x03
            && result.Header.ProgrammingInterface == 0x02
            && result.Header.Subclass == 0x00
            && result.Header.ClassCode == 0x02
            && result.Header.HeaderType == PciConfigurationHeader.HeaderTypeMultifunctionBit
            && result.Header.HeaderTypeCode == PciConfigurationHeader.Type0HeaderType
            && result.Header.IsMultifunction
            && !result.Header.HasCapabilitiesList
            && result.Header.CapabilityPointer == 0
            && !unsupportedHeader.Succeeded
            && unsupportedHeader.IsInitialized
            && unsupportedHeader.Status == PciConfigurationHeaderStatus.UnsupportedHeaderType
            && !tooSmall.Succeeded
            && tooSmall.IsInitialized
            && tooSmall.Status == PciConfigurationHeaderStatus.BufferTooSmall;
    }

    private static bool TestRecordsBarsAndCapabilityPointer()
    {
        byte[] headerBytes = CreateBaseHeader();
        EndianPrimitives.WriteUInt16LittleEndian(
            headerBytes.AsSpan(0x06, sizeof(ushort)),
            PciConfigurationHeader.StatusCapabilitiesListBit);
        headerBytes[0x34] = 0x40;

        WriteUInt32(headerBytes, 0x10, 0xFE000000);
        WriteUInt32(headerBytes, 0x14, 0x0000C001);
        WriteUInt32(headerBytes, 0x18, 0xFE100000);
        WriteUInt32(headerBytes, 0x1C, 0xFE200000);
        WriteUInt32(headerBytes, 0x20, 0x00000000);
        WriteUInt32(headerBytes, 0x24, 0x00000000);

        PciConfigurationHeaderResult result = PciConfigurationHeader.ParseType0(headerBytes);

        return result.Succeeded
            && result.IsInitialized
            && result.Header.StatusRegister == PciConfigurationHeader.StatusCapabilitiesListBit
            && result.Header.Bar0 == 0xFE000000
            && result.Header.Bar1 == 0x0000C001
            && result.Header.Bar2 == 0xFE100000
            && result.Header.Bar3 == 0xFE200000
            && result.Header.Bar4 == 0
            && result.Header.Bar5 == 0
            && result.Header.HasCapabilitiesList
            && result.Header.CapabilityPointer == 0x40;
    }

    private static bool TestRejectsInvalidVendor()
    {
        byte[] headerBytes = CreateBaseHeader();
        EndianPrimitives.WriteUInt16LittleEndian(
            headerBytes.AsSpan(0x00, sizeof(ushort)),
            PciConfigurationHeader.InvalidVendorId);

        PciConfigurationHeaderResult result = PciConfigurationHeader.ParseType0(headerBytes);

        return !result.Succeeded
            && result.IsInitialized
            && result.Status == PciConfigurationHeaderStatus.InvalidVendor;
    }

    private static bool TestRejectsInvalidCapabilityPointer()
    {
        byte[] misalignedBytes = CreateBaseHeader();
        EndianPrimitives.WriteUInt16LittleEndian(
            misalignedBytes.AsSpan(0x06, sizeof(ushort)),
            PciConfigurationHeader.StatusCapabilitiesListBit);
        misalignedBytes[0x34] = 0x41;

        byte[] outOfRangeBytes = CreateBaseHeader();
        EndianPrimitives.WriteUInt16LittleEndian(
            outOfRangeBytes.AsSpan(0x06, sizeof(ushort)),
            PciConfigurationHeader.StatusCapabilitiesListBit);
        outOfRangeBytes[0x34] = 0x38;

        PciConfigurationHeaderResult misaligned = PciConfigurationHeader.ParseType0(misalignedBytes);
        PciConfigurationHeaderResult outOfRange = PciConfigurationHeader.ParseType0(outOfRangeBytes);

        return !misaligned.Succeeded
            && misaligned.IsInitialized
            && misaligned.Status == PciConfigurationHeaderStatus.InvalidCapabilityPointerAlignment
            && !outOfRange.Succeeded
            && outOfRange.IsInitialized
            && outOfRange.Status == PciConfigurationHeaderStatus.InvalidCapabilityPointerRange;
    }

    private static bool TestDefaultResultIsNotSuccess()
    {
        PciConfigurationHeaderResult result = default;

        return !result.IsInitialized
            && !result.Succeeded
            && result.Status == PciConfigurationHeaderStatus.Success
            && result.Header.VendorId == 0
            && result.Header.DeviceId == 0
            && result.Header.StatusRegister == 0
            && result.Header.HeaderType == 0
            && !result.Header.HasCapabilitiesList
            && result.Header.CapabilityPointer == 0;
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
