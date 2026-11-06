using OS.Boot;

namespace OS.Kernel.Runtime;

public static unsafe partial class CompilerClassConstructorExecution
{
    private struct State
    {
        internal ulong Phase, Self, Capacity, ModuleOwner, ModuleCapacity, ModuleHandle;
        internal EfiLoadedImageRangeCapturePlan Capture;
        internal M01LoadedImageVirtualMappingEvidence Mapping;
        internal ulong EagerTable;
        internal uint EagerCapacity, LazyCapacity, EagerCount, EagerCompleted;
        internal uint EagerPhase, LazyCount, ActiveLazy;
        internal bool EagerPresent;
    }
}
