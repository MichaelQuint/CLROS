using System.Runtime.CompilerServices;

namespace OS.Std.Numerics;
public readonly struct ArithmeticConformanceVector
{
    internal ArithmeticConformanceVector(ArithmeticOperation operation, ulong left, ulong right, ulong expected, ArithmeticEvaluationStatus expectedStatus)
    {
        Operation = operation;
        Left = left;
        Right = right;
        Expected = expected;
        ExpectedStatus = expectedStatus;
    }

    public ArithmeticOperation Operation { get; }
    public ulong Left { get; }
    public ulong Right { get; }
    public ulong Expected { get; }
    public ArithmeticEvaluationStatus ExpectedStatus { get; }
}
