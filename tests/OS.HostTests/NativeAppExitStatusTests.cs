using System.Runtime.CompilerServices;
using System.Text;
using OS.Kernel.Runtime;
using OS.Pal;

internal static class NativeAppExitStatusTests
{

    [HostTest("appmodel.exit status formats success marker", HostLane.Fast, 0)]
    private static bool TestFormatsSuccessMarker()
    {
        NativeAppExitStatusCreateResult create =
            NativeAppExitStatusFormatter.CreateNormalExit(0, OSHostStatus.Ok);
        Span<byte> marker = stackalloc byte[NativeAppExitStatusFormatter.MaximumMarkerBytes];

        NativeAppExitMarkerResult format =
            NativeAppExitStatusFormatter.TryFormat(create.Record, marker);

        return HostCheck.Result(create.Succeeded
            && create.IsInitialized
            && format.Succeeded
            && format.IsInitialized
            && format.BytesWritten == format.RequiredBytes
            && ToAscii(marker[..format.BytesWritten]) ==
                "APP_EXIT code=0 status=OSHOST_STATUS_OK");
    }

    [HostTest("appmodel.exit status formats nonzero marker", HostLane.Fast, 1)]
    private static bool TestFormatsNonzeroMarker()
    {
        NativeAppExitStatusCreateResult create =
            NativeAppExitStatusFormatter.CreateNormalExit(7, OSHostStatus.InternalError);
        Span<byte> marker = stackalloc byte[NativeAppExitStatusFormatter.MaximumMarkerBytes];

        NativeAppExitMarkerResult format =
            NativeAppExitStatusFormatter.TryFormat(create.Record, marker);

        return HostCheck.Result(create.Succeeded
            && create.IsInitialized
            && !create.Record.LauncherFailure
            && format.Succeeded
            && format.IsInitialized
            && ToAscii(marker[..format.BytesWritten]) ==
                "APP_EXIT code=7 status=OSHOST_STATUS_INTERNAL_ERROR");
    }

    [HostTest("appmodel.exit status formats launcher failure", HostLane.Fast, 2)]
    private static bool TestFormatsLauncherFailure()
    {
        NativeAppExitStatusCreateResult create =
            NativeAppExitStatusFormatter.CreateLauncherFailure(OSHostStatus.NotSupported);
        Span<byte> marker = stackalloc byte[NativeAppExitStatusFormatter.MaximumMarkerBytes];

        NativeAppExitMarkerResult format =
            NativeAppExitStatusFormatter.TryFormat(create.Record, marker);

        return HostCheck.Result(create.Succeeded
            && create.IsInitialized
            && create.Record.LauncherFailure
            && create.Record.ExitCode == NativeAppExitStatusFormatter.MaximumExitCode
            && format.Succeeded
            && format.IsInitialized
            && ToAscii(marker[..format.BytesWritten]) ==
                "APP_EXIT code=255 status=OSHOST_STATUS_NOT_SUPPORTED");
    }

    [HostTest("appmodel.exit status rejects invalid input", HostLane.Fast, 3)]
    private static bool TestRejectsInvalidInput()
    {
        NativeAppExitStatusCreateResult negative =
            NativeAppExitStatusFormatter.CreateNormalExit(-1, OSHostStatus.Ok);
        NativeAppExitStatusCreateResult tooLarge =
            NativeAppExitStatusFormatter.CreateNormalExit(256, OSHostStatus.Ok);
        NativeAppExitStatusCreateResult invalidStatus =
            NativeAppExitStatusFormatter.CreateNormalExit(0, (OSHostStatus)999);
        NativeAppExitStatusCreateResult invalidLauncher =
            NativeAppExitStatusFormatter.CreateLauncherFailure(OSHostStatus.Ok);

        Span<byte> small = stackalloc byte[8];
        NativeAppExitMarkerResult tooSmall =
            NativeAppExitStatusFormatter.TryFormat(
                NativeAppExitStatusFormatter.CreateNormalExit(0, OSHostStatus.Ok).Record,
                small);

        return HostCheck.Result(negative.Status == NativeAppExitMarkerStatus.InvalidExitCode
            && negative.IsInitialized
            && tooLarge.Status == NativeAppExitMarkerStatus.InvalidExitCode
            && tooLarge.IsInitialized
            && invalidStatus.Status == NativeAppExitMarkerStatus.InvalidStatus
            && invalidStatus.IsInitialized
            && invalidLauncher.Status == NativeAppExitMarkerStatus.InvalidStatus
            && invalidLauncher.IsInitialized
            && tooSmall.Status == NativeAppExitMarkerStatus.DestinationTooSmall
            && tooSmall.IsInitialized
            && tooSmall.RequiredBytes > small.Length);
    }

