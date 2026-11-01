using OS.Boot;
using OS.Std.Text;

namespace OS.Manifest;
public static class NativeAppLaunchPlanDiagnosticFormatter
{
    public static NativeAppLaunchPlanDiagnosticFormatResult FormatResult(in NativeAppLaunchPlanResult result, ReadOnlySpan<byte> manifestSource, Span<byte> destination)
    {
        NativeAppLaunchPlanDiagnosticFormatStatus validationStatus = Validate(in result, manifestSource);
        if (validationStatus != NativeAppLaunchPlanDiagnosticFormatStatus.Success)
        {
            return NativeAppLaunchPlanDiagnosticFormatResult.Fail(validationStatus, result.Status, requiredBytes: 0, destination.Length);
        }

        int requiredBytes = GetRequiredBytes(in result);
        if (destination.Length < requiredBytes)
        {
            return NativeAppLaunchPlanDiagnosticFormatResult.Fail(NativeAppLaunchPlanDiagnosticFormatStatus.DestinationTooSmall, result.Status, requiredBytes, destination.Length);
        }

        GetLaunchMetadata(in result, out int appNameBytes, out int appPathBytes, out int argumentBytes, out int environmentCount, out bool hasListenEndpoint, out int listenPort, out bool requiresPackageStorage, out bool requiresNetwork, out bool appSelected);
        int offset = 0;
        if (!TryAppendBytes("APP_LAUNCH_PLAN status="u8, destination, ref offset) || !TryAppendBytes(GetLaunchStatusName(result.Status), destination, ref offset) || !TryAppendBytes(" initialized="u8, destination, ref offset) || !TryAppendBool(result.IsInitialized, destination, ref offset) || !TryAppendBytes(" succeeded="u8, destination, ref offset) || !TryAppendBool(result.Succeeded, destination, ref offset) || !TryAppendBytes(" appNameBytes="u8, destination, ref offset) || !TryAppendInt64Decimal(appNameBytes, destination, ref offset) || !TryAppendBytes(" appPathBytes="u8, destination, ref offset) || !TryAppendInt64Decimal(appPathBytes, destination, ref offset) || !TryAppendBytes(" argumentBytes="u8, destination, ref offset) || !TryAppendInt64Decimal(argumentBytes, destination, ref offset) || !TryAppendBytes(" environmentCount="u8, destination, ref offset) || !TryAppendInt64Decimal(environmentCount, destination, ref offset) || !TryAppendBytes(" hasListenEndpoint="u8, destination, ref offset) || !TryAppendBool(hasListenEndpoint, destination, ref offset) || !TryAppendBytes(" listenPort="u8, destination, ref offset) || !TryAppendInt64Decimal(listenPort, destination, ref offset) || !TryAppendBytes(" requiresPackageStorage="u8, destination, ref offset) || !TryAppendBool(requiresPackageStorage, destination, ref offset) || !TryAppendBytes(" requiresNetwork="u8, destination, ref offset) || !TryAppendBool(requiresNetwork, destination, ref offset) || !TryAppendBytes(" requiredBootCapabilities="u8, destination, ref offset) || !TryAppendInt64Decimal((int)result.RequiredBootCapabilities, destination, ref offset) || !TryAppendBytes(" missingBootCapabilities="u8, destination, ref offset) || !TryAppendInt64Decimal((int)result.MissingBootCapabilities, destination, ref offset) || !TryAppendBytes(" unknownBootCapabilities="u8, destination, ref offset) || !TryAppendInt64Decimal((int)result.UnknownBootCapabilities, destination, ref offset) || !TryAppendBytes(" requiredRuntimeCapabilities="u8, destination, ref offset) || !TryAppendInt64Decimal((int)result.RequiredRuntimeCapabilities, destination, ref offset) || !TryAppendBytes(" missingRuntimeCapabilities="u8, destination, ref offset) || !TryAppendInt64Decimal((int)result.MissingRuntimeCapabilities, destination, ref offset) || !TryAppendBytes(" appSelected="u8, destination, ref offset) || !TryAppendBool(appSelected, destination, ref offset) || !TryAppendBytes(" appLoaded="u8, destination, ref offset) || !TryAppendBool(false, destination, ref offset) || !TryAppendBytes(" appExecuted="u8, destination, ref offset) || !TryAppendBool(false, destination, ref offset) || !TryAppendBytes(" hostedDotNetStarted="u8, destination, ref offset) || !TryAppendBool(false, destination, ref offset) || !TryAppendBytes(" kestrelStarted="u8, destination, ref offset) || !TryAppendBool(false, destination, ref offset))
        {
            return NativeAppLaunchPlanDiagnosticFormatResult.Fail(NativeAppLaunchPlanDiagnosticFormatStatus.DestinationTooSmall, result.Status, requiredBytes, destination.Length);
        }

        return new NativeAppLaunchPlanDiagnosticFormatResult(NativeAppLaunchPlanDiagnosticFormatStatus.Success, result.Status, bytesWritten: offset, requiredBytes, availableBytes: destination.Length);
    }

