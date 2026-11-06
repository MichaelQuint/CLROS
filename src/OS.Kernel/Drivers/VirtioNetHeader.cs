namespace OS.Drivers;

using OS.Net;
using OS.Std.Binary;

public readonly struct VirtioNetHeader
{
    public const int HeaderLengthWithoutMergeableBuffers = 10;
    public const int HeaderLengthWithMergeableBuffers = 12;
    public const int ModernHeaderLength = 12;
    public const byte FlagNeedsChecksum = 0x01;
    public const byte FlagDataValid = 0x02;
    public const byte FlagRscInfo = 0x04;
    public const byte GsoTypeNone = 0x00;
    public const byte GsoTypeTcpV4 = 0x01;
    public const byte GsoTypeUdp = 0x03;
    public const byte GsoTypeTcpV6 = 0x04;
    public const byte GsoTypeEcn = 0x80;
    public const byte GsoTypeMask = 0x7F;
    public VirtioNetHeader(byte flags, byte gsoType, ushort headerLength, ushort gsoSize, ushort checksumStart, ushort checksumOffset)
    {
        Flags = flags;
        GsoType = gsoType;
        HeaderLength = headerLength;
        GsoSize = gsoSize;
        ChecksumStart = checksumStart;
        ChecksumOffset = checksumOffset;
    }

    public byte Flags
    {
        get;
    }
    public byte GsoType
    {
        get;
    }
    public ushort HeaderLength
    {
        get;
    }
    public ushort GsoSize
    {
        get;
    }
    public ushort ChecksumStart
    {
        get;
    }
    public ushort ChecksumOffset
    {
        get;
    }

    public VirtioNetHeaderStatus ValidateNoOffload()
    {
        if ((Flags & FlagNeedsChecksum) != 0 || ChecksumStart != 0 || ChecksumOffset != 0)
        {
            return VirtioNetHeaderStatus.UnsupportedChecksumOffload;
        }

        if (Flags != 0)
        {
            return VirtioNetHeaderStatus.UnsupportedOffloadFlags;
        }

        if ((GsoType & GsoTypeMask) != GsoTypeNone || (GsoType & GsoTypeEcn) != 0 || HeaderLength != 0 || GsoSize != 0)
        {
            return VirtioNetHeaderStatus.UnsupportedGenericSegmentationOffload;
        }

        return VirtioNetHeaderStatus.Success;
    }

    public static VirtioNetHeaderParseResult TryParse(ReadOnlySpan<byte> source)
    {
        if (source.Length < HeaderLengthWithoutMergeableBuffers)
        {
            return VirtioNetHeaderParseResult.Fail(VirtioNetHeaderStatus.BufferTooSmall, default, source.Length);
        }

        VirtioNetHeader header = new VirtioNetHeader(source[0], source[1], EndianPrimitives.ReadUInt16LittleEndian(source.Slice(2, sizeof(ushort))), EndianPrimitives.ReadUInt16LittleEndian(source.Slice(4, sizeof(ushort))), EndianPrimitives.ReadUInt16LittleEndian(source.Slice(6, sizeof(ushort))), EndianPrimitives.ReadUInt16LittleEndian(source.Slice(8, sizeof(ushort))));
        return VirtioNetHeaderParseResult.Success(header, HeaderLengthWithoutMergeableBuffers);
    }

    public static VirtioNetReceivedFrameParseResult TryParseReceivedFrameNoOffload(ReadOnlySpan<byte> source) => ParseReceivedFrame(source, HeaderLengthWithoutMergeableBuffers);
    /// <summary>VERSION_1 RX uses a 12-byte header even without MRG_RXBUF.
    /// This bounded parser requires exactly one device-reported buffer.</summary>

    public static VirtioNetReceivedFrameParseResult TryParseModernReceivedFrameNoOffload(ReadOnlySpan<byte> source) => ParseReceivedFrame(source, ModernHeaderLength);
    private static VirtioNetReceivedFrameParseResult ParseReceivedFrame(ReadOnlySpan<byte> source, int headerLength)
    {
        if (source.Length < headerLength)
            return VirtioNetReceivedFrameParseResult.Fail(VirtioNetHeaderStatus.BufferTooSmall, default);
        VirtioNetHeaderParseResult headerResult = TryParse(source);
        if (!headerResult.Succeeded)
        {
            return VirtioNetReceivedFrameParseResult.Fail(headerResult.Status, headerResult.Header);
        }

        if (source.Length < headerLength + EthernetHeader.HeaderLength)
        {
            return VirtioNetReceivedFrameParseResult.Fail(VirtioNetHeaderStatus.MissingEthernetHeader, headerResult.Header);
        }

        VirtioNetHeaderStatus offloadStatus = headerResult.Header.ValidateNoOffload();
        if (offloadStatus != VirtioNetHeaderStatus.Success)
        {
            return VirtioNetReceivedFrameParseResult.Fail(offloadStatus, headerResult.Header);
        }

        if (headerLength == ModernHeaderLength && EndianPrimitives.ReadUInt16LittleEndian(source.Slice(10, sizeof(ushort))) != 1)
            return VirtioNetReceivedFrameParseResult.Fail(VirtioNetHeaderStatus.UnsupportedReceiveBufferCount, headerResult.Header);
        ReadOnlySpan<byte> ethernetFrame = source.Slice(headerLength);
        PacketParseStatus ethernetStatus = EthernetHeader.TryParse(ethernetFrame, out EthernetHeader ethernetHeader, out ReadOnlySpan<byte> ethernetPayload);
        if (ethernetStatus != PacketParseStatus.Success)
        {
            return VirtioNetReceivedFrameParseResult.Fail(VirtioNetHeaderStatus.MissingEthernetHeader, headerResult.Header);
        }

        VirtioNetReceivedFrameMetadata metadata = new VirtioNetReceivedFrameMetadata(headerResult.Header, ethernetHeader, headerLength, headerLength, ethernetFrame.Length, headerLength + EthernetHeader.HeaderLength, ethernetPayload.Length);
        return VirtioNetReceivedFrameParseResult.Success(headerResult.Header, metadata);
    }

    public static VirtioNetHeaderStatus TryWriteNoOffloadTxHeader(Span<byte> destination, out int bytesWritten)
    {
        if (destination.Length < HeaderLengthWithoutMergeableBuffers)
        {
            bytesWritten = 0;
            return VirtioNetHeaderStatus.BufferTooSmall;
        }

        destination.Slice(0, HeaderLengthWithoutMergeableBuffers).Clear();
        bytesWritten = HeaderLengthWithoutMergeableBuffers;
        return VirtioNetHeaderStatus.Success;
    }
}
