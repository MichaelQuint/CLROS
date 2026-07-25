using OS.Std;
using OS.Std.Text;

namespace OS.Manifest;

public enum NativeManifestParseStatus
{
    Success = 0,
    EmptyInput,
    InputTooLarge,
    NonAsciiOrControlCharacter,
    MalformedLine,
    UnknownKey,
    DuplicateKey,
    MissingFormat,
    UnsupportedFormat,
    MissingVersion,
    UnsupportedVersion,
    MissingAppName,
    AppNameTooLong,
    MissingAppPath,
    AppPathTooLong,
    MissingArgs,
    ArgsTooLong,
    MissingAppKind,
    UnsupportedAppKind,
    MissingRuntimeKind,
    UnsupportedRuntimeKind,
    MissingNetworkMode,
    InvalidNetworkMode,
    MissingLogMode,
    InvalidLogMode,
    EnvironmentKeyTooLong,
    EnvironmentValueTooLong,
    InvalidEnvironmentKey,
    TooManyEnvironmentFields,
    DuplicateEnvironmentKey,
    InvalidListenEndpoint,
    ListenRequiresNetwork
}

public readonly struct NativeManifestParseResult
{
    private NativeManifestParseResult(
        NativeManifestParseStatus status,
        bool isInitialized,
        NativeManifest manifest,
        int errorOffset)
    {
        Status = status;
        IsInitialized = isInitialized;
        Manifest = manifest;
        ErrorOffset = errorOffset;
    }

    public NativeManifestParseStatus Status { get; }

    public bool IsInitialized { get; }

    public NativeManifest Manifest { get; }

    public int ErrorOffset { get; }

    public bool Succeeded => IsInitialized && Status == NativeManifestParseStatus.Success;

    public static NativeManifestParseResult Success(NativeManifest manifest)
    {
        return new NativeManifestParseResult(
            NativeManifestParseStatus.Success,
            isInitialized: true,
            manifest,
            -1);
    }

    public static NativeManifestParseResult Fail(NativeManifestParseStatus status, int errorOffset)
    {
        return new NativeManifestParseResult(
            status,
            isInitialized: true,
            default,
            errorOffset);
    }
}

public static class NativeManifestParser
{
    public const int MaximumInputLength = 4096;
    public const int MaximumAppNameLength = 64;
    public const int MaximumAppPathLength = 128;
    public const int MaximumArgsLength = 256;
    public const int MaximumEnvironmentCount = 16;
    public const int MaximumEnvironmentKeyLength = 64;
    public const int MaximumEnvironmentValueLength = 256;
    public const int MaximumListenHostLength = 64;

    private const uint FormatSeen = 1u << 0;
    private const uint VersionSeen = 1u << 1;
    private const uint AppNameSeen = 1u << 2;
    private const uint AppPathSeen = 1u << 3;
    private const uint ArgsSeen = 1u << 4;
    private const uint NetworkSeen = 1u << 5;
    private const uint LogSeen = 1u << 6;
    private const uint AppKindSeen = 1u << 7;
    private const uint RuntimeKindSeen = 1u << 8;
    private const uint ListenSeen = 1u << 9;

