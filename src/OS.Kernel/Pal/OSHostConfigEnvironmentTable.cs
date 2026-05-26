using OS.Manifest;
using OS.Std;
using OS.Std.Memory;
using OS.Std.Text;

namespace OS.Pal;

public readonly struct OSHostConfigEnvironmentEntry
{
    public OSHostConfigEnvironmentEntry(ReadOnlyMemory<byte> keyUtf8, ReadOnlyMemory<byte> valueUtf8)
    {
        KeyUtf8 = keyUtf8;
        ValueUtf8 = valueUtf8;
    }

    public ReadOnlyMemory<byte> KeyUtf8 { get; }

    public ReadOnlyMemory<byte> ValueUtf8 { get; }
}

public readonly struct OSHostConfigQueryResult
{
    public OSHostConfigQueryResult(
        OSHostStatus status,
        ulong bytesWritten,
        ulong requiredBytes,
        bool isInitialized = true)
    {
        Status = status;
        BytesWritten = bytesWritten;
        RequiredBytes = requiredBytes;
        IsInitialized = isInitialized;
    }

    public OSHostStatus Status { get; }

    public ulong BytesWritten { get; }

    public ulong RequiredBytes { get; }

    public bool IsInitialized { get; }

    public bool Succeeded => IsInitialized && Status == OSHostStatus.Ok;

    public static OSHostConfigQueryResult FromStatus(OSHostStatus status, ulong requiredBytes = 0)
    {
        return new OSHostConfigQueryResult(status, bytesWritten: 0, requiredBytes);
    }
}

public readonly struct OSHostEnvironmentEnumerationResult
{
    public OSHostEnvironmentEnumerationResult(
        OSHostStatus status,
        ulong keyBytesWritten,
        ulong valueBytesWritten,
        ulong requiredKeyBytes,
        ulong requiredValueBytes,
        bool isInitialized = true)
    {
        Status = status;
        KeyBytesWritten = keyBytesWritten;
        ValueBytesWritten = valueBytesWritten;
        RequiredKeyBytes = requiredKeyBytes;
        RequiredValueBytes = requiredValueBytes;
        IsInitialized = isInitialized;
    }

    public OSHostStatus Status { get; }

    public ulong KeyBytesWritten { get; }

    public ulong ValueBytesWritten { get; }

    public ulong RequiredKeyBytes { get; }

    public ulong RequiredValueBytes { get; }

    public bool IsInitialized { get; }

    public bool Succeeded => IsInitialized && Status == OSHostStatus.Ok;

    public static OSHostEnvironmentEnumerationResult FromStatus(
        OSHostStatus status,
        ulong requiredKeyBytes = 0,
        ulong requiredValueBytes = 0)
    {
        return new OSHostEnvironmentEnumerationResult(
            status,
            keyBytesWritten: 0,
            valueBytesWritten: 0,
            requiredKeyBytes,
            requiredValueBytes);
    }
}

public readonly struct OSHostConfigEnvironmentTableBuildResult
{
    public OSHostConfigEnvironmentTableBuildResult(
        OSHostStatus status,
        OSHostConfigEnvironmentTable? table)
    {
        Status = status;
        Table = table;
    }

    public OSHostStatus Status { get; }

    public OSHostConfigEnvironmentTable? Table { get; }

    public bool Succeeded => Status == OSHostStatus.Ok && Table is not null;

    public static OSHostConfigEnvironmentTableBuildResult FromStatus(OSHostStatus status)
    {
        return new OSHostConfigEnvironmentTableBuildResult(status, null);
    }
}

public sealed class OSHostConfigEnvironmentTable
{
    public const int MaxConfigEntries = 16;
    public const int MaxEnvironmentEntries = 16;

    public const bool HostOnly = true;
    public const bool ExportsPalFunctions = false;
    public const bool ReadsGuestStorage = false;
    public const bool OpensSockets = false;
    public const bool StartsKestrel = false;

    private readonly OSHostConfigEnvironmentEntry[] configEntries;
    private readonly OSHostConfigEnvironmentEntry[] environmentEntries;

