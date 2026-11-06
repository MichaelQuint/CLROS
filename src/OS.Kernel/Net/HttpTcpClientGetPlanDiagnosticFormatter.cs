using OS.Std.Text;

namespace OS.Net;

public static class HttpTcpClientGetPlanDiagnosticFormatter
{
    public static HttpTcpClientGetPlanDiagnosticFormatResult FormatResult(in HttpTcpClientGetPlanResult result, Span<byte> destination)
    {
        HttpTcpClientGetPlanDiagnosticFormatStatus validationStatus = Validate(in result);
        if (validationStatus != HttpTcpClientGetPlanDiagnosticFormatStatus.Success)
        {
            return HttpTcpClientGetPlanDiagnosticFormatResult.Fail(validationStatus, result.Status, requiredBytes: 0, destination.Length);
        }

        int requiredBytes = GetRequiredBytes(in result);
        if (destination.Length < requiredBytes)
        {
            return HttpTcpClientGetPlanDiagnosticFormatResult.Fail(HttpTcpClientGetPlanDiagnosticFormatStatus.DestinationTooSmall, result.Status, requiredBytes, destination.Length);
        }

        int offset = 0;
        if (!TryAppendBytes("HTTP_TCP_GET_PLAN status="u8, destination, ref offset) || !TryAppendBytes(GetPlanStatusName(result.Status), destination, ref offset) || !TryAppendBytes(" state="u8, destination, ref offset) || !TryAppendBytes(GetStateName(result.State), destination, ref offset) || !TryAppendBytes(" tcpFrame="u8, destination, ref offset) || !TryAppendBytes(GetTcpFrameStatusName(result.TcpFrameStatus), destination, ref offset) || !TryAppendBytes(" tcp="u8, destination, ref offset) || !TryAppendBytes(GetTcpStatusName(result.TcpStatus), destination, ref offset) || !TryAppendBytes(" stream="u8, destination, ref offset) || !TryAppendBytes(GetStreamStatusName(result.StreamStatus), destination, ref offset) || !TryAppendBytes(" http="u8, destination, ref offset) || !TryAppendBytes(GetHttpStatusName(result.HttpStatus), destination, ref offset) || !TryAppendBytes(" request="u8, destination, ref offset) || !TryAppendBytes(GetRequestStatusName(result.RequestStatus), destination, ref offset) || !TryAppendBytes(" response="u8, destination, ref offset) || !TryAppendBytes(GetResponseStatusName(result.ResponseStatus), destination, ref offset) || !TryAppendBytes(" packet="u8, destination, ref offset) || !TryAppendBytes(GetPacketStatusName(result.PacketStatus), destination, ref offset) || !TryAppendBytes(" bytes="u8, destination, ref offset) || !TryAppendInt64Decimal(result.BytesWritten, destination, ref offset) || !TryAppendBytes(" tcpSegmentOffset="u8, destination, ref offset) || !TryAppendInt64Decimal(result.TcpSegmentOffset, destination, ref offset) || !TryAppendBytes(" tcpSegmentBytes="u8, destination, ref offset) || !TryAppendInt64Decimal(result.TcpSegmentLength, destination, ref offset) || !TryAppendBytes(" requestBytes="u8, destination, ref offset) || !TryAppendInt64Decimal(result.RequestBytesWritten, destination, ref offset) || !TryAppendBytes(" responseBytes="u8, destination, ref offset) || !TryAppendInt64Decimal(result.ResponseBytesRead, destination, ref offset) || !TryAppendBytes(" httpStatus="u8, destination, ref offset) || !TryAppendUInt64Decimal(result.HttpStatusCode, destination, ref offset) || !TryAppendBytes(" bodyOffset="u8, destination, ref offset) || !TryAppendInt64Decimal(result.HttpBodyOffset, destination, ref offset) || !TryAppendBytes(" bodyBytes="u8, destination, ref offset) || !TryAppendInt64Decimal(result.HttpBodyLength, destination, ref offset) || !TryAppendBytes(" localSeq="u8, destination, ref offset) || !TryAppendUInt64Decimal(result.LocalNextSequenceNumber, destination, ref offset) || !TryAppendBytes(" remoteSeq="u8, destination, ref offset) || !TryAppendUInt64Decimal(result.RemoteNextSequenceNumber, destination, ref offset))
        {
            return HttpTcpClientGetPlanDiagnosticFormatResult.Fail(HttpTcpClientGetPlanDiagnosticFormatStatus.DestinationTooSmall, result.Status, requiredBytes, destination.Length);
        }

        return new HttpTcpClientGetPlanDiagnosticFormatResult(HttpTcpClientGetPlanDiagnosticFormatStatus.Success, result.Status, bytesWritten: offset, requiredBytes, availableBytes: destination.Length);
    }

