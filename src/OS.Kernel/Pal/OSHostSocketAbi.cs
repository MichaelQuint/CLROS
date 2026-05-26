using System.Runtime.InteropServices;

namespace OS.Pal;

public static class OSHostSocketConstants
{
    public const ushort AddressFamilyIpv4 = 2;

    public const uint DomainIpv4 = 2;

    public const uint TypeStream = 1;

    public const uint ProtocolTcp = 6;

    public const uint CreateFlagNonBlocking = 1;

    public const uint PollReadable = 1;

    public const uint PollWritable = 2;

    public const uint PollError = 4;

    public const uint PollHangup = 8;

    public const uint PollAccept = 16;

    public const uint ShutdownReceive = 1;

    public const uint ShutdownSend = 2;

    public const uint ShutdownBoth = ShutdownReceive | ShutdownSend;

    public const uint SocketOptionNoDelay = 1;

    public const uint SocketOptionReuseAddress = 2;

    public const uint SocketOptionReceiveTimeoutMilliseconds = 3;

    public const uint SocketOptionSendTimeoutMilliseconds = 4;

    public const uint SocketOptionKeepAlive = 5;

    public const uint TimeoutKindInfinite = 0;

    public const uint TimeoutKindImmediate = 1;

    public const uint TimeoutKindRelativeTicks = 2;

    public const uint TimeoutKindAbsoluteTicks = 3;

    public const uint WaitModeNone = 0;

    public const uint WaitModeRead = 1;

    public const uint WaitModeWrite = 2;

    public const uint WaitModeAccept = 3;

    public const uint WaitModeConnect = 4;

    public const uint AddressKindLocal = 1;

    public const uint AddressKindRemote = 2;
}

[StructLayout(LayoutKind.Sequential)]
public readonly struct OSHostSocketOptionValue
{
    public readonly long Value;

    public readonly ulong Reserved;

    public OSHostSocketOptionValue(long value, ulong reserved)
    {
        Value = value;
        Reserved = reserved;
    }

    public bool IsBooleanEnabled => Value == 1;

    public static OSHostSocketOptionValue Boolean(bool enabled)
    {
        return new OSHostSocketOptionValue(enabled ? 1 : 0, reserved: 0);
    }

    public static OSHostSocketOptionValue TimeoutMilliseconds(long milliseconds)
    {
        return new OSHostSocketOptionValue(milliseconds, reserved: 0);
    }
}

[StructLayout(LayoutKind.Sequential)]
public readonly struct OSHostSocketHandle
{
    public readonly ulong Value;

    public OSHostSocketHandle(ulong value)
    {
        Value = value;
    }

    public bool IsValid => Value != 0;

    public OSHostHandle AsHandle()
    {
        return new OSHostHandle(Value);
    }

    public static OSHostSocketHandle FromHandle(OSHostHandle handle)
    {
        return new OSHostSocketHandle(handle.Value);
    }
}

