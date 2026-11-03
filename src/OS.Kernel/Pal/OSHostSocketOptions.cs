using System.Runtime.InteropServices;

namespace OS.Pal;

public static class OSHostSocketOptions
{
    public static bool IsDefined(uint optionName)
    {
        return optionName is OSHostSocketConstants.SocketOptionNoDelay or OSHostSocketConstants.SocketOptionReuseAddress or OSHostSocketConstants.SocketOptionReceiveTimeoutMilliseconds or OSHostSocketConstants.SocketOptionSendTimeoutMilliseconds or OSHostSocketConstants.SocketOptionKeepAlive;
    }

    public static OSHostStatus Validate(uint optionName, OSHostSocketOptionValue value)
    {
        if (!IsDefined(optionName) || value.Reserved != 0)
        {
            return OSHostStatus.InvalidArgument;
        }

        return optionName switch
        {
            OSHostSocketConstants.SocketOptionNoDelay or OSHostSocketConstants.SocketOptionReuseAddress or OSHostSocketConstants.SocketOptionKeepAlive => ValidateBoolean(value),
            OSHostSocketConstants.SocketOptionReceiveTimeoutMilliseconds or OSHostSocketConstants.SocketOptionSendTimeoutMilliseconds => ValidateTimeoutMilliseconds(value),
            _ => OSHostStatus.InvalidArgument
        };
    }

    private static OSHostStatus ValidateBoolean(OSHostSocketOptionValue value)
    {
        return value.Value is 0 or 1 ? OSHostStatus.Ok : OSHostStatus.InvalidArgument;
    }

    private static OSHostStatus ValidateTimeoutMilliseconds(OSHostSocketOptionValue value)
    {
        return value.Value >= -1 ? OSHostStatus.Ok : OSHostStatus.InvalidArgument;
    }
}
