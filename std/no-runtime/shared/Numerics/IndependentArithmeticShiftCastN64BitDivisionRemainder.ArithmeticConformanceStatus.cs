using System.Runtime.CompilerServices;

namespace OS.Std.Numerics;
public enum ArithmeticConformanceStatus : byte
{
    Uninitialized = 0,
    Passed = 1,
    MissingVector = 2,
    UnexpectedStatus = 3,
    UnexpectedValue = 4,
    FingerprintMismatch = 5
}
