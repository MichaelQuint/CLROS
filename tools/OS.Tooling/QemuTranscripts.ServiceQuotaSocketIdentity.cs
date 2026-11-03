namespace OS.Tooling;

public static partial class QemuTranscripts
{
    /// <summary>Copied original transport generation and tuple. Slot is the
    /// native network slot; Index belongs to its owned connection table.</summary>

    public readonly record struct ServiceQuotaSocketIdentity(ulong Owner, ulong Epoch, ulong Generation, uint Index, uint Slot, ulong Local, uint LocalPort, ulong Remote, uint RemotePort);
}
