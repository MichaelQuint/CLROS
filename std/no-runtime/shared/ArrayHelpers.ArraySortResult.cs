namespace OS.Std;

public readonly struct ArraySortResult
{
    private ArraySortResult(bool isInitialized, ArraySortStatus status, int start, int length, int sourceLength)
    {
        IsInitialized = isInitialized;
        Status = status;
        Start = start;
        Length = length;
        SourceLength = sourceLength;
    }

    public bool IsInitialized
    {
        get;
    }
    public ArraySortStatus Status
    {
        get;
    }
    public int Start
    {
        get;
    }
    public int Length
    {
        get;
    }
    public int SourceLength
    {
        get;
    }
    public bool Succeeded => IsInitialized && Status == ArraySortStatus.Success;

    public static ArraySortResult Success(int start, int length, int sourceLength)
    {
        return new ArraySortResult(true, ArraySortStatus.Success, start, length, sourceLength);
    }

    public static ArraySortResult InvalidRange(int start, int length, int sourceLength)
    {
        return new ArraySortResult(true, ArraySortStatus.InvalidRange, start, length, sourceLength);
    }
}
