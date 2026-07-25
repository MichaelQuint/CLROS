namespace OS.Net;

using OS.Std.Text;

public enum Ipv4NeighborRoutePlanDiagnosticFormatStatus
{
    Success = 0,
    InvalidResult = 1,
    DestinationTooSmall = 2
}

public readonly struct Ipv4NeighborRoutePlanDiagnosticFormatResult
{
    private const uint InitializedFlag = 1;

    public Ipv4NeighborRoutePlanDiagnosticFormatResult(
        Ipv4NeighborRoutePlanDiagnosticFormatStatus status,
        Ipv4NeighborRoutePlanStatus routeStatus,
        int bytesWritten,
        int requiredBytes,
        int availableBytes,
        uint flags = InitializedFlag)
    {
        Status = status;
        RouteStatus = routeStatus;
        BytesWritten = bytesWritten;
        RequiredBytes = requiredBytes;
        AvailableBytes = availableBytes;
        Flags = flags;
    }

    public Ipv4NeighborRoutePlanDiagnosticFormatStatus Status { get; }

    public Ipv4NeighborRoutePlanStatus RouteStatus { get; }

    public int BytesWritten { get; }

    public int RequiredBytes { get; }

    public int AvailableBytes { get; }

    public uint Flags { get; }

    public bool IsInitialized => (Flags & InitializedFlag) != 0;

    public bool Succeeded =>
        IsInitialized &&
        Status == Ipv4NeighborRoutePlanDiagnosticFormatStatus.Success &&
        RequiredBytes > 0 &&
        BytesWritten == RequiredBytes &&
        RequiredBytes <= AvailableBytes;

    public static Ipv4NeighborRoutePlanDiagnosticFormatResult Fail(
        Ipv4NeighborRoutePlanDiagnosticFormatStatus status,
        Ipv4NeighborRoutePlanStatus routeStatus,
        int requiredBytes,
        int availableBytes)
    {
        return new Ipv4NeighborRoutePlanDiagnosticFormatResult(
            status,
            routeStatus,
            bytesWritten: 0,
            requiredBytes,
            availableBytes);
    }
}

public static class Ipv4NeighborRoutePlanDiagnosticFormatter
{
    private const int MacAddressTextLength = 17;

    public static Ipv4NeighborRoutePlanDiagnosticFormatResult FormatResult(
        in Ipv4NeighborRoutePlanResult result,
        Span<byte> destination)
    {
        Ipv4NeighborRoutePlanDiagnosticFormatStatus validationStatus = Validate(in result);
        if (validationStatus != Ipv4NeighborRoutePlanDiagnosticFormatStatus.Success)
        {
            return Ipv4NeighborRoutePlanDiagnosticFormatResult.Fail(
                validationStatus,
                result.Status,
                requiredBytes: 0,
                destination.Length);
        }

        int requiredBytes = GetRequiredBytes(in result);
        if (destination.Length < requiredBytes)
        {
            return Ipv4NeighborRoutePlanDiagnosticFormatResult.Fail(
                Ipv4NeighborRoutePlanDiagnosticFormatStatus.DestinationTooSmall,
                result.Status,
                requiredBytes,
                destination.Length);
        }

        int offset = 0;
        if (!TryAppendBytes("IPV4_NEIGHBOR_ROUTE status="u8, destination, ref offset) ||
            !TryAppendBytes(GetRouteStatusName(result.Status), destination, ref offset) ||
            !TryAppendBytes(" action="u8, destination, ref offset) ||
            !TryAppendBytes(GetActionName(result.Action), destination, ref offset) ||
            !TryAppendBytes(" cache="u8, destination, ref offset) ||
            !TryAppendBytes(GetCacheStatusName(result.CacheStatus), destination, ref offset) ||
            !TryAppendBytes(" arp="u8, destination, ref offset) ||
            !TryAppendBytes(GetArpStatusName(result.ArpStatus), destination, ref offset) ||
            !TryAppendBytes(" packet="u8, destination, ref offset) ||
            !TryAppendBytes(GetPacketStatusName(result.PacketStatus), destination, ref offset) ||
            !TryAppendBytes(" bytes="u8, destination, ref offset) ||
            !TryAppendInt32Decimal(result.BytesWritten, destination, ref offset) ||
            !TryAppendBytes(" cacheCount="u8, destination, ref offset) ||
            !TryAppendInt32Decimal(result.CacheCount, destination, ref offset) ||
            !TryAppendBytes(" cacheCapacity="u8, destination, ref offset) ||
            !TryAppendInt32Decimal(result.CacheCapacity, destination, ref offset))
        {
            return Ipv4NeighborRoutePlanDiagnosticFormatResult.Fail(
                Ipv4NeighborRoutePlanDiagnosticFormatStatus.DestinationTooSmall,
                result.Status,
                requiredBytes,
                destination.Length);
        }

        if (result.Action == Ipv4NeighborRoutePlanAction.UseCachedMac)
        {
            if (!TryAppendBytes(" targetMac="u8, destination, ref offset) ||
                !TryAppendMacAddress(result.TargetMac, destination, ref offset))
            {
                return Ipv4NeighborRoutePlanDiagnosticFormatResult.Fail(
                    Ipv4NeighborRoutePlanDiagnosticFormatStatus.DestinationTooSmall,
                    result.Status,
                    requiredBytes,
                    destination.Length);
            }
        }

        return new Ipv4NeighborRoutePlanDiagnosticFormatResult(
            Ipv4NeighborRoutePlanDiagnosticFormatStatus.Success,
            result.Status,
            bytesWritten: offset,
            requiredBytes,
            availableBytes: destination.Length);
    }

