namespace OS.Kernel.Threading;
public unsafe partial struct X64ThreadContext
{
    public enum Status : byte
    {
        Ok,
        InvalidBuffer,
        BufferTooSmall,
        Uninitialized,
        InvalidAddress,
        InvalidFlags,
        InvalidMxcsrMask,
        InvalidFpState
    }
}
