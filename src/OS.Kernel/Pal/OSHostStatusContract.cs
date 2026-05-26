using OS.Std.Text;

namespace OS.Pal;

public enum OSHostAbiStatusDomain : int
{
    Config = 0,
    Environment = 1,
    Time = 2,
    ConsoleLog = 3,
    Memory = 4,
    PackageFile = 5,
    Socket = 6
}

public static class OSHostStatusContract
{
    private const ulong OkBit = 1UL << (int)OSHostStatus.Ok;

    private const ulong InvalidArgumentBit = 1UL << (int)OSHostStatus.InvalidArgument;

    private const ulong NotSupportedBit = 1UL << (int)OSHostStatus.NotSupported;

    private const ulong NotFoundBit = 1UL << (int)OSHostStatus.NotFound;

    private const ulong AccessDeniedBit = 1UL << (int)OSHostStatus.AccessDenied;

    private const ulong BufferTooSmallBit = 1UL << (int)OSHostStatus.BufferTooSmall;

    private const ulong OutOfMemoryBit = 1UL << (int)OSHostStatus.OutOfMemory;

    private const ulong WouldBlockBit = 1UL << (int)OSHostStatus.WouldBlock;

    private const ulong TimedOutBit = 1UL << (int)OSHostStatus.TimedOut;

    private const ulong InterruptedBit = 1UL << (int)OSHostStatus.Interrupted;

    private const ulong EndOfFileBit = 1UL << (int)OSHostStatus.EndOfFile;

    private const ulong ConnectionResetBit = 1UL << (int)OSHostStatus.ConnectionReset;

    private const ulong ConnectionRefusedBit = 1UL << (int)OSHostStatus.ConnectionRefused;

    private const ulong AddressInUseBit = 1UL << (int)OSHostStatus.AddressInUse;

    private const ulong AddressNotAvailableBit = 1UL << (int)OSHostStatus.AddressNotAvailable;

    private const ulong NetworkDownBit = 1UL << (int)OSHostStatus.NetworkDown;

    private const ulong TryAgainBit = 1UL << (int)OSHostStatus.TryAgain;

    private const ulong IoErrorBit = 1UL << (int)OSHostStatus.IoError;

    private const ulong InternalErrorBit = 1UL << (int)OSHostStatus.InternalError;

    private const ulong ClosedBit = 1UL << (int)OSHostStatus.Closed;

    private const ulong ConfigStatusMask =
        OkBit
        | InvalidArgumentBit
        | NotFoundBit
        | BufferTooSmallBit
        | OutOfMemoryBit
        | InternalErrorBit;

    private const ulong EnvironmentStatusMask = ConfigStatusMask;

    private const ulong TimeStatusMask =
        OkBit
        | InvalidArgumentBit
        | NotSupportedBit
        | OutOfMemoryBit
        | WouldBlockBit
        | TimedOutBit
        | InterruptedBit
        | InternalErrorBit
        | ClosedBit;

    private const ulong ConsoleLogStatusMask =
        OkBit
        | InvalidArgumentBit
        | BufferTooSmallBit
        | OutOfMemoryBit
        | InterruptedBit
        | InternalErrorBit
        | ClosedBit;

    private const ulong MemoryStatusMask =
        OkBit
        | InvalidArgumentBit
        | NotSupportedBit
        | NotFoundBit
        | AccessDeniedBit
        | BufferTooSmallBit
        | OutOfMemoryBit
        | InternalErrorBit;

    private const ulong PackageFileStatusMask =
        OkBit
        | InvalidArgumentBit
        | NotSupportedBit
        | NotFoundBit
        | AccessDeniedBit
        | BufferTooSmallBit
        | OutOfMemoryBit
        | EndOfFileBit
        | IoErrorBit
        | InternalErrorBit
        | ClosedBit;

