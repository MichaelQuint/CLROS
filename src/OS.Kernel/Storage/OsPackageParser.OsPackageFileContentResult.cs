using OS.Std.Binary;
using OS.Std.Memory;

namespace OS.Storage;

public readonly ref struct OsPackageFileContentResult
{
    private readonly ReadOnlySpan<byte> _content;

    private OsPackageFileContentResult(
        OsPackageStatus status,
        OsPackageHeader header,
        OsPackageFileEntry entry,
        ReadOnlySpan<byte> content)
    {
        IsInitialized = true;
        Status = status;
        Header = header;
        Entry = entry;
        _content = content;
    }

    public bool IsInitialized
    {
        get;
    }

    public OsPackageStatus Status
    {
        get;
    }

    public OsPackageHeader Header
    {
        get;
    }

    public OsPackageFileEntry Entry
    {
        get;
    }

    public ReadOnlySpan<byte> Content => _content;

    public bool Succeeded => IsInitialized && Status == OsPackageStatus.Success;

    public static OsPackageFileContentResult Success(
        OsPackageHeader header,
        OsPackageFileEntry entry,
        ReadOnlySpan<byte> content)
    {
        return new OsPackageFileContentResult(OsPackageStatus.Success, header, entry, content);
    }

    public static OsPackageFileContentResult Fail(OsPackageStatus status)
    {
        return new OsPackageFileContentResult(status, default, default, default);
    }

    public static OsPackageFileContentResult Fail(OsPackageStatus status, OsPackageHeader header)
    {
        return new OsPackageFileContentResult(status, header, default, default);
    }
}
