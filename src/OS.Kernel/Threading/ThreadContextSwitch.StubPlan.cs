namespace OS.Kernel.Threading;

public static unsafe partial class ThreadContextSwitch
{
    /// <summary>
    /// Fixed source/destination CALL stub, not a general scheduler entry. The
    /// trusted owner pins separately admitted CPL0 callback/rejection code and
    /// the callback's declared control extent. The callback uses Win64
    /// byte(control, source, destination), returns exactly1 only after fresh
    /// ownership/CPU/context admission, and may not alter pinned identities or
    /// context storage. HelperFx contains initialized empty Win64 x87/SSE state.
    /// JumpSlot is separate owned RW/NX storage; it is not a parked return slot.
    /// RejectRip names an owned terminal/recovery path outside this stub, able
    /// to restore the independently retained original owner state. No field
    /// supplies execution, mapping, callback or recovery authority by itself.
    /// </summary>

    public struct StubPlan
    {
        public uint CodeOffset, CallbackBytes;
        public ulong AdmissionCallback, CallbackContext, HelperFx, JumpSlot, RejectRip;
    }
}
