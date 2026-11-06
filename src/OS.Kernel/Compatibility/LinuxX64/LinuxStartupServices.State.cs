using OS.Std.Hash;
using OS.Std.Memory;

namespace OS.Kernel.Compatibility.LinuxX64;

public static unsafe partial class LinuxStartupServices
{
    /// <summary>Caller-owned single-use state; do not copy a live instance.
    /// Finite output consumes captured bytes; service output advances its DRBG.
    /// After valid preflight an attempted copy irrevocably consumes the entire
    /// generated request even on callback failure. Invalid pointers consume none.
    /// </summary>

    public struct State
    {
        internal Admission Owner;
        public readonly OS.Manifest.LinuxAppConfiguration.GcBudget GcBudget => Initialized == 1 ? Owner.GcBudget : default;

        internal ulong CurrentBreak, LastTicks;
        internal uint Initialized, UsedEntropy, BarrierRegistered;
        internal uint EntropyDiscarded;
        internal ulong GeneratedRequests, GeneratedBytes;
        public readonly ulong Break => CurrentBreak;
        /// <summary>Unused finite reservoir bytes; always0 for service mode,
        /// never a DRBG availability or entropy-quality observation.</summary>

        public readonly uint RemainingEntropy => Initialized == 1 ? Owner.CapturedEntropyBytes - UsedEntropy : 0;
        /// <summary>Lifetime generated service requests, including failed output
        /// copies; serialized overflow refuses another request before mutation.</summary>

        public readonly ulong RandomRequests => GeneratedRequests;
        /// <summary>Lifetime generated service bytes, including failed output
        /// copies; these are output accounting, not credited physical entropy.</summary>

        public readonly ulong RandomBytes => GeneratedBytes;
    }
}