[StructLayout(LayoutKind.Sequential)]
public readonly struct OSHostIpv4Endpoint
{
    public readonly ushort Family;

    public readonly ushort PortNetworkOrder;

    public readonly byte Address0;

    public readonly byte Address1;

    public readonly byte Address2;

    public readonly byte Address3;

    public OSHostIpv4Endpoint(
        ushort family,
        ushort portNetworkOrder,
        byte address0,
        byte address1,
        byte address2,
        byte address3)
    {
        Family = family;
        PortNetworkOrder = portNetworkOrder;
        Address0 = address0;
        Address1 = address1;
        Address2 = address2;
        Address3 = address3;
    }

    public ushort PortHostOrder => OSHostSocketNumbers.NetworkToHost16(PortNetworkOrder);

    public bool IsValid => Validate(this) == OSHostStatus.Ok;

    public OSHostSocketAddress AsSocketAddress()
    {
        return new OSHostSocketAddress(
            Family,
            PortNetworkOrder,
            flowInfo: 0,
            scopeId: 0,
            addressHigh: 0,
            addressLow: ToAddressNetworkOrder());
    }

    public uint ToAddressNetworkOrder()
    {
        return ((uint)Address0 << 24)
            | ((uint)Address1 << 16)
            | ((uint)Address2 << 8)
            | Address3;
    }

    public static OSHostStatus TryCreate(
        byte address0,
        byte address1,
        byte address2,
        byte address3,
        ushort portHostOrder,
        out OSHostIpv4Endpoint endpoint)
    {
        if (portHostOrder == 0)
        {
            endpoint = default;
            return OSHostStatus.InvalidArgument;
        }

        endpoint = new OSHostIpv4Endpoint(
            OSHostSocketConstants.AddressFamilyIpv4,
            OSHostSocketNumbers.HostToNetwork16(portHostOrder),
            address0,
            address1,
            address2,
            address3);
        return OSHostStatus.Ok;
    }

    public static OSHostStatus Validate(OSHostIpv4Endpoint endpoint)
    {
        if (endpoint.Family != OSHostSocketConstants.AddressFamilyIpv4)
        {
            return OSHostStatus.InvalidArgument;
        }

        if (endpoint.PortNetworkOrder == 0)
        {
            return OSHostStatus.InvalidArgument;
        }

        return OSHostStatus.Ok;
    }
}

[StructLayout(LayoutKind.Sequential)]
public readonly struct OSHostSocketWaitOptions
{
    public readonly OSHostSocketHandle Socket;

    public readonly OSHostTimeout Timeout;

    public readonly uint Mode;

    public readonly uint Flags;

    public OSHostSocketWaitOptions(
        OSHostSocketHandle socket,
        OSHostTimeout timeout,
        uint mode,
        uint flags)
    {
        Socket = socket;
        Timeout = timeout;
        Mode = mode;
        Flags = flags;
    }

    public OSHostStatus Validate()
    {
        if (!Socket.IsValid)
        {
            return OSHostStatus.InvalidArgument;
        }

        if (!OSHostSocketTimeouts.IsValid(Timeout))
        {
            return OSHostStatus.InvalidArgument;
        }

        return OSHostSocketWaitModes.IsDefined(Mode)
            ? OSHostStatus.Ok
            : OSHostStatus.InvalidArgument;
    }
}

[StructLayout(LayoutKind.Sequential)]
public readonly struct OSHostSocketOperationResult
{
    public readonly int StatusCode;

    public readonly uint Flags;

    public readonly ulong BytesTransferred;

    public readonly OSHostSocketHandle Socket;

    public readonly ulong Reserved;

    public OSHostSocketOperationResult(
        int statusCode,
        uint flags,
        ulong bytesTransferred,
        OSHostSocketHandle socket,
        ulong reserved)
    {
        StatusCode = statusCode;
        Flags = flags;
        BytesTransferred = bytesTransferred;
        Socket = socket;
        Reserved = reserved;
    }

    public bool Succeeded => StatusCode == (int)OSHostStatus.Ok && Socket.IsValid;

    public bool IsRetryable => StatusCode == (int)OSHostStatus.WouldBlock
        || StatusCode == (int)OSHostStatus.TimedOut
        || StatusCode == (int)OSHostStatus.Interrupted
        || StatusCode == (int)OSHostStatus.TryAgain;

    public OSHostStatus ValidateStatus()
    {
        return OSHostStatusNames.IsDefined(StatusCode)
            ? OSHostStatus.Ok
            : OSHostStatus.InvalidArgument;
    }

    public static OSHostSocketOperationResult FromStatus(
        OSHostStatus status,
        ulong bytesTransferred,
        OSHostSocketHandle socket,
        uint flags = 0)
    {
        return new OSHostSocketOperationResult(
            (int)status,
            flags,
            bytesTransferred,
            socket,
            reserved: 0);
    }
}

[StructLayout(LayoutKind.Sequential)]
public readonly struct OSHostSocketBindRequest
{
    public readonly OSHostHandle Socket;

    public readonly OSHostSocketAddress Address;

    public readonly uint Flags;

    public readonly uint Reserved;

    public OSHostSocketBindRequest(OSHostHandle socket, OSHostSocketAddress address, uint flags, uint reserved)
    {
        Socket = socket;
        Address = address;
        Flags = flags;
        Reserved = reserved;
    }
}