    public static NativeManifestParseResult Parse(ReadOnlySpan<byte> source)
    {
        if (source.Length == 0)
        {
            return NativeManifestParseResult.Fail(NativeManifestParseStatus.EmptyInput, 0);
        }

        if (source.Length > MaximumInputLength)
        {
            return NativeManifestParseResult.Fail(NativeManifestParseStatus.InputTooLarge, MaximumInputLength);
        }

        uint seen = 0;
        NativeManifestField appName = default;
        NativeManifestField appPath = default;
        NativeManifestField args = default;
        NativeManifestAppKind appKind = NativeManifestAppKind.Package;
        NativeManifestRuntimeKind runtimeKind = NativeManifestRuntimeKind.NativeAot;
        NativeManifestNetworkMode networkMode = NativeManifestNetworkMode.None;
        NativeManifestLogMode logMode = NativeManifestLogMode.None;
        NativeManifestEnvironmentBlock environment = default;
        NativeManifestListenEndpoint listenEndpoint = default;

        Span<AsciiTokenRange> lineTokens = stackalloc AsciiTokenRange[source.Length + 1];
        AsciiTokenSplitResult lineSplit = AsciiTokenSplitter.Split(source, (byte)'\n', lineTokens);
        if (!lineSplit.Succeeded)
        {
            return NativeManifestParseResult.Fail(
                MapTokenSplitFailure(lineSplit, NativeManifestParseStatus.MalformedLine),
                GetTokenSplitErrorOffset(lineSplit, 0));
        }

        for (int lineTokenIndex = 0; lineTokenIndex < lineSplit.TokenCount; lineTokenIndex++)
        {
            AsciiTokenRange lineToken = lineTokens[lineTokenIndex];
            if (IsTrailingLineDelimiterToken(source, lineToken, lineTokenIndex, lineSplit.TokenCount))
            {
                continue;
            }

            int lineStart = lineToken.Start;
            int lineLength = lineToken.Length;
            NativeManifestParseStatus lineStatus = NormalizeAndValidateLine(
                source,
                lineStart,
                ref lineLength,
                out int errorOffset);
            if (lineStatus != NativeManifestParseStatus.Success)
            {
                return NativeManifestParseResult.Fail(lineStatus, errorOffset);
            }

            NativeManifestParseStatus parseStatus = ParseLine(
                source,
                lineStart,
                lineLength,
                ref seen,
                ref appName,
                ref appPath,
                ref args,
                ref appKind,
                ref runtimeKind,
                ref networkMode,
                ref logMode,
                ref environment,
                ref listenEndpoint,
                out errorOffset);
            if (parseStatus != NativeManifestParseStatus.Success)
            {
                return NativeManifestParseResult.Fail(parseStatus, errorOffset);
            }
        }

        NativeManifestParseStatus requiredStatus = ValidateRequiredFields(seen);
        if (requiredStatus != NativeManifestParseStatus.Success)
        {
            return NativeManifestParseResult.Fail(requiredStatus, source.Length);
        }

        NativeManifestParseStatus listenStatus = ValidateListenEndpoint(seen, networkMode);
        if (listenStatus != NativeManifestParseStatus.Success)
        {
            return NativeManifestParseResult.Fail(listenStatus, source.Length);
        }

        NativeManifest manifest = new NativeManifest(
            NativeManifest.Version0,
            appName,
            appPath,
            args,
            appKind,
            runtimeKind,
            networkMode,
            logMode,
            environment,
            listenEndpoint);
        return NativeManifestParseResult.Success(manifest);
    }

    private static NativeManifestParseStatus NormalizeAndValidateLine(
        ReadOnlySpan<byte> source,
        int lineStart,
        ref int lineLength,
        out int errorOffset)
    {
        errorOffset = -1;

        if (lineLength > 0 && source[lineStart + lineLength - 1] == (byte)'\r')
        {
            int delimiterOffset = lineStart + lineLength;
            if (delimiterOffset < source.Length && source[delimiterOffset] == (byte)'\n')
            {
                lineLength--;
            }
            else
            {
                errorOffset = lineStart + lineLength - 1;
                return NativeManifestParseStatus.MalformedLine;
            }
        }

        int lineEnd = lineStart + lineLength;
        for (int index = lineStart; index < lineEnd; index++)
        {
            byte value = source[index];
            if (value == (byte)'\r')
            {
                errorOffset = index;
                return NativeManifestParseStatus.MalformedLine;
            }

            if (!IsPrintableAscii(value))
            {
                errorOffset = index;
                return NativeManifestParseStatus.NonAsciiOrControlCharacter;
            }
        }

        return NativeManifestParseStatus.Success;
    }

