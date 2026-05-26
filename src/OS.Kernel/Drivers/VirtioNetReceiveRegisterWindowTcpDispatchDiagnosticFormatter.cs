namespace OS.Drivers;

using OS.Net;
using OS.Std.Text;

public enum VirtioNetReceiveRegisterWindowTcpDispatchDiagnosticFormatStatus
{
    Success = 0,
    InvalidResult = 1,
    DestinationTooSmall = 2
}

public readonly struct VirtioNetReceiveRegisterWindowTcpDispatchDiagnosticFormatResult
{
    private const uint InitializedFlag = 1;

    public VirtioNetReceiveRegisterWindowTcpDispatchDiagnosticFormatResult(
        VirtioNetReceiveRegisterWindowTcpDispatchDiagnosticFormatStatus status,
        VirtioNetReceiveRegisterWindowTcpDispatchStatus dispatchStatus,
        int bytesWritten,
        int requiredBytes,
        int availableBytes,
        uint flags = InitializedFlag)
    {
        Status = status;
        DispatchStatus = dispatchStatus;
        BytesWritten = bytesWritten;
        RequiredBytes = requiredBytes;
        AvailableBytes = availableBytes;
        Flags = flags;
    }

    public VirtioNetReceiveRegisterWindowTcpDispatchDiagnosticFormatStatus Status { get; }

    public VirtioNetReceiveRegisterWindowTcpDispatchStatus DispatchStatus { get; }

    public int BytesWritten { get; }

    public int RequiredBytes { get; }

    public int AvailableBytes { get; }

    public uint Flags { get; }

    public bool IsInitialized => (Flags & InitializedFlag) != 0;

    public bool Succeeded =>
        IsInitialized &&
        Status == VirtioNetReceiveRegisterWindowTcpDispatchDiagnosticFormatStatus.Success &&
        RequiredBytes > 0 &&
        BytesWritten == RequiredBytes &&
        RequiredBytes <= AvailableBytes;

    public static VirtioNetReceiveRegisterWindowTcpDispatchDiagnosticFormatResult Fail(
        VirtioNetReceiveRegisterWindowTcpDispatchDiagnosticFormatStatus status,
        VirtioNetReceiveRegisterWindowTcpDispatchStatus dispatchStatus,
        int requiredBytes,
        int availableBytes)
    {
        return new VirtioNetReceiveRegisterWindowTcpDispatchDiagnosticFormatResult(
            status,
            dispatchStatus,
            bytesWritten: 0,
            requiredBytes,
            availableBytes);
    }
}

public static class VirtioNetReceiveRegisterWindowTcpDispatchDiagnosticFormatter
{
    public const bool UsesSharedAsciiTextBuilder = true;
    public const bool UsesCallerProvidedDestination = true;
    public const bool AllocatesManagedStorage = false;
    public const bool PerformsLiveMmio = false;
    public const bool PerformsPacketIo = false;
    public const bool PerformsPacketReceive = false;
    public const bool OpensSockets = false;
    public const bool DispatchesSockets = false;
    public const bool StartsKestrel = false;