    public static HttpTcpClientGetPlanDiagnosticFormatResult QueryRequiredBytes(in HttpTcpClientGetPlanResult result)
    {
        HttpTcpClientGetPlanDiagnosticFormatStatus validationStatus = Validate(in result);
        if (validationStatus != HttpTcpClientGetPlanDiagnosticFormatStatus.Success)
        {
            return HttpTcpClientGetPlanDiagnosticFormatResult.Fail(validationStatus, result.Status, requiredBytes: 0, availableBytes: 0);
        }

        return new HttpTcpClientGetPlanDiagnosticFormatResult(HttpTcpClientGetPlanDiagnosticFormatStatus.Success, result.Status, bytesWritten: 0, requiredBytes: GetRequiredBytes(in result), availableBytes: 0);
    }

    private static HttpTcpClientGetPlanDiagnosticFormatStatus Validate(in HttpTcpClientGetPlanResult result)
    {
        if (!result.IsInitialized || !IsKnownPlanStatus(result.Status) || !IsKnownState(result.State) || !IsKnownTcpFrameStatus(result.TcpFrameStatus) || !IsKnownTcpStatus(result.TcpStatus) || !IsKnownStreamStatus(result.StreamStatus) || !IsKnownHttpStatus(result.HttpStatus) || !IsKnownRequestStatus(result.RequestStatus) || !IsKnownResponseStatus(result.ResponseStatus) || !IsKnownPacketStatus(result.PacketStatus) || result.BytesWritten < 0 || result.TcpSegmentOffset < 0 || result.TcpSegmentLength < 0 || result.RequestBytesWritten < 0 || result.ResponseBytesRead < 0 || result.HttpBodyOffset < 0 || result.HttpBodyLength < 0)
        {
            return HttpTcpClientGetPlanDiagnosticFormatStatus.InvalidResult;
        }

        return HttpTcpClientGetPlanDiagnosticFormatStatus.Success;
    }

    private static int GetRequiredBytes(in HttpTcpClientGetPlanResult result)
    {
        return "HTTP_TCP_GET_PLAN status="u8.Length + GetPlanStatusName(result.Status).Length + " state="u8.Length + GetStateName(result.State).Length + " tcpFrame="u8.Length + GetTcpFrameStatusName(result.TcpFrameStatus).Length + " tcp="u8.Length + GetTcpStatusName(result.TcpStatus).Length + " stream="u8.Length + GetStreamStatusName(result.StreamStatus).Length + " http="u8.Length + GetHttpStatusName(result.HttpStatus).Length + " request="u8.Length + GetRequestStatusName(result.RequestStatus).Length + " response="u8.Length + GetResponseStatusName(result.ResponseStatus).Length + " packet="u8.Length + GetPacketStatusName(result.PacketStatus).Length + " bytes="u8.Length + AsciiDecimalFormatter.CountInt64Digits(result.BytesWritten) + " tcpSegmentOffset="u8.Length + AsciiDecimalFormatter.CountInt64Digits(result.TcpSegmentOffset) + " tcpSegmentBytes="u8.Length + AsciiDecimalFormatter.CountInt64Digits(result.TcpSegmentLength) + " requestBytes="u8.Length + AsciiDecimalFormatter.CountInt64Digits(result.RequestBytesWritten) + " responseBytes="u8.Length + AsciiDecimalFormatter.CountInt64Digits(result.ResponseBytesRead) + " httpStatus="u8.Length + AsciiDecimalFormatter.CountUInt64Digits(result.HttpStatusCode) + " bodyOffset="u8.Length + AsciiDecimalFormatter.CountInt64Digits(result.HttpBodyOffset) + " bodyBytes="u8.Length + AsciiDecimalFormatter.CountInt64Digits(result.HttpBodyLength) + " localSeq="u8.Length + AsciiDecimalFormatter.CountUInt64Digits(result.LocalNextSequenceNumber) + " remoteSeq="u8.Length + AsciiDecimalFormatter.CountUInt64Digits(result.RemoteNextSequenceNumber);
    }

