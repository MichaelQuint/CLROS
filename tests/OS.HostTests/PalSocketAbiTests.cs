using System.Reflection;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using OS.Pal;

internal static class PalSocketAbiTests
{
    private static readonly Type[] SocketAbiStructTypes =
    [
        typeof(OSHostSocketHandle),
        typeof(OSHostIpv4Endpoint),
        typeof(OSHostSocketOptionValue),
        typeof(OSHostSocketWaitOptions),
        typeof(OSHostSocketOperationResult),
        typeof(OSHostSocketBindRequest),
        typeof(OSHostSocketListenRequest),
        typeof(OSHostSocketAcceptRequest),
        typeof(OSHostSocketAcceptOutput),
        typeof(OSHostSocketConnectRequest),
        typeof(OSHostSocketShutdownRequest),
        typeof(OSHostSocketOptionRequest),
        typeof(OSHostSocketAddressQueryRequest)
    ];

    [HostTest("pal.socket.abi validates pod layouts", HostLane.Fast, 0)]
    private static bool TestValidatesPodLayouts()
    {
        foreach (Type type in SocketAbiStructTypes)
        {
            StructLayoutAttribute? layout = type.StructLayoutAttribute;
            if (layout is null || layout.Value != LayoutKind.Sequential)
            {
                Console.WriteLine("PAL socket ABI struct is not sequential: " + type.FullName);
                return HostCheck.Result(false);
            }
        }

        if (Marshal.SizeOf<OSHostSocketBindRequest>() != 48
            || Marshal.SizeOf<OSHostSocketListenRequest>() != 24
            || Marshal.SizeOf<OSHostSocketAcceptRequest>() != 40
            || Marshal.SizeOf<OSHostSocketAcceptOutput>() != 72
            || Marshal.SizeOf<OSHostSocketConnectRequest>() != 72
            || Marshal.SizeOf<OSHostSocketShutdownRequest>() != 24
            || Marshal.SizeOf<OSHostSocketOptionRequest>() != 32
            || Marshal.SizeOf<OSHostSocketAddressQueryRequest>() != 24)
        {
            return HostCheck.Result(false);
        }

        return HostCheck.Result(Offset<OSHostSocketBindRequest>(nameof(OSHostSocketBindRequest.Socket)) == 0
            && Offset<OSHostSocketBindRequest>(nameof(OSHostSocketBindRequest.Address)) == 8
            && Offset<OSHostSocketBindRequest>(nameof(OSHostSocketBindRequest.Flags)) == 40
            && Offset<OSHostSocketListenRequest>(nameof(OSHostSocketListenRequest.Backlog)) == 8
            && Offset<OSHostSocketAcceptRequest>(nameof(OSHostSocketAcceptRequest.Timeout)) == 8
            && Offset<OSHostSocketAcceptRequest>(nameof(OSHostSocketAcceptRequest.Flags)) == 32
            && Offset<OSHostSocketAcceptOutput>(nameof(OSHostSocketAcceptOutput.AcceptedSocket)) == 0
            && Offset<OSHostSocketAcceptOutput>(nameof(OSHostSocketAcceptOutput.LocalAddress)) == 8
            && Offset<OSHostSocketAcceptOutput>(nameof(OSHostSocketAcceptOutput.RemoteAddress)) == 40
            && Offset<OSHostSocketConnectRequest>(nameof(OSHostSocketConnectRequest.RemoteAddress)) == 8
            && Offset<OSHostSocketConnectRequest>(nameof(OSHostSocketConnectRequest.Timeout)) == 40
            && Offset<OSHostSocketShutdownRequest>(nameof(OSHostSocketShutdownRequest.Direction)) == 8
            && Offset<OSHostSocketOptionRequest>(nameof(OSHostSocketOptionRequest.OptionName)) == 8
            && Offset<OSHostSocketOptionRequest>(nameof(OSHostSocketOptionRequest.Value)) == 16
            && Offset<OSHostSocketAddressQueryRequest>(nameof(OSHostSocketAddressQueryRequest.AddressKind)) == 8);
    }

