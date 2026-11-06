using System.Runtime.CompilerServices;
using System.Text;
using OS.Boot;
using OS.Manifest;
using OS.Std.Binary;
using OS.Std.Hash;
using OS.Storage;

internal static partial class NativeAppSelectionLoaderTests
{
    private sealed class SelectionScratch
    {
        public SelectionScratch(int manifestBufferLength = NativeManifestParser.MaximumInputLength, int packageBufferLength = 1024)
        {
            ManifestBuffer = new byte[manifestBufferLength];
            PackageBuffer = new byte[packageBufferLength];
        }

        public byte[] PrimaryHeader { get; } = new byte[SectorSize];
        public byte[] PartitionEntries { get; } = new byte[checked((int)(PartitionEntryCount * PartitionEntrySize))];
        public GptPartitionDescriptor[] Partitions { get; } = new GptPartitionDescriptor[2];
        public byte[] ManifestBuffer
        {
            get;
        }
        public byte[] PackageBuffer
        {
            get;
        }
    }
}
