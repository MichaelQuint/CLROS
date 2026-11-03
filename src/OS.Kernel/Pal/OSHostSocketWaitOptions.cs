using System.Runtime.InteropServices;

namespace OS.Pal;

[StructLayout(LayoutKind.Sequential)]
public readonly struct OSHostSocketWaitOptions
{
    public readonly OSHostSocketHandle Socket;
    public readonly OSHostTimeout Timeout;
    public readonly uint Mode;
    public readonly uint Flags;
    public OSHostSocketWaitOptions(OSHostSocketHandle socket, OSHostTimeout timeout, uint mode, uint flags)
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

        return OSHostSocketWaitModes.IsDefined(Mode) ? OSHostStatus.Ok : OSHostStatus.InvalidArgument;
    }
}
