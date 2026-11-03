using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using OS.Boot;
using OS.Kernel.Platform;
using OS.Std.Binary;

internal static partial class PlatformUefiGoneSimulationTests
{
    [HostTest("platform.uefi gone simulation denies all guarded adapters", HostLane.Fast, 0)]
    private static bool TestSimulationDeniesAllGuardedAdapters()
    {
        FirmwareServicesGuard firmware = new FirmwareServicesGuard();
        ConsoleSinkDouble consoleSink = new ConsoleSinkDouble(available: true);
        MemoryMapSourceDouble memoryMapSource = new MemoryMapSourceDouble(CreateValidSnapshot());
        ConfigurationTableSourceDouble configurationSource = new ConfigurationTableSourceDouble(CreateSingleAcpi20TableBytes(), tableCount: 1);
        TimerSourceDouble timerSource = new TimerSourceDouble();
        RandomSourceDouble randomSource = new RandomSourceDouble();
        PhysicalMemorySourceDouble physicalSource = new PhysicalMemorySourceDouble();
        FramebufferSourceDouble framebufferSource = new FramebufferSourceDouble();
        FileSystemSourceDouble fileSystemSource = new FileSystemSourceDouble();
        PlatformUefiGoneSimulationResult result = PlatformUefiGoneSimulation.Run(firmware, new GuardedPlatformConsole(firmware, consoleSink), new GuardedPlatformMemoryMap(firmware, memoryMapSource), new GuardedPlatformConfigurationTables(firmware, configurationSource), new GuardedPlatformTimer(firmware, timerSource), new GuardedPlatformRandom(firmware, randomSource), new GuardedPlatformPhysicalMemory(firmware, physicalSource), new GuardedPlatformFramebuffer(firmware, framebufferSource), new GuardedPlatformFileSystem(firmware, fileSystemSource));
        return HostCheck.Result(result.Succeeded && result.Status == PlatformUefiGoneSimulationStatus.Success && result.Marker == PlatformUefiGoneSimulation.PlatformDeniedMarker && firmware.Mode == FirmwareServicesMode.UefiGoneSimulated && result.Console.Status == PlatformConsoleStatus.FirmwareServicesUnavailable && result.MemoryMap.Status == PlatformMemoryMapStatus.FirmwareServicesUnavailable && result.ConfigurationTables.Status == PlatformConfigurationTableScanStatus.FirmwareServicesUnavailable && result.Timer.Status == PlatformTimerStatus.FirmwareServicesUnavailable && result.Timer.Mode == FirmwareServicesMode.UefiGoneSimulated && result.Timer.Operation == PlatformTimerOperations.ReadMonotonic && result.Random.Status == PlatformRandomStatus.FirmwareServicesUnavailable && result.Random.Mode == FirmwareServicesMode.UefiGoneSimulated && result.Random.Operation == PlatformRandomOperations.FillBytes && result.PhysicalMemory.Status == PlatformPhysicalMemoryStatus.FirmwareServicesUnavailable && result.PhysicalMemory.Mode == FirmwareServicesMode.UefiGoneSimulated && result.PhysicalMemory.Operation == PlatformPhysicalMemoryOperations.ReadBytes && result.Framebuffer.Status == PlatformFramebufferStatus.FirmwareServicesUnavailable && result.Framebuffer.Mode == FirmwareServicesMode.UefiGoneSimulated && result.Framebuffer.Operation == PlatformFramebufferOperations.CaptureSnapshot && result.FileSystem.Status == PlatformFileSystemStatus.FirmwareServicesUnavailable && result.FileSystem.Mode == FirmwareServicesMode.UefiGoneSimulated && result.FileSystem.Operation == PlatformFileSystemOperations.ReadFile && consoleSink.LineCount == 0 && memoryMapSource.CaptureCount == 0 && configurationSource.ScanCount == 0 && timerSource.ReadCount == 0 && randomSource.FillCount == 0 && physicalSource.ReadCount == 0 && framebufferSource.CaptureCount == 0 && fileSystemSource.ReadCount == 0);
    }

