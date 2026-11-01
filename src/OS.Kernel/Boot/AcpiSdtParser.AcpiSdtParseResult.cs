using OS.Std.Binary;

namespace OS.Boot;
public readonly struct AcpiSdtParseResult
{
    private AcpiSdtParseResult(AcpiSdtParseStatus status, AcpiSdtHeader header, int pointerCount, uint declaredLength, uint requiredLength, int requiredPointerCapacity, int entryIndex)
    {
        Status = status;
        Header = header;
        PointerCount = pointerCount;
        DeclaredLength = declaredLength;
        RequiredLength = requiredLength;
        RequiredPointerCapacity = requiredPointerCapacity;
        EntryIndex = entryIndex;
    }

    public AcpiSdtParseStatus Status { get; }
    public AcpiSdtHeader Header { get; }
    public AcpiSdtTableKind TableKind => Header.TableKind;
    public int PointerCount { get; }
    public uint DeclaredLength { get; }
    public uint RequiredLength { get; }
    public int RequiredPointerCapacity { get; }
    public int EntryIndex { get; }
    public bool Succeeded => Status == AcpiSdtParseStatus.Success;

    public static AcpiSdtParseResult Success(AcpiSdtHeader header, int pointerCount)
    {
        return new AcpiSdtParseResult(AcpiSdtParseStatus.Success, header, pointerCount, header.Length, header.Length, pointerCount, entryIndex: -1);
    }

    public static AcpiSdtParseResult Fail(AcpiSdtParseStatus status, AcpiSdtHeader header = default, uint declaredLength = 0, uint requiredLength = 0, int requiredPointerCapacity = 0, int entryIndex = -1)
    {
        return new AcpiSdtParseResult(status, header, pointerCount: 0, declaredLength, requiredLength, requiredPointerCapacity, entryIndex);
    }
}