    [HostTest("pal.socket.abi validates status codes", HostLane.Fast, 1)]
    private static bool TestValidatesStatusCodes()
    {
        return HostCheck.Result(HasStatusCode(OSHostStatus.InvalidArgument, 1)
            && HasStatusCode(OSHostStatus.NotSupported, 2)
            && HasStatusCode(OSHostStatus.OutOfMemory, 6)
            && HasStatusCode(OSHostStatus.WouldBlock, 7)
            && HasStatusCode(OSHostStatus.TimedOut, 8)
            && HasStatusCode(OSHostStatus.ConnectionReset, 11)
            && HasStatusCode(OSHostStatus.ConnectionRefused, 12)
            && HasStatusCode(OSHostStatus.Closed, 19)
            && OSHostStatusNames.GetConstantName(OSHostStatus.NotSupported) == "OSHOST_STATUS_NOT_SUPPORTED"
            && OSHostStatusNames.GetConstantName(OSHostStatus.OutOfMemory) == "OSHOST_STATUS_OUT_OF_MEMORY"
            && OSHostStatusNames.GetConstantName(OSHostStatus.ConnectionReset) == "OSHOST_STATUS_CONNECTION_RESET"
            && OSHostStatusNames.GetConstantName(OSHostStatus.Closed) == "OSHOST_STATUS_CLOSED"
            && OSHostStatusNames.IsDefined((int)OSHostStatus.Closed)
            && !OSHostStatusNames.IsDefined(20));
    }

    [HostTest("pal.socket.abi rejects managed boundary assumptions", HostLane.Fast, 2)]
    private static bool TestRejectsManagedBoundaryAssumptions()
    {
        foreach (Type type in SocketAbiStructTypes)
        {
            if (ContainsReferenceField(type, new HashSet<Type>()))
            {
                Console.WriteLine("PAL socket ABI struct contains a managed field: " + type.FullName);
                return HostCheck.Result(false);
            }

            foreach (FieldInfo field in type.GetFields(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic))
            {
                if (!field.IsPublic || !field.IsInitOnly)
                {
                    Console.WriteLine("PAL socket ABI field is not public readonly: " + type.FullName + "." + field.Name);
                    return HostCheck.Result(false);
                }
            }
        }

        return HostCheck.Result(AssertUnmanaged<OSHostSocketBindRequest>()
            && AssertUnmanaged<OSHostSocketListenRequest>()
            && AssertUnmanaged<OSHostSocketAcceptRequest>()
            && AssertUnmanaged<OSHostSocketAcceptOutput>()
            && AssertUnmanaged<OSHostSocketConnectRequest>()
            && AssertUnmanaged<OSHostSocketShutdownRequest>()
            && AssertUnmanaged<OSHostSocketOptionRequest>()
            && AssertUnmanaged<OSHostSocketAddressQueryRequest>());
    }

    [HostTest("pal.socket abi exposes endpoint contracts", HostLane.Fast, 3)]
    private static bool TestExposesEndpointContracts()
    {
        if (RuntimeHelpers.IsReferenceOrContainsReferences<OSHostSocketHandle>()
            || RuntimeHelpers.IsReferenceOrContainsReferences<OSHostIpv4Endpoint>()
            || RuntimeHelpers.IsReferenceOrContainsReferences<OSHostSocketOptionValue>()
            || RuntimeHelpers.IsReferenceOrContainsReferences<OSHostSocketWaitOptions>()
            || RuntimeHelpers.IsReferenceOrContainsReferences<OSHostSocketOperationResult>())
        {
            return HostCheck.Result(false);
        }

        if (Marshal.SizeOf<OSHostSocketHandle>() != 8
            || Marshal.SizeOf<OSHostIpv4Endpoint>() != 8
            || Marshal.SizeOf<OSHostSocketOptionValue>() != 16
            || Marshal.SizeOf<OSHostSocketWaitOptions>() != 40
            || Marshal.SizeOf<OSHostSocketOperationResult>() != 32)
        {
            return HostCheck.Result(false);
        }

        if (Offset<OSHostIpv4Endpoint>(nameof(OSHostIpv4Endpoint.Family)) != 0
            || Offset<OSHostIpv4Endpoint>(nameof(OSHostIpv4Endpoint.PortNetworkOrder)) != 2
            || Offset<OSHostIpv4Endpoint>(nameof(OSHostIpv4Endpoint.Address0)) != 4
            || Offset<OSHostSocketOptionValue>(nameof(OSHostSocketOptionValue.Value)) != 0
            || Offset<OSHostSocketOptionValue>(nameof(OSHostSocketOptionValue.Reserved)) != 8
            || Offset<OSHostSocketWaitOptions>(nameof(OSHostSocketWaitOptions.Socket)) != 0
            || Offset<OSHostSocketWaitOptions>(nameof(OSHostSocketWaitOptions.Timeout)) != 8
            || Offset<OSHostSocketWaitOptions>(nameof(OSHostSocketWaitOptions.Mode)) != 32
            || Offset<OSHostSocketOperationResult>(nameof(OSHostSocketOperationResult.StatusCode)) != 0
            || Offset<OSHostSocketOperationResult>(nameof(OSHostSocketOperationResult.BytesTransferred)) != 8
            || Offset<OSHostSocketOperationResult>(nameof(OSHostSocketOperationResult.Socket)) != 16)
        {
            return HostCheck.Result(false);
        }

        OSHostStatus status = OSHostIpv4Endpoint.TryCreate(
            127,
            0,
            0,
            1,
            portHostOrder: 8080,
            out OSHostIpv4Endpoint endpoint);
        if (status != OSHostStatus.Ok
            || !endpoint.IsValid
            || endpoint.Family != OSHostSocketConstants.AddressFamilyIpv4
            || endpoint.PortNetworkOrder != 0x901F
            || endpoint.PortHostOrder != 8080
            || endpoint.ToAddressNetworkOrder() != 0x7F000001)
        {
            return HostCheck.Result(false);
        }

        OSHostSocketAddress genericAddress = endpoint.AsSocketAddress();
        if (genericAddress.Family != OSHostSocketConstants.AddressFamilyIpv4
            || genericAddress.PortNetworkOrder != endpoint.PortNetworkOrder
            || genericAddress.AddressHigh != 0
            || genericAddress.AddressLow != 0x7F000001)
        {
            return HostCheck.Result(false);
        }

        OSHostSocketHandle socket = new OSHostSocketHandle(0x100);
        OSHostSocketWaitOptions waitOptions = new OSHostSocketWaitOptions(
            socket,
            OSHostSocketTimeouts.Immediate,
            OSHostSocketConstants.WaitModeAccept,
            flags: 0);

        return HostCheck.Result(socket.IsValid
            && socket.AsHandle().Value == 0x100
            && OSHostSocketHandle.FromHandle(socket.AsHandle()).Value == socket.Value
            && waitOptions.Validate() == OSHostStatus.Ok);
    }

