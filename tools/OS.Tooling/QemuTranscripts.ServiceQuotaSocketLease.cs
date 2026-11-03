using OS.Kernel.Compatibility.LinuxX64;
using OS.Kernel.Threading;

namespace OS.Tooling;
public static partial class QemuTranscripts
{
    /// <summary>One observed RX/TX borrower transition for an original native
    /// lifetime census generation. End distinguishes post-End abort release;
    /// bytes measure leased capacities, not payload or physical allocation.</summary>

    public readonly record struct ServiceQuotaSocketLease(int RecordIndex, bool Returned, ServiceQuotaTransportLifetime Lifetime, ulong ReceiveBytes, ulong SendBytes, bool End);
}
