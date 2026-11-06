using OS.Std.Text;

namespace OS.Storage;

public static class VirtioBlockPackageFileStagingPreflightDiagnosticFormatter
{
    public static VirtioBlockPackageFileStagingPreflightDiagnosticFormatResult FormatResult(
        in VirtioBlockPackageFileStagingPreflightResult preflight,
        Span<byte> destination)
    {
        VirtioBlockPackageFileStagingPreflightDiagnosticFormatStatus validationStatus =
            Validate(in preflight);
        if (validationStatus != VirtioBlockPackageFileStagingPreflightDiagnosticFormatStatus.Success)
        {
            return VirtioBlockPackageFileStagingPreflightDiagnosticFormatResult.Fail(
                validationStatus,
                preflight.Status,
                requiredBytes: 0,
                destination.Length);
        }

        int requiredBytes = GetRequiredBytes(in preflight);
        if (destination.Length < requiredBytes)
        {
            return VirtioBlockPackageFileStagingPreflightDiagnosticFormatResult.Fail(
                VirtioBlockPackageFileStagingPreflightDiagnosticFormatStatus.DestinationTooSmall,
                preflight.Status,
                requiredBytes,
                destination.Length);
        }

        int offset = 0;
        AppendBytes("APP_PACKAGE_FILE_STAGING_PREFLIGHT status="u8, destination, ref offset);
        AppendBytes(GetPreflightStatusName(preflight.Status), destination, ref offset);
        AppendBytes(" readSession="u8, destination, ref offset);
        AppendBytes(GetReadSessionStatusName(preflight.ReadSessionStatus), destination, ref offset);
        AppendBytes(" pipeline="u8, destination, ref offset);
        AppendBytes(GetPipelineStatusName(preflight.ReadSessionPipelineStatus), destination, ref offset);
        AppendBytes(" packagePreflight="u8, destination, ref offset);
        AppendBytes(GetPackagePreflightStatusName(preflight.PackagePreflightStatus), destination, ref offset);
        AppendBytes(" packageOpenInitialized="u8, destination, ref offset);
        AppendBool(preflight.PackageOpen.IsInitialized, destination, ref offset);
        AppendBytes(" packageOpen="u8, destination, ref offset);
        AppendBytes(GetPackageOpenStatusName(preflight.PackageOpenStatus), destination, ref offset);
        AppendBytes(" packageStagingInitialized="u8, destination, ref offset);
        AppendBool(preflight.PackageStaging.IsInitialized, destination, ref offset);
        AppendBytes(" packageFileStaging="u8, destination, ref offset);
        AppendBytes(GetPackageStagingStatusName(preflight.PackageStagingStatus), destination, ref offset);
        AppendBytes(" readSessionBytes="u8, destination, ref offset);
        AppendUInt64Decimal(preflight.ReadSessionByteCount, destination, ref offset);
        AppendBytes(" packageBytes="u8, destination, ref offset);
        AppendUInt64Decimal(preflight.PackageBytesRead, destination, ref offset);
        AppendBytes(" stagedBytes="u8, destination, ref offset);
        AppendUInt64Decimal(preflight.PackageFileBytesStaged, destination, ref offset);
        AppendBytes(" stagingCapacity="u8, destination, ref offset);
        AppendUInt64Decimal(preflight.StagingDestinationBytesAvailable, destination, ref offset);
        AppendBytes(" sourceFileBytes="u8, destination, ref offset);
        AppendUInt64Decimal(preflight.PackageStaging.FileLength, destination, ref offset);
        AppendBytes(" requiredBytes="u8, destination, ref offset);
        AppendUInt64Decimal(preflight.PackageStaging.RequiredBytes, destination, ref offset);
        AppendBytes(" sourceRangeOverflow="u8, destination, ref offset);
        AppendBool(preflight.PackageStaging.SourceRangeOverflow, destination, ref offset);

        return new VirtioBlockPackageFileStagingPreflightDiagnosticFormatResult(
            VirtioBlockPackageFileStagingPreflightDiagnosticFormatStatus.Success,
            preflight.Status,
            bytesWritten: offset,
            requiredBytes,
            availableBytes: destination.Length);
    }

