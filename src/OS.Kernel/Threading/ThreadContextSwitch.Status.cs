namespace OS.Kernel.Threading;
public static unsafe partial class ThreadContextSwitch
{
    public enum Status : byte
    {
        Ok,
        Uninitialized,
        InvalidOwner,
        UnsupportedCpu,
        InvalidRoot,
        InvalidRange,
        Overlap,
        InvalidIdentity,
        StaleIdentity,
        InvalidContext,
        InvalidStack,
        InvalidContinuation,
        InvalidFlags,
        InvalidTls
    }
}