    private OSHostConfigEnvironmentTable(
        ReadOnlySpan<OSHostConfigEnvironmentEntry> configEntries,
        ReadOnlySpan<OSHostConfigEnvironmentEntry> environmentEntries)
    {
        this.configEntries = configEntries.ToArray();
        this.environmentEntries = environmentEntries.ToArray();
    }

    public int ConfigCount => configEntries.Length;

    public int EnvironmentCount => environmentEntries.Length;

    public static OSHostConfigEnvironmentTableBuildResult Create(
        ReadOnlySpan<OSHostConfigEnvironmentEntry> configEntries,
        ReadOnlySpan<OSHostConfigEnvironmentEntry> environmentEntries)
    {
        if (configEntries.Length > MaxConfigEntries ||
            environmentEntries.Length > MaxEnvironmentEntries)
        {
            return OSHostConfigEnvironmentTableBuildResult.FromStatus(OSHostStatus.OutOfMemory);
        }

        return new OSHostConfigEnvironmentTableBuildResult(
            OSHostStatus.Ok,
            new OSHostConfigEnvironmentTable(configEntries, environmentEntries));
    }

    public static OSHostConfigEnvironmentTableBuildResult FromNativeLaunchPlan(
        ReadOnlyMemory<byte> manifestSource,
        NativeAppLaunchPlan plan)
    {
        if (!TryGetMemory(manifestSource, plan.AppName, out ReadOnlyMemory<byte> appName)
            || !TryGetMemory(manifestSource, plan.AppPath, out ReadOnlyMemory<byte> appPath)
            || !TryGetMemory(manifestSource, plan.Arguments, out ReadOnlyMemory<byte> arguments))
        {
            return OSHostConfigEnvironmentTableBuildResult.FromStatus(OSHostStatus.InvalidArgument);
        }

        OSHostConfigEnvironmentEntry[] configEntries =
            new OSHostConfigEnvironmentEntry[plan.HasListenEndpoint ? 10 : 7];
        int configIndex = 0;
        configEntries[configIndex++] = Entry("app.name", appName);
        configEntries[configIndex++] = Entry("app.path", appPath);
        configEntries[configIndex++] = Entry("app.args", arguments);
        configEntries[configIndex++] = Entry("app.kind", AppKindValue(plan.AppKind));
        configEntries[configIndex++] = Entry("runtime.kind", RuntimeKindValue(plan.RuntimeKind));
        configEntries[configIndex++] = Entry("network", NetworkModeValue(plan.RequestedNetworkMode));
        configEntries[configIndex++] = Entry("log", LogModeValue(plan.LogMode));

        if (plan.HasListenEndpoint)
        {
            if (!TryGetMemory(manifestSource, plan.ListenEndpoint.Host, out ReadOnlyMemory<byte> listenHost))
            {
                return OSHostConfigEnvironmentTableBuildResult.FromStatus(OSHostStatus.InvalidArgument);
            }

            byte[] listenPort = PortBytes(plan.ListenPort);
            if (!TryBuildHttpListenEndpoint(
                listenHost.Span,
                listenPort,
                out byte[] listenEndpoint))
            {
                return OSHostConfigEnvironmentTableBuildResult.FromStatus(OSHostStatus.InvalidArgument);
            }

            configEntries[configIndex++] = Entry("kestrel.listen.0", listenEndpoint);
            configEntries[configIndex++] = Entry("kestrel.listen.0.host", listenHost);
            configEntries[configIndex++] = Entry("kestrel.listen.0.port", listenPort);
        }

        OSHostConfigEnvironmentEntry[] environmentEntries =
            new OSHostConfigEnvironmentEntry[plan.EnvironmentCount];
        for (int index = 0; index < environmentEntries.Length; index++)
        {
            NativeManifestEnvironmentEntry environment = plan.GetEnvironmentEntry(index);
            if (!environment.IsPresent
                || !TryGetMemory(manifestSource, environment.Key, out ReadOnlyMemory<byte> key)
                || !TryGetMemory(manifestSource, environment.Value, out ReadOnlyMemory<byte> value))
            {
                return OSHostConfigEnvironmentTableBuildResult.FromStatus(OSHostStatus.InvalidArgument);
            }

            environmentEntries[index] = new OSHostConfigEnvironmentEntry(key, value);
        }

        return Create(configEntries, environmentEntries);
    }

