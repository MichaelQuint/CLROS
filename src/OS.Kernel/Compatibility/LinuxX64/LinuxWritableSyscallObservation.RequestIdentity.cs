using System;

namespace OS.Kernel.Compatibility.LinuxX64;

public static unsafe partial class LinuxWritableSyscallObservation
{
    /// <summary>Allzero explicitly means no request existed at capture time.
    /// Observation sequence must never be substituted for this generation.</summary>

    public struct RequestIdentity
    {
        public ulong Owner, Epoch, Generation;
    }
}
