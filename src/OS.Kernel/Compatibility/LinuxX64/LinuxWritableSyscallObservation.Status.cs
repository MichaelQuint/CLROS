using System;

namespace OS.Kernel.Compatibility.LinuxX64;

public static unsafe partial class LinuxWritableSyscallObservation
{
    public enum Status : byte
    {
        Success,
        Empty,
        NotRecorded,
        InvalidStorage,
        InvalidRecord,
        StaleEpoch,
        Busy,
        Incomplete,
        CapacityExceeded,
        SequenceExhausted
    }
}