    public OSHostConfigQueryResult TryGetConfigValue(
        ReadOnlySpan<byte> keyUtf8,
        Span<byte> destination)
    {
        return TryGetValue(configEntries, keyUtf8, destination);
    }

    public OSHostConfigQueryResult TryGetEnvironmentValue(
        ReadOnlySpan<byte> keyUtf8,
        Span<byte> destination)
    {
        return TryGetValue(environmentEntries, keyUtf8, destination);
    }

    public OSHostEnvironmentEnumerationResult EnumerateEnvironment(
        uint index,
        Span<byte> keyDestination,
        Span<byte> valueDestination)
    {
        return EnumerateEntry(environmentEntries, index, keyDestination, valueDestination);
    }

    public OSHostEnvironmentEnumerationResult EnumerateConfig(
        uint index,
        Span<byte> keyDestination,
        Span<byte> valueDestination)
    {
        return EnumerateEntry(configEntries, index, keyDestination, valueDestination);
    }

    private static OSHostEnvironmentEnumerationResult EnumerateEntry(
        OSHostConfigEnvironmentEntry[] entries,
        uint index,
        Span<byte> keyDestination,
        Span<byte> valueDestination)
    {
        if (index >= (uint)entries.Length)
        {
            return OSHostEnvironmentEnumerationResult.FromStatus(OSHostStatus.NotFound);
        }

        OSHostConfigEnvironmentEntry entry = entries[(int)index];
        ReadOnlySpan<byte> key = entry.KeyUtf8.Span;
        ReadOnlySpan<byte> value = entry.ValueUtf8.Span;

        if (keyDestination.Length < key.Length || valueDestination.Length < value.Length)
        {
            return OSHostEnvironmentEnumerationResult.FromStatus(
                OSHostStatus.BufferTooSmall,
                (ulong)key.Length,
                (ulong)value.Length);
        }

        BufferOperationResult keyCopy = BufferOperations.MemoryCopy(key, keyDestination);
        if (!keyCopy.Succeeded)
        {
            return OSHostEnvironmentEnumerationResult.FromStatus(
                OSHostStatus.BufferTooSmall,
                (ulong)key.Length,
                (ulong)value.Length);
        }

        BufferOperationResult valueCopy = BufferOperations.MemoryCopy(value, valueDestination);
        if (!valueCopy.Succeeded)
        {
            return OSHostEnvironmentEnumerationResult.FromStatus(
                OSHostStatus.BufferTooSmall,
                (ulong)key.Length,
                (ulong)value.Length);
        }

        return new OSHostEnvironmentEnumerationResult(
            OSHostStatus.Ok,
            (ulong)key.Length,
            (ulong)value.Length,
            (ulong)key.Length,
            (ulong)value.Length);
    }

    private static OSHostConfigQueryResult TryGetValue(
        OSHostConfigEnvironmentEntry[] entries,
        ReadOnlySpan<byte> keyUtf8,
        Span<byte> destination)
    {
        if (!IsValidKey(keyUtf8))
        {
            return OSHostConfigQueryResult.FromStatus(OSHostStatus.InvalidArgument);
        }

        for (int index = 0; index < entries.Length; index++)
        {
            ReadOnlySpan<byte> key = entries[index].KeyUtf8.Span;
            if (!OS.Std.MemoryExtensions.SequenceEqual(key, keyUtf8))
            {
                continue;
            }

            ReadOnlySpan<byte> value = entries[index].ValueUtf8.Span;
            if (destination.Length < value.Length)
            {
                return OSHostConfigQueryResult.FromStatus(
                    OSHostStatus.BufferTooSmall,
                    (ulong)value.Length);
            }

            BufferOperationResult copy = BufferOperations.MemoryCopy(value, destination);
            if (!copy.Succeeded)
            {
                return OSHostConfigQueryResult.FromStatus(
                    OSHostStatus.BufferTooSmall,
                    (ulong)value.Length);
            }

            return new OSHostConfigQueryResult(
                OSHostStatus.Ok,
                (ulong)value.Length,
                (ulong)value.Length);
        }

        return OSHostConfigQueryResult.FromStatus(OSHostStatus.NotFound);
    }

