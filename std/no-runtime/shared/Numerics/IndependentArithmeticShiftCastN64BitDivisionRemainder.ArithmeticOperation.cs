using System.Runtime.CompilerServices;

namespace OS.Std.Numerics;
// Independent C# language vectors. Historical SharpOS supplies test categories,
// never executable code or expected values. See the compatibility ledger.
public enum ArithmeticOperation : byte
{
    Uninitialized = 0,
    Add32 = 1,
    Subtract32 = 2,
    Multiply32 = 3,
    Add64 = 4,
    Subtract64 = 5,
    Multiply64 = 6,
    ShiftLeft32 = 7,
    ShiftRightSigned32 = 8,
    ShiftRightUnsigned32 = 9,
    ShiftLeft64 = 10,
    ShiftRightSigned64 = 11,
    ShiftRightUnsigned64 = 12,
    CastI8 = 13,
    CastU8 = 14,
    CastI16 = 15,
    CastU16 = 16,
    CastI32 = 17,
    CastU32 = 18,
    EnumI8 = 19,
    EnumU8 = 20,
    EnumI16 = 21,
    EnumU16 = 22,
    EnumI32 = 23,
    EnumU32 = 24,
    EnumI64 = 25,
    EnumU64 = 26,
    DivideI64 = 27,
    RemainderI64 = 28,
    DivideU64 = 29,
    RemainderU64 = 30,
}
