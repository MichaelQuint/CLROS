using OS.Std;

namespace OS.Std.Text;
public readonly struct AsciiTokenRange
{
    private AsciiTokenRange(bool isInitialized, int start, int length)
    {
        IsInitialized = isInitialized;
        Start = start;
        Length = length;
    }

    public bool IsInitialized { get; }
    public int Start { get; }
    public int Length { get; }
    public int End => Start + Length;
    public bool Succeeded => IsInitialized && Start >= 0 && Length >= 0 && End >= Start;

    public static AsciiTokenRange Create(int start, int length)
    {
        return new AsciiTokenRange(true, start, length);
    }
}
