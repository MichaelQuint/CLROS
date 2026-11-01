using OS.Std.Binary;

namespace OS.Kernel.Diagnostics;
public readonly struct IdtrDescriptorResult
{
    private IdtrDescriptorResult(bool succeeded, IdtrDescriptorStatus status, IdtrDescriptor descriptor, int actualBytes, int requiredBytes)
    {
        Succeeded = succeeded;
        Status = status;
        Descriptor = descriptor;
        ActualBytes = actualBytes;
        RequiredBytes = requiredBytes;
    }

    public bool Succeeded { get; }
    public IdtrDescriptorStatus Status { get; }
    public IdtrDescriptor Descriptor { get; }
    public int ActualBytes { get; }
    public int RequiredBytes { get; }

    public static IdtrDescriptorResult Success(IdtrDescriptor descriptor, int actualBytes, int requiredBytes = IdtrDescriptor.RequiredTableByteLength)
    {
        return new IdtrDescriptorResult(true, IdtrDescriptorStatus.Ok, descriptor, actualBytes, requiredBytes);
    }

    public static IdtrDescriptorResult Fail(IdtrDescriptorStatus status, int actualBytes, int requiredBytes = IdtrDescriptor.RequiredTableByteLength)
    {
        return new IdtrDescriptorResult(false, status, default, actualBytes, requiredBytes);
    }
}