    private static NativeManifestParseStatus ParseLine(
        ReadOnlySpan<byte> source,
        int lineOffset,
        int lineLength,
        ref uint seen,
        ref NativeManifestField appName,
        ref NativeManifestField appPath,
        ref NativeManifestField args,
        ref NativeManifestAppKind appKind,
        ref NativeManifestRuntimeKind runtimeKind,
        ref NativeManifestNetworkMode networkMode,
        ref NativeManifestLogMode logMode,
        ref NativeManifestEnvironmentBlock environment,
        ref NativeManifestListenEndpoint listenEndpoint,
        out int errorOffset)
    {
        errorOffset = lineOffset;
        if (lineLength == 0)
        {
            return NativeManifestParseStatus.MalformedLine;
        }

        NativeManifestParseStatus splitStatus = TrySplitKeyValue(
            source,
            lineOffset,
            lineLength,
            out AsciiTokenRange keyRange,
            out int valueOffset,
            out int valueLength,
            out errorOffset);
        if (splitStatus != NativeManifestParseStatus.Success)
        {
            return splitStatus;
        }

        ReadOnlySpan<byte> key = source.Slice(keyRange.Start, keyRange.Length);
        ReadOnlySpan<byte> value = source.Slice(valueOffset, valueLength);

        if (OS.Std.MemoryExtensions.StartsWith(key, "env."u8))
        {
            return ParseEnvironment(
                source,
                key.Slice(4),
                lineOffset + 4,
                value,
                valueOffset,
                ref environment,
                out errorOffset);
        }

        if (AsciiString.Equals(key, "format"u8))
        {
            return ParseFormat(value, valueOffset, ref seen, out errorOffset);
        }

        if (AsciiString.Equals(key, "version"u8))
        {
            return ParseVersion(value, valueOffset, ref seen, out errorOffset);
        }

        if (AsciiString.Equals(key, "app.name"u8))
        {
            return ParseRequiredField(
                value,
                valueOffset,
                AppNameSeen,
                NativeManifestParseStatus.MissingAppName,
                NativeManifestParseStatus.AppNameTooLong,
                MaximumAppNameLength,
                ref seen,
                ref appName,
                out errorOffset);
        }

        if (AsciiString.Equals(key, "app.path"u8))
        {
            return ParseRequiredField(
                value,
                valueOffset,
                AppPathSeen,
                NativeManifestParseStatus.MissingAppPath,
                NativeManifestParseStatus.AppPathTooLong,
                MaximumAppPathLength,
                ref seen,
                ref appPath,
                out errorOffset);
        }

        if (AsciiString.Equals(key, "args"u8))
        {
            if (HasSeen(seen, ArgsSeen))
            {
                errorOffset = lineOffset;
                return NativeManifestParseStatus.DuplicateKey;
            }

            if (value.Length > MaximumArgsLength)
            {
                errorOffset = valueOffset;
                return NativeManifestParseStatus.ArgsTooLong;
            }

            seen |= ArgsSeen;
            args = new NativeManifestField(valueOffset, value.Length);
            return NativeManifestParseStatus.Success;
        }

        if (AsciiString.Equals(key, "app.kind"u8))
        {
            if (HasSeen(seen, AppKindSeen))
            {
                errorOffset = lineOffset;
                return NativeManifestParseStatus.DuplicateKey;
            }

            if (!TryParseAppKind(value, out appKind))
            {
                errorOffset = valueOffset;
                return NativeManifestParseStatus.UnsupportedAppKind;
            }

            seen |= AppKindSeen;
            return NativeManifestParseStatus.Success;
        }

        if (AsciiString.Equals(key, "runtime.kind"u8))
        {
            if (HasSeen(seen, RuntimeKindSeen))
            {
                errorOffset = lineOffset;
                return NativeManifestParseStatus.DuplicateKey;
            }

            if (!TryParseRuntimeKind(value, out runtimeKind))
            {
                errorOffset = valueOffset;
                return NativeManifestParseStatus.UnsupportedRuntimeKind;
            }

            seen |= RuntimeKindSeen;
            return NativeManifestParseStatus.Success;
        }

        if (AsciiString.Equals(key, "network"u8))
        {
            if (HasSeen(seen, NetworkSeen))
            {
                errorOffset = lineOffset;
                return NativeManifestParseStatus.DuplicateKey;
            }

            if (!TryParseNetworkMode(value, out networkMode))
            {
                errorOffset = valueOffset;
                return NativeManifestParseStatus.InvalidNetworkMode;
            }

            seen |= NetworkSeen;
            return NativeManifestParseStatus.Success;
        }

        if (AsciiString.Equals(key, "listen"u8))
        {
            return ParseListenEndpoint(
                source,
                valueOffset,
                valueLength,
                lineOffset,
                ref seen,
                ref listenEndpoint,
                out errorOffset);
        }

        if (AsciiString.Equals(key, "log"u8))
        {
            if (HasSeen(seen, LogSeen))
            {
                errorOffset = lineOffset;
                return NativeManifestParseStatus.DuplicateKey;
            }

            if (!TryParseLogMode(value, out logMode))
            {
                errorOffset = valueOffset;
                return NativeManifestParseStatus.InvalidLogMode;
            }

            seen |= LogSeen;
            return NativeManifestParseStatus.Success;
        }

        return NativeManifestParseStatus.UnknownKey;
    }

