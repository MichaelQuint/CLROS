using OS.Drivers;
using OS.Std.Binary;

namespace OS.Boot;
public readonly struct AcpiMcfgParseResult
{
    private AcpiMcfgParseResult(AcpiMcfgParseStatus status, int segmentCount, uint declaredLength, uint requiredLength, int requiredSegmentCapacity, int entryIndex)
    {
        Status = status;
        SegmentCount = segmentCount;
        DeclaredLength = declaredLength;
        RequiredLength = requiredLength;
        RequiredSegmentCapacity = requiredSegmentCapacity;
        EntryIndex = entryIndex;
    }

    public AcpiMcfgParseStatus Status { get; }
    public int SegmentCount { get; }
    public uint DeclaredLength { get; }
    public uint RequiredLength { get; }
    public int RequiredSegmentCapacity { get; }
    public int EntryIndex { get; }
    public bool Succeeded => Status == AcpiMcfgParseStatus.Success;

    public static AcpiMcfgParseResult Success(int segmentCount, uint declaredLength)
    {
        return new AcpiMcfgParseResult(AcpiMcfgParseStatus.Success, segmentCount, declaredLength, requiredLength: declaredLength, requiredSegmentCapacity: segmentCount, entryIndex: -1);
    }

    public static AcpiMcfgParseResult Fail(AcpiMcfgParseStatus status, uint declaredLength = 0, uint requiredLength = 0, int requiredSegmentCapacity = 0, int entryIndex = -1)
    {
        return new AcpiMcfgParseResult(status, segmentCount: 0, declaredLength, requiredLength, requiredSegmentCapacity, entryIndex);
    }
}
