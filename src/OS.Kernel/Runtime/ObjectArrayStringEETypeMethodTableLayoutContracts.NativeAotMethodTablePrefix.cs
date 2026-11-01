using System.Runtime.CompilerServices;

namespace OS.Kernel.Runtime;
// Scalar observation only: this POD is not a native MethodTable memory overlay.
// Token/count/hash fields do not establish addresses or optional-tail storage.
public readonly struct NativeAotMethodTablePrefix
{
    public NativeAotMethodTablePrefix(uint flags, uint baseSize, ulong relatedTypeToken, ushort vTableSlotCount, ushort interfaceCount, uint typeHash)
    {
        Flags = flags;
        BaseSize = baseSize;
        RelatedTypeToken = relatedTypeToken;
        VTableSlotCount = vTableSlotCount;
        InterfaceCount = interfaceCount;
        TypeHash = typeHash;
    }

    public uint Flags { get; }
    public uint BaseSize { get; }
    public ulong RelatedTypeToken { get; }
    public ushort VTableSlotCount { get; }
    public ushort InterfaceCount { get; }
    public uint TypeHash { get; }
}
