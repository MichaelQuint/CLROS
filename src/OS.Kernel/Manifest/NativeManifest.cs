namespace OS.Manifest;

public enum NativeManifestNetworkMode
{
    None = 0,
    Dhcp = 1,
    Static = 2
}

public enum NativeManifestLogMode
{
    None = 0,
    Serial = 1
}

public enum NativeManifestAppKind
{
    None = 0,
    Package = 1
}

public enum NativeManifestRuntimeKind
{
    None = 0,
    NativeAot = 1
}

public readonly struct NativeManifestField
{
    public NativeManifestField(int offset, int length)
    {
        Offset = offset;
        Length = length;
        IsPresent = true;
    }

    public int Offset { get; }

    public int Length { get; }

    public bool IsPresent { get; }

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

public struct NativeManifestEnvironmentBlock
{
    private NativeManifestEnvironmentEntry _entry0;
    private NativeManifestEnvironmentEntry _entry1;
    private NativeManifestEnvironmentEntry _entry2;
    private NativeManifestEnvironmentEntry _entry3;
    private NativeManifestEnvironmentEntry _entry4;
    private NativeManifestEnvironmentEntry _entry5;
    private NativeManifestEnvironmentEntry _entry6;
    private NativeManifestEnvironmentEntry _entry7;
    private NativeManifestEnvironmentEntry _entry8;
    private NativeManifestEnvironmentEntry _entry9;
    private NativeManifestEnvironmentEntry _entry10;
    private NativeManifestEnvironmentEntry _entry11;
    private NativeManifestEnvironmentEntry _entry12;
    private NativeManifestEnvironmentEntry _entry13;
    private NativeManifestEnvironmentEntry _entry14;
    private NativeManifestEnvironmentEntry _entry15;

    public int Count { get; private set; }

    public bool TryAdd(NativeManifestEnvironmentEntry entry)
    {
        if (Count >= NativeManifestParser.MaximumEnvironmentCount)
        {
            return false;
        }

        SetAt(Count, entry);
        Count++;
        return true;
    }

    public NativeManifestEnvironmentEntry GetAt(int index)
    {
        return index switch
        {
            0 => _entry0,
            1 => _entry1,
            2 => _entry2,
            3 => _entry3,
            4 => _entry4,
            5 => _entry5,
            6 => _entry6,
            7 => _entry7,
            8 => _entry8,
            9 => _entry9,
            10 => _entry10,
            11 => _entry11,
            12 => _entry12,
            13 => _entry13,
            14 => _entry14,
            15 => _entry15,
            _ => default
        };
    }

    private void SetAt(int index, NativeManifestEnvironmentEntry entry)
    {
        switch (index)
        {
            case 0:
                _entry0 = entry;
                break;
            case 1:
                _entry1 = entry;
                break;
            case 2:
                _entry2 = entry;
                break;
            case 3:
                _entry3 = entry;
                break;
            case 4:
                _entry4 = entry;
                break;
            case 5:
                _entry5 = entry;
                break;
            case 6:
                _entry6 = entry;
                break;
            case 7:
                _entry7 = entry;
                break;
            case 8:
                _entry8 = entry;
                break;
            case 9:
                _entry9 = entry;
                break;
            case 10:
                _entry10 = entry;
                break;
            case 11:
                _entry11 = entry;
                break;
            case 12:
                _entry12 = entry;
                break;
            case 13:
                _entry13 = entry;
                break;
            case 14:
                _entry14 = entry;
                break;
            case 15:
                _entry15 = entry;
                break;
        }
    }
}

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

public readonly struct NativeManifest
{
    public const byte Version0 = 0;

    public NativeManifest(
        byte version,
        NativeManifestField appName,
        NativeManifestField appPath,
        NativeManifestField args,
        NativeManifestAppKind appKind,
        NativeManifestRuntimeKind runtimeKind,
        NativeManifestNetworkMode networkMode,
        NativeManifestLogMode logMode)
        : this(
            version,
            appName,
            appPath,
            args,
            appKind,
            runtimeKind,
            networkMode,
            logMode,
            default,
            default)
    {
    }

    public NativeManifest(
        byte version,
        NativeManifestField appName,
        NativeManifestField appPath,
        NativeManifestField args,
        NativeManifestAppKind appKind,
        NativeManifestRuntimeKind runtimeKind,
        NativeManifestNetworkMode networkMode,
        NativeManifestLogMode logMode,
        NativeManifestEnvironmentBlock environment)
        : this(
            version,
            appName,
            appPath,
            args,
            appKind,
            runtimeKind,
            networkMode,
            logMode,
            environment,
            default)
    {
    }

    public NativeManifest(
        byte version,
        NativeManifestField appName,
        NativeManifestField appPath,
        NativeManifestField args,
        NativeManifestAppKind appKind,
        NativeManifestRuntimeKind runtimeKind,
        NativeManifestNetworkMode networkMode,
        NativeManifestLogMode logMode,
        NativeManifestEnvironmentBlock environment,
        NativeManifestListenEndpoint listenEndpoint)
    {
        Version = version;
        AppName = appName;
        AppPath = appPath;
        Args = args;
        AppKind = appKind;
        RuntimeKind = runtimeKind;
        NetworkMode = networkMode;
        LogMode = logMode;
        Environment = environment;
        ListenEndpoint = listenEndpoint;
    }

    public byte Version { get; }

    public NativeManifestField AppName { get; }

    public NativeManifestField AppPath { get; }

    public NativeManifestField Args { get; }

    public NativeManifestAppKind AppKind { get; }

    public NativeManifestRuntimeKind RuntimeKind { get; }

    public NativeManifestNetworkMode NetworkMode { get; }

    public NativeManifestLogMode LogMode { get; }

    public NativeManifestEnvironmentBlock Environment { get; }

    public int EnvironmentCount => Environment.Count;

    public NativeManifestListenEndpoint ListenEndpoint { get; }

    public bool HasListenEndpoint => ListenEndpoint.IsPresent;

    public NativeManifestEnvironmentEntry GetEnvironmentEntry(int index)
    {
        return Environment.GetAt(index);
    }
}
