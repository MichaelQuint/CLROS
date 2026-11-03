using OS.Kernel.Compatibility.LinuxX64;
using OS.Kernel.Threading;

namespace OS.Tooling;
public static partial class QemuTranscripts
{
    /// <summary>Copied audited cooperative backing and a separately named main
    /// supervisor usable bound. Bytes remain retained; none is a guest pthread
    /// extent, written-stack depth or newly allocated physical peak.</summary>

    public readonly record struct ServiceQuotaStackBacking(int RecordIndex, ulong CooperativePages, ulong WorkerMappedBytes, ulong SwitchMappedBytes, ulong IrqMappedBytes, ulong MainSupervisorUsableBound);
}
