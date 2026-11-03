using OS.Std.Binary;

namespace OS.Boot;

public static class AcpiTableDirectory
{
    public const uint McfgSignature = 0x4746434D;
    public static AcpiTableDirectoryResult TrySelect(ReadOnlySpan<byte> pointerTable, Span<ulong> pointerScratch, ReadOnlySpan<AcpiTableSource> tableSources, uint requestedSignature, Span<AcpiTableSnapshot> destination)
    {
        AcpiSdtParseResult pointerListResult = AcpiSdtParser.TryParsePointerList(pointerTable, pointerScratch);
        if (!pointerListResult.Succeeded)
        {
            return AcpiTableDirectoryResult.Fail(AcpiTableDirectoryStatus.SourcePointerListFailed, requestedSignature: requestedSignature, declaredLength: pointerListResult.DeclaredLength, requiredLength: pointerListResult.RequiredLength, requiredSnapshotCapacity: pointerListResult.RequiredPointerCapacity, pointerListStatus: pointerListResult.Status);
        }

        return TrySelect(pointerScratch.Slice(0, pointerListResult.PointerCount), tableSources, requestedSignature, destination);
    }

    public static AcpiTableDirectoryResult TrySelect(ReadOnlySpan<ulong> tablePointers, ReadOnlySpan<AcpiTableSource> tableSources, uint requestedSignature, Span<AcpiTableSnapshot> destination)
    {
        for (int pointerIndex = 0; pointerIndex < tablePointers.Length; pointerIndex++)
        {
            ulong physicalAddress = tablePointers[pointerIndex];
            AcpiTableDirectoryResult validation = TryReadSnapshot(physicalAddress, pointerIndex, tablePointers.Length, tableSources, out AcpiTableSnapshot snapshot);
            if (!validation.Succeeded)
            {
                return validation;
            }

            if (snapshot.Signature != requestedSignature)
            {
                continue;
            }

            if (destination.Length == 0)
            {
                return AcpiTableDirectoryResult.Fail(AcpiTableDirectoryStatus.DestinationTooSmall, tablePointers.Length, pointerIndex, snapshot.SourceIndex, physicalAddress, requestedSignature, snapshot.Signature, snapshot.Length, snapshot.Length, requiredDestinationCapacity: 1);
            }

            destination[0] = snapshot;
            return AcpiTableDirectoryResult.SuccessSelected(tablePointers.Length, pointerIndex, snapshot.SourceIndex, physicalAddress, requestedSignature, snapshot.Signature, snapshot.Length);
        }

        return AcpiTableDirectoryResult.Fail(AcpiTableDirectoryStatus.SignatureNotFound, tablePointers.Length, requestedSignature: requestedSignature);
    }

    public static AcpiTableDirectoryResult TryBuild(ReadOnlySpan<ulong> tablePointers, ReadOnlySpan<AcpiTableSource> tableSources, Span<AcpiTableSnapshot> destination)
    {
        if (destination.Length < tablePointers.Length)
        {
            return AcpiTableDirectoryResult.Fail(AcpiTableDirectoryStatus.SnapshotCapacityTooSmall, tablePointers.Length, requiredSnapshotCapacity: tablePointers.Length);
        }

        for (int pointerIndex = 0; pointerIndex < tablePointers.Length; pointerIndex++)
        {
            ulong physicalAddress = tablePointers[pointerIndex];
            AcpiTableDirectoryResult validation = TryReadSnapshot(physicalAddress, pointerIndex, tablePointers.Length, tableSources, out AcpiTableSnapshot snapshot);
            if (!validation.Succeeded)
            {
                return validation;
            }

            destination[pointerIndex] = snapshot;
        }

        return AcpiTableDirectoryResult.SuccessSnapshots(tablePointers.Length, tablePointers.Length);
    }

    public static uint CreateSignature(byte first, byte second, byte third, byte fourth)
    {
        return (uint)first | ((uint)second << 8) | ((uint)third << 16) | ((uint)fourth << 24);
    }

    private static AcpiTableDirectoryResult TryReadSnapshot(ulong physicalAddress, int pointerIndex, int pointerCount, ReadOnlySpan<AcpiTableSource> tableSources, out AcpiTableSnapshot snapshot)
    {
        snapshot = default;
        int sourceIndex = FindSourceIndex(tableSources, physicalAddress);
        if (sourceIndex < 0)
        {
            return AcpiTableDirectoryResult.Fail(AcpiTableDirectoryStatus.TableSourceMissing, pointerCount, pointerIndex: pointerIndex, physicalAddress: physicalAddress);
        }

        ReadOnlySpan<byte> table = tableSources[sourceIndex].TableBytes.Span;
        if (table.Length < AcpiSdtParser.HeaderLength)
        {
            return AcpiTableDirectoryResult.Fail(AcpiTableDirectoryStatus.TableSourceTooShort, pointerCount, pointerIndex: pointerIndex, sourceIndex: sourceIndex, physicalAddress: physicalAddress, requiredLength: AcpiSdtParser.HeaderLength);
        }

        uint signature = EndianPrimitives.ReadUInt32LittleEndian(table.Slice(AcpiSdtParser.SignatureOffset, sizeof(uint)));
        uint declaredLength = EndianPrimitives.ReadUInt32LittleEndian(table.Slice(AcpiSdtParser.LengthOffset, sizeof(uint)));
        if (declaredLength < AcpiSdtParser.HeaderLength)
        {
            return AcpiTableDirectoryResult.Fail(AcpiTableDirectoryStatus.TableLengthInvalid, pointerCount, pointerIndex: pointerIndex, sourceIndex: sourceIndex, physicalAddress: physicalAddress, tableSignature: signature, declaredLength: declaredLength, requiredLength: AcpiSdtParser.HeaderLength);
        }

        if (declaredLength > table.Length)
        {
            return AcpiTableDirectoryResult.Fail(AcpiTableDirectoryStatus.TableLengthInvalid, pointerCount, pointerIndex: pointerIndex, sourceIndex: sourceIndex, physicalAddress: physicalAddress, tableSignature: signature, declaredLength: declaredLength, requiredLength: declaredLength);
        }

        ReadOnlySpan<byte> declaredTable = table.Slice(0, (int)declaredLength);
        if (!IsChecksumValid(declaredTable))
        {
            return AcpiTableDirectoryResult.Fail(AcpiTableDirectoryStatus.TableChecksumInvalid, pointerCount, pointerIndex: pointerIndex, sourceIndex: sourceIndex, physicalAddress: physicalAddress, tableSignature: signature, declaredLength: declaredLength, requiredLength: declaredLength);
        }

        snapshot = new AcpiTableSnapshot(physicalAddress, signature, declaredLength, pointerIndex, sourceIndex);
        return AcpiTableDirectoryResult.SuccessSnapshots(pointerCount: 1, snapshotCount: 1);
    }

    private static int FindSourceIndex(ReadOnlySpan<AcpiTableSource> tableSources, ulong physicalAddress)
    {
        for (int index = 0; index < tableSources.Length; index++)
        {
            if (tableSources[index].PhysicalAddress == physicalAddress)
            {
                return index;
            }
        }

        return -1;
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
