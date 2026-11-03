using System.Runtime.InteropServices;

namespace OS.Pal;

[StructLayout(LayoutKind.Sequential)]
public readonly struct OSHostThreadStart
{
    public readonly ulong EntryPoint;
    public readonly ulong Context;
    public OSHostThreadStart(ulong entryPoint, ulong context)
    {
        EntryPoint = entryPoint;
        Context = context;
    }
}