    [HostTest("pal.socket abi rejects invalid endpoint values", HostLane.Fast, 4)]
    private static bool TestRejectsInvalidEndpointValues()
    {
        OSHostStatus zeroPortStatus = OSHostIpv4Endpoint.TryCreate(
            0,
            0,
            0,
            0,
            portHostOrder: 0,
            out OSHostIpv4Endpoint zeroPortEndpoint);
        if (zeroPortStatus != OSHostStatus.InvalidArgument
            || zeroPortEndpoint.IsValid)
        {
            return HostCheck.Result(false);
        }

        OSHostIpv4Endpoint wrongFamily = new OSHostIpv4Endpoint(
            family: 10,
            portNetworkOrder: OSHostSocketNumbers.HostToNetwork16(80),
            address0: 127,
            address1: 0,
            address2: 0,
            address3: 1);
        if (OSHostIpv4Endpoint.Validate(wrongFamily) != OSHostStatus.InvalidArgument)
        {
            return HostCheck.Result(false);
        }

        OSHostSocketWaitOptions invalidSocket = new OSHostSocketWaitOptions(
            new OSHostSocketHandle(0),
            OSHostSocketTimeouts.Immediate,
            OSHostSocketConstants.WaitModeRead,
            flags: 0);
        if (invalidSocket.Validate() != OSHostStatus.InvalidArgument)
        {
            return HostCheck.Result(false);
        }

        OSHostSocketWaitOptions invalidMode = new OSHostSocketWaitOptions(
            new OSHostSocketHandle(1),
            OSHostSocketTimeouts.Immediate,
            mode: 99,
            flags: 0);
        if (invalidMode.Validate() != OSHostStatus.InvalidArgument)
        {
            return HostCheck.Result(false);
        }

        OSHostSocketWaitOptions invalidTimeout = new OSHostSocketWaitOptions(
            new OSHostSocketHandle(1),
            new OSHostTimeout(kind: 99, flags: 0, ticks: 0, tickFrequency: 0),
            OSHostSocketConstants.WaitModeRead,
            flags: 0);
        if (invalidTimeout.Validate() != OSHostStatus.InvalidArgument)
        {
            return HostCheck.Result(false);
        }

        if (OSHostSocketTimeouts.TryFromMilliseconds(-2, tickFrequency: 1_000, out _) != OSHostStatus.InvalidArgument)
        {
            return HostCheck.Result(false);
        }

        if (OSHostSocketTimeouts.TryFromMilliseconds(-1, tickFrequency: 1_000, out OSHostTimeout infinite) != OSHostStatus.Ok
            || infinite.Kind != OSHostSocketConstants.TimeoutKindInfinite
            || !OSHostSocketTimeouts.IsValid(infinite))
        {
            return HostCheck.Result(false);
        }

        if (OSHostSocketTimeouts.TryFromMilliseconds(0, tickFrequency: 1_000, out OSHostTimeout immediate) != OSHostStatus.Ok
            || immediate.Kind != OSHostSocketConstants.TimeoutKindImmediate
            || !OSHostSocketTimeouts.IsValid(immediate))
        {
            return HostCheck.Result(false);
        }

        if (OSHostSocketTimeouts.TryFromMilliseconds(1_500, tickFrequency: 1_000, out OSHostTimeout relative) != OSHostStatus.Ok
            || relative.Kind != OSHostSocketConstants.TimeoutKindRelativeTicks
            || relative.Ticks != 1_500
            || relative.TickFrequency != 1_000)
        {
            return HostCheck.Result(false);
        }

        return HostCheck.Result(OSHostSocketTimeouts.TryFromMilliseconds(long.MaxValue, ulong.MaxValue, out OSHostTimeout clamped) == OSHostStatus.Ok
            && clamped.Ticks == ulong.MaxValue
            && OSHostSocketTimeouts.IsValid(clamped));
    }