    private const ulong SocketStatusMask =
        OkBit
        | InvalidArgumentBit
        | NotSupportedBit
        | NotFoundBit
        | AccessDeniedBit
        | BufferTooSmallBit
        | OutOfMemoryBit
        | WouldBlockBit
        | TimedOutBit
        | InterruptedBit
        | EndOfFileBit
        | ConnectionResetBit
        | ConnectionRefusedBit
        | AddressInUseBit
        | AddressNotAvailableBit
        | NetworkDownBit
        | TryAgainBit
        | IoErrorBit
        | InternalErrorBit
        | ClosedBit;

    public static OSHostStatusContractResult Validate(
        OSHostAbiStatusDomain domain,
        OSHostStatus status)
    {
        ulong mask = GetAllowedStatusMask(domain);
        if (mask == 0)
        {
            return new OSHostStatusContractResult(
                OSHostStatus.InvalidArgument,
                domain,
                status,
                OSHostStatusCategory.Unknown,
                allowedStatusMask: 0,
                allowed: false);
        }

        OSHostStatusCategory category = OSHostStatusCategoryFormatter.Categorize(status);
        if (!IsCanonicalStatus(status))
        {
            return new OSHostStatusContractResult(
                OSHostStatus.InvalidArgument,
                domain,
                status,
                category,
                mask,
                allowed: false);
        }

        bool allowed = (mask & StatusBit(status)) != 0;
        return new OSHostStatusContractResult(
            allowed ? OSHostStatus.Ok : OSHostStatus.NotSupported,
            domain,
            status,
            category,
            mask,
            allowed);
    }

    public static OSHostStatusDomainNameFormatResult FormatDomainName(
        OSHostAbiStatusDomain domain,
        Span<byte> destination)
    {
        ReadOnlySpan<byte> name = GetCanonicalDomainName(domain);
        if (name.Length == 0)
        {
            return OSHostStatusDomainNameFormatResult.FromStatus(
                OSHostStatus.InvalidArgument,
                domain);
        }

        if (destination.Length < name.Length)
        {
            return new OSHostStatusDomainNameFormatResult(
                OSHostStatus.BufferTooSmall,
                domain,
                bytesWritten: 0,
                requiredBytes: (uint)name.Length);
        }

        AsciiTextBuilderResult builderResult = AsciiTextBuilder.AppendBytes(destination, 0, name);
        if (!builderResult.Succeeded)
        {
            return OSHostStatusDomainNameFormatResult.FromStatus(
                OSHostStatus.InvalidArgument,
                domain);
        }

        return new OSHostStatusDomainNameFormatResult(
            OSHostStatus.Ok,
            domain,
            bytesWritten: (uint)builderResult.BytesWritten,
            requiredBytes: (uint)name.Length);
    }

    public static OSHostStatusDomainNameFormatResult QueryDomainNameRequiredBytes(
        OSHostAbiStatusDomain domain)
    {
        ReadOnlySpan<byte> name = GetCanonicalDomainName(domain);
        if (name.Length == 0)
        {
            return OSHostStatusDomainNameFormatResult.FromStatus(
                OSHostStatus.InvalidArgument,
                domain);
        }

        return new OSHostStatusDomainNameFormatResult(
            OSHostStatus.Ok,
            domain,
            bytesWritten: 0,
            requiredBytes: (uint)name.Length);
    }

    public static ulong GetAllowedStatusMask(OSHostAbiStatusDomain domain)
    {
        return domain switch
        {
            OSHostAbiStatusDomain.Config => ConfigStatusMask,
            OSHostAbiStatusDomain.Environment => EnvironmentStatusMask,
            OSHostAbiStatusDomain.Time => TimeStatusMask,
            OSHostAbiStatusDomain.ConsoleLog => ConsoleLogStatusMask,
            OSHostAbiStatusDomain.Memory => MemoryStatusMask,
            OSHostAbiStatusDomain.PackageFile => PackageFileStatusMask,
            OSHostAbiStatusDomain.Socket => SocketStatusMask,
            _ => 0
        };
    }

