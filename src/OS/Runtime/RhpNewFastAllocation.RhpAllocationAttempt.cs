using OS.Kernel.Runtime;

namespace OS.Runtime;
#pragma warning disable CS0649
#pragma warning restore CS0649
#pragma warning disable CS0649
#pragma warning restore CS0649
internal readonly struct RhpAllocationAttempt
{
    internal RhpAllocationAttempt(RhpAdmissionStatus status, RhpNewFastResult leaf = default, RhpObjectObservation observation = default)
    {
        Status = status;
        Leaf = leaf;
        Observation = observation;
    }

    internal RhpAdmissionStatus Status
    {
        get;
    }
    internal RhpNewFastResult Leaf
    {
        get;
    }
    internal RhpObjectObservation Observation
    {
        get;
    }
    internal bool Succeeded => Status == RhpAdmissionStatus.Success && Leaf.AllocationSucceeded;
}
#if NOSTDLIB_EFI && M04_RHP_NEW_FAST_PROBE











#pragma warning disable 649

#pragma warning restore 649
#endif