    private static NativeManifestParseStatus ParseListenEndpoint(
        ReadOnlySpan<byte> source,
        int valueOffset,
        int valueLength,
        int lineOffset,
        ref uint seen,
        ref NativeManifestListenEndpoint listenEndpoint,
        out int errorOffset)
    {
        errorOffset = valueOffset;
        if (HasSeen(seen, ListenSeen))
        {
            errorOffset = lineOffset;
            return NativeManifestParseStatus.DuplicateKey;
        }

        Span<AsciiTokenRange> endpointTokens = stackalloc AsciiTokenRange[3];
        AsciiTokenSplitResult endpointSplit = AsciiTokenSplitter.SplitRange(
            source,
            valueOffset,
            valueLength,
            (byte)':',
            endpointTokens);
        if (!endpointSplit.Succeeded || endpointSplit.TokenCount != 2)
        {
            return NativeManifestParseStatus.InvalidListenEndpoint;
        }

        AsciiTokenRange hostRange = endpointTokens[0];
        AsciiTokenRange portRange = endpointTokens[1];
        ReadOnlySpan<byte> host = source.Slice(hostRange.Start, hostRange.Length);
        ReadOnlySpan<byte> port = source.Slice(portRange.Start, portRange.Length);
        if (!IsValidListenHost(host) || !TryParseListenPort(port, out ushort portNumber))
        {
            return NativeManifestParseStatus.InvalidListenEndpoint;
        }

        listenEndpoint = new NativeManifestListenEndpoint(
            new NativeManifestField(hostRange.Start, hostRange.Length),
            portNumber);
        seen |= ListenSeen;
        return NativeManifestParseStatus.Success;
    }

    private static NativeManifestParseStatus ParseEnvironment(
        ReadOnlySpan<byte> source,
        ReadOnlySpan<byte> key,
        int keyOffset,
        ReadOnlySpan<byte> value,
        int valueOffset,
        ref NativeManifestEnvironmentBlock environment,
        out int errorOffset)
    {
        errorOffset = keyOffset;
        if (!IsValidEnvironmentKey(key))
        {
            return NativeManifestParseStatus.InvalidEnvironmentKey;
        }

        if (key.Length > MaximumEnvironmentKeyLength)
        {
            return NativeManifestParseStatus.EnvironmentKeyTooLong;
        }

        if (value.Length > MaximumEnvironmentValueLength)
        {
            errorOffset = valueOffset;
            return NativeManifestParseStatus.EnvironmentValueTooLong;
        }

        for (int index = 0; index < environment.Count; index++)
        {
            NativeManifestEnvironmentEntry existing = environment.GetAt(index);
            if (existing.TryGetKey(source, out ReadOnlySpan<byte> existingKey)
                && OS.Std.MemoryExtensions.SequenceEqual(existingKey, key))
            {
                return NativeManifestParseStatus.DuplicateEnvironmentKey;
            }
        }

        if (!environment.TryAdd(
            new NativeManifestEnvironmentEntry(
                new NativeManifestField(keyOffset, key.Length),
                new NativeManifestField(valueOffset, value.Length))))
        {
            return NativeManifestParseStatus.TooManyEnvironmentFields;
        }

        return NativeManifestParseStatus.Success;
    }

    private static NativeManifestParseStatus ParseFormat(
        ReadOnlySpan<byte> value,
        int valueOffset,
        ref uint seen,
        out int errorOffset)
    {
        errorOffset = valueOffset;
        if (HasSeen(seen, FormatSeen))
        {
            return NativeManifestParseStatus.DuplicateKey;
        }

        if (!AsciiString.Equals(value, "os-native-manifest"u8))
        {
            return NativeManifestParseStatus.UnsupportedFormat;
        }

        seen |= FormatSeen;
        return NativeManifestParseStatus.Success;
    }

    private static NativeManifestParseStatus ParseVersion(
        ReadOnlySpan<byte> value,
        int valueOffset,
        ref uint seen,
        out int errorOffset)
    {
        errorOffset = valueOffset;
        if (HasSeen(seen, VersionSeen))
        {
            return NativeManifestParseStatus.DuplicateKey;
        }

        if (!AsciiString.Equals(value, "0"u8))
        {
            return NativeManifestParseStatus.UnsupportedVersion;
        }

        seen |= VersionSeen;
        return NativeManifestParseStatus.Success;
    }

