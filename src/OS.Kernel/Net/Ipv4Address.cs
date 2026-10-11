namespace OS.Net;

using OS.Std.Binary;

public readonly struct Ipv4Address
#if !NOSTDLIB_EFI
    : IEquatable<Ipv4Address>
#endif
{
    private readonly uint _wireValue;

    public Ipv4Address(byte octet0, byte octet1, byte octet2, byte octet3)
    {
        _wireValue = ((uint)octet0 << 24)
            | ((uint)octet1 << 16)
            | ((uint)octet2 << 8)
            | octet3;
    }

    private Ipv4Address(uint wireValue)
    {
        _wireValue = wireValue;
    }

    public byte Octet0 => (byte)(_wireValue >> 24);

    public byte Octet1 => (byte)(_wireValue >> 16);

    public byte Octet2 => (byte)(_wireValue >> 8);

    public byte Octet3 => (byte)_wireValue;

    public static PacketParseStatus TryRead(ReadOnlySpan<byte> buffer, int offset, out Ipv4Address address)
    {
        if (buffer.Length - offset < 4)
        {
            address = default;
            return PacketParseStatus.BufferTooSmall;
        }

        address = new Ipv4Address(EndianPrimitives.ReadUInt32BigEndian(buffer.Slice(offset, 4)));
        return PacketParseStatus.Success;
    }

    public PacketParseStatus TryWrite(Span<byte> buffer, int offset)
    {
        if (buffer.Length - offset < 4)
        {
            return PacketParseStatus.BufferTooSmall;
        }

        EndianPrimitives.WriteUInt32BigEndian(buffer.Slice(offset, 4), _wireValue);
        return PacketParseStatus.Success;
    }

    public bool Equals(Ipv4Address other)
    {
        return _wireValue == other._wireValue;
    }

#if !NOSTDLIB_EFI
    public override bool Equals(object? obj)
    {
        return obj is Ipv4Address other && Equals(other);
    }

    public override int GetHashCode()
    {
        return _wireValue.GetHashCode();
    }
#endif

    public static bool operator ==(Ipv4Address left, Ipv4Address right)
    {
        return left.Equals(right);
    }

    public static bool operator !=(Ipv4Address left, Ipv4Address right)
    {
        return !left.Equals(right);
    }
}
