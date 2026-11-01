using OS.Boot;
using Layout = OS.Kernel.Runtime.ObjectArrayStringEETypeMethodTableLayoutContracts;
using Unwind = OS.Kernel.Runtime.X64PERuntimeFunctionUnwindMetadataHostileInput;

namespace OS.Kernel.Runtime;
/// <summary>
/// Caller-owned exact compiler contributions, not lengths authenticated by PE or the EH stream.
/// CodeBytes includes the root and all contiguous funclets; EhBytes excludes linker padding.
/// No-EH methods use zero EhRva/EhBytes. Split/hot-cold contributions are unsupported.
/// </summary>

public readonly struct NativeAotEhMethodBounds
{
    public NativeAotEhMethodBounds(uint rootMethodRva, uint codeBytes, uint ehRva, uint ehBytes)
    {
        RootMethodRva = rootMethodRva;
        CodeBytes = codeBytes;
        EhRva = ehRva;
        EhBytes = ehBytes;
    }

    public uint RootMethodRva { get; }
    public uint CodeBytes { get; }
    public uint EhRva { get; }
    public uint EhBytes { get; }
}
