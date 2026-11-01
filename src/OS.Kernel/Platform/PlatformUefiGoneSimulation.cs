namespace OS.Kernel.Platform;
public static class PlatformUefiGoneSimulation
{
    public const string StartMarker = "UEFI_GONE_SIM_START";
    public const string PlatformDeniedMarker = "UEFI_GONE_SIM_PLATFORM_DENIED";
    public const string PassMarker = "UEFI_GONE_SIM_PASS";
    public const string FailMarkerPrefix = "UEFI_GONE_SIM_FAIL reason=";
    public const string ForbiddenConsoleLine = "UEFI_GONE_SIM_FORBIDDEN_CONSOLE";
    public static PlatformUefiGoneSimulationResult Run(FirmwareServicesGuard firmware, IPlatformConsole console, IPlatformMemoryMap memoryMap, IPlatformConfigurationTables configurationTables)
    {
        return RunCore(firmware, console, memoryMap, configurationTables, timer: null, random: null, physicalMemory: null, framebuffer: null, fileSystem: null);
    }

    public static PlatformUefiGoneSimulationResult Run(FirmwareServicesGuard firmware, IPlatformConsole console, IPlatformMemoryMap memoryMap, IPlatformConfigurationTables configurationTables, IPlatformTimer timer, IPlatformRandom random, IPlatformPhysicalMemory physicalMemory, IPlatformFramebuffer framebuffer)
    {
        return Run(firmware, console, memoryMap, configurationTables, timer, random, physicalMemory, framebuffer, fileSystem: null);
    }

    public static PlatformUefiGoneSimulationResult Run(FirmwareServicesGuard firmware, IPlatformConsole console, IPlatformMemoryMap memoryMap, IPlatformConfigurationTables configurationTables, IPlatformTimer timer, IPlatformRandom random, IPlatformPhysicalMemory physicalMemory, IPlatformFramebuffer framebuffer, IPlatformFileSystem? fileSystem)
    {
        return RunCore(firmware, console, memoryMap, configurationTables, timer, random, physicalMemory, framebuffer, fileSystem);
    }

    private static PlatformUefiGoneSimulationResult RunCore(FirmwareServicesGuard firmware, IPlatformConsole console, IPlatformMemoryMap memoryMap, IPlatformConfigurationTables configurationTables, IPlatformTimer? timer, IPlatformRandom? random, IPlatformPhysicalMemory? physicalMemory, IPlatformFramebuffer? framebuffer, IPlatformFileSystem? fileSystem)
    {
        FirmwareCallResult transition = firmware.EnterUefiGoneSimulation();
        if (!transition.Succeeded)
        {
            return PlatformUefiGoneSimulationResult.FromStatus(PlatformUefiGoneSimulationStatus.SimulationDenied, transition, default, default, default);
        }

        PlatformConsoleResult consoleResult = console.WriteLine(ForbiddenConsoleLine);
        if (!IsDenied(consoleResult))
        {
            return PlatformUefiGoneSimulationResult.FromStatus(PlatformUefiGoneSimulationStatus.ConsoleCallNotDenied, transition, consoleResult, default, default);
        }

        PlatformMemoryMapResult memoryMapResult = memoryMap.CopySnapshot();
        if (!IsDenied(memoryMapResult))
        {
            return PlatformUefiGoneSimulationResult.FromStatus(PlatformUefiGoneSimulationStatus.MemoryMapCallNotDenied, transition, consoleResult, memoryMapResult, default);
        }

        PlatformConfigurationTableScanResult configurationTableResult = configurationTables.ScanAcpiRsdp();
        if (!IsDenied(configurationTableResult))
        {
            return PlatformUefiGoneSimulationResult.FromStatus(PlatformUefiGoneSimulationStatus.ConfigurationTableCallNotDenied, transition, consoleResult, memoryMapResult, configurationTableResult);
        }

        PlatformTimerResult timerResult = default;
        if (timer != null)
        {
            timerResult = timer.ReadMonotonic();
            if (!IsDenied(timerResult))
            {
                return PlatformUefiGoneSimulationResult.FromStatus(PlatformUefiGoneSimulationStatus.TimerCallNotDenied, transition, consoleResult, memoryMapResult, configurationTableResult, timerResult, default, default, default, default);
            }
        }

        PlatformRandomResult randomResult = default;
        if (random != null)
        {
            Span<byte> randomScratch = stackalloc byte[1];
            randomResult = random.FillBytes(randomScratch);
            if (!IsDenied(randomResult))
            {
                return PlatformUefiGoneSimulationResult.FromStatus(PlatformUefiGoneSimulationStatus.RandomCallNotDenied, transition, consoleResult, memoryMapResult, configurationTableResult, timerResult, randomResult, default, default, default);
            }
        }

        PlatformPhysicalMemoryResult physicalMemoryResult = default;
        if (physicalMemory != null)
        {
            Span<byte> physicalScratch = stackalloc byte[1];
            physicalMemoryResult = physicalMemory.ReadBytes(physicalAddress: 1, byteCount: physicalScratch.Length, physicalScratch);
            if (!IsDenied(physicalMemoryResult))
            {
                return PlatformUefiGoneSimulationResult.FromStatus(PlatformUefiGoneSimulationStatus.PhysicalMemoryCallNotDenied, transition, consoleResult, memoryMapResult, configurationTableResult, timerResult, randomResult, physicalMemoryResult, default, default);
            }
        }

        PlatformFramebufferResult framebufferResult = default;
        if (framebuffer != null)
        {
            framebufferResult = framebuffer.CaptureSnapshot();
            if (!IsDenied(framebufferResult))
            {
                return PlatformUefiGoneSimulationResult.FromStatus(PlatformUefiGoneSimulationStatus.FramebufferCallNotDenied, transition, consoleResult, memoryMapResult, configurationTableResult, timerResult, randomResult, physicalMemoryResult, framebufferResult, default);
            }
        }

        PlatformFileSystemReadResult fileSystemResult = default;
        if (fileSystem != null)
        {
            Span<byte> fileScratch = stackalloc byte[1];
            fileSystemResult = fileSystem.ReadFile("EFI/BOOT/BOOTX64.EFI"u8, fileScratch);
            if (!IsDenied(fileSystemResult))
            {
                return PlatformUefiGoneSimulationResult.FromStatus(PlatformUefiGoneSimulationStatus.FileSystemCallNotDenied, transition, consoleResult, memoryMapResult, configurationTableResult, timerResult, randomResult, physicalMemoryResult, framebufferResult, fileSystemResult);
            }
        }

        return PlatformUefiGoneSimulationResult.FromStatus(PlatformUefiGoneSimulationStatus.Success, transition, consoleResult, memoryMapResult, configurationTableResult, timerResult, randomResult, physicalMemoryResult, framebufferResult, fileSystemResult);
    }

