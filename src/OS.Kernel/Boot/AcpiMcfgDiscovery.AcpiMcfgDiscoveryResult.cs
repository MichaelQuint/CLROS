using OS.Drivers;
using OS.Kernel.Platform;
using OS.Std.Binary;

namespace OS.Boot;
public readonly struct AcpiMcfgDiscoveryResult
{
    private AcpiMcfgDiscoveryResult(AcpiMcfgDiscoveryStatus status, AcpiTableDirectoryResult directoryResult, AcpiMcfgParseResult mcfgParseResult, bool mcfgParseAttempted)
    {
        Status = status;
        DirectoryResult = directoryResult;
        McfgParseResult = mcfgParseResult;
        McfgParseAttempted = mcfgParseAttempted;
    }

    public AcpiMcfgDiscoveryStatus Status { get; }
    public AcpiTableDirectoryResult DirectoryResult { get; }
    public AcpiMcfgParseResult McfgParseResult { get; }
    public bool McfgParseAttempted { get; }
    public bool Succeeded => Status == AcpiMcfgDiscoveryStatus.Success;
    public AcpiTableDirectoryStatus DirectoryStatus => DirectoryResult.Status;
    public AcpiMcfgParseStatus McfgParseStatus => McfgParseResult.Status;
    public ulong SelectedPhysicalAddress => DirectoryResult.PhysicalAddress;
    public int SelectedPointerIndex => DirectoryResult.PointerIndex;
    public int SelectedSourceIndex => DirectoryResult.SourceIndex;
    public int PointerCount => DirectoryResult.PointerCount;
    public int SegmentCount => McfgParseResult.SegmentCount;
    public int RequiredSegmentCapacity => McfgParseResult.RequiredSegmentCapacity;
    public uint SelectedTableLength => DirectoryResult.DeclaredLength;

    public static AcpiMcfgDiscoveryResult Success(AcpiTableDirectoryResult directoryResult, AcpiMcfgParseResult mcfgParseResult)
    {
        return new AcpiMcfgDiscoveryResult(AcpiMcfgDiscoveryStatus.Success, directoryResult, mcfgParseResult, mcfgParseAttempted: true);
    }

    public static AcpiMcfgDiscoveryResult DirectoryFailed(AcpiTableDirectoryResult directoryResult)
    {
        return new AcpiMcfgDiscoveryResult(AcpiMcfgDiscoveryStatus.DirectoryFailed, directoryResult, default, mcfgParseAttempted: false);
    }

    public static AcpiMcfgDiscoveryResult McfgParseFailed(AcpiTableDirectoryResult directoryResult, AcpiMcfgParseResult mcfgParseResult)
    {
        return new AcpiMcfgDiscoveryResult(AcpiMcfgDiscoveryStatus.McfgParseFailed, directoryResult, mcfgParseResult, mcfgParseAttempted: true);
    }

    public static AcpiMcfgDiscoveryResult SelectedSourceInvalid(AcpiTableDirectoryResult directoryResult)
    {
        return new AcpiMcfgDiscoveryResult(AcpiMcfgDiscoveryStatus.SelectedSourceInvalid, directoryResult, default, mcfgParseAttempted: false);
    }
}