[StructLayout(LayoutKind.Sequential)]
public readonly struct OSHostSocketListenRequest
{
    public readonly OSHostHandle Socket;

    public readonly uint Backlog;

    public readonly uint Flags;

    public readonly ulong Reserved;

    public OSHostSocketListenRequest(OSHostHandle socket, uint backlog, uint flags, ulong reserved)
    {
        Socket = socket;
        Backlog = backlog;
        Flags = flags;
        Reserved = reserved;
    }
}

[StructLayout(LayoutKind.Sequential)]
public readonly struct OSHostSocketAcceptRequest
{
    public readonly OSHostHandle Listener;

    public readonly OSHostTimeout Timeout;

    public readonly uint Flags;

    public readonly uint Reserved;

    public OSHostSocketAcceptRequest(OSHostHandle listener, OSHostTimeout timeout, uint flags, uint reserved)
    {
        Listener = listener;
        Timeout = timeout;
        Flags = flags;
        Reserved = reserved;
    }
}

[StructLayout(LayoutKind.Sequential)]
public readonly struct OSHostSocketAcceptOutput
{
    public readonly OSHostHandle AcceptedSocket;

    public readonly OSHostSocketAddress LocalAddress;

    public readonly OSHostSocketAddress RemoteAddress;

    public OSHostSocketAcceptOutput(
        OSHostHandle acceptedSocket,
        OSHostSocketAddress localAddress,
        OSHostSocketAddress remoteAddress)
    {
        AcceptedSocket = acceptedSocket;
        LocalAddress = localAddress;
        RemoteAddress = remoteAddress;
    }
}

[StructLayout(LayoutKind.Sequential)]
public readonly struct OSHostSocketConnectRequest
{
    public readonly OSHostHandle Socket;

    public readonly OSHostSocketAddress RemoteAddress;

    public readonly OSHostTimeout Timeout;

    public readonly uint Flags;

    public readonly uint Reserved;

    public OSHostSocketConnectRequest(
        OSHostHandle socket,
        OSHostSocketAddress remoteAddress,
        OSHostTimeout timeout,
        uint flags,
        uint reserved)
    {
        Socket = socket;
        RemoteAddress = remoteAddress;
        Timeout = timeout;
        Flags = flags;
        Reserved = reserved;
    }
}

[StructLayout(LayoutKind.Sequential)]
public readonly struct OSHostSocketShutdownRequest
{
    public readonly OSHostHandle Socket;

    public readonly uint Direction;

    public readonly uint Flags;

    public readonly ulong Reserved;

    public OSHostSocketShutdownRequest(OSHostHandle socket, uint direction, uint flags, ulong reserved)
    {
        Socket = socket;
        Direction = direction;
        Flags = flags;
        Reserved = reserved;
    }
}

[StructLayout(LayoutKind.Sequential)]
public readonly struct OSHostSocketOptionRequest
{
    public readonly OSHostHandle Socket;

    public readonly uint OptionName;

    public readonly uint Flags;

    public readonly OSHostSocketOptionValue Value;

    public OSHostSocketOptionRequest(
        OSHostHandle socket,
        uint optionName,
        uint flags,
        OSHostSocketOptionValue value)
    {
        Socket = socket;
        OptionName = optionName;
        Flags = flags;
        Value = value;
    }
}

[StructLayout(LayoutKind.Sequential)]
public readonly struct OSHostSocketAddressQueryRequest
{
    public readonly OSHostHandle Socket;

    public readonly uint AddressKind;

    public readonly uint Flags;

    public readonly ulong Reserved;

    public OSHostSocketAddressQueryRequest(OSHostHandle socket, uint addressKind, uint flags, ulong reserved)
    {
        Socket = socket;
        AddressKind = addressKind;
        Flags = flags;
        Reserved = reserved;
    }
}

