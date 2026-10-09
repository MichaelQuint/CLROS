using System.IO;
using System.Runtime.CompilerServices;
using System.Text;
using OS.Pal;

internal static class PalStatusContractTests
{

    [HostTest("pal.status_contract validates config and environment statuses", HostLane.Fast, 0)]
    private static bool TestConfigAndEnvironmentStatusSets()
    {
        OSHostStatusContractResult configBuffer =
            OSHostStatusContract.Validate(OSHostAbiStatusDomain.Config, OSHostStatus.BufferTooSmall);
        OSHostStatusContractResult configWouldBlock =
            OSHostStatusContract.Validate(OSHostAbiStatusDomain.Config, OSHostStatus.WouldBlock);
        OSHostStatusContractResult environmentNotFound =
            OSHostStatusContract.Validate(OSHostAbiStatusDomain.Environment, OSHostStatus.NotFound);
        OSHostStatusContractResult environmentConnectionReset =
            OSHostStatusContract.Validate(OSHostAbiStatusDomain.Environment, OSHostStatus.ConnectionReset);

        return HostCheck.Result(configBuffer.Succeeded
            && configBuffer.IsAllowed
            && configBuffer.Category == OSHostStatusCategory.CallerError
            && (configBuffer.AllowedStatusMask & (1UL << (int)OSHostStatus.BufferTooSmall)) != 0
            && configWouldBlock.IsInitialized
            && !configWouldBlock.Succeeded
            && !configWouldBlock.IsAllowed
            && configWouldBlock.Status == OSHostStatus.NotSupported
            && environmentNotFound.Succeeded
            && environmentNotFound.Domain == OSHostAbiStatusDomain.Environment
            && environmentConnectionReset.IsInitialized
            && !environmentConnectionReset.Succeeded
            && environmentConnectionReset.Status == OSHostStatus.NotSupported);
    }

    [HostTest("pal.status_contract validates time and console statuses", HostLane.Fast, 1)]
    private static bool TestTimeAndConsoleStatusSets()
    {
        OSHostStatusContractResult timeNotSupported =
            OSHostStatusContract.Validate(OSHostAbiStatusDomain.Time, OSHostStatus.NotSupported);
        OSHostStatusContractResult timeTimedOut =
            OSHostStatusContract.Validate(OSHostAbiStatusDomain.Time, OSHostStatus.TimedOut);
        OSHostStatusContractResult timeAddressInUse =
            OSHostStatusContract.Validate(OSHostAbiStatusDomain.Time, OSHostStatus.AddressInUse);
        OSHostStatusContractResult consoleOutOfMemory =
            OSHostStatusContract.Validate(OSHostAbiStatusDomain.ConsoleLog, OSHostStatus.OutOfMemory);
        OSHostStatusContractResult consoleNetworkDown =
            OSHostStatusContract.Validate(OSHostAbiStatusDomain.ConsoleLog, OSHostStatus.NetworkDown);

        return HostCheck.Result(timeNotSupported.Succeeded
            && timeNotSupported.Category == OSHostStatusCategory.Unsupported
            && timeTimedOut.Succeeded
            && timeTimedOut.Category == OSHostStatusCategory.RuntimeUnavailable
            && !timeAddressInUse.Succeeded
            && timeAddressInUse.Status == OSHostStatus.NotSupported
            && consoleOutOfMemory.Succeeded
            && consoleOutOfMemory.Category == OSHostStatusCategory.ResourceError
            && !consoleNetworkDown.Succeeded
            && consoleNetworkDown.Status == OSHostStatus.NotSupported);
    }

