using OS.Std.Binary;

namespace OS.Kernel.Diagnostics;

public readonly struct GdtDescriptorPlanResult
{
    private GdtDescriptorPlanResult(bool succeeded, GdtDescriptorPlanStatus status, int bytesWritten, int requiredBytes)
    {
        Succeeded = succeeded;
        Status = status;
        BytesWritten = bytesWritten;
        RequiredBytes = requiredBytes;
    }

    public bool Succeeded
    {
        get;
    }
    public GdtDescriptorPlanStatus Status
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

    public static GdtDescriptorPlanResult Success(int bytesWritten)
    {
        return new GdtDescriptorPlanResult(true, GdtDescriptorPlanStatus.Ok, bytesWritten, GdtDescriptorPlan.RequiredByteLength);
    }

    public static GdtDescriptorPlanResult Fail(GdtDescriptorPlanStatus status, int bytesWritten, int requiredBytes = GdtDescriptorPlan.RequiredByteLength)
    {
        return new GdtDescriptorPlanResult(false, status, bytesWritten, requiredBytes);
    }
}
