using OS.Std.Binary;

namespace OS.Kernel.Diagnostics;
public readonly struct IdtGateDescriptorResult
{
    private IdtGateDescriptorResult(bool succeeded, IdtGateDescriptorStatus status, IdtGateDescriptor descriptor)
    {
        Succeeded = succeeded;
        Status = status;
        Descriptor = descriptor;
    }

    public bool Succeeded { get; }
    public IdtGateDescriptorStatus Status { get; }
    public IdtGateDescriptor Descriptor { get; }

    public static IdtGateDescriptorResult Success(IdtGateDescriptor descriptor)
    {
        return new IdtGateDescriptorResult(true, IdtGateDescriptorStatus.Ok, descriptor);
    }

    public static IdtGateDescriptorResult Fail(IdtGateDescriptorStatus status)
    {
        return new IdtGateDescriptorResult(false, status, default);
    }
}
