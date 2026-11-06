namespace OS.Manifest;

public readonly struct NativeManifestField
{
    public NativeManifestField(int offset, int length)
    {
        Offset = offset;
        Length = length;
        IsPresent = true;
    }

    public int Offset
    {
        get;
    }
    public int Length
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
