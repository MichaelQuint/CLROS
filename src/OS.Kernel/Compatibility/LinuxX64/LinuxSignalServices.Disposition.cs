using OS.Pal;

namespace OS.Kernel.Compatibility.LinuxX64;

public static unsafe partial class LinuxSignalServices
{
    /// <summary>Exact 32-byte rt_sigaction kernel layout. Handler/restorer are
    /// user values, not callable kernel pointers. Registration stores them;
    /// delivery separately validates executable user mappings.</summary>

    public struct Disposition
    {
        public ulong Handler, Flags, Restorer, Mask;
    }
}
