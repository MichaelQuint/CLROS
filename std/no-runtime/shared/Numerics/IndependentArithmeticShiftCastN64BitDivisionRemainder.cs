using System.Runtime.CompilerServices;

namespace OS.Std.Numerics;
/// <summary>
/// Fixed integer operator conformance only; no canonical System API or runtime
/// completeness claim. Values are raw 64-bit patterns; signed results extend
/// their sign. 32-bit unsigned arithmetic truncates inputs and zero extends.
/// </summary>

public static partial class IndependentArithmeticShiftCastN64BitDivisionRemainder
{
    public const int VectorCount = 57;
    public const ulong ExpectedFingerprint = 0x45FDFA24DD72EA28UL;
    public static bool TryGetVector(int index, out ArithmeticConformanceVector vector)
    {
        switch (index)
        {
            case 0:
                vector = new(ArithmeticOperation.Add32, 0xFFFFFFFFUL, 0x1UL, 0x0UL, ArithmeticEvaluationStatus.Success);
                return true;
            case 1:
                vector = new(ArithmeticOperation.Add32, 0x7FFFFFFFUL, 0x1UL, 0x80000000UL, ArithmeticEvaluationStatus.Success);
                return true;
            case 2:
                vector = new(ArithmeticOperation.Subtract32, 0x0UL, 0x1UL, 0xFFFFFFFFUL, ArithmeticEvaluationStatus.Success);
                return true;
            case 3:
                vector = new(ArithmeticOperation.Multiply32, 0x10001UL, 0x10001UL, 0x20001UL, ArithmeticEvaluationStatus.Success);
                return true;
            case 4:
                vector = new(ArithmeticOperation.Add64, 0xFFFFFFFFFFFFFFFFUL, 0x1UL, 0x0UL, ArithmeticEvaluationStatus.Success);
                return true;
            case 5:
                vector = new(ArithmeticOperation.Add64, 0x7FFFFFFFFFFFFFFFUL, 0x1UL, 0x8000000000000000UL, ArithmeticEvaluationStatus.Success);
                return true;
            case 6:
                vector = new(ArithmeticOperation.Subtract64, 0x0UL, 0x1UL, 0xFFFFFFFFFFFFFFFFUL, ArithmeticEvaluationStatus.Success);
                return true;
            case 7:
                vector = new(ArithmeticOperation.Multiply64, 0x100000001UL, 0x100000001UL, 0x200000001UL, ArithmeticEvaluationStatus.Success);
                return true;
            case 8:
                vector = new(ArithmeticOperation.ShiftLeft32, 0x1UL, 0x0UL, 0x1UL, ArithmeticEvaluationStatus.Success);
                return true;
            case 9:
                vector = new(ArithmeticOperation.ShiftLeft32, 0x1UL, 0x1FUL, 0x80000000UL, ArithmeticEvaluationStatus.Success);
                return true;
            case 10:
                vector = new(ArithmeticOperation.ShiftLeft32, 0x1UL, 0x20UL, 0x1UL, ArithmeticEvaluationStatus.Success);
                return true;
            case 11:
                vector = new(ArithmeticOperation.ShiftLeft32, 0x1UL, 0xFFFFFFFFFFFFFFFFUL, 0x80000000UL, ArithmeticEvaluationStatus.Success);
                return true;
            case 12:
                vector = new(ArithmeticOperation.ShiftRightSigned32, 0x80000000UL, 0x1UL, 0xFFFFFFFFC0000000UL, ArithmeticEvaluationStatus.Success);
                return true;
            case 13:
                vector = new(ArithmeticOperation.ShiftRightSigned32, 0x80000000UL, 0x20UL, 0xFFFFFFFF80000000UL, ArithmeticEvaluationStatus.Success);
                return true;
            case 14:
                vector = new(ArithmeticOperation.ShiftRightUnsigned32, 0x80000000UL, 0x1UL, 0x40000000UL, ArithmeticEvaluationStatus.Success);
                return true;
            case 15:
                vector = new(ArithmeticOperation.ShiftRightUnsigned32, 0xFFFFFFFFUL, 0x21UL, 0x7FFFFFFFUL, ArithmeticEvaluationStatus.Success);
                return true;
            case 16:
                vector = new(ArithmeticOperation.ShiftLeft64, 0x1UL, 0x3FUL, 0x8000000000000000UL, ArithmeticEvaluationStatus.Success);
                return true;
            case 17:
                vector = new(ArithmeticOperation.ShiftLeft64, 0x1UL, 0x40UL, 0x1UL, ArithmeticEvaluationStatus.Success);
                return true;
            case 18:
                vector = new(ArithmeticOperation.ShiftLeft64, 0x1UL, 0xFFFFFFFFFFFFFFFFUL, 0x8000000000000000UL, ArithmeticEvaluationStatus.Success);
                return true;
            case 19:
                vector = new(ArithmeticOperation.ShiftRightSigned64, 0x8000000000000000UL, 0x1UL, 0xC000000000000000UL, ArithmeticEvaluationStatus.Success);
                return true;
            case 20:
                vector = new(ArithmeticOperation.ShiftRightSigned64, 0x8000000000000000UL, 0x40UL, 0x8000000000000000UL, ArithmeticEvaluationStatus.Success);
                return true;
            case 21:
                vector = new(ArithmeticOperation.ShiftRightUnsigned64, 0x8000000000000000UL, 0x3FUL, 0x1UL, ArithmeticEvaluationStatus.Success);
                return true;
            case 22:
                vector = new(ArithmeticOperation.ShiftRightUnsigned64, 0xFFFFFFFFFFFFFFFFUL, 0x41UL, 0x7FFFFFFFFFFFFFFFUL, ArithmeticEvaluationStatus.Success);
                return true;
            case 23:
                vector = new(ArithmeticOperation.CastI8, 0x80UL, 0x0UL, 0xFFFFFFFFFFFFFF80UL, ArithmeticEvaluationStatus.Success);
                return true;
            case 24:
                vector = new(ArithmeticOperation.CastU8, 0x1FFUL, 0x0UL, 0xFFUL, ArithmeticEvaluationStatus.Success);
                return true;
            case 25:
                vector = new(ArithmeticOperation.CastI16, 0x8000UL, 0x0UL, 0xFFFFFFFFFFFF8000UL, ArithmeticEvaluationStatus.Success);
                return true;
            case 26:
                vector = new(ArithmeticOperation.CastU16, 0x1FFFFUL, 0x0UL, 0xFFFFUL, ArithmeticEvaluationStatus.Success);
                return true;
            case 27:
                vector = new(ArithmeticOperation.CastI32, 0x80000000UL, 0x0UL, 0xFFFFFFFF80000000UL, ArithmeticEvaluationStatus.Success);
                return true;
            case 28:
                vector = new(ArithmeticOperation.CastU32, 0x1FFFFFFFFUL, 0x0UL, 0xFFFFFFFFUL, ArithmeticEvaluationStatus.Success);
                return true;
            case 29:
                vector = new(ArithmeticOperation.EnumI8, 0x81UL, 0x0UL, 0xFFFFFFFFFFFFFF81UL, ArithmeticEvaluationStatus.Success);
                return true;
            case 30:
                vector = new(ArithmeticOperation.EnumU8, 0x1FEUL, 0x0UL, 0xFEUL, ArithmeticEvaluationStatus.Success);
                return true;
            case 31:
                vector = new(ArithmeticOperation.EnumI16, 0x8001UL, 0x0UL, 0xFFFFFFFFFFFF8001UL, ArithmeticEvaluationStatus.Success);
                return true;
            case 32:
                vector = new(ArithmeticOperation.EnumU16, 0x1FFFEUL, 0x0UL, 0xFFFEUL, ArithmeticEvaluationStatus.Success);
                return true;
            case 33:
                vector = new(ArithmeticOperation.EnumI32, 0x80000001UL, 0x0UL, 0xFFFFFFFF80000001UL, ArithmeticEvaluationStatus.Success);
                return true;
            case 34:
                vector = new(ArithmeticOperation.EnumU32, 0x1FFFFFFFEUL, 0x0UL, 0xFFFFFFFEUL, ArithmeticEvaluationStatus.Success);
                return true;
            case 35:
                vector = new(ArithmeticOperation.EnumI64, 0x8000000000000001UL, 0x0UL, 0x8000000000000001UL, ArithmeticEvaluationStatus.Success);
                return true;
            case 36:
                vector = new(ArithmeticOperation.EnumU64, 0xFFFFFFFFFFFFFFFFUL, 0x0UL, 0xFFFFFFFFFFFFFFFFUL, ArithmeticEvaluationStatus.Success);
                return true;
            case 37:
                vector = new(ArithmeticOperation.DivideI64, 0xFFFFFFFFFFFFFFE3UL, 0x6UL, 0xFFFFFFFFFFFFFFFCUL, ArithmeticEvaluationStatus.Success);
                return true;
            case 38:
                vector = new(ArithmeticOperation.RemainderI64, 0xFFFFFFFFFFFFFFE3UL, 0x6UL, 0xFFFFFFFFFFFFFFFBUL, ArithmeticEvaluationStatus.Success);
                return true;
            case 39:
                vector = new(ArithmeticOperation.DivideI64, 0x1DUL, 0xFFFFFFFFFFFFFFFAUL, 0xFFFFFFFFFFFFFFFCUL, ArithmeticEvaluationStatus.Success);
                return true;
            case 40:
                vector = new(ArithmeticOperation.RemainderI64, 0x1DUL, 0xFFFFFFFFFFFFFFFAUL, 0x5UL, ArithmeticEvaluationStatus.Success);
                return true;
            case 41:
                vector = new(ArithmeticOperation.DivideI64, 0xFFFFFFFFFFFFFFE3UL, 0xFFFFFFFFFFFFFFFAUL, 0x4UL, ArithmeticEvaluationStatus.Success);
                return true;
            case 42:
                vector = new(ArithmeticOperation.RemainderI64, 0xFFFFFFFFFFFFFFE3UL, 0xFFFFFFFFFFFFFFFAUL, 0xFFFFFFFFFFFFFFFBUL, ArithmeticEvaluationStatus.Success);
                return true;
            case 43:
                vector = new(ArithmeticOperation.DivideI64, 0x8000000000000000UL, 0x3UL, 0xD555555555555556UL, ArithmeticEvaluationStatus.Success);
                return true;
            case 44:
                vector = new(ArithmeticOperation.RemainderI64, 0x8000000000000000UL, 0x3UL, 0xFFFFFFFFFFFFFFFEUL, ArithmeticEvaluationStatus.Success);
                return true;
            case 45:
                vector = new(ArithmeticOperation.DivideU64, 0xFFFFFFFFFFFFFFFFUL, 0x100000001UL, 0xFFFFFFFFUL, ArithmeticEvaluationStatus.Success);
                return true;
            case 46:
                vector = new(ArithmeticOperation.RemainderU64, 0xFFFFFFFFFFFFFFFFUL, 0x100000001UL, 0x0UL, ArithmeticEvaluationStatus.Success);
                return true;
            case 47:
                vector = new(ArithmeticOperation.DivideU64, 0xFFFFFFFFFFFFFFFFUL, 0x7UL, 0x2492492492492492UL, ArithmeticEvaluationStatus.Success);
                return true;
            case 48:
                vector = new(ArithmeticOperation.RemainderU64, 0xFFFFFFFFFFFFFFFFUL, 0x7UL, 0x1UL, ArithmeticEvaluationStatus.Success);
                return true;
            case 49:
                vector = new(ArithmeticOperation.DivideI64, 0x0UL, 0x3UL, 0x0UL, ArithmeticEvaluationStatus.Success);
                return true;
            case 50:
                vector = new(ArithmeticOperation.RemainderU64, 0x0UL, 0x3UL, 0x0UL, ArithmeticEvaluationStatus.Success);
                return true;
            case 51:
                vector = new(ArithmeticOperation.DivideI64, 0x9UL, 0x0UL, 0x0UL, ArithmeticEvaluationStatus.DivideByZero);
                return true;
            case 52:
                vector = new(ArithmeticOperation.RemainderI64, 0x9UL, 0x0UL, 0x0UL, ArithmeticEvaluationStatus.DivideByZero);
                return true;
            case 53:
                vector = new(ArithmeticOperation.DivideU64, 0x9UL, 0x0UL, 0x0UL, ArithmeticEvaluationStatus.DivideByZero);
                return true;
            case 54:
                vector = new(ArithmeticOperation.RemainderU64, 0x9UL, 0x0UL, 0x0UL, ArithmeticEvaluationStatus.DivideByZero);
                return true;
            case 55:
                vector = new(ArithmeticOperation.DivideI64, 0x8000000000000000UL, 0xFFFFFFFFFFFFFFFFUL, 0x0UL, ArithmeticEvaluationStatus.SignedOverflow);
                return true;
            case 56:
                vector = new(ArithmeticOperation.RemainderI64, 0x8000000000000000UL, 0xFFFFFFFFFFFFFFFFUL, 0x0UL, ArithmeticEvaluationStatus.SignedOverflow);
                return true;
            default:
                vector = default;
                return false;
        }
    }

