namespace OS.Pal;

public sealed partial class OSHostSocketTable
{
    private readonly struct SocketOptions
    {
        public readonly OSHostSocketOptionValue NoDelay;
        public readonly OSHostSocketOptionValue ReuseAddress;
        public readonly OSHostSocketOptionValue ReceiveTimeoutMilliseconds;
        public readonly OSHostSocketOptionValue SendTimeoutMilliseconds;
        public readonly OSHostSocketOptionValue KeepAlive;
        public SocketOptions(OSHostSocketOptionValue noDelay, OSHostSocketOptionValue reuseAddress, OSHostSocketOptionValue receiveTimeoutMilliseconds, OSHostSocketOptionValue sendTimeoutMilliseconds, OSHostSocketOptionValue keepAlive)
        {
            NoDelay = noDelay;
            ReuseAddress = reuseAddress;
            ReceiveTimeoutMilliseconds = receiveTimeoutMilliseconds;
            SendTimeoutMilliseconds = sendTimeoutMilliseconds;
            KeepAlive = keepAlive;
        }

        public static SocketOptions Default => new(OSHostSocketOptionValue.Boolean(enabled: false), OSHostSocketOptionValue.Boolean(enabled: false), OSHostSocketOptionValue.TimeoutMilliseconds(-1), OSHostSocketOptionValue.TimeoutMilliseconds(-1), OSHostSocketOptionValue.Boolean(enabled: false));

        public SocketOptions Set(uint optionName, OSHostSocketOptionValue value)
        {
            return optionName switch
            {
                OSHostSocketConstants.SocketOptionNoDelay => new SocketOptions(value, ReuseAddress, ReceiveTimeoutMilliseconds, SendTimeoutMilliseconds, KeepAlive),
                OSHostSocketConstants.SocketOptionReuseAddress => new SocketOptions(NoDelay, value, ReceiveTimeoutMilliseconds, SendTimeoutMilliseconds, KeepAlive),
                OSHostSocketConstants.SocketOptionReceiveTimeoutMilliseconds => new SocketOptions(NoDelay, ReuseAddress, value, SendTimeoutMilliseconds, KeepAlive),
                OSHostSocketConstants.SocketOptionSendTimeoutMilliseconds => new SocketOptions(NoDelay, ReuseAddress, ReceiveTimeoutMilliseconds, value, KeepAlive),
                OSHostSocketConstants.SocketOptionKeepAlive => new SocketOptions(NoDelay, ReuseAddress, ReceiveTimeoutMilliseconds, SendTimeoutMilliseconds, value),
                _ => this
            };
        }

        public OSHostStatus TryGet(uint optionName, out OSHostSocketOptionValue value)
        {
            switch (optionName)
            {
                case OSHostSocketConstants.SocketOptionNoDelay:
                    value = NoDelay;
                    return OSHostStatus.Ok;
                case OSHostSocketConstants.SocketOptionReuseAddress:
                    value = ReuseAddress;
                    return OSHostStatus.Ok;
                case OSHostSocketConstants.SocketOptionReceiveTimeoutMilliseconds:
                    value = ReceiveTimeoutMilliseconds;
                    return OSHostStatus.Ok;
                case OSHostSocketConstants.SocketOptionSendTimeoutMilliseconds:
                    value = SendTimeoutMilliseconds;
                    return OSHostStatus.Ok;
                case OSHostSocketConstants.SocketOptionKeepAlive:
                    value = KeepAlive;
                    return OSHostStatus.Ok;
                default:
                    value = default;
                    return OSHostStatus.InvalidArgument;
            }
        }
    }
}