    public static VirtioNetReceiveRegisterWindowTcpDispatchDiagnosticFormatResult FormatResult(
        in VirtioNetReceiveRegisterWindowTcpDispatchResult result,
        Span<byte> destination)
    {
        VirtioNetReceiveRegisterWindowTcpDispatchDiagnosticFormatStatus validationStatus =
            Validate(in result);
        if (validationStatus !=
            VirtioNetReceiveRegisterWindowTcpDispatchDiagnosticFormatStatus.Success)
        {
            return VirtioNetReceiveRegisterWindowTcpDispatchDiagnosticFormatResult.Fail(
                validationStatus,
                result.Status,
                requiredBytes: 0,
                destination.Length);
        }

        int requiredBytes = GetRequiredBytes(in result);
        if (destination.Length < requiredBytes)
        {
            return VirtioNetReceiveRegisterWindowTcpDispatchDiagnosticFormatResult.Fail(
                VirtioNetReceiveRegisterWindowTcpDispatchDiagnosticFormatStatus.DestinationTooSmall,
                result.Status,
                requiredBytes,
                destination.Length);
        }

        int offset = 0;
        if (!TryAppendBytes("VIRTIO_NET_RX_TCP_DISPATCH status="u8, destination, ref offset) ||
            !TryAppendBytes(GetDispatchStatusName(result.Status), destination, ref offset) ||
            !TryAppendBytes(" rx="u8, destination, ref offset) ||
            !TryAppendBytes(GetReceiveDispatchStatusName(result.ReceiveDispatchStatus), destination, ref offset) ||
            !TryAppendBytes(" slab="u8, destination, ref offset) ||
            !TryAppendBytes(GetSlabCompletionStatusName(result.SlabCompletionStatus), destination, ref offset) ||
            !TryAppendBytes(" network="u8, destination, ref offset) ||
            !TryAppendBytes(GetNetworkStatusName(result.NetworkDispatchStatus), destination, ref offset) ||
            !TryAppendBytes(" protocol="u8, destination, ref offset) ||
            !TryAppendUInt64Decimal(result.NetworkDispatch.Ipv4Header.Protocol, destination, ref offset) ||
            !TryAppendBytes(" payloadKind="u8, destination, ref offset) ||
            !TryAppendBytes(GetPayloadKindName(result.NetworkDispatch.PayloadKind), destination, ref offset) ||
            !TryAppendBytes(" tcp="u8, destination, ref offset) ||
            !TryAppendBytes(GetTcpDispatchStatusName(result.TcpDispatchStatus), destination, ref offset) ||
            !TryAppendBytes(" action="u8, destination, ref offset) ||
            !TryAppendBytes(GetTcpActionName(result.TcpAction), destination, ref offset) ||
            !TryAppendBytes(" descriptorHead="u8, destination, ref offset) ||
            !TryAppendUInt64Decimal(result.DescriptorHead, destination, ref offset) ||
            !TryAppendBytes(" bufferIndex="u8, destination, ref offset) ||
            !TryAppendUInt64Decimal(result.BufferIndex, destination, ref offset) ||
            !TryAppendBytes(" selectedOffset="u8, destination, ref offset) ||
            !TryAppendUInt64Decimal(result.SelectedBufferOffset, destination, ref offset) ||
            !TryAppendBytes(" selectedLength="u8, destination, ref offset) ||
            !TryAppendUInt64Decimal(result.SelectedBufferLength, destination, ref offset) ||
            !TryAppendBytes(" usedBytes="u8, destination, ref offset) ||
            !TryAppendUInt64Decimal(result.UsedByteCount, destination, ref offset) ||
            !TryAppendBytes(" payloadBytes="u8, destination, ref offset) ||
            !TryAppendInt64Decimal(result.PayloadBytes, destination, ref offset) ||
            !TryAppendBytes(" passiveAckBytes="u8, destination, ref offset) ||
            !TryAppendInt64Decimal(result.PassiveAckBytes, destination, ref offset) ||
            !TryAppendBytes(" liveMmio="u8, destination, ref offset) ||
            !TryAppendBool(VirtioNetReceiveRegisterWindowTcpDispatchPlanner.PerformsLiveMmio, destination, ref offset) ||
            !TryAppendBytes(" packetIo="u8, destination, ref offset) ||
            !TryAppendBool(VirtioNetReceiveRegisterWindowTcpDispatchPlanner.PerformsPacketIo, destination, ref offset) ||
            !TryAppendBytes(" packetRx="u8, destination, ref offset) ||
            !TryAppendBool(VirtioNetReceiveRegisterWindowTcpDispatchPlanner.PerformsPacketReceive, destination, ref offset) ||
            !TryAppendBytes(" sockets="u8, destination, ref offset) ||
            !TryAppendBool(VirtioNetReceiveRegisterWindowTcpDispatchPlanner.OpensSockets, destination, ref offset) ||
            !TryAppendBytes(" socketDispatch="u8, destination, ref offset) ||
            !TryAppendBool(VirtioNetReceiveRegisterWindowTcpDispatchPlanner.DispatchesSockets, destination, ref offset) ||
            !TryAppendBytes(" kestrel="u8, destination, ref offset) ||
            !TryAppendBool(VirtioNetReceiveRegisterWindowTcpDispatchPlanner.StartsKestrel, destination, ref offset) ||
            !TryAppendBytes(" tcpControlBlocks="u8, destination, ref offset) ||
            !TryAppendBool(VirtioNetReceiveRegisterWindowTcpDispatchPlanner.MutatesTcpControlBlocks, destination, ref offset))
        {
            return VirtioNetReceiveRegisterWindowTcpDispatchDiagnosticFormatResult.Fail(
                VirtioNetReceiveRegisterWindowTcpDispatchDiagnosticFormatStatus.DestinationTooSmall,
                result.Status,
                requiredBytes,
                destination.Length);
        }

        return new VirtioNetReceiveRegisterWindowTcpDispatchDiagnosticFormatResult(
            VirtioNetReceiveRegisterWindowTcpDispatchDiagnosticFormatStatus.Success,
            result.Status,
            bytesWritten: offset,
            requiredBytes,
            availableBytes: destination.Length);
    }

