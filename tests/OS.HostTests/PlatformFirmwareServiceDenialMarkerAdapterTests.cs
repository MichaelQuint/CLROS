using System.Runtime.CompilerServices;
using System.Text;
using OS.Boot;
using OS.Kernel.Platform;

internal static class PlatformFirmwareServiceDenialMarkerAdapterTests
{

    [HostTest("platform.firmware denial adapters format guarded results", HostLane.Fast, 0)]
    private static bool TestFormatsGuardedResults()
    {
        FirmwareCallResult consoleDenied = Denied(PlatformConsoleOperations.OutputString);
        FirmwareCallResult memoryMapDenied = Denied(PlatformMemoryMapOperations.GetMemoryMap);
        FirmwareCallResult configurationDenied = Denied(PlatformConfigurationTableOperations.ScanAcpiRsdp);
        FirmwareCallResult timerDenied = Denied(PlatformTimerOperations.ReadMonotonic);
        FirmwareCallResult randomDenied = Denied(PlatformRandomOperations.FillBytes);
        FirmwareCallResult physicalDenied = Denied(PlatformPhysicalMemoryOperations.ReadBytes);
        FirmwareCallResult framebufferDenied = Denied(PlatformFramebufferOperations.CaptureSnapshot);

        PlatformConsoleResult console =
            PlatformConsoleResult.FirmwareServicesUnavailable(consoleDenied);
        PlatformMemoryMapResult memoryMap =
            PlatformMemoryMapResult.FirmwareServicesUnavailable(memoryMapDenied);
        PlatformConfigurationTableScanResult configuration =
            PlatformConfigurationTableScanResult.ScanAcpiRsdp(configurationDenied, default);
        PlatformTimerResult timer =
            PlatformTimerResult.FirmwareServicesUnavailable(timerDenied);
        PlatformRandomResult random =
            PlatformRandomResult.FirmwareServicesUnavailable(randomDenied);
        PlatformPhysicalMemoryResult physicalMemory =
            PlatformPhysicalMemoryResult.FirmwareServicesUnavailable(physicalDenied);
        PlatformFramebufferResult framebuffer =
            PlatformFramebufferResult.FirmwareServicesUnavailable(framebufferDenied);

        return HostCheck.Result(Format(console)
                == "UEFI_SERVICE_DENIED operation=OutputString mode=UefiGoneSimulated"
            && Format(memoryMap)
                == "UEFI_SERVICE_DENIED operation=GetMemoryMap mode=UefiGoneSimulated"
            && Format(configuration)
                == "UEFI_SERVICE_DENIED operation=ConfigurationTable.ScanAcpiRsdp mode=UefiGoneSimulated"
            && Format(timer)
                == "UEFI_SERVICE_DENIED operation=Timer.ReadMonotonic mode=UefiGoneSimulated"
            && Format(random)
                == "UEFI_SERVICE_DENIED operation=Random.FillBytes mode=UefiGoneSimulated"
            && Format(physicalMemory)
                == "UEFI_SERVICE_DENIED operation=PhysicalMemory.ReadBytes mode=UefiGoneSimulated"
            && Format(framebuffer)
                == "UEFI_SERVICE_DENIED operation=Framebuffer.CaptureSnapshot mode=UefiGoneSimulated");
    }

    [HostTest("platform.firmware denial adapters query required bytes", HostLane.Fast, 1)]
    private static bool TestQueriesRequiredBytes()
    {
        PlatformRandomResult denied = PlatformRandomResult.FirmwareServicesUnavailable(
            Denied(PlatformRandomOperations.FillBytes));

        PlatformFirmwareServiceDenialMarkerResult query =
            PlatformFirmwareServiceDenialMarkerAdapters.QueryRequiredBytes(denied);

        Span<byte> destination = stackalloc byte[96];
        PlatformFirmwareServiceDenialMarkerResult format =
            PlatformFirmwareServiceDenialMarkerAdapters.TryFormatDeniedCall(denied, destination);

        return HostCheck.Result(query.Succeeded
            && format.Succeeded
            && query.BytesWritten == 0
            && query.RequiredBytes == format.RequiredBytes
            && format.BytesWritten == format.RequiredBytes
            && query.OperationBytes == PlatformRandomOperations.FillBytes.Length);
    }

