namespace OS.Pal;

public sealed class OSHostSocketFacade
{
    private readonly OSHostSocketTable table;

    public OSHostSocketFacade(OSHostSocketTable table)
    {
        this.table = table;
    }

    public OSHostSocketCreateResult Create(OSHostSocketCreateOptions options)
    {
        return table.Create(options);
    }

    public OSHostStatus Bind(OSHostSocketBindRequest request)
    {
        if (request.Flags != 0 || request.Reserved != 0)
        {
            return OSHostStatus.InvalidArgument;
        }

        OSHostStatus addressStatus = TryConvertAddress(request.Address, out OSHostIpv4Endpoint endpoint);
        return addressStatus == OSHostStatus.Ok
            ? table.Bind(OSHostSocketHandle.FromHandle(request.Socket), endpoint)
            : addressStatus;
    }

    public OSHostStatus Listen(OSHostSocketListenRequest request)
    {
        if (request.Flags != 0 || request.Reserved != 0 || request.Backlog > int.MaxValue)
        {
            return OSHostStatus.InvalidArgument;
        }

        return table.Listen(OSHostSocketHandle.FromHandle(request.Socket), (int)request.Backlog);
    }

    public OSHostStatus Accept(OSHostSocketAcceptRequest request, out OSHostSocketAcceptOutput output)
    {
        output = default;
        if (request.Flags != 0 || request.Reserved != 0 || !OSHostSocketTimeouts.IsValid(request.Timeout))
        {
            return OSHostStatus.InvalidArgument;
        }

        OSHostSocketAcceptResult accepted = table.Accept(OSHostSocketHandle.FromHandle(request.Listener));
        if (!accepted.Succeeded)
        {
            return accepted.Status;
        }

        output = new OSHostSocketAcceptOutput(
            accepted.Socket.AsHandle(),
            accepted.LocalEndpoint.AsSocketAddress(),
            accepted.RemoteEndpoint.AsSocketAddress());
        return OSHostStatus.Ok;
    }

    public OSHostStatus Connect(OSHostSocketConnectRequest request)
    {
        if (request.Flags != 0 || request.Reserved != 0 || !OSHostSocketTimeouts.IsValid(request.Timeout))
        {
            return OSHostStatus.InvalidArgument;
        }

        OSHostStatus addressStatus = TryConvertAddress(request.RemoteAddress, out OSHostIpv4Endpoint endpoint);
        return addressStatus == OSHostStatus.Ok
            ? table.Connect(OSHostSocketHandle.FromHandle(request.Socket), endpoint)
            : addressStatus;
    }

    public OSHostSocketOperationResult Send(
        OSHostSocketIoRequest request,
        ReadOnlySpan<byte> source)
    {
        if (request.Flags != 0 || request.Reserved != 0 || request.Buffer.Length != (ulong)source.Length)
        {
            return OSHostSocketOperationResult.FromStatus(
                OSHostStatus.InvalidArgument,
                bytesTransferred: 0,
                OSHostSocketHandle.FromHandle(request.Socket));
        }

        return table.Send(OSHostSocketHandle.FromHandle(request.Socket), source);
    }

    public OSHostSocketOperationResult Receive(
        OSHostSocketIoRequest request,
        Span<byte> destination)
    {
        if (request.Flags != 0 || request.Reserved != 0 || request.Buffer.Length != (ulong)destination.Length)
        {
            return OSHostSocketOperationResult.FromStatus(
                OSHostStatus.InvalidArgument,
                bytesTransferred: 0,
                OSHostSocketHandle.FromHandle(request.Socket));
        }

        return table.Receive(OSHostSocketHandle.FromHandle(request.Socket), destination);
    }

    public OSHostSocketPollEntry Poll(OSHostSocketPollEntry request)
    {
        if (request.Flags != 0)
        {
            return new OSHostSocketPollEntry(
                request.Socket,
                request.RequestedEvents,
                observedEvents: OSHostSocketConstants.PollError,
                (int)OSHostStatus.InvalidArgument,
                flags: 0);
        }

        OSHostSocketPollResult result = table.Poll(
            OSHostSocketHandle.FromHandle(request.Socket),
            request.RequestedEvents);
        return new OSHostSocketPollEntry(
            request.Socket,
            request.RequestedEvents,
            result.ObservedEvents,
            (int)result.Status,
            flags: 0);
    }

    public OSHostStatus Shutdown(OSHostSocketShutdownRequest request)
    {
        if (request.Flags != 0 || request.Reserved != 0)
        {
            return OSHostStatus.InvalidArgument;
        }

        return table.Shutdown(OSHostSocketHandle.FromHandle(request.Socket), request.Direction);
    }

    public OSHostStatus SetSocketOption(OSHostSocketOptionRequest request)
    {
        if (request.Flags != 0)
        {
            return OSHostStatus.InvalidArgument;
        }

        return table.SetSocketOption(
            OSHostSocketHandle.FromHandle(request.Socket),
            request.OptionName,
            request.Value);
    }

    public OSHostStatus GetAddress(OSHostSocketAddressQueryRequest request, out OSHostSocketAddress address)
    {
        address = default;
        if (request.Flags != 0 || request.Reserved != 0)
        {
            return OSHostStatus.InvalidArgument;
        }

        if (request.AddressKind == OSHostSocketConstants.AddressKindLocal)
        {
            OSHostStatus status = table.GetLocalEndpoint(
                OSHostSocketHandle.FromHandle(request.Socket),
                out OSHostIpv4Endpoint endpoint);
            if (status == OSHostStatus.Ok)
            {
                address = endpoint.AsSocketAddress();
            }

            return status;
        }

        if (request.AddressKind == OSHostSocketConstants.AddressKindRemote)
        {
            OSHostStatus status = table.GetRemoteEndpoint(
                OSHostSocketHandle.FromHandle(request.Socket),
                out OSHostIpv4Endpoint endpoint);
            if (status == OSHostStatus.Ok)
            {
                address = endpoint.AsSocketAddress();
            }

            return status;
        }

        return OSHostStatus.InvalidArgument;
    }

    public OSHostStatus Close(OSHostHandle socket)
    {
        return table.Close(OSHostSocketHandle.FromHandle(socket));
    }

    private static OSHostStatus TryConvertAddress(
        OSHostSocketAddress address,
        out OSHostIpv4Endpoint endpoint)
    {
        endpoint = default;
        if (address.Family != OSHostSocketConstants.AddressFamilyIpv4
            || address.FlowInfo != 0
            || address.ScopeId != 0
            || address.AddressHigh != 0
            || address.AddressLow > uint.MaxValue)
        {
            return OSHostStatus.InvalidArgument;
        }

        uint ipv4 = (uint)address.AddressLow;
        endpoint = new OSHostIpv4Endpoint(
            address.Family,
            address.PortNetworkOrder,
            (byte)(ipv4 >> 24),
            (byte)(ipv4 >> 16),
            (byte)(ipv4 >> 8),
            (byte)ipv4);
        return OSHostIpv4Endpoint.Validate(endpoint);
    }
}