    private static NativeManifestParseStatus ParseRequiredField(
        ReadOnlySpan<byte> value,
        int valueOffset,
        uint seenFlag,
        NativeManifestParseStatus missingStatus,
        NativeManifestParseStatus tooLongStatus,
        int maximumLength,
        ref uint seen,
        ref NativeManifestField field,
        out int errorOffset)
    {
        errorOffset = valueOffset;
        if (HasSeen(seen, seenFlag))
        {
            return NativeManifestParseStatus.DuplicateKey;
        }

        if (value.Length == 0)
        {
            return missingStatus;
        }

        if (value.Length > maximumLength)
        {
            return tooLongStatus;
        }

        seen |= seenFlag;
        field = new NativeManifestField(valueOffset, value.Length);
        return NativeManifestParseStatus.Success;
    }

    private static NativeManifestParseStatus ValidateRequiredFields(uint seen)
    {
        if (!HasSeen(seen, FormatSeen))
        {
            return NativeManifestParseStatus.MissingFormat;
        }

        if (!HasSeen(seen, VersionSeen))
        {
            return NativeManifestParseStatus.MissingVersion;
        }

        if (!HasSeen(seen, AppNameSeen))
        {
            return NativeManifestParseStatus.MissingAppName;
        }

        if (!HasSeen(seen, AppPathSeen))
        {
            return NativeManifestParseStatus.MissingAppPath;
        }

        if (!HasSeen(seen, ArgsSeen))
        {
            return NativeManifestParseStatus.MissingArgs;
        }

        if (!HasSeen(seen, NetworkSeen))
        {
            return NativeManifestParseStatus.MissingNetworkMode;
        }

        if (!HasSeen(seen, LogSeen))
        {
            return NativeManifestParseStatus.MissingLogMode;
        }

        return NativeManifestParseStatus.Success;
    }

    private static NativeManifestParseStatus ValidateListenEndpoint(
        uint seen,
        NativeManifestNetworkMode networkMode)
    {
        if (HasSeen(seen, ListenSeen) && networkMode == NativeManifestNetworkMode.None)
        {
            return NativeManifestParseStatus.ListenRequiresNetwork;
        }

        return NativeManifestParseStatus.Success;
    }

    private static bool TryParseAppKind(ReadOnlySpan<byte> value, out NativeManifestAppKind kind)
    {
        if (AsciiString.Equals(value, "package"u8))
        {
            kind = NativeManifestAppKind.Package;
            return true;
        }

        kind = NativeManifestAppKind.None;
        return false;
    }

    private static bool TryParseRuntimeKind(ReadOnlySpan<byte> value, out NativeManifestRuntimeKind kind)
    {
        if (AsciiString.Equals(value, "native-aot"u8))
        {
            kind = NativeManifestRuntimeKind.NativeAot;
            return true;
        }

        kind = NativeManifestRuntimeKind.None;
        return false;
    }

    private static bool TryParseNetworkMode(ReadOnlySpan<byte> value, out NativeManifestNetworkMode mode)
    {
        if (AsciiString.Equals(value, "none"u8))
        {
            mode = NativeManifestNetworkMode.None;
            return true;
        }

        if (AsciiString.Equals(value, "dhcp"u8))
        {
            mode = NativeManifestNetworkMode.Dhcp;
            return true;
        }

        if (AsciiString.Equals(value, "static"u8))
        {
            mode = NativeManifestNetworkMode.Static;
            return true;
        }

        mode = NativeManifestNetworkMode.None;
        return false;
    }

    private static bool TryParseLogMode(ReadOnlySpan<byte> value, out NativeManifestLogMode mode)
    {
        if (AsciiString.Equals(value, "serial"u8))
        {
            mode = NativeManifestLogMode.Serial;
            return true;
        }

        if (AsciiString.Equals(value, "none"u8))
        {
            mode = NativeManifestLogMode.None;
            return true;
        }

        mode = NativeManifestLogMode.None;
        return false;
    }

    private static bool TryParseListenPort(ReadOnlySpan<byte> value, out ushort port)
    {
        port = 0;

        AsciiUInt32ParseResult result = AsciiDecimalParser.ParseUInt32(value);
        if (!result.Succeeded || result.Value == 0 || result.Value > ushort.MaxValue)
        {
            return false;
        }

        port = (ushort)result.Value;
        return true;
    }