    [HostTest("pal.status_contract validates memory package and socket statuses", HostLane.Fast, 2)]
    private static bool TestMemoryPackageAndSocketStatusSets()
    {
        OSHostStatusContractResult memoryAccessDenied =
            OSHostStatusContract.Validate(OSHostAbiStatusDomain.Memory, OSHostStatus.AccessDenied);
        OSHostStatusContractResult memoryWouldBlock =
            OSHostStatusContract.Validate(OSHostAbiStatusDomain.Memory, OSHostStatus.WouldBlock);
        OSHostStatusContractResult packageEndOfFile =
            OSHostStatusContract.Validate(OSHostAbiStatusDomain.PackageFile, OSHostStatus.EndOfFile);
        OSHostStatusContractResult packageTimedOut =
            OSHostStatusContract.Validate(OSHostAbiStatusDomain.PackageFile, OSHostStatus.TimedOut);
        OSHostStatusContractResult socketAddressInUse =
            OSHostStatusContract.Validate(OSHostAbiStatusDomain.Socket, OSHostStatus.AddressInUse);
        OSHostStatusContractResult socketNetworkDown =
            OSHostStatusContract.Validate(OSHostAbiStatusDomain.Socket, OSHostStatus.NetworkDown);

        return HostCheck.Result(memoryAccessDenied.Succeeded
            && memoryAccessDenied.Category == OSHostStatusCategory.CallerError
            && (memoryAccessDenied.AllowedStatusMask & (1UL << (int)OSHostStatus.AccessDenied)) != 0
            && memoryWouldBlock.IsInitialized
            && !memoryWouldBlock.Succeeded
            && memoryWouldBlock.Status == OSHostStatus.NotSupported
            && packageEndOfFile.Succeeded
            && packageEndOfFile.Category == OSHostStatusCategory.ResourceError
            && packageEndOfFile.Domain == OSHostAbiStatusDomain.PackageFile
            && packageTimedOut.IsInitialized
            && !packageTimedOut.Succeeded
            && packageTimedOut.Status == OSHostStatus.NotSupported
            && socketAddressInUse.Succeeded
            && socketAddressInUse.Category == OSHostStatusCategory.CallerError
            && socketNetworkDown.Succeeded
            && socketNetworkDown.Category == OSHostStatusCategory.RuntimeUnavailable);
    }

    [HostTest("pal.status_contract formats domain names", HostLane.Fast, 3)]
    private static bool TestFormatsDomainNames()
    {
        Span<byte> destination = stackalloc byte[64];
        destination.Fill(0);

        OSHostStatusDomainNameFormatResult format =
            OSHostStatusContract.FormatDomainName(OSHostAbiStatusDomain.ConsoleLog, destination);
        string actualName = Encoding.ASCII.GetString(destination[..(int)format.BytesWritten]);

        OSHostStatusDomainNameFormatResult query =
            OSHostStatusContract.QueryDomainNameRequiredBytes(OSHostAbiStatusDomain.Environment);

        Span<byte> shortDestination = stackalloc byte[3];
        shortDestination.Fill(0xCC);
        OSHostStatusDomainNameFormatResult shortResult =
            OSHostStatusContract.FormatDomainName(OSHostAbiStatusDomain.Time, shortDestination);

        return HostCheck.Result(format.Succeeded
            && actualName == "OSHOST_ABI_DOMAIN_CONSOLE_LOG"
            && query.IsInitialized
            && !query.Succeeded
            && query.Status == OSHostStatus.Ok
            && query.RequiredBytes == (uint)"OSHOST_ABI_DOMAIN_ENVIRONMENT".Length
            && shortResult.IsInitialized
            && !shortResult.Succeeded
            && shortResult.Status == OSHostStatus.BufferTooSmall
            && shortResult.RequiredBytes == (uint)"OSHOST_ABI_DOMAIN_TIME".Length
            && shortDestination[0] == 0xCC
            && shortDestination[1] == 0xCC
            && shortDestination[2] == 0xCC);
    }

