using OS.Kernel.Platform;

namespace OS.Drivers;

public static unsafe partial class ContinuingVirtioNet
{
    public enum Status : byte
    {
        Uninitialized,
        Success,
        Empty,
        Full,
        ShortBuffer,
        InvalidStorage,
        InvalidState,
        InvalidFrame,
        InvalidCompletion,
        OwnershipFailed,
        OrderingFailed,
        NotQuiescent
    }
}
