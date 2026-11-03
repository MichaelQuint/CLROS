using OS.Manifest;
using OS.Std.Text;
using OS.Storage;

namespace OS.Kernel.Runtime;

public static class NativeAppPackageStagingReadinessFormatter
{
    public const int MaximumMarkerBytes = 1024;
    private static ReadOnlySpan<byte> NotReadyPrefix => "APP_PACKAGE_STAGING_NOT_READY "u8;
    private static ReadOnlySpan<byte> StagingReadinessFailedName => "StagingReadinessFailed"u8;

    public static NativeAppPackageStagingMarkerFormatResult TryFormatNotReadyMarker(NativeAppPackageStagingReadinessResult readiness, Span<byte> destination)
    {
        NativeAppPackageStagingMarkerFormatStatus validation = Validate(readiness);
        if (validation != NativeAppPackageStagingMarkerFormatStatus.Success)
        {
            return new NativeAppPackageStagingMarkerFormatResult(validation, 0, 0);
        }

        int requiredBytes = CountNotReadyMarkerBytes(readiness);
        if (requiredBytes > MaximumMarkerBytes)
        {
            return new NativeAppPackageStagingMarkerFormatResult(NativeAppPackageStagingMarkerFormatStatus.MarkerTooLong, 0, requiredBytes);
        }

        if (destination.Length < requiredBytes)
        {
            return new NativeAppPackageStagingMarkerFormatResult(NativeAppPackageStagingMarkerFormatStatus.DestinationTooSmall, 0, requiredBytes);
        }

        int offset = 0;
        if (!TryWrite(NotReadyPrefix, destination, ref offset) || !TryWrite("status="u8, destination, ref offset) || !TryWrite(StagingReadinessFailedName, destination, ref offset) || !TryWrite(" initialized="u8, destination, ref offset) || !TryWriteBool(readiness.IsInitialized, destination, ref offset) || !TryWrite(" readiness="u8, destination, ref offset) || !TryWrite(GetName(readiness.Status), destination, ref offset) || !TryWrite(" manifest="u8, destination, ref offset) || !TryWrite(GetName(readiness.ManifestStatus), destination, ref offset) || !TryWrite(" package="u8, destination, ref offset) || !TryWrite(GetName(readiness.PackageStatus), destination, ref offset) || !TryWrite(" launch="u8, destination, ref offset) || !TryWrite(GetName(readiness.LaunchStatus), destination, ref offset) || !TryWrite(" descriptor="u8, destination, ref offset) || !TryWrite(GetName(readiness.DescriptorStatus), destination, ref offset) || !TryWrite(" packageFileStaging="u8, destination, ref offset) || !TryWrite(GetName(readiness.PackageFileStagingStatus), destination, ref offset) || !TryWrite(" mapping="u8, destination, ref offset) || !TryWrite(GetName(readiness.PayloadMappingStatus), destination, ref offset) || !TryWrite(" payloadStaging="u8, destination, ref offset) || !TryWrite(GetName(readiness.PayloadStagingStatus), destination, ref offset) || !TryWrite(" memory="u8, destination, ref offset) || !TryWrite(GetName(readiness.ExecutableMemoryStatus), destination, ref offset) || !TryWrite(" invocation="u8, destination, ref offset) || !TryWrite(GetName(readiness.InvocationStatus), destination, ref offset) || !TryWrite(" fileIndex="u8, destination, ref offset) || !TryWriteUnsigned(readiness.AppBinaryFileIndex, destination, ref offset) || !TryWrite(" packageBytes="u8, destination, ref offset) || !TryWriteUnsigned(readiness.PackageFileBytesStaged, destination, ref offset) || !TryWrite(" payloadOffset="u8, destination, ref offset) || !TryWriteUnsigned(readiness.PayloadOffsetInPackage, destination, ref offset) || !TryWrite(" executableOffset="u8, destination, ref offset) || !TryWriteUnsigned(readiness.ExecutableOffsetInPackage, destination, ref offset) || !TryWrite(" executableBytes="u8, destination, ref offset) || !TryWriteUnsigned(readiness.PayloadExecutableBytesStaged, destination, ref offset) || !TryWrite(" writableStagingBytes="u8, destination, ref offset) || !TryWriteUnsigned(readiness.RequiredWritableStagingBytes, destination, ref offset) || !TryWrite(" stackBytes="u8, destination, ref offset) || !TryWriteUnsigned(readiness.RequiredStackBytes, destination, ref offset) || !TryWrite(" entryOffset="u8, destination, ref offset) || !TryWriteUnsigned(readiness.EntryOffsetInPackage, destination, ref offset))
        {
            return new NativeAppPackageStagingMarkerFormatResult(NativeAppPackageStagingMarkerFormatStatus.DestinationTooSmall, 0, requiredBytes);
        }

        return new NativeAppPackageStagingMarkerFormatResult(NativeAppPackageStagingMarkerFormatStatus.Success, offset, requiredBytes);
    }