    public static VirtioNetReceiveRegisterWindowTcpDispatchDiagnosticFormatResult QueryRequiredBytes(
        in VirtioNetReceiveRegisterWindowTcpDispatchResult result)
    {
        VirtioNetReceiveRegisterWindowTcpDispatchDiagnosticFormatStatus validationStatus =
            Validate(in result);
        if (validationStatus !=
            VirtioNetReceiveRegisterWindowTcpDispatchDiagnosticFormatStatus.Success)
        {
            return VirtioNetReceiveRegisterWindowTcpDispatchDiagnosticFormatResult.Fail(
                validationStatus,
                result.Status,
                requiredBytes: 0,
                availableBytes: 0);
        }

        return new VirtioNetReceiveRegisterWindowTcpDispatchDiagnosticFormatResult(
            VirtioNetReceiveRegisterWindowTcpDispatchDiagnosticFormatStatus.Success,
            result.Status,
            bytesWritten: 0,
            requiredBytes: GetRequiredBytes(in result),
            availableBytes: 0);
    }

    private static VirtioNetReceiveRegisterWindowTcpDispatchDiagnosticFormatStatus Validate(
        in VirtioNetReceiveRegisterWindowTcpDispatchResult result)
    {
        if (!result.IsInitialized ||
            !IsKnownDispatchStatus(result.Status) ||
            !IsKnownReceiveDispatchStatus(result.ReceiveDispatchStatus) ||
            !IsKnownSlabCompletionStatus(result.SlabCompletionStatus) ||
            !IsKnownNetworkStatus(result.NetworkDispatchStatus) ||
            !IsKnownPayloadKind(result.NetworkDispatch.PayloadKind) ||
            !IsKnownTcpDispatchStatus(result.TcpDispatchStatus) ||
            !IsKnownTcpAction(result.TcpAction) ||
            result.UsedByteCount > result.SelectedBufferLength ||
            result.PayloadBytes < 0 ||
            result.PassiveAckBytes < 0)
        {
            return VirtioNetReceiveRegisterWindowTcpDispatchDiagnosticFormatStatus.InvalidResult;
        }

        return VirtioNetReceiveRegisterWindowTcpDispatchDiagnosticFormatStatus.Success;
    }