    [HostTest("platform.uefi gone simulation denies filesystem adapter", HostLane.Fast, 1)]
    private static bool TestSimulationDeniesFileSystemAdapter()
    {
        FirmwareServicesGuard firmware = new FirmwareServicesGuard();
        ConsoleSinkDouble consoleSink = new ConsoleSinkDouble(available: true);
        MemoryMapSourceDouble memoryMapSource = new MemoryMapSourceDouble(CreateValidSnapshot());
        ConfigurationTableSourceDouble configurationSource = new ConfigurationTableSourceDouble(CreateSingleAcpi20TableBytes(), tableCount: 1);
        TimerSourceDouble timerSource = new TimerSourceDouble();
        RandomSourceDouble randomSource = new RandomSourceDouble();
        PhysicalMemorySourceDouble physicalSource = new PhysicalMemorySourceDouble();
        FramebufferSourceDouble framebufferSource = new FramebufferSourceDouble();
        FileSystemSourceDouble fileSystemSource = new FileSystemSourceDouble();
        PlatformUefiGoneSimulationResult result = PlatformUefiGoneSimulation.Run(firmware, new GuardedPlatformConsole(firmware, consoleSink), new GuardedPlatformMemoryMap(firmware, memoryMapSource), new GuardedPlatformConfigurationTables(firmware, configurationSource), new GuardedPlatformTimer(firmware, timerSource), new GuardedPlatformRandom(firmware, randomSource), new GuardedPlatformPhysicalMemory(firmware, physicalSource), new GuardedPlatformFramebuffer(firmware, framebufferSource), new GuardedPlatformFileSystem(firmware, fileSystemSource));
        return HostCheck.Result(result.Succeeded && result.FileSystem.Status == PlatformFileSystemStatus.FirmwareServicesUnavailable && result.FileSystem.Mode == FirmwareServicesMode.UefiGoneSimulated && result.FileSystem.Operation == PlatformFileSystemOperations.ReadFile && fileSystemSource.ReadCount == 0 && consoleSink.LineCount == 0 && memoryMapSource.CaptureCount == 0 && configurationSource.ScanCount == 0 && timerSource.ReadCount == 0 && randomSource.FillCount == 0 && physicalSource.ReadCount == 0 && framebufferSource.CaptureCount == 0);
    }

    [HostTest("platform.uefi gone simulation denies console memorymap and config adapters", HostLane.Fast, 2)]
    private static bool TestSimulationDeniesGuardedAdapters()
    {
        FirmwareServicesGuard firmware = new FirmwareServicesGuard();
        ConsoleSinkDouble consoleSink = new ConsoleSinkDouble(available: true);
        MemoryMapSourceDouble memoryMapSource = new MemoryMapSourceDouble(CreateValidSnapshot());
        ConfigurationTableSourceDouble configurationSource = new ConfigurationTableSourceDouble(CreateSingleAcpi20TableBytes(), tableCount: 1);
        IPlatformConsole console = new GuardedPlatformConsole(firmware, consoleSink);
        IPlatformMemoryMap memoryMap = new GuardedPlatformMemoryMap(firmware, memoryMapSource);
        IPlatformConfigurationTables configurationTables = new GuardedPlatformConfigurationTables(firmware, configurationSource);
        PlatformUefiGoneSimulationResult result = PlatformUefiGoneSimulation.Run(firmware, console, memoryMap, configurationTables);
        return HostCheck.Result(result.Succeeded && result.Status == PlatformUefiGoneSimulationStatus.Success && result.Marker == PlatformUefiGoneSimulation.PlatformDeniedMarker && firmware.Mode == FirmwareServicesMode.UefiGoneSimulated && result.Transition.Succeeded && result.Console.Status == PlatformConsoleStatus.FirmwareServicesUnavailable && result.Console.Mode == FirmwareServicesMode.UefiGoneSimulated && result.Console.Operation == PlatformConsoleOperations.OutputString && result.MemoryMap.Status == PlatformMemoryMapStatus.FirmwareServicesUnavailable && result.MemoryMap.Mode == FirmwareServicesMode.UefiGoneSimulated && result.MemoryMap.Operation == PlatformMemoryMapOperations.GetMemoryMap && result.ConfigurationTables.Status == PlatformConfigurationTableScanStatus.FirmwareServicesUnavailable && result.ConfigurationTables.Mode == FirmwareServicesMode.UefiGoneSimulated && result.ConfigurationTables.Operation == PlatformConfigurationTableOperations.ScanAcpiRsdp && consoleSink.LineCount == 0 && memoryMapSource.CaptureCount == 0 && configurationSource.ScanCount == 0);
    }