    [HostTest("pal.socket abi exposes deterministic status codes", HostLane.Fast, 5)]
    private static bool TestExposesDeterministicStatusCodes()
    {
        if (!HasStatusCode(OSHostStatus.WouldBlock, 7)
            || !HasStatusCode(OSHostStatus.TimedOut, 8)
            || !HasStatusCode(OSHostStatus.ConnectionReset, 11)
            || !HasStatusCode(OSHostStatus.ConnectionRefused, 12)
            || !HasStatusCode(OSHostStatus.AddressInUse, 13)
            || !HasStatusCode(OSHostStatus.AddressNotAvailable, 14)
            || !HasStatusCode(OSHostStatus.NetworkDown, 15)
            || !HasStatusCode(OSHostStatus.TryAgain, 16)
            || !HasStatusCode(OSHostStatus.Closed, 19))
        {
            return HostCheck.Result(false);
        }

        if (OSHostStatusNames.GetConstantName(OSHostStatus.Ok) != "OSHOST_STATUS_OK"
            || OSHostStatusNames.GetConstantName(OSHostStatus.WouldBlock) != "OSHOST_STATUS_WOULD_BLOCK"
            || OSHostStatusNames.GetConstantName(OSHostStatus.TimedOut) != "OSHOST_STATUS_TIMED_OUT"
            || OSHostStatusNames.GetConstantName(OSHostStatus.ConnectionReset) != "OSHOST_STATUS_CONNECTION_RESET"
            || OSHostStatusNames.GetConstantName(OSHostStatus.ConnectionRefused) != "OSHOST_STATUS_CONNECTION_REFUSED"
            || OSHostStatusNames.GetConstantName(OSHostStatus.AddressInUse) != "OSHOST_STATUS_ADDR_IN_USE"
            || OSHostStatusNames.GetConstantName(OSHostStatus.AddressNotAvailable) != "OSHOST_STATUS_ADDR_NOT_AVAILABLE"
            || OSHostStatusNames.GetConstantName(OSHostStatus.NetworkDown) != "OSHOST_STATUS_NETWORK_DOWN"
            || OSHostStatusNames.GetConstantName(OSHostStatus.Closed) != "OSHOST_STATUS_CLOSED"
            || OSHostStatusNames.GetConstantName(999) != OSHostStatusNames.Unknown)
        {
            return HostCheck.Result(false);
        }

        OSHostSocketOperationResult wouldBlock = OSHostSocketOperationResult.FromStatus(
            OSHostStatus.WouldBlock,
            bytesTransferred: 0,
            new OSHostSocketHandle(7));
        if (wouldBlock.Succeeded
            || !wouldBlock.IsRetryable
            || wouldBlock.ValidateStatus() != OSHostStatus.Ok)
        {
            return HostCheck.Result(false);
        }

        OSHostSocketOperationResult ok = OSHostSocketOperationResult.FromStatus(
            OSHostStatus.Ok,
            bytesTransferred: 128,
            new OSHostSocketHandle(7));
        if (!ok.Succeeded
            || ok.IsRetryable
            || ok.BytesTransferred != 128)
        {
            return HostCheck.Result(false);
        }

        OSHostSocketOperationResult invalid = new OSHostSocketOperationResult(
            statusCode: 999,
            flags: 0,
            bytesTransferred: 0,
            socket: new OSHostSocketHandle(7),
            reserved: 0);
        return HostCheck.Result(invalid.ValidateStatus() == OSHostStatus.InvalidArgument);
    }

