using System.Runtime.CompilerServices;
using OS.Kernel.Platform;

namespace OS.Kernel.Diagnostics;

public static unsafe partial class WritableStorageTiming
{
    /// <summary>Stack-local synchronous hook receipt. It cannot authorize later
    /// access or survive cancellation; default receipts are inert.</summary>

    public readonly struct Stamp
    {
        internal readonly State* Owner;
        internal readonly ulong Generation, Ordinal, Raw, Audit;
        internal readonly Bucket Kind;
        internal Stamp(State* owner, ulong ordinal, ulong raw, Bucket kind)
        {
            Owner = owner;
            Generation = owner->Generation;
            Ordinal = ordinal;
            Raw = raw;
            Audit = owner->AuditTicks;
            Kind = kind;
        }
    }
}
