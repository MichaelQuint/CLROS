using System.Runtime.CompilerServices;
using OS.Storage;

internal static class GptPartitionSelectorTests
{
    private static readonly Guid DiskGuid = new("11111111-2222-3333-4444-555555555555");
    private static readonly Guid EfiSystemPartitionGuid = new("c12a7328-f81f-11d2-ba4b-00a0c93ec93b");
    private static readonly Guid BasicDataPartitionGuid = new("ebd0a0a2-b9e5-4433-87c0-68b6b72699c7");
    private static readonly Guid FirstUniqueGuid = new("aaaaaaaa-bbbb-cccc-dddd-eeeeeeeeeeee");
    private static readonly Guid SecondUniqueGuid = new("bbbbbbbb-cccc-dddd-eeee-ffffffffffff");
    private static readonly Guid ThirdUniqueGuid = new("cccccccc-dddd-eeee-ffff-111111111111");

    [HostTest("storage.gpt selector selects type guid", HostLane.Fast, 0)]
    private static bool TestSelectsTypeGuid()
    {
        GptPartitionDescriptor[] descriptors = CreateDescriptors();
        GptPartitionTableParseResult parse = CreateSuccessfulParse(descriptors.Length);

        GptPartitionSelectorStatus status = GptPartitionSelector.SelectByPartitionTypeGuid(
            parse,
            descriptors,
            EfiSystemPartitionGuid,
            out GptPartitionSelectorResult result);

        return HostCheck.Result(status == GptPartitionSelectorStatus.Success
            && result.Succeeded
            && result.SelectorKind == GptPartitionSelectorKind.PartitionTypeGuid
            && result.ParseStatus == GptPartitionTableStatus.Success
            && result.DescriptorSpanLength == descriptors.Length
            && result.PartitionsScanned == descriptors.Length
            && result.SelectedDescriptorIndex == 1
            && result.SelectedEntryIndex == 2
            && result.SelectedEntryByteOffset == 256
            && result.FirstLba == 60
            && result.LastLba == 80
            && result.BlockCount == 21
            && result.NameByteOffset == 256 + GptPartitionDescriptor.NameOffsetWithinEntryBytes
            && result.NameByteLength == GptPartitionDescriptor.NameFieldLengthBytes
            && result.NameUtf16CodeUnitLength == 3
            && result.MatchCount == 1
            && result.DuplicateCount == 0
            && result.RequestedGuid == EfiSystemPartitionGuid
            && GptPartitionSelector.UsesCallerOwnedDescriptors
            && !GptPartitionSelector.ReparsesGptBytes
            && !GptPartitionSelector.AllocatesManagedStorage
            && !GptPartitionSelector.ReadsBlockDevice
            && !GptPartitionSelector.LoadsPackage);
    }

    [HostTest("storage.gpt selector selects unique guid", HostLane.Fast, 1)]
    private static bool TestSelectsUniqueGuid()
    {
        GptPartitionDescriptor[] descriptors = CreateDescriptors();
        GptPartitionTableParseResult parse = CreateSuccessfulParse(descriptors.Length);

        GptPartitionSelectorStatus status = GptPartitionSelector.SelectByUniquePartitionGuid(
            parse,
            descriptors,
            FirstUniqueGuid,
            out GptPartitionSelectorResult result);

        return HostCheck.Result(status == GptPartitionSelectorStatus.Success
            && result.Succeeded
            && result.SelectorKind == GptPartitionSelectorKind.UniquePartitionGuid
            && result.SelectedDescriptorIndex == 0
            && result.SelectedEntryIndex == 0
            && result.FirstLba == 40
            && result.LastLba == 50
            && result.BlockCount == 11
            && result.NameByteOffset == GptPartitionDescriptor.NameOffsetWithinEntryBytes
            && result.MatchCount == 1
            && result.DuplicateCount == 0
            && result.RequestedGuid == FirstUniqueGuid);
    }

