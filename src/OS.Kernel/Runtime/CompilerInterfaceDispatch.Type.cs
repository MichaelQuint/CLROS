using OS.Boot;

namespace OS.Kernel.Runtime;
public static unsafe partial class CompilerInterfaceDispatch
{
    private struct Type
    {
        internal ulong Address, Parent, Vtable, Interfaces, Map, SealedTable, GenericDefinition, Composition;
        internal uint Flags, BaseSize;
        internal ushort Slots, InterfaceCount;
    }
}