    [HostTest("platform.uefi gone simulation reports console allowance", HostLane.Fast, 3)]
    private static bool TestSimulationReportsConsoleAllowance()
    {
        FirmwareServicesGuard firmware = new FirmwareServicesGuard();
        UnguardedConsole console = new UnguardedConsole();
        MemoryMapSourceDouble memoryMapSource = new MemoryMapSourceDouble(CreateValidSnapshot());
        ConfigurationTableSourceDouble configurationSource = new ConfigurationTableSourceDouble(CreateSingleAcpi20TableBytes(), tableCount: 1);
        IPlatformMemoryMap memoryMap = new GuardedPlatformMemoryMap(firmware, memoryMapSource);
        IPlatformConfigurationTables configurationTables = new GuardedPlatformConfigurationTables(firmware, configurationSource);
        PlatformUefiGoneSimulationResult result = PlatformUefiGoneSimulation.Run(firmware, console, memoryMap, configurationTables);
        return HostCheck.Result(!result.Succeeded && result.Status == PlatformUefiGoneSimulationStatus.ConsoleCallNotDenied && result.Marker == PlatformUefiGoneSimulation.FailMarkerPrefix + nameof(PlatformUefiGoneSimulationStatus.ConsoleCallNotDenied) && console.WriteCount == 1 && memoryMapSource.CaptureCount == 0 && configurationSource.ScanCount == 0);
    }

    [HostTest("platform.uefi gone simulation reports timer allowance", HostLane.Fast, 4)]
    private static bool TestSimulationReportsTimerAllowance()
    {
        FirmwareServicesGuard firmware = new FirmwareServicesGuard();
        ConsoleSinkDouble consoleSink = new ConsoleSinkDouble(available: true);
        MemoryMapSourceDouble memoryMapSource = new MemoryMapSourceDouble(CreateValidSnapshot());
        ConfigurationTableSourceDouble configurationSource = new ConfigurationTableSourceDouble(CreateSingleAcpi20TableBytes(), tableCount: 1);
        UnguardedTimer timer = new UnguardedTimer();
        RandomSourceDouble randomSource = new RandomSourceDouble();
        PhysicalMemorySourceDouble physicalSource = new PhysicalMemorySourceDouble();
        FramebufferSourceDouble framebufferSource = new FramebufferSourceDouble();
        PlatformUefiGoneSimulationResult result = PlatformUefiGoneSimulation.Run(firmware, new GuardedPlatformConsole(firmware, consoleSink), new GuardedPlatformMemoryMap(firmware, memoryMapSource), new GuardedPlatformConfigurationTables(firmware, configurationSource), timer, new GuardedPlatformRandom(firmware, randomSource), new GuardedPlatformPhysicalMemory(firmware, physicalSource), new GuardedPlatformFramebuffer(firmware, framebufferSource));
        return HostCheck.Result(!result.Succeeded && result.Status == PlatformUefiGoneSimulationStatus.TimerCallNotDenied && result.Marker == PlatformUefiGoneSimulation.FailMarkerPrefix + nameof(PlatformUefiGoneSimulationStatus.TimerCallNotDenied) && result.Console.Status == PlatformConsoleStatus.FirmwareServicesUnavailable && result.MemoryMap.Status == PlatformMemoryMapStatus.FirmwareServicesUnavailable && result.ConfigurationTables.Status == PlatformConfigurationTableScanStatus.FirmwareServicesUnavailable && result.Timer.Succeeded && result.Timer.Status == PlatformTimerStatus.Success && result.Timer.MonotonicTicks == 123 && result.Timer.FrequencyHertz == 1000 && timer.ReadCount == 1 && consoleSink.LineCount == 0 && memoryMapSource.CaptureCount == 0 && configurationSource.ScanCount == 0 && randomSource.FillCount == 0 && physicalSource.ReadCount == 0 && framebufferSource.CaptureCount == 0);
    }

    [HostTest("platform.uefi gone simulation rejects already-exited firmware", HostLane.Fast, 5)]
    private static bool TestSimulationRejectsAlreadyExitedFirmware()
    {
        FirmwareServicesGuard firmware = new FirmwareServicesGuard();
        _ = firmware.MarkBootServicesExited();
        ConsoleSinkDouble consoleSink = new ConsoleSinkDouble(available: true);
        MemoryMapSourceDouble memoryMapSource = new MemoryMapSourceDouble(CreateValidSnapshot());
        ConfigurationTableSourceDouble configurationSource = new ConfigurationTableSourceDouble(CreateSingleAcpi20TableBytes(), tableCount: 1);
        PlatformUefiGoneSimulationResult result = PlatformUefiGoneSimulation.Run(firmware, new GuardedPlatformConsole(firmware, consoleSink), new GuardedPlatformMemoryMap(firmware, memoryMapSource), new GuardedPlatformConfigurationTables(firmware, configurationSource));
        return HostCheck.Result(!result.Succeeded && result.Status == PlatformUefiGoneSimulationStatus.SimulationDenied && result.Transition.Mode == FirmwareServicesMode.BootServicesExited && firmware.Mode == FirmwareServicesMode.BootServicesExited && consoleSink.LineCount == 0 && memoryMapSource.CaptureCount == 0 && configurationSource.ScanCount == 0);
    }

