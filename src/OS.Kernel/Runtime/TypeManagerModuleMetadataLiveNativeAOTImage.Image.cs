using OS.Boot;

namespace OS.Kernel.Runtime;

public static unsafe partial class TypeManagerModuleMetadataLiveNativeAOTImage
{
    private struct Image
    {
        internal ulong Base, Bytes, Headers, Sections, SectionCount;
        internal ulong Modules, ModuleBytes, Header, TableBytes, Slot, Gc, Tls;
        internal uint Rows;
    }
}