    private static int GetRequiredBytes(
        in VirtioNetReceiveRegisterWindowTcpDispatchResult result)
    {
        return "VIRTIO_NET_RX_TCP_DISPATCH status="u8.Length
            + GetDispatchStatusName(result.Status).Length
            + " rx="u8.Length
            + GetReceiveDispatchStatusName(result.ReceiveDispatchStatus).Length
            + " slab="u8.Length
            + GetSlabCompletionStatusName(result.SlabCompletionStatus).Length
            + " network="u8.Length
            + GetNetworkStatusName(result.NetworkDispatchStatus).Length
            + " protocol="u8.Length
            + AsciiDecimalFormatter.CountUInt32Digits(result.NetworkDispatch.Ipv4Header.Protocol)
            + " payloadKind="u8.Length
            + GetPayloadKindName(result.NetworkDispatch.PayloadKind).Length
            + " tcp="u8.Length
            + GetTcpDispatchStatusName(result.TcpDispatchStatus).Length
            + " action="u8.Length
            + GetTcpActionName(result.TcpAction).Length
            + " descriptorHead="u8.Length
            + AsciiDecimalFormatter.CountUInt32Digits(result.DescriptorHead)
            + " bufferIndex="u8.Length
            + AsciiDecimalFormatter.CountUInt32Digits(result.BufferIndex)
            + " selectedOffset="u8.Length
            + AsciiDecimalFormatter.CountUInt32Digits(result.SelectedBufferOffset)
            + " selectedLength="u8.Length
            + AsciiDecimalFormatter.CountUInt32Digits(result.SelectedBufferLength)
            + " usedBytes="u8.Length
            + AsciiDecimalFormatter.CountUInt32Digits(result.UsedByteCount)
            + " payloadBytes="u8.Length
            + AsciiDecimalFormatter.CountInt32Digits(result.PayloadBytes)
            + " passiveAckBytes="u8.Length
            + AsciiDecimalFormatter.CountInt32Digits(result.PassiveAckBytes)
            + " liveMmio="u8.Length
            + GetBoolName(VirtioNetReceiveRegisterWindowTcpDispatchPlanner.PerformsLiveMmio).Length
            + " packetIo="u8.Length
            + GetBoolName(VirtioNetReceiveRegisterWindowTcpDispatchPlanner.PerformsPacketIo).Length
            + " packetRx="u8.Length
            + GetBoolName(VirtioNetReceiveRegisterWindowTcpDispatchPlanner.PerformsPacketReceive).Length
            + " sockets="u8.Length
            + GetBoolName(VirtioNetReceiveRegisterWindowTcpDispatchPlanner.OpensSockets).Length
            + " socketDispatch="u8.Length
            + GetBoolName(VirtioNetReceiveRegisterWindowTcpDispatchPlanner.DispatchesSockets).Length
            + " kestrel="u8.Length
            + GetBoolName(VirtioNetReceiveRegisterWindowTcpDispatchPlanner.StartsKestrel).Length
            + " tcpControlBlocks="u8.Length
            + GetBoolName(VirtioNetReceiveRegisterWindowTcpDispatchPlanner.MutatesTcpControlBlocks).Length;
    }

    private static bool IsKnownDispatchStatus(
        VirtioNetReceiveRegisterWindowTcpDispatchStatus status)
    {
        return status >= VirtioNetReceiveRegisterWindowTcpDispatchStatus.Success &&
            status <= VirtioNetReceiveRegisterWindowTcpDispatchStatus.TcpDispatchFailed;
    }

    private static bool IsKnownReceiveDispatchStatus(
        VirtioNetReceiveRegisterWindowDispatchStatus status)
    {
        return status >= VirtioNetReceiveRegisterWindowDispatchStatus.Success &&
            status <= VirtioNetReceiveRegisterWindowDispatchStatus.DispatchFailed;
    }

    private static bool IsKnownSlabCompletionStatus(
        VirtioNetReceiveRegisterWindowSlabCompletionStatus status)
    {
        return status >= VirtioNetReceiveRegisterWindowSlabCompletionStatus.Success &&
            status <= VirtioNetReceiveRegisterWindowSlabCompletionStatus.ClassificationFailed;
    }

    private static bool IsKnownNetworkStatus(NetworkReceiveDispatchStatus status)
    {
        return status >= NetworkReceiveDispatchStatus.Success &&
            status <= NetworkReceiveDispatchStatus.UnsupportedPayload;
    }