    public static Ipv4NeighborRoutePlanDiagnosticFormatResult QueryRequiredBytes(
        in Ipv4NeighborRoutePlanResult result)
    {
        Ipv4NeighborRoutePlanDiagnosticFormatStatus validationStatus = Validate(in result);
        if (validationStatus != Ipv4NeighborRoutePlanDiagnosticFormatStatus.Success)
        {
            return Ipv4NeighborRoutePlanDiagnosticFormatResult.Fail(
                validationStatus,
                result.Status,
                requiredBytes: 0,
                availableBytes: 0);
        }

        return new Ipv4NeighborRoutePlanDiagnosticFormatResult(
            Ipv4NeighborRoutePlanDiagnosticFormatStatus.Success,
            result.Status,
            bytesWritten: 0,
            requiredBytes: GetRequiredBytes(in result),
            availableBytes: 0);
    }

    private static Ipv4NeighborRoutePlanDiagnosticFormatStatus Validate(
        in Ipv4NeighborRoutePlanResult result)
    {
        if (!result.IsInitialized ||
            !IsKnownRouteStatus(result.Status) ||
            !IsKnownAction(result.Action) ||
            !IsKnownCacheStatus(result.CacheStatus) ||
            !IsKnownArpStatus(result.ArpStatus) ||
            !IsKnownPacketStatus(result.PacketStatus) ||
            result.BytesWritten < 0 ||
            result.CacheCount < 0 ||
            result.CacheCapacity < 0 ||
            result.CacheCount > result.CacheCapacity)
        {
            return Ipv4NeighborRoutePlanDiagnosticFormatStatus.InvalidResult;
        }

        if (result.Action == Ipv4NeighborRoutePlanAction.UseCachedMac &&
            (result.TargetMac.IsZero || result.TargetMac.IsBroadcast))
        {
            return Ipv4NeighborRoutePlanDiagnosticFormatStatus.InvalidResult;
        }

        return Ipv4NeighborRoutePlanDiagnosticFormatStatus.Success;
    }

