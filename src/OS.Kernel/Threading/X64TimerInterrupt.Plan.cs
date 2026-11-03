namespace OS.Kernel.Threading;

public static unsafe partial class X64TimerInterrupt
{
    /// <summary>Retained native callback and immutable neutral FXSAVE image.
    /// Callback signature is byte(void* context, Frame* frame), Win64, on the owned
    /// IST with IF clear/DF clear. Returning1 authorizes every field for IRETQ;
    /// all other bytes fail closed. Data must remain mapped/pinned through cleanup.
    /// Addresses/ranges are planning inputs, not mapping/CPU/lifetime proof.</summary>

    public struct Plan
    {
        public ulong CodeAddress, Callback, CallbackContext, HelperFx;
        public uint CallbackBytes;
    }
}
