using System.Runtime.InteropServices;

namespace OS.Pal;
[StructLayout(LayoutKind.Sequential)]
public readonly struct OSHostSocketOperationResult
{
    public readonly int StatusCode;
    public readonly uint Flags;
    public readonly ulong BytesTransferred;
    public readonly OSHostSocketHandle Socket;
    public readonly ulong Reserved;
    public OSHostSocketOperationResult(int statusCode, uint flags, ulong bytesTransferred, OSHostSocketHandle socket, ulong reserved)
    {
        StatusCode = statusCode;
        Flags = flags;
        BytesTransferred = bytesTransferred;
        Socket = socket;
        Reserved = reserved;
    }

    public bool Succeeded => StatusCode == (int)OSHostStatus.Ok && Socket.IsValid;
    public bool IsRetryable => StatusCode == (int)OSHostStatus.WouldBlock || StatusCode == (int)OSHostStatus.TimedOut || StatusCode == (int)OSHostStatus.Interrupted || StatusCode == (int)OSHostStatus.TryAgain;

    public OSHostStatus ValidateStatus()
    {
        return OSHostStatusNames.IsDefined(StatusCode) ? OSHostStatus.Ok : OSHostStatus.InvalidArgument;
    }

    public static OSHostSocketOperationResult FromStatus(OSHostStatus status, ulong bytesTransferred, OSHostSocketHandle socket, uint flags = 0)
    {
        return new OSHostSocketOperationResult((int)status, flags, bytesTransferred, socket, reserved: 0);
    }
}
