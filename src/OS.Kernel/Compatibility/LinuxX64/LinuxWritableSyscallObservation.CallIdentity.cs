using System;

namespace OS.Kernel.Compatibility.LinuxX64;
public static unsafe partial class LinuxWritableSyscallObservation
{
    public struct CallIdentity
    {
        public ulong DescriptorOwner, AttachmentOwner, AttachmentEpoch, ThreadIdentity, Ordinal, Number, Rip;
    }
}
