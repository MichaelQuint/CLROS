using OS.Std.Binary;

namespace OS.Kernel.Diagnostics;
public readonly struct CpuFaultEntryFrameLayoutResult
{
    private CpuFaultEntryFrameLayoutResult(bool succeeded, CpuFaultEntryFrameLayoutStatus status, CpuFaultFrame frame, ushort codeSegment, ushort stackSegment, int vector, int requiredBytes)
    {
        Succeeded = succeeded;
        Status = status;
        Frame = frame;
        CodeSegment = codeSegment;
        StackSegment = stackSegment;
        Vector = vector;
        RequiredBytes = requiredBytes;
    }

    public bool Succeeded { get; }
    public CpuFaultEntryFrameLayoutStatus Status { get; }
    public CpuFaultFrame Frame { get; }
    public ushort CodeSegment { get; }
    public ushort StackSegment { get; }
    public int Vector { get; }
    public int RequiredBytes { get; }

    public static CpuFaultEntryFrameLayoutResult Success(in CpuFaultFrame frame, ushort codeSegment, ushort stackSegment)
    {
        return new CpuFaultEntryFrameLayoutResult(true, CpuFaultEntryFrameLayoutStatus.Ok, frame, codeSegment, stackSegment, frame.Vector, CpuFaultEntryFrameLayout.TotalSize);
    }

    public static CpuFaultEntryFrameLayoutResult Fail(CpuFaultEntryFrameLayoutStatus status, int vector = 0)
    {
        return new CpuFaultEntryFrameLayoutResult(false, status, default, 0, 0, vector, CpuFaultEntryFrameLayout.TotalSize);
    }
}
