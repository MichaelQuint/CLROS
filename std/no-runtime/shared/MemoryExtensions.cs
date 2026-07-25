using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace OS.Std;

public static class MemoryExtensions
{
    public const CompatibilityStatus Status = CompatibilityStatus.Partial;

    public static bool Contains(this ReadOnlySpan<byte> span, byte value)
    {
        return IndexOf(span, value) >= 0;
    }

    public static int Count(this ReadOnlySpan<byte> span, byte value)
    {
        int count = 0;
        for (int i = 0; i < span.Length; i++)
        {
            if (span[i] == value)
            {
                count++;
            }
        }

        return count;
    }

    public static bool EndsWith(this ReadOnlySpan<byte> span, ReadOnlySpan<byte> value)
    {
        if (value.Length > span.Length)
        {
            return false;
        }

        int start = span.Length - value.Length;
        for (int i = 0; i < value.Length; i++)
        {
            if (span[start + i] != value[i])
            {
                return false;
            }
        }

        return true;
    }

    public static int IndexOf(this ReadOnlySpan<byte> span, byte value)
    {
        for (int i = 0; i < span.Length; i++)
        {
            if (span[i] == value)
            {
                return i;
            }
        }

        return -1;
    }

    public static int IndexOf(this ReadOnlySpan<byte> span, ReadOnlySpan<byte> value)
    {
        if (value.Length == 0)
        {
            return 0;
        }

        if (value.Length > span.Length)
        {
            return -1;
        }

        int lastStart = span.Length - value.Length;
        byte first = value[0];

        for (int start = 0; start <= lastStart; start++)
        {
            if (span[start] != first)
            {
                continue;
            }

            bool matched = true;
            for (int offset = 1; offset < value.Length; offset++)
            {
                if (span[start + offset] != value[offset])
                {
                    matched = false;
                    break;
                }
            }

            if (matched)
            {
                return start;
            }
        }

        return -1;
    }

    public static int IndexOfAny(this ReadOnlySpan<byte> span, byte value0, byte value1)
    {
        for (int i = 0; i < span.Length; i++)
        {
            byte value = span[i];
            if (value == value0 || value == value1)
            {
                return i;
            }
        }

        return -1;
    }

    public static int IndexOfAny(this ReadOnlySpan<byte> span, byte value0, byte value1, byte value2)
    {
        for (int i = 0; i < span.Length; i++)
        {
            byte value = span[i];
            if (value == value0 || value == value1 || value == value2)
            {
                return i;
            }
        }

        return -1;
    }

    public static int LastIndexOf(this ReadOnlySpan<byte> span, byte value)
    {
        for (int i = span.Length - 1; i >= 0; i--)
        {
            if (span[i] == value)
            {
                return i;
            }
        }

        return -1;
    }

    public static int LastIndexOf(this ReadOnlySpan<byte> span, ReadOnlySpan<byte> value)
    {
        if (value.Length == 0)
        {
            return span.Length;
        }

        if (value.Length > span.Length)
        {
            return -1;
        }

        int lastStart = span.Length - value.Length;
        byte first = value[0];

        for (int start = lastStart; start >= 0; start--)
        {
            if (span[start] != first)
            {
                continue;
            }

            bool matched = true;
            for (int offset = 1; offset < value.Length; offset++)
            {
                if (span[start + offset] != value[offset])
                {
                    matched = false;
                    break;
                }
            }

            if (matched)
            {
                return start;
            }
        }

        return -1;
    }

    public static void CopyTo(this ReadOnlySpan<byte> source, Span<byte> destination)
    {
        if (source.Length > destination.Length)
        {
            throw new ArgumentException("Destination is too short.", nameof(destination));
        }

        if (source.Length == 0)
        {
            return;
        }

        ref byte sourceReference = ref MemoryMarshal.GetReference(source);
        ref byte destinationReference = ref MemoryMarshal.GetReference(destination);
        nint destinationOffset = Unsafe.ByteOffset(ref sourceReference, ref destinationReference);

        if (destinationOffset > 0 && destinationOffset < source.Length)
        {
            for (int i = source.Length - 1; i >= 0; i--)
            {
                destination[i] = source[i];
            }

            return;
        }

        for (int i = 0; i < source.Length; i++)
        {
            destination[i] = source[i];
        }
    }