    [HostTest("storage.gpt selector reports missing partition", HostLane.Fast, 2)]
    private static bool TestReportsMissingPartition()
    {
        GptPartitionDescriptor[] descriptors = CreateDescriptors();
        GptPartitionTableParseResult parse = CreateSuccessfulParse(descriptors.Length);
        Guid missingGuid = new("dddddddd-eeee-ffff-1111-222222222222");

        GptPartitionSelectorStatus status = GptPartitionSelector.SelectByPartitionTypeGuid(
            parse,
            descriptors,
            missingGuid,
            out GptPartitionSelectorResult result);

        GptPartitionSelectorStatus missingGuidStatus =
            GptPartitionSelector.SelectByUniquePartitionGuid(
                parse,
                descriptors,
                Guid.Empty,
                out GptPartitionSelectorResult missingGuidResult);

        return HostCheck.Result(status == GptPartitionSelectorStatus.NotFound
            && result.IsInitialized
            && !result.Succeeded
            && result.SelectorKind == GptPartitionSelectorKind.PartitionTypeGuid
            && result.PartitionsScanned == descriptors.Length
            && result.MatchCount == 0
            && result.DuplicateCount == 0
            && result.SelectedDescriptorIndex == -1
            && result.RequestedGuid == missingGuid
            && missingGuidStatus == GptPartitionSelectorStatus.MissingGuid
            && !missingGuidResult.Succeeded
            && missingGuidResult.RequestedGuid == Guid.Empty);
    }

    [HostTest("storage.gpt selector reports duplicate type metadata", HostLane.Fast, 3)]
    private static bool TestReportsDuplicateTypeMetadata()
    {
        GptPartitionDescriptor[] descriptors = CreateDescriptors();
        descriptors[2] = CreateDescriptor(
            entryIndex: 3,
            entryByteOffset: 384,
            EfiSystemPartitionGuid,
            ThirdUniqueGuid,
            firstLba: 81,
            lastLba: 90,
            nameCodeUnits: 5);
        GptPartitionTableParseResult parse = CreateSuccessfulParse(descriptors.Length);

        GptPartitionSelectorStatus status = GptPartitionSelector.SelectByPartitionTypeGuid(
            parse,
            descriptors,
            EfiSystemPartitionGuid,
            out GptPartitionSelectorResult result);

        return HostCheck.Result(status == GptPartitionSelectorStatus.Success
            && result.Succeeded
            && result.SelectedDescriptorIndex == 1
            && result.SelectedEntryIndex == 2
            && result.MatchCount == 2
            && result.DuplicateCount == 1
            && result.FirstLba == 60
            && result.LastLba == 80);
    }

    [HostTest("storage.gpt selector rejects failed default parse result", HostLane.Fast, 4)]
    private static bool TestRejectsFailedDefaultParseResult()
    {
        GptPartitionDescriptor[] descriptors = CreateDescriptors();

        GptPartitionSelectorStatus defaultStatus = GptPartitionSelector.SelectByPartitionTypeGuid(
            default,
            descriptors,
            EfiSystemPartitionGuid,
            out GptPartitionSelectorResult defaultResult);

        GptPartitionTableParseResult failedParse =
            GptPartitionTableParseResult.Fail(GptPartitionTableStatus.HeaderCrcMismatch);
        GptPartitionSelectorStatus failedStatus = GptPartitionSelector.SelectByPartitionTypeGuid(
            failedParse,
            descriptors,
            EfiSystemPartitionGuid,
            out GptPartitionSelectorResult failedResult);

        GptPartitionTableParseResult mismatchParse = CreateSuccessfulParse(descriptors.Length + 1);
        GptPartitionSelectorStatus mismatchStatus = GptPartitionSelector.SelectByPartitionTypeGuid(
            mismatchParse,
            descriptors,
            EfiSystemPartitionGuid,
            out GptPartitionSelectorResult mismatchResult);

        GptPartitionTableParseResult emptyParse = CreateSuccessfulParse(partitionsWritten: 0);
        GptPartitionSelectorStatus emptyStatus = GptPartitionSelector.SelectByPartitionTypeGuid(
            emptyParse,
            ReadOnlySpan<GptPartitionDescriptor>.Empty,
            EfiSystemPartitionGuid,
            out GptPartitionSelectorResult emptyResult);

        GptPartitionSelectorResult zeroResult = default;

        return HostCheck.Result(defaultStatus == GptPartitionSelectorStatus.ParseResultNotInitialized
            && defaultResult.IsInitialized
            && !defaultResult.Succeeded
            && defaultResult.ParseStatus == GptPartitionTableStatus.Success
            && failedStatus == GptPartitionSelectorStatus.ParseFailed
            && failedResult.ParseStatus == GptPartitionTableStatus.HeaderCrcMismatch
            && mismatchStatus == GptPartitionSelectorStatus.DescriptorCountMismatch
            && mismatchResult.DescriptorSpanLength == descriptors.Length
            && emptyStatus == GptPartitionSelectorStatus.DescriptorSpanEmpty
            && emptyResult.PartitionsScanned == 0
            && !zeroResult.IsInitialized
            && !zeroResult.Succeeded
            && zeroResult.Status == GptPartitionSelectorStatus.Success);
    }