    [HostTest("appmodel.exit status parser parses formatted marker", HostLane.Fast, 4)]
    private static bool TestParsesFormattedMarker()
    {
        Span<byte> successMarker = stackalloc byte[NativeAppExitStatusFormatter.MaximumMarkerBytes];
        NativeAppExitStatusCreateResult successCreate =
            NativeAppExitStatusFormatter.CreateNormalExit(0, OSHostStatus.Ok);
        NativeAppExitMarkerResult successFormat =
            NativeAppExitStatusFormatter.TryFormat(successCreate.Record, successMarker);
        NativeAppExitParseResult successParse =
            NativeAppExitStatusFormatter.TryParse(successMarker[..successFormat.BytesWritten]);

        Span<byte> nonzeroMarker = stackalloc byte[NativeAppExitStatusFormatter.MaximumMarkerBytes];
        NativeAppExitStatusCreateResult nonzeroCreate =
            NativeAppExitStatusFormatter.CreateNormalExit(7, OSHostStatus.InternalError);
        NativeAppExitMarkerResult nonzeroFormat =
            NativeAppExitStatusFormatter.TryFormat(nonzeroCreate.Record, nonzeroMarker);
        NativeAppExitParseResult nonzeroParse =
            NativeAppExitStatusFormatter.TryParse(nonzeroMarker[..nonzeroFormat.BytesWritten]);

        Span<byte> launcherMarker = stackalloc byte[NativeAppExitStatusFormatter.MaximumMarkerBytes];
        NativeAppExitStatusCreateResult launcherCreate =
            NativeAppExitStatusFormatter.CreateLauncherFailure(OSHostStatus.NotSupported);
        NativeAppExitMarkerResult launcherFormat =
            NativeAppExitStatusFormatter.TryFormat(launcherCreate.Record, launcherMarker);
        NativeAppExitParseResult launcherParse =
            NativeAppExitStatusFormatter.TryParse(launcherMarker[..launcherFormat.BytesWritten]);

        return HostCheck.Result(successCreate.Succeeded
            && successFormat.Succeeded
            && successParse.Succeeded
            && successParse.IsInitialized
            && successParse.BytesRead == successFormat.BytesWritten
            && successParse.Record.ExitCode == 0
            && successParse.Record.Status == OSHostStatus.Ok
            && !successParse.Record.LauncherFailure
            && nonzeroCreate.Succeeded
            && nonzeroFormat.Succeeded
            && nonzeroParse.Succeeded
            && nonzeroParse.Record.ExitCode == 7
            && nonzeroParse.Record.Status == OSHostStatus.InternalError
            && !nonzeroParse.Record.LauncherFailure
            && launcherCreate.Succeeded
            && launcherFormat.Succeeded
            && launcherParse.Succeeded
            && launcherParse.Record.ExitCode == NativeAppExitStatusFormatter.MaximumExitCode
            && launcherParse.Record.Status == OSHostStatus.NotSupported
            && launcherParse.Record.LauncherFailure);
    }

    [HostTest("appmodel.exit status parser rejects malformed marker", HostLane.Fast, 5)]
    private static bool TestParserRejectsMalformedMarker()
    {
        NativeAppExitParseResult invalidPrefix =
            ParseAscii("APP_DONE code=0 status=OSHOST_STATUS_OK");
        NativeAppExitParseResult fieldOrder =
            ParseAscii("APP_EXIT status=OSHOST_STATUS_OK code=0");
        NativeAppExitParseResult malformedCode =
            ParseAscii("APP_EXIT code=abc status=OSHOST_STATUS_OK");
        NativeAppExitParseResult codeTooLarge =
            ParseAscii("APP_EXIT code=256 status=OSHOST_STATUS_OK");
        NativeAppExitParseResult invalidStatus =
            ParseAscii("APP_EXIT code=0 status=OSHOST_STATUS_NOT_A_STATUS");
        NativeAppExitParseResult extraField =
            ParseAscii("APP_EXIT code=0 status=OSHOST_STATUS_OK extra=1");
        NativeAppExitParseResult truncatedPrefix =
            ParseAscii("APP_EXIT");
        NativeAppExitParseResult truncatedStatusField =
            ParseAscii("APP_EXIT code=0");
        NativeAppExitParseResult truncatedStatusValue =
            ParseAscii("APP_EXIT code=0 status=");

        return HostCheck.Result(invalidPrefix.IsInitialized
            && !invalidPrefix.Succeeded
            && invalidPrefix.Status == NativeAppExitParseStatus.InvalidPrefix
            && fieldOrder.Status == NativeAppExitParseStatus.InvalidFieldOrder
            && malformedCode.Status == NativeAppExitParseStatus.MalformedCode
            && codeTooLarge.Status == NativeAppExitParseStatus.MalformedCode
            && invalidStatus.Status == NativeAppExitParseStatus.InvalidStatusToken
            && extraField.Status == NativeAppExitParseStatus.ExtraFields
            && truncatedPrefix.Status == NativeAppExitParseStatus.TruncatedFields
            && truncatedStatusField.Status == NativeAppExitParseStatus.TruncatedFields
            && truncatedStatusValue.Status == NativeAppExitParseStatus.TruncatedFields);
    }

