using OS.Manifest;
using OS.Std.Text;

namespace OS.Kernel.Compatibility.LinuxX64;

public partial struct LinuxAppLogBudget
{
    /// <summary>Zero/default is rejection; exhausted quota is a distinct failure.</summary>

    public enum ChargeStatus : byte
    {
        Rejected,
        Accepted,
        QuotaExceeded
    }
}
