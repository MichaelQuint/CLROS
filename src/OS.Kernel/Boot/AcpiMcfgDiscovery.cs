using OS.Drivers;
using OS.Kernel.Platform;
using OS.Std.Binary;

namespace OS.Boot;
public static class AcpiMcfgDiscovery
{
    public static AcpiMcfgPhysicalDiscoveryResult TryDiscoverFromRootSdtSnapshot(AcpiRootSdtSnapshotResult rootSdtSnapshot, ReadOnlySpan<byte> rootSdtTable, IPlatformPhysicalMemory physicalMemory, Span<ulong> pointerScratch, Span<byte> childHeaderScratch, Span<byte> childTableDestination, Span<PciEcamSegment> destination)
    {
        if (!rootSdtSnapshot.Succeeded)
        {
            return AcpiMcfgPhysicalDiscoveryResult.InvalidRootSdtSnapshot(rootSdtSnapshot.Status);
        }

        if (physicalMemory is null)
        {
            return AcpiMcfgPhysicalDiscoveryResult.PhysicalMemoryUnavailable();
        }

        if (childHeaderScratch.Length < AcpiSdtParser.HeaderLength)
        {
            return AcpiMcfgPhysicalDiscoveryResult.HeaderScratchTooSmall(AcpiSdtParser.HeaderLength);
        }

        ReadOnlySpan<byte> rootTable = rootSdtTable;
        if (rootSdtSnapshot.BytesRead > 0 && rootSdtSnapshot.BytesRead <= rootSdtTable.Length)
        {
            rootTable = rootSdtTable.Slice(0, rootSdtSnapshot.BytesRead);
        }

        AcpiSdtParseResult pointerListResult = AcpiSdtParser.TryParsePointerList(rootTable, pointerScratch);
        if (!pointerListResult.Succeeded)
        {
            return AcpiMcfgPhysicalDiscoveryResult.RootPointerListFailed(pointerListResult);
        }

        ReadOnlySpan<ulong> tablePointers = pointerScratch.Slice(0, pointerListResult.PointerCount);
        for (int pointerIndex = 0; pointerIndex < tablePointers.Length; pointerIndex++)
        {
            ulong physicalAddress = tablePointers[pointerIndex];
            PlatformPhysicalMemoryResult headerRead = physicalMemory.ReadBytes(physicalAddress, AcpiSdtParser.HeaderLength, childHeaderScratch);
            if (!headerRead.Succeeded)
            {
                return AcpiMcfgPhysicalDiscoveryResult.ChildHeaderReadFailed(pointerListResult, pointerIndex, physicalAddress, headerRead);
            }

            uint signature = EndianPrimitives.ReadUInt32LittleEndian(childHeaderScratch.Slice(AcpiSdtParser.SignatureOffset, sizeof(uint)));
            uint declaredLength = EndianPrimitives.ReadUInt32LittleEndian(childHeaderScratch.Slice(AcpiSdtParser.LengthOffset, sizeof(uint)));
            if (declaredLength < AcpiSdtParser.HeaderLength || declaredLength > int.MaxValue)
            {
                return AcpiMcfgPhysicalDiscoveryResult.ChildTableLengthInvalid(pointerListResult, pointerIndex, physicalAddress, signature, declaredLength, AcpiSdtParser.HeaderLength);
            }

            if (signature != AcpiTableDirectory.McfgSignature)
            {
                continue;
            }

            int tableLength = (int)declaredLength;
            if (childTableDestination.Length < tableLength)
            {
                return AcpiMcfgPhysicalDiscoveryResult.ChildTableDestinationTooSmall(pointerListResult, pointerIndex, physicalAddress, signature, declaredLength, tableLength);
            }

            PlatformPhysicalMemoryResult fullRead = physicalMemory.ReadBytes(physicalAddress, tableLength, childTableDestination);
            if (!fullRead.Succeeded)
            {
                return AcpiMcfgPhysicalDiscoveryResult.ChildFullReadFailed(pointerListResult, pointerIndex, physicalAddress, signature, declaredLength, fullRead);
            }

            ReadOnlySpan<byte> mcfgTable = childTableDestination.Slice(0, tableLength);
            if (!IsChecksumValid(mcfgTable))
            {
                return AcpiMcfgPhysicalDiscoveryResult.ChildTableChecksumInvalid(pointerListResult, pointerIndex, physicalAddress, signature, declaredLength);
            }

            AcpiMcfgParseResult mcfgParseResult = AcpiMcfgParser.TryParse(mcfgTable, destination);
            if (!mcfgParseResult.Succeeded)
            {
                return AcpiMcfgPhysicalDiscoveryResult.McfgParseFailed(pointerListResult, pointerIndex, physicalAddress, signature, declaredLength, mcfgParseResult);
            }

            return AcpiMcfgPhysicalDiscoveryResult.Success(pointerListResult, pointerIndex, physicalAddress, declaredLength, mcfgParseResult);
        }

        return AcpiMcfgPhysicalDiscoveryResult.SignatureNotFound(pointerListResult);
    }

    public static AcpiMcfgDiscoveryResult TryDiscover(ReadOnlySpan<byte> pointerTable, Span<ulong> pointerScratch, ReadOnlySpan<AcpiTableSource> tableSources, Span<PciEcamSegment> destination)
    {
        Span<AcpiTableSnapshot> selectedTable = stackalloc AcpiTableSnapshot[1];
        AcpiTableDirectoryResult directoryResult = AcpiTableDirectory.TrySelect(pointerTable, pointerScratch, tableSources, AcpiTableDirectory.McfgSignature, selectedTable);
        return ParseSelectedTable(directoryResult, tableSources, destination);
    }

    public static AcpiMcfgDiscoveryResult TryDiscover(ReadOnlySpan<ulong> tablePointers, ReadOnlySpan<AcpiTableSource> tableSources, Span<PciEcamSegment> destination)
    {
        Span<AcpiTableSnapshot> selectedTable = stackalloc AcpiTableSnapshot[1];
        AcpiTableDirectoryResult directoryResult = AcpiTableDirectory.TrySelect(tablePointers, tableSources, AcpiTableDirectory.McfgSignature, selectedTable);
        return ParseSelectedTable(directoryResult, tableSources, destination);
    }

    private static AcpiMcfgDiscoveryResult ParseSelectedTable(AcpiTableDirectoryResult directoryResult, ReadOnlySpan<AcpiTableSource> tableSources, Span<PciEcamSegment> destination)
    {
        if (!directoryResult.Succeeded)
        {
            return AcpiMcfgDiscoveryResult.DirectoryFailed(directoryResult);
        }

        int sourceIndex = directoryResult.SourceIndex;
        if ((uint)sourceIndex >= (uint)tableSources.Length)
        {
            return AcpiMcfgDiscoveryResult.SelectedSourceInvalid(directoryResult);
        }

        AcpiMcfgParseResult mcfgParseResult = AcpiMcfgParser.TryParse(tableSources[sourceIndex].TableBytes.Span, destination);
        if (!mcfgParseResult.Succeeded)
        {
            return AcpiMcfgDiscoveryResult.McfgParseFailed(directoryResult, mcfgParseResult);
        }

        return AcpiMcfgDiscoveryResult.Success(directoryResult, mcfgParseResult);
    }

    private static bool IsChecksumValid(ReadOnlySpan<byte> table)
    {
        uint sum = 0;
        for (int index = 0; index < table.Length; index++)
        {
            sum += table[index];
        }

        return (sum & 0xff) == 0;
    }
}
