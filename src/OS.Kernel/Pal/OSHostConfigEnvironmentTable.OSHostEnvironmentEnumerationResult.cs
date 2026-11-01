using OS.Manifest;
using OS.Std;
using OS.Std.Memory;
using OS.Std.Text;

namespace OS.Pal;
public readonly struct OSHostEnvironmentEnumerationResult
{
    public OSHostEnvironmentEnumerationResult(OSHostStatus status, ulong keyBytesWritten, ulong valueBytesWritten, ulong requiredKeyBytes, ulong requiredValueBytes, bool isInitialized = true)
    {
        Status = status;
        KeyBytesWritten = keyBytesWritten;
        ValueBytesWritten = valueBytesWritten;
        RequiredKeyBytes = requiredKeyBytes;
        RequiredValueBytes = requiredValueBytes;
        IsInitialized = isInitialized;
    }

    public OSHostStatus Status { get; }
    public ulong KeyBytesWritten { get; }
    public ulong ValueBytesWritten { get; }
    public ulong RequiredKeyBytes { get; }
    public ulong RequiredValueBytes { get; }
    public bool IsInitialized { get; }
    public bool Succeeded => IsInitialized && Status == OSHostStatus.Ok;

    public static OSHostEnvironmentEnumerationResult FromStatus(OSHostStatus status, ulong requiredKeyBytes = 0, ulong requiredValueBytes = 0)
    {
        return new OSHostEnvironmentEnumerationResult(status, keyBytesWritten: 0, valueBytesWritten: 0, requiredKeyBytes, requiredValueBytes);
    }
}