    [HostTest("appmodel.exit status parser accepts address status aliases", HostLane.Fast, 6)]
    private static bool TestParserAcceptsAddressStatusAliases()
    {
        NativeAppExitParseResult addrInUse =
            ParseAscii("APP_EXIT code=1 status=OSHOST_STATUS_ADDR_IN_USE");
        NativeAppExitParseResult addressInUse =
            ParseAscii("APP_EXIT code=1 status=OSHOST_STATUS_ADDRESS_IN_USE");
        NativeAppExitParseResult addrNotAvailable =
            ParseAscii("APP_EXIT code=1 status=OSHOST_STATUS_ADDR_NOT_AVAILABLE");
        NativeAppExitParseResult addressNotAvailable =
            ParseAscii("APP_EXIT code=1 status=OSHOST_STATUS_ADDRESS_NOT_AVAILABLE");

        return HostCheck.Result(addrInUse.Succeeded
            && addrInUse.Record.Status == OSHostStatus.AddressInUse
            && addressInUse.Succeeded
            && addressInUse.Record.Status == OSHostStatus.AddressInUse
            && addrNotAvailable.Succeeded
            && addrNotAvailable.Record.Status == OSHostStatus.AddressNotAvailable
            && addressNotAvailable.Succeeded
            && addressNotAvailable.Record.Status == OSHostStatus.AddressNotAvailable);
    }

    [HostTest("appmodel.exit status parser uses shared memory helpers", HostLane.Fast, 7)]
    private static bool TestParserUsesSharedMemoryHelpers()
    {
        string repositoryRoot = HostToolProcess.FindRepositoryRoot();
        string source = File.ReadAllText(
            Path.Combine(repositoryRoot, "src", "OS.Kernel", "Runtime", "NativeAppExitStatus.cs"));

        return HostCheck.Result(source.Contains(
                "OS.Std.MemoryExtensions.StartsWith(line, AppExitMarker)",
                StringComparison.Ordinal)
            && source.Contains(
                "OS.Std.MemoryExtensions.StartsWith(line, AppExitCodePrefix)",
                StringComparison.Ordinal)
            && source.Contains(
                "OS.Std.MemoryExtensions.StartsWith(statusField, AppExitStatusPrefix)",
                StringComparison.Ordinal)
            && source.Contains(
                "OS.Std.MemoryExtensions.SequenceEqual(token, \"OSHOST_STATUS_OK\"u8)",
                StringComparison.Ordinal)
            && source.Contains(
                "OS.Std.MemoryExtensions.SequenceEqual(token, \"OSHOST_STATUS_ADDR_IN_USE\"u8)",
                StringComparison.Ordinal)
            && source.Contains(
                "OS.Std.MemoryExtensions.SequenceEqual(token, \"OSHOST_STATUS_ADDRESS_IN_USE\"u8)",
                StringComparison.Ordinal)
            && source.Contains(
                "OS.Std.MemoryExtensions.SequenceEqual(token, \"OSHOST_STATUS_ADDR_NOT_AVAILABLE\"u8)",
                StringComparison.Ordinal)
            && source.Contains(
                "OS.Std.MemoryExtensions.SequenceEqual(token, \"OSHOST_STATUS_ADDRESS_NOT_AVAILABLE\"u8)",
                StringComparison.Ordinal)
            && !source.Contains(
                "private static bool StartsWith(ReadOnlySpan<byte>",
                StringComparison.Ordinal)
            && !source.Contains(
                "private static bool EqualsBytes(ReadOnlySpan<byte>",
                StringComparison.Ordinal));
    }