    // Keep operands runtime supplied in both host and future EFI conformance.
    [MethodImpl(MethodImplOptions.NoInlining)]
    public static ArithmeticEvaluation Evaluate(ArithmeticOperation operation, ulong left, ulong right)
    {
        unchecked
        {
            // This no-exception API rejects both potentially trapping signed
            // divide/rem pairs before issuing either operation. It does not test
            // the runtime's exception machinery or define a BCL remainder API.
            if (operation == ArithmeticOperation.DivideI64 || operation == ArithmeticOperation.RemainderI64 || operation == ArithmeticOperation.DivideU64 || operation == ArithmeticOperation.RemainderU64)
            {
                if (right == 0)
                    return new(ArithmeticEvaluationStatus.DivideByZero, 0);
                if ((operation == ArithmeticOperation.DivideI64 || operation == ArithmeticOperation.RemainderI64) && left == 0x8000000000000000UL && right == 0xFFFFFFFFFFFFFFFFUL)
                    return new(ArithmeticEvaluationStatus.SignedOverflow, 0);
            }

            ulong value;
            switch (operation)
            {
                case ArithmeticOperation.Add32:
                    value = (uint)left + (uint)right;
                    break;
                case ArithmeticOperation.Subtract32:
                    value = (uint)left - (uint)right;
                    break;
                case ArithmeticOperation.Multiply32:
                    value = (uint)left * (uint)right;
                    break;
                case ArithmeticOperation.Add64:
                    value = left + right;
                    break;
                case ArithmeticOperation.Subtract64:
                    value = left - right;
                    break;
                case ArithmeticOperation.Multiply64:
                    value = left * right;
                    break;
                case ArithmeticOperation.ShiftLeft32:
                    value = (uint)left << (int)right;
                    break;
                case ArithmeticOperation.ShiftRightSigned32:
                    value = (ulong)((int)left >> (int)right);
                    break;
                case ArithmeticOperation.ShiftRightUnsigned32:
                    value = (uint)left >> (int)right;
                    break;
                case ArithmeticOperation.ShiftLeft64:
                    value = left << (int)right;
                    break;
                case ArithmeticOperation.ShiftRightSigned64:
                    value = (ulong)((long)left >> (int)right);
                    break;
                case ArithmeticOperation.ShiftRightUnsigned64:
                    value = left >> (int)right;
                    break;
                case ArithmeticOperation.CastI8:
                    value = (ulong)(sbyte)left;
                    break;
                case ArithmeticOperation.CastU8:
                    value = (byte)left;
                    break;
                case ArithmeticOperation.CastI16:
                    value = (ulong)(short)left;
                    break;
                case ArithmeticOperation.CastU16:
                    value = (ushort)left;
                    break;
                case ArithmeticOperation.CastI32:
                    value = (ulong)(int)left;
                    break;
                case ArithmeticOperation.CastU32:
                    value = (uint)left;
                    break;
                case ArithmeticOperation.EnumI8:
                    value = (ulong)(sbyte)(Signed8)left;
                    break;
                case ArithmeticOperation.EnumU8:
                    value = (byte)(Unsigned8)left;
                    break;
                case ArithmeticOperation.EnumI16:
                    value = (ulong)(short)(Signed16)left;
                    break;
                case ArithmeticOperation.EnumU16:
                    value = (ushort)(Unsigned16)left;
                    break;
                case ArithmeticOperation.EnumI32:
                    value = (ulong)(int)(Signed32)left;
                    break;
                case ArithmeticOperation.EnumU32:
                    value = (uint)(Unsigned32)left;
                    break;
                case ArithmeticOperation.EnumI64:
                    value = (ulong)(long)(Signed64)left;
                    break;
                case ArithmeticOperation.EnumU64:
                    value = (ulong)(Unsigned64)left;
                    break;
                case ArithmeticOperation.DivideI64:
                    value = (ulong)((long)left / (long)right);
                    break;
                case ArithmeticOperation.RemainderI64:
                    value = (ulong)((long)left % (long)right);
                    break;
                case ArithmeticOperation.DivideU64:
                    value = left / right;
                    break;
                case ArithmeticOperation.RemainderU64:
                    value = left % right;
                    break;
                default:
                    return new(ArithmeticEvaluationStatus.UnknownOperation, 0);
            }

            return new(ArithmeticEvaluationStatus.Success, value);
        }
    }

