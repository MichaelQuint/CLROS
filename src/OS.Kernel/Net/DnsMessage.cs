namespace OS.Net;
using OS.Std.Binary;

public static class DnsMessage
{
    public const int HeaderLength = 12;
    public const int MaxLabelLength = 63;
    public const int MaxNameLength = 255;
    public const ushort TypeA = 1;
    public const ushort ClassIn = 1;
    public const ushort StandardQueryFlags = 0x0100;
    public const ushort ResponseFlag = 0x8000;
    public const ushort OpcodeMask = 0x7800;
    public const ushort TruncatedFlag = 0x0200;
    public const ushort ResponseCodeMask = 0x000F;
    public const ushort ResponseCodeNoError = 0;
    private const int QuestionTrailerLength = 4;
    private const int ResourceRecordTrailerLength = 10;
    private const int MaxCompressionJumps = 16;
    public static DnsMessageStatus TryWriteQuery(Span<byte> buffer, ushort transactionId, ReadOnlySpan<char> hostName, out int bytesWritten)
    {
        bytesWritten = 0;
        DnsMessageStatus nameStatus = TryGetEncodedNameLength(hostName, out int encodedNameLength);
        if (nameStatus != DnsMessageStatus.Success)
        {
            return nameStatus;
        }

        int messageLength = HeaderLength + encodedNameLength + QuestionTrailerLength;
        if (buffer.Length < messageLength)
        {
            return DnsMessageStatus.BufferTooSmall;
        }

        EndianPrimitives.WriteUInt16BigEndian(buffer.Slice(0, 2), transactionId);
        EndianPrimitives.WriteUInt16BigEndian(buffer.Slice(2, 2), StandardQueryFlags);
        EndianPrimitives.WriteUInt16BigEndian(buffer.Slice(4, 2), 1);
        EndianPrimitives.WriteUInt16BigEndian(buffer.Slice(6, 2), 0);
        EndianPrimitives.WriteUInt16BigEndian(buffer.Slice(8, 2), 0);
        EndianPrimitives.WriteUInt16BigEndian(buffer.Slice(10, 2), 0);
        int offset = HeaderLength;
        DnsMessageStatus writeNameStatus = TryWriteName(buffer, hostName, offset, out int nameBytesWritten);
        if (writeNameStatus != DnsMessageStatus.Success)
        {
            return writeNameStatus;
        }

        offset += nameBytesWritten;
        EndianPrimitives.WriteUInt16BigEndian(buffer.Slice(offset, 2), TypeA);
        EndianPrimitives.WriteUInt16BigEndian(buffer.Slice(offset + 2, 2), ClassIn);
        bytesWritten = messageLength;
        return DnsMessageStatus.Success;
    }

