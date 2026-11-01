namespace OS.Storage;

public static class GptPartitionSelector
{
    public const bool UsesCallerOwnedDescriptors = true;
    public const bool ReparsesGptBytes = false;
    public const bool AllocatesManagedStorage = false;
    public const bool ReadsBlockDevice = false;
    public const bool LoadsPackage = false;

    public static GptPartitionSelectorStatus SelectByPartitionTypeGuid(
        GptPartitionTableParseResult parse,
        ReadOnlySpan<GptPartitionDescriptor> descriptors,
        Guid partitionTypeGuid,
        out GptPartitionSelectorResult result)
    {
        return Select(
            parse,
            descriptors,
            partitionTypeGuid,
            GptPartitionSelectorKind.PartitionTypeGuid,
            out result);
    }

    public static GptPartitionSelectorStatus SelectByUniquePartitionGuid(
        GptPartitionTableParseResult parse,
        ReadOnlySpan<GptPartitionDescriptor> descriptors,
        Guid uniquePartitionGuid,
        out GptPartitionSelectorResult result)
    {
        return Select(
            parse,
            descriptors,
            uniquePartitionGuid,
            GptPartitionSelectorKind.UniquePartitionGuid,
            out result);
    }

    private static GptPartitionSelectorStatus Select(
        GptPartitionTableParseResult parse,
        ReadOnlySpan<GptPartitionDescriptor> descriptors,
        Guid requestedGuid,
        GptPartitionSelectorKind selectorKind,
        out GptPartitionSelectorResult result)
    {
        result = default;

        if (!parse.IsInitialized)
        {
            result = GptPartitionSelectorResult.Fail(
                GptPartitionSelectorStatus.ParseResultNotInitialized,
                selectorKind,
                parse.Status,
                descriptors.Length,
                partitionsScanned: 0,
                requestedGuid);
            return result.Status;
        }

        if (!parse.Succeeded)
        {
            result = GptPartitionSelectorResult.Fail(
                GptPartitionSelectorStatus.ParseFailed,
                selectorKind,
                parse.Status,
                descriptors.Length,
                partitionsScanned: 0,
                requestedGuid);
            return result.Status;
        }

        if (requestedGuid == Guid.Empty)
        {
            result = GptPartitionSelectorResult.Fail(
                GptPartitionSelectorStatus.MissingGuid,
                selectorKind,
                parse.Status,
                descriptors.Length,
                partitionsScanned: 0,
                requestedGuid);
            return result.Status;
        }

        if (parse.PartitionsWritten <= 0 || descriptors.Length == 0)
        {
            result = GptPartitionSelectorResult.Fail(
                GptPartitionSelectorStatus.DescriptorSpanEmpty,
                selectorKind,
                parse.Status,
                descriptors.Length,
                partitionsScanned: 0,
                requestedGuid);
            return result.Status;
        }

        if (parse.PartitionsWritten > descriptors.Length)
        {
            result = GptPartitionSelectorResult.Fail(
                GptPartitionSelectorStatus.DescriptorCountMismatch,
                selectorKind,
                parse.Status,
                descriptors.Length,
                partitionsScanned: 0,
                requestedGuid);
            return result.Status;
        }

        int selectedIndex = -1;
        int matchCount = 0;
        GptPartitionDescriptor selected = default;
        for (int index = 0; index < parse.PartitionsWritten; index++)
        {
            GptPartitionDescriptor descriptor = descriptors[index];
            if (!IsDescriptorMetadataValid(parse.Header, descriptor))
            {
                result = GptPartitionSelectorResult.Fail(
                    GptPartitionSelectorStatus.InvalidDescriptorMetadata,
                    selectorKind,
                    parse.Status,
                    descriptors.Length,
                    index + 1,
                    requestedGuid,
                    matchCount,
                    index);
                return result.Status;
            }

            if (!Matches(descriptor, requestedGuid, selectorKind))
            {
                continue;
            }

            matchCount++;
            if (selectedIndex < 0)
            {
                selectedIndex = index;
                selected = descriptor;
            }
        }

        if (selectedIndex < 0)
        {
            result = GptPartitionSelectorResult.Fail(
                GptPartitionSelectorStatus.NotFound,
                selectorKind,
                parse.Status,
                descriptors.Length,
                parse.PartitionsWritten,
                requestedGuid);
            return result.Status;
        }

        result = GptPartitionSelectorResult.Success(
            selectorKind,
            parse.Status,
            selected,
            descriptors.Length,
            parse.PartitionsWritten,
            selectedIndex,
            matchCount,
            requestedGuid);
        return result.Status;
    }

    private static bool Matches(
        GptPartitionDescriptor descriptor,
        Guid requestedGuid,
        GptPartitionSelectorKind selectorKind)
    {
        return selectorKind == GptPartitionSelectorKind.PartitionTypeGuid
            ? descriptor.PartitionTypeGuid == requestedGuid
            : descriptor.UniquePartitionGuid == requestedGuid;
    }

    private static bool IsDescriptorMetadataValid(
        GptHeader header,
        GptPartitionDescriptor descriptor)
    {
        if (descriptor.EntryIndex >= header.PartitionEntryCount ||
            descriptor.EntryByteOffset < 0 ||
            descriptor.FirstLba < header.FirstUsableLba ||
            descriptor.LastLba > header.LastUsableLba ||
            descriptor.FirstLba > descriptor.LastLba ||
            descriptor.PartitionTypeGuid == Guid.Empty ||
            descriptor.UniquePartitionGuid == Guid.Empty)
        {
            return false;
        }

        int expectedNameOffset =
            descriptor.EntryByteOffset + GptPartitionDescriptor.NameOffsetWithinEntryBytes;
        return descriptor.NameByteOffset == expectedNameOffset &&
            descriptor.NameUtf16CodeUnitLength >= 0 &&
            descriptor.NameUtf16CodeUnitLength <= GptPartitionDescriptor.MaximumNameUtf16CodeUnits &&
            descriptor.NameByteLength == GptPartitionDescriptor.NameFieldLengthBytes;
    }
}
