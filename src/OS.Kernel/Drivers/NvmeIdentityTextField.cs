using OS.Std.Binary;

namespace OS.Drivers;

public readonly struct NvmeIdentityTextField
{
    public NvmeIdentityTextField(int offset, int length, int maximumLength)
    {
        Offset = offset;
        Length = length;
        MaximumLength = maximumLength;
        IsPresent = length > 0;
    }

    public int Offset
    {
        get;
    }
    public int Length
    {
        get;
    }
    public int MaximumLength
    {
        get;
    }
    public bool IsPresent
    {
        get;
    }

    public bool TryGetSlice(ReadOnlySpan<byte> source, out ReadOnlySpan<byte> value)
    {
        if (!IsPresent || Offset < 0 || Length < 0 || Offset > source.Length || source.Length - Offset < Length)
        {
            value = default;
            return false;
        }

        value = source.Slice(Offset, Length);
        return true;
    }
}
