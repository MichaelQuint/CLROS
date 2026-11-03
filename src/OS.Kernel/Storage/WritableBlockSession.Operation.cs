using OS.Drivers;

namespace OS.Storage;

public static unsafe partial class WritableBlockSession
{
    public enum Operation : byte
    {
        Write, Flush, Read
    }
}
