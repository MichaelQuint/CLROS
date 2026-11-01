using C = OS.Storage.ContinuingFat32Mutation;

namespace OS.Kernel.Compatibility.LinuxX64;
public static unsafe partial class LinuxContinuingWritableDescriptorServices
{
    /// <summary>Noncopyable-by-contract retained owner. Contains only POD and
    /// borrowed pointers, never Span or managed references. Dirty/recovery
    /// failure retains the complete provider and engine lifetime; resetting the
    /// device is not permission to discard either owner's unresolved state.</summary>

    public struct State
    {
        internal ulong Self, KnownRevision, NextGeneration, NextAlias;
        internal Admission Input;
        internal LockAdmission LockInput;
        internal ProcessToken PrimaryOwner;
        internal Pending Work;
        internal bool Active, Retiring, Poisoned, RetiredValue, Locking;
        public readonly bool RequestActive => Work.Active;
        public readonly bool IsRetiring => Retiring;
        public readonly bool Retired => RetiredValue;
        public readonly bool Quarantined => Poisoned;
    }
#if !NOSTDLIB_EFI || M16_027_TIMING_PROBE
#endif
}
