using OS.Std.Binary;

namespace OS.Drivers;
public static class VirtioPciBarRegisterWindowLiveAccessorBoundary
{
    public const bool RequiresAccessPlan = true;
    public const bool RequiresInjectedAccessor = true;
    public const bool ConsumesMappedRegisterWindowAccessPlan = true;
    public const bool DelegatesOnlyPlannedAccessorOperations = true;
    public const bool ContainsLiveMmioImplementation = false;
    public const bool ContainsPortIoImplementation = false;
    public const bool MapsBars = false;
    public const bool AllocatesDmaMemory = false;
    public const bool PerformsDmaMapping = false;
    public const bool PerformsPacketIo = false;
    public const bool ConfiguresQueuesByItself = false;
    public const bool NotifiesDeviceByItself = false;
    public const bool PollsDevice = false;
    public const bool HandlesInterrupts = false;
    public static VirtioPciBarRegisterWindowLiveAccessorResult Read16(VirtioPciBarRegisterWindowAccessPlan plan, IVirtioPciRegisterAccessor? accessor)
    {
        VirtioPciBarRegisterWindowLiveAccessorStatus status = ValidatePlan(plan, VirtioPciRegisterAccessWidth.Word, isRead: true);
        if (status != VirtioPciBarRegisterWindowLiveAccessorStatus.Success)
        {
            return VirtioPciBarRegisterWindowLiveAccessorResult.FromPlan(status, VirtioPciBarRegisterWindowLiveAccessorOperation.Read16, plan, VirtioPciRegisterReadStatus.NotReady, VirtioPciRegisterWriteStatus.Success, value: 0);
        }

        if (accessor is null)
        {
            return VirtioPciBarRegisterWindowLiveAccessorResult.FromPlan(VirtioPciBarRegisterWindowLiveAccessorStatus.AccessorNotReady, VirtioPciBarRegisterWindowLiveAccessorOperation.Read16, plan, VirtioPciRegisterReadStatus.NotReady, VirtioPciRegisterWriteStatus.Success, value: 0);
        }

        VirtioPciRegisterRead16Result read = accessor.Read16(plan.Binding.Aperture, plan.RelativeOffset);
        if (read.Succeeded)
        {
            return VirtioPciBarRegisterWindowLiveAccessorResult.FromPlan(VirtioPciBarRegisterWindowLiveAccessorStatus.Success, VirtioPciBarRegisterWindowLiveAccessorOperation.Read16, plan, read.Status, VirtioPciRegisterWriteStatus.Success, read.Value);
        }

        return VirtioPciBarRegisterWindowLiveAccessorResult.FromPlan(read.Status == VirtioPciRegisterReadStatus.NotReady ? VirtioPciBarRegisterWindowLiveAccessorStatus.AccessorNotReady : VirtioPciBarRegisterWindowLiveAccessorStatus.RegisterReadFailed, VirtioPciBarRegisterWindowLiveAccessorOperation.Read16, plan, read.Status, VirtioPciRegisterWriteStatus.Success, read.Value);
    }

    public static VirtioPciBarRegisterWindowLiveAccessorResult Write16(VirtioPciBarRegisterWindowAccessPlan plan, ushort value, IVirtioPciRegisterAccessor? accessor)
    {
        return Write(plan, VirtioPciBarRegisterWindowLiveAccessorOperation.Write16, VirtioPciRegisterAccessWidth.Word, value, accessor);
    }

    public static VirtioPciBarRegisterWindowLiveAccessorResult Write64(VirtioPciBarRegisterWindowAccessPlan plan, ulong value, IVirtioPciRegisterAccessor? accessor)
    {
        return Write(plan, VirtioPciBarRegisterWindowLiveAccessorOperation.Write64, VirtioPciRegisterAccessWidth.QWord, value, accessor);
    }

    private static VirtioPciBarRegisterWindowLiveAccessorResult Write(VirtioPciBarRegisterWindowAccessPlan plan, VirtioPciBarRegisterWindowLiveAccessorOperation operation, VirtioPciRegisterAccessWidth expectedWidth, ulong value, IVirtioPciRegisterAccessor? accessor)
    {
        VirtioPciBarRegisterWindowLiveAccessorStatus status = ValidatePlan(plan, expectedWidth, isRead: false);
        if (status != VirtioPciBarRegisterWindowLiveAccessorStatus.Success)
        {
            return VirtioPciBarRegisterWindowLiveAccessorResult.FromPlan(status, operation, plan, VirtioPciRegisterReadStatus.Success, VirtioPciRegisterWriteStatus.NotReady, value: 0);
        }

        if (accessor is null)
        {
            return VirtioPciBarRegisterWindowLiveAccessorResult.FromPlan(VirtioPciBarRegisterWindowLiveAccessorStatus.AccessorNotReady, operation, plan, VirtioPciRegisterReadStatus.Success, VirtioPciRegisterWriteStatus.NotReady, value: 0);
        }

        VirtioPciRegisterWriteStatus writeStatus = operation == VirtioPciBarRegisterWindowLiveAccessorOperation.Write16 ? accessor.Write16(plan.Binding.Aperture, plan.RelativeOffset, (ushort)value) : accessor.Write64(plan.Binding.Aperture, plan.RelativeOffset, value);
        if (writeStatus == VirtioPciRegisterWriteStatus.Success)
        {
            return VirtioPciBarRegisterWindowLiveAccessorResult.FromPlan(VirtioPciBarRegisterWindowLiveAccessorStatus.Success, operation, plan, VirtioPciRegisterReadStatus.Success, writeStatus, value);
        }

        return VirtioPciBarRegisterWindowLiveAccessorResult.FromPlan(writeStatus == VirtioPciRegisterWriteStatus.NotReady ? VirtioPciBarRegisterWindowLiveAccessorStatus.AccessorNotReady : VirtioPciBarRegisterWindowLiveAccessorStatus.RegisterWriteFailed, operation, plan, VirtioPciRegisterReadStatus.Success, writeStatus, value: 0);
    }

    private static VirtioPciBarRegisterWindowLiveAccessorStatus ValidatePlan(VirtioPciBarRegisterWindowAccessPlan plan, VirtioPciRegisterAccessWidth expectedWidth, bool isRead)
    {
        if (!plan.Binding.IsBound || plan.WidthBytes == 0)
        {
            return VirtioPciBarRegisterWindowLiveAccessorStatus.MissingAccessPlan;
        }

        if (plan.Width != expectedWidth || plan.WidthBytes != GetWidthBytes(expectedWidth))
        {
            return isRead ? VirtioPciBarRegisterWindowLiveAccessorStatus.UnsupportedReadWidth : VirtioPciBarRegisterWindowLiveAccessorStatus.UnsupportedWriteWidth;
        }

        return VirtioPciBarRegisterWindowLiveAccessorStatus.Success;
    }

    private static uint GetWidthBytes(VirtioPciRegisterAccessWidth width)
    {
        return width == VirtioPciRegisterAccessWidth.Word ? 2U : width == VirtioPciRegisterAccessWidth.QWord ? 8U : 0U;
    }
}
