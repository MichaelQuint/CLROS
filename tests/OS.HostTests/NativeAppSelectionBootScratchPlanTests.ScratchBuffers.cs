using System.Runtime.CompilerServices;
using OS.Manifest;
using OS.Storage;

internal static partial class NativeAppSelectionBootScratchPlanTests
{
    private sealed class ScratchBuffers
    {
        private ScratchBuffers(int headerSectorBytes, int partitionEntrySectorBytes, int partitionDescriptorCount, int manifestBufferBytes, int packageBufferBytes)
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

        public static ScratchBuffers Create(int headerSectorBytes = (int)NativeAppSelectionBootScratchPlan.DefaultSectorSizeBytes, int partitionEntrySectorBytes = (int)(NativeAppSelectionBootScratchPlan.DefaultPartitionEntrySectorsRequired * NativeAppSelectionBootScratchPlan.DefaultSectorSizeBytes), int partitionDescriptorCount = (int)NativeAppSelectionBootScratchPlan.DefaultPartitionDescriptorCount, int manifestBufferBytes = (int)NativeAppSelectionBootScratchPlan.DefaultManifestBufferBytes, int packageBufferBytes = (int)NativeAppSelectionBootScratchPlan.DefaultPackageBufferBytes)
        {
            return new ScratchBuffers(headerSectorBytes, partitionEntrySectorBytes, partitionDescriptorCount, manifestBufferBytes, packageBufferBytes);
        }
    }
}
