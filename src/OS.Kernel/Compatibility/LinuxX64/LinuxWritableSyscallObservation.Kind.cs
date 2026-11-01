using System;

namespace OS.Kernel.Compatibility.LinuxX64;
public static unsafe partial class LinuxWritableSyscallObservation
{
    public enum Kind : byte
    {
        Path = 1,
        PwriteInput,
        ReadOutput,
        StatOutput
    }
}
