namespace OS.Std;

public enum ArraySearchStatus
{
    Success = 0,
    NotFound = 1,
    InvalidRange = 2
}

public enum ArraySortStatus
{
    Success = 0,
    InvalidRange = 1
}

public readonly struct ArraySearchResult
{
    private ArraySearchResult(
        bool isInitialized,
        ArraySearchStatus status,
        int index,
        int insertionIndex,
        int start,
        int length,
        int sourceLength)
    {
        IsInitialized = isInitialized;
        Status = status;
        Index = index;
        InsertionIndex = insertionIndex;
        Start = start;
        Length = length;
        SourceLength = sourceLength;
    }

    public bool IsInitialized { get; }

    public ArraySearchStatus Status { get; }

    public int Index { get; }

    public int InsertionIndex { get; }

    public int Start { get; }

    public int Length { get; }

    public int SourceLength { get; }

    public bool Succeeded =>
        IsInitialized &&
        Status == ArraySearchStatus.Success &&
        Index >= Start &&
        Index < Start + Length;

    public static ArraySearchResult Success(
        int index,
        int start,
        int length,
        int sourceLength)
    {
        return new ArraySearchResult(
            true,
            ArraySearchStatus.Success,
            index,
            index,
            start,
            length,
            sourceLength);
    }

    public static ArraySearchResult NotFound(
        int insertionIndex,
        int start,
        int length,
        int sourceLength)
    {
        return new ArraySearchResult(
            true,
            ArraySearchStatus.NotFound,
            ~insertionIndex,
            insertionIndex,
            start,
            length,
            sourceLength);
    }

    public static ArraySearchResult InvalidRange(
        int start,
        int length,
        int sourceLength)
    {
        return new ArraySearchResult(
            true,
            ArraySearchStatus.InvalidRange,
            -1,
            -1,
            start,
            length,
            sourceLength);
    }
}

public readonly struct ArraySortResult
{
    private ArraySortResult(
        bool isInitialized,
        ArraySortStatus status,
        int start,
        int length,
        int sourceLength)
    {
        IsInitialized = isInitialized;
        Status = status;
        Start = start;
        Length = length;
        SourceLength = sourceLength;
    }

    public bool IsInitialized { get; }

    public ArraySortStatus Status { get; }

    public int Start { get; }

    public int Length { get; }

    public int SourceLength { get; }

    public bool Succeeded =>
        IsInitialized &&
        Status == ArraySortStatus.Success;

    public static ArraySortResult Success(
        int start,
        int length,
        int sourceLength)
    {
        return new ArraySortResult(
            true,
            ArraySortStatus.Success,
            start,
            length,
            sourceLength);
    }

    public static ArraySortResult InvalidRange(
        int start,
        int length,
        int sourceLength)
    {
        return new ArraySortResult(
            true,
            ArraySortStatus.InvalidRange,
            start,
            length,
            sourceLength);
    }
}

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

    public static ArraySortResult TrySort<T>(
        Span<T> span,
        int start,
        int length)
        where T : unmanaged, IComparable<T>
    {
        if (!IsValidRange(span.Length, start, length))
        {
            return ArraySortResult.InvalidRange(start, length, span.Length);
        }

        SortCore(span, start, length);
        return ArraySortResult.Success(start, length, span.Length);
    }

    public static ArraySearchResult TryBinarySearch<T>(
        ReadOnlySpan<T> sortedSpan,
        int start,
        int length,
        T value)
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
                return ArraySearchResult.Success(
                    mid,
                    start,
                    length,
                    sortedSpan.Length);
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
        return start >= 0 &&
            length >= 0 &&
            start <= sourceLength &&
            length <= sourceLength - start;
    }
}