    private static bool IsKnownPlanStatus(HttpTcpClientGetPlanStatus status)
    {
        return status >= HttpTcpClientGetPlanStatus.Success && status <= HttpTcpClientGetPlanStatus.HttpResponseFailed;
    }

    private static bool IsKnownState(HttpTcpClientGetPlannerState state)
    {
        return state >= HttpTcpClientGetPlannerState.Initial && state <= HttpTcpClientGetPlannerState.Failed;
    }

    private static bool IsKnownTcpFrameStatus(TcpIpv4FramePlanStatus status)
    {
        return status >= TcpIpv4FramePlanStatus.Success && status <= TcpIpv4FramePlanStatus.BadTcpChecksum;
    }

    private static bool IsKnownTcpStatus(TcpActiveConnectStatus status)
    {
        return status >= TcpActiveConnectStatus.Success && status <= TcpActiveConnectStatus.Timeout;
    }

    private static bool IsKnownStreamStatus(TcpEstablishedStreamStatus status)
    {
        return status >= TcpEstablishedStreamStatus.Success && status <= TcpEstablishedStreamStatus.InvalidAcknowledgementNumber;
    }

    private static bool IsKnownHttpStatus(HttpClientGetTransactionStatus status)
    {
        return status >= HttpClientGetTransactionStatus.Success && status <= HttpClientGetTransactionStatus.TruncatedBody;
    }

    private static bool IsKnownRequestStatus(HttpRequestBuilderStatus status)
    {
        return status >= HttpRequestBuilderStatus.Success && status <= HttpRequestBuilderStatus.InvalidPath;
    }

    private static bool IsKnownResponseStatus(HttpResponseParseStatus status)
    {
        return status >= HttpResponseParseStatus.Success && status <= HttpResponseParseStatus.DuplicateContentLength;
    }

    private static bool IsKnownPacketStatus(PacketParseStatus status)
    {
        return status >= PacketParseStatus.Success && status <= PacketParseStatus.UnsupportedIcmpCode;
    }

    private static ReadOnlySpan<byte> GetPlanStatusName(HttpTcpClientGetPlanStatus status)
    {
        return status switch
        {
            HttpTcpClientGetPlanStatus.Success => "Success"u8,
            HttpTcpClientGetPlanStatus.InvalidState => "InvalidState"u8,
            HttpTcpClientGetPlanStatus.RequestBufferTooSmall => "RequestBufferTooSmall"u8,
            HttpTcpClientGetPlanStatus.RequestTooLarge => "RequestTooLarge"u8,
            HttpTcpClientGetPlanStatus.ResponseBufferTooSmall => "ResponseBufferTooSmall"u8,
            HttpTcpClientGetPlanStatus.TcpFrameFailed => "TcpFrameFailed"u8,
            HttpTcpClientGetPlanStatus.FrameBufferTooSmall => "FrameBufferTooSmall"u8,
            HttpTcpClientGetPlanStatus.FrameWriteFailed => "FrameWriteFailed"u8,
            HttpTcpClientGetPlanStatus.FrameParseFailed => "FrameParseFailed"u8,
            HttpTcpClientGetPlanStatus.UnsupportedEtherType => "UnsupportedEtherType"u8,
            HttpTcpClientGetPlanStatus.UnsupportedIpProtocol => "UnsupportedIpProtocol"u8,
            HttpTcpClientGetPlanStatus.WrongLocalMac => "WrongLocalMac"u8,
            HttpTcpClientGetPlanStatus.WrongRemoteMac => "WrongRemoteMac"u8,
            HttpTcpClientGetPlanStatus.WrongLocalAddress => "WrongLocalAddress"u8,
            HttpTcpClientGetPlanStatus.WrongRemoteAddress => "WrongRemoteAddress"u8,
            HttpTcpClientGetPlanStatus.TcpSegmentParseFailed => "TcpSegmentParseFailed"u8,
            HttpTcpClientGetPlanStatus.WrongTcpPorts => "WrongTcpPorts"u8,
            HttpTcpClientGetPlanStatus.InvalidTcpFlags => "InvalidTcpFlags"u8,
            HttpTcpClientGetPlanStatus.WrongAcknowledgementNumber => "WrongAcknowledgementNumber"u8,
            HttpTcpClientGetPlanStatus.TcpStreamSendFailed => "TcpStreamSendFailed"u8,
            HttpTcpClientGetPlanStatus.TcpStreamReceiveFailed => "TcpStreamReceiveFailed"u8,
            HttpTcpClientGetPlanStatus.TcpStreamReadFailed => "TcpStreamReadFailed"u8,
            HttpTcpClientGetPlanStatus.HttpRequestFailed => "HttpRequestFailed"u8,
            HttpTcpClientGetPlanStatus.HttpResponseFailed => "HttpResponseFailed"u8,
            _ => "Unknown"u8
        };
    }

