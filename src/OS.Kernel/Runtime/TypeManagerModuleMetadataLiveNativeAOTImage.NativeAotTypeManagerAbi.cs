using OS.Boot;

namespace OS.Kernel.Runtime;
/// <summary>
/// The pinned .NET 11 RC2 x64 TypeManager data layout, not the CLROS owner.
/// Class-library fields stay zero; recording GC/TLS addresses initializes neither.
/// Explicit trailing padding makes all 48 bytes deterministic.
/// </summary>

public struct NativeAotTypeManagerAbi
{
    public ulong OsModule;
    public ulong ReadyToRunHeader;
    public ulong GcStaticRegion;
    public ulong ThreadStaticRegion;
    public ulong ClassLibraryFunctions;
    public uint ClassLibraryFunctionCount;
    public uint Padding;
}