    [HostTest("pal.socket abi default operation result is not success", HostLane.Fast, 6)]
    private static bool TestDefaultOperationResultIsNotSuccess()
    {
        OSHostSocketOperationResult result = default;
        OSHostSocketOperationResult zeroByteSuccess = OSHostSocketOperationResult.FromStatus(
            OSHostStatus.Ok,
            bytesTransferred: 0,
            new OSHostSocketHandle(7));

        return HostCheck.Result(result.StatusCode == (int)OSHostStatus.Ok
            && !result.Succeeded
            && !result.Socket.IsValid
            && result.BytesTransferred == 0
            && result.ValidateStatus() == OSHostStatus.Ok
            && zeroByteSuccess.Succeeded
            && zeroByteSuccess.BytesTransferred == 0
            && !RuntimeHelpers.IsReferenceOrContainsReferences<OSHostSocketOperationResult>());
    }

    [HostTest("pal.socket options reject unsupported values", HostLane.Fast, 7)]
    private static bool TestRejectsUnsupportedSocketOptions()
    {
        if (!OSHostSocketOptions.IsDefined(OSHostSocketConstants.SocketOptionNoDelay)
            || !OSHostSocketOptions.IsDefined(OSHostSocketConstants.SocketOptionReuseAddress)
            || !OSHostSocketOptions.IsDefined(OSHostSocketConstants.SocketOptionReceiveTimeoutMilliseconds)
            || !OSHostSocketOptions.IsDefined(OSHostSocketConstants.SocketOptionSendTimeoutMilliseconds)
            || !OSHostSocketOptions.IsDefined(OSHostSocketConstants.SocketOptionKeepAlive)
            || OSHostSocketOptions.IsDefined(99))
        {
            return HostCheck.Result(false);
        }

        if (OSHostSocketOptions.Validate(
                OSHostSocketConstants.SocketOptionNoDelay,
                OSHostSocketOptionValue.Boolean(enabled: true)) != OSHostStatus.Ok
            || OSHostSocketOptions.Validate(
                OSHostSocketConstants.SocketOptionKeepAlive,
                OSHostSocketOptionValue.Boolean(enabled: false)) != OSHostStatus.Ok
            || OSHostSocketOptions.Validate(
                OSHostSocketConstants.SocketOptionReceiveTimeoutMilliseconds,
                OSHostSocketOptionValue.TimeoutMilliseconds(-1)) != OSHostStatus.Ok
            || OSHostSocketOptions.Validate(
                OSHostSocketConstants.SocketOptionSendTimeoutMilliseconds,
                OSHostSocketOptionValue.TimeoutMilliseconds(1500)) != OSHostStatus.Ok)
        {
            return HostCheck.Result(false);
        }

        return HostCheck.Result(OSHostSocketOptions.Validate(
                OSHostSocketConstants.SocketOptionNoDelay,
                new OSHostSocketOptionValue(2, reserved: 0)) == OSHostStatus.InvalidArgument
            && OSHostSocketOptions.Validate(
                OSHostSocketConstants.SocketOptionReuseAddress,
                new OSHostSocketOptionValue(1, reserved: 1)) == OSHostStatus.InvalidArgument
            && OSHostSocketOptions.Validate(
                OSHostSocketConstants.SocketOptionReceiveTimeoutMilliseconds,
                OSHostSocketOptionValue.TimeoutMilliseconds(-2)) == OSHostStatus.InvalidArgument
            && OSHostSocketOptions.Validate(
                99,
                OSHostSocketOptionValue.Boolean(enabled: true)) == OSHostStatus.InvalidArgument);
    }

    private static int Offset<T>(string fieldName)
    {
        return (int)Marshal.OffsetOf<T>(fieldName);
    }

    private static bool HasStatusCode(OSHostStatus status, int expected)
    {
        return (int)status == expected;
    }

    private static bool AssertUnmanaged<T>()
        where T : struct
    {
        return !RuntimeHelpers.IsReferenceOrContainsReferences<T>();
    }

    private static bool ContainsReferenceField(Type type, HashSet<Type> seen)
    {
        if (!type.IsValueType)
        {
            return true;
        }

        if (type.IsPrimitive || type.IsEnum || !seen.Add(type))
        {
            return false;
        }

        foreach (FieldInfo field in type.GetFields(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic))
        {
            if (ContainsReferenceField(field.FieldType, seen))
            {
                Console.WriteLine("Reference field: " + type.FullName + "." + field.Name + " type=" + field.FieldType.FullName);
                return true;
            }
        }

        return false;
    }
}
