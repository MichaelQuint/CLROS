using System.Runtime.InteropServices;

namespace OS.Pal;

[StructLayout(LayoutKind.Sequential)]
public readonly struct OSHostSocketHandle
{
    public readonly ulong Value;
    public OSHostSocketHandle(ulong value)
    {
        Value = value;
    }

    public bool IsValid => Value != 0;

    public OSHostHandle AsHandle()
    {
        return new OSHostHandle(Value);
    }

    public static OSHostSocketHandle FromHandle(OSHostHandle handle)
    {
        return new OSHostSocketHandle(handle.Value);
    }
}
