namespace OS.Tooling;
public static partial class QemuTranscripts
{
    private sealed record QuotaPairHostBytes(byte[] Reads, byte[] Writes, byte[] Unknown);
}