    private static bool IsValidListenHost(ReadOnlySpan<byte> host)
    {
        if (host.Length == 0 || host.Length > MaximumListenHostLength)
        {
            return false;
        }

        bool previousWasDot = false;
        for (int index = 0; index < host.Length; index++)
        {
            byte value = host[index];
            if (value == (byte)'.')
            {
                if (index == 0 || index == host.Length - 1 || previousWasDot)
                {
                    return false;
                }

                previousWasDot = true;
                continue;
            }

            previousWasDot = false;
            if (value == (byte)'-')
            {
                if (index == 0 || index == host.Length - 1)
                {
                    return false;
                }

                continue;
            }

            if (!OS.Std.Ascii.IsLetterOrDigit(value))
            {
                return false;
            }
        }

        return true;
    }

    private static bool HasSeen(uint seen, uint flag)
    {
        return (seen & flag) != 0;
    }

    private static bool IsTrailingLineDelimiterToken(
        ReadOnlySpan<byte> source,
        AsciiTokenRange token,
        int tokenIndex,
        int tokenCount)
    {
        return tokenIndex == tokenCount - 1 &&
            token.Length == 0 &&
            token.Start == source.Length &&
            source.Length > 0 &&
            source[source.Length - 1] == (byte)'\n';
    }

    private static NativeManifestParseStatus TrySplitKeyValue(
        ReadOnlySpan<byte> source,
        int lineOffset,
        int lineLength,
        out AsciiTokenRange keyRange,
        out int valueOffset,
        out int valueLength,
        out int errorOffset)
    {
        keyRange = default;
        valueOffset = 0;
        valueLength = 0;
        errorOffset = lineOffset;

        AsciiKeyValueSplitResult keyValueSplit = AsciiKeyValueSplitter.SplitRange(
            source,
            lineOffset,
            lineLength,
            (byte)'=');

        if (!keyValueSplit.Succeeded)
        {
            errorOffset = GetTokenSplitErrorOffset(keyValueSplit, lineOffset);
            return MapTokenSplitFailure(keyValueSplit, NativeManifestParseStatus.MalformedLine);
        }

        keyRange = keyValueSplit.Key;
        valueOffset = keyValueSplit.Value.Start;
        valueLength = keyValueSplit.Value.Length;
        return NativeManifestParseStatus.Success;
    }

    private static NativeManifestParseStatus MapTokenSplitFailure(
        AsciiTokenSplitResult result,
        NativeManifestParseStatus fallbackStatus)
    {
        return result.Status == AsciiTokenSplitStatus.NonAsciiInput
            ? NativeManifestParseStatus.NonAsciiOrControlCharacter
            : fallbackStatus;
    }

    private static NativeManifestParseStatus MapTokenSplitFailure(
        AsciiKeyValueSplitResult result,
        NativeManifestParseStatus fallbackStatus)
    {
        return result.Status == AsciiKeyValueSplitStatus.NonAsciiInput
            ? NativeManifestParseStatus.NonAsciiOrControlCharacter
            : fallbackStatus;
    }

    private static int GetTokenSplitErrorOffset(AsciiTokenSplitResult result, int fallbackOffset)
    {
        return result.InvalidInputIndex >= 0
            ? result.InvalidInputIndex
            : fallbackOffset;
    }

    private static int GetTokenSplitErrorOffset(AsciiKeyValueSplitResult result, int fallbackOffset)
    {
        return result.InvalidInputIndex >= 0
            ? result.InvalidInputIndex
            : fallbackOffset;
    }

    private static bool IsValidEnvironmentKey(ReadOnlySpan<byte> key)
    {
        if (key.Length == 0)
        {
            return false;
        }

        if (!OS.Std.Ascii.IsUpperLetter(key[0]))
        {
            return false;
        }

        for (int index = 1; index < key.Length; index++)
        {
            byte value = key[index];
            if (!OS.Std.Ascii.IsUpperLetter(value) && !OS.Std.Ascii.IsDigit(value) && value != (byte)'_')
            {
                return false;
            }
        }

        return true;
    }

    private static bool IsPrintableAscii(byte value)
    {
        return Ascii.IsPrintable(value);
    }
}
