using System.Runtime.CompilerServices;

namespace OS.Std.Numerics;
public readonly struct ArithmeticConformanceResult
{
    internal ArithmeticConformanceResult(ArithmeticConformanceStatus status, int completedCount, int failedIndex, ulong fingerprint)
    {
        Status = status;
        CompletedCount = completedCount;
        FailedIndex = failedIndex;
        Fingerprint = fingerprint;
    }

    public ArithmeticConformanceStatus Status { get; }
    public int CompletedCount { get; }
    public int FailedIndex { get; }
    public ulong Fingerprint { get; }
    public bool Succeeded => Status == ArithmeticConformanceStatus.Passed && CompletedCount == IndependentArithmeticShiftCastN64BitDivisionRemainder.VectorCount && FailedIndex == -1 && Fingerprint == IndependentArithmeticShiftCastN64BitDivisionRemainder.ExpectedFingerprint;
}