    public static ArithmeticConformanceResult RunCatalog()
    {
        ulong fingerprint = 14695981039346656037UL;
        for (int index = 0; index < VectorCount; index++)
        {
            if (!TryGetVector(index, out ArithmeticConformanceVector vector))
                return new(ArithmeticConformanceStatus.MissingVector, index, index, fingerprint);
            ArithmeticEvaluation actual = Evaluate(vector.Operation, vector.Left, vector.Right);
            if (actual.Status != vector.ExpectedStatus)
                return new(ArithmeticConformanceStatus.UnexpectedStatus, index, index, fingerprint);
            if (actual.Value != vector.Expected)
                return new(ArithmeticConformanceStatus.UnexpectedValue, index, index, fingerprint);
            fingerprint = HashWord(fingerprint, (ulong)index);
            fingerprint = HashWord(fingerprint, (ulong)vector.Operation);
            fingerprint = HashWord(fingerprint, vector.Left);
            fingerprint = HashWord(fingerprint, vector.Right);
            fingerprint = HashWord(fingerprint, (ulong)actual.Status);
            fingerprint = HashWord(fingerprint, actual.Value);
        }

        return new(fingerprint == ExpectedFingerprint ? ArithmeticConformanceStatus.Passed : ArithmeticConformanceStatus.FingerprintMismatch, VectorCount, -1, fingerprint);
    }

    private static ulong HashWord(ulong hash, ulong value)
    {
        for (int shift = 0; shift < 64; shift += 8)
            hash = unchecked((hash ^ (byte)(value >> shift)) * 1099511628211UL);
        return hash;
    }
}