    public static NativeAppLaunchPlanDiagnosticFormatResult QueryRequiredBytes(in NativeAppLaunchPlanResult result, ReadOnlySpan<byte> manifestSource)
    {
        NativeAppLaunchPlanDiagnosticFormatStatus validationStatus = Validate(in result, manifestSource);
        if (validationStatus != NativeAppLaunchPlanDiagnosticFormatStatus.Success)
        {
            return NativeAppLaunchPlanDiagnosticFormatResult.Fail(validationStatus, result.Status, requiredBytes: 0, availableBytes: 0);
        }

        return new NativeAppLaunchPlanDiagnosticFormatResult(NativeAppLaunchPlanDiagnosticFormatStatus.Success, result.Status, bytesWritten: 0, requiredBytes: GetRequiredBytes(in result), availableBytes: 0);
    }

    private static NativeAppLaunchPlanDiagnosticFormatStatus Validate(in NativeAppLaunchPlanResult result, ReadOnlySpan<byte> manifestSource)
    {
        if (!result.IsInitialized || GetLaunchStatusName(result.Status).Length == 0)
        {
            return NativeAppLaunchPlanDiagnosticFormatStatus.InvalidResult;
        }

        if (result.Status == NativeAppLaunchStatus.Success && !result.Succeeded)
        {
            return NativeAppLaunchPlanDiagnosticFormatStatus.InvalidResult;
        }

        if (!result.Succeeded)
        {
            return NativeAppLaunchPlanDiagnosticFormatStatus.Success;
        }

        if (!IsValidField(manifestSource, result.Plan.AppName) || !IsValidField(manifestSource, result.Plan.AppPath) || !IsValidField(manifestSource, result.Plan.Arguments) || result.Plan.EnvironmentCount < 0 || result.Plan.EnvironmentCount > NativeManifestParser.MaximumEnvironmentCount)
        {
            return NativeAppLaunchPlanDiagnosticFormatStatus.SourceRangeInvalid;
        }

        for (int index = 0; index < result.Plan.EnvironmentCount; index++)
        {
            NativeManifestEnvironmentEntry entry = result.Plan.GetEnvironmentEntry(index);
            if (!entry.IsPresent || !IsValidField(manifestSource, entry.Key) || !IsValidField(manifestSource, entry.Value))
            {
                return NativeAppLaunchPlanDiagnosticFormatStatus.SourceRangeInvalid;
            }
        }

        if (result.Plan.HasListenEndpoint && (!IsValidField(manifestSource, result.Plan.ListenEndpoint.Host) || result.Plan.ListenPort == 0))
        {
            return NativeAppLaunchPlanDiagnosticFormatStatus.SourceRangeInvalid;
        }

        return NativeAppLaunchPlanDiagnosticFormatStatus.Success;
    }

    private static bool IsValidField(ReadOnlySpan<byte> source, NativeManifestField field)
    {
        return field.IsPresent && field.Offset >= 0 && field.Length >= 0 && field.Offset <= source.Length && source.Length - field.Offset >= field.Length;
    }

