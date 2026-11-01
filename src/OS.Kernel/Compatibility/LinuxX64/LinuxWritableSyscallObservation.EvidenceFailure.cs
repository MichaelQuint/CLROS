using System;

namespace OS.Kernel.Compatibility.LinuxX64;
public static unsafe partial class LinuxWritableSyscallObservation
{
    public enum EvidenceFailure : byte
    {
        None,
        Capacity,
        Sequence
    }
}
