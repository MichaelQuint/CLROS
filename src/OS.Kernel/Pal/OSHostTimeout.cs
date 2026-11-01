using System.Runtime.InteropServices;

namespace OS.Pal;
[StructLayout(LayoutKind.Sequential)]
public readonly struct OSHostTimeout
{
    public readonly uint Kind;
    public readonly uint Flags;
    public readonly ulong Ticks;
    public readonly ulong TickFrequency;
    public OSHostTimeout(uint kind, uint flags, ulong ticks, ulong tickFrequency)
    {
        Kind = kind;
        Flags = flags;
        Ticks = ticks;
        TickFrequency = tickFrequency;
    }
}