    [HostTest("platform.config tables scans ACPI before UEFI gone", HostLane.Fast, 6)]
    private static bool TestConfigurationTablesScanBeforeUefiGone()
    {
        FirmwareServicesGuard firmware = new FirmwareServicesGuard();
        ConfigurationTableSourceDouble source = new ConfigurationTableSourceDouble(CreateSingleAcpi20TableBytes(), tableCount: 1);
        IPlatformConfigurationTables configurationTables = new GuardedPlatformConfigurationTables(firmware, source);
        PlatformConfigurationTableScanResult result = configurationTables.ScanAcpiRsdp();
        return HostCheck.Result(result.Succeeded && result.Status == PlatformConfigurationTableScanStatus.Success && result.Mode == FirmwareServicesMode.BootServicesAvailable && result.Operation == PlatformConfigurationTableOperations.ScanAcpiRsdp && result.AcpiScanAttempted && result.AcpiScanStatus == EfiConfigurationTableScanStatus.Ok && result.TableKind == EfiAcpiTableKind.Acpi20 && result.RsdpAddress == 0x1000 && source.ScanCount == 1);
    }

    [HostTest("platform.config tables rejects scan in UEFI-gone simulation", HostLane.Fast, 7)]
    private static bool TestConfigurationTablesRejectAfterUefiGone()
    {
        FirmwareServicesGuard firmware = new FirmwareServicesGuard();
        _ = firmware.EnterUefiGoneSimulation();
        ConfigurationTableSourceDouble source = new ConfigurationTableSourceDouble(CreateSingleAcpi20TableBytes(), tableCount: 1);
        IPlatformConfigurationTables configurationTables = new GuardedPlatformConfigurationTables(firmware, source);
        PlatformConfigurationTableScanResult result = configurationTables.ScanAcpiRsdp();
        return HostCheck.Result(!result.Succeeded && result.Status == PlatformConfigurationTableScanStatus.FirmwareServicesUnavailable && result.Mode == FirmwareServicesMode.UefiGoneSimulated && result.Operation == PlatformConfigurationTableOperations.ScanAcpiRsdp && !result.AcpiScanAttempted && source.ScanCount == 0);
    }

    private static UefiMemoryMapSnapshot CreateValidSnapshot()
    {
        UefiMemoryDescriptor[] descriptors = [new UefiMemoryDescriptor(UefiMemoryType.BootServicesData, 0x100000, 0, 2, 0)];
        UefiMemoryMapResult result = UefiMemoryMapResult.Captured(mapKey: 0x55, descriptorSize: 48, descriptorVersion: 1, descriptorEntries: descriptors);
        return result.Snapshot;
    }

    private static byte[] CreateSingleAcpi20TableBytes()
    {
        byte[] tableBytes = new byte[(int)EfiConfigurationTableScanner.EntrySize];
        WriteGuid(tableBytes.AsSpan(0, 16), EfiConfigurationTableScanner.Acpi20TableGuid);
        EndianPrimitives.WriteUInt64LittleEndian(tableBytes.AsSpan(16, 8), 0x1000);
        return tableBytes;
    }

    private static void WriteGuid(Span<byte> destination, EfiConfigurationTableGuid value)
    {
        EndianPrimitives.WriteUInt32LittleEndian(destination.Slice(0, 4), value.Data1);
        EndianPrimitives.WriteUInt16LittleEndian(destination.Slice(4, 2), value.Data2);
        EndianPrimitives.WriteUInt16LittleEndian(destination.Slice(6, 2), value.Data3);
        destination[8] = value.Data4;
        destination[9] = value.Data5;
        destination[10] = value.Data6;
        destination[11] = value.Data7;
        destination[12] = value.Data8;
        destination[13] = value.Data9;
        destination[14] = value.Data10;
        destination[15] = value.Data11;
    }
}
