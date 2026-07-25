using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using OS.Boot;
using OS.Kernel.Platform;
using OS.Std.Binary;

internal static class PlatformUefiGoneSimulationTests
{
    [ModuleInitializer]
    internal static void RunAll()
    {
        int failures = 0;

        Run(
            "platform.uefi gone simulation denies all guarded adapters",
            TestSimulationDeniesAllGuardedAdapters,
            ref failures);
        Run(
            "platform.uefi gone simulation denies filesystem adapter",
            TestSimulationDeniesFileSystemAdapter,
            ref failures);
        Run(
            "platform.uefi gone simulation denies console memorymap and config adapters",
            TestSimulationDeniesGuardedAdapters,
            ref failures);
        Run(
            "platform.uefi gone simulation reports console allowance",
            TestSimulationReportsConsoleAllowance,
            ref failures);
        Run(
            "platform.uefi gone simulation reports timer allowance",
            TestSimulationReportsTimerAllowance,
            ref failures);
        Run(
            "platform.uefi gone simulation rejects already-exited firmware",
            TestSimulationRejectsAlreadyExitedFirmware,
            ref failures);
        Run(
            "platform.config tables scans ACPI before UEFI gone",
            TestConfigurationTablesScanBeforeUefiGone,
            ref failures);
        Run(
            "platform.config tables rejects scan in UEFI-gone simulation",
            TestConfigurationTablesRejectAfterUefiGone,
            ref failures);

        if (failures != 0)
        {
            throw new InvalidOperationException(
                "Platform UEFI-gone simulation host tests failed: " + failures);
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

    private static bool TestSimulationDeniesAllGuardedAdapters()
    {
        FirmwareServicesGuard firmware = new FirmwareServicesGuard();
        ConsoleSinkDouble consoleSink = new ConsoleSinkDouble(available: true);
        MemoryMapSourceDouble memoryMapSource = new MemoryMapSourceDouble(CreateValidSnapshot());
        ConfigurationTableSourceDouble configurationSource =
            new ConfigurationTableSourceDouble(CreateSingleAcpi20TableBytes(), tableCount: 1);
        TimerSourceDouble timerSource = new TimerSourceDouble();
        RandomSourceDouble randomSource = new RandomSourceDouble();
        PhysicalMemorySourceDouble physicalSource = new PhysicalMemorySourceDouble();
        FramebufferSourceDouble framebufferSource = new FramebufferSourceDouble();
        FileSystemSourceDouble fileSystemSource = new FileSystemSourceDouble();

        PlatformUefiGoneSimulationResult result = PlatformUefiGoneSimulation.Run(
            firmware,
            new GuardedPlatformConsole(firmware, consoleSink),
            new GuardedPlatformMemoryMap(firmware, memoryMapSource),
            new GuardedPlatformConfigurationTables(firmware, configurationSource),
            new GuardedPlatformTimer(firmware, timerSource),
            new GuardedPlatformRandom(firmware, randomSource),
            new GuardedPlatformPhysicalMemory(firmware, physicalSource),
            new GuardedPlatformFramebuffer(firmware, framebufferSource),
            new GuardedPlatformFileSystem(firmware, fileSystemSource));

        return result.Succeeded
            && result.Status == PlatformUefiGoneSimulationStatus.Success
            && result.Marker == PlatformUefiGoneSimulation.PlatformDeniedMarker
            && firmware.Mode == FirmwareServicesMode.UefiGoneSimulated
            && result.Console.Status == PlatformConsoleStatus.FirmwareServicesUnavailable
            && result.MemoryMap.Status == PlatformMemoryMapStatus.FirmwareServicesUnavailable
            && result.ConfigurationTables.Status
                == PlatformConfigurationTableScanStatus.FirmwareServicesUnavailable
            && result.Timer.Status == PlatformTimerStatus.FirmwareServicesUnavailable
            && result.Timer.Mode == FirmwareServicesMode.UefiGoneSimulated
            && result.Timer.Operation == PlatformTimerOperations.ReadMonotonic
            && result.Random.Status == PlatformRandomStatus.FirmwareServicesUnavailable
            && result.Random.Mode == FirmwareServicesMode.UefiGoneSimulated
            && result.Random.Operation == PlatformRandomOperations.FillBytes
            && result.PhysicalMemory.Status
                == PlatformPhysicalMemoryStatus.FirmwareServicesUnavailable
            && result.PhysicalMemory.Mode == FirmwareServicesMode.UefiGoneSimulated
            && result.PhysicalMemory.Operation == PlatformPhysicalMemoryOperations.ReadBytes
            && result.Framebuffer.Status == PlatformFramebufferStatus.FirmwareServicesUnavailable
            && result.Framebuffer.Mode == FirmwareServicesMode.UefiGoneSimulated
            && result.Framebuffer.Operation == PlatformFramebufferOperations.CaptureSnapshot
            && result.FileSystem.Status == PlatformFileSystemStatus.FirmwareServicesUnavailable
            && result.FileSystem.Mode == FirmwareServicesMode.UefiGoneSimulated
            && result.FileSystem.Operation == PlatformFileSystemOperations.ReadFile
            && consoleSink.LineCount == 0
            && memoryMapSource.CaptureCount == 0
            && configurationSource.ScanCount == 0
            && timerSource.ReadCount == 0
            && randomSource.FillCount == 0
            && physicalSource.ReadCount == 0
            && framebufferSource.CaptureCount == 0
            && fileSystemSource.ReadCount == 0;
    }

    private static bool TestSimulationDeniesFileSystemAdapter()
    {
        FirmwareServicesGuard firmware = new FirmwareServicesGuard();
        ConsoleSinkDouble consoleSink = new ConsoleSinkDouble(available: true);
        MemoryMapSourceDouble memoryMapSource = new MemoryMapSourceDouble(CreateValidSnapshot());
        ConfigurationTableSourceDouble configurationSource =
            new ConfigurationTableSourceDouble(CreateSingleAcpi20TableBytes(), tableCount: 1);
        TimerSourceDouble timerSource = new TimerSourceDouble();
        RandomSourceDouble randomSource = new RandomSourceDouble();
        PhysicalMemorySourceDouble physicalSource = new PhysicalMemorySourceDouble();
        FramebufferSourceDouble framebufferSource = new FramebufferSourceDouble();
        FileSystemSourceDouble fileSystemSource = new FileSystemSourceDouble();

        PlatformUefiGoneSimulationResult result = PlatformUefiGoneSimulation.Run(
            firmware,
            new GuardedPlatformConsole(firmware, consoleSink),
            new GuardedPlatformMemoryMap(firmware, memoryMapSource),
            new GuardedPlatformConfigurationTables(firmware, configurationSource),
            new GuardedPlatformTimer(firmware, timerSource),
            new GuardedPlatformRandom(firmware, randomSource),
            new GuardedPlatformPhysicalMemory(firmware, physicalSource),
            new GuardedPlatformFramebuffer(firmware, framebufferSource),
            new GuardedPlatformFileSystem(firmware, fileSystemSource));

        return result.Succeeded
            && result.FileSystem.Status == PlatformFileSystemStatus.FirmwareServicesUnavailable
            && result.FileSystem.Mode == FirmwareServicesMode.UefiGoneSimulated
            && result.FileSystem.Operation == PlatformFileSystemOperations.ReadFile
            && fileSystemSource.ReadCount == 0
            && consoleSink.LineCount == 0
            && memoryMapSource.CaptureCount == 0
            && configurationSource.ScanCount == 0
            && timerSource.ReadCount == 0
            && randomSource.FillCount == 0
            && physicalSource.ReadCount == 0
            && framebufferSource.CaptureCount == 0;
    }

    private static bool TestSimulationDeniesGuardedAdapters()
    {
        FirmwareServicesGuard firmware = new FirmwareServicesGuard();
        ConsoleSinkDouble consoleSink = new ConsoleSinkDouble(available: true);
        MemoryMapSourceDouble memoryMapSource = new MemoryMapSourceDouble(CreateValidSnapshot());
        ConfigurationTableSourceDouble configurationSource =
            new ConfigurationTableSourceDouble(CreateSingleAcpi20TableBytes(), tableCount: 1);

        IPlatformConsole console = new GuardedPlatformConsole(firmware, consoleSink);
        IPlatformMemoryMap memoryMap = new GuardedPlatformMemoryMap(firmware, memoryMapSource);
        IPlatformConfigurationTables configurationTables =
            new GuardedPlatformConfigurationTables(firmware, configurationSource);

        PlatformUefiGoneSimulationResult result = PlatformUefiGoneSimulation.Run(
            firmware,
            console,
            memoryMap,
            configurationTables);

        return result.Succeeded
            && result.Status == PlatformUefiGoneSimulationStatus.Success
            && result.Marker == PlatformUefiGoneSimulation.PlatformDeniedMarker
            && firmware.Mode == FirmwareServicesMode.UefiGoneSimulated
            && result.Transition.Succeeded
            && result.Console.Status == PlatformConsoleStatus.FirmwareServicesUnavailable
            && result.Console.Mode == FirmwareServicesMode.UefiGoneSimulated
            && result.Console.Operation == PlatformConsoleOperations.OutputString
            && result.MemoryMap.Status == PlatformMemoryMapStatus.FirmwareServicesUnavailable
            && result.MemoryMap.Mode == FirmwareServicesMode.UefiGoneSimulated
            && result.MemoryMap.Operation == PlatformMemoryMapOperations.GetMemoryMap
            && result.ConfigurationTables.Status
                == PlatformConfigurationTableScanStatus.FirmwareServicesUnavailable
            && result.ConfigurationTables.Mode == FirmwareServicesMode.UefiGoneSimulated
            && result.ConfigurationTables.Operation
                == PlatformConfigurationTableOperations.ScanAcpiRsdp
            && consoleSink.LineCount == 0
            && memoryMapSource.CaptureCount == 0
            && configurationSource.ScanCount == 0;
    }

    private static bool TestSimulationReportsConsoleAllowance()
    {
        FirmwareServicesGuard firmware = new FirmwareServicesGuard();
        UnguardedConsole console = new UnguardedConsole();
        MemoryMapSourceDouble memoryMapSource = new MemoryMapSourceDouble(CreateValidSnapshot());
        ConfigurationTableSourceDouble configurationSource =
            new ConfigurationTableSourceDouble(CreateSingleAcpi20TableBytes(), tableCount: 1);

        IPlatformMemoryMap memoryMap = new GuardedPlatformMemoryMap(firmware, memoryMapSource);
        IPlatformConfigurationTables configurationTables =
            new GuardedPlatformConfigurationTables(firmware, configurationSource);

        PlatformUefiGoneSimulationResult result = PlatformUefiGoneSimulation.Run(
            firmware,
            console,
            memoryMap,
            configurationTables);

        return !result.Succeeded
            && result.Status == PlatformUefiGoneSimulationStatus.ConsoleCallNotDenied
            && result.Marker
                == PlatformUefiGoneSimulation.FailMarkerPrefix
                    + nameof(PlatformUefiGoneSimulationStatus.ConsoleCallNotDenied)
            && console.WriteCount == 1
            && memoryMapSource.CaptureCount == 0
            && configurationSource.ScanCount == 0;
    }

    private static bool TestSimulationReportsTimerAllowance()
    {
        FirmwareServicesGuard firmware = new FirmwareServicesGuard();
        ConsoleSinkDouble consoleSink = new ConsoleSinkDouble(available: true);
        MemoryMapSourceDouble memoryMapSource = new MemoryMapSourceDouble(CreateValidSnapshot());
        ConfigurationTableSourceDouble configurationSource =
            new ConfigurationTableSourceDouble(CreateSingleAcpi20TableBytes(), tableCount: 1);
        UnguardedTimer timer = new UnguardedTimer();
        RandomSourceDouble randomSource = new RandomSourceDouble();
        PhysicalMemorySourceDouble physicalSource = new PhysicalMemorySourceDouble();
        FramebufferSourceDouble framebufferSource = new FramebufferSourceDouble();

        PlatformUefiGoneSimulationResult result = PlatformUefiGoneSimulation.Run(
            firmware,
            new GuardedPlatformConsole(firmware, consoleSink),
            new GuardedPlatformMemoryMap(firmware, memoryMapSource),
            new GuardedPlatformConfigurationTables(firmware, configurationSource),
            timer,
            new GuardedPlatformRandom(firmware, randomSource),
            new GuardedPlatformPhysicalMemory(firmware, physicalSource),
            new GuardedPlatformFramebuffer(firmware, framebufferSource));

        return !result.Succeeded
            && result.Status == PlatformUefiGoneSimulationStatus.TimerCallNotDenied
            && result.Marker
                == PlatformUefiGoneSimulation.FailMarkerPrefix
                    + nameof(PlatformUefiGoneSimulationStatus.TimerCallNotDenied)
            && result.Console.Status == PlatformConsoleStatus.FirmwareServicesUnavailable
            && result.MemoryMap.Status == PlatformMemoryMapStatus.FirmwareServicesUnavailable
            && result.ConfigurationTables.Status
                == PlatformConfigurationTableScanStatus.FirmwareServicesUnavailable
            && result.Timer.Succeeded
            && result.Timer.Status == PlatformTimerStatus.Success
            && result.Timer.MonotonicTicks == 123
            && result.Timer.FrequencyHertz == 1000
            && timer.ReadCount == 1
            && consoleSink.LineCount == 0
            && memoryMapSource.CaptureCount == 0
            && configurationSource.ScanCount == 0
            && randomSource.FillCount == 0
            && physicalSource.ReadCount == 0
            && framebufferSource.CaptureCount == 0;
    }

    private static bool TestSimulationRejectsAlreadyExitedFirmware()
    {
        FirmwareServicesGuard firmware = new FirmwareServicesGuard();
        _ = firmware.MarkBootServicesExited();
        ConsoleSinkDouble consoleSink = new ConsoleSinkDouble(available: true);
        MemoryMapSourceDouble memoryMapSource = new MemoryMapSourceDouble(CreateValidSnapshot());
        ConfigurationTableSourceDouble configurationSource =
            new ConfigurationTableSourceDouble(CreateSingleAcpi20TableBytes(), tableCount: 1);

        PlatformUefiGoneSimulationResult result = PlatformUefiGoneSimulation.Run(
            firmware,
            new GuardedPlatformConsole(firmware, consoleSink),
            new GuardedPlatformMemoryMap(firmware, memoryMapSource),
            new GuardedPlatformConfigurationTables(firmware, configurationSource));

        return !result.Succeeded
            && result.Status == PlatformUefiGoneSimulationStatus.SimulationDenied
            && result.Transition.Mode == FirmwareServicesMode.BootServicesExited
            && firmware.Mode == FirmwareServicesMode.BootServicesExited
            && consoleSink.LineCount == 0
            && memoryMapSource.CaptureCount == 0
            && configurationSource.ScanCount == 0;
    }

    private static bool TestConfigurationTablesScanBeforeUefiGone()
    {
        FirmwareServicesGuard firmware = new FirmwareServicesGuard();
        ConfigurationTableSourceDouble source =
            new ConfigurationTableSourceDouble(CreateSingleAcpi20TableBytes(), tableCount: 1);
        IPlatformConfigurationTables configurationTables =
            new GuardedPlatformConfigurationTables(firmware, source);

        PlatformConfigurationTableScanResult result = configurationTables.ScanAcpiRsdp();

        return result.Succeeded
            && result.Status == PlatformConfigurationTableScanStatus.Success
            && result.Mode == FirmwareServicesMode.BootServicesAvailable
            && result.Operation == PlatformConfigurationTableOperations.ScanAcpiRsdp
            && result.AcpiScanAttempted
            && result.AcpiScanStatus == EfiConfigurationTableScanStatus.Ok
            && result.TableKind == EfiAcpiTableKind.Acpi20
            && result.RsdpAddress == 0x1000
            && source.ScanCount == 1;
    }

    private static bool TestConfigurationTablesRejectAfterUefiGone()
    {
        FirmwareServicesGuard firmware = new FirmwareServicesGuard();
        _ = firmware.EnterUefiGoneSimulation();
        ConfigurationTableSourceDouble source =
            new ConfigurationTableSourceDouble(CreateSingleAcpi20TableBytes(), tableCount: 1);
        IPlatformConfigurationTables configurationTables =
            new GuardedPlatformConfigurationTables(firmware, source);

        PlatformConfigurationTableScanResult result = configurationTables.ScanAcpiRsdp();

        return !result.Succeeded
            && result.Status == PlatformConfigurationTableScanStatus.FirmwareServicesUnavailable
            && result.Mode == FirmwareServicesMode.UefiGoneSimulated
            && result.Operation == PlatformConfigurationTableOperations.ScanAcpiRsdp
            && !result.AcpiScanAttempted
            && source.ScanCount == 0;
    }

    private static UefiMemoryMapSnapshot CreateValidSnapshot()
    {
        UefiMemoryDescriptor[] descriptors =
        [
            new UefiMemoryDescriptor(
                UefiMemoryType.BootServicesData,
                0x100000,
                0,
                2,
                0)
        ];

        UefiMemoryMapResult result = UefiMemoryMapResult.Captured(
            mapKey: 0x55,
            descriptorSize: 48,
            descriptorVersion: 1,
            descriptorEntries: descriptors);

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

    private sealed class ConsoleSinkDouble : IPlatformConsoleSink
    {
        public ConsoleSinkDouble(bool available)
        {
            Available = available;
        }

        public bool Available { get; }

        public int LineCount { get; private set; }

        public void WriteLine(string line)
        {
            LineCount++;
        }
    }

    private sealed class MemoryMapSourceDouble : IPlatformMemoryMapSource
    {
        private readonly UefiMemoryMapSnapshot _snapshot;

        public MemoryMapSourceDouble(UefiMemoryMapSnapshot snapshot)
        {
            _snapshot = snapshot;
        }

        public int CaptureCount { get; private set; }

        public UefiMemoryMapSnapshot CaptureSnapshot()
        {
            CaptureCount++;
            return _snapshot;
        }
    }

    private sealed class ConfigurationTableSourceDouble : IPlatformConfigurationTableSource
    {
        private readonly GCHandle _tableHandle;
        private readonly ulong _tableCount;

        public ConfigurationTableSourceDouble(byte[] tableBytes, ulong tableCount)
        {
            _tableHandle = GCHandle.Alloc(tableBytes, GCHandleType.Pinned);
            _tableCount = tableCount;
        }

        public int ScanCount { get; private set; }

        public PlatformConfigurationTableMetadata GetMetadata()
        {
            ScanCount++;
            return new PlatformConfigurationTableMetadata(
                unchecked((ulong)_tableHandle.AddrOfPinnedObject().ToInt64()),
                _tableCount);
        }
    }

    private sealed class TimerSourceDouble : IPlatformTimerSource
    {
        public int ReadCount { get; private set; }

        public PlatformTimerMetadata ReadMonotonic()
        {
            ReadCount++;
            return new PlatformTimerMetadata(monotonicTicks: 123, frequencyHertz: 1000);
        }
    }

    private sealed class RandomSourceDouble : IPlatformRandomSource
    {
        public int FillCount { get; private set; }

        public PlatformRandomSourceResult FillBytes(Span<byte> destination)
        {
            FillCount++;
            destination[0] = 0x5A;
            return PlatformRandomSourceResult.Filled(destination.Length);
        }
    }

    private sealed class PhysicalMemorySourceDouble : IPlatformPhysicalMemorySource
    {
        public int ReadCount { get; private set; }

        public PlatformPhysicalMemorySourceResult ReadBytes(
            ulong physicalAddress,
            int byteCount,
            Span<byte> destination)
        {
            ReadCount++;
            destination[0] = 0xA5;
            return PlatformPhysicalMemorySourceResult.Read(byteCount);
        }
    }

    private sealed class FramebufferSourceDouble : IPlatformFramebufferSource
    {
        public int CaptureCount { get; private set; }

        public PlatformFramebufferSourceResult CaptureSnapshot()
        {
            CaptureCount++;
            return PlatformFramebufferSourceResult.Captured(
                new PlatformFramebufferMetadata(
                    baseAddress: 0xE0000000,
                    byteLength: 800 * 600 * PlatformFramebufferResult.BytesPerPixel,
                    width: 800,
                    height: 600,
                    stridePixels: 800,
                    PlatformFramebufferPixelFormat.Rgbx8888));
        }
    }

    private sealed class FileSystemSourceDouble : IPlatformFileSystemSource
    {
        public int ReadCount { get; private set; }

        public PlatformFileSystemSourceReadResult ReadFile(
            ReadOnlySpan<byte> relativePath,
            Span<byte> destination)
        {
            ReadCount++;
            if (destination.Length == 0)
            {
                return PlatformFileSystemSourceReadResult.DestinationTooSmall(1, 0);
            }

            destination[0] = 0x42;
            return PlatformFileSystemSourceReadResult.Read(1, destination.Length);
        }
    }

    private sealed class UnguardedConsole : IPlatformConsole
    {
        public int WriteCount { get; private set; }

        public PlatformConsoleResult WriteLine(string line)
        {
            WriteCount++;
            return PlatformConsoleResult.Written(
                FirmwareCallResult.Allowed(
                    PlatformConsoleOperations.OutputString,
                    FirmwareServicesMode.BootServicesAvailable),
                line);
        }
    }

    private sealed class UnguardedTimer : IPlatformTimer
    {
        public int ReadCount { get; private set; }

        public PlatformTimerResult ReadMonotonic()
        {
            ReadCount++;
            return PlatformTimerResult.Read(
                FirmwareCallResult.Allowed(
                    PlatformTimerOperations.ReadMonotonic,
                    FirmwareServicesMode.BootServicesAvailable),
                new PlatformTimerMetadata(monotonicTicks: 123, frequencyHertz: 1000));
        }
    }
}
