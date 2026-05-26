using System.Runtime.CompilerServices;
using OS.Manifest;
using OS.Storage;

internal static class NativeAppSelectionBootScratchPlanTests
{
    [ModuleInitializer]
    internal static void RunAll()
    {
        int failures = 0;

        Run("appmodel.selectionscratch accepts default image buffers", TestAcceptsDefaultImageBuffers, ref failures);
        Run("appmodel.selectionscratch rejects short manifest buffer", TestRejectsShortManifestBuffer, ref failures);
        Run("appmodel.selectionscratch rejects short package buffer", TestRejectsShortPackageBuffer, ref failures);
        Run("appmodel.selectionscratch rejects short gpt entry buffer", TestRejectsShortGptEntryBuffer, ref failures);
        Run("appmodel.selectionscratch rejects invalid requested sizes", TestRejectsInvalidRequestedSizes, ref failures);
        Run("appmodel.selectionscratch rejects unaligned sector size", TestRejectsUnalignedSectorSize, ref failures);
        Run("appmodel.selectionscratch default result is not success", TestDefaultResultIsNotSuccess, ref failures);
        Run("appmodel.selectionscratch preserves no-live-storage boundary", TestPreservesNoLiveStorageBoundary, ref failures);

        if (failures != 0)
        {
            throw new InvalidOperationException("Native app selection boot scratch plan host tests failed: " + failures);
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

    private static bool TestAcceptsDefaultImageBuffers()
    {
        ScratchBuffers scratch = ScratchBuffers.CreateDefault();

        NativeAppSelectionBootScratchPlanResult result = ValidateDefault(scratch);

        return result.Succeeded
            && result.IsInitialized
            && result.Status == NativeAppSelectionBootScratchPlanStatus.Success
            && result.SectorSizeBytes == NativeAppSelectionBootScratchPlan.DefaultSectorSizeBytes
            && result.PartitionEntryCount == NativeAppSelectionBootScratchPlan.DefaultPartitionEntryCount
            && result.PartitionEntrySizeBytes == NativeAppSelectionBootScratchPlan.DefaultPartitionEntrySizeBytes
            && result.PartitionDescriptorCountRequired == NativeAppSelectionBootScratchPlan.DefaultPartitionDescriptorCount
            && result.GptHeaderSectorBytesRequired == NativeAppSelectionBootScratchPlan.DefaultSectorSizeBytes
            && result.GptHeaderSectorBytesAvailable == NativeAppSelectionBootScratchPlan.DefaultSectorSizeBytes
            && result.GptPartitionEntryBytesRequired ==
                NativeAppSelectionBootScratchPlan.DefaultPartitionEntryCount *
                NativeAppSelectionBootScratchPlan.DefaultPartitionEntrySizeBytes
            && result.GptPartitionEntrySectorsRequired ==
                NativeAppSelectionBootScratchPlan.DefaultPartitionEntrySectorsRequired
            && result.GptPartitionEntrySectorBytesRequired ==
                NativeAppSelectionBootScratchPlan.DefaultPartitionEntrySectorsRequired *
                NativeAppSelectionBootScratchPlan.DefaultSectorSizeBytes
            && result.GptPartitionEntrySectorBytesAvailable ==
                NativeAppSelectionBootScratchPlan.DefaultPartitionEntrySectorsRequired *
                NativeAppSelectionBootScratchPlan.DefaultSectorSizeBytes
            && result.GptPartitionDescriptorCountAvailable == NativeAppSelectionBootScratchPlan.DefaultPartitionDescriptorCount
            && result.ManifestBytesRequired == NativeAppSelectionBootScratchPlan.DefaultManifestBufferBytes
            && result.ManifestBytesAvailable == NativeAppSelectionBootScratchPlan.DefaultManifestBufferBytes
            && result.PackageBytesRequired == NativeAppSelectionBootScratchPlan.DefaultPackageBufferBytes
            && result.PackageBytesAvailable == NativeAppSelectionBootScratchPlan.DefaultPackageBufferBytes;
    }

    private static bool TestRejectsShortManifestBuffer()
    {
        ScratchBuffers scratch = ScratchBuffers.Create(
            manifestBufferBytes: checked((int)NativeAppSelectionBootScratchPlan.DefaultManifestBufferBytes - 1));

        NativeAppSelectionBootScratchPlanResult result = ValidateDefault(scratch);

        return !result.Succeeded
            && result.IsInitialized
            && result.Status == NativeAppSelectionBootScratchPlanStatus.ManifestBufferTooSmall
            && result.ManifestBytesRequired == NativeAppSelectionBootScratchPlan.DefaultManifestBufferBytes
            && result.ManifestBytesAvailable == NativeAppSelectionBootScratchPlan.DefaultManifestBufferBytes - 1
            && result.PackageBytesAvailable == NativeAppSelectionBootScratchPlan.DefaultPackageBufferBytes;
    }

    private static bool TestRejectsShortPackageBuffer()
    {
        ScratchBuffers scratch = ScratchBuffers.Create(
            packageBufferBytes: checked((int)NativeAppSelectionBootScratchPlan.DefaultPackageBufferBytes - 1));

        NativeAppSelectionBootScratchPlanResult result = ValidateDefault(scratch);

        return !result.Succeeded
            && result.IsInitialized
            && result.Status == NativeAppSelectionBootScratchPlanStatus.PackageBufferTooSmall
            && result.PackageBytesRequired == NativeAppSelectionBootScratchPlan.DefaultPackageBufferBytes
            && result.PackageBytesAvailable == NativeAppSelectionBootScratchPlan.DefaultPackageBufferBytes - 1
            && result.ManifestBytesAvailable == NativeAppSelectionBootScratchPlan.DefaultManifestBufferBytes;
    }

    private static bool TestRejectsShortGptEntryBuffer()
    {
        ScratchBuffers scratch = ScratchBuffers.Create(
            partitionEntrySectorBytes:
                checked((int)(NativeAppSelectionBootScratchPlan.DefaultPartitionEntrySectorsRequired *
                NativeAppSelectionBootScratchPlan.DefaultSectorSizeBytes) - 1));

        NativeAppSelectionBootScratchPlanResult result = ValidateDefault(scratch);

        return !result.Succeeded
            && result.IsInitialized
            && result.Status == NativeAppSelectionBootScratchPlanStatus.GptPartitionEntrySectorBufferTooSmall
            && result.GptPartitionEntryBytesRequired ==
                NativeAppSelectionBootScratchPlan.DefaultPartitionEntryCount *
                NativeAppSelectionBootScratchPlan.DefaultPartitionEntrySizeBytes
            && result.GptPartitionEntrySectorsRequired ==
                NativeAppSelectionBootScratchPlan.DefaultPartitionEntrySectorsRequired
            && result.GptPartitionEntrySectorBytesRequired ==
                NativeAppSelectionBootScratchPlan.DefaultPartitionEntrySectorsRequired *
                NativeAppSelectionBootScratchPlan.DefaultSectorSizeBytes
            && result.GptPartitionEntrySectorBytesAvailable ==
                NativeAppSelectionBootScratchPlan.DefaultPartitionEntrySectorsRequired *
                NativeAppSelectionBootScratchPlan.DefaultSectorSizeBytes - 1;
    }

    private static bool TestRejectsInvalidRequestedSizes()
    {
        ScratchBuffers scratch = ScratchBuffers.CreateDefault();

        NativeAppSelectionBootScratchPlanResult sector = NativeAppSelectionBootScratchPlan.Validate(
            scratch.PrimaryHeaderSector,
            scratch.PartitionEntrySectors,
            scratch.Partitions,
            scratch.ManifestBuffer,
            scratch.PackageBuffer,
            sectorSizeBytes: 0,
            partitionEntryCount: NativeAppSelectionBootScratchPlan.DefaultPartitionEntryCount,
            partitionEntrySizeBytes: NativeAppSelectionBootScratchPlan.DefaultPartitionEntrySizeBytes,
            partitionDescriptorCount: NativeAppSelectionBootScratchPlan.DefaultPartitionDescriptorCount,
            manifestBufferBytesRequired: NativeAppSelectionBootScratchPlan.DefaultManifestBufferBytes,
            packageBufferBytesRequired: NativeAppSelectionBootScratchPlan.DefaultPackageBufferBytes);

        NativeAppSelectionBootScratchPlanResult descriptor = NativeAppSelectionBootScratchPlan.Validate(
            scratch.PrimaryHeaderSector,
            scratch.PartitionEntrySectors,
            scratch.Partitions,
            scratch.ManifestBuffer,
            scratch.PackageBuffer,
            sectorSizeBytes: NativeAppSelectionBootScratchPlan.DefaultSectorSizeBytes,
            partitionEntryCount: NativeAppSelectionBootScratchPlan.DefaultPartitionEntryCount,
            partitionEntrySizeBytes: NativeAppSelectionBootScratchPlan.DefaultPartitionEntrySizeBytes,
            partitionDescriptorCount: 0,
            manifestBufferBytesRequired: NativeAppSelectionBootScratchPlan.DefaultManifestBufferBytes,
            packageBufferBytesRequired: NativeAppSelectionBootScratchPlan.DefaultPackageBufferBytes);

        return !sector.Succeeded
            && sector.IsInitialized
            && sector.Status == NativeAppSelectionBootScratchPlanStatus.InvalidSectorSize
            && !descriptor.Succeeded
            && descriptor.IsInitialized
            && descriptor.Status == NativeAppSelectionBootScratchPlanStatus.InvalidPartitionDescriptorCount;
    }

    private static bool TestRejectsUnalignedSectorSize()
    {
        ScratchBuffers scratch = ScratchBuffers.CreateDefault();
        uint sectorSize = NativeAppSelectionBootScratchPlan.DefaultSectorSizeBytes + 1;

        NativeAppSelectionBootScratchPlanResult result = NativeAppSelectionBootScratchPlan.Validate(
            scratch.PrimaryHeaderSector,
            scratch.PartitionEntrySectors,
            scratch.Partitions,
            scratch.ManifestBuffer,
            scratch.PackageBuffer,
            sectorSize,
            NativeAppSelectionBootScratchPlan.DefaultPartitionEntryCount,
            NativeAppSelectionBootScratchPlan.DefaultPartitionEntrySizeBytes,
            NativeAppSelectionBootScratchPlan.DefaultPartitionDescriptorCount,
            NativeAppSelectionBootScratchPlan.DefaultManifestBufferBytes,
            NativeAppSelectionBootScratchPlan.DefaultPackageBufferBytes);

        return !result.Succeeded
            && result.IsInitialized
            && result.Status == NativeAppSelectionBootScratchPlanStatus.InvalidSectorSize
            && result.SectorSizeBytes == sectorSize
            && result.GptHeaderSectorBytesRequired == sectorSize
            && result.GptHeaderSectorBytesAvailable == NativeAppSelectionBootScratchPlan.DefaultSectorSizeBytes
            && result.GptPartitionEntryBytesRequired == 0
            && result.GptPartitionEntrySectorsRequired == 0
            && result.GptPartitionEntrySectorBytesRequired == 0;
    }

    private static bool TestDefaultResultIsNotSuccess()
    {
        NativeAppSelectionBootScratchPlanResult result = default;

        return !result.IsInitialized
            && !result.Succeeded
            && result.Status == NativeAppSelectionBootScratchPlanStatus.Success
            && result.SectorSizeBytes == 0
            && result.PartitionEntryCount == 0
            && result.PartitionEntrySizeBytes == 0
            && result.PartitionDescriptorCountRequired == 0
            && result.GptHeaderSectorBytesRequired == 0
            && result.GptHeaderSectorBytesAvailable == 0
            && result.GptPartitionEntryBytesRequired == 0
            && result.GptPartitionEntrySectorsRequired == 0
            && result.GptPartitionEntrySectorBytesRequired == 0
            && result.GptPartitionEntrySectorBytesAvailable == 0
            && result.GptPartitionDescriptorCountAvailable == 0
            && result.ManifestBytesRequired == 0
            && result.ManifestBytesAvailable == 0
            && result.PackageBytesRequired == 0
            && result.PackageBytesAvailable == 0
            && !RuntimeHelpers.IsReferenceOrContainsReferences<NativeAppSelectionBootScratchPlanResult>();
    }

    private static bool TestPreservesNoLiveStorageBoundary()
    {
        ScratchBuffers scratch = ScratchBuffers.CreateDefault();
        Fill(scratch.PrimaryHeaderSector, 0xA5);
        Fill(scratch.PartitionEntrySectors, 0x5A);
        Fill(scratch.ManifestBuffer, 0x33);
        Fill(scratch.PackageBuffer, 0xCC);

        NativeAppSelectionBootScratchPlanResult result = ValidateDefault(scratch);

        return result.Succeeded
            && result.IsInitialized
            && !NativeAppSelectionBootScratchPlan.ReadsStorage
            && !NativeAppSelectionBootScratchPlan.ParsesManifestOrPackage
            && !NativeAppSelectionBootScratchPlan.AllocatesBuffers
            && !NativeAppSelectionBootScratchPlan.ExecutesAppCode
            && !RuntimeHelpers.IsReferenceOrContainsReferences<NativeAppSelectionBootScratchPlanResult>()
            && AllBytesEqual(scratch.PrimaryHeaderSector, 0xA5)
            && AllBytesEqual(scratch.PartitionEntrySectors, 0x5A)
            && AllBytesEqual(scratch.ManifestBuffer, 0x33)
            && AllBytesEqual(scratch.PackageBuffer, 0xCC);
    }

    private static NativeAppSelectionBootScratchPlanResult ValidateDefault(ScratchBuffers scratch)
    {
        return NativeAppSelectionBootScratchPlan.ValidateDefault(
            scratch.PrimaryHeaderSector,
            scratch.PartitionEntrySectors,
            scratch.Partitions,
            scratch.ManifestBuffer,
            scratch.PackageBuffer);
    }

    private static void Fill(byte[] buffer, byte value)
    {
        for (int index = 0; index < buffer.Length; index++)
        {
            buffer[index] = value;
        }
    }

    private static bool AllBytesEqual(byte[] buffer, byte expected)
    {
        for (int index = 0; index < buffer.Length; index++)
        {
            if (buffer[index] != expected)
            {
                return false;
            }
        }

        return true;
    }

    private sealed class ScratchBuffers
    {
        private ScratchBuffers(
            int headerSectorBytes,
            int partitionEntrySectorBytes,
            int partitionDescriptorCount,
            int manifestBufferBytes,
            int packageBufferBytes)
        {
            PrimaryHeaderSector = new byte[headerSectorBytes];
            PartitionEntrySectors = new byte[partitionEntrySectorBytes];
            Partitions = new GptPartitionDescriptor[partitionDescriptorCount];
            ManifestBuffer = new byte[manifestBufferBytes];
            PackageBuffer = new byte[packageBufferBytes];
        }

        public byte[] PrimaryHeaderSector { get; }

        public byte[] PartitionEntrySectors { get; }

        public GptPartitionDescriptor[] Partitions { get; }

        public byte[] ManifestBuffer { get; }

        public byte[] PackageBuffer { get; }

        public static ScratchBuffers CreateDefault()
        {
            return Create();
        }

        public static ScratchBuffers Create(
            int headerSectorBytes = (int)NativeAppSelectionBootScratchPlan.DefaultSectorSizeBytes,
            int partitionEntrySectorBytes =
                (int)(NativeAppSelectionBootScratchPlan.DefaultPartitionEntrySectorsRequired *
                NativeAppSelectionBootScratchPlan.DefaultSectorSizeBytes),
            int partitionDescriptorCount = (int)NativeAppSelectionBootScratchPlan.DefaultPartitionDescriptorCount,
            int manifestBufferBytes = (int)NativeAppSelectionBootScratchPlan.DefaultManifestBufferBytes,
            int packageBufferBytes = (int)NativeAppSelectionBootScratchPlan.DefaultPackageBufferBytes)
        {
            return new ScratchBuffers(
                headerSectorBytes,
                partitionEntrySectorBytes,
                partitionDescriptorCount,
                manifestBufferBytes,
                packageBufferBytes);
        }
    }
}
