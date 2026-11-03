using System.Runtime.InteropServices;

namespace OS.Pal;

[StructLayout(LayoutKind.Sequential)]
public readonly struct OSHostWaitResult
{
    public readonly uint Kind;
    public readonly uint SignaledIndex;
    public readonly int StatusCode;
    public readonly uint Flags;
    public readonly OSHostHandle SignaledHandle;
    public OSHostWaitResult(uint kind, uint signaledIndex, int statusCode, uint flags, OSHostHandle signaledHandle)
    {
        Kind = kind;
        SignaledIndex = signaledIndex;
        StatusCode = statusCode;
        Flags = flags;
        SignaledHandle = signaledHandle;
    }
}
