using OS.Std.Binary;
using OS.Std.Memory;

namespace OS.Storage;

public readonly struct OsPackageFileLookupResult
{
    private OsPackageFileLookupResult(
        OsPackageStatus status,
        OsPackageHeader header,
        OsPackageFileEntry entry)
    {
        IsInitialized = true;
        Status = status;
        Header = header;
        Entry = entry;
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

    public bool Succeeded => IsInitialized && Status == OsPackageStatus.Success;

    public static OsPackageFileLookupResult Success(OsPackageHeader header, OsPackageFileEntry entry)
    {
        return new OsPackageFileLookupResult(OsPackageStatus.Success, header, entry);
    }

    public static OsPackageFileLookupResult Fail(OsPackageStatus status)
    {
        return new OsPackageFileLookupResult(status, default, default);
    }

    public static OsPackageFileLookupResult Fail(OsPackageStatus status, OsPackageHeader header)
    {
        return new OsPackageFileLookupResult(status, header, default);
    }
}