    public static DnsMessageStatus TryParseAResponse(ReadOnlySpan<byte> message, ushort expectedTransactionId, out Ipv4Address address)
    {
        address = default;
        if (message.Length < HeaderLength)
        {
            return DnsMessageStatus.BufferTooSmall;
        }

        ushort transactionId = EndianPrimitives.ReadUInt16BigEndian(message.Slice(0, 2));
        if (transactionId != expectedTransactionId)
        {
            return DnsMessageStatus.TransactionIdMismatch;
        }

        ushort flags = EndianPrimitives.ReadUInt16BigEndian(message.Slice(2, 2));
        if ((flags & ResponseFlag) == 0)
        {
            return DnsMessageStatus.NotResponse;
        }

        if ((flags & OpcodeMask) != 0)
        {
            return DnsMessageStatus.UnsupportedOpcode;
        }

        if ((flags & TruncatedFlag) != 0)
        {
            return DnsMessageStatus.TruncatedResponse;
        }

        if ((flags & ResponseCodeMask) != ResponseCodeNoError)
        {
            return DnsMessageStatus.ResponseCodeFailure;
        }

        ushort questionCount = EndianPrimitives.ReadUInt16BigEndian(message.Slice(4, 2));
        ushort answerCount = EndianPrimitives.ReadUInt16BigEndian(message.Slice(6, 2));
        if (questionCount != 1)
        {
            return DnsMessageStatus.UnsupportedQuestionCount;
        }

        if (answerCount == 0)
        {
            return DnsMessageStatus.NoAnswer;
        }

        DnsMessageStatus questionNameStatus = TrySkipName(message, HeaderLength, out int offset);
        if (questionNameStatus != DnsMessageStatus.Success)
        {
            return MapNameStatus(questionNameStatus, DnsMessageStatus.TruncatedQuestion);
        }

        if (message.Length - offset < QuestionTrailerLength)
        {
            return DnsMessageStatus.TruncatedQuestion;
        }

        ushort questionType = EndianPrimitives.ReadUInt16BigEndian(message.Slice(offset, 2));
        ushort questionClass = EndianPrimitives.ReadUInt16BigEndian(message.Slice(offset + 2, 2));
        if (questionType != TypeA)
        {
            return DnsMessageStatus.UnsupportedType;
        }

        if (questionClass != ClassIn)
        {
            return DnsMessageStatus.UnsupportedClass;
        }

        offset += QuestionTrailerLength;
        DnsMessageStatus answerNameStatus = TrySkipName(message, offset, out offset);
        if (answerNameStatus != DnsMessageStatus.Success)
        {
            return MapNameStatus(answerNameStatus, DnsMessageStatus.TruncatedAnswer);
        }

        if (message.Length - offset < ResourceRecordTrailerLength)
        {
            return DnsMessageStatus.TruncatedAnswer;
        }

        ushort type = EndianPrimitives.ReadUInt16BigEndian(message.Slice(offset, 2));
        ushort recordClass = EndianPrimitives.ReadUInt16BigEndian(message.Slice(offset + 2, 2));
        ushort dataLength = EndianPrimitives.ReadUInt16BigEndian(message.Slice(offset + 8, 2));
        offset += ResourceRecordTrailerLength;
        if (message.Length - offset < dataLength)
        {
            return DnsMessageStatus.TruncatedAnswer;
        }

        if (type != TypeA)
        {
            return DnsMessageStatus.UnsupportedType;
        }

        if (recordClass != ClassIn)
        {
            return DnsMessageStatus.UnsupportedClass;
        }

        if (dataLength != 4)
        {
            return DnsMessageStatus.InvalidLength;
        }

        PacketParseStatus addressStatus = Ipv4Address.TryRead(message, offset, out address);
        if (addressStatus != PacketParseStatus.Success)
        {
            address = default;
            return DnsMessageStatus.TruncatedAnswer;
        }

        return DnsMessageStatus.Success;
    }

    private static DnsMessageStatus TryGetEncodedNameLength(ReadOnlySpan<char> hostName, out int encodedNameLength)
    {
        encodedNameLength = 0;
        if (hostName.Length == 0)
        {
            return DnsMessageStatus.InvalidLabel;
        }

        int hostEnd = hostName[^1] == '.' ? hostName.Length - 1 : hostName.Length;
        if (hostEnd == 0)
        {
            return DnsMessageStatus.InvalidLabel;
        }

        int labelLength = 0;
        int labelStart = 0;
        int wireLength = 1;
        for (int index = 0; index < hostEnd; index++)
        {
            char value = hostName[index];
            if (value == '.')
            {
                if (labelLength == 0 || hostName[index - 1] == '-')
                {
                    return DnsMessageStatus.InvalidLabel;
                }

                wireLength += labelLength + 1;
                labelLength = 0;
                labelStart = index + 1;
                continue;
            }

            if (!IsHostNameChar(value))
            {
                return DnsMessageStatus.InvalidLabel;
            }

            if (labelLength == 0 && value == '-')
            {
                return DnsMessageStatus.InvalidLabel;
            }

            labelLength++;
            if (labelLength > MaxLabelLength)
            {
                return DnsMessageStatus.InvalidLabel;
            }
        }

        if (labelLength == 0 || hostName[hostEnd - 1] == '-')
        {
            return DnsMessageStatus.InvalidLabel;
        }

        wireLength += labelLength + 1;
        if (wireLength > MaxNameLength || labelStart >= hostEnd)
        {
            return DnsMessageStatus.InvalidLabel;
        }

        encodedNameLength = wireLength;
        return DnsMessageStatus.Success;
    }

