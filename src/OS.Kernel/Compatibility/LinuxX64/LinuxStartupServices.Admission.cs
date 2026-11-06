using OS.Std.Hash;
using OS.Std.Memory;

namespace OS.Kernel.Compatibility.LinuxX64;

public static unsafe partial class LinuxStartupServices
{
    /// <summary>Immutable facts and borrowed capabilities. Physical/available
    /// bytes are actual owned snapshots, not Linux-host values. The IRQ-off
    /// single-context owner excludes other resource consumers during execution.
    /// AvailableBytes includes the unused pool; pool commitments are deducted.
    /// Entropy must be separate from AT_RANDOM and never published elsewhere.
    /// Finite CapturedEntropyBytes is exactly32,256 or4096 genuinely acquired
    /// bytes. Explicit ContinuingRandom instead borrows a private initialized
    /// generator with no reservoir; both lifetimes stay exclusive through cleanup.
    /// Realtime, when present, stays exclusively mapped through cleanup and uses
    /// this exact monotonic source/frequency. RequireRealtime prevents heap
    /// admission without an available UTC owner. No firmware authority crosses
    /// this boundary; optional absence retains the unsupported realtime errno.
    /// </summary>

    public struct Admission
    {
        public LinuxMemoryServices.State* Memory;
        public LinuxSyscallAccess User;
        public delegate* unmanaged<void*, ulong*, byte> ReadClock;
        public delegate* unmanaged<void*, ulong, byte*, uint, byte> CopyToUser;
        public delegate* unmanaged<void*, byte> MemoryBarrier;
        public OS.Kernel.Platform.OwnedRealtimeClock.State* Realtime;
        public ulong FrequencyHz, PhysicalBytes, AvailableBytes, HeapStart, HeapBytes, StackBytes;
        /// <summary>Optional value-only budget from the immutable package and
        /// sealed boot ledger. Success must match this actual pool and cover the
        /// initial heap; failed preflight cannot grant startup. Legacy absence
        /// retains prior policy. This does not partition GC/native pool pages.</summary>

        public OS.Manifest.LinuxAppConfiguration.GcBudget GcBudget;
        public uint ProcessId;
        public byte* Entropy;
        public uint CapturedEntropyBytes;
        /// <summary>Explicit service-only borrowed generator, initialized from
        /// independently captured private entropy, never AT_RANDOM or a TCP key.
        /// ContinuingRandom requires preemptible admission and excludes the finite
        /// reservoir. Keep it exclusive/mapped through DiscardEntropy; no firmware
        /// callback or source fallback is retained. Reseed is owner-authorized.</summary>

        public HmacSha256Drbg.State* Generator;
        public bool ContinuingRandom;
        public bool SingleCpuValidated, FirmwareExited, RequireRealtime;
    }
}
