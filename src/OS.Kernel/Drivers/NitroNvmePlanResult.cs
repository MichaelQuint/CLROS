using OS.Std.Binary;

namespace OS.Drivers;
public readonly struct NitroNvmePlanResult
{
    private NitroNvmePlanResult(bool isInitialized, NitroNvmePlanStatus status, byte failingBarIndex, NitroNvmePlan plan)
    {
        IsInitialized = isInitialized;
        Status = status;
        FailingBarIndex = failingBarIndex;
        Plan = plan;
    }

    public NitroNvmePlanStatus Status { get; }
    public byte FailingBarIndex { get; }
    public NitroNvmePlan Plan { get; }
    public bool IsInitialized { get; }
    public bool Succeeded => IsInitialized && Status == NitroNvmePlanStatus.Success;

    internal static NitroNvmePlanResult Success(NitroNvmePlan plan)
    {
        return new NitroNvmePlanResult(isInitialized: true, NitroNvmePlanStatus.Success, PciBarDecoder.NoBarIndex, plan);
    }

    internal static NitroNvmePlanResult Fail(NitroNvmePlanStatus status, byte failingBarIndex = PciBarDecoder.NoBarIndex)
    {
        return new NitroNvmePlanResult(isInitialized: true, status, failingBarIndex, default);
    }
}
