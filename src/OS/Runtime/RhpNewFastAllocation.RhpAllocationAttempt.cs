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

#if M04_CHECKED_REFERENCE_BARRIER_PROBE

#else

#if M04_STRING_ALLOCATION_PROBE

#elif M04_SZ_ARRAY_ALLOCATION_PROBE

#else

#if M04_RHP_NEW_FAST_BEFORE_BIND_FAILURE_PROBE

#else

#if M04_RHP_NEW_FAST_EXHAUSTION_FAILURE_PROBE

#else

#endif
#endif

#endif
#endif

#if M04_GC_STATIC_ROOT_MATERIALIZATION_PROBE

#elif M04_CHECKED_REFERENCE_BARRIER_PROBE

#elif M04_STRING_ALLOCATION_PROBE

#elif M04_SZ_ARRAY_ALLOCATION_PROBE

#else

#endif

#if M04_STRING_ALLOCATION_PROBE

#if M04_STRING_ALLOCATION_BEFORE_BIND_PROBE

#else

#if M04_STRING_ALLOCATION_INVALID_LENGTH_PROBE

#else

#if M04_STRING_ALLOCATION_EXHAUSTION_PROBE

#else

#endif
#endif
#endif

#endif

#if M04_SZ_ARRAY_ALLOCATION_PROBE

#if M04_SZ_ARRAY_ALLOCATION_BEFORE_BIND_PROBE

#else

#if M04_SZ_ARRAY_ALLOCATION_EXHAUSTION_PROBE

#else

#endif
#endif

#if M04_STRING_ALLOCATION_PROBE

#else

#endif

#endif

#if M04_STRING_ALLOCATION_PROBE

#endif

#if M04_STRING_ALLOCATION_PROBE

#elif M04_SZ_ARRAY_ALLOCATION_PROBE

#else

#endif

#if M04_STRING_ALLOCATION_PROBE

#elif M04_SZ_ARRAY_ALLOCATION_PROBE

#endif

#if M04_CHECKED_REFERENCE_BARRIER_PROBE

#endif

#if M04_STRING_ALLOCATION_PROBE

#elif M04_SZ_ARRAY_ALLOCATION_PROBE

#else

#endif

#if M04_SZ_ARRAY_ALLOCATION_PROBE

#endif
#if M04_STRING_ALLOCATION_PROBE

#endif

#pragma warning disable 649

#pragma warning restore 649
#endif
