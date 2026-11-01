namespace OS.Boot;
/// <summary>
/// Exact source ledgers which must agree for a range binding.
/// </summary>

public enum M01OwnedCr3CompositeTransitionRangeProvenance : byte
{
    Uninitialized = 0,
    AllocationReservationAndIdentityMapping = 1,
    ReservationAndIdentityMapping = 2,
    ContainedInAllocationReservationAndIdentityMapping = 3
}
#if !NOSTDLIB_EFI
#endif
