using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Text;
using OS.Kernel.Platform;

internal static class PlatformFirmwareServiceDenialMarkerTests
{

    [HostTest("platform.firmware denial marker formats uefi gone", HostLane.Fast, 0)]
    private static bool TestFormatsUefiGoneDenial()
    {
        FirmwareServicesGuard guard = new FirmwareServicesGuard();
        _ = guard.EnterUefiGoneSimulation();
        FirmwareCallResult denied = guard.RequireBootServices("LocateProtocol");

        Span<byte> destination = stackalloc byte[96];
        PlatformFirmwareServiceDenialMarkerResult query =
            PlatformFirmwareServiceDenialMarker.QueryRequiredBytes(denied);
        PlatformFirmwareServiceDenialMarkerResult format =
            PlatformFirmwareServiceDenialMarker.TryFormatDeniedCall(denied, destination);

        string marker = Encoding.ASCII.GetString(destination[..format.BytesWritten]);

        return HostCheck.Result(query.Succeeded
            && query.RequiredBytes == format.RequiredBytes
            && query.BytesWritten == 0
            && format.Succeeded
            && format.Mode == FirmwareServicesMode.UefiGoneSimulated
            && format.OperationBytes == "LocateProtocol".Length
            && marker
                == "UEFI_SERVICE_DENIED operation=LocateProtocol mode=UefiGoneSimulated");
    }

    [HostTest("platform.firmware denial marker formats exited services", HostLane.Fast, 1)]
    private static bool TestFormatsExitedServicesDenial()
    {
        FirmwareServicesGuard guard = new FirmwareServicesGuard();
        _ = guard.MarkBootServicesExited();
        FirmwareCallResult denied = guard.RequireBootServices("GetMemoryMap");

        Span<byte> destination = stackalloc byte[96];
        PlatformFirmwareServiceDenialMarkerResult format =
            PlatformFirmwareServiceDenialMarker.TryFormatDeniedCall(denied, destination);

        string marker = Encoding.ASCII.GetString(destination[..format.BytesWritten]);

        return HostCheck.Result(format.Succeeded
            && format.Mode == FirmwareServicesMode.BootServicesExited
            && marker
                == "UEFI_SERVICE_DENIED operation=GetMemoryMap mode=BootServicesExited");
    }

    [HostTest("platform.firmware denial marker reports short destination", HostLane.Fast, 2)]
    private static bool TestReportsShortDestination()
    {
        FirmwareServicesGuard guard = new FirmwareServicesGuard();
        _ = guard.EnterUefiGoneSimulation();
        FirmwareCallResult denied = guard.RequireBootServices("GetMemoryMap");

        Span<byte> destination = stackalloc byte[12];
        PlatformFirmwareServiceDenialMarkerResult query =
            PlatformFirmwareServiceDenialMarker.QueryRequiredBytes(denied);
        PlatformFirmwareServiceDenialMarkerResult format =
            PlatformFirmwareServiceDenialMarker.TryFormatDeniedCall(denied, destination);

        return HostCheck.Result(query.Succeeded
            && format.IsInitialized
            && !format.Succeeded
            && format.Status == PlatformFirmwareServiceDenialMarkerStatus.DestinationTooSmall
            && format.BytesWritten == 0
            && format.RequiredBytes == query.RequiredBytes
            && format.OperationBytes == "GetMemoryMap".Length);
    }

    [HostTest("platform.firmware denial marker rejects allowed and default results", HostLane.Fast, 3)]
    private static bool TestRejectsAllowedAndDefaultResults()
    {
        FirmwareServicesGuard guard = new FirmwareServicesGuard();
        FirmwareCallResult allowed = guard.RequireBootServices("GetMemoryMap");
        FirmwareCallResult defaultResult = default;

        PlatformFirmwareServiceDenialMarkerResult allowedFormat =
            PlatformFirmwareServiceDenialMarker.TryFormatDeniedCall(allowed, stackalloc byte[96]);
        PlatformFirmwareServiceDenialMarkerResult defaultFormat =
            PlatformFirmwareServiceDenialMarker.TryFormatDeniedCall(defaultResult, stackalloc byte[96]);

        return HostCheck.Result(allowedFormat.IsInitialized
            && !allowedFormat.Succeeded
            && allowedFormat.Status == PlatformFirmwareServiceDenialMarkerStatus.AllowedCall
            && defaultFormat.IsInitialized
            && !defaultFormat.Succeeded
            && defaultFormat.Status == PlatformFirmwareServiceDenialMarkerStatus.InvalidResult);
    }

    [HostTest("platform.firmware denial marker rejects invalid operation token", HostLane.Fast, 4)]
    private static bool TestRejectsInvalidOperationToken()
    {
        FirmwareCallResult denied = FirmwareCallResult.Denied(
            "Get MemoryMap",
            FirmwareServicesMode.UefiGoneSimulated);

        PlatformFirmwareServiceDenialMarkerResult format =
            PlatformFirmwareServiceDenialMarker.TryFormatDeniedCall(denied, stackalloc byte[96]);

        return HostCheck.Result(format.IsInitialized
            && !format.Succeeded
            && format.Status == PlatformFirmwareServiceDenialMarkerStatus.InvalidOperation
            && format.Mode == FirmwareServicesMode.UefiGoneSimulated);
    }

    [HostTest("platform.firmware denial marker uses shared ascii text builder", HostLane.Fast, 5)]
    private static bool TestUsesSharedAsciiTextBuilder()
    {
        string repositoryRoot = HostToolProcess.FindRepositoryRoot();
        string source = File.ReadAllText(
            Path.Combine(
                repositoryRoot,
                "src",
                "OS.Kernel",
                "Platform",
                "PlatformFirmwareServiceDenialMarker.cs"));

        return HostCheck.Result(source.Contains("using OS.Std.Text;", StringComparison.Ordinal)
            && source.Contains(
                "AsciiTextBuilder.AppendBytes(destination, offset, source)",
                StringComparison.Ordinal)
            && source.Contains(
                "AsciiTextBuilder.AppendChars(destination, offset, operation)",
                StringComparison.Ordinal)
            && !source.Contains("CopyTo(destination", StringComparison.Ordinal)
            && !source.Contains("destination.Slice(offset)", StringComparison.Ordinal)
            && !source.Contains("private static void WriteOperation(", StringComparison.Ordinal));
    }

    [HostTest("platform.firmware denial marker result contains no managed references", HostLane.Fast, 6)]
    private static bool TestResultContainsNoManagedReferences()
    {
        PlatformFirmwareServiceDenialMarkerResult result = default;

        return HostCheck.Result(!result.IsInitialized
            && !result.Succeeded
            && result.Status == PlatformFirmwareServiceDenialMarkerStatus.Success
            && !RuntimeHelpers.IsReferenceOrContainsReferences<
                PlatformFirmwareServiceDenialMarkerResult>()
            && !RuntimeHelpers.IsReferenceOrContainsReferences<
                PlatformFirmwareServiceDenialMarkerStatus>());
    }
}
