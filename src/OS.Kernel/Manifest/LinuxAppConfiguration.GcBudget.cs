using OS.Std.Text;
using OS.Kernel.Memory;

namespace OS.Manifest;
public static unsafe partial class LinuxAppConfiguration
{
    public readonly struct GcBudget
    {
        internal GcBudget(Status status, ulong pool = 0, ulong statics = 0, ulong native = 0, ulong gc = 0, ulong entitlement = 0, bool terminateOnPhysicalBackingExhaustion = false)
        {
            Result = status;
            TerminateOnPhysicalBackingExhaustion = terminateOnPhysicalBackingExhaustion;
            PoolBytes = pool;
            StaticBytes = statics;
            NativeReserveBytes = native;
            GcLimitBytes = gc;
            EntitlementBytes = entitlement;
        }

        public readonly Status Result;
        /// <summary>Explicit immutable owner policy; absent by default. This bit
        /// supplies no failure classification, process or cleanup authority and
        /// does not partition the shared GC/native pool. Only a successful budget
        /// can carry it; the EFI service owner separately admits its diagnostic.</summary>

        public readonly bool TerminateOnPhysicalBackingExhaustion;
        public readonly ulong PoolBytes, StaticBytes, NativeReserveBytes, GcLimitBytes, EntitlementBytes;
        public bool Succeeded => Result == Status.Success;
    }
}