    private static ReadOnlySpan<byte> GetStateName(HttpTcpClientGetPlannerState state)
    {
        return state switch
        {
            HttpTcpClientGetPlannerState.Initial => "Initial"u8,
            HttpTcpClientGetPlannerState.SynSent => "SynSent"u8,
            HttpTcpClientGetPlannerState.Established => "Established"u8,
            HttpTcpClientGetPlannerState.RequestWritten => "RequestWritten"u8,
            HttpTcpClientGetPlannerState.ResponseReceived => "ResponseReceived"u8,
            HttpTcpClientGetPlannerState.Failed => "Failed"u8,
            _ => "Unknown"u8
        };
    }

    private static ReadOnlySpan<byte> GetTcpFrameStatusName(TcpIpv4FramePlanStatus status)
    {
        return status switch
        {
            TcpIpv4FramePlanStatus.Success => "Success"u8,
            TcpIpv4FramePlanStatus.InvalidLocalIdentity => "InvalidLocalIdentity"u8,
            TcpIpv4FramePlanStatus.InvalidRemoteIdentity => "InvalidRemoteIdentity"u8,
            TcpIpv4FramePlanStatus.InvalidState => "InvalidState"u8,
            TcpIpv4FramePlanStatus.FrameBufferTooSmall => "FrameBufferTooSmall"u8,
            TcpIpv4FramePlanStatus.TcpSegmentWriteFailed => "TcpSegmentWriteFailed"u8,
            TcpIpv4FramePlanStatus.FrameWriteFailed => "FrameWriteFailed"u8,
            TcpIpv4FramePlanStatus.FrameParseFailed => "FrameParseFailed"u8,
            TcpIpv4FramePlanStatus.UnsupportedEtherType => "UnsupportedEtherType"u8,
            TcpIpv4FramePlanStatus.UnsupportedIpProtocol => "UnsupportedIpProtocol"u8,
            TcpIpv4FramePlanStatus.WrongLocalMac => "WrongLocalMac"u8,
            TcpIpv4FramePlanStatus.WrongRemoteMac => "WrongRemoteMac"u8,
            TcpIpv4FramePlanStatus.WrongLocalAddress => "WrongLocalAddress"u8,
            TcpIpv4FramePlanStatus.WrongRemoteAddress => "WrongRemoteAddress"u8,
            TcpIpv4FramePlanStatus.TcpSegmentParseFailed => "TcpSegmentParseFailed"u8,
            TcpIpv4FramePlanStatus.WrongTcpPorts => "WrongTcpPorts"u8,
            TcpIpv4FramePlanStatus.InvalidTcpFlags => "InvalidTcpFlags"u8,
            TcpIpv4FramePlanStatus.WrongAcknowledgementNumber => "WrongAcknowledgementNumber"u8,
            TcpIpv4FramePlanStatus.WrongSequenceNumber => "WrongSequenceNumber"u8,
            TcpIpv4FramePlanStatus.BadTcpChecksum => "BadTcpChecksum"u8,
            _ => "Unknown"u8
        };
    }

