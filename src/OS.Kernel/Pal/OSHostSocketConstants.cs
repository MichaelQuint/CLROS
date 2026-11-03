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