    public static NativeAppPackageStagingMarkerFormatResult QueryNotReadyMarkerRequiredBytes(NativeAppPackageStagingReadinessResult readiness)
    {
        NativeAppPackageStagingMarkerFormatStatus validation = Validate(readiness);
        if (validation != NativeAppPackageStagingMarkerFormatStatus.Success)
        {
            return new NativeAppPackageStagingMarkerFormatResult(validation, 0, 0);
        }

        int requiredBytes = CountNotReadyMarkerBytes(readiness);
        return requiredBytes > MaximumMarkerBytes ? new NativeAppPackageStagingMarkerFormatResult(NativeAppPackageStagingMarkerFormatStatus.MarkerTooLong, 0, requiredBytes) : new NativeAppPackageStagingMarkerFormatResult(NativeAppPackageStagingMarkerFormatStatus.Success, 0, requiredBytes);
    }

    private static NativeAppPackageStagingMarkerFormatStatus Validate(NativeAppPackageStagingReadinessResult readiness)
    {
        return GetName(readiness.Status).Length != 0 && GetName(readiness.ManifestStatus).Length != 0 && GetName(readiness.PackageStatus).Length != 0 && GetName(readiness.LaunchStatus).Length != 0 && GetName(readiness.DescriptorStatus).Length != 0 && GetName(readiness.PackageFileStagingStatus).Length != 0 && GetName(readiness.PayloadMappingStatus).Length != 0 && GetName(readiness.PayloadStagingStatus).Length != 0 && GetName(readiness.ExecutableMemoryStatus).Length != 0 && GetName(readiness.InvocationStatus).Length != 0 ? NativeAppPackageStagingMarkerFormatStatus.Success : NativeAppPackageStagingMarkerFormatStatus.InvalidStatus;
    }

    private static int CountNotReadyMarkerBytes(NativeAppPackageStagingReadinessResult readiness)
    {
        return NotReadyPrefix.Length + "status="u8.Length + StagingReadinessFailedName.Length + " initialized="u8.Length + (readiness.IsInitialized ? "true"u8.Length : "false"u8.Length) + " readiness="u8.Length + GetName(readiness.Status).Length + " manifest="u8.Length + GetName(readiness.ManifestStatus).Length + " package="u8.Length + GetName(readiness.PackageStatus).Length + " launch="u8.Length + GetName(readiness.LaunchStatus).Length + " descriptor="u8.Length + GetName(readiness.DescriptorStatus).Length + " packageFileStaging="u8.Length + GetName(readiness.PackageFileStagingStatus).Length + " mapping="u8.Length + GetName(readiness.PayloadMappingStatus).Length + " payloadStaging="u8.Length + GetName(readiness.PayloadStagingStatus).Length + " memory="u8.Length + GetName(readiness.ExecutableMemoryStatus).Length + " invocation="u8.Length + GetName(readiness.InvocationStatus).Length + " fileIndex="u8.Length + AsciiDecimalFormatter.CountUInt64Digits(readiness.AppBinaryFileIndex) + " packageBytes="u8.Length + AsciiDecimalFormatter.CountUInt64Digits(readiness.PackageFileBytesStaged) + " payloadOffset="u8.Length + AsciiDecimalFormatter.CountUInt64Digits(readiness.PayloadOffsetInPackage) + " executableOffset="u8.Length + AsciiDecimalFormatter.CountUInt64Digits(readiness.ExecutableOffsetInPackage) + " executableBytes="u8.Length + AsciiDecimalFormatter.CountUInt64Digits(readiness.PayloadExecutableBytesStaged) + " writableStagingBytes="u8.Length + AsciiDecimalFormatter.CountUInt64Digits(readiness.RequiredWritableStagingBytes) + " stackBytes="u8.Length + AsciiDecimalFormatter.CountUInt64Digits(readiness.RequiredStackBytes) + " entryOffset="u8.Length + AsciiDecimalFormatter.CountUInt64Digits(readiness.EntryOffsetInPackage);
    }

