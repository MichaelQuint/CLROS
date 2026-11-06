using System.Runtime.CompilerServices;

namespace OS.Std.Numerics;

public readonly struct ArithmeticEvaluation
{
    internal ArithmeticEvaluation(ArithmeticEvaluationStatus status, ulong value)
    {
        Status = status;
        Value = value;
    }

    public ArithmeticEvaluationStatus Status
    {
        get;
    }
    public ulong Value
    {
        get;
    }
    public bool Succeeded => Status == ArithmeticEvaluationStatus.Success;
}
