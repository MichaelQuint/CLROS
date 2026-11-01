using System;

namespace OS.Kernel.Compatibility.LinuxX64;
public static unsafe partial class LinuxWritableSyscallObservation
{
    public enum Boundary : byte
    {
        Admission = 1,
        ImmediateOutput,
        CompletionOutput
    }
}
