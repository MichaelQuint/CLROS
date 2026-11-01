namespace OS.Manifest;
public readonly struct NativeManifestEnvironmentEntry
{
    public NativeManifestEnvironmentEntry(NativeManifestField key, NativeManifestField value)
    {
        Key = key;
        Value = value;
        IsPresent = true;
    }

    public NativeManifestField Key { get; }
    public NativeManifestField Value { get; }
    public bool IsPresent { get; }

    public bool TryGetKey(ReadOnlySpan<byte> source, out ReadOnlySpan<byte> value)
    {
        return Key.TryGetSlice(source, out value);
    }

    public bool TryGetValue(ReadOnlySpan<byte> source, out ReadOnlySpan<byte> value)
    {
        return Value.TryGetSlice(source, out value);
    }
}