    private static ReadOnlySpan<byte> GetName(NativeAppPackageStagingReadinessStatus status)
    {
        return status switch
        {
            NativeAppPackageStagingReadinessStatus.Success => "Success"u8,
            NativeAppPackageStagingReadinessStatus.ManifestLoadFailed => "ManifestLoadFailed"u8,
            NativeAppPackageStagingReadinessStatus.PackageParseFailed => "PackageParseFailed"u8,
            NativeAppPackageStagingReadinessStatus.LaunchPlanFailed => "LaunchPlanFailed"u8,
            NativeAppPackageStagingReadinessStatus.LaunchDescriptorFailed => "LaunchDescriptorFailed"u8,
            NativeAppPackageStagingReadinessStatus.PackageFileStagingFailed => "PackageFileStagingFailed"u8,
            NativeAppPackageStagingReadinessStatus.PayloadMappingFailed => "PayloadMappingFailed"u8,
            NativeAppPackageStagingReadinessStatus.PayloadStagingFailed => "PayloadStagingFailed"u8,
            NativeAppPackageStagingReadinessStatus.ExecutableMemoryPlanFailed => "ExecutableMemoryPlanFailed"u8,
            NativeAppPackageStagingReadinessStatus.InvocationPlanFailed => "InvocationPlanFailed"u8,
            NativeAppPackageStagingReadinessStatus.PackageStagingByteMismatch => "PackageStagingByteMismatch"u8,
            NativeAppPackageStagingReadinessStatus.PayloadStagingByteMismatch => "PayloadStagingByteMismatch"u8,
            NativeAppPackageStagingReadinessStatus.WritableStagingByteMismatch => "WritableStagingByteMismatch"u8,
            NativeAppPackageStagingReadinessStatus.EntryOffsetMismatch => "EntryOffsetMismatch"u8,
            _ => ReadOnlySpan<byte>.Empty
        };
    }

    private static ReadOnlySpan<byte> GetName(NativeManifestLoadStatus status)
    {
        return status switch
        {
            NativeManifestLoadStatus.Success => "Success"u8,
            NativeManifestLoadStatus.GptFailed => "GptFailed"u8,
            NativeManifestLoadStatus.PartitionDeviceFailed => "PartitionDeviceFailed"u8,
            NativeManifestLoadStatus.Fat32ReadFailed => "Fat32ReadFailed"u8,
            NativeManifestLoadStatus.ManifestParseFailed => "ManifestParseFailed"u8,
            _ => ReadOnlySpan<byte>.Empty
        };
    }

    private static ReadOnlySpan<byte> GetName(OsPackageStatus status)
    {
        return status switch
        {
            OsPackageStatus.Success => "Success"u8,
            OsPackageStatus.PackageTooSmall => "PackageTooSmall"u8,
            OsPackageStatus.InvalidMagic => "InvalidMagic"u8,
            OsPackageStatus.UnsupportedVersion => "UnsupportedVersion"u8,
            OsPackageStatus.InvalidHeaderSize => "InvalidHeaderSize"u8,
            OsPackageStatus.InvalidFileCount => "InvalidFileCount"u8,
            OsPackageStatus.InvalidFileTableEntrySize => "InvalidFileTableEntrySize"u8,
            OsPackageStatus.InvalidFileTableRange => "InvalidFileTableRange"u8,
            OsPackageStatus.InvalidEntryRange => "InvalidEntryRange"u8,
            OsPackageStatus.EntryOverlapsMetadata => "EntryOverlapsMetadata"u8,
            OsPackageStatus.EntryOverlapsEntry => "EntryOverlapsEntry"u8,
            OsPackageStatus.InvalidPath => "InvalidPath"u8,
            OsPackageStatus.DuplicatePath => "DuplicatePath"u8,
            OsPackageStatus.MissingFile => "MissingFile"u8,
            OsPackageStatus.FileIndexOutOfRange => "FileIndexOutOfRange"u8,
            OsPackageStatus.DestinationTooSmall => "DestinationTooSmall"u8,
            _ => ReadOnlySpan<byte>.Empty
        };
    }

