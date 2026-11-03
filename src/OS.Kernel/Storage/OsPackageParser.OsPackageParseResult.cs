using OS.Std.Binary;
using OS.Std.Memory;

namespace OS.Storage;

public readonly struct OsPackageParseResult
{
    private OsPackageParseResult(OsPackageStatus status, OsPackageHeader header)
    {
        IsInitialized = true;
        Status = status;
        Header = header;
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

    public bool Succeeded => IsInitialized && Status == OsPackageStatus.Success;

    public static OsPackageParseResult Success(OsPackageHeader header)
    {
        return new OsPackageParseResult(OsPackageStatus.Success, header);
    }

    public static OsPackageParseResult Fail(OsPackageStatus status)
    {
        return new OsPackageParseResult(status, default);
    }
}
