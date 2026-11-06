using System.Buffers.Binary;
using System.Security.Cryptography;
using System.Text;
using OS.Storage;

namespace OS.Tooling;

public static partial class WebRootImageFixture
{
    /// <summary>Copied evidence for one fixed fixture mutation and its actual
    /// independent extraction failure. Offsets address the original image's FAT
    /// words. This is not a healthy inventory, device observation or guest result.</summary>

    public sealed record FaultInspection(FaultReceipt Receipt, ulong PartitionFirstLba, ulong PartitionLastLba, uint TargetFirstCluster, uint TargetBytes, string OriginalTargetSha256, ulong Fat0WordOffset, ulong Fat1WordOffset, uint Fat0Before, uint Fat1Before, uint Fat0After, uint Fat1After, int ChangedBytes)
    {
        public bool GrantsNativeDeviceOrProcessAuthority => false;
        public bool GrantsGuestReadFailureAuthority => false;
    }
}
