using OS.Std.Binary;

namespace OS.Drivers;

public readonly struct VirtioNetQueueMmioSetupRegisterWindowPlanResult
{
    private VirtioNetQueueMmioSetupRegisterWindowPlanResult(bool isInitialized, VirtioNetQueueMmioSetupRegisterWindowPlanStatus status, VirtioNetQueueMmioSetupPlanStatus setupPlanStatus, VirtioPciBarRegisterWindowPlanStatus registerWindowStatus, VirtioNetQueueMmioSetupCommandKind commandKind, int commandIndex, int accessPlansWritten, int requiredAccessPlanCount, int availableAccessPlanCount, ushort queueIndex, uint relativeOffset, uint widthBytes)
    {
        IsInitialized = isInitialized;
        Status = status;
        SetupPlanStatus = setupPlanStatus;
        RegisterWindowStatus = registerWindowStatus;
        CommandKind = commandKind;
        CommandIndex = commandIndex;
        AccessPlansWritten = accessPlansWritten;
        RequiredAccessPlanCount = requiredAccessPlanCount;
        AvailableAccessPlanCount = availableAccessPlanCount;
        QueueIndex = queueIndex;
        RelativeOffset = relativeOffset;
        WidthBytes = widthBytes;
    }

    public VirtioNetQueueMmioSetupRegisterWindowPlanStatus Status
    {
        get;
    }
    public VirtioNetQueueMmioSetupPlanStatus SetupPlanStatus
    {
        get;
    }
    public VirtioPciBarRegisterWindowPlanStatus RegisterWindowStatus
    {
        get;
    }
    public VirtioNetQueueMmioSetupCommandKind CommandKind
    {
        get;
    }
    public int CommandIndex
    {
        get;
    }
    public int AccessPlansWritten
    {
        get;
    }
    public int RequiredAccessPlanCount
    {
        get;
    }
    public int AvailableAccessPlanCount
    {
        get;
    }
    public ushort QueueIndex
    {
        get;
    }
    public uint RelativeOffset
    {
        get;
    }
    public uint WidthBytes
    {
        get;
    }
    public bool IsInitialized
    {
        get;
    }
    public bool Succeeded => IsInitialized && Status == VirtioNetQueueMmioSetupRegisterWindowPlanStatus.Success;

    internal static VirtioNetQueueMmioSetupRegisterWindowPlanResult Success(VirtioNetQueueMmioSetupPlanResult setupPlan, int accessPlansWritten, int availableAccessPlanCount)
    {
        return new VirtioNetQueueMmioSetupRegisterWindowPlanResult(isInitialized: true, VirtioNetQueueMmioSetupRegisterWindowPlanStatus.Success, setupPlan.Status, VirtioPciBarRegisterWindowPlanStatus.Success, VirtioNetQueueMmioSetupCommandKind.WriteQueueEnable, commandIndex: accessPlansWritten - 1, accessPlansWritten, setupPlan.CommandCount, availableAccessPlanCount, queueIndex: 0, relativeOffset: 0, widthBytes: 0);
    }

    internal static VirtioNetQueueMmioSetupRegisterWindowPlanResult Fail(VirtioNetQueueMmioSetupRegisterWindowPlanStatus status, VirtioNetQueueMmioSetupPlanStatus setupPlanStatus, VirtioPciBarRegisterWindowPlanStatus registerWindowStatus, VirtioNetQueueMmioSetupCommand command, int commandIndex, int accessPlansWritten, int requiredAccessPlanCount, int availableAccessPlanCount)
    {
        return new VirtioNetQueueMmioSetupRegisterWindowPlanResult(isInitialized: true, status, setupPlanStatus, registerWindowStatus, command.Kind, commandIndex, accessPlansWritten, requiredAccessPlanCount, availableAccessPlanCount, command.QueueIndex, command.RelativeOffset, command.WidthBytes);
    }
}
