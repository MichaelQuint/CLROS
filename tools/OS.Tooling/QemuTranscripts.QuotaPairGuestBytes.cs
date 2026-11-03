namespace OS.Tooling;
public static partial class QemuTranscripts
{
    private sealed record QuotaPairGuestBytes(int RequestExtent, byte[] RequestPrefix, (int Offset, byte[] Bytes)[] Requests, byte[] ResponsePrefix);
}
