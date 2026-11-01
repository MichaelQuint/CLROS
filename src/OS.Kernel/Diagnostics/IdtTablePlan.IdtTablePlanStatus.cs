namespace OS.Kernel.Diagnostics;
public enum IdtTablePlanStatus
{
    Ok = 0,
    TableTooSmall = 1,
    HandlerTableTooSmall = 2,
    DescriptorBufferTooSmall = 3,
    InvalidDescriptor = 4,
    MissingRequiredVector = 5,
    DoubleFaultIstRequired = 6
}