    private static int GetRequiredBytes(in NativeAppLaunchPlanResult result)
    {
        GetLaunchMetadata(in result, out int appNameBytes, out int appPathBytes, out int argumentBytes, out int environmentCount, out bool hasListenEndpoint, out int listenPort, out bool requiresPackageStorage, out bool requiresNetwork, out bool appSelected);
        return "APP_LAUNCH_PLAN status="u8.Length + GetLaunchStatusName(result.Status).Length + " initialized="u8.Length + GetBoolByteCount(result.IsInitialized) + " succeeded="u8.Length + GetBoolByteCount(result.Succeeded) + " appNameBytes="u8.Length + AsciiDecimalFormatter.CountInt32Digits(appNameBytes) + " appPathBytes="u8.Length + AsciiDecimalFormatter.CountInt32Digits(appPathBytes) + " argumentBytes="u8.Length + AsciiDecimalFormatter.CountInt32Digits(argumentBytes) + " environmentCount="u8.Length + AsciiDecimalFormatter.CountInt32Digits(environmentCount) + " hasListenEndpoint="u8.Length + GetBoolByteCount(hasListenEndpoint) + " listenPort="u8.Length + AsciiDecimalFormatter.CountInt32Digits(listenPort) + " requiresPackageStorage="u8.Length + GetBoolByteCount(requiresPackageStorage) + " requiresNetwork="u8.Length + GetBoolByteCount(requiresNetwork) + " requiredBootCapabilities="u8.Length + AsciiDecimalFormatter.CountInt32Digits((int)result.RequiredBootCapabilities) + " missingBootCapabilities="u8.Length + AsciiDecimalFormatter.CountInt32Digits((int)result.MissingBootCapabilities) + " unknownBootCapabilities="u8.Length + AsciiDecimalFormatter.CountInt32Digits((int)result.UnknownBootCapabilities) + " requiredRuntimeCapabilities="u8.Length + AsciiDecimalFormatter.CountInt32Digits((int)result.RequiredRuntimeCapabilities) + " missingRuntimeCapabilities="u8.Length + AsciiDecimalFormatter.CountInt32Digits((int)result.MissingRuntimeCapabilities) + " appSelected="u8.Length + GetBoolByteCount(appSelected) + " appLoaded="u8.Length + GetBoolByteCount(false) + " appExecuted="u8.Length + GetBoolByteCount(false) + " hostedDotNetStarted="u8.Length + GetBoolByteCount(false) + " kestrelStarted="u8.Length + GetBoolByteCount(false);
    }

    private static void GetLaunchMetadata(in NativeAppLaunchPlanResult result, out int appNameBytes, out int appPathBytes, out int argumentBytes, out int environmentCount, out bool hasListenEndpoint, out int listenPort, out bool requiresPackageStorage, out bool requiresNetwork, out bool appSelected)
    {
        if (!result.Succeeded)
        {
            appNameBytes = 0;
            appPathBytes = 0;
            argumentBytes = 0;
            environmentCount = 0;
            hasListenEndpoint = false;
            listenPort = 0;
            requiresPackageStorage = false;
            requiresNetwork = false;
            appSelected = false;
            return;
        }

        appNameBytes = result.Plan.AppName.Length;
        appPathBytes = result.Plan.AppPath.Length;
        argumentBytes = result.Plan.ArgumentByteLength;
        environmentCount = result.Plan.EnvironmentCount;
        hasListenEndpoint = result.Plan.HasListenEndpoint;
        listenPort = hasListenEndpoint ? result.Plan.ListenPort : 0;
        requiresPackageStorage = result.Plan.RequiresPackageStorage;
        requiresNetwork = result.Plan.RequiresNetwork;
        appSelected = true;
    }

    private static ReadOnlySpan<byte> GetLaunchStatusName(NativeAppLaunchStatus status)
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

    private static int GetBoolByteCount(bool value)
    {
        return value ? "true"u8.Length : "false"u8.Length;
    }

    private static bool TryAppendBytes(ReadOnlySpan<byte> value, Span<byte> destination, ref int offset)
    {
        AsciiTextBuilderResult result = AsciiTextBuilder.AppendBytes(destination, offset, value);
        if (!result.Succeeded)
        {
            return false;
        }

        offset = result.NextOffset;
        return true;
    }

    private static bool TryAppendBool(bool value, Span<byte> destination, ref int offset)
    {
        return TryAppendBytes(value ? "true"u8 : "false"u8, destination, ref offset);
    }

    private static bool TryAppendInt64Decimal(long value, Span<byte> destination, ref int offset)
    {
        AsciiTextBuilderResult result = AsciiTextBuilder.AppendInt64Decimal(destination, offset, value);
        if (!result.Succeeded)
        {
            return false;
        }

        offset = result.NextOffset;
        return true;
    }
}
