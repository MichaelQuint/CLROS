namespace OS.Std.Binary;

public static partial class EndianPrimitives
{
    public static ushort ReadUInt16LittleEndian(ReadOnlySpan<byte> source)
    {
        return (ushort)(source[0] | (source[1] << 8));
    }

    public static ushort ReadUInt16BigEndian(ReadOnlySpan<byte> source)
    {
        return (ushort)((source[0] << 8) | source[1]);
    }

    public static uint ReadUInt32LittleEndian(ReadOnlySpan<byte> source)
    {
        return source[0]
            | ((uint)source[1] << 8)
            | ((uint)source[2] << 16)
            | ((uint)source[3] << 24);
    }

    public static uint ReadUInt32BigEndian(ReadOnlySpan<byte> source)
    {
        return ((uint)source[0] << 24)
            | ((uint)source[1] << 16)
            | ((uint)source[2] << 8)
            | source[3];
    }

    public static ulong ReadUInt64LittleEndian(ReadOnlySpan<byte> source)
    {
        return source[0]
            | ((ulong)source[1] << 8)
            | ((ulong)source[2] << 16)
            | ((ulong)source[3] << 24)
            | ((ulong)source[4] << 32)
            | ((ulong)source[5] << 40)
            | ((ulong)source[6] << 48)
            | ((ulong)source[7] << 56);
    }

    public static ulong ReadUInt64BigEndian(ReadOnlySpan<byte> source)
    {
        return ((ulong)source[0] << 56)
            | ((ulong)source[1] << 48)
            | ((ulong)source[2] << 40)
            | ((ulong)source[3] << 32)
            | ((ulong)source[4] << 24)
            | ((ulong)source[5] << 16)
            | ((ulong)source[6] << 8)
            | source[7];
    }

    public static bool TryReadUInt16LittleEndian(ReadOnlySpan<byte> source, out ushort value)
    {
        if (source.Length < 2)
        {
            value = 0;
            return false;
        }

        value = ReadUInt16LittleEndian(source);
        return true;
    }

    public static bool TryReadUInt16BigEndian(ReadOnlySpan<byte> source, out ushort value)
    {
        if (source.Length < 2)
        {
            value = 0;
            return false;
        }

        value = ReadUInt16BigEndian(source);
        return true;
    }

    public static bool TryReadUInt32LittleEndian(ReadOnlySpan<byte> source, out uint value)
    {
        if (source.Length < 4)
        {
            value = 0;
            return false;
        }

        value = ReadUInt32LittleEndian(source);
        return true;
    }

    public static bool TryReadUInt32BigEndian(ReadOnlySpan<byte> source, out uint value)
    {
        if (source.Length < 4)
        {
            value = 0;
            return false;
        }

        value = ReadUInt32BigEndian(source);
        return true;
    }

    public static bool TryReadUInt64LittleEndian(ReadOnlySpan<byte> source, out ulong value)
    {
        if (source.Length < 8)
        {
            value = 0;
            return false;
        }

        value = ReadUInt64LittleEndian(source);
        return true;
    }

    public static bool TryReadUInt64BigEndian(ReadOnlySpan<byte> source, out ulong value)
    {
        if (source.Length < 8)
        {
            value = 0;
            return false;
        }

        value = ReadUInt64BigEndian(source);
        return true;
    }

    public static void WriteUInt16LittleEndian(Span<byte> destination, ushort value)
    {
        destination[0] = (byte)value;
        destination[1] = (byte)(value >> 8);
    }

    public static void WriteUInt16BigEndian(Span<byte> destination, ushort value)
    {
        destination[0] = (byte)(value >> 8);
        destination[1] = (byte)value;
    }

    public static void WriteUInt32LittleEndian(Span<byte> destination, uint value)
    {
        destination[0] = (byte)value;
        destination[1] = (byte)(value >> 8);
        destination[2] = (byte)(value >> 16);
        destination[3] = (byte)(value >> 24);
    }

    public static void WriteUInt32BigEndian(Span<byte> destination, uint value)
    {
        destination[0] = (byte)(value >> 24);
        destination[1] = (byte)(value >> 16);
        destination[2] = (byte)(value >> 8);
        destination[3] = (byte)value;
    }

    public static void WriteUInt64LittleEndian(Span<byte> destination, ulong value)
    {
        destination[0] = (byte)value;
        destination[1] = (byte)(value >> 8);
        destination[2] = (byte)(value >> 16);
        destination[3] = (byte)(value >> 24);
        destination[4] = (byte)(value >> 32);
        destination[5] = (byte)(value >> 40);
        destination[6] = (byte)(value >> 48);
        destination[7] = (byte)(value >> 56);
    }

    public static void WriteUInt64BigEndian(Span<byte> destination, ulong value)
    {
        destination[0] = (byte)(value >> 56);
        destination[1] = (byte)(value >> 48);
        destination[2] = (byte)(value >> 40);
        destination[3] = (byte)(value >> 32);
        destination[4] = (byte)(value >> 24);
        destination[5] = (byte)(value >> 16);
        destination[6] = (byte)(value >> 8);
        destination[7] = (byte)value;
    }

    public static bool TryWriteUInt16LittleEndian(Span<byte> destination, ushort value)
    {
        if (destination.Length < 2)
        {
            return false;
        }

        WriteUInt16LittleEndian(destination, value);
        return true;
    }

    public static bool TryWriteUInt16BigEndian(Span<byte> destination, ushort value)
    {
        if (destination.Length < 2)
        {
            return false;
        }

        WriteUInt16BigEndian(destination, value);
        return true;
    }

    public static bool TryWriteUInt32LittleEndian(Span<byte> destination, uint value)
    {
        if (destination.Length < 4)
        {
            return false;
        }

        WriteUInt32LittleEndian(destination, value);
        return true;
    }

    public static bool TryWriteUInt32BigEndian(Span<byte> destination, uint value)
    {
        if (destination.Length < 4)
        {
            return false;
        }

        WriteUInt32BigEndian(destination, value);
        return true;
    }

    public static bool TryWriteUInt64LittleEndian(Span<byte> destination, ulong value)
    {
        if (destination.Length < 8)
        {
            return false;
        }

        WriteUInt64LittleEndian(destination, value);
        return true;
    }

    public static bool TryWriteUInt64BigEndian(Span<byte> destination, ulong value)
    {
        if (destination.Length < 8)
        {
            return false;
        }

        WriteUInt64BigEndian(destination, value);
        return true;
    }
}
