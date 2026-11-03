using OS.Std.Binary;

namespace OS.Drivers;

public struct XApicSetupResult
{
    public XApicSetupStatus Status;
    public uint LocalVersion, IoVersion, MaskedLocalEntries, MaskedIoEntries, TaskPriority, Spurious;
    public bool LegacyPicMasked;
}
