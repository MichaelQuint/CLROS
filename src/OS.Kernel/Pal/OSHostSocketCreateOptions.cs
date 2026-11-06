using System.Runtime.InteropServices;

namespace OS.Pal;

[StructLayout(LayoutKind.Sequential)]
public readonly struct OSHostSocketCreateOptions
{
    public readonly uint Domain;
    public readonly uint Type;
    public readonly uint Protocol;
    public readonly uint Flags;
    public readonly uint Version;
    public readonly uint Reserved;
    public OSHostSocketCreateOptions(uint domain, uint type, uint protocol, uint flags, uint version, uint reserved)
    {
        Domain = domain;
        Type = type;
        Protocol = protocol;
        Flags = flags;
        Version = version;
        Reserved = reserved;
    }
}