    public static VirtioBlockPackageFileStagingPreflightDiagnosticFormatResult QueryRequiredBytes(
        in VirtioBlockPackageFileStagingPreflightResult preflight)
    {
        VirtioBlockPackageFileStagingPreflightDiagnosticFormatStatus validationStatus =
            Validate(in preflight);
        if (validationStatus != VirtioBlockPackageFileStagingPreflightDiagnosticFormatStatus.Success)
        {
            return VirtioBlockPackageFileStagingPreflightDiagnosticFormatResult.Fail(
                validationStatus,
                preflight.Status,
                requiredBytes: 0,
                availableBytes: 0);
        }

        return new VirtioBlockPackageFileStagingPreflightDiagnosticFormatResult(
            VirtioBlockPackageFileStagingPreflightDiagnosticFormatStatus.Success,
            preflight.Status,
            bytesWritten: 0,
            requiredBytes: GetRequiredBytes(in preflight),
            availableBytes: 0);
    }

    private static VirtioBlockPackageFileStagingPreflightDiagnosticFormatStatus Validate(
        in VirtioBlockPackageFileStagingPreflightResult preflight)
    {
        if (!preflight.IsInitialized ||
            GetPreflightStatusName(preflight.Status).Length == 0 ||
            GetReadSessionStatusName(preflight.ReadSessionStatus).Length == 0 ||
            GetPipelineStatusName(preflight.ReadSessionPipelineStatus).Length == 0 ||
            GetPackagePreflightStatusName(preflight.PackagePreflightStatus).Length == 0 ||
            GetPackageOpenStatusName(preflight.PackageOpenStatus).Length == 0 ||
            GetPackageStagingStatusName(preflight.PackageStagingStatus).Length == 0)
        {
            return VirtioBlockPackageFileStagingPreflightDiagnosticFormatStatus.InvalidResult;
        }

        return VirtioBlockPackageFileStagingPreflightDiagnosticFormatStatus.Success;
    }

    private static int GetRequiredBytes(in VirtioBlockPackageFileStagingPreflightResult preflight)
    {
        return "APP_PACKAGE_FILE_STAGING_PREFLIGHT status="u8.Length
            + GetPreflightStatusName(preflight.Status).Length
            + " readSession="u8.Length
            + GetReadSessionStatusName(preflight.ReadSessionStatus).Length
            + " pipeline="u8.Length
            + GetPipelineStatusName(preflight.ReadSessionPipelineStatus).Length
            + " packagePreflight="u8.Length
            + GetPackagePreflightStatusName(preflight.PackagePreflightStatus).Length
            + " packageOpenInitialized="u8.Length
            + GetBoolByteCount(preflight.PackageOpen.IsInitialized)
            + " packageOpen="u8.Length
            + GetPackageOpenStatusName(preflight.PackageOpenStatus).Length
            + " packageStagingInitialized="u8.Length
            + GetBoolByteCount(preflight.PackageStaging.IsInitialized)
            + " packageFileStaging="u8.Length
            + GetPackageStagingStatusName(preflight.PackageStagingStatus).Length
            + " readSessionBytes="u8.Length
            + AsciiDecimalFormatter.CountUInt64Digits(preflight.ReadSessionByteCount)
            + " packageBytes="u8.Length
            + AsciiDecimalFormatter.CountUInt64Digits(preflight.PackageBytesRead)
            + " stagedBytes="u8.Length
            + AsciiDecimalFormatter.CountUInt64Digits(preflight.PackageFileBytesStaged)
            + " stagingCapacity="u8.Length
            + AsciiDecimalFormatter.CountUInt64Digits(preflight.StagingDestinationBytesAvailable)
            + " sourceFileBytes="u8.Length
            + AsciiDecimalFormatter.CountUInt64Digits(preflight.PackageStaging.FileLength)
            + " requiredBytes="u8.Length
            + AsciiDecimalFormatter.CountUInt64Digits(preflight.PackageStaging.RequiredBytes)
            + " sourceRangeOverflow="u8.Length
            + GetBoolByteCount(preflight.PackageStaging.SourceRangeOverflow);
    }

    private static ReadOnlySpan<byte> GetPreflightStatusName(
        VirtioBlockPackageFileStagingPreflightStatus status)
    {
        return status switch
        {
            VirtioBlockPackageFileStagingPreflightStatus.Success => "Success"u8,
            VirtioBlockPackageFileStagingPreflightStatus.PackagePreflightFailed =>
                "PackagePreflightFailed"u8,
            VirtioBlockPackageFileStagingPreflightStatus.PackageOpenFailed =>
                "PackageOpenFailed"u8,
            VirtioBlockPackageFileStagingPreflightStatus.PackageStagingFailed =>
                "PackageStagingFailed"u8,
            _ => ReadOnlySpan<byte>.Empty
        };
    }

    private static ReadOnlySpan<byte> GetReadSessionStatusName(
        VirtioBlockReadOnlyReadSessionStatus status)
    {
        return status switch
        {
            VirtioBlockReadOnlyReadSessionStatus.Success => "Success"u8,
            VirtioBlockReadOnlyReadSessionStatus.AdapterFailed => "AdapterFailed"u8,
            _ => ReadOnlySpan<byte>.Empty
        };
    }

