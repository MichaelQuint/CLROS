using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Text;
using OS.Kernel.Platform;

internal static class PlatformFirmwareServiceDenialMarkerTests
{
    [ModuleInitializer]
    internal static void RunAll()
    {
        int failures = 0;

        Run(
            "platform.firmware denial marker formats uefi gone",
            TestFormatsUefiGoneDenial,
            ref failures);
        Run(
            "platform.firmware denial marker formats exited services",
            TestFormatsExitedServicesDenial,
            ref failures);
        Run(
            "platform.firmware denial marker reports short destination",
            TestReportsShortDestination,
            ref failures);
        Run(
            "platform.firmware denial marker rejects allowed and default results",
            TestRejectsAllowedAndDefaultResults,
            ref failures);
        Run(
            "platform.firmware denial marker rejects invalid operation token",
            TestRejectsInvalidOperationToken,
            ref failures);
        Run(
            "platform.firmware denial marker uses shared ascii text builder",
            TestUsesSharedAsciiTextBuilder,
            ref failures);
        Run(
            "platform.firmware denial marker result contains no managed references",
            TestResultContainsNoManagedReferences,
            ref failures);

        if (failures != 0)
        {
            throw new InvalidOperationException(
                "Platform firmware service-denial marker host tests failed: " + failures);
        }
    }

    private static void Run(string name, Func<bool> test, ref int failures)
    {
        Console.WriteLine("TEST_START " + name);

        try
        {
            if (test())
            {
                Console.WriteLine("TEST_PASS " + name);
                return;
            }

            failures++;
            Console.WriteLine("TEST_FAIL " + name);
        }
        catch (Exception ex)
        {
            failures++;
            Console.WriteLine("TEST_FAIL " + name + " " + ex.GetType().Name + ": " + ex.Message);
        }
    }

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

        return query.Succeeded
            && query.RequiredBytes == format.RequiredBytes
            && query.BytesWritten == 0
            && format.Succeeded
            && format.Mode == FirmwareServicesMode.UefiGoneSimulated
            && format.OperationBytes == "LocateProtocol".Length
            && marker
                == "UEFI_SERVICE_DENIED operation=LocateProtocol mode=UefiGoneSimulated";
    }

    private static bool TestFormatsExitedServicesDenial()
    {
        FirmwareServicesGuard guard = new FirmwareServicesGuard();
        _ = guard.MarkBootServicesExited();
        FirmwareCallResult denied = guard.RequireBootServices("GetMemoryMap");

        Span<byte> destination = stackalloc byte[96];
        PlatformFirmwareServiceDenialMarkerResult format =
            PlatformFirmwareServiceDenialMarker.TryFormatDeniedCall(denied, destination);

        string marker = Encoding.ASCII.GetString(destination[..format.BytesWritten]);

        return format.Succeeded
            && format.Mode == FirmwareServicesMode.BootServicesExited
            && marker
                == "UEFI_SERVICE_DENIED operation=GetMemoryMap mode=BootServicesExited";
    }

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

        return query.Succeeded
            && format.IsInitialized
            && !format.Succeeded
            && format.Status == PlatformFirmwareServiceDenialMarkerStatus.DestinationTooSmall
            && format.BytesWritten == 0
            && format.RequiredBytes == query.RequiredBytes
            && format.OperationBytes == "GetMemoryMap".Length;
    }

    private static bool TestRejectsAllowedAndDefaultResults()
    {
        FirmwareServicesGuard guard = new FirmwareServicesGuard();
        FirmwareCallResult allowed = guard.RequireBootServices("GetMemoryMap");
        FirmwareCallResult defaultResult = default;

        PlatformFirmwareServiceDenialMarkerResult allowedFormat =
            PlatformFirmwareServiceDenialMarker.TryFormatDeniedCall(allowed, stackalloc byte[96]);
        PlatformFirmwareServiceDenialMarkerResult defaultFormat =
            PlatformFirmwareServiceDenialMarker.TryFormatDeniedCall(defaultResult, stackalloc byte[96]);

        return allowedFormat.IsInitialized
            && !allowedFormat.Succeeded
            && allowedFormat.Status == PlatformFirmwareServiceDenialMarkerStatus.AllowedCall
            && defaultFormat.IsInitialized
            && !defaultFormat.Succeeded
            && defaultFormat.Status == PlatformFirmwareServiceDenialMarkerStatus.InvalidResult;
    }

    private static bool TestRejectsInvalidOperationToken()
    {
        FirmwareCallResult denied = FirmwareCallResult.Denied(
            "Get MemoryMap",
            FirmwareServicesMode.UefiGoneSimulated);

        PlatformFirmwareServiceDenialMarkerResult format =
            PlatformFirmwareServiceDenialMarker.TryFormatDeniedCall(denied, stackalloc byte[96]);

        return format.IsInitialized
            && !format.Succeeded
            && format.Status == PlatformFirmwareServiceDenialMarkerStatus.InvalidOperation
            && format.Mode == FirmwareServicesMode.UefiGoneSimulated;
    }

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

        return source.Contains("using OS.Std.Text;", StringComparison.Ordinal)
            && source.Contains(
                "AsciiTextBuilder.AppendBytes(destination, offset, source)",
                StringComparison.Ordinal)
            && source.Contains(
                "AsciiTextBuilder.AppendChars(destination, offset, operation)",
                StringComparison.Ordinal)
            && !source.Contains("CopyTo(destination", StringComparison.Ordinal)
            && !source.Contains("destination.Slice(offset)", StringComparison.Ordinal)
            && !source.Contains("private static void WriteOperation(", StringComparison.Ordinal);
    }

    private static bool TestResultContainsNoManagedReferences()
    {
        PlatformFirmwareServiceDenialMarkerResult result = default;

        return !result.IsInitialized
            && !result.Succeeded
            && result.Status == PlatformFirmwareServiceDenialMarkerStatus.Success
            && !RuntimeHelpers.IsReferenceOrContainsReferences<
                PlatformFirmwareServiceDenialMarkerResult>()
            && !RuntimeHelpers.IsReferenceOrContainsReferences<
                PlatformFirmwareServiceDenialMarkerStatus>();
    }
}
