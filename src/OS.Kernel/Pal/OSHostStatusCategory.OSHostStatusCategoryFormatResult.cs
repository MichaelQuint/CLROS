using OS.Std.Text;

namespace OS.Pal;
public readonly struct OSHostStatusCategoryFormatResult
{
    private const uint InitializedFlag = 1;
    public readonly OSHostStatus Status;
    public readonly OSHostStatusCategory Category;
    public readonly uint BytesWritten;
    public readonly uint RequiredBytes;
    public readonly uint Flags;
    public OSHostStatusCategoryFormatResult(OSHostStatus status, OSHostStatusCategory category, uint bytesWritten, uint requiredBytes, uint flags = InitializedFlag)
    {
        Status = status;
        Category = category;
        BytesWritten = bytesWritten;
        RequiredBytes = requiredBytes;
        Flags = flags;
    }

    public bool IsInitialized => (Flags & InitializedFlag) != 0;
    public bool Succeeded => IsInitialized && Status == OSHostStatus.Ok && RequiredBytes != 0 && BytesWritten == RequiredBytes;

    public static OSHostStatusCategoryFormatResult FromStatus(OSHostStatus status, OSHostStatusCategory category)
    {
        return new OSHostStatusCategoryFormatResult(status, category, bytesWritten: 0, requiredBytes: 0);
    }
}