    private static bool IsDenied(PlatformConsoleResult result)
    {
        return !result.Succeeded && result.Status == PlatformConsoleStatus.FirmwareServicesUnavailable && result.Mode == FirmwareServicesMode.UefiGoneSimulated;
    }

    private static bool IsDenied(PlatformMemoryMapResult result)
    {
        return !result.Succeeded && result.Status == PlatformMemoryMapStatus.FirmwareServicesUnavailable && result.Mode == FirmwareServicesMode.UefiGoneSimulated;
    }

    private static bool IsDenied(PlatformConfigurationTableScanResult result)
    {
        return !result.Succeeded && result.Status == PlatformConfigurationTableScanStatus.FirmwareServicesUnavailable && result.Mode == FirmwareServicesMode.UefiGoneSimulated;
    }

    private static bool IsDenied(PlatformTimerResult result)
    {
        return !result.Succeeded && result.Status == PlatformTimerStatus.FirmwareServicesUnavailable && result.Mode == FirmwareServicesMode.UefiGoneSimulated;
    }

    private static bool IsDenied(PlatformRandomResult result)
    {
        return !result.Succeeded && result.Status == PlatformRandomStatus.FirmwareServicesUnavailable && result.Mode == FirmwareServicesMode.UefiGoneSimulated;
    }

    private static bool IsDenied(PlatformPhysicalMemoryResult result)
    {
        return !result.Succeeded && result.Status == PlatformPhysicalMemoryStatus.FirmwareServicesUnavailable && result.Mode == FirmwareServicesMode.UefiGoneSimulated;
    }

    private static bool IsDenied(PlatformFramebufferResult result)
    {
        return !result.Succeeded && result.Status == PlatformFramebufferStatus.FirmwareServicesUnavailable && result.Mode == FirmwareServicesMode.UefiGoneSimulated;
    }

    private static bool IsDenied(PlatformFileSystemReadResult result)
    {
        return !result.Succeeded && result.Status == PlatformFileSystemStatus.FirmwareServicesUnavailable && result.Mode == FirmwareServicesMode.UefiGoneSimulated;
    }
}
