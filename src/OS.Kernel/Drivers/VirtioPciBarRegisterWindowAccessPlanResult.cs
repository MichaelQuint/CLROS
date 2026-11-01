using OS.Std.Binary;

namespace OS.Drivers;
public readonly struct VirtioPciBarRegisterWindowAccessPlanResult
{
    private VirtioPciBarRegisterWindowAccessPlanResult(VirtioPciBarRegisterWindowPlanStatus status, VirtioPciCapabilityType capabilityType, byte barIndex, uint relativeOffset, VirtioPciRegisterAccessWidth width, VirtioPciBarRegisterWindowAccessPlan plan, bool isInitialized)
    {
        Status = status;
        CapabilityType = capabilityType;
        BarIndex = barIndex;
        RelativeOffset = relativeOffset;
        Width = width;
        Plan = plan;
        IsInitialized = isInitialized;
    }

    public VirtioPciBarRegisterWindowPlanStatus Status { get; }
    public bool IsInitialized { get; }
    public VirtioPciCapabilityType CapabilityType { get; }
    public byte BarIndex { get; }
    public uint RelativeOffset { get; }
    public VirtioPciRegisterAccessWidth Width { get; }
    public VirtioPciBarRegisterWindowAccessPlan Plan { get; }
    public bool Succeeded => IsInitialized && Status == VirtioPciBarRegisterWindowPlanStatus.Success;

    internal static VirtioPciBarRegisterWindowAccessPlanResult Success(VirtioPciBarRegisterWindowAccessPlan plan)
    {
        return new VirtioPciBarRegisterWindowAccessPlanResult(VirtioPciBarRegisterWindowPlanStatus.Success, plan.CapabilityType, plan.BarIndex, plan.RelativeOffset, plan.Width, plan, isInitialized: true);
    }

    internal static VirtioPciBarRegisterWindowAccessPlanResult Fail(VirtioPciBarRegisterWindowPlanStatus status, VirtioPciBarRegisterWindowBinding binding, uint relativeOffset, VirtioPciRegisterAccessWidth width)
    {
        return new VirtioPciBarRegisterWindowAccessPlanResult(status, binding.CapabilityType, binding.BarIndex, relativeOffset, width, default, isInitialized: true);
    }
}