    [HostTest("platform.firmware denial adapters reject non-denial results", HostLane.Fast, 2)]
    private static bool TestRejectsNonDenialResults()
    {
        FirmwareCallResult allowed = FirmwareCallResult.Allowed(
            PlatformTimerOperations.ReadMonotonic,
            FirmwareServicesMode.BootServicesAvailable);
        PlatformTimerResult success = PlatformTimerResult.Read(
            allowed,
            new PlatformTimerMetadata(monotonicTicks: 10, frequencyHertz: 1000));
        PlatformTimerResult invalidMetadata = PlatformTimerResult.InvalidMetadata(allowed);
        PlatformTimerResult defaultResult = default;

        PlatformFirmwareServiceDenialMarkerResult successFormat =
            PlatformFirmwareServiceDenialMarkerAdapters.TryFormatDeniedCall(
                success,
                stackalloc byte[96]);
        PlatformFirmwareServiceDenialMarkerResult invalidFormat =
            PlatformFirmwareServiceDenialMarkerAdapters.TryFormatDeniedCall(
                invalidMetadata,
                stackalloc byte[96]);
        PlatformFirmwareServiceDenialMarkerResult defaultFormat =
            PlatformFirmwareServiceDenialMarkerAdapters.TryFormatDeniedCall(
                defaultResult,
                stackalloc byte[96]);

        return HostCheck.Result(successFormat.IsInitialized
            && !successFormat.Succeeded
            && successFormat.Status == PlatformFirmwareServiceDenialMarkerStatus.AllowedCall
            && invalidFormat.IsInitialized
            && !invalidFormat.Succeeded
            && invalidFormat.Status == PlatformFirmwareServiceDenialMarkerStatus.InvalidResult
            && defaultFormat.IsInitialized
            && !defaultFormat.Succeeded
            && defaultFormat.Status == PlatformFirmwareServiceDenialMarkerStatus.InvalidResult);
    }

    [HostTest("platform.firmware denial adapters result layout contains no managed references", HostLane.Fast, 3)]
    private static bool TestResultLayoutContainsNoManagedReferences()
    {
        return HostCheck.Result(!RuntimeHelpers.IsReferenceOrContainsReferences<
                PlatformFirmwareServiceDenialMarkerResult>()
            && !RuntimeHelpers.IsReferenceOrContainsReferences<
                PlatformFirmwareServiceDenialMarkerStatus>());
    }

    private static FirmwareCallResult Denied(string operation)
    {
        return FirmwareCallResult.Denied(
            operation,
            FirmwareServicesMode.UefiGoneSimulated);
    }

    private static string Format(PlatformConsoleResult result)
    {
        Span<byte> destination = stackalloc byte[128];
        PlatformFirmwareServiceDenialMarkerResult format =
            PlatformFirmwareServiceDenialMarkerAdapters.TryFormatDeniedCall(result, destination);
        return Encoding.ASCII.GetString(destination[..format.BytesWritten]);
    }

    private static string Format(PlatformMemoryMapResult result)
    {
        Span<byte> destination = stackalloc byte[128];
        PlatformFirmwareServiceDenialMarkerResult format =
            PlatformFirmwareServiceDenialMarkerAdapters.TryFormatDeniedCall(result, destination);
        return Encoding.ASCII.GetString(destination[..format.BytesWritten]);
    }

    private static string Format(PlatformConfigurationTableScanResult result)
    {
        Span<byte> destination = stackalloc byte[128];
        PlatformFirmwareServiceDenialMarkerResult format =
            PlatformFirmwareServiceDenialMarkerAdapters.TryFormatDeniedCall(result, destination);
        return Encoding.ASCII.GetString(destination[..format.BytesWritten]);
    }

    private static string Format(PlatformTimerResult result)
    {
        Span<byte> destination = stackalloc byte[128];
        PlatformFirmwareServiceDenialMarkerResult format =
            PlatformFirmwareServiceDenialMarkerAdapters.TryFormatDeniedCall(result, destination);
        return Encoding.ASCII.GetString(destination[..format.BytesWritten]);
    }

    private static string Format(PlatformRandomResult result)
    {
        Span<byte> destination = stackalloc byte[128];
        PlatformFirmwareServiceDenialMarkerResult format =
            PlatformFirmwareServiceDenialMarkerAdapters.TryFormatDeniedCall(result, destination);
        return Encoding.ASCII.GetString(destination[..format.BytesWritten]);
    }

    private static string Format(PlatformPhysicalMemoryResult result)
    {
        Span<byte> destination = stackalloc byte[128];
        PlatformFirmwareServiceDenialMarkerResult format =
            PlatformFirmwareServiceDenialMarkerAdapters.TryFormatDeniedCall(result, destination);
        return Encoding.ASCII.GetString(destination[..format.BytesWritten]);
    }

    private static string Format(PlatformFramebufferResult result)
    {
        Span<byte> destination = stackalloc byte[128];
        PlatformFirmwareServiceDenialMarkerResult format =
            PlatformFirmwareServiceDenialMarkerAdapters.TryFormatDeniedCall(result, destination);
        return Encoding.ASCII.GetString(destination[..format.BytesWritten]);
    }
}