    private static ReadOnlySpan<byte> GetCanonicalDomainName(OSHostAbiStatusDomain domain)
    {
        return domain switch
        {
            OSHostAbiStatusDomain.Config => "OSHOST_ABI_DOMAIN_CONFIG"u8,
            OSHostAbiStatusDomain.Environment => "OSHOST_ABI_DOMAIN_ENVIRONMENT"u8,
            OSHostAbiStatusDomain.Time => "OSHOST_ABI_DOMAIN_TIME"u8,
            OSHostAbiStatusDomain.ConsoleLog => "OSHOST_ABI_DOMAIN_CONSOLE_LOG"u8,
            OSHostAbiStatusDomain.Memory => "OSHOST_ABI_DOMAIN_MEMORY"u8,
            OSHostAbiStatusDomain.PackageFile => "OSHOST_ABI_DOMAIN_PACKAGE_FILE"u8,
            OSHostAbiStatusDomain.Socket => "OSHOST_ABI_DOMAIN_SOCKET"u8,
            _ => ReadOnlySpan<byte>.Empty
        };
    }

    private static bool IsCanonicalStatus(OSHostStatus status)
    {
        return status >= OSHostStatus.Ok && status <= OSHostStatus.Closed;
    }

    private static ulong StatusBit(OSHostStatus status)
    {
        return 1UL << (int)status;
    }
}

public readonly struct OSHostStatusContractResult
{
    private const uint InitializedFlag = 1;

    private const uint AllowedFlag = 2;

    public readonly OSHostStatus Status;

    public readonly OSHostAbiStatusDomain Domain;

    public readonly OSHostStatus CandidateStatus;

    public readonly OSHostStatusCategory Category;

    public readonly ulong AllowedStatusMask;

    public readonly uint Flags;

    public OSHostStatusContractResult(
        OSHostStatus status,
        OSHostAbiStatusDomain domain,
        OSHostStatus candidateStatus,
        OSHostStatusCategory category,
        ulong allowedStatusMask,
        bool allowed,
        uint flags = InitializedFlag)
    {
        Status = status;
        Domain = domain;
        CandidateStatus = candidateStatus;
        Category = category;
        AllowedStatusMask = allowedStatusMask;
        Flags = allowed ? flags | AllowedFlag : flags;
    }

    public bool IsInitialized => (Flags & InitializedFlag) != 0;

    public bool IsAllowed => (Flags & AllowedFlag) != 0;

    public bool Succeeded => IsInitialized && Status == OSHostStatus.Ok && IsAllowed;
}

public readonly struct OSHostStatusDomainNameFormatResult
{
    private const uint InitializedFlag = 1;

    public readonly OSHostStatus Status;

    public readonly OSHostAbiStatusDomain Domain;

    public readonly uint BytesWritten;

    public readonly uint RequiredBytes;

    public readonly uint Flags;

    public OSHostStatusDomainNameFormatResult(
        OSHostStatus status,
        OSHostAbiStatusDomain domain,
        uint bytesWritten,
        uint requiredBytes,
        uint flags = InitializedFlag)
    {
        Status = status;
        Domain = domain;
        BytesWritten = bytesWritten;
        RequiredBytes = requiredBytes;
        Flags = flags;
    }

    public bool IsInitialized => (Flags & InitializedFlag) != 0;

    public bool Succeeded => IsInitialized
        && Status == OSHostStatus.Ok
        && RequiredBytes != 0
        && BytesWritten == RequiredBytes;

    public static OSHostStatusDomainNameFormatResult FromStatus(
        OSHostStatus status,
        OSHostAbiStatusDomain domain)
    {
        return new OSHostStatusDomainNameFormatResult(
            status,
            domain,
            bytesWritten: 0,
            requiredBytes: 0);
    }
}
