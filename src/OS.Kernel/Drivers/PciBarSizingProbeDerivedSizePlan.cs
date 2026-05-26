namespace OS.Drivers;

public enum PciBarSizingProbeDerivedSizeStatus
{
    Success = 0,
    AddressPlanFailed = 1,
    ExecutionFailed = 2,
    SizingFailed = 3
}

public readonly struct PciBarSizingProbeDerivedSizeResult
{
    private PciBarSizingProbeDerivedSizeResult(
        PciBarSizingProbeDerivedSizeStatus status,
        PciBarSizingProbeAddressPlanResult addressPlan,
        PciBarSizingProbeExecutionResult execution,
        PciBarSizingPlanResult sizing,
        byte failingBarIndex,
        PciBarSizeDescriptorStatus failingDescriptorStatus,
        bool isInitialized)
    {
        Status = status;
        AddressPlan = addressPlan;
        Execution = execution;
        Sizing = sizing;
        FailingBarIndex = failingBarIndex;
        FailingDescriptorStatus = failingDescriptorStatus;
        IsInitialized = isInitialized;
    }

    public PciBarSizingProbeDerivedSizeStatus Status { get; }

    public PciBarSizingProbeAddressPlanResult AddressPlan { get; }

    public PciBarSizingProbeAddressPlanStatus AddressPlanStatus => AddressPlan.Status;

    public PciBarSizingProbeExecutionResult Execution { get; }

    public PciBarSizingProbeExecutionStatus ExecutionStatus => Execution.Status;

    public PciBarSizingPlanResult Sizing { get; }

    public PciBarSizingPlanStatus SizingStatus => Sizing.Status;

    public PciBarSizeDescriptorSet Bars => Sizing.Bars;

    public byte FailingBarIndex { get; }

    public PciBarSizeDescriptorStatus FailingDescriptorStatus { get; }

    public bool IsInitialized { get; }

    public bool Succeeded => IsInitialized && Status == PciBarSizingProbeDerivedSizeStatus.Success;

    public bool TryCopySizesTo(Span<ulong> destination)
    {
        return Succeeded && Sizing.TryCopySizesTo(destination);
    }

    public static PciBarSizingProbeDerivedSizeResult Create(
        PciBarSizingProbeDerivedSizeStatus status,
        PciBarSizingProbeAddressPlanResult addressPlan,
        PciBarSizingProbeExecutionResult execution,
        PciBarSizingPlanResult sizing,
        byte failingBarIndex,
        PciBarSizeDescriptorStatus failingDescriptorStatus)
    {
        return new PciBarSizingProbeDerivedSizeResult(
            status,
            addressPlan,
            execution,
            sizing,
            failingBarIndex,
            failingDescriptorStatus,
            isInitialized: true);
    }
}

public static class PciBarSizingProbeDerivedSizePlanner
{
    public const bool RequiresDecodedBarDescriptors = true;
    public const bool RequiresAddressPlan = true;
    public const bool RequiresFakeProbeExecution = true;
    public const bool UsesCallerOwnedReadbackSnapshots = true;
    public const bool ProducesVirtioApertureValidatorInput = true;
    public const bool PerformsLiveConfigWrites = false;
    public const bool PerformsLiveConfigReads = false;
    public const bool RestoresHardwareBars = false;
    public const bool MapsEcam = false;
    public const bool MapsBars = false;
    public const bool PerformsMmio = false;
    public const bool ContainsVolatileAccess = false;
    public const bool AllocatesDmaMemory = false;
    public const bool PerformsDmaMapping = false;
    public const bool HandlesInterrupts = false;
    public const bool PerformsPacketIo = false;
    public const bool PerformsBlockIo = false;

    public static PciBarSizingProbeDerivedSizeResult Plan(
        PciEcamSegment segment,
        byte bus,
        byte device,
        byte function,
        PciBarDescriptorSet bars,
        IPciConfigDwordAccessor? accessor,
        Span<PciBarSizingProbeAddressCommand> commands,
        Span<uint> sizingReadbacks)
    {
        PciBarSizingProbeAddressPlanResult addressPlan =
            PciBarSizingProbeAddressPlanner.Plan(
                segment,
                bus,
                device,
                function,
                bars,
                commands);
        if (!addressPlan.Succeeded)
        {
            return PciBarSizingProbeDerivedSizeResult.Create(
                PciBarSizingProbeDerivedSizeStatus.AddressPlanFailed,
                addressPlan,
                execution: default,
                sizing: default,
                addressPlan.FailingBarIndex,
                failingDescriptorStatus: default);
        }

        PciBarSizingProbeExecutionResult execution =
            PciBarSizingProbeExecutor.Execute(
                addressPlan,
                commands,
                accessor,
                sizingReadbacks);
        if (!execution.Succeeded)
        {
            return PciBarSizingProbeDerivedSizeResult.Create(
                PciBarSizingProbeDerivedSizeStatus.ExecutionFailed,
                addressPlan,
                execution,
                sizing: default,
                execution.FailingBarIndex,
                failingDescriptorStatus: default);
        }

        PciBarSizingPlanResult sizing = PciBarSizingPlanner.Plan(bars, sizingReadbacks);
        if (!sizing.Succeeded)
        {
            return PciBarSizingProbeDerivedSizeResult.Create(
                PciBarSizingProbeDerivedSizeStatus.SizingFailed,
                addressPlan,
                execution,
                sizing,
                sizing.FailingBarIndex,
                sizing.FailingDescriptorStatus);
        }

        return PciBarSizingProbeDerivedSizeResult.Create(
            PciBarSizingProbeDerivedSizeStatus.Success,
            addressPlan,
            execution,
            sizing,
            PciBarDecoder.NoBarIndex,
            PciBarSizeDescriptorStatus.Success);
    }
}
