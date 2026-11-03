using System.Runtime.InteropServices;

namespace OS.Pal;

[StructLayout(LayoutKind.Sequential)]
public readonly struct OSHostSocketPollEntry
{
    public readonly OSHostHandle Socket;
    public readonly uint RequestedEvents;
    public readonly uint ObservedEvents;
    public readonly int StatusCode;
    public readonly uint Flags;
    public OSHostSocketPollEntry(OSHostHandle socket, uint requestedEvents, uint observedEvents, int statusCode, uint flags)
    {
        Socket = socket;
        RequestedEvents = requestedEvents;
        ObservedEvents = observedEvents;
        StatusCode = statusCode;
        Flags = flags;
    }
}
