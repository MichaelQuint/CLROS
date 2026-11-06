using System.Runtime.InteropServices;

namespace OS.Pal;

[StructLayout(LayoutKind.Sequential)]
public readonly struct OSHostLogRecord
{
    public readonly uint Level;
    public readonly uint Flags;
    public readonly OSHostByteSpan Subsystem;
    public readonly OSHostByteSpan Message;
    public OSHostLogRecord(uint level, uint flags, OSHostByteSpan subsystem, OSHostByteSpan message)
    {
        Level = level;
        Flags = flags;
        Subsystem = subsystem;
        Message = message;
    }
}
