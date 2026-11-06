using OS.Std.Text;

namespace OS.Pal;

public readonly struct OSHostStatusNameFormatResult
{
    private const uint InitializedFlag = 1;
    public readonly OSHostStatus Status;
    public readonly uint BytesWritten;
    public readonly uint RequiredBytes;
    public readonly uint Flags;
    public OSHostStatusNameFormatResult(OSHostStatus status, uint bytesWritten, uint requiredBytes, uint flags = InitializedFlag)
    {
        Status = status;
        BytesWritten = bytesWritten;
        RequiredBytes = requiredBytes;
        Flags = flags;
    }

    public bool IsInitialized => (Flags & InitializedFlag) != 0;
    public bool Succeeded => IsInitialized && Status == OSHostStatus.Ok && RequiredBytes != 0 && BytesWritten == RequiredBytes;

    public static OSHostStatusNameFormatResult FromStatus(OSHostStatus status)
    {
        return new OSHostStatusNameFormatResult(status, bytesWritten: 0, requiredBytes: 0);
    }
}
