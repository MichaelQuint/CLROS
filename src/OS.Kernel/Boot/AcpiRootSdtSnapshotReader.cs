using OS.Kernel.Platform;
using OS.Std.Binary;

namespace OS.Boot;

public static class AcpiRootSdtSnapshotReader
{
    public static AcpiRootSdtSnapshotResult ReadAndValidate(AcpiRsdpSnapshotResult rsdpSnapshot, IPlatformPhysicalMemory physicalMemory, Span<byte> headerScratch, Span<byte> tableDestination)
    {
        if (!rsdpSnapshot.Succeeded)
        {
            return AcpiRootSdtSnapshotResult.InvalidRsdpSnapshot(rsdpSnapshot.Status);
        }

        if (physicalMemory is null)
        {
            return AcpiRootSdtSnapshotResult.PhysicalMemoryUnavailable();
        }

        AcpiSdtTableKind tableKind = SelectRootTableKind(rsdpSnapshot, out ulong physicalAddress, out bool usedRsdtFallback);
        if (physicalAddress == 0)
        {
            return AcpiRootSdtSnapshotResult.NoUsableRootTableAddress(rsdpSnapshot.Status);
        }

        if (headerScratch.Length < AcpiSdtParser.HeaderLength)
        {
            return AcpiRootSdtSnapshotResult.ScratchTooSmall(AcpiSdtParser.HeaderLength, tableKind, physicalAddress, usedRsdtFallback);
        }

        PlatformPhysicalMemoryResult headerRead = physicalMemory.ReadBytes(physicalAddress, AcpiSdtParser.HeaderLength, headerScratch);
        if (!headerRead.Succeeded)
        {
            return AcpiRootSdtSnapshotResult.PhysicalReadFailed(AcpiRootSdtSnapshotStatus.HeaderReadFailed, tableKind, physicalAddress, headerRead, usedRsdtFallback);
        }

        uint signature = EndianPrimitives.ReadUInt32LittleEndian(headerScratch.Slice(AcpiSdtParser.SignatureOffset, sizeof(uint)));
        uint expectedSignature = tableKind == AcpiSdtTableKind.Xsdt ? AcpiSdtParser.XsdtSignature : AcpiSdtParser.RsdtSignature;
        if (signature != expectedSignature)
        {
            return AcpiRootSdtSnapshotResult.InvalidTable(AcpiRootSdtSnapshotStatus.InvalidSignature, tableKind, physicalAddress, AcpiSdtParser.HeaderLength, declaredLength: 0, usedRsdtFallback, signature);
        }

        uint declaredLength = EndianPrimitives.ReadUInt32LittleEndian(headerScratch.Slice(AcpiSdtParser.LengthOffset, sizeof(uint)));
        if (declaredLength < AcpiSdtParser.HeaderLength || declaredLength > int.MaxValue)
        {
            return AcpiRootSdtSnapshotResult.InvalidTable(AcpiRootSdtSnapshotStatus.InvalidTableLength, tableKind, physicalAddress, AcpiSdtParser.HeaderLength, declaredLength, usedRsdtFallback, signature);
        }

        int tableLength = (int)declaredLength;
        if (tableDestination.Length < tableLength)
        {
            return AcpiRootSdtSnapshotResult.DestinationTooSmall(tableKind, physicalAddress, tableLength, declaredLength, usedRsdtFallback);
        }

        PlatformPhysicalMemoryResult tableRead = physicalMemory.ReadBytes(physicalAddress, tableLength, tableDestination);
        if (!tableRead.Succeeded)
        {
            return AcpiRootSdtSnapshotResult.PhysicalReadFailed(AcpiRootSdtSnapshotStatus.FullReadFailed, tableKind, physicalAddress, tableRead, usedRsdtFallback);
        }

        ReadOnlySpan<byte> table = tableDestination.Slice(0, tableLength);
        if (!IsChecksumValid(table))
        {
            return AcpiRootSdtSnapshotResult.InvalidTable(AcpiRootSdtSnapshotStatus.InvalidChecksum, tableKind, physicalAddress, tableLength, declaredLength, usedRsdtFallback, signature);
        }

        int entryBytes = tableLength - AcpiSdtParser.HeaderLength;
        int pointerSize = tableKind == AcpiSdtTableKind.Xsdt ? AcpiSdtParser.XsdtPointerSize : AcpiSdtParser.RsdtPointerSize;
        if ((entryBytes % pointerSize) != 0)
        {
            return AcpiRootSdtSnapshotResult.InvalidTable(AcpiRootSdtSnapshotStatus.InvalidPointerArrayLength, tableKind, physicalAddress, tableLength + (pointerSize - (entryBytes % pointerSize)), declaredLength, usedRsdtFallback, signature);
        }

        return AcpiRootSdtSnapshotResult.Success(tableKind, physicalAddress, tableLength, declaredLength, entryBytes / pointerSize, usedRsdtFallback);
    }

    private static AcpiSdtTableKind SelectRootTableKind(AcpiRsdpSnapshotResult rsdpSnapshot, out ulong physicalAddress, out bool usedRsdtFallback)
    {
        if (rsdpSnapshot.RevisionKind == AcpiRsdpRevisionKind.Acpi20OrGreater && rsdpSnapshot.XsdtAddress != 0)
        {
            physicalAddress = rsdpSnapshot.XsdtAddress;
            usedRsdtFallback = false;
            return AcpiSdtTableKind.Xsdt;
        }

        physicalAddress = rsdpSnapshot.RsdtAddress;
        usedRsdtFallback = rsdpSnapshot.RevisionKind == AcpiRsdpRevisionKind.Acpi20OrGreater && rsdpSnapshot.XsdtAddress == 0 && rsdpSnapshot.RsdtAddress != 0;
        return AcpiSdtTableKind.Rsdt;
    }

    private static bool IsChecksumValid(ReadOnlySpan<byte> source)
    {
        uint sum = 0;
        for (int index = 0; index < source.Length; index++)
        {
            sum += source[index];
        }

        return (sum & 0xff) == 0;
    }
}
