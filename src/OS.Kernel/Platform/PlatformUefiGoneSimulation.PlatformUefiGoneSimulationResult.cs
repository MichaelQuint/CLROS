namespace OS.Kernel.Platform;

public readonly struct PlatformUefiGoneSimulationResult
{
    private PlatformUefiGoneSimulationResult(PlatformUefiGoneSimulationStatus status, FirmwareCallResult transition, PlatformConsoleResult console, PlatformMemoryMapResult memoryMap, PlatformConfigurationTableScanResult configurationTables, PlatformTimerResult timer, PlatformRandomResult random, PlatformPhysicalMemoryResult physicalMemory, PlatformFramebufferResult framebuffer, PlatformFileSystemReadResult fileSystem)
    {
        Status = status;
        Transition = transition;
        Console = console;
        MemoryMap = memoryMap;
        ConfigurationTables = configurationTables;
        Timer = timer;
        Random = random;
        PhysicalMemory = physicalMemory;
        Framebuffer = framebuffer;
        FileSystem = fileSystem;
    }

    public PlatformUefiGoneSimulationStatus Status
    {
        get;
    }
    public FirmwareCallResult Transition
    {
        get;
    }
    public PlatformConsoleResult Console
    {
        get;
    }
    public PlatformMemoryMapResult MemoryMap
    {
        get;
    }
    public PlatformConfigurationTableScanResult ConfigurationTables
    {
        get;
    }
    public PlatformTimerResult Timer
    {
        get;
    }
    public PlatformRandomResult Random
    {
        get;
    }
    public PlatformPhysicalMemoryResult PhysicalMemory
    {
        get;
    }
    public PlatformFramebufferResult Framebuffer
    {
        get;
    }
    public PlatformFileSystemReadResult FileSystem
    {
        get;
    }
    public bool Succeeded => Status == PlatformUefiGoneSimulationStatus.Success;
    public string Marker => Succeeded ? PlatformUefiGoneSimulation.PlatformDeniedMarker : PlatformUefiGoneSimulation.FailMarkerPrefix + Status;

    public static PlatformUefiGoneSimulationResult FromStatus(PlatformUefiGoneSimulationStatus status, FirmwareCallResult transition, PlatformConsoleResult console, PlatformMemoryMapResult memoryMap, PlatformConfigurationTableScanResult configurationTables)
    {
        return FromStatus(status, transition, console, memoryMap, configurationTables, default, default, default, default, default);
    }

    public static PlatformUefiGoneSimulationResult FromStatus(PlatformUefiGoneSimulationStatus status, FirmwareCallResult transition, PlatformConsoleResult console, PlatformMemoryMapResult memoryMap, PlatformConfigurationTableScanResult configurationTables, PlatformTimerResult timer, PlatformRandomResult random, PlatformPhysicalMemoryResult physicalMemory, PlatformFramebufferResult framebuffer)
    {
        return FromStatus(status, transition, console, memoryMap, configurationTables, timer, random, physicalMemory, framebuffer, default);
    }

    public static PlatformUefiGoneSimulationResult FromStatus(PlatformUefiGoneSimulationStatus status, FirmwareCallResult transition, PlatformConsoleResult console, PlatformMemoryMapResult memoryMap, PlatformConfigurationTableScanResult configurationTables, PlatformTimerResult timer, PlatformRandomResult random, PlatformPhysicalMemoryResult physicalMemory, PlatformFramebufferResult framebuffer, PlatformFileSystemReadResult fileSystem)
    {
        return new PlatformUefiGoneSimulationResult(status, transition, console, memoryMap, configurationTables, timer, random, physicalMemory, framebuffer, fileSystem);
    }
}
