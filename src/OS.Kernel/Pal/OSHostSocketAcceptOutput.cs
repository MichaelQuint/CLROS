using System.Runtime.InteropServices;

namespace OS.Pal;

[StructLayout(LayoutKind.Sequential)]
public readonly struct OSHostSocketAcceptOutput
{
    public readonly OSHostHandle AcceptedSocket;
    public readonly OSHostSocketAddress LocalAddress;
    public readonly OSHostSocketAddress RemoteAddress;
    public OSHostSocketAcceptOutput(OSHostHandle acceptedSocket, OSHostSocketAddress localAddress, OSHostSocketAddress remoteAddress)
    {
        AcceptedSocket = acceptedSocket;
        LocalAddress = localAddress;
        RemoteAddress = remoteAddress;
    }
}