    private static int GetRequiredBytes(in Ipv4NeighborRoutePlanResult result)
    {
        int required = "IPV4_NEIGHBOR_ROUTE status="u8.Length
            + GetRouteStatusName(result.Status).Length
            + " action="u8.Length
            + GetActionName(result.Action).Length
            + " cache="u8.Length
            + GetCacheStatusName(result.CacheStatus).Length
            + " arp="u8.Length
            + GetArpStatusName(result.ArpStatus).Length
            + " packet="u8.Length
            + GetPacketStatusName(result.PacketStatus).Length
            + " bytes="u8.Length
            + AsciiDecimalFormatter.CountInt32Digits(result.BytesWritten)
            + " cacheCount="u8.Length
            + AsciiDecimalFormatter.CountInt32Digits(result.CacheCount)
            + " cacheCapacity="u8.Length
            + AsciiDecimalFormatter.CountInt32Digits(result.CacheCapacity);

        if (result.Action == Ipv4NeighborRoutePlanAction.UseCachedMac)
        {
            required += " targetMac="u8.Length + MacAddressTextLength;
        }

        return required;
    }

    private static bool IsKnownRouteStatus(Ipv4NeighborRoutePlanStatus status)
    {
        return status >= Ipv4NeighborRoutePlanStatus.Success &&
            status <= Ipv4NeighborRoutePlanStatus.RequestWriteFailed;
    }

    private static bool IsKnownAction(Ipv4NeighborRoutePlanAction action)
    {
        return action >= Ipv4NeighborRoutePlanAction.None &&
            action <= Ipv4NeighborRoutePlanAction.SendArpRequest;
    }

    private static bool IsKnownCacheStatus(ArpNeighborCacheStatus status)
    {
        return status >= ArpNeighborCacheStatus.Success &&
            status <= ArpNeighborCacheStatus.InvalidTick;
    }

    private static bool IsKnownArpStatus(ArpNeighborResolutionStatus status)
    {
        return status >= ArpNeighborResolutionStatus.Success &&
            status <= ArpNeighborResolutionStatus.Timeout;
    }

    private static bool IsKnownPacketStatus(PacketParseStatus status)
    {
        return status >= PacketParseStatus.Success &&
            status <= PacketParseStatus.UnsupportedIcmpCode;
    }

    private static ReadOnlySpan<byte> GetRouteStatusName(Ipv4NeighborRoutePlanStatus status)
    {
        return status switch
        {
            Ipv4NeighborRoutePlanStatus.Success => "Success"u8,
            Ipv4NeighborRoutePlanStatus.InvalidTargetAddress => "InvalidTargetAddress"u8,
            Ipv4NeighborRoutePlanStatus.InvalidLocalAddress => "InvalidLocalAddress"u8,
            Ipv4NeighborRoutePlanStatus.InvalidLocalMacAddress => "InvalidLocalMacAddress"u8,
            Ipv4NeighborRoutePlanStatus.RequestBufferTooSmall => "RequestBufferTooSmall"u8,
            Ipv4NeighborRoutePlanStatus.RequestWriteFailed => "RequestWriteFailed"u8,
            _ => "Unknown"u8
        };
    }

    private static ReadOnlySpan<byte> GetActionName(Ipv4NeighborRoutePlanAction action)
    {
        return action switch
        {
            Ipv4NeighborRoutePlanAction.None => "None"u8,
            Ipv4NeighborRoutePlanAction.UseCachedMac => "UseCachedMac"u8,
            Ipv4NeighborRoutePlanAction.SendArpRequest => "SendArpRequest"u8,
            _ => "Unknown"u8
        };
    }

    private static ReadOnlySpan<byte> GetCacheStatusName(ArpNeighborCacheStatus status)
    {
        return status switch
        {
            ArpNeighborCacheStatus.Success => "Success"u8,
            ArpNeighborCacheStatus.InvalidAddress => "InvalidAddress"u8,
            ArpNeighborCacheStatus.InvalidMacAddress => "InvalidMacAddress"u8,
            ArpNeighborCacheStatus.NotFound => "NotFound"u8,
            ArpNeighborCacheStatus.CapacityExceeded => "CapacityExceeded"u8,
            ArpNeighborCacheStatus.InvalidTtl => "InvalidTtl"u8,
            ArpNeighborCacheStatus.InvalidTick => "InvalidTick"u8,
            _ => "Unknown"u8
        };
    }