    private static DnsMessageStatus TryWriteName(Span<byte> buffer, ReadOnlySpan<char> hostName, int offset, out int bytesWritten)
    {
        bytesWritten = 0;
        int hostEnd = hostName[^1] == '.' ? hostName.Length - 1 : hostName.Length;
        int cursor = offset;
        int labelLengthOffset = cursor;
        int labelLength = 0;
        cursor++;
        for (int index = 0; index < hostEnd; index++)
        {
            char value = hostName[index];
            if (value == '.')
            {
                buffer[labelLengthOffset] = (byte)labelLength;
                labelLengthOffset = cursor;
                labelLength = 0;
                cursor++;
                continue;
            }

            buffer[cursor] = (byte)value;
            cursor++;
            labelLength++;
        }

        buffer[labelLengthOffset] = (byte)labelLength;
        buffer[cursor] = 0;
        cursor++;
        bytesWritten = cursor - offset;
        return DnsMessageStatus.Success;
    }

    private static DnsMessageStatus TrySkipName(ReadOnlySpan<byte> message, int offset, out int nextOffset)
    {
        nextOffset = offset;
        if ((uint)offset >= (uint)message.Length)
        {
            return DnsMessageStatus.BufferTooSmall;
        }

        int cursor = offset;
        int consumedEnd = offset;
        int expandedLength = 0;
        int jumpCount = 0;
        bool jumped = false;
        while (true)
        {
            if ((uint)cursor >= (uint)message.Length)
            {
                return DnsMessageStatus.BufferTooSmall;
            }

            byte length = message[cursor];
            byte labelKind = (byte)(length & 0xC0);
            if (labelKind == 0)
            {
                cursor++;
                if (length == 0)
                {
                    expandedLength++;
                    if (expandedLength > MaxNameLength)
                    {
                        return DnsMessageStatus.InvalidLabel;
                    }

                    if (!jumped)
                    {
                        consumedEnd = cursor;
                    }

                    nextOffset = consumedEnd;
                    return DnsMessageStatus.Success;
                }

                if (length > MaxLabelLength || message.Length - cursor < length)
                {
                    return DnsMessageStatus.BufferTooSmall;
                }

                if (!IsWireLabelValid(message.Slice(cursor, length)))
                {
                    return DnsMessageStatus.InvalidLabel;
                }

                expandedLength += length + 1;
                if (expandedLength > MaxNameLength)
                {
                    return DnsMessageStatus.InvalidLabel;
                }

                cursor += length;
                if (!jumped)
                {
                    consumedEnd = cursor;
                }

                continue;
            }

            if (labelKind == 0xC0)
            {
                if (message.Length - cursor < 2)
                {
                    return DnsMessageStatus.BufferTooSmall;
                }

                int pointerOffset = ((length & 0x3F) << 8) | message[cursor + 1];
                if ((uint)pointerOffset >= (uint)message.Length)
                {
                    return DnsMessageStatus.CompressionPointerOutOfRange;
                }

                if (pointerOffset >= cursor)
                {
                    return DnsMessageStatus.CompressionPointerLoop;
                }

                if (!jumped)
                {
                    consumedEnd = cursor + 2;
                    jumped = true;
                }

                jumpCount++;
                if (jumpCount > MaxCompressionJumps)
                {
                    return DnsMessageStatus.CompressionPointerLoop;
                }

                cursor = pointerOffset;
                continue;
            }

            return DnsMessageStatus.InvalidLabel;
        }
    }

    private static DnsMessageStatus MapNameStatus(DnsMessageStatus status, DnsMessageStatus truncatedStatus)
    {
        return status == DnsMessageStatus.BufferTooSmall ? truncatedStatus : status;
    }

    private static bool IsWireLabelValid(ReadOnlySpan<byte> label)
    {
        if (label.Length == 0 || label[0] == '-' || label[^1] == '-')
        {
            return false;
        }

        for (int index = 0; index < label.Length; index++)
        {
            if (!IsHostNameByte(label[index]))
            {
                return false;
            }
        }

        return true;
    }

    private static bool IsHostNameChar(char value)
    {
        return value <= 0x7F && IsHostNameByte((byte)value);
    }

    private static bool IsHostNameByte(byte value)
    {
        return OS.Std.Ascii.IsLetterOrDigit(value) || value == (byte)'-';
    }
}
