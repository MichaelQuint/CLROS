using OS.Pal;

namespace OS.Kernel.Compatibility.LinuxX64;

public static unsafe partial class LinuxSignalServices
{
    /// <summary>Delivery at an authenticated user-return boundary. Masked is not
    /// consumption: the pending owner must retain it. Unsupported covers Linux
    /// job control, which this single-workload owner cannot truthfully emulate.
    /// BadFrame requires owned SIGSEGV termination without resuming user code.</summary>

    public enum Delivery : byte
    {
        Rejected,
        Masked,
        Ignored,
        Delivered,
        Terminate,
        Unsupported,
        BadFrame,
        None
    }
}
