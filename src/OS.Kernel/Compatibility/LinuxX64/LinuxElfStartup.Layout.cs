using OS.Manifest;
using OS.Std.Binary;
using OS.Std.Text;

namespace OS.Kernel.Compatibility.LinuxX64;
public static unsafe partial class LinuxElfStartup
{
    private struct Layout
    {
        internal LinuxElfStartupPlan Plan;
        internal uint Platform, Random, Frame, ArgBytes;
        internal fixed uint Arguments[129], Environment[16];
    }
}
