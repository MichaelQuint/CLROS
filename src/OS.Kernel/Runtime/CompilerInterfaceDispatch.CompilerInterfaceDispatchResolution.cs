using OS.Boot;

namespace OS.Kernel.Runtime;

public struct CompilerInterfaceDispatchResolution
{
    public ulong Target, ReceiverType, RequestedInterface, ImplementedInterface, DeclaringType;
    public ulong AllocationSequence;
    public ushort InterfaceSlot, ImplementationSlot;
    public bool CacheHit;
}
