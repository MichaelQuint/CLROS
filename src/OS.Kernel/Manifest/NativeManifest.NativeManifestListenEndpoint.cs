namespace OS.Manifest;
public readonly struct NativeManifestListenEndpoint
{
    public NativeManifestListenEndpoint(NativeManifestField host, ushort port)
    {
        Host = host;
        Port = port;
        IsPresent = true;
    }

    public NativeManifestField Host { get; }
    public ushort Port { get; }
    public bool IsPresent { get; }

    public bool TryGetHost(ReadOnlySpan<byte> source, out ReadOnlySpan<byte> value)
    {
        return Host.TryGetSlice(source, out value);
    }
}
