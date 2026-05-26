namespace OS.Net;

using OS.Std.Binary;

public readonly struct ArpPacket
{
    public const int PacketLength = 28;
    public const ushort HardwareTypeEthernet = 1;
    public const ushort ProtocolTypeIpv4 = EthernetHeader.EtherTypeIpv4;
    public const byte HardwareAddressLengthEthernet = MacAddress.Length;
    public const byte ProtocolAddressLengthIpv4 = 4;
    public const ushort OperationRequest = 1;
    public const ushort OperationReply = 2;

    public ArpPacket(
        ushort operation,
        MacAddress senderMac,
        Ipv4Address senderAddress,
        MacAddress targetMac,
        Ipv4Address targetAddress)
    {
        Operation = operation;
        SenderMac = senderMac;
        SenderAddress = senderAddress;
        TargetMac = targetMac;
        TargetAddress = targetAddress;
    }

    public ushort Operation { get; }

    public MacAddress SenderMac { get; }

    public Ipv4Address SenderAddress { get; }

    public MacAddress TargetMac { get; }

    public Ipv4Address TargetAddress { get; }

    public static PacketParseStatus TryParse(ReadOnlySpan<byte> packet, out ArpPacket arp)
    {
        if (packet.Length < PacketLength)
        {
            arp = default;
            return PacketParseStatus.BufferTooSmall;
        }

        ushort hardwareType = EndianPrimitives.ReadUInt16BigEndian(packet.Slice(0, 2));
        if (hardwareType != HardwareTypeEthernet)
        {
            arp = default;
            return PacketParseStatus.UnsupportedHardwareType;
        }

        ushort protocolType = EndianPrimitives.ReadUInt16BigEndian(packet.Slice(2, 2));
        if (protocolType != ProtocolTypeIpv4)
        {
            arp = default;
            return PacketParseStatus.UnsupportedProtocolType;
        }

        if (packet[4] != HardwareAddressLengthEthernet)
        {
            arp = default;
            return PacketParseStatus.UnsupportedHardwareAddressLength;
        }

        if (packet[5] != ProtocolAddressLengthIpv4)
        {
            arp = default;
            return PacketParseStatus.UnsupportedProtocolAddressLength;
        }

        ushort operation = EndianPrimitives.ReadUInt16BigEndian(packet.Slice(6, 2));
        if (!IsSupportedOperation(operation))
        {
            arp = default;
            return PacketParseStatus.UnsupportedOperation;
        }

        PacketParseStatus senderMacStatus = MacAddress.TryRead(packet, 8, out MacAddress senderMac);
        PacketParseStatus senderAddressStatus = Ipv4Address.TryRead(packet, 14, out Ipv4Address senderAddress);
        PacketParseStatus targetMacStatus = MacAddress.TryRead(packet, 18, out MacAddress targetMac);
        PacketParseStatus targetAddressStatus = Ipv4Address.TryRead(packet, 24, out Ipv4Address targetAddress);
        if (senderMacStatus != PacketParseStatus.Success
            || senderAddressStatus != PacketParseStatus.Success
            || targetMacStatus != PacketParseStatus.Success
            || targetAddressStatus != PacketParseStatus.Success)
        {
            arp = default;
            return PacketParseStatus.BufferTooSmall;
        }

        arp = new ArpPacket(operation, senderMac, senderAddress, targetMac, targetAddress);
        return PacketParseStatus.Success;
    }

    public static PacketParseStatus TryWrite(Span<byte> packet, in ArpPacket arp, out int bytesWritten)
    {
        bytesWritten = 0;

        if (!IsSupportedOperation(arp.Operation))
        {
            return PacketParseStatus.UnsupportedOperation;
        }

        if (packet.Length < PacketLength)
        {
            return PacketParseStatus.BufferTooSmall;
        }

        EndianPrimitives.WriteUInt16BigEndian(packet.Slice(0, 2), HardwareTypeEthernet);
        EndianPrimitives.WriteUInt16BigEndian(packet.Slice(2, 2), ProtocolTypeIpv4);
        packet[4] = HardwareAddressLengthEthernet;
        packet[5] = ProtocolAddressLengthIpv4;
        EndianPrimitives.WriteUInt16BigEndian(packet.Slice(6, 2), arp.Operation);
        arp.SenderMac.TryWrite(packet, 8);
        arp.SenderAddress.TryWrite(packet, 14);
        arp.TargetMac.TryWrite(packet, 18);
        arp.TargetAddress.TryWrite(packet, 24);

        bytesWritten = PacketLength;
        return PacketParseStatus.Success;
    }

    private static bool IsSupportedOperation(ushort operation)
    {
        return operation == OperationRequest || operation == OperationReply;
    }
}
