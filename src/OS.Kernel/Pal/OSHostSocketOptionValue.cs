using System.Runtime.InteropServices;

namespace OS.Pal;

[StructLayout(LayoutKind.Sequential)]
public readonly struct OSHostSocketOptionValue
{
    public readonly long Value;
    public readonly ulong Reserved;
    public OSHostSocketOptionValue(long value, ulong reserved)
    {
        Value = value;
        Reserved = reserved;
    }

    public bool IsBooleanEnabled => Value == 1;

    public static OSHostSocketOptionValue Boolean(bool enabled)
    {
        return new OSHostSocketOptionValue(enabled ? 1 : 0, reserved: 0);
    }

    public static OSHostSocketOptionValue TimeoutMilliseconds(long milliseconds)
    {
        return new OSHostSocketOptionValue(milliseconds, reserved: 0);
    }
}