    [HostTest("storage.gpt selector rejects invalid descriptor metadata", HostLane.Fast, 5)]
    private static bool TestRejectsInvalidDescriptorMetadata()
    {
        GptPartitionDescriptor[] descriptors = CreateDescriptors();
        descriptors[1] = CreateDescriptor(
            entryIndex: 2,
            entryByteOffset: 256,
            EfiSystemPartitionGuid,
            SecondUniqueGuid,
            firstLba: 90,
            lastLba: 80,
            nameCodeUnits: 3);
        GptPartitionTableParseResult parse = CreateSuccessfulParse(descriptors.Length);

        GptPartitionSelectorStatus status = GptPartitionSelector.SelectByPartitionTypeGuid(
            parse,
            descriptors,
            EfiSystemPartitionGuid,
            out GptPartitionSelectorResult result);

        return HostCheck.Result(status == GptPartitionSelectorStatus.InvalidDescriptorMetadata
            && result.IsInitialized
            && !result.Succeeded
            && result.FailingDescriptorIndex == 1
            && result.PartitionsScanned == 2
            && result.MatchCount == 0
            && result.ParseStatus == GptPartitionTableStatus.Success);
    }

    [HostTest("storage.gpt selector result layouts contain no managed references", HostLane.Fast, 6)]
    private static bool TestResultLayoutsContainNoManagedReferences()
    {
        return HostCheck.Result(!RuntimeHelpers.IsReferenceOrContainsReferences<GptPartitionSelectorResult>()
            && !RuntimeHelpers.IsReferenceOrContainsReferences<GptPartitionDescriptor>()
            && !RuntimeHelpers.IsReferenceOrContainsReferences<GptPartitionTableParseResult>());
    }

    private static GptPartitionDescriptor[] CreateDescriptors()
    {
        return
        [
            CreateDescriptor(
                entryIndex: 0,
                entryByteOffset: 0,
                BasicDataPartitionGuid,
                FirstUniqueGuid,
                firstLba: 40,
                lastLba: 50,
                nameCodeUnits: 4),
            CreateDescriptor(
                entryIndex: 2,
                entryByteOffset: 256,
                EfiSystemPartitionGuid,
                SecondUniqueGuid,
                firstLba: 60,
                lastLba: 80,
                nameCodeUnits: 3),
            CreateDescriptor(
                entryIndex: 3,
                entryByteOffset: 384,
                BasicDataPartitionGuid,
                ThirdUniqueGuid,
                firstLba: 81,
                lastLba: 90,
                nameCodeUnits: 5)
        ];
    }

    private static GptPartitionDescriptor CreateDescriptor(
        uint entryIndex,
        int entryByteOffset,
        Guid partitionTypeGuid,
        Guid uniquePartitionGuid,
        ulong firstLba,
        ulong lastLba,
        int nameCodeUnits)
    {
        return new GptPartitionDescriptor(
            entryIndex,
            entryByteOffset,
            partitionTypeGuid,
            uniquePartitionGuid,
            firstLba,
            lastLba,
            attributes: 0,
            entryByteOffset + GptPartitionDescriptor.NameOffsetWithinEntryBytes,
            nameCodeUnits,
            nameIsNullTerminated: true);
    }

    private static GptPartitionTableParseResult CreateSuccessfulParse(int partitionsWritten)
    {
        GptHeader header = new GptHeader(
            GptPartitionTableParser.SupportedRevision,
            GptPartitionTableParser.MinimumHeaderSize,
            GptPartitionTableParser.PrimaryHeaderLba,
            backupLba: 127,
            firstUsableLba: 34,
            lastUsableLba: 94,
            DiskGuid,
            partitionEntryLba: 2,
            partitionEntryCount: 4,
            partitionEntrySize: GptPartitionTableParser.MinimumPartitionEntrySize,
            partitionEntryArrayCrc32: 0x1010_2020);

        return GptPartitionTableParseResult.Success(
            header,
            partitionsWritten,
            emptyEntriesSkipped: 0,
            partitionEntryBytesRequired: 512,
            partitionEntrySectorsRequired: 1,
            expectedHeaderCrc32: 0x1111_2222,
            computedHeaderCrc32: 0x1111_2222,
            expectedPartitionEntryArrayCrc32: 0x3333_4444,
            computedPartitionEntryArrayCrc32: 0x3333_4444);
    }
}