    private static bool IsValidKey(ReadOnlySpan<byte> keyUtf8)
    {
        if (keyUtf8.Length == 0)
        {
            return false;
        }

        for (int index = 0; index < keyUtf8.Length; index++)
        {
            if (!Ascii.IsVisible(keyUtf8[index]))
            {
                return false;
            }
        }

        return true;
    }

    private static bool TryGetMemory(
        ReadOnlyMemory<byte> source,
        NativeManifestField field,
        out ReadOnlyMemory<byte> value)
    {
        if (!field.IsPresent
            || field.Offset < 0
            || field.Length < 0
            || field.Offset > source.Length
            || source.Length - field.Offset < field.Length)
        {
            value = default;
            return false;
        }

        value = source.Slice(field.Offset, field.Length);
        return true;
    }

    private static OSHostConfigEnvironmentEntry Entry(string key, ReadOnlyMemory<byte> value)
    {
        return new OSHostConfigEnvironmentEntry(AsciiBytes(key), value);
    }

    private static OSHostConfigEnvironmentEntry Entry(string key, string value)
    {
        return new OSHostConfigEnvironmentEntry(AsciiBytes(key), AsciiBytes(value));
    }

    private static bool TryBuildHttpListenEndpoint(
        ReadOnlySpan<byte> host,
        ReadOnlySpan<byte> port,
        out byte[] value)
    {
        const int PrefixLength = 7;
        value = new byte[PrefixLength + host.Length + 1 + port.Length];
        value[0] = (byte)'h';
        value[1] = (byte)'t';
        value[2] = (byte)'t';
        value[3] = (byte)'p';
        value[4] = (byte)':';
        value[5] = (byte)'/';
        value[6] = (byte)'/';
        BufferOperationResult hostCopy =
            BufferOperations.MemoryCopy(host, value.AsSpan(PrefixLength, host.Length));
        if (!hostCopy.Succeeded)
        {
            value = new byte[0];
            return false;
        }

        value[PrefixLength + host.Length] = (byte)':';
        BufferOperationResult portCopy =
            BufferOperations.MemoryCopy(port, value.AsSpan(PrefixLength + host.Length + 1, port.Length));
        if (!portCopy.Succeeded)
        {
            value = new byte[0];
            return false;
        }

        return true;
    }

    private static byte[] PortBytes(ushort port)
    {
        int count = AsciiDecimalFormatter.CountUInt64Digits(port);
        byte[] bytes = new byte[count];
        AsciiTextBuilderResult result = AsciiTextBuilder.AppendUInt64Decimal(bytes, 0, port);
        if (!result.Succeeded)
        {
            return new byte[0];
        }

        return bytes;
    }

    private static string AppKindValue(NativeManifestAppKind kind)
    {
        return kind == NativeManifestAppKind.Package ? "package" : "none";
    }

    private static string RuntimeKindValue(NativeManifestRuntimeKind kind)
    {
        return kind == NativeManifestRuntimeKind.NativeAot ? "native-aot" : "none";
    }

    private static string NetworkModeValue(NativeManifestNetworkMode mode)
    {
        return mode switch
        {
            NativeManifestNetworkMode.Dhcp => "dhcp",
            NativeManifestNetworkMode.Static => "static",
            _ => "none"
        };
    }

    private static string LogModeValue(NativeManifestLogMode mode)
    {
        return mode == NativeManifestLogMode.Serial ? "serial" : "none";
    }

    private static byte[] AsciiBytes(string value)
    {
        byte[] bytes = new byte[value.Length];
        for (int index = 0; index < value.Length; index++)
        {
            bytes[index] = (byte)value[index];
        }

        return bytes;
    }
}
