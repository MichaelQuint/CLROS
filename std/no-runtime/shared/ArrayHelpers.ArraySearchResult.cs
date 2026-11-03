namespace OS.Std;
public readonly struct ArraySearchResult
{
    private ArraySearchResult(bool isInitialized, ArraySearchStatus status, int index, int insertionIndex, int start, int length, int sourceLength)
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
    public bool Succeeded => IsInitialized && Status == ArraySearchStatus.Success && Index >= Start && Index < Start + Length;

    public static ArraySearchResult Success(int index, int start, int length, int sourceLength)
    {
        return new ArraySearchResult(true, ArraySearchStatus.Success, index, index, start, length, sourceLength);
    }

    public static ArraySearchResult NotFound(int insertionIndex, int start, int length, int sourceLength)
    {
        return new ArraySearchResult(true, ArraySearchStatus.NotFound, ~insertionIndex, insertionIndex, start, length, sourceLength);
    }

    public static ArraySearchResult InvalidRange(int start, int length, int sourceLength)
    {
        return new ArraySearchResult(true, ArraySearchStatus.InvalidRange, -1, -1, start, length, sourceLength);
    }
}