    private static ReadOnlySpan<byte> GetPipelineStatusName(
        VirtioBlockReadQueuePipelineStatus status)
    {
        return status switch
        {
            VirtioBlockReadQueuePipelineStatus.Success => "Success"u8,
            VirtioBlockReadQueuePipelineStatus.PublicationFailed => "PublicationFailed"u8,
            VirtioBlockReadQueuePipelineStatus.NotifyReadinessFailed => "NotifyReadinessFailed"u8,
            VirtioBlockReadQueuePipelineStatus.NotifyCompletionFailed => "NotifyCompletionFailed"u8,
            VirtioBlockReadQueuePipelineStatus.BlockReadProjectionFailed =>
                "BlockReadProjectionFailed"u8,
            _ => ReadOnlySpan<byte>.Empty
        };
    }

    private static ReadOnlySpan<byte> GetPackagePreflightStatusName(
        VirtioBlockPackageReadPreflightStatus status)
    {
        return status switch
        {
            VirtioBlockPackageReadPreflightStatus.Success => "Success"u8,
            VirtioBlockPackageReadPreflightStatus.IncompleteReadSession =>
                "IncompleteReadSession"u8,
            VirtioBlockPackageReadPreflightStatus.InvalidBlockDevice => "InvalidBlockDevice"u8,
            VirtioBlockPackageReadPreflightStatus.HeaderScratchTooSmall =>
                "HeaderScratchTooSmall"u8,
            VirtioBlockPackageReadPreflightStatus.PartitionEntryScratchTooSmall =>
                "PartitionEntryScratchTooSmall"u8,
            VirtioBlockPackageReadPreflightStatus.PartitionDescriptorTableEmpty =>
                "PartitionDescriptorTableEmpty"u8,
            VirtioBlockPackageReadPreflightStatus.PackageBufferTooSmall =>
                "PackageBufferTooSmall"u8,
            VirtioBlockPackageReadPreflightStatus.PackageLoadFailed => "PackageLoadFailed"u8,
            _ => ReadOnlySpan<byte>.Empty
        };
    }

    private static ReadOnlySpan<byte> GetPackageOpenStatusName(OsPackageFileSystemStatus status)
    {
        return status switch
        {
            OsPackageFileSystemStatus.Success => "Success"u8,
            OsPackageFileSystemStatus.PackageParseFailed => "PackageParseFailed"u8,
            OsPackageFileSystemStatus.InvalidPath => "InvalidPath"u8,
            OsPackageFileSystemStatus.MissingFile => "MissingFile"u8,
            OsPackageFileSystemStatus.DestinationTooSmall => "DestinationTooSmall"u8,
            OsPackageFileSystemStatus.InvalidReadRange => "InvalidReadRange"u8,
            OsPackageFileSystemStatus.FileIndexOutOfRange => "FileIndexOutOfRange"u8,
            _ => ReadOnlySpan<byte>.Empty
        };
    }

    private static ReadOnlySpan<byte> GetPackageStagingStatusName(
        OsPackageFileStagingStatus status)
    {
        return status switch
        {
            OsPackageFileStagingStatus.Success => "Success"u8,
            OsPackageFileStagingStatus.OpenFileFailed => "OpenFileFailed"u8,
            OsPackageFileStagingStatus.EmptyFile => "EmptyFile"u8,
            OsPackageFileStagingStatus.PackageRangeInvalid => "PackageRangeInvalid"u8,
            OsPackageFileStagingStatus.DestinationTooSmall => "DestinationTooSmall"u8,
            _ => ReadOnlySpan<byte>.Empty
        };
    }

    private static int GetBoolByteCount(bool value)
    {
        return value ? "true"u8.Length : "false"u8.Length;
    }

    private static void AppendBytes(ReadOnlySpan<byte> value, Span<byte> destination, ref int offset)
    {
        AsciiTextBuilderResult result = AsciiTextBuilder.AppendBytes(destination, offset, value);
        if (result.Succeeded)
        {
            offset = result.NextOffset;
        }
    }

    private static void AppendBool(bool value, Span<byte> destination, ref int offset)
    {
        AppendBytes(value ? "true"u8 : "false"u8, destination, ref offset);
    }

    private static void AppendUInt64Decimal(ulong value, Span<byte> destination, ref int offset)
    {
        AsciiTextBuilderResult result =
            AsciiTextBuilder.AppendUInt64Decimal(destination, offset, value);
        if (result.Succeeded)
        {
            offset = result.NextOffset;
        }
    }
}
