namespace OS.Manifest;
public readonly struct NativeAppArgument
{
    public NativeAppArgument(NativeManifestField value)
    {
        Value = value;
        IsPresent = true;
    }

    public NativeManifestField Value { get; }
    public bool IsPresent { get; }

    public bool TryGetValue(ReadOnlySpan<byte> manifestSource, out ReadOnlySpan<byte> value)
    {
        return Value.TryGetSlice(manifestSource, out value);
    }
}
