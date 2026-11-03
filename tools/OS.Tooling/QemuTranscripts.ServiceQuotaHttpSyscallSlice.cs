namespace OS.Tooling;

public static partial class QemuTranscripts
{
    /// <summary>Clipped consuming receive or accepted send bytes. Original
    /// retains its normalized row/CALL indices, identity and syscall ordinal;
    /// StreamOffset is directional and ByteCount may cover only part of it.
    /// A peek supplies no consuming slice and no transmission is implied.</summary>

    public readonly record struct ServiceQuotaHttpSyscallSlice(ServiceQuotaSocketIo Original, ulong StreamOffset, uint ByteCount);
}
