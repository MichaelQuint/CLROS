using System.Runtime.InteropServices;

namespace OS.Pal;

[StructLayout(LayoutKind.Sequential)]
public readonly struct OSHostEventCreateOptions
{
    public readonly uint Kind;
    public readonly uint InitiallySignaled;
    public readonly uint Flags;
    public readonly uint Reserved;
    public OSHostEventCreateOptions(uint kind, uint initiallySignaled, uint flags, uint reserved)
    {
        Kind = kind;
        InitiallySignaled = initiallySignaled;
        Flags = flags;
        Reserved = reserved;
    }
}
