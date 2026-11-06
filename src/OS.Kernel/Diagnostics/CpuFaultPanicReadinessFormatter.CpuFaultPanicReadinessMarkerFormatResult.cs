using OS.Std.Text;

namespace OS.Kernel.Diagnostics;

public readonly struct CpuFaultPanicReadinessMarkerFormatResult
{
    private const uint InitializedFlag = 1;
    public CpuFaultPanicReadinessMarkerFormatResult(CpuFaultPanicReadinessMarkerFormatStatus status, int bytesWritten, int requiredBytes, uint flags = InitializedFlag)
    {
        Status = status;
        BytesWritten = bytesWritten;
        RequiredBytes = requiredBytes;
        Flags = flags;
    }

    public CpuFaultPanicReadinessMarkerFormatStatus Status
    {
        get;
    }
    public int BytesWritten
    {
        get;
    }
    public int RequiredBytes
    {
        get;
    }
    public uint Flags
    {
        get;
    }
    public bool IsInitialized => (Flags & InitializedFlag) != 0;
    public bool Succeeded => IsInitialized && Status == CpuFaultPanicReadinessMarkerFormatStatus.Success && RequiredBytes != 0 && BytesWritten == RequiredBytes;
}
