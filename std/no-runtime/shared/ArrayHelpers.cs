namespace OS.Std;

public static class ArrayHelpers
{
    public const CompatibilityStatus Status = CompatibilityStatus.Partial;
    public static void Clear<T>(Span<T> span)
        where T : unmanaged
    {
        for (int i = 0; i < span.Length; i++)
        {
            span[i] = default;
        }
    }

    public static void Fill<T>(Span<T> span, T value)
        where T : unmanaged
    {
        for (int i = 0; i < span.Length; i++)
        {
            span[i] = value;
        }
    }

    public static int IndexOf<T>(ReadOnlySpan<T> span, T value)
        where T : unmanaged, IEquatable<T>
    {
        for (int i = 0; i < span.Length; i++)
        {
            if (span[i].Equals(value))
            {
                return i;
            }
        }

        return -1;
    }

    public static int LastIndexOf<T>(ReadOnlySpan<T> span, T value)
        where T : unmanaged, IEquatable<T>
    {
        for (int i = span.Length - 1; i >= 0; i--)
        {
            if (span[i].Equals(value))
            {
                return i;
            }
        }

        return -1;
    }

    public static int BinarySearch<T>(ReadOnlySpan<T> sortedSpan, T value)
        where T : unmanaged, IComparable<T>
    {
        return TryBinarySearch(sortedSpan, 0, sortedSpan.Length, value).Index;
    }

    public static void Sort<T>(Span<T> span)
        where T : unmanaged, IComparable<T>
    {
        SortCore(span, 0, span.Length);
    }

    public static ArraySortResult TrySort<T>(Span<T> span, int start, int length)
        where T : unmanaged, IComparable<T>
    {
        if (!IsValidRange(span.Length, start, length))
        {
            return ArraySortResult.InvalidRange(start, length, span.Length);
        }

        SortCore(span, start, length);
        return ArraySortResult.Success(start, length, span.Length);
    }

    public static ArraySearchResult TryBinarySearch<T>(ReadOnlySpan<T> sortedSpan, int start, int length, T value)
        where T : unmanaged, IComparable<T>
    {
        if (!IsValidRange(sortedSpan.Length, start, length))
        {
            return ArraySearchResult.InvalidRange(start, length, sortedSpan.Length);
        }

        int low = start;
        int high = start + length - 1;
        while (low <= high)
        {
            int mid = low + ((high - low) >> 1);
            int comparison = sortedSpan[mid].CompareTo(value);
            if (comparison == 0)
            {
                return ArraySearchResult.Success(mid, start, length, sortedSpan.Length);
            }

            if (comparison < 0)
            {
                low = mid + 1;
            }
            else
            {
                high = mid - 1;
            }
        }

        return ArraySearchResult.NotFound(low, start, length, sortedSpan.Length);
    }

    private static void SortCore<T>(Span<T> span, int start, int length)
        where T : unmanaged, IComparable<T>
    {
        int end = start + length;
        for (int i = start + 1; i < end; i++)
        {
            T value = span[i];
            int insert = i - 1;
            while (insert >= start && span[insert].CompareTo(value) > 0)
            {
                span[insert + 1] = span[insert];
                insert--;
            }

            span[insert + 1] = value;
        }
    }

    private static bool IsValidRange(int sourceLength, int start, int length)
    {
        return start >= 0 && length >= 0 && start <= sourceLength && length <= sourceLength - start;
    }
}
