using OS.Std.Binary;

namespace OS.Drivers;
public readonly struct VirtioPciBarRegisterWindowBindingResult
{
    private VirtioPciBarRegisterWindowBindingResult(VirtioPciBarRegisterWindowPlanStatus status, VirtioPciBindingPlanStatus bindingPlanStatus, VirtioPciBarApertureValidationStatus apertureValidationStatus, VirtioPciCapabilityType failingCapabilityType, byte failingBarIndex, VirtioPciBarRegisterWindowBindingSet bindings, bool isInitialized)
    {
        Status = status;
        BindingPlanStatus = bindingPlanStatus;
        ApertureValidationStatus = apertureValidationStatus;
        FailingCapabilityType = failingCapabilityType;
        FailingBarIndex = failingBarIndex;
        Bindings = bindings;
        IsInitialized = isInitialized;
    }

    public VirtioPciBarRegisterWindowPlanStatus Status { get; }
    public bool IsInitialized { get; }
    public VirtioPciBindingPlanStatus BindingPlanStatus { get; }
    public VirtioPciBarApertureValidationStatus ApertureValidationStatus { get; }
    public VirtioPciCapabilityType FailingCapabilityType { get; }
    public byte FailingBarIndex { get; }
    public VirtioPciBarRegisterWindowBindingSet Bindings { get; }
    public bool Succeeded => IsInitialized && Status == VirtioPciBarRegisterWindowPlanStatus.Success;

    internal static VirtioPciBarRegisterWindowBindingResult Success(VirtioPciBindingPlanStatus bindingPlanStatus, VirtioPciBarApertureValidationStatus apertureValidationStatus, VirtioPciBarRegisterWindowBindingSet bindings)
    {
        return new VirtioPciBarRegisterWindowBindingResult(VirtioPciBarRegisterWindowPlanStatus.Success, bindingPlanStatus, apertureValidationStatus, default, PciBarDecoder.NoBarIndex, bindings, isInitialized: true);
    }

    internal static VirtioPciBarRegisterWindowBindingResult Fail(VirtioPciBarRegisterWindowPlanStatus status, VirtioPciBindingPlanStatus bindingPlanStatus, VirtioPciBarApertureValidationStatus apertureValidationStatus, VirtioPciCapabilityType failingCapabilityType, byte failingBarIndex)
    {
        return new VirtioPciBarRegisterWindowBindingResult(status, bindingPlanStatus, apertureValidationStatus, failingCapabilityType, failingBarIndex, default, isInitialized: true);
    }
}