    private static ReadOnlySpan<byte> GetName(NativeAppLaunchStatus status)
    {
        return status switch
        {
            NativeAppLaunchStatus.Success => "Success"u8,
            NativeAppLaunchStatus.UnsupportedManifestVersion => "UnsupportedManifestVersion"u8,
            NativeAppLaunchStatus.MissingAppName => "MissingAppName"u8,
            NativeAppLaunchStatus.MissingAppPath => "MissingAppPath"u8,
            NativeAppLaunchStatus.MissingArguments => "MissingArguments"u8,
            NativeAppLaunchStatus.InvalidAppNameField => "InvalidAppNameField"u8,
            NativeAppLaunchStatus.InvalidAppPathField => "InvalidAppPathField"u8,
            NativeAppLaunchStatus.InvalidArgumentsField => "InvalidArgumentsField"u8,
            NativeAppLaunchStatus.AppPathNotPackageRelative => "AppPathNotPackageRelative"u8,
            NativeAppLaunchStatus.ArgumentsTooLong => "ArgumentsTooLong"u8,
            NativeAppLaunchStatus.UnsupportedAppKind => "UnsupportedAppKind"u8,
            NativeAppLaunchStatus.UnsupportedRuntimeKind => "UnsupportedRuntimeKind"u8,
            NativeAppLaunchStatus.UnsupportedNetworkMode => "UnsupportedNetworkMode"u8,
            NativeAppLaunchStatus.UnsupportedLogMode => "UnsupportedLogMode"u8,
            NativeAppLaunchStatus.InvalidListenEndpoint => "InvalidListenEndpoint"u8,
            NativeAppLaunchStatus.ListenRequiresNetwork => "ListenRequiresNetwork"u8,
            NativeAppLaunchStatus.MissingBootCapabilities => "MissingBootCapabilities"u8,
            NativeAppLaunchStatus.UnknownBootCapabilities => "UnknownBootCapabilities"u8,
            NativeAppLaunchStatus.MissingRuntimeCapabilities => "MissingRuntimeCapabilities"u8,
            _ => ReadOnlySpan<byte>.Empty
        };
    }

    private static ReadOnlySpan<byte> GetName(NativeAppLaunchDescriptorStatus status)
    {
        return status switch
        {
            NativeAppLaunchDescriptorStatus.Success => "Success"u8,
            NativeAppLaunchDescriptorStatus.LaunchPlanFailed => "LaunchPlanFailed"u8,
            NativeAppLaunchDescriptorStatus.AppPathUnavailable => "AppPathUnavailable"u8,
            NativeAppLaunchDescriptorStatus.PackageFileMissing => "PackageFileMissing"u8,
            NativeAppLaunchDescriptorStatus.PackageFileInvalidPath => "PackageFileInvalidPath"u8,
            NativeAppLaunchDescriptorStatus.PackageFileSystemFailed => "PackageFileSystemFailed"u8,
            NativeAppLaunchDescriptorStatus.InvalidPayloadHeader => "InvalidPayloadHeader"u8,
            _ => ReadOnlySpan<byte>.Empty
        };
    }

    private static ReadOnlySpan<byte> GetName(OsPackageFileStagingStatus status)
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

    private static ReadOnlySpan<byte> GetName(NativeAppPayloadMappingPlanStatus status)
    {
        return status switch
        {
            NativeAppPayloadMappingPlanStatus.Success => "Success"u8,
            NativeAppPayloadMappingPlanStatus.DescriptorFailed => "DescriptorFailed"u8,
            NativeAppPayloadMappingPlanStatus.PayloadTooSmall => "PayloadTooSmall"u8,
            NativeAppPayloadMappingPlanStatus.ZeroExecutableContent => "ZeroExecutableContent"u8,
            NativeAppPayloadMappingPlanStatus.InvalidEntryOffset => "InvalidEntryOffset"u8,
            NativeAppPayloadMappingPlanStatus.RangeOverflow => "RangeOverflow"u8,
            _ => ReadOnlySpan<byte>.Empty
        };
    }