    private static ReadOnlySpan<byte> GetTcpStatusName(TcpActiveConnectStatus status)
    {
        return status switch
        {
            TcpActiveConnectStatus.Success => "Success"u8,
            TcpActiveConnectStatus.InvalidState => "InvalidState"u8,
            TcpActiveConnectStatus.BufferTooSmall => "BufferTooSmall"u8,
            TcpActiveConnectStatus.SegmentWriteFailed => "SegmentWriteFailed"u8,
            TcpActiveConnectStatus.SegmentParseFailed => "SegmentParseFailed"u8,
            TcpActiveConnectStatus.UnexpectedPorts => "UnexpectedPorts"u8,
            TcpActiveConnectStatus.InvalidFlags => "InvalidFlags"u8,
            TcpActiveConnectStatus.WrongAcknowledgementNumber => "WrongAcknowledgementNumber"u8,
            TcpActiveConnectStatus.WrongSequenceNumber => "WrongSequenceNumber"u8,
            TcpActiveConnectStatus.Timeout => "Timeout"u8,
            _ => "Unknown"u8
        };
    }

    private static ReadOnlySpan<byte> GetStreamStatusName(TcpEstablishedStreamStatus status)
    {
        return status switch
        {
            TcpEstablishedStreamStatus.Success => "Success"u8,
            TcpEstablishedStreamStatus.InvalidState => "InvalidState"u8,
            TcpEstablishedStreamStatus.SegmentParseFailed => "SegmentParseFailed"u8,
            TcpEstablishedStreamStatus.SegmentWriteFailed => "SegmentWriteFailed"u8,
            TcpEstablishedStreamStatus.UnexpectedPorts => "UnexpectedPorts"u8,
            TcpEstablishedStreamStatus.InvalidFlags => "InvalidFlags"u8,
            TcpEstablishedStreamStatus.NoPayload => "NoPayload"u8,
            TcpEstablishedStreamStatus.DuplicateSegment => "DuplicateSegment"u8,
            TcpEstablishedStreamStatus.OutOfOrderSegment => "OutOfOrderSegment"u8,
            TcpEstablishedStreamStatus.ReceiveBufferTooSmall => "ReceiveBufferTooSmall"u8,
            TcpEstablishedStreamStatus.NoData => "NoData"u8,
            TcpEstablishedStreamStatus.DestinationTooSmall => "DestinationTooSmall"u8,
            TcpEstablishedStreamStatus.InvalidMaximumPayloadLength => "InvalidMaximumPayloadLength"u8,
            TcpEstablishedStreamStatus.InvalidAcknowledgementNumber => "InvalidAcknowledgementNumber"u8,
            _ => "Unknown"u8
        };
    }

    private static ReadOnlySpan<byte> GetHttpStatusName(HttpClientGetTransactionStatus status)
    {
        return status switch
        {
            HttpClientGetTransactionStatus.Success => "Success"u8,
            HttpClientGetTransactionStatus.InvalidState => "InvalidState"u8,
            HttpClientGetTransactionStatus.RequestBufferTooSmall => "RequestBufferTooSmall"u8,
            HttpClientGetTransactionStatus.EmptyHost => "EmptyHost"u8,
            HttpClientGetTransactionStatus.InvalidHost => "InvalidHost"u8,
            HttpClientGetTransactionStatus.EmptyPath => "EmptyPath"u8,
            HttpClientGetTransactionStatus.MissingLeadingSlash => "MissingLeadingSlash"u8,
            HttpClientGetTransactionStatus.InvalidPath => "InvalidPath"u8,
            HttpClientGetTransactionStatus.RequestWriteFailed => "RequestWriteFailed"u8,
            HttpClientGetTransactionStatus.MalformedResponse => "MalformedResponse"u8,
            HttpClientGetTransactionStatus.UnsupportedTransferEncoding => "UnsupportedTransferEncoding"u8,
            HttpClientGetTransactionStatus.TruncatedBody => "TruncatedBody"u8,
            _ => "Unknown"u8
        };
    }

    private static ReadOnlySpan<byte> GetRequestStatusName(HttpRequestBuilderStatus status)
    {
        return status switch
        {
            HttpRequestBuilderStatus.Success => "Success"u8,
            HttpRequestBuilderStatus.DestinationTooSmall => "DestinationTooSmall"u8,
            HttpRequestBuilderStatus.EmptyHost => "EmptyHost"u8,
            HttpRequestBuilderStatus.InvalidHost => "InvalidHost"u8,
            HttpRequestBuilderStatus.EmptyPath => "EmptyPath"u8,
            HttpRequestBuilderStatus.MissingLeadingSlash => "MissingLeadingSlash"u8,
            HttpRequestBuilderStatus.InvalidPath => "InvalidPath"u8,
            _ => "Unknown"u8
        };
    }

