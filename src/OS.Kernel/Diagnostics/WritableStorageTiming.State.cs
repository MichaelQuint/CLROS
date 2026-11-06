using System.Runtime.CompilerServices;
using OS.Kernel.Platform;

namespace OS.Kernel.Diagnostics;

public static unsafe partial class WritableStorageTiming
{
    /// <summary>Caller-owned, zero-initialized and disjoint from every measured
    /// owner's storage and the counter register. Do not move, mutate or release
    /// it while installed. Failed accounting invalidates the complete report.</summary>

    public struct State
    {
        public ulong Self, Generation, Counter, Started, OuterTicks, RawReads, AdjacentReadTicks;
        public ulong AuditOutsideEnclosingTicks, UnattributedTicks;
        public ulong AuditCalls, AuditTicks, OutsideDeviceCalls;
        public ulong PairCalls, PairTicks, PairAuditTicks;
        public ulong PumpCalls, PumpTicks, PumpAuditTicks, EnginePumpCalls;
        public ulong EngineOwner;
        public uint Width, Phase, AuditDepth, PairDepth, PumpDepth, EngineDepth;
        public uint FirstEnginePhase, FirstEngineStep, FirstEngineWork;
        public uint LastEnginePhase, LastEngineStep, LastEngineWork;
        public bool FirstEngineIo, LastEngineIo, HasEngineProgress, Failed;
    }
}