    private static bool IsKnownPayloadKind(EthernetPayloadKind kind)
    {
        return kind >= EthernetPayloadKind.Unsupported &&
            kind <= EthernetPayloadKind.Arp;
    }

    private static bool IsKnownTcpDispatchStatus(TcpReceiveDispatchStatus status)
    {
        return status >= TcpReceiveDispatchStatus.Success &&
            status <= TcpReceiveDispatchStatus.ControlSegmentRejected;
    }

    private static bool IsKnownTcpAction(TcpReceiveDispatchAction action)
    {
        return action >= TcpReceiveDispatchAction.None &&
            action <= TcpReceiveDispatchAction.ResetReceived;
    }

    private static ReadOnlySpan<byte> GetDispatchStatusName(
        VirtioNetReceiveRegisterWindowTcpDispatchStatus status)
    {
        return status switch
        {
            VirtioNetReceiveRegisterWindowTcpDispatchStatus.Success => "Success"u8,
            VirtioNetReceiveRegisterWindowTcpDispatchStatus.ReceiveDispatchFailed => "ReceiveDispatchFailed"u8,
            VirtioNetReceiveRegisterWindowTcpDispatchStatus.SelectedBufferRangeInvalid => "SelectedBufferRangeInvalid"u8,
            VirtioNetReceiveRegisterWindowTcpDispatchStatus.UnsupportedProtocol => "UnsupportedProtocol"u8,
            VirtioNetReceiveRegisterWindowTcpDispatchStatus.BadTcpChecksum => "BadTcpChecksum"u8,
            VirtioNetReceiveRegisterWindowTcpDispatchStatus.TcpStreamFailed => "TcpStreamFailed"u8,
            VirtioNetReceiveRegisterWindowTcpDispatchStatus.TcpDispatchFailed => "TcpDispatchFailed"u8,
            _ => "Unknown"u8
        };
    }

    private static ReadOnlySpan<byte> GetReceiveDispatchStatusName(
        VirtioNetReceiveRegisterWindowDispatchStatus status)
    {
        return status switch
        {
            VirtioNetReceiveRegisterWindowDispatchStatus.Success => "Success"u8,
            VirtioNetReceiveRegisterWindowDispatchStatus.SlabCompletionFailed => "SlabCompletionFailed"u8,
            VirtioNetReceiveRegisterWindowDispatchStatus.SelectedBufferRangeInvalid => "SelectedBufferRangeInvalid"u8,
            VirtioNetReceiveRegisterWindowDispatchStatus.DispatchFailed => "DispatchFailed"u8,
            _ => "Unknown"u8
        };
    }

    private static ReadOnlySpan<byte> GetSlabCompletionStatusName(
        VirtioNetReceiveRegisterWindowSlabCompletionStatus status)
    {
        return status switch
        {
            VirtioNetReceiveRegisterWindowSlabCompletionStatus.Success => "Success"u8,
            VirtioNetReceiveRegisterWindowSlabCompletionStatus.ReadinessFailed => "ReadinessFailed"u8,
            VirtioNetReceiveRegisterWindowSlabCompletionStatus.QueueMemoryTooSmall => "QueueMemoryTooSmall"u8,
            VirtioNetReceiveRegisterWindowSlabCompletionStatus.NoCompletion => "NoCompletion"u8,
            VirtioNetReceiveRegisterWindowSlabCompletionStatus.UnexpectedUsedIndex => "UnexpectedUsedIndex"u8,
            VirtioNetReceiveRegisterWindowSlabCompletionStatus.UnexpectedDescriptorHead => "UnexpectedDescriptorHead"u8,
            VirtioNetReceiveRegisterWindowSlabCompletionStatus.ReceiveSlabTooSmall => "ReceiveSlabTooSmall"u8,
            VirtioNetReceiveRegisterWindowSlabCompletionStatus.CompletionFailed => "CompletionFailed"u8,
            VirtioNetReceiveRegisterWindowSlabCompletionStatus.ClassificationFailed => "ClassificationFailed"u8,
            _ => "Unknown"u8
        };
    }