    private static ReadOnlySpan<byte> GetArpStatusName(ArpNeighborResolutionStatus status)
    {
        return status switch
        {
            ArpNeighborResolutionStatus.Success => "Success"u8,
            ArpNeighborResolutionStatus.InvalidState => "InvalidState"u8,
            ArpNeighborResolutionStatus.BufferTooSmall => "BufferTooSmall"u8,
            ArpNeighborResolutionStatus.FrameWriteFailed => "FrameWriteFailed"u8,
            ArpNeighborResolutionStatus.FrameParseFailed => "FrameParseFailed"u8,
            ArpNeighborResolutionStatus.UnsupportedEtherType => "UnsupportedEtherType"u8,
            ArpNeighborResolutionStatus.UnexpectedOperation => "UnexpectedOperation"u8,
            ArpNeighborResolutionStatus.SenderAddressMismatch => "SenderAddressMismatch"u8,
            ArpNeighborResolutionStatus.TargetAddressMismatch => "TargetAddressMismatch"u8,
            ArpNeighborResolutionStatus.TargetMacMismatch => "TargetMacMismatch"u8,
            ArpNeighborResolutionStatus.EthernetDestinationMismatch => "EthernetDestinationMismatch"u8,
            ArpNeighborResolutionStatus.EthernetSourceMismatch => "EthernetSourceMismatch"u8,
            ArpNeighborResolutionStatus.Timeout => "Timeout"u8,
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

    private static bool TryAppendBytes(
        ReadOnlySpan<byte> value,
        Span<byte> destination,
        ref int offset)
    {
        AsciiTextBuilderResult result = AsciiTextBuilder.AppendBytes(destination, offset, value);
        if (!result.Succeeded)
        {
            return false;
        }

        offset = result.NextOffset;
        return true;
    }

    private static bool TryAppendInt32Decimal(int value, Span<byte> destination, ref int offset)
    {
        AsciiTextBuilderResult result =
            AsciiTextBuilder.AppendInt64Decimal(destination, offset, value);
        if (!result.Succeeded)
        {
            return false;
        }

        offset = result.NextOffset;
        return true;
    }

    private static bool TryAppendMacAddress(
        MacAddress macAddress,
        Span<byte> destination,
        ref int offset)
    {
        return TryAppendHexByte(macAddress.Octet0, destination, ref offset) &&
            TryAppendColon(destination, ref offset) &&
            TryAppendHexByte(macAddress.Octet1, destination, ref offset) &&
            TryAppendColon(destination, ref offset) &&
            TryAppendHexByte(macAddress.Octet2, destination, ref offset) &&
            TryAppendColon(destination, ref offset) &&
            TryAppendHexByte(macAddress.Octet3, destination, ref offset) &&
            TryAppendColon(destination, ref offset) &&
            TryAppendHexByte(macAddress.Octet4, destination, ref offset) &&
            TryAppendColon(destination, ref offset) &&
            TryAppendHexByte(macAddress.Octet5, destination, ref offset);
    }

    private static bool TryAppendColon(Span<byte> destination, ref int offset)
    {
        AsciiTextBuilderResult result = AsciiTextBuilder.AppendByte(destination, offset, (byte)':');
        if (!result.Succeeded)
        {
            return false;
        }

        offset = result.NextOffset;
        return true;
    }

    private static bool TryAppendHexByte(byte value, Span<byte> destination, ref int offset)
    {
        AsciiTextBuilderResult high =
            AsciiTextBuilder.AppendByte(destination, offset, ToUpperHexNibble((byte)(value >> 4)));
        if (!high.Succeeded)
        {
            return false;
        }

        AsciiTextBuilderResult low =
            AsciiTextBuilder.AppendByte(destination, high.NextOffset, ToUpperHexNibble((byte)(value & 0x0F)));
        if (!low.Succeeded)
        {
            return false;
        }

        offset = low.NextOffset;
        return true;
    }

    private static byte ToUpperHexNibble(byte value)
    {
        return value < 10
            ? (byte)('0' + value)
            : (byte)('A' + (value - 10));
    }
}