    public static bool TryCopyTo(this ReadOnlySpan<byte> source, Span<byte> destination)
    {
        if (source.Length > destination.Length)
        {
            return false;
        }

        if (source.Length == 0)
        {
            return true;
        }

        ref byte sourceReference = ref MemoryMarshal.GetReference(source);
        ref byte destinationReference = ref MemoryMarshal.GetReference(destination);
        nint destinationOffset = Unsafe.ByteOffset(ref sourceReference, ref destinationReference);

        if (destinationOffset > 0 && destinationOffset < source.Length)
        {
            for (int i = source.Length - 1; i >= 0; i--)
            {
                destination[i] = source[i];
            }

            return true;
        }

        for (int i = 0; i < source.Length; i++)
        {
            destination[i] = source[i];
        }

        return true;
    }

    public static bool SequenceEqual(this ReadOnlySpan<byte> span, ReadOnlySpan<byte> other)
    {
        if (span.Length != other.Length)
        {
            return false;
        }

        for (int i = 0; i < span.Length; i++)
        {
            if (span[i] != other[i])
            {
                return false;
            }
        }

        return true;
    }

    public static bool StartsWith(this ReadOnlySpan<byte> span, ReadOnlySpan<byte> value)
    {
        if (value.Length > span.Length)
        {
            return false;
        }

        for (int i = 0; i < value.Length; i++)
        {
            if (span[i] != value[i])
            {
                return false;
            }
        }

        return true;
    }

    public static int IndexOf(this ReadOnlySpan<char> span, char value)
    {
        for (int i = 0; i < span.Length; i++)
        {
            if (span[i] == value)
            {
                return i;
            }
        }

        return -1;
    }

    public static int LastIndexOf(this ReadOnlySpan<char> span, char value)
    {
        for (int i = span.Length - 1; i >= 0; i--)
        {
            if (span[i] == value)
            {
                return i;
            }
        }

        return -1;
    }

    public static bool SequenceEqual(this ReadOnlySpan<char> span, ReadOnlySpan<char> other)
    {
        if (span.Length != other.Length)
        {
            return false;
        }

        for (int i = 0; i < span.Length; i++)
        {
            if (span[i] != other[i])
            {
                return false;
            }
        }

        return true;
    }

    public static bool StartsWith(this ReadOnlySpan<char> span, ReadOnlySpan<char> value)
    {
        if (value.Length > span.Length)
        {
            return false;
        }

        for (int i = 0; i < value.Length; i++)
        {
            if (span[i] != value[i])
            {
                return false;
            }
        }

        return true;
    }

    public static bool EndsWith(this ReadOnlySpan<char> span, ReadOnlySpan<char> value)
    {
        if (value.Length > span.Length)
        {
            return false;
        }

        int start = span.Length - value.Length;
        for (int i = 0; i < value.Length; i++)
        {
            if (span[start + i] != value[i])
            {
                return false;
            }
        }

        return true;
    }

    public static void CopyTo(this ReadOnlySpan<char> source, Span<char> destination)
    {
        if (source.Length > destination.Length)
        {
            throw new ArgumentException("Destination is too short.", nameof(destination));
        }

        if (source.Length == 0)
        {
            return;
        }

        ref char sourceReference = ref MemoryMarshal.GetReference(source);
        ref char destinationReference = ref MemoryMarshal.GetReference(destination);
        nint destinationOffset = Unsafe.ByteOffset(ref sourceReference, ref destinationReference);
        nint sourceByteLength = (nint)source.Length * sizeof(char);

        if (destinationOffset > 0 && destinationOffset < sourceByteLength)
        {
            for (int i = source.Length - 1; i >= 0; i--)
            {
                destination[i] = source[i];
            }

            return;
        }

        for (int i = 0; i < source.Length; i++)
        {
            destination[i] = source[i];
        }
    }

    public static bool TryCopyTo(this ReadOnlySpan<char> source, Span<char> destination)
    {
        if (source.Length > destination.Length)
        {
            return false;
        }

        if (source.Length == 0)
        {
            return true;
        }

        ref char sourceReference = ref MemoryMarshal.GetReference(source);
        ref char destinationReference = ref MemoryMarshal.GetReference(destination);
        nint destinationOffset = Unsafe.ByteOffset(ref sourceReference, ref destinationReference);
        nint sourceByteLength = (nint)source.Length * sizeof(char);

        if (destinationOffset > 0 && destinationOffset < sourceByteLength)
        {
            for (int i = source.Length - 1; i >= 0; i--)
            {
                destination[i] = source[i];
            }

            return true;
        }

        for (int i = 0; i < source.Length; i++)
        {
            destination[i] = source[i];
        }

        return true;
    }
}