    private static ReadOnlySpan<byte> GetNetworkStatusName(NetworkReceiveDispatchStatus status)
    {
        return status switch
        {
            NetworkReceiveDispatchStatus.Success => "Success"u8,
            NetworkReceiveDispatchStatus.ClassifierFailed => "ClassifierFailed"u8,
            NetworkReceiveDispatchStatus.ArpParseFailed => "ArpParseFailed"u8,
            NetworkReceiveDispatchStatus.Ipv4ParseFailed => "Ipv4ParseFailed"u8,
            NetworkReceiveDispatchStatus.UnsupportedPayload => "UnsupportedPayload"u8,
            _ => "Unknown"u8
        };
    }

    private static ReadOnlySpan<byte> GetPayloadKindName(EthernetPayloadKind kind)
    {
        return kind switch
        {
            EthernetPayloadKind.Unsupported => "Unsupported"u8,
            EthernetPayloadKind.Ipv4 => "Ipv4"u8,
            EthernetPayloadKind.Arp => "Arp"u8,
            _ => "Unknown"u8
        };
    }

    private static ReadOnlySpan<byte> GetTcpDispatchStatusName(TcpReceiveDispatchStatus status)
    {
        return status switch
        {
            TcpReceiveDispatchStatus.Success => "Success"u8,
            TcpReceiveDispatchStatus.NetworkDispatchFailed => "NetworkDispatchFailed"u8,
            TcpReceiveDispatchStatus.NonIpv4Payload => "NonIpv4Payload"u8,
            TcpReceiveDispatchStatus.UnsupportedIpProtocol => "UnsupportedIpProtocol"u8,
            TcpReceiveDispatchStatus.InvalidPayloadRange => "InvalidPayloadRange"u8,
            TcpReceiveDispatchStatus.TcpParseFailed => "TcpParseFailed"u8,
            TcpReceiveDispatchStatus.BadTcpChecksum => "BadTcpChecksum"u8,
            TcpReceiveDispatchStatus.ConnectionMismatch => "ConnectionMismatch"u8,
            TcpReceiveDispatchStatus.TcpStreamFailed => "TcpStreamFailed"u8,
            TcpReceiveDispatchStatus.CloseFailed => "CloseFailed"u8,
            TcpReceiveDispatchStatus.AckChecksumFailed => "AckChecksumFailed"u8,
            TcpReceiveDispatchStatus.UnsupportedTcpFlags => "UnsupportedTcpFlags"u8,
            TcpReceiveDispatchStatus.ControlSegmentRejected => "ControlSegmentRejected"u8,
            _ => "Unknown"u8
        };
    }

    private static ReadOnlySpan<byte> GetTcpActionName(TcpReceiveDispatchAction action)
    {
        return action switch
        {
            TcpReceiveDispatchAction.None => "None"u8,
            TcpReceiveDispatchAction.PayloadAccepted => "PayloadAccepted"u8,
            TcpReceiveDispatchAction.FinAcknowledged => "FinAcknowledged"u8,
            TcpReceiveDispatchAction.ResetReceived => "ResetReceived"u8,
            _ => "Unknown"u8
        };
    }

    private static ReadOnlySpan<byte> GetBoolName(bool value)
    {
        return value ? "true"u8 : "false"u8;
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

    private static bool TryAppendInt64Decimal(
        long value,
        Span<byte> destination,
        ref int offset)
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

    private static bool TryAppendUInt64Decimal(
        ulong value,
        Span<byte> destination,
        ref int offset)
    {
        AsciiTextBuilderResult result =
            AsciiTextBuilder.AppendUInt64Decimal(destination, offset, value);
        if (!result.Succeeded)
        {
            return false;
        }

        offset = result.NextOffset;
        return true;
    }

    private static bool TryAppendBool(
        bool value,
        Span<byte> destination,
        ref int offset)
    {
        return TryAppendBytes(GetBoolName(value), destination, ref offset);
    }
}
