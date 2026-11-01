using OS.Std.Binary;

namespace OS.Drivers;
public struct MsixProgrammingResult
{
    public MsixStatus Status;
    public uint MaskedEntries, PendingWords, MessageAddress, MessageData;
    public ushort Control, ConfigVector;
}
