using System;

namespace OS.Kernel.Compatibility.LinuxX64;
public static unsafe partial class LinuxWritableSyscallObservation
{
    public enum CopyDisposition : byte
    {
        Uncommitted,
        Committed,
        Failed,
        Cancelled,
        Revoked,
        Repark
    }
}