    private static ReadOnlySpan<byte> GetName(NativeAppPayloadStagingStatus status)
    {
        return status switch
        {
            NativeAppPayloadStagingStatus.Success => "Success"u8,
            NativeAppPayloadStagingStatus.MappingPlanFailed => "MappingPlanFailed"u8,
            NativeAppPayloadStagingStatus.RangeOverflow => "RangeOverflow"u8,
            NativeAppPayloadStagingStatus.PackageRangeOutOfBounds => "PackageRangeOutOfBounds"u8,
            NativeAppPayloadStagingStatus.DestinationTooSmall => "DestinationTooSmall"u8,
            _ => ReadOnlySpan<byte>.Empty
        };
    }

    private static ReadOnlySpan<byte> GetName(NativeAppExecutableMemoryPlanStatus status)
    {
        return status switch
        {
            NativeAppExecutableMemoryPlanStatus.Success => "Success"u8,
            NativeAppExecutableMemoryPlanStatus.MappingPlanFailed => "MappingPlanFailed"u8,
            NativeAppExecutableMemoryPlanStatus.InvalidPayloadMappingPlan => "InvalidPayloadMappingPlan"u8,
            NativeAppExecutableMemoryPlanStatus.ZeroExecutableContent => "ZeroExecutableContent"u8,
            NativeAppExecutableMemoryPlanStatus.InvalidPageSize => "InvalidPageSize"u8,
            NativeAppExecutableMemoryPlanStatus.InvalidAlignment => "InvalidAlignment"u8,
            NativeAppExecutableMemoryPlanStatus.AlignmentGreaterThanPageSize => "AlignmentGreaterThanPageSize"u8,
            NativeAppExecutableMemoryPlanStatus.UnalignedExecutableOffset => "UnalignedExecutableOffset"u8,
            NativeAppExecutableMemoryPlanStatus.AccidentalWritableExecutable => "AccidentalWritableExecutable"u8,
            NativeAppExecutableMemoryPlanStatus.UnsafeProtectionState => "UnsafeProtectionState"u8,
            NativeAppExecutableMemoryPlanStatus.InvalidEntryOffset => "InvalidEntryOffset"u8,
            NativeAppExecutableMemoryPlanStatus.RangeOverflow => "RangeOverflow"u8,
            _ => ReadOnlySpan<byte>.Empty
        };
    }

    private static ReadOnlySpan<byte> GetName(NativeAppInvocationPlanStatus status)
    {
        return status switch
        {
            NativeAppInvocationPlanStatus.Success => "Success"u8,
            NativeAppInvocationPlanStatus.ExecutableMemoryPlanFailed => "ExecutableMemoryPlanFailed"u8,
            NativeAppInvocationPlanStatus.LaunchPlanFailed => "LaunchPlanFailed"u8,
            NativeAppInvocationPlanStatus.InvalidExecutableMemoryMetadata => "InvalidExecutableMemoryMetadata"u8,
            NativeAppInvocationPlanStatus.InvalidEntryOffset => "InvalidEntryOffset"u8,
            NativeAppInvocationPlanStatus.InvalidStackSize => "InvalidStackSize"u8,
            NativeAppInvocationPlanStatus.MetadataOutOfBounds => "MetadataOutOfBounds"u8,
            NativeAppInvocationPlanStatus.RangeOverflow => "RangeOverflow"u8,
            _ => ReadOnlySpan<byte>.Empty
        };
    }

    private static bool TryWrite(ReadOnlySpan<byte> source, Span<byte> destination, ref int offset)
    {
        AsciiTextBuilderResult result = AsciiTextBuilder.AppendBytes(destination, offset, source);
        if (!result.Succeeded)
        {
            return false;
        }

        offset = result.NextOffset;
        return true;
    }

    private static bool TryWriteBool(bool value, Span<byte> destination, ref int offset)
    {
        return TryWrite(value ? "true"u8 : "false"u8, destination, ref offset);
    }

    private static bool TryWriteUnsigned(ulong value, Span<byte> destination, ref int offset)
    {
        AsciiTextBuilderResult result = AsciiTextBuilder.AppendUInt64Decimal(destination, offset, value);
        if (!result.Succeeded)
        {
            return false;
        }

        offset = result.NextOffset;
        return true;
    }
}
