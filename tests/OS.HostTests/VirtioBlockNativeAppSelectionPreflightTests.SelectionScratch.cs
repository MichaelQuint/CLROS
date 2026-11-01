using System.Runtime.CompilerServices;
using System.Text;
using OS.Boot;
using OS.Drivers;
using OS.Kernel.Diagnostics;
using OS.Manifest;
using OS.Std.Binary;
using OS.Std.Hash;
using OS.Storage;

internal static partial class VirtioBlockNativeAppSelectionPreflightTests
{
    private sealed class SelectionScratch
    {
        public SelectionScratch(
            int manifestBufferLength = (int)NativeAppSelectionBootScratchPlan.DefaultManifestBufferBytes,
            int packageBufferLength = (int)NativeAppSelectionBootScratchPlan.DefaultPackageBufferBytes)
        {
            ManifestBuffer = new byte[manifestBufferLength];
            PackageBuffer = new byte[packageBufferLength];
        }

        public byte[] PrimaryHeader { get; } = new byte[SectorSize];

        public byte[] PartitionEntries { get; } =
            new byte[checked((int)(PartitionEntryCount * PartitionEntrySize))];

        public GptPartitionDescriptor[] Partitions { get; } = new GptPartitionDescriptor[2];

        public byte[] ManifestBuffer { get; }

        public byte[] PackageBuffer { get; }
    }
}