public static class OSHostSocketNumbers
{
    public static ushort HostToNetwork16(ushort value)
    {
        return (ushort)((value << 8) | (value >> 8));
    }

    public static ushort NetworkToHost16(ushort value)
    {
        return HostToNetwork16(value);
    }
}

public static class OSHostSocketOptions
{
    public static bool IsDefined(uint optionName)
    {
        return optionName is OSHostSocketConstants.SocketOptionNoDelay
            or OSHostSocketConstants.SocketOptionReuseAddress
            or OSHostSocketConstants.SocketOptionReceiveTimeoutMilliseconds
            or OSHostSocketConstants.SocketOptionSendTimeoutMilliseconds
            or OSHostSocketConstants.SocketOptionKeepAlive;
    }

    public static OSHostStatus Validate(uint optionName, OSHostSocketOptionValue value)
    {
        if (!IsDefined(optionName) || value.Reserved != 0)
        {
            return OSHostStatus.InvalidArgument;
        }

        return optionName switch
        {
            OSHostSocketConstants.SocketOptionNoDelay
                or OSHostSocketConstants.SocketOptionReuseAddress
                or OSHostSocketConstants.SocketOptionKeepAlive
                => ValidateBoolean(value),
            OSHostSocketConstants.SocketOptionReceiveTimeoutMilliseconds
                or OSHostSocketConstants.SocketOptionSendTimeoutMilliseconds
                => ValidateTimeoutMilliseconds(value),
            _ => OSHostStatus.InvalidArgument
        };
    }

    private static OSHostStatus ValidateBoolean(OSHostSocketOptionValue value)
    {
        return value.Value is 0 or 1
            ? OSHostStatus.Ok
            : OSHostStatus.InvalidArgument;
    }

    private static OSHostStatus ValidateTimeoutMilliseconds(OSHostSocketOptionValue value)
    {
        return value.Value >= -1
            ? OSHostStatus.Ok
            : OSHostStatus.InvalidArgument;
    }
}

public static class OSHostSocketTimeouts
{
    public static OSHostTimeout Infinite => new OSHostTimeout(
        OSHostSocketConstants.TimeoutKindInfinite,
        flags: 0,
        ticks: 0,
        tickFrequency: 0);

    public static OSHostTimeout Immediate => new OSHostTimeout(
        OSHostSocketConstants.TimeoutKindImmediate,
        flags: 0,
        ticks: 0,
        tickFrequency: 0);

    public static OSHostStatus TryFromMilliseconds(
        long milliseconds,
        ulong tickFrequency,
        out OSHostTimeout timeout)
    {
        if (milliseconds == -1)
        {
            timeout = Infinite;
            return OSHostStatus.Ok;
        }

        if (milliseconds < -1)
        {
            timeout = default;
            return OSHostStatus.InvalidArgument;
        }

        if (milliseconds == 0)
        {
            timeout = Immediate;
            return OSHostStatus.Ok;
        }

        if (tickFrequency == 0)
        {
            timeout = default;
            return OSHostStatus.InvalidArgument;
        }

        ulong ticks = ConvertMillisecondsToTicks((ulong)milliseconds, tickFrequency);
        timeout = new OSHostTimeout(
            OSHostSocketConstants.TimeoutKindRelativeTicks,
            flags: 0,
            ticks,
            tickFrequency);
        return OSHostStatus.Ok;
    }

    public static bool IsValid(OSHostTimeout timeout)
    {
        return timeout.Kind switch
        {
            OSHostSocketConstants.TimeoutKindInfinite => timeout.Ticks == 0 && timeout.TickFrequency == 0,
            OSHostSocketConstants.TimeoutKindImmediate => timeout.Ticks == 0 && timeout.TickFrequency == 0,
            OSHostSocketConstants.TimeoutKindRelativeTicks => timeout.TickFrequency != 0,
            OSHostSocketConstants.TimeoutKindAbsoluteTicks => timeout.TickFrequency != 0,
            _ => false
        };
    }