    [HostTest("appmodel.exit status formatter uses shared ascii text builder", HostLane.Fast, 8)]
    private static bool TestFormatterUsesSharedAsciiTextBuilder()
    {
        string repositoryRoot = HostToolProcess.FindRepositoryRoot();
        string source = File.ReadAllText(
            Path.Combine(repositoryRoot, "src", "OS.Kernel", "Runtime", "NativeAppExitStatus.cs"));

        return HostCheck.Result(source.Contains(
                "AsciiDecimalFormatter.CountInt32Digits(record.ExitCode)",
                StringComparison.Ordinal)
            && source.Contains(
                "AsciiTextBuilder.AppendChars(destination, offset, value)",
                StringComparison.Ordinal)
            && source.Contains(
                "AsciiTextBuilder.AppendUInt64Decimal(destination, offset, value)",
                StringComparison.Ordinal)
            && !source.Contains(
                "private static int CountDecimalDigits",
                StringComparison.Ordinal)
            && !source.Contains(
                "private static void WriteDecimal",
                StringComparison.Ordinal)
            && !source.Contains(
                "destination[cursor--] = (byte)('0' +",
                StringComparison.Ordinal));
    }

    [HostTest("appmodel.exit status create default result is not success", HostLane.Fast, 9)]
    private static bool TestCreateDefaultResultIsNotSuccess()
    {
        NativeAppExitStatusCreateResult result = default;

        return HostCheck.Result(!result.IsInitialized
            && !result.Succeeded
            && result.Status == NativeAppExitMarkerStatus.Success
            && result.Record.ExitCode == 0
            && result.Record.Status == OSHostStatus.Ok
            && !result.Record.LauncherFailure
            && !RuntimeHelpers.IsReferenceOrContainsReferences<NativeAppExitStatusCreateResult>());
    }

    [HostTest("appmodel.exit status marker default result is not success", HostLane.Fast, 10)]
    private static bool TestMarkerDefaultResultIsNotSuccess()
    {
        NativeAppExitMarkerResult result = default;

        return HostCheck.Result(!result.IsInitialized
            && !result.Succeeded
            && result.Status == NativeAppExitMarkerStatus.Success
            && result.BytesWritten == 0
            && result.RequiredBytes == 0
            && result.LogStatus == OSHostStatus.Ok
            && !RuntimeHelpers.IsReferenceOrContainsReferences<NativeAppExitMarkerResult>());
    }

    [HostTest("appmodel.exit status parser default result is not success", HostLane.Fast, 11)]
    private static bool TestParserDefaultResultIsNotSuccess()
    {
        NativeAppExitParseResult result = default;

        return HostCheck.Result(!result.IsInitialized
            && !result.Succeeded
            && result.Status == NativeAppExitParseStatus.Success
            && result.Record.ExitCode == 0
            && result.Record.Status == OSHostStatus.Ok
            && !result.Record.LauncherFailure
            && result.BytesRead == 0
            && !RuntimeHelpers.IsReferenceOrContainsReferences<NativeAppExitParseResult>());
    }

    [HostTest("appmodel.exit status writes marker to app log", HostLane.Fast, 12)]
    private static bool TestWritesMarkerToAppLog()
    {
        OSHostAppLogTable table = new(recordCapacity: 1, maxRecordBytes: 96);
        NativeAppExitStatusCreateResult create =
            NativeAppExitStatusFormatter.CreateNormalExit(0, OSHostStatus.Ok);

        NativeAppExitMarkerResult write =
            NativeAppExitStatusFormatter.WriteToLog(create.Record, table);

        Span<byte> marker = stackalloc byte[write.RequiredBytes];
        OSHostAppLogReadResult read = table.TryCopyRecord(0, marker);

        return HostCheck.Result(write.Succeeded
            && create.IsInitialized
            && write.IsInitialized
            && write.LogStatus == OSHostStatus.Ok
            && table.Count == 1
            && read.Succeeded
            && read.StreamId == OSHostAppLogConstants.StreamStdout
            && read.Flags == OSHostAppLogConstants.FlagMachineReadable
            && ToAscii(marker[..(int)read.BytesCopied]) ==
                "APP_EXIT code=0 status=OSHOST_STATUS_OK");
    }

    private static string ToAscii(ReadOnlySpan<byte> bytes)
    {
        return Encoding.ASCII.GetString(bytes);
    }

    private static NativeAppExitParseResult ParseAscii(string value)
    {
        return NativeAppExitStatusFormatter.TryParse(Encoding.ASCII.GetBytes(value));
    }
}
