namespace OS.Net;

public readonly struct MacAddress
#if !NOSTDLIB_EFI
    : IEquatable<MacAddress>
#endif
{
    public const int Length = 6;

    public MacAddress(byte octet0, byte octet1, byte octet2, byte octet3, byte octet4, byte octet5)
    {
        Octet0 = octet0;
        Octet1 = octet1;
        Octet2 = octet2;
        Octet3 = octet3;
        Octet4 = octet4;
        Octet5 = octet5;
    }

    public byte Octet0 { get; }

    public byte Octet1 { get; }

    public byte Octet2 { get; }

    public byte Octet3 { get; }

    public byte Octet4 { get; }

    public byte Octet5 { get; }

    public bool IsZero =>
        Octet0 == 0 &&
        Octet1 == 0 &&
        Octet2 == 0 &&
        Octet3 == 0 &&
        Octet4 == 0 &&
        Octet5 == 0;

    public bool IsBroadcast =>
        Octet0 == 0xFF &&
        Octet1 == 0xFF &&
        Octet2 == 0xFF &&
        Octet3 == 0xFF &&
        Octet4 == 0xFF &&
        Octet5 == 0xFF;

    public static PacketParseStatus TryRead(ReadOnlySpan<byte> buffer, int offset, out MacAddress address)
    {
        if (buffer.Length - offset < Length)
        {
            address = default;
            return PacketParseStatus.BufferTooSmall;
        }

        address = new MacAddress(
            buffer[offset],
            buffer[offset + 1],
            buffer[offset + 2],
            buffer[offset + 3],
            buffer[offset + 4],
            buffer[offset + 5]);
        return PacketParseStatus.Success;
    }

    public PacketParseStatus TryWrite(Span<byte> buffer, int offset)
    {
        if (buffer.Length - offset < Length)
        {
            return PacketParseStatus.BufferTooSmall;
        }

        buffer[offset] = Octet0;
        buffer[offset + 1] = Octet1;
        buffer[offset + 2] = Octet2;
        buffer[offset + 3] = Octet3;
        buffer[offset + 4] = Octet4;
        buffer[offset + 5] = Octet5;
        return PacketParseStatus.Success;
    }

    public bool Equals(MacAddress other)
    {
        return Octet0 == other.Octet0
            && Octet1 == other.Octet1
            && Octet2 == other.Octet2
            && Octet3 == other.Octet3
            && Octet4 == other.Octet4
            && Octet5 == other.Octet5;
    }

#if !NOSTDLIB_EFI
    public override bool Equals(object? obj)
    {
        return obj is MacAddress other && Equals(other);
    }

    public override int GetHashCode()
    {
        HashCode hash = new HashCode();
        hash.Add(Octet0);
        hash.Add(Octet1);
        hash.Add(Octet2);
        hash.Add(Octet3);
        hash.Add(Octet4);
        hash.Add(Octet5);
        return hash.ToHashCode();
    }
#endif

    public static bool operator ==(MacAddress left, MacAddress right)
    {
        return left.Equals(right);
    }

    public static bool operator !=(MacAddress left, MacAddress right)
    {
        return !left.Equals(right);
    }
}
