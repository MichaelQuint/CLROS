using System;

namespace OS.Kernel.Compatibility.LinuxX64;
public static unsafe partial class LinuxWritableSyscallObservation
{
    /// <summary>Copied original owner identities. Numeric values do not admit
    /// their pointers or establish that any observed call actually occurred.</summary>

    public struct Admission
    {
        public ulong DescriptorOwner, AttachmentOwner, AttachmentEpoch, ObservationEpoch;
    }
}
