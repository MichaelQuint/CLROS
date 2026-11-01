using System.Runtime.InteropServices;

namespace OS.Pal;
[StructLayout(LayoutKind.Sequential)]
public readonly struct OSHostSocketAddressQueryRequest
{
    public readonly OSHostHandle Socket;
    public readonly uint AddressKind;
    public readonly uint Flags;
    public readonly ulong Reserved;
    public OSHostSocketAddressQueryRequest(OSHostHandle socket, uint addressKind, uint flags, ulong reserved)
    {
        Socket = socket;
        AddressKind = addressKind;
        Flags = flags;
        Reserved = reserved;
    }
}