    private static ulong ConvertMillisecondsToTicks(ulong milliseconds, ulong tickFrequency)
    {
        ulong wholeSeconds = milliseconds / 1_000;
        ulong remainderMilliseconds = milliseconds % 1_000;

        if (wholeSeconds > ulong.MaxValue / tickFrequency)
        {
            return ulong.MaxValue;
        }

        ulong ticks = wholeSeconds * tickFrequency;
        ulong remainderTicks = ConvertRemainderMillisecondsToTicks(remainderMilliseconds, tickFrequency);

        return ulong.MaxValue - ticks < remainderTicks
            ? ulong.MaxValue
            : ticks + remainderTicks;
    }

    private static ulong ConvertRemainderMillisecondsToTicks(
        ulong remainderMilliseconds,
        ulong tickFrequency)
    {
        if (remainderMilliseconds == 0)
        {
            return 0;
        }

        if (tickFrequency > (ulong.MaxValue - 999) / remainderMilliseconds)
        {
            return ulong.MaxValue;
        }

        return ((remainderMilliseconds * tickFrequency) + 999) / 1_000;
    }
}

public static class OSHostSocketWaitModes
{
    public static bool IsDefined(uint mode)
    {
        return mode is OSHostSocketConstants.WaitModeNone
            or OSHostSocketConstants.WaitModeRead
            or OSHostSocketConstants.WaitModeWrite
            or OSHostSocketConstants.WaitModeAccept
            or OSHostSocketConstants.WaitModeConnect;
    }
}

public static class OSHostStatusNames
{
    public const string Unknown = "OSHOST_STATUS_UNKNOWN";

    public static bool IsDefined(int statusCode)
    {
        return statusCode is >= (int)OSHostStatus.Ok and <= (int)OSHostStatus.Closed;
    }

    public static string GetConstantName(OSHostStatus status)
    {
        return GetConstantName((int)status);
    }

    public static string GetConstantName(int statusCode)
    {
        return statusCode switch
        {
            (int)OSHostStatus.Ok => "OSHOST_STATUS_OK",
            (int)OSHostStatus.InvalidArgument => "OSHOST_STATUS_INVALID_ARGUMENT",
            (int)OSHostStatus.NotSupported => "OSHOST_STATUS_NOT_SUPPORTED",
            (int)OSHostStatus.NotFound => "OSHOST_STATUS_NOT_FOUND",
            (int)OSHostStatus.AccessDenied => "OSHOST_STATUS_ACCESS_DENIED",
            (int)OSHostStatus.BufferTooSmall => "OSHOST_STATUS_BUFFER_TOO_SMALL",
            (int)OSHostStatus.OutOfMemory => "OSHOST_STATUS_OUT_OF_MEMORY",
            (int)OSHostStatus.WouldBlock => "OSHOST_STATUS_WOULD_BLOCK",
            (int)OSHostStatus.TimedOut => "OSHOST_STATUS_TIMED_OUT",
            (int)OSHostStatus.Interrupted => "OSHOST_STATUS_INTERRUPTED",
            (int)OSHostStatus.EndOfFile => "OSHOST_STATUS_END_OF_FILE",
            (int)OSHostStatus.ConnectionReset => "OSHOST_STATUS_CONNECTION_RESET",
            (int)OSHostStatus.ConnectionRefused => "OSHOST_STATUS_CONNECTION_REFUSED",
            (int)OSHostStatus.AddressInUse => "OSHOST_STATUS_ADDR_IN_USE",
            (int)OSHostStatus.AddressNotAvailable => "OSHOST_STATUS_ADDR_NOT_AVAILABLE",
            (int)OSHostStatus.NetworkDown => "OSHOST_STATUS_NETWORK_DOWN",
            (int)OSHostStatus.TryAgain => "OSHOST_STATUS_TRY_AGAIN",
            (int)OSHostStatus.IoError => "OSHOST_STATUS_IO_ERROR",
            (int)OSHostStatus.InternalError => "OSHOST_STATUS_INTERNAL_ERROR",
            (int)OSHostStatus.Closed => "OSHOST_STATUS_CLOSED",
            _ => Unknown
        };
    }
}
