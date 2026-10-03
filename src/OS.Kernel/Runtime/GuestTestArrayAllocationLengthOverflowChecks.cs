namespace OS.Kernel.Runtime;

/// <summary>
/// Bounded SZ-array bytes on the same retained-page controller as fixed objects.
/// Prefix/token association and stable exclusive mapped storage are caller
/// obligations. Numeric references are not host CLR objects. Pointer components
/// are zero slots only; no reference stores, barriers, GC or compiled-array proof.
/// </summary>
public static unsafe class GuestTestArrayAllocationLengthOverflowChecks
{
    public static RhpNewFastResult TryAllocate(RhpNewFastHeapAccess access,
        RhpNewFastBumpState* state, ulong stateCapacity, NativeAotLayoutProfile profile,
        NativeAotMethodTablePrefix prefix, ulong methodTableToken, int length) =>
        GuestTestRhpNewFast.TryAllocateArray(access, state, stateCapacity,
            profile, prefix, methodTableToken, length);
}
