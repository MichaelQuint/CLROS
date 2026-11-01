using OS.Std.Binary;

namespace OS.Kernel.Diagnostics;
public readonly struct TssIstPlanResult
{
    private TssIstPlanResult(bool succeeded, TssIstPlanStatus status, byte istIndex, int actualBytes, int requiredBytes)
    {
        Succeeded = succeeded;
        Status = status;
        IstIndex = istIndex;
        ActualBytes = actualBytes;
        RequiredBytes = requiredBytes;
    }

    public bool Succeeded { get; }
    public TssIstPlanStatus Status { get; }
    public byte IstIndex { get; }
    public int ActualBytes { get; }
    public int RequiredBytes { get; }

    public static TssIstPlanResult Success(byte istIndex)
    {
        return new TssIstPlanResult(true, TssIstPlanStatus.Ok, istIndex, TssIstPlan.MinimumTssByteLength, TssIstPlan.MinimumTssByteLength);
    }

    public static TssIstPlanResult Fail(TssIstPlanStatus status, byte istIndex, int actualBytes, int requiredBytes = TssIstPlan.MinimumTssByteLength)
    {
        return new TssIstPlanResult(false, status, istIndex, actualBytes, requiredBytes);
    }
}
