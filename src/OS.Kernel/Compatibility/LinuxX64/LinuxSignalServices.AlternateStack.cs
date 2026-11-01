using OS.Pal;

namespace OS.Kernel.Compatibility.LinuxX64;
public static unsafe partial class LinuxSignalServices
{
    /// <summary>Exact 24-byte Linux x64 stack_t. Padding is ignored on input and
    /// zero on output. Registration acquires no user backing or mapping lease.</summary>

    public struct AlternateStack
    {
        public ulong Address;
        public uint Flags, Padding;
        public ulong Bytes;
    }
}
