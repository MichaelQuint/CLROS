using OS.Drivers;

namespace OS.Storage;

public readonly struct VirtioBlockQueueMmioSetupRegisterWindowPlanResult
{
    private VirtioBlockQueueMmioSetupRegisterWindowPlanResult(
        bool isInitialized,
        VirtioBlockQueueMmioSetupRegisterWindowPlanStatus status,
        VirtioBlockQueueMmioSetupPlanStatus setupPlanStatus,
        VirtioPciBarRegisterWindowPlanStatus registerWindowStatus,
        VirtioBlockQueueMmioSetupCommandKind commandKind,
        int commandIndex,
        int accessPlansWritten,
        int requiredAccessPlanCount,
        int availableAccessPlanCount,
        ushort queueIndex,
        uint relativeOffset,
        uint widthBytes)
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

    public bool IsInitialized { get; }

    public VirtioBlockQueueMmioSetupRegisterWindowPlanStatus Status { get; }

    public VirtioBlockQueueMmioSetupPlanStatus SetupPlanStatus { get; }

    public VirtioPciBarRegisterWindowPlanStatus RegisterWindowStatus { get; }

    public VirtioBlockQueueMmioSetupCommandKind CommandKind { get; }

    public int CommandIndex { get; }

    public int AccessPlansWritten { get; }

    public int RequiredAccessPlanCount { get; }

    public int AvailableAccessPlanCount { get; }

    public ushort QueueIndex { get; }

    public uint RelativeOffset { get; }

    public uint WidthBytes { get; }

    public bool Succeeded =>
        IsInitialized &&
        Status == VirtioBlockQueueMmioSetupRegisterWindowPlanStatus.Success;

    internal static VirtioBlockQueueMmioSetupRegisterWindowPlanResult Success(
        VirtioBlockQueueMmioSetupPlanResult setupPlan,
        VirtioBlockQueueMmioSetupCommand command,
        int accessPlansWritten,
        int availableAccessPlanCount)
    {
        return new VirtioBlockQueueMmioSetupRegisterWindowPlanResult(
            isInitialized: true,
            VirtioBlockQueueMmioSetupRegisterWindowPlanStatus.Success,
            setupPlan.Status,
            VirtioPciBarRegisterWindowPlanStatus.Success,
            command.Kind,
            commandIndex: accessPlansWritten - 1,
            accessPlansWritten,
            setupPlan.CommandCount,
            availableAccessPlanCount,
            command.QueueIndex,
            command.RelativeOffset,
            command.WidthBytes);
    }

    internal static VirtioBlockQueueMmioSetupRegisterWindowPlanResult Fail(
        VirtioBlockQueueMmioSetupRegisterWindowPlanStatus status,
        VirtioBlockQueueMmioSetupPlanStatus setupPlanStatus,
        VirtioPciBarRegisterWindowPlanStatus registerWindowStatus,
        VirtioBlockQueueMmioSetupCommand command,
        int commandIndex,
        int accessPlansWritten,
        int requiredAccessPlanCount,
        int availableAccessPlanCount)
    {
        return new VirtioBlockQueueMmioSetupRegisterWindowPlanResult(
            isInitialized: true,
            status,
            setupPlanStatus,
            registerWindowStatus,
            command.Kind,
            commandIndex,
            accessPlansWritten,
            requiredAccessPlanCount,
            availableAccessPlanCount,
            command.QueueIndex,
            command.RelativeOffset,
            command.WidthBytes);
    }
}
