using OS.Std.Binary;

namespace OS.Manifest;

public readonly struct NativeAppPayloadHeader
{
    public const ushort SupportedVersion = 0;
    public const ushort HeaderSize = 32;
    private static ReadOnlySpan<byte> Magic => "OSAPPv0\0"u8;

    private const int MagicLength = 8;
    private const uint SupportedFlags = 0;
    private const ulong ReservedValue = 0;
    private NativeAppPayloadHeader(ushort version, ushort headerSize, uint flags, ulong entryOffset)
    {
        Version = version;
        HeaderSizeValue = headerSize;
        Flags = flags;
        EntryOffset = entryOffset;
    }

    public ushort Version
    {
        get;
    }
    public ushort HeaderSizeValue
    {
        get;
    }
    public uint Flags
    {
        get;
    }
    public ulong EntryOffset
    {
        get;
    }

    public static NativeAppPayloadHeaderResult Parse(ReadOnlySpan<byte> payload)
    {
        if (payload.Length < HeaderSize)
        {
            return NativeAppPayloadHeaderResult.Fail(NativeAppPayloadHeaderStatus.PayloadTooSmall);
        }

        if (!HasMagic(payload.Slice(0, MagicLength)))
        {
            return NativeAppPayloadHeaderResult.Fail(NativeAppPayloadHeaderStatus.InvalidMagic);
        }

        if (!EndianPrimitives.TryReadUInt16LittleEndian(payload.Slice(8, 2), out ushort version) || !EndianPrimitives.TryReadUInt16LittleEndian(payload.Slice(10, 2), out ushort headerSize) || !EndianPrimitives.TryReadUInt32LittleEndian(payload.Slice(12, 4), out uint flags) || !EndianPrimitives.TryReadUInt64LittleEndian(payload.Slice(16, 8), out ulong entryOffset) || !EndianPrimitives.TryReadUInt64LittleEndian(payload.Slice(24, 8), out ulong reserved))
        {
            return NativeAppPayloadHeaderResult.Fail(NativeAppPayloadHeaderStatus.PayloadTooSmall);
        }

        if (version != SupportedVersion)
        {
            return NativeAppPayloadHeaderResult.Fail(NativeAppPayloadHeaderStatus.UnsupportedVersion);
        }

        if (headerSize != HeaderSize)
        {
            return NativeAppPayloadHeaderResult.Fail(NativeAppPayloadHeaderStatus.InvalidHeaderSize);
        }

        if (flags != SupportedFlags)
        {
            return NativeAppPayloadHeaderResult.Fail(NativeAppPayloadHeaderStatus.UnsupportedFlags);
        }

        if (reserved != ReservedValue)
        {
            return NativeAppPayloadHeaderResult.Fail(NativeAppPayloadHeaderStatus.InvalidReserved);
        }

        if (entryOffset < HeaderSize || entryOffset >= (ulong)payload.Length)
        {
            return NativeAppPayloadHeaderResult.Fail(NativeAppPayloadHeaderStatus.InvalidEntryOffset);
        }

        return NativeAppPayloadHeaderResult.Success(new NativeAppPayloadHeader(version, headerSize, flags, entryOffset));
    }

    private static bool HasMagic(ReadOnlySpan<byte> value)
    {
        return value.Length == MagicLength && OS.Std.MemoryExtensions.SequenceEqual(value, Magic);
    }
}
