using OS.Manifest;
using OS.Std;
using OS.Std.Memory;
using OS.Std.Text;

namespace OS.Pal;

public readonly struct OSHostConfigEnvironmentEntry
{
    public OSHostConfigEnvironmentEntry(ReadOnlyMemory<byte> keyUtf8, ReadOnlyMemory<byte> valueUtf8)
    {
        KeyUtf8 = keyUtf8;
        ValueUtf8 = valueUtf8;
    }

    public ReadOnlyMemory<byte> KeyUtf8
    {
        get;
    }
    public ReadOnlyMemory<byte> ValueUtf8
    {
        get;
    }
}
