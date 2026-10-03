namespace OS.Kernel.Runtime;

/// <summary>
/// Bounded zero-initialized String storage on the existing retained-page cursor.
/// Length is a signed x64 scalar, not a native export declaration. The result is
/// numeric caller-owned storage, never a CLR String or compiled constructor proof.
/// Caller initialization of UTF-16 code units must stay within the reported length;
/// it must preserve the zero terminator, header, metadata slot and adjacent objects.
/// Authentic prefix/token association and mapped lifetime remain caller obligations.
/// No interning, encoding, character copy, reset, refill or GC support is provided.
/// </summary>
public static unsafe class GuestTestStringAllocationCharacterStorage
{
    public static RhpNewFastResult TryAllocate(RhpNewFastHeapAccess access,
        RhpNewFastBumpState* state, ulong stateCapacity, NativeAotLayoutProfile profile,
        NativeAotMethodTablePrefix prefix, ulong methodTableToken, long length) =>
        GuestTestRhpNewFast.TryAllocateString(access, state, stateCapacity, profile, prefix,
            methodTableToken, length);
}
