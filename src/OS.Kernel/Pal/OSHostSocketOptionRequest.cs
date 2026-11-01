using System.Runtime.InteropServices;

namespace OS.Pal;
[StructLayout(LayoutKind.Sequential)]
public readonly struct OSHostSocketOptionRequest
{
    public readonly OSHostHandle Socket;
    public readonly uint OptionName;
    public readonly uint Flags;
    public readonly OSHostSocketOptionValue Value;
    public OSHostSocketOptionRequest(OSHostHandle socket, uint optionName, uint flags, OSHostSocketOptionValue value)
    {
        Socket = socket;
        OptionName = optionName;
        Flags = flags;
        Value = value;
    }
}
