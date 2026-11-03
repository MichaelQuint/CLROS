namespace OS.Drivers;

public readonly struct VirtioPciBarSizeSource
{
    public VirtioPciBarSizeSource(int sourceIndex, ReadOnlyMemory<ulong> barSizes)
    {
        SourceIndex = sourceIndex;
        BarSizes = barSizes;
    }

    public int SourceIndex
    {
        get;
    }
    public ReadOnlyMemory<ulong> BarSizes
    {
        get;
    }
}
