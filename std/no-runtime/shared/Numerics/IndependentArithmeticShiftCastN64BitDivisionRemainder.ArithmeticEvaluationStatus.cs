using System.Runtime.CompilerServices;

namespace OS.Std.Numerics;
public enum ArithmeticEvaluationStatus : byte
{
    Uninitialized = 0,
    Success = 1,
    UnknownOperation = 2,
    DivideByZero = 3,
    SignedOverflow = 4
}