    [HostTest("pal.status_contract formats extended domain names", HostLane.Fast, 4)]
    private static bool TestFormatsExtendedDomainNames()
    {
        Span<byte> destination = stackalloc byte[64];
        destination.Fill(0);

        OSHostStatusDomainNameFormatResult format =
            OSHostStatusContract.FormatDomainName(OSHostAbiStatusDomain.PackageFile, destination);
        string actualName = Encoding.ASCII.GetString(destination[..(int)format.BytesWritten]);

        OSHostStatusDomainNameFormatResult query =
            OSHostStatusContract.QueryDomainNameRequiredBytes(OSHostAbiStatusDomain.Socket);

        Span<byte> shortDestination = stackalloc byte[4];
        shortDestination.Fill(0xA5);
        OSHostStatusDomainNameFormatResult shortResult =
            OSHostStatusContract.FormatDomainName(OSHostAbiStatusDomain.Memory, shortDestination);

        return HostCheck.Result(format.Succeeded
            && actualName == "OSHOST_ABI_DOMAIN_PACKAGE_FILE"
            && query.IsInitialized
            && !query.Succeeded
            && query.Status == OSHostStatus.Ok
            && query.RequiredBytes == (uint)"OSHOST_ABI_DOMAIN_SOCKET".Length
            && shortResult.IsInitialized
            && !shortResult.Succeeded
            && shortResult.Status == OSHostStatus.BufferTooSmall
            && shortResult.RequiredBytes == (uint)"OSHOST_ABI_DOMAIN_MEMORY".Length
            && shortDestination[0] == 0xA5
            && shortDestination[1] == 0xA5
            && shortDestination[2] == 0xA5
            && shortDestination[3] == 0xA5);
    }

    [HostTest("pal.status_contract rejects malformed inputs without throwing", HostLane.Fast, 5)]
    private static bool TestRejectsMalformedInputs()
    {
        OSHostStatusContractResult unknownDomain =
            OSHostStatusContract.Validate((OSHostAbiStatusDomain)99, OSHostStatus.Ok);
        OSHostStatusContractResult unknownStatus =
            OSHostStatusContract.Validate(OSHostAbiStatusDomain.Config, (OSHostStatus)999);
        OSHostStatusDomainNameFormatResult unknownDomainName =
            OSHostStatusContract.QueryDomainNameRequiredBytes((OSHostAbiStatusDomain)(-1));

        return HostCheck.Result(unknownDomain.IsInitialized
            && !unknownDomain.Succeeded
            && unknownDomain.Status == OSHostStatus.InvalidArgument
            && unknownDomain.AllowedStatusMask == 0
            && unknownStatus.IsInitialized
            && !unknownStatus.Succeeded
            && unknownStatus.Status == OSHostStatus.InvalidArgument
            && unknownStatus.Category == OSHostStatusCategory.Unknown
            && unknownDomainName.IsInitialized
            && !unknownDomainName.Succeeded
            && unknownDomainName.Status == OSHostStatus.InvalidArgument
            && unknownDomainName.RequiredBytes == 0);
    }

    [HostTest("pal.status_contract result layouts contain no managed references", HostLane.Fast, 6)]
    private static bool TestResultLayoutsContainNoManagedReferences()
    {
        return HostCheck.Result(!RuntimeHelpers.IsReferenceOrContainsReferences<OSHostStatusContractResult>()
            && !RuntimeHelpers.IsReferenceOrContainsReferences<OSHostStatusDomainNameFormatResult>());
    }

    [HostTest("pal.status_contract default results are not success", HostLane.Fast, 7)]
    private static bool TestDefaultResultsAreNotSuccess()
    {
        OSHostStatusContractResult statusResult = default;
        OSHostStatusDomainNameFormatResult nameResult = default;

        return HostCheck.Result(!statusResult.IsInitialized
            && !statusResult.IsAllowed
            && !statusResult.Succeeded
            && statusResult.Status == OSHostStatus.Ok
            && statusResult.AllowedStatusMask == 0
            && statusResult.Flags == 0
            && !nameResult.IsInitialized
            && !nameResult.Succeeded
            && nameResult.Status == OSHostStatus.Ok
            && nameResult.BytesWritten == 0
            && nameResult.RequiredBytes == 0
            && nameResult.Flags == 0);
    }

    [HostTest("pal.status_contract domain formatter uses shared ascii text builder", HostLane.Fast, 8)]
    private static bool TestDomainFormatterUsesSharedAsciiTextBuilder()
    {
        string repositoryRoot = HostToolProcess.FindRepositoryRoot();
        string source = File.ReadAllText(
            Path.Combine(repositoryRoot, "src", "OS.Kernel", "Pal", "OSHostStatusContract.cs"));

        return HostCheck.Result(source.Contains("using OS.Std.Text;", StringComparison.Ordinal)
            && source.Contains(
                "AsciiTextBuilder.AppendBytes(destination, 0, name)",
                StringComparison.Ordinal)
            && !source.Contains("name.CopyTo(destination)", StringComparison.Ordinal));
    }
}
