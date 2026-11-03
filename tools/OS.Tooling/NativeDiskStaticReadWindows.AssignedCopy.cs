namespace OS.Tooling;

public static partial class NativeDiskStaticReadWindows
{
    public readonly record struct AssignedCopy(ReadWindow Window, string Path, WebRootDiskInventory.Sector Sector, SectorCopy Copy);
}