    private static ReadOnlySpan<byte> GetResponseStatusName(HttpResponseParseStatus status)
    {
        return status switch
        {
            HttpResponseParseStatus.Success => "Success"u8,
            HttpResponseParseStatus.BufferTooSmall => "BufferTooSmall"u8,
            HttpResponseParseStatus.MissingStatusLineTerminator => "MissingStatusLineTerminator"u8,
            HttpResponseParseStatus.InvalidProtocol => "InvalidProtocol"u8,
            HttpResponseParseStatus.InvalidStatusCode => "InvalidStatusCode"u8,
            HttpResponseParseStatus.MissingHeaderTerminator => "MissingHeaderTerminator"u8,
            HttpResponseParseStatus.InvalidHeader => "InvalidHeader"u8,
            HttpResponseParseStatus.InvalidContentLength => "InvalidContentLength"u8,
            HttpResponseParseStatus.BodyTooShort => "BodyTooShort"u8,
            HttpResponseParseStatus.UnsupportedTransferEncoding => "UnsupportedTransferEncoding"u8,
            HttpResponseParseStatus.MissingContentLength => "MissingContentLength"u8,
            HttpResponseParseStatus.DuplicateContentLength => "DuplicateContentLength"u8,
            _ => "Unknown"u8
        };
    }

    private static ReadOnlySpan<byte> GetPacketStatusName(PacketParseStatus status)
    {
        return status switch
        {
            PacketParseStatus.Success => "Success"u8,
            PacketParseStatus.BufferTooSmall => "BufferTooSmall"u8,
            PacketParseStatus.InvalidLength => "InvalidLength"u8,
            PacketParseStatus.UnsupportedEtherType => "UnsupportedEtherType"u8,
            PacketParseStatus.UnsupportedIpVersion => "UnsupportedIpVersion"u8,
            PacketParseStatus.UnsupportedIpHeaderLength => "UnsupportedIpHeaderLength"u8,
            PacketParseStatus.UnsupportedIpProtocol => "UnsupportedIpProtocol"u8,
            PacketParseStatus.BadChecksum => "BadChecksum"u8,
            PacketParseStatus.TruncatedPayload => "TruncatedPayload"u8,
            PacketParseStatus.UnsupportedHardwareType => "UnsupportedHardwareType"u8,
            PacketParseStatus.UnsupportedProtocolType => "UnsupportedProtocolType"u8,
            PacketParseStatus.UnsupportedHardwareAddressLength => "UnsupportedHardwareAddressLength"u8,
            PacketParseStatus.UnsupportedProtocolAddressLength => "UnsupportedProtocolAddressLength"u8,
            PacketParseStatus.UnsupportedOperation => "UnsupportedOperation"u8,
            PacketParseStatus.UnsupportedIcmpType => "UnsupportedIcmpType"u8,
            PacketParseStatus.UnsupportedIcmpCode => "UnsupportedIcmpCode"u8,
            _ => "Unknown"u8
        };
    }

    private static bool TryAppendBytes(ReadOnlySpan<byte> value, Span<byte> destination, ref int offset)
    {
        AsciiTextBuilderResult result = AsciiTextBuilder.AppendBytes(destination, offset, value);
        if (!result.Succeeded)
        {
            return false;
        }

        offset = result.NextOffset;
        return true;
    }

    private static bool TryAppendInt64Decimal(long value, Span<byte> destination, ref int offset)
    {
        AsciiTextBuilderResult result = AsciiTextBuilder.AppendInt64Decimal(destination, offset, value);
        if (!result.Succeeded)
        {
            return false;
        }

        offset = result.NextOffset;
        return true;
    }

    private static bool TryAppendUInt64Decimal(ulong value, Span<byte> destination, ref int offset)
    {
        AsciiTextBuilderResult result = AsciiTextBuilder.AppendUInt64Decimal(destination, offset, value);
        if (!result.Succeeded)
        {
            return false;
        }

        offset = result.NextOffset;
        return true;
    }
}
